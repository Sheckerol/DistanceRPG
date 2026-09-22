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
    public const string RuleLingerNamesElement = "a lingering element names an element that exists and the status it leaves";
    public const string RuleUniqueNeverRolled = "unique entries appear in neverRolled, and neverRolled names only unique entries";

    public const string RuleEnchantmentsExcluded = "a weapon's enchantments never include two that exclude each other";
    public const string RuleEnchantmentDependency = "an enchantment with a dependency appears only after its prerequisites on the same weapon";
    public const string RuleOverhealBesideHealing = "an enchantment that converts surplus healing appears only beside a source of it";
    public const string RuleDependencyOnCurrency = "an enchantment that depends on another appears only on a weapon forged with a currency modifier";
    public const string RuleUniqueSoulOnUnique = "a unique enchantment arrives only on a unique weapon";
    public const string RuleArrivingEnchantments = "no weapon arrives carrying more than three enchantments";

    public const string RuleUniqueDerivedFrom = "a unique names the variant it derives from, and nothing else does";
    public const string RuleUniqueStatline = "a unique keeps its variant's class, statline and shape";
    public const string RuleUniqueDerivation = "a unique is its variant with exactly one modifier it already carries raised to x3 and nothing else changed";
    public const string RuleLightUniqueShape = "a Light-forged unique carries its signature at x2 or x3 and nothing else changed, with 5 - n enchantments, one of them unique or at tier 3";
    public const string RuleCasterUniqueEnchantments = "a caster unique keeps its variant's spread and differs from it only in its enchantments";

    /// <summary>What a unique raises its one modifier to: the forge limit, and why nothing is forged past it (§1.5).</summary>
    private const int UniqueRaise = 3;

    /// <summary>A Light-forged unique trades forged depth for souls: <c>signature xn</c> buys <c>5 - n</c> enchantments (§1.5).</summary>
    private const int LightShapeSlots = 5;

    /// <summary>The floor of the Light shape: at x1 the weapon is the variant it derives from.</summary>
    private const int LightShapeFloor = 2;

    /// <summary>
    /// The most enchantments any weapon arrives carrying (§1.5): the Light
    /// shape at its floor, which spends the last stack there is to spend. The
    /// enchanter builds on to five from there (§6.2); nothing drops with more.
    /// </summary>
    private const int MostArrivingEnchantments = LightShapeSlots - LightShapeFloor;

    /// <summary>The tier that makes a catalogue enchantment an artifact's: the only place a drop starts above tier 1.</summary>
    private const int ArtifactTier = 3;

    /// <summary>
    /// The currency group (§1.1): the two discounts, forge-limited to x1 and
    /// excluding each other. The combo platform of §1.5 — the only weapons an
    /// enchantment may depend on another existing on, because they are the
    /// only ones that can pay for two triggers (Light earns the mana, Resonant
    /// spends less of it). Compiled, like <see cref="Signature"/>: what a group
    /// of modifiers is for is code; which weapon carries them is data.
    /// </summary>
    private static readonly ModifierType[] CurrencyGroup = [ModifierType.Light, ModifierType.Resonant];

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
    /// applier — a staff's effect, Serrated's wound — names its status, an
    /// elemental entry names its type, and a lingering element names the
    /// element it lingers and the status it leaves. With the first rule, the
    /// second makes every unique magnitude quote no flat trigger (S:239): it
    /// applies a status, so its price is the ladder over what it lands.
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
            if (e.Effect is EffectKind.ApplyStatus or EffectKind.Serrated && e.Applies == null)
                throw new ContentException(e.Id, RuleApplierNamesStatus);
            if (e.Effect == EffectKind.ElementalDamage && e.DamageType is null or DamageType.None)
                throw new ContentException(e.Id, RuleElementNamesType);
            if (e.Effect == EffectKind.LingeringElement && (e.DamageType is null or DamageType.None || e.Applies == null))
                throw new ContentException(e.Id, RuleLingerNamesElement,
                    e.Applies == null ? "leaves no status" : "names no element");
        }
    }

    /// <summary>
    /// The §5.5/§5.7 cross-check (S:265-270, S:277): the <c>unique</c> flag says
    /// the tier is pinned, <c>neverRolled</c> says no roll grants it, and the
    /// two are one question — every unique entry is in <c>neverRolled</c>, and
    /// <c>neverRolled</c> names nothing but unique entries — so declaring one
    /// and forgetting the other aborts startup rather than letting a unique
    /// appear in the enchanter's catalogue. <see cref="EnchantmentCatalogue"/>
    /// runs this itself as well.
    /// </summary>
    public static void ValidateNeverRolled(EnchantmentsData data, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(restricted);

        var never = new HashSet<string>(restricted.NeverRolled, StringComparer.Ordinal);
        foreach (var e in data.Enchantments)
            if (e.Unique && !never.Contains(e.Id))
                throw new ContentException(e.Id, RuleUniqueNeverRolled, "unique, but absent from neverRolled");

        var byId = data.Enchantments.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var id in restricted.NeverRolled)
            if (!byId.TryGetValue(id, out var e) || !e.Unique)
                throw new ContentException(id, RuleUniqueNeverRolled, "in neverRolled, but not a unique enchantment");
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

        // A lingering element lingers an element somebody carries: Searing is nothing without its Flaming.
        foreach (var e in data.Enchantments)
            if (e.Effect == EffectKind.LingeringElement && (e.DamageType is not { } lingered || !byType.ContainsKey(lingered)))
                throw new ContentException(e.Id, RuleLingerNamesElement, "no innate enchantment carries its element");

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
    /// forged somewhere, or it could never exist. An enchantment's dependency
    /// is met on the weapon itself (§1.5): a relation-keyed prerequisite ahead
    /// of its dependent, a surplus-healing converter beside a source of
    /// healing, and either only on a weapon forged with a currency modifier —
    /// the combo platform — while a unique enchantment arrives only on a
    /// unique, and nothing arrives carrying more than three enchantments.
    /// Unique derivation is checked with the uniques.
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

            // Three souls is the most any weapon arrives carrying (§1.5).
            if (def.Enchantments.Count > MostArrivingEnchantments)
                throw new ContentException(def.Id, RuleArrivingEnchantments, $"{def.Enchantments.Count} enchantments");

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

            // A dependency (§5.5 requires, keyed by an enchantment) is met on the
            // weapon itself, ahead of the dependent in attachment order: the loop
            // fires in that order (§1.7), and a lingering element reads the
            // element that fired before it, so behind it the burn could never
            // fire. Content that leaves the element off is refused here; a
            // transfer that takes it off later (§6.5) leaves the burn quiet.
            for (int i = 0; i < def.Enchantments.Count; i++)
            {
                string id = def.Enchantments[i].Id;
                foreach (var need in enchantments.Prerequisites(id))
                    if (!def.Enchantments.Take(i).Any(r => r.Id == need))
                        throw new ContentException(def.Id, RuleEnchantmentDependency, $"'{id}' needs '{need}' ahead of it");
            }

            // Overheal alone is inert (§1.5): an entry whose behaviour fires on
            // healing above full converts a surplus something else must make,
            // so it sits only beside something that heals the wielder — a
            // status the table says restores HP, or a kind whose behaviour drinks.
            foreach (var reference in def.Enchantments)
                if (ConvertsSurplusHealing(enchantments[reference.Id])
                    && !def.Enchantments.Any(r => IsHealingSource(enchantments[r.Id])))
                    throw new ContentException(def.Id, RuleOverhealBesideHealing, $"'{reference.Id}' has no healing source beside it");

            // And a dependency needs two souls paid for, which only the currency
            // group can afford (§1.5): "the only place in the design where an
            // enchantment is allowed to depend on another one existing". A
            // Light artifact earns the second trigger's mana, a Resonant one
            // spends less on both; anything else carrying a pair that feeds
            // itself is a combo the design never priced.
            foreach (var reference in def.Enchantments)
                if (DependsOnAnother(enchantments[reference.Id], enchantments) && !CurrencyGroup.Any(t => spread.Stacks(t) > 0))
                    throw new ContentException(def.Id, RuleDependencyOnCurrency, $"'{reference.Id}' on a weapon forged without {string.Join(" or ", CurrencyGroup)}");

            // A unique enchantment is a thing that exists, not a catalogue entry
            // (§1.5, §3.3): content hands one only to the uniques that carry
            // it, never to a variant a drop could roll.
            if (!def.Unique)
                foreach (var reference in def.Enchantments)
                    if (enchantments[reference.Id].Unique)
                        throw new ContentException(def.Id, RuleUniqueSoulOnUnique, $"'{reference.Id}' on a variant");

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
                if (def.Unique && def.Role != null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, $"a unique carries the role {def.Role}");
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

        ValidateUniques(data, enchantments);

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
    /// The derivation rule (§1.5, S:238), checked over the whole unique table
    /// (K:179-182): a unique names the variant it derives from, and nothing
    /// else does; it keeps that variant's class, statline and shape —
    /// "everything else is unchanged". A martial unique is the variant with
    /// exactly one modifier it already carries raised to x3 and nothing else
    /// changed — never a second x3, never a modifier the variant lacks, never
    /// a freely-authored spread — unless the variant is Light-forged, in which
    /// case the currency cannot be raised and the artifact's identity lives in
    /// its souls: the signature at x2 or x3 and nothing else changed, with
    /// 5 - n enchantments, one of them unique or at tier 3. A caster unique
    /// raises nothing — Resonant is forge-limited to x1 — and differs from its
    /// variant only in its enchantment list, which is the whole artifact.
    /// </summary>
    private static void ValidateUniques(WeaponsData data, EnchantmentCatalogue enchantments)
    {
        var byId = data.Weapons.ToDictionary(w => w.Id, StringComparer.Ordinal);
        foreach (var def in data.Weapons)
        {
            if (!def.Unique)
            {
                if (def.DerivedFrom != null)
                    throw new ContentException(def.Id, RuleUniqueDerivedFrom, $"a variant names '{def.DerivedFrom}'");
                continue;
            }

            if (def.DerivedFrom == null)
                throw new ContentException(def.Id, RuleUniqueDerivedFrom, "names no variant");
            if (!byId.TryGetValue(def.DerivedFrom, out var variant) || variant.Unique)
                throw new ContentException(def.Id, RuleUniqueDerivedFrom, $"'{def.DerivedFrom}' is not a variant");
            if (variant.Class != def.Class || variant.Range != def.Range || variant.Damage != def.Damage
                || variant.Cost != def.Cost || variant.ManaCost != def.ManaCost || !Equals(variant.Shape, def.Shape))
                throw new ContentException(def.Id, RuleUniqueStatline, $"differs from '{variant.Id}' beyond its spread and enchantments");

            var changed = Enum.GetValues<ModifierType>()
                .Where(t => def.Forged.GetValueOrDefault(t) != variant.Forged.GetValueOrDefault(t))
                .ToList();

            if (Weapon.KindOf(def.Class) == WeaponKind.Caster)
            {
                if (changed.Count != 0)
                    throw new ContentException(def.Id, RuleCasterUniqueEnchantments, $"changes {string.Join(", ", changed)}");
                if (def.Enchantments.SequenceEqual(variant.Enchantments))
                    throw new ContentException(def.Id, RuleCasterUniqueEnchantments, "its enchantments are the variant's own");
                continue;
            }

            var signature = Signature[def.Class];
            if (variant.Forged.GetValueOrDefault(ModifierType.Light) > 0)
            {
                // The Light shape: depth traded for souls, one per stack given up, floor x2.
                if (changed.Count != 1 || changed[0] != signature)
                    throw new ContentException(def.Id, RuleLightUniqueShape, changed.Count == 0
                        ? "raises nothing"
                        : $"changes {string.Join(", ", changed)}, not the {def.Class} signature {signature} alone");
                int n = def.Forged.GetValueOrDefault(signature);
                if (n < LightShapeFloor || n > UniqueRaise)
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"{signature} x{n}: the floor is x{LightShapeFloor} and the forge limit x{UniqueRaise}");
                int souls = LightShapeSlots - n;
                if (def.Enchantments.Count != souls)
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"{def.Enchantments.Count} enchantments at {signature} x{n}, not {souls}");
                if (!def.Enchantments.Any(r => enchantments[r.Id].Unique || r.Tier >= ArtifactTier))
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"none of its enchantments is unique or at tier {ArtifactTier}");
                continue;
            }

            if (changed.Count != 1)
                throw new ContentException(def.Id, RuleUniqueDerivation, changed.Count == 0
                    ? $"is '{variant.Id}' itself"
                    : $"changes {string.Join(", ", changed)}: exactly one modifier is raised");
            var raised = changed[0];
            int was = variant.Forged.GetValueOrDefault(raised), now = def.Forged.GetValueOrDefault(raised);
            if (was == 0)
                throw new ContentException(def.Id, RuleUniqueDerivation, $"adds {raised}, which '{variant.Id}' lacks");
            if (now != UniqueRaise)
                throw new ContentException(def.Id, RuleUniqueDerivation, now < was ? $"drops {raised}" : $"{raised} x{now}, not x{UniqueRaise}");
        }
    }

    /// <summary>Whether an entry heals its wielder: a status applier whose status the table says restores HP (a Regeneration innate), or a kind whose behaviour drinks (<see cref="EnchantmentBehaviours.HealsTheWielder"/>).</summary>
    private static bool IsHealingSource(EnchantmentDef def)
        => (def.Applies is { } status && StatusRules.Of(status).RestoresHp) || EnchantmentBehaviours.HealsTheWielder(def.Effect);

    /// <summary>Whether an entry's behaviour fires on healing above full — converts a surplus rather than making one (Overheal) — read off the behaviour tables, never off the kind's name.</summary>
    private static bool ConvertsSurplusHealing(EnchantmentDef def)
        => EnchantmentBehaviours.FiresOn(GameEvent.HealingAboveFull, def.Effect);

    /// <summary>
    /// Whether an entry cannot work alone: it names prerequisites in the
    /// relations' enchantment-keyed <c>requires</c> (a lingering element's
    /// element), or it converts a surplus of healing some other entry must
    /// make. The two dependencies §1.5 places, and the reason each sits only
    /// on the combo platform.
    /// </summary>
    private static bool DependsOnAnother(EnchantmentDef def, EnchantmentCatalogue enchantments)
        => enchantments.Prerequisites(def.Id).Count > 0 || ConvertsSurplusHealing(def);

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
