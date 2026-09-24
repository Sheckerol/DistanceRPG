namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Live state to the save's plain values and back (§5.1, §5.2). Capture reads
/// the public state and the raw pools; restore resolves every id against the
/// content handed in and rebuilds through the types' internal doors —
/// <see cref="CampaignState.Restore"/>, <see cref="PartyMemberState.Restore"/>,
/// the restore <see cref="Weapon"/> constructor, <see cref="ActorState.RawHp"/>
/// and <see cref="ActorState.RawMana"/> — so no gameplay type grows a public
/// setter for a load's sake.
/// <para>
/// <strong>One state is one text.</strong> Every table it writes is filled in
/// its enum's order (<see cref="OrderedDictionary{TKey, TValue}"/>, whose order is
/// its insertion order by contract), and every list keeps the order the state
/// gives it where that order is the state's: the party, the bag, statuses in
/// landing order, enchantments in attachment order, the campaign's sorted
/// views. Nothing is read off a hash container's enumeration.
/// </para>
/// <para>
/// <strong>A save never limps.</strong> A name or an id the content does not
/// have, a count out of range, a null where a row belongs, or two answers to
/// one question is a <see cref="SaveFormatException"/> naming the path in the
/// document, and nothing is half-built: the restore throws before it hands
/// anything back.
/// </para>
/// </summary>
internal static class SaveMapper
{
    // ── Capture ──────────────────────────────────────────────────────────────

    /// <summary>The campaign between runs, as the document's <c>campaign</c>.</summary>
    internal static CampaignDto Capture(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        return new CampaignDto(
            campaign.Party.Select(Member).ToArray(),
            campaign.SeenInOrder,
            campaign.RunCounter,
            campaign.LockedInOrder.Select(entry => new LockDto(entry.DungeonId, entry.LockedUntilRun)).ToArray(),
            campaign.ClearedInOrder,
            campaign.RetiredInOrder,
            campaign.LastResult is { } result ? Result(result) : null);
    }

    internal static MemberDto Member(PartyMemberState member) => new(
        member.Id,
        member.ColorIndex,
        new StatsDto(member.Stats.STR, member.Stats.DEX, member.Stats.CON, member.Stats.INT),
        Table(member.WeaponXp.Entries),
        member.HpXp,
        member.ManaXp,
        Table(member.Innate.Entries),
        member.Attunement is { } attunement ? Name(attunement) : null,
        Body(member),
        member.Inventory.Select(carried => carried is null ? null : Weapon(carried)).ToArray(),
        member.DistLeft,
        member.EffectiveMax,
        member.SavedMovement,
        member.BudgetBeforeMire);

    internal static ActorDto Body(ActorState actor) => new(
        actor.X,
        actor.Y,
        actor.RawHp,
        actor.RawMana,
        actor.Alive,
        actor.HeldShots,
        actor.StatusEffects.Select(status => new StatusDto(Name(status.Type), status.Element is { } element ? Name(element) : null, status.Levels))
            .ToArray());

    internal static WeaponDto Weapon(Weapon weapon) => new(
        weapon.Id,
        Table(weapon.Forged.Entries),
        Table(weapon.Modifiers.Entries),
        weapon.Enchantments.Select(entry => new EnchantmentDto(entry.Id, entry.Tier, entry.Xp)).ToArray(),
        weapon.ForgedEnchantmentCount);

    internal static RunResultDto Result(RunResult result) => new(
        result.DungeonId,
        Name(result.Outcome),
        result.BossDefeated,
        result.DeepestFloor,
        result.BossFloorRevealed,
        result.ServiceTick,
        result.MercyTick,
        result.RunCounter,
        result.LockedUntilRun,
        result.Retired,
        result.SpoilsKept.Select(Weapon).ToArray(),
        result.SpoilsLost.Select(Weapon).ToArray(),
        result.Clawbacks.Select(c => new ClawbackDto(c.MemberId, c.Pool, c.Before, c.After)).ToArray());

