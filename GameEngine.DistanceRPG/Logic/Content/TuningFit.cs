namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Where the tuning meets the content, the tuning bends — never the content,
/// and never with an abort (1g; A3; S:73-74, "a typo should cost a log line, not
/// a session"). Three keys are measured against the content as well as against
/// themselves, and a value inside its own clamp range can still break content
/// that is fine: a carry of 4 is one slot a member while every roster member
/// carries two weapons (<see cref="ContentValidator.RulePartyBagFits"/>); an
/// offset the tuning's own range is happy with can let a dungeon's pinned floor
/// pay the farm's stacks (<see cref="ContentValidator.RuleBossPaysLessThanTheFarm"/>);
/// a forge limit below a weapon's forging, or a class's baseline, breaks
/// <see cref="ContentValidator.RuleMaxForged"/>.
/// So each is raised against the content in hand, within its own clamp range,
/// with one warning naming the entry that needed it.
/// <para>
/// Content that no in-range value can serve — a roster member carrying twelve
/// weapons, a dungeon pinned to floor 500, a weapon forged x4 — is content's own
/// error: it is left to fail as content, naming the entry and the rule, and the
/// tuning says nothing about it. A record that failed to parse arrives null and
/// is skipped: nothing is fitted to content that is not loaded. Run by
/// <see cref="ContentFiles.Load"/> after every file has parsed and before
/// anything validates, so every tuning a file can make loads with the shipped
/// content, and the fitted tuning is what the game runs under and hashes.
/// </para>
/// </summary>
internal static class TuningFit
{
    /// <summary>
    /// <paramref name="tuning"/> with <see cref="Tuning.MaxForged"/>,
    /// <see cref="Tuning.PartyCarrySlots"/> and <see cref="Tuning.BossStackOffset"/>
    /// raised, in that order, as far as the content in hand needs and each key's
    /// clamp range allows; a warning for each one raised.
    /// </summary>
    public static Tuning ToContent(Tuning tuning, WeaponsData? weapons, DungeonsData? dungeons, PartyData? party,
        ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(warnings);

        if (weapons is not null)
            tuning = FitForgeLimits(tuning, weapons, warnings);
        if (party is not null)
            tuning = FitCarry(tuning, party, warnings);
        if (dungeons is not null)
            tuning = FitOffset(tuning, dungeons, warnings);
        return tuning;
    }

    /// <summary>
    /// Every modifier's forge limit raised to the deepest any loaded weapon is
    /// forged with it, naming the first weapon in file order forged that deep —
    /// so after the fit every theme's limit is still its unique's x3, which is
    /// what makes the boss's graft land on the unique by arithmetic
    /// (<see cref="ContentValidator.RuleThemeHasAUnique"/>). A class row's
    /// baseline is held to the limit as well, and every weapon of the class
    /// carries it at least as deep, so a class is named only where its baseline
    /// is deeper than every weapon — a class that fields none — the first such in
    /// the enum's order.
    /// </summary>
    private static Tuning FitForgeLimits(Tuning tuning, WeaponsData weapons, ICollection<string> warnings)
    {
        var key = Key<TableKey>(nameof(Tuning.MaxForged));
        var limits = new Dictionary<string, int>(key.Get(tuning), StringComparer.Ordinal);
        bool raised = false;
        foreach (var type in Enum.GetValues<ModifierType>())
        {
            string? deepest = null;   // who needs the limit raised, as the warning names it
            int depth = 0;
            foreach (var weapon in weapons.Weapons)
            {
                int forged = weapon.Forged is { } spread ? spread.GetValueOrDefault(type) : 0;
                if (forged > depth)
                {
                    deepest = $"{weapon.Id}, which is forged";
                    depth = forged;
                }
            }
            foreach (var row in weapons.Classes)
            {
                int forged = row.Baseline is { } baseline ? baseline.GetValueOrDefault(type) : 0;
                if (forged > depth)
                {
                    deepest = $"the {row.Class} class, whose baseline carries";
                    depth = forged;
                }
            }

            string name = type.ToString();
            int limit = limits[name];
            if (deepest is null || depth <= limit || depth > key.Max)
                continue;   // served already, or forged past any limit there is: the weapon's or the class's own error

            warnings.Add($"{TuningJson.Prefix}{key.Name}.{name} {limit} is below {deepest} {name} x{depth} - using {depth}");
            limits[name] = depth;
            raised = true;
        }
        return raised ? key.Set(tuning, limits) : tuning;
    }

    /// <summary>
    /// The party's carry raised until the roster's largest starting loadout fits
    /// one member's share — <see cref="GameConstants.PartySize"/> times it, which
    /// shares evenly — naming the first member in roster order carrying that many,
    /// and what they carry.
    /// </summary>
    private static Tuning FitCarry(Tuning tuning, PartyData party, ICollection<string> warnings)
    {
        PartyMemberDef? widest = null;
        int largest = 0;
        foreach (var member in party.Members)
        {
            int carried = 1 + (member.BagWeaponIds?.Count ?? 0);
            if (carried > largest)
            {
                widest = member;
                largest = carried;
            }
        }

        int slots = PartyMemberState.SlotsFor(tuning);
        var key = Key<IntKey>(nameof(Tuning.PartyCarrySlots));
        int needed = GameConstants.PartySize * largest;
        if (widest is null || largest <= slots || needed > key.Max)
            return tuning;   // it fits, or no carry there is could: the roster's own error

        var carries = (widest.BagWeaponIds ?? []).Prepend(widest.StartingWeaponId);
        warnings.Add($"{TuningJson.Prefix}{key.Name} {tuning.PartyCarrySlots} is {slots} {(slots == 1 ? "slot" : "slots")} a member, and {widest.Id} carries "
            + $"{largest} weapons ({string.Join(", ", carries)}) - using {needed}");
        return key.Set(tuning, needed);
    }

    /// <summary>
    /// The boss offset raised until the deepest boss of every dungeon — its own
    /// pinned floor, or the tuning's range where it pins none — pays fewer
    /// guaranteed stacks than a well-farmed weapon banks, naming the dungeon that
    /// needed the most.
    /// </summary>
    private static Tuning FitOffset(Tuning tuning, DungeonsData dungeons, ICollection<string> warnings)
    {
        var key = Key<IntKey>(nameof(Tuning.BossStackOffset));
        int banks = ContentValidator.FarmBanks(tuning.AcquiredHeadroom);
        int needed = tuning.BossStackOffset;
        DungeonDef? cause = null;
        int causeFloor = 0;
        foreach (var def in dungeons.Dungeons)
        {
            if (def is null) continue;
            int deepest = def.BossFloorMax ?? tuning.BossFloorMax;
            int offset = deepest - banks + 1;   // the least offset under which that floor pays fewer than a farm banks
            if (deepest < 1 || offset <= needed || offset > key.Max)
                continue;   // served already, or floored past any offset there is: the dungeon's own error
            needed = offset;
            cause = def;
            causeFloor = deepest;
        }
        if (cause is null)
            return tuning;

        int pays = DungeonState.PromisedStacksFor(causeFloor, tuning);
        warnings.Add($"{TuningJson.Prefix}{key.Name} {tuning.BossStackOffset} lets the {cause.Id} boss on floor {causeFloor} pay {pays} stacks, "
            + $"and a well-farmed weapon banks {banks} - using {needed}");
        return key.Set(tuning, needed);
    }

    private static T Key<T>(string name) where T : TuningKey
        => TuningSchema.Find(name) as T ?? throw new InvalidOperationException($"The tuning schema has no {typeof(T).Name} named {name}.");
}
