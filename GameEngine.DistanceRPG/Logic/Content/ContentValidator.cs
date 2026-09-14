namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The well-formedness checks content must pass before anything reads it
/// (§5.4, §5.5). The relations are validated first because they are the
/// predicates everything after them is checked against. Every failure throws a
/// <see cref="ContentException"/> naming the entry and the rule; the first
/// failure aborts, in a fixed check order, so the report is deterministic.
/// </summary>
public static class ContentValidator
{
    public const string RuleUnknownId = "id does not resolve to a modifier or an enchantment";
    public const string RuleModifierRelationIds = "a modifier relation may only name modifier ids";
    public const string RuleKindValue = "kind must be melee, ranged or caster";
    public const string RuleSelfExclusion = "an id may not exclude itself";
    public const string RuleExcludesSymmetric = "excludes must close symmetrically";
    public const string RuleRequiresAcyclic = "requires must be acyclic";
    public const string RuleRequiresAndExcludes = "an id may not both require and exclude the same id";
    public const string RuleRequiresConflict = "an id may not require two ids that exclude each other";

    /// <summary>
    /// The §5.5 checks on <c>restricted.json</c>: every id resolves, a modifier
    /// relation names only modifiers, no id excludes itself, excludes closes
    /// symmetrically, requires is acyclic, and nothing both requires and excludes
    /// the same id. <paramref name="knownIds"/> is every modifier and enchantment
    /// id content may name. (The "every forgedOnly id is forged on at least one
    /// weapon" check needs the weapon list and lives with the weapon checks.)
    /// </summary>
    public static void ValidateRestricted(RestrictedData data, IReadOnlySet<string> knownIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownIds);

        // Every id resolves to a real modifier or enchantment: a typo in a
        // relation is a rule that silently does not apply.
        // Dictionaries are walked in ordinal key order so the first failure
        // reported is the same one every run — a report, not a race.
        foreach (var group in data.Excludes)
            RequireKnown(group, knownIds);
        foreach (var (id, needs) in data.Requires.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            RequireKnown([id], knownIds);
            RequireKnown(needs, knownIds);
        }
        foreach (var (id, kind) in data.Kind.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            RequireKnown([id], knownIds);
            if (!RestrictedData.TryParseKind(kind, out _))
                throw new ContentException(id, RuleKindValue);
        }
        RequireKnown(data.ForgedOnly, knownIds);
        RequireKnown(data.NeverRolled, knownIds);

        // A relation keyed by a modifier names only modifiers; see the method.
        ValidateModifierRelations(data);

        // No id excludes itself: a group naming an id twice would.
        foreach (var group in data.Excludes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in group)
                if (!seen.Add(id))
                    throw new ContentException(id, RuleSelfExclusion);
        }

        // Excludes closes symmetrically. Expanding groups guarantees it; the check
        // stays so the invariant outlives any future loader that takes pairs directly.
        var excludes = Closure(data.Excludes);
        foreach (var (a, others) in excludes)
            foreach (var b in others)
                if (!excludes.TryGetValue(b, out var back) || !back.Contains(a))
                    throw new ContentException(a, RuleExcludesSymmetric);

        // Requires is acyclic, and nothing both requires and excludes the same id
        // — directly or through a prerequisite's own prerequisites — because
        // that id could never legally exist.
        foreach (var id in data.Requires.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var prerequisites = Prerequisites(id, data.Requires);
            if (excludes.TryGetValue(id, out var excludedById))
                foreach (var p in prerequisites)
                    if (excludedById.Contains(p))
                        throw new ContentException(id, RuleRequiresAndExcludes);
            foreach (var p in prerequisites)
                if (excludes.TryGetValue(p, out var excludedByP) && prerequisites.Any(excludedByP.Contains))
                    throw new ContentException(id, RuleRequiresConflict);
        }
    }

    /// <summary>
    /// A relation keyed by a modifier names only modifiers: a <c>requires</c> row
    /// keyed by one, and an exclusion group containing one. <see cref="ModifierRules.Allowed"/>
    /// sees a weapon's modifiers and nothing else, so a modifier requiring or
    /// excluding an enchantment id could only be dropped on the way in — the
    /// rule that silently does not apply, which the id check exists to prevent.
    /// Relations keyed by an enchantment are free to name enchantments; they are
    /// read beside the enchantment catalogue. The entry named is the modifier.
    /// <see cref="ModifierRules"/> runs this itself as well, so the rules can
    /// never be built around a dropped relation.
    /// </summary>
    public static void ValidateModifierRelations(RestrictedData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var isModifier = (string id) => ModifierRules.ModifierIds.Contains(id);

        foreach (var group in data.Excludes)
        {
            var modifier = group.FirstOrDefault(isModifier);
            if (modifier != null && !group.All(isModifier))
                throw new ContentException(modifier, RuleModifierRelationIds);
        }

        foreach (var (id, needs) in data.Requires.OrderBy(r => r.Key, StringComparer.Ordinal))
            if (isModifier(id) && !needs.All(isModifier))
                throw new ContentException(id, RuleModifierRelationIds);
    }

    private static void RequireKnown(IEnumerable<string> ids, IReadOnlySet<string> knownIds)
    {
        foreach (var id in ids)
            if (!knownIds.Contains(id))
                throw new ContentException(id, RuleUnknownId);
    }

    /// <summary>Directed pairs from the groups, over string ids, for the checks above.</summary>
    private static Dictionary<string, HashSet<string>> Closure(IReadOnlyList<string[]> groups)
    {
        var closure = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var group in groups)
            foreach (var a in group)
                foreach (var b in group)
                {
                    if (a == b)
                        continue;
                    if (!closure.TryGetValue(a, out var excluded))
                        closure[a] = excluded = new HashSet<string>(StringComparer.Ordinal);
                    excluded.Add(b);
                }
        return closure;
    }

    /// <summary>Everything <paramref name="root"/> transitively requires; throws on a cycle, naming the id that closes it.</summary>
    private static HashSet<string> Prerequisites(string root, IReadOnlyDictionary<string, string[]> requires)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var onPath = new HashSet<string>(StringComparer.Ordinal) { root };
        Visit(root);
        return result;

        void Visit(string id)
        {
            if (!requires.TryGetValue(id, out var needs))
                return;
            foreach (var need in needs)
            {
                if (!onPath.Add(need))
                    throw new ContentException(need, RuleRequiresAcyclic);
                result.Add(need);
                Visit(need);
                onPath.Remove(need);
            }
        }
    }
}