    /// <summary>An enum-keyed count table by member name, in the enum's order — the order its entries come in.</summary>
    private static OrderedDictionary<string, int> Table<T>(IEnumerable<(T Key, int Count)> entries) where T : struct, Enum
    {
        var table = new OrderedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, count) in entries)
            table.Add(Name(key), count);
        return table;
    }

    /// <summary>An enum member's own name: the file's spelling of it.</summary>
    private static string Name<T>(T value) where T : struct, Enum
        => Enum.GetName(value) ?? throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a {typeof(T).Name}: nothing names it.");

    // ── Restore ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The campaign a document's <c>campaign</c> describes, under
    /// <paramref name="content"/> — which must be the current content, since a
    /// member and a weapon are built under it.
    /// </summary>
    /// <exception cref="SaveFormatException">Something in it does not resolve or is out of range, and where.</exception>
    internal static CampaignState Restore(CampaignDto campaign, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        const string at = "campaign";

        var party = Party(campaign.Party, content, $"{at}.party");
        var seen = Ids(campaign.Seen, $"{at}.seen", "enchantment", id => content.Enchantments.TryGet(id, out _));
        NotNegative(campaign.RunCounter, $"{at}.runCounter");
        var locks = Locks(campaign.Locks, content, $"{at}.locks");
        var cleared = Ids(campaign.Cleared, $"{at}.cleared", "dungeon", id => content.Dungeons.TryGet(id, out _));
        var retired = Ids(campaign.Retired, $"{at}.retired", "dungeon", id => content.Dungeons.TryGet(id, out _));
        var lastResult = campaign.LastResult is { } result ? Result(result, content, party, $"{at}.lastResult") : null;

        return CampaignState.Restore(party, seen, campaign.RunCounter, locks, cleared, retired, lastResult);
    }

    /// <summary>The party, in the document's order: exactly a party's size, each id once.</summary>
    private static PartyMemberState[] Party(IReadOnlyList<MemberDto> dtos, GameContent content, string at)
    {
        var rows = Rows(dtos, at, "member");
        if (rows.Count != GameConstants.PartySize)
            throw new SaveFormatException(at, $"{rows.Count} members: a party is {GameConstants.PartySize}");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var members = new PartyMemberState[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            string where = $"{at}[{i}]";
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id))
                throw new SaveFormatException($"{where}.id", "blank: a member is named by the roster's stable id");
            if (!ids.Add(row.Id))
                throw new SaveFormatException($"{where}.id", $"'{row.Id}' is already in the party: a member is there once");
            members[i] = Member(row, content, where);
        }
        return members;
    }

    private static PartyMemberState Member(MemberDto row, GameContent content, string at)
    {
        if (row.ColorIndex < 0 || row.ColorIndex >= GameConstants.PartySize)
            throw new SaveFormatException($"{at}.colorIndex", $"{row.ColorIndex} is no party colour: they run 0 to {GameConstants.PartySize - 1}");
        var stats = Stats(row.Stats, $"{at}.stats");
        var bag = Required(row.Inventory, $"{at}.inventory");
        if (bag.Count < 1 || bag.Count > GameConstants.MaxInventorySlots)
            throw new SaveFormatException($"{at}.inventory",
                $"{bag.Count} slots: a member carries at least the one in hand and at most {GameConstants.MaxInventorySlots}");

        var member = PartyMemberState.Restore(row.Id, row.ColorIndex, stats, bag.Count);

        foreach (var (name, xp) in Required(row.WeaponXp, $"{at}.weaponXp"))
            member.WeaponXp[EnumOf<WeaponClass>(name, $"{at}.weaponXp.{name}", "weapon class")] = NotNegative(xp, $"{at}.weaponXp.{name}");
        member.HpXp = NotNegative(row.HpXp, $"{at}.hpXp");
        member.ManaXp = NotNegative(row.ManaXp, $"{at}.manaXp");
        member.Innate = Modifiers(row.Innate, $"{at}.innate");
        member.Attunement = row.Attunement is { } attunement ? EnumOf<DamageType>(attunement, $"{at}.attunement", "damage type") : null;
        Body(member, Required(row.Body, $"{at}.body"), $"{at}.body");

        for (int slot = 0; slot < bag.Count; slot++)
            member.Inventory[slot] = bag[slot] is { } carried ? Weapon(carried, content, $"{at}.inventory[{slot}]") : null;

        member.DistLeft = row.DistLeft;
        member.EffectiveMax = row.EffectiveMax;
        member.SavedMovement = row.SavedMovement;
        member.BudgetBeforeMire = row.BudgetBeforeMire;
        return member;
    }

    /// <summary>A spread as saved: every stat a divisor the §2.1 table can use, and never re-asked the permutation rule, which is the roster file's.</summary>
    private static InnateStats Stats(StatsDto? stats, string at)
    {
        var row = Required(stats, at);
        int Divisor(int value, string name)
            => value is >= InnateStats.Low and <= InnateStats.High
                ? value
                : throw new SaveFormatException($"{at}.{name}", $"{value} is no stat: a stat runs {InnateStats.Low} to {InnateStats.High}");
        return new InnateStats(Divisor(row.Str, "str"), Divisor(row.Dex, "dex"), Divisor(row.Con, "con"), Divisor(row.Int, "int"));
    }

    /// <summary>An actor's body onto <paramref name="actor"/>: where it stands, its pools as stored, and its statuses landed again in their order.</summary>
    private static void Body(ActorState actor, ActorDto body, string at)
    {
        actor.X = body.X;
        actor.Y = body.Y;
        actor.RawHp = body.Hp is { } hp ? NotNegative(hp, $"{at}.hp") : null;
        actor.RawMana = body.Mana is { } mana ? NotNegative(mana, $"{at}.mana") : null;
        actor.Alive = body.Alive;
        actor.HeldShots = NotNegative(body.HeldShots, $"{at}.heldShots");

        var statuses = Rows(body.Statuses, $"{at}.statuses", "status");
        var landed = new HashSet<(StatusEffectType, DamageType?)>();
        for (int i = 0; i < statuses.Count; i++)
        {
            string where = $"{at}.statuses[{i}]";
            var status = statuses[i];
            var type = EnumOf<StatusEffectType>(status.Type, $"{where}.type", "status effect");
            DamageType? element = status.Element is { } name ? EnumOf<DamageType>(name, $"{where}.element", "damage type") : null;
            if (StatusRules.KeysOnElement(type))
            {
                if (element is null or DamageType.None)
                    throw new SaveFormatException($"{where}.element", $"{type} is keyed on the element that lit it, and none is named");
            }
            else if (element is not null)
            {
                throw new SaveFormatException($"{where}.element", $"{type} carries no element, and '{status.Element}' is named");
            }
            if (status.Levels < 1)
                throw new SaveFormatException($"{where}.levels", $"{status.Levels}: a status on an actor has a level or more, and at none it is gone");
            if (!landed.Add((type, element)))
                throw new SaveFormatException(where, $"{type}{(element is null ? "" : $" ({element})")} is already on the actor: one entry per status");

            actor.ApplyStatus(type, element, status.Levels);   // landed as play lands one, so the element rule is the actor's own
        }
    }

    /// <summary>A weapon as saved: the row by id, both spreads by name, the entries by id at their tiers, in attachment order.</summary>
    private static Weapon Weapon(WeaponDto row, GameContent content, string at)
    {
        if (!content.Weapons.TryGet(row.Id, out var def))
            throw new SaveFormatException($"{at}.id", $"'{row.Id}' is no weapon in the content");

        var forged = Modifiers(row.Forged, $"{at}.forged");
        var live = Modifiers(row.Modifiers, $"{at}.modifiers");
        foreach (var (type, stacks) in forged.Entries)
            if (live.Stacks(type) < stacks)
                throw new SaveFormatException($"{at}.modifiers.{Name(type)}",
                    $"{live.Stacks(type)} is below the {stacks} forged: the live spread is the forged one plus what was acquired, never less");

        var rows = Rows(row.Enchantments, $"{at}.enchantments", "enchantment");
        var entries = new Enchantment[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            string where = $"{at}.enchantments[{i}]";
            var entry = rows[i];
            if (!content.Enchantments.TryGet(entry.Id, out var entryDef))
                throw new SaveFormatException($"{where}.id", $"'{entry.Id}' is no enchantment in the content");
            if (entry.Tier < 1)
                throw new SaveFormatException($"{where}.tier", $"{entry.Tier} is no tier: an entry is attached at tier 1 or deeper");
            // The content's own def — the tuned one — so a unique's pin holds whatever the text says.
            entries[i] = new Enchantment(entryDef, entry.Tier) { Xp = NotNegative(entry.Xp, $"{where}.xp") };
        }

        if (row.ForgedEnchantments < 0 || row.ForgedEnchantments > entries.Length)
            throw new SaveFormatException($"{at}.forgedEnchantments",
                $"{row.ForgedEnchantments} of {entries.Length} entries: the forged ones are a prefix of the list");

        return new Weapon(def, forged, live, entries, row.ForgedEnchantments);
    }

    /// <summary>A modifier table by name: every key a <see cref="ModifierType"/>, every count whole and not negative.</summary>
    private static ModifierSet Modifiers(IReadOnlyDictionary<string, int>? table, string at)
    {
        var stacks = new List<(ModifierType, int)>();
        foreach (var (name, count) in Required(table, at))
            stacks.Add((EnumOf<ModifierType>(name, $"{at}.{name}", "modifier"), NotNegative(count, $"{at}.{name}")));
        return ModifierSet.Of(stacks.ToArray());
    }

    /// <summary>The locks, each on a dungeon the content has, each dungeon once.</summary>
    private static (string DungeonId, int LockedUntilRun)[] Locks(IReadOnlyList<LockDto> dtos, GameContent content, string at)
    {
        var rows = Rows(dtos, at, "lock");
        var locked = new HashSet<string>(StringComparer.Ordinal);
        var locks = new (string, int)[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            string where = $"{at}[{i}]";
            var row = rows[i];
            if (!content.Dungeons.TryGet(row.DungeonId, out _))
                throw new SaveFormatException($"{where}.dungeonId", $"'{row.DungeonId}' is no dungeon in the content");
            if (!locked.Add(row.DungeonId))
                throw new SaveFormatException($"{where}.dungeonId", $"'{row.DungeonId}' is already locked: a dungeon has one lock");
            locks[i] = (row.DungeonId, NotNegative(row.LockedUntilRun, $"{where}.lockedUntilRun"));
        }
        return locks;
    }

    /// <summary>A list of ids that must each name something in the content, each once.</summary>
    private static string[] Ids(IReadOnlyList<string> ids, string at, string kind, Func<string, bool> resolves)
    {
        var rows = Rows(ids, at, kind);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!resolves(rows[i]))
                throw new SaveFormatException($"{at}[{i}]", $"'{rows[i]}' is no {kind} in the content");
            if (!seen.Add(rows[i]))
                throw new SaveFormatException($"{at}[{i}]", $"'{rows[i]}' is listed twice: an id is in a set once");
        }
        return rows.ToArray();
    }

    /// <summary>How the last run ended, as its summary reads it: the dungeon and the members it names are the content's and the party's, its spoils weapons like any other.</summary>
    private static RunResult Result(RunResultDto row, GameContent content, IReadOnlyList<PartyMemberState> party, string at)
    {
        if (!content.Dungeons.TryGet(row.DungeonId, out _))
            throw new SaveFormatException($"{at}.dungeonId", $"'{row.DungeonId}' is no dungeon in the content");
        var outcome = EnumOf<RunOutcome>(row.Outcome, $"{at}.outcome", "run outcome");

        Weapon[] Spoils(IReadOnlyList<WeaponDto> spoils, string where)
        {
            var rows = Rows(spoils, where, "weapon");
            return rows.Select((spoil, i) => Weapon(spoil, content, $"{where}[{i}]")).ToArray();
        }

        var clawbacks = Rows(row.Clawbacks, $"{at}.clawbacks", "clawback");
        var taken = new XpClawback[clawbacks.Count];
        for (int i = 0; i < clawbacks.Count; i++)
        {
            string where = $"{at}.clawbacks[{i}]";
            var clawback = clawbacks[i];
            if (!party.Any(member => member.Id == clawback.MemberId))
                throw new SaveFormatException($"{where}.memberId", $"'{clawback.MemberId}' is no member of the party");
            if (clawback.Pool is not (XpSnapshot.HpPool or XpSnapshot.ManaPool) && !Enum.GetNames<WeaponClass>().Contains(clawback.Pool, StringComparer.Ordinal))
                throw new SaveFormatException($"{where}.pool", $"'{clawback.Pool}' is no XP pool: a weapon class, {XpSnapshot.HpPool} or {XpSnapshot.ManaPool}");
            taken[i] = new XpClawback(clawback.MemberId, clawback.Pool, NotNegative(clawback.Before, $"{where}.before"), NotNegative(clawback.After, $"{where}.after"));
        }

        return new RunResult(row.DungeonId, outcome, row.BossDefeated, row.DeepestFloor, row.BossFloorRevealed, row.ServiceTick, row.MercyTick,
            row.RunCounter, row.LockedUntilRun, row.Retired, Spoils(row.SpoilsKept, $"{at}.spoilsKept"), Spoils(row.SpoilsLost, $"{at}.spoilsLost"), taken);
    }

    // ── What every reader asks ───────────────────────────────────────────────

    /// <summary>
    /// A list whose every row is there. The document's nullability is enforced
    /// on members, not on what a list holds, so a <c>null</c> row in a list of
    /// rows is refused here; the bag, whose empty slots are null, is read apart.
    /// </summary>
    private static IReadOnlyList<T> Rows<T>(IReadOnlyList<T>? rows, string at, string kind) where T : class
    {
        var list = Required(rows, at);
        for (int i = 0; i < list.Count; i++)
            if (list[i] is null)
                throw new SaveFormatException($"{at}[{i}]", $"null where a {kind} belongs");
        return list;
    }

    /// <summary>A member the document must hold: what a document built in code, rather than read, could leave out.</summary>
    private static T Required<T>(T? value, string at) where T : class
        => value ?? throw new SaveFormatException(at, "missing");

    private static int NotNegative(int value, string at)
        => value >= 0 ? value : throw new SaveFormatException(at, $"{value} is below 0");

    /// <summary>An enum member by its exact name: never a number, never another case, never a list of flags.</summary>
    private static T EnumOf<T>(string? name, string at, string kind) where T : struct, Enum
        => name is not null && Names<T>.ByName.TryGetValue(name, out var value)
            ? value
            : throw new SaveFormatException(at, $"'{name}' is no {kind}");

    /// <summary>An enum's members by their exact names, built once per enum.</summary>
    private static class Names<T> where T : struct, Enum
    {
        public static readonly IReadOnlyDictionary<string, T> ByName =
            Enum.GetValues<T>().ToDictionary(value => Enum.GetName(value)!, StringComparer.Ordinal);
    }
}
