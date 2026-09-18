namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The well-formedness checks content must pass before anything reads it
/// (§5.4–§5.7). The relations are validated first because they are the
/// predicates everything after them is checked against; the enchantments next,
/// because the weapons name them; the weapons last. Every failure throws a
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

    public const string RuleDuplicateId = "an id is declared more than once";
    public const string RuleZeroTrigger = "a trigger cost is never zero";
    public const string RuleTriggerXorApplies = "an entry quotes a flat trigger cost or applies a status, never both";
    public const string RuleApplierNamesStatus = "a status applier names the status it applies";
    public const string RuleElementNamesType = "an elemental entry names the damage type it carries";
    public const string RuleOpposition = "damage-type opposition must be symmetric and total";

    public const string RuleEnchantmentsExcluded = "a weapon's enchantments never include two that exclude each other";

    public const string RuleForgedNotAllowed = "every forged spread must be allowed";
    public const string RuleForgedStackCount = "a forged stack count is at least one";
    public const string RuleMaxForged = "nothing is forged past MaxForged";
    public const string RuleTwoForgedAxes = "no weapon is forged with fewer than two modifiers";
    public const string RuleCasterForged = "a caster is forged Resonant x1 and nothing else";
    public const string RuleCasterInnate = "a caster carries exactly one innate enchantment";
    public const string RuleWandShape = "a wand carries an area shape and nothing else does";
    public const string RuleWandNoStatusApplier = "a wand carries no status-applying enchantment: an area cast names no target to land it on";
    public const string RuleRoleOnMartialOnly = "only a martial variant carries a role";
    public const string RuleVariantRoles = "a martial class has four variants, one per role";
    public const string RuleClassBaseline = "a class baseline is its signature plus a second modifier";
    public const string RuleVariantDelta = "a variant is its class baseline plus exactly one added modifier type";
    public const string RuleEfficiencyAddsLight = "an Efficiency variant adds Light x1";
    public const string RulePurityDeepensBaseline = "a Purity variant deepens the class signature";
    public const string RuleForgedOnlyUnused = "every forgedOnly id is forged on at least one weapon";

    /// <summary>
    /// The "class feature" column of the §1.2 table (phase-1-modifiers.md:16-25):
    /// the modifier a class is named for, which every variant of the class
    /// carries and a Purity variant deepens. Compiled, like <see cref="Weapon.KindOf"/>,
    /// because a class is code and so is what it is for. Casters are absent:
    /// they vary by effect and shape rather than by role and are never checked
    /// as variants.
    /// </summary>
    private static readonly IReadOnlyDictionary<WeaponClass, ModifierType> Signature = new Dictionary<WeaponClass, ModifierType>
    {
        [WeaponClass.Dagger] = ModifierType.CritWindow,
        [WeaponClass.Sword] = ModifierType.Block,
        [WeaponClass.Spear] = ModifierType.Brace,
        [WeaponClass.Axe] = ModifierType.Cleave,
        [WeaponClass.Ranged] = ModifierType.Longshot,
        [WeaponClass.Throwing] = ModifierType.Charges,
    };

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

    /// <summary>
    /// The §5.7 checks that need the entries alone: ids are unique, no trigger
    /// cost is zero (no enchantment fires free, ever), an entry quotes a flat
    /// trigger cost or applies a status and never both (a status applier prices
    /// off its level count, so a flat cost would decay into free), a status
    /// applier names its status, and an elemental entry names its type.
    /// <see cref="EnchantmentCatalogue"/> runs this itself as well.
    /// </summary>
    public static void ValidateEnchantments(EnchantmentsData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in data.Enchantments)
        {
            if (!seen.Add(e.Id))
                throw new ContentException(e.Id, RuleDuplicateId);
            if (e.Trigger is <= 0)
                throw new ContentException(e.Id, RuleZeroTrigger, $"trigger {e.Trigger}");
            if ((e.Trigger != null) == (e.Applies != null))
                throw new ContentException(e.Id, RuleTriggerXorApplies,
                    e.Trigger != null ? "quotes a trigger and applies a status" : "quotes no trigger and applies nothing");
            if (e.Effect == EffectKind.ApplyStatus && e.Applies == null)
                throw new ContentException(e.Id, RuleApplierNamesStatus);
            if (e.Effect == EffectKind.ElementalDamage && e.DamageType is null or DamageType.None)
                throw new ContentException(e.Id, RuleElementNamesType);
        }
    }

    /// <summary>
    /// §1.4's chart as content (S:278): four types, two pairs, every type opposed
    /// by exactly one. Each <see cref="DamageType"/> other than None is carried by
    /// exactly one elemental entry, and the exclusion groups of
    /// <paramref name="restricted"/> oppose every element to exactly one other,
    /// both ways. Returns the chart it validated — each type to the one that
    /// opposes it — for the catalogue to answer the type-chart step with.
    /// </summary>
    public static IReadOnlyDictionary<DamageType, DamageType> ValidateOpposition(EnchantmentsData data, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(restricted);

        var elements = data.Enchantments.Where(e => e.Effect == EffectKind.ElementalDamage).ToList();
        var byType = new Dictionary<DamageType, EnchantmentDef>();
        foreach (var e in elements)
        {
            var type = e.DamageType ?? DamageType.None;
            if (type == DamageType.None)
                throw new ContentException(e.Id, RuleElementNamesType);
            if (!byType.TryAdd(type, e))
                throw new ContentException(e.Id, RuleOpposition, $"{type} is carried by '{byType[type].Id}' as well");
        }
        foreach (var type in Enum.GetValues<DamageType>())
            if (type != DamageType.None && !byType.ContainsKey(type))
                throw new ContentException(type.ToString(), RuleOpposition, "no innate enchantment carries it");

        var excludes = Closure(restricted.Excludes);
        var byId = elements.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var chart = new Dictionary<DamageType, DamageType>();
        foreach (var e in elements)
        {
            var opposed = excludes.TryGetValue(e.Id, out var set) ? set.Where(byId.ContainsKey).ToList() : new List<string>();
            if (opposed.Count != 1)
                throw new ContentException(e.Id, RuleOpposition, $"opposed by {opposed.Count} elements, not one");
            if (!excludes.TryGetValue(opposed[0], out var back) || !back.Contains(e.Id))
                throw new ContentException(e.Id, RuleOpposition, $"'{opposed[0]}' does not oppose it back");
            chart[e.DamageType!.Value] = byId[opposed[0]].DamageType!.Value;
        }
        return chart;
    }

    /// <summary>
    /// The §5.6 checks on <c>weapons.json</c> (S:231-240): ids are unique; every
    /// enchantment a weapon names resolves; every forged stack count is at least
    /// one; every forged modifier is allowed
    /// beside the rest of its spread — the state every later roll deepens
    /// from, asked of <see cref="ModifierRules.Allowed"/> with the whole spread
    /// present, because a from-zero build-up would refuse the forge's own
    /// <c>Charges</c>, and the forge is exactly what <c>forgedOnly</c> reserves
    /// the first stack for; nothing is forged past <see cref="ModifierRules.MaxForged"/>;
    /// a martial weapon is forged on at least two modifiers and a caster on
    /// <c>Resonant x1</c> alone beside its one innate enchantment (a staff's
    /// fixed status effect; a wand's element, supplied at instantiation); only
    /// wands carry a shape, no wand carries a status-applying enchantment (an
    /// area cast is aimed at a point and names no target to land one on), and
    /// only martial variants carry a role; each martial class
    /// fields four variants, one per role, on a two-modifier baseline, every
    /// variant adding exactly one modifier type to it (Efficiency <c>Light x1</c>,
    /// Purity the class signature again, Control and Support a new one); no
    /// weapon arrives with two enchantments that exclude each other — opposed
    /// types cannot share a weapon (§1.4), Flaming and Cold excluding each
    /// other exactly as Push and Drag do, off the same groups, while
    /// non-opposing types stack freely; and every <c>forgedOnly</c> id is
    /// forged somewhere, or it could never exist. Unique derivation is checked
    /// with the uniques.
    /// </summary>
    public static void ValidateWeapons(WeaponsData data, ModifierRules rules, RestrictedData restricted, EnchantmentCatalogue enchantments)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in data.Weapons)
            if (!seen.Add(def.Id))
                throw new ContentException(def.Id, RuleDuplicateId);

        var excludes = Closure(restricted.Excludes);
        foreach (var def in data.Weapons)
        {
            var spread = Spread(def);
            var kind = Weapon.KindOf(def.Class);

            foreach (var reference in def.Enchantments)
                if (!enchantments.TryGet(reference.Id, out _))
                    throw new ContentException(def.Id, RuleUnknownId, reference.Id);

            // One hit cannot be two contradictory things: an entry's enchantments
            // never include a pair the relations exclude, in attachment order so
            // the pair reported is the same one every run.
            for (int i = 0; i < def.Enchantments.Count; i++)
                for (int j = i + 1; j < def.Enchantments.Count; j++)
                {
                    string a = def.Enchantments[i].Id, b = def.Enchantments[j].Id;
                    if (excludes.TryGetValue(a, out var excludedByA) && excludedByA.Contains(b))
                        throw new ContentException(def.Id, RuleEnchantmentsExcluded, $"'{a}' and '{b}'");
                }

            foreach (var (t, n) in spread.Entries)
            {
                if (!rules.Allowed(t, kind, spread))
                    throw new ContentException(def.Id, RuleForgedNotAllowed, $"{t} x{n} on a {kind} weapon beside {spread}");
                if (n > rules.MaxForged(t))
                    throw new ContentException(def.Id, RuleMaxForged, $"{t} x{n}, MaxForged {rules.MaxForged(t)}");
            }

            if (kind == WeaponKind.Caster)
            {
                if (!spread.Equals(ModifierSet.Of((ModifierType.Resonant, 1))))
                    throw new ContentException(def.Id, RuleCasterForged, spread.ToString());
                if (def.Role != null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, def.Role.ToString());
                if ((def.Class == WeaponClass.Wand) != (def.Shape != null))
                    throw new ContentException(def.Id, RuleWandShape);
                ValidateCasterInnate(def, enchantments);
            }
            else
            {
                if (spread.Entries.Count() < 2)
                    throw new ContentException(def.Id, RuleTwoForgedAxes, spread.ToString());
                if (def.Shape != null)
                    throw new ContentException(def.Id, RuleWandShape);
                if (!def.Unique && def.Role == null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, "a martial variant carries no role");
            }
        }

        foreach (var cls in Enum.GetValues<WeaponClass>())
        {
            if (Weapon.KindOf(cls) == WeaponKind.Caster)
                continue;   // casters vary by effect and shape, not by role
            var variants = data.Weapons.Where(w => w.Class == cls && !w.Unique).ToList();
            if (variants.Count == 0)
                continue;   // a class the file leaves out is not malformed, only absent
            ValidateVariants(cls, variants);
        }

        foreach (var id in restricted.ForgedOnly)
            if (ModifierRules.TryParseId(id, out var t) && !data.Weapons.Any(w => w.Forged.GetValueOrDefault(t) > 0))
                throw new ContentException(id, RuleForgedOnlyUnused);
    }

    private static void ValidateCasterInnate(WeaponDef def, EnchantmentCatalogue enchantments)
    {
        var attached = def.Enchantments.Select(r => enchantments[r.Id]).ToList();
        if (def.Class == WeaponClass.Staff)
        {
            // Fixed by variant: the first enchantment is the effect it casts.
            if (attached.Count == 0 || attached[0].Effect != EffectKind.ApplyStatus || attached[0].Applies == null)
                throw new ContentException(def.Id, RuleCasterInnate, "a staff's first enchantment is the status it casts");
            if (attached[0].Targets == TargetSide.Any)
                throw new ContentException(def.Id, RuleCasterInnate, "a staff's effect targets allies or enemies, not either");
            if (!def.Unique && attached.Count != 1)
                throw new ContentException(def.Id, RuleCasterInnate, $"{attached.Count} enchantments on a variant");
        }
        else
        {
            // A wand's innate is a damage type: rolled with the drop, so a variant
            // lists none and the caller supplies it; a unique may fix its element.
            if (!def.Unique && attached.Count != 0)
                throw new ContentException(def.Id, RuleCasterInnate, "a wand variant's element is supplied at instantiation");
            if (attached.Count > 0 && attached[0].Effect != EffectKind.ElementalDamage)
                throw new ContentException(def.Id, RuleCasterInnate, "a wand's first enchantment is its element");
            // An area cast is aimed at a point and names the caster as its own
            // target (the Cast payload's ruling), so a status-applying cast
            // entry — a staff's kind — would land its status on the caster.
            // Refused, wherever it sits in the list, until Phase 3 decides what
            // one means on a wand: every caught actor, or nothing. Hit-side
            // kinds (a lingering element) fire on the hits and are unaffected.
            var applier = attached.FirstOrDefault(e => e.Effect == EffectKind.ApplyStatus);
            if (applier != null)
                throw new ContentException(def.Id, RuleWandNoStatusApplier, $"'{applier.Id}' applies {applier.Applies}");
        }
    }

    private static void ValidateVariants(WeaponClass cls, List<WeaponDef> variants)
    {
        foreach (var role in Enum.GetValues<VariantRole>())
        {
            int count = variants.Count(v => v.Role == role);
            if (count != 1)
                throw new ContentException(cls.ToString(), RuleVariantRoles, $"{count} {role} variants");
        }

        // The baseline is what every variant of the class carries: the per-type
        // minimum over the four, which leaves each variant's own addition out.
        // Two modifiers, one of them the class signature.
        var baseline = new Dictionary<ModifierType, int>();
        foreach (var t in Enum.GetValues<ModifierType>())
        {
            int min = variants.Min(v => v.Forged.GetValueOrDefault(t));
            if (min > 0)
                baseline[t] = min;
        }
        var signature = Signature[cls];
        if (baseline.Count != 2 || !baseline.ContainsKey(signature))
            throw new ContentException(cls.ToString(), RuleClassBaseline,
                $"baseline is {string.Join(", ", baseline.Select(b => $"{b.Key} x{b.Value}"))}, signature {signature}");

        foreach (var v in variants)
        {
            var added = v.Forged
                .Where(f => f.Value > baseline.GetValueOrDefault(f.Key))
                .Select(f => f.Key)
                .ToList();
            if (added.Count != 1)
                throw new ContentException(v.Id, RuleVariantDelta, $"adds {added.Count} modifier types to the {cls} baseline");

            var t = added[0];
            switch (v.Role)
            {
                case VariantRole.Efficiency:
                    if (t != ModifierType.Light || v.Forged[t] != 1)
                        throw new ContentException(v.Id, RuleEfficiencyAddsLight, $"adds {t} x{v.Forged[t]}");
                    break;
                case VariantRole.Purity:
                    if (t != signature)
                        throw new ContentException(v.Id, RulePurityDeepensBaseline, $"adds {t}, not the {cls} signature {signature}");
                    break;
                default:
                    if (baseline.ContainsKey(t))
                        throw new ContentException(v.Id, RuleVariantDelta, $"deepens {t} instead of adding a modifier");
                    break;
            }
        }
    }

    /// <summary>
    /// A def's forged spread as a set, unclamped: the forge is bounded by
    /// MaxForged above, never by the acquisition cap. A zero or negative count
    /// is refused here, naming the entry, before <see cref="ModifierSet.Of"/>
    /// would refuse it as an argument: invalid content aborts as content
    /// (§5.4). The compiled defaults cannot carry one; a file can. Walked in
    /// enum order so the count reported is the same one every run.
    /// </summary>
    private static ModifierSet Spread(WeaponDef def)
    {
        foreach (var (t, n) in def.Forged.OrderBy(kv => kv.Key))
            if (n <= 0)
                throw new ContentException(def.Id, RuleForgedStackCount, $"{t} x{n}");
        return ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
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
