namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Live state to the save's plain values and back (§5.1, §5.2). Capture reads
/// the public state and the raw pools; restore resolves every id against the
/// content handed in and rebuilds through the types' internal doors —
/// <see cref="CampaignState.Restore"/>, <see cref="PartyMemberState.Restore"/>,
/// <see cref="DungeonState.Restore"/>, <see cref="Floor.Restore"/>, the restore
/// <see cref="Weapon"/> constructor, <see cref="ActorState.RawHp"/>,
/// <see cref="ActorState.RawMana"/> and <see cref="EnemyState.PinnedDrop"/> — so
/// no gameplay type grows a public setter for a load's sake.
/// <para>
/// <strong>One state is one text.</strong> Every table it writes is filled in
/// its enum's order (<see cref="OrderedDictionary{TKey, TValue}"/>, whose order is
/// its insertion order by contract), and every list keeps the order the state
/// gives it where that order is the state's: the party, the bag, statuses in
/// landing order, enchantments in attachment order, the campaign's sorted
/// views, the floors, a floor's rows in spawn order and its loose items in the
/// order left, the spoils in the order taken. The one list read off hash
/// containers — the turn's counters, keyed by reference — is sorted into a
/// canonical order before it is written (<see cref="Carry"/>): nothing is ever
/// written in a hash container's enumeration order.
/// </para>
/// <para>
/// <strong>A save never limps.</strong> A name or an id the content does not
/// have, a count out of range, a null where a row belongs, or two answers to
/// one question is a <see cref="SaveFormatException"/> naming the path in the
/// document, and nothing is half-built: the restore throws before it hands
/// anything back.
/// </para>
/// <para>
/// <strong>And a save is raw JSON, so no field is trusted</strong> (the user's
/// ruling of 2026-09-25, R3(b)). Every field is read on its own first — every
/// count from its floor, and every count whose larger value would wrap the
/// arithmetic that reads it up to a ceiling, beside its floor: a forged stack,
/// a live one, a member's innate, an entry's tier, a weapon's entries and its
/// wear, the run counter, a body's hit points, the boss floor, a status's levels,
/// a hold, and
/// every count play grows a step at a time (<see cref="Count"/>). A float is
/// finite, a body stands on the map, and a member's movement budgets nest as a
/// turn leaves them. A lock wraps nothing, so it
/// keeps its floor alone (<see cref="LockedUntil"/>). Each ceiling is read off
/// <see cref="ContentBounds"/> — a <c>tuning.json</c> clamp's ceiling or a rule,
/// never the tuning in hand — so no tuning in range and no content that loads
/// refuses a save play wrote. The one count play itself can carry to its
/// ceiling is the run counter, and only from a hand edit that left it just
/// short (settled.md, R3(b): the walk's known edge). Only then are the fields
/// asked of each other (<see cref="SaveConsistency"/>), so every refusal of a
/// field keeps its own path.
/// </para>
/// </summary>
internal static class SaveMapper
{
    // ── Capture ──────────────────────────────────────────────────────────────

    /// <summary>The campaign, as the document's <c>campaign</c>: between runs, or with the visit in progress inside it.</summary>
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
            campaign.LastResult is { } result ? Result(result) : null,
            campaign.Visit is { } visit ? Visit(visit, campaign.Party) : null);
    }

    /// <summary>
    /// The visit in progress (§5.1's Run group): the values a load uses as they
    /// are, the party's XP at the door, where each spoil is, this turn's counters
    /// in their canonical order, every floor down to the deepest the party has
    /// stood on — floors are adjacent only, so that is every visited floor — and
    /// how many d20s the visit's own die has rolled.
    /// </summary>
    /// <param name="visit">The visit, at a safe point.</param>
    /// <param name="party">The members, in roster order: the members the visit is being made by.</param>
    internal static VisitDto Visit(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(party);

        var floors = new FloorDto[visit.DeepestFloorIndex + 1];
        for (int n = 0; n < floors.Length; n++)
        {
            var floor = visit.Floors[n];
            if (!floor.Visited)
                throw new InvalidOperationException(
                    $"Floor {n + 1} is above the deepest the party reached, {visit.DeepestFloorIndex + 1}, and was never stood on: floors are adjacent, and every one down to the deepest is visited.");
            floors[n] = FloorOf(floor, visit);
        }

        return new VisitDto(
            visit.Def.Id,
            visit.MapSeed,
            visit.BossFloor,
            visit.PromisedStacks,
            visit.FloorIndex,
            visit.DeepestFloorIndex,
            visit.Turn,
            visit.BossDefeated,
            visit.BossDefeatedAtTurn,
            Snapshot(visit.EntrySnapshot),
            visit.Spoils.Select((spoil, i) => SpoilOf(spoil, i, party, visit)).ToArray(),
            Carry(visit.CurrentCarry(), party, visit),
            floors,
            visit.DiceRolled);
    }

    /// <summary>
    /// A visited floor: its rows in spawn order, what each lies on (pinned — the
    /// current floor's answered by its open system, any other's as that system
    /// would answer), its explored fog, its loose items in the order left, and on
    /// the floor the party stands on what it can see this turn.
    /// </summary>
    private static FloorDto FloorOf(Floor floor, DungeonState visit)
    {
        bool current = floor.Index == visit.FloorIndex;
        var open = current ? visit.Turns : null;
        return new FloorDto(
            floor.Index,
            floor.Enemies.Select(row => EnemyOf(row, floor, visit.BossDefeated, open)).ToArray(),
            FogState.Encode(floor.Fog.Seen),
            floor.LooseItems.Select(item => new LooseItemDto(Weapon(item.Weapon), item.Tile.R, item.Tile.C)).ToArray(),
            current ? FogState.Encode(floor.Fog.Visible) : null);
    }

    /// <summary>
    /// One row: body, weapon and the farm's numbers as they stand, whether it is
    /// the floor's boss, and the drop it lies on — what the game answers for it
    /// now, the floor's open system when there is one
    /// (<see cref="TurnSystem.GroundItemOf"/>) and otherwise the same rule off the
    /// floor (<see cref="DungeonState.GroundItemOf"/>) — written only while it lies
    /// on one, so a load pins the weapon that was there.
    /// </summary>
    private static EnemyDto EnemyOf(EnemyState row, Floor floor, bool bossDefeated, TurnSystem? open)
    {
        var lying = open is not null ? open.GroundItemOf(row) : DungeonState.GroundItemOf(floor, row, bossDefeated);
        return new EnemyDto(
            row.SpawnIndex,
            ReferenceEquals(row, floor.Boss),
            Body(row),
            Weapon(row.Weapon),
            row.DefeatedAtTurn,
            row.TurnsSinceSeen,
            row.DefeatCount,
            row.RevivalsRolled,
            row.RevivalDamage,
            row.RevivalMaxHp,
            row.DropTaken,
            lying is null ? null : new DropDto(Weapon(lying.Weapon), lying.WasUniqueRoll));
    }

    /// <summary>The party's XP at the door: each member's weapon XP by class name, a class at zero left out, in <see cref="WeaponClass"/> order, then its two pools.</summary>
    private static XpSnapshotDto Snapshot(XpSnapshot snapshot)
    {
        var classes = Enum.GetValues<WeaponClass>();
        return new XpSnapshotDto(snapshot.Members
            .Select(member => new MemberXpDto(
                member.MemberId,
                Table(classes.Select((cls, i) => (cls, i < member.WeaponXp.Count ? member.WeaponXp[i] : 0)).Where(entry => entry.Item2 != 0)),
                member.HpXp,
                member.ManaXp))
            .ToArray());
    }

    /// <summary>
    /// Where spoil <paramref name="index"/> is now: the bag slot holding that very
    /// weapon, or its place among a visited floor's loose items. A spoil is in one
    /// or the other for as long as the visit lasts — only a run's end takes one
    /// out of the dungeon — so one found nowhere is a fault, never a thing to drop.
    /// </summary>
    private static SpoilDto SpoilOf(Weapon spoil, int index, IReadOnlyList<PartyMemberState> party, DungeonState visit)
    {
        foreach (var member in party)
        {
            int slot = Array.FindIndex(member.Inventory, carried => ReferenceEquals(carried, spoil));
            if (slot >= 0)
                return new SpoilDto(Member: member.Id, Slot: slot);
        }
        for (int n = 0; n <= visit.DeepestFloorIndex; n++)
        {
            int loose = visit.Floors[n].LooseItems.FindIndex(item => ReferenceEquals(item.Weapon, spoil));
            if (loose >= 0)
                return new SpoilDto(Floor: n, Loose: loose);
        }
        throw new InvalidOperationException(
            $"Spoil {index}, {spoil.Name}, is in no member's bag and on no floor: a spoil is carried or lying somewhere until the run ends.");
    }

    /// <summary>
    /// The turn's counters (<see cref="TurnCarry"/>) in their one canonical order,
    /// or null for none. The live counters are keyed by reference in hash
    /// containers, whose enumeration order .NET does not specify and whose
    /// insertion history differs between a campaign played through and the same
    /// campaign reloaded (the party's zone is exported before the enemies', an
    /// earlier floor's entries merged in before a later's), so every list is
    /// sorted before it is written: members in roster order, then enemies by
    /// floor index and spawn index; a reaction's uses by that actor, then by its
    /// modifier in <see cref="ModifierType"/> order. The live containers are only
    /// read. One turn's counters are one text, however they were built.
    /// </summary>
    /// <param name="carry">The counters, as <see cref="DungeonState.CurrentCarry"/> gives them.</param>
    /// <param name="party">The members, in roster order.</param>
    /// <param name="visit">The visit, whose visited floors hold every enemy the counters can name.</param>
    /// <exception cref="InvalidOperationException">The counters name an actor that is neither a member nor a row of a visited floor.</exception>
    internal static CarryDto? Carry(TurnCarry? carry, IReadOnlyList<PartyMemberState> party, DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(visit);
        if (carry is null)
            return null;

        var refs = new Dictionary<ActorState, (ActorRefDto Ref, (int Side, int Major, int Minor) Order)>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < party.Count; i++)
            refs[party[i]] = (new ActorRefDto(Member: party[i].Id), (0, i, 0));
        for (int n = 0; n <= visit.DeepestFloorIndex; n++)
            foreach (var row in visit.Floors[n].Enemies)
                refs[row] = (new ActorRefDto(Floor: n, Spawn: row.SpawnIndex), (1, n, row.SpawnIndex));

        (ActorRefDto Ref, (int Side, int Major, int Minor) Order) RefOf(ActorState actor)
            => refs.TryGetValue(actor, out var found)
                ? found
                : throw new InvalidOperationException("The turn's counters name an actor that is neither a member of the party nor a row of a floor the party has stood on.");

        CountDto[] Counts(IReadOnlyDictionary<ActorState, int> counts)
            => counts
                .Select(entry => (Actor: RefOf(entry.Key), entry.Value))
                .OrderBy(entry => entry.Actor.Order)
                .Select(entry => new CountDto(entry.Actor.Ref, entry.Value))
                .ToArray();

        return new CarryDto(
            carry.Turn,
            Counts(carry.Attacks),
            Counts(carry.ZoneUses),
            carry.ReactionUses
                .Select(entry => (Actor: RefOf(entry.Key.Actor), entry.Key.Type, entry.Value))
                .OrderBy(entry => entry.Actor.Order)
                .ThenBy(entry => entry.Type)
                .Select(entry => new ReactionUseDto(entry.Actor.Ref, Name(entry.Type), entry.Value))
                .ToArray(),
            carry.Seen
                .Select(enemy => RefOf(enemy))
                .OrderBy(actor => actor.Order)
                .Select(actor => actor.Ref)
                .ToArray());
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
        weapon.ForgedEnchantmentCount,
        weapon.Wear,
        weapon.WearCashed);

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
    /// member and a weapon are built under it. Every field is read on its own
    /// first, and then the fields are asked of each other
    /// (<see cref="SaveConsistency.Check"/>): a save no play could have written is
    /// refused whole, before any campaign is built of it.
    /// </summary>
    /// <exception cref="SaveFormatException">Something in it does not resolve, is out of range, or contradicts another field, and where.</exception>
    internal static CampaignState Restore(CampaignDto campaign, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        const string at = "campaign";

        var party = Party(campaign.Party, content, $"{at}.party");
        var seen = Ids(campaign.Seen, $"{at}.seen", "enchantment", id => content.Enchantments.TryGet(id, out _));
        Runs(campaign.RunCounter, $"{at}.runCounter");
        var locks = Locks(campaign.Locks, content, $"{at}.locks");
        var cleared = Ids(campaign.Cleared, $"{at}.cleared", "dungeon", id => content.Dungeons.TryGet(id, out _));
        var retired = Ids(campaign.Retired, $"{at}.retired", "dungeon", id => content.Dungeons.TryGet(id, out _));
        var lastResult = campaign.LastResult is { } result ? Result(result, content, party, $"{at}.lastResult") : null;
        var visit = campaign.Visit is { } saved ? Visit(saved, content, party, $"{at}.visit") : null;

        // Every field has been read on its own, so a refusal of one keeps its own
        // path; now the fields against each other (R3(b)).
        SaveConsistency.Check(campaign);

        return CampaignState.Restore(party, seen, campaign.RunCounter, locks, cleared, retired, lastResult, visit);
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
            member.WeaponXp[EnumOf<WeaponClass>(name, $"{at}.weaponXp.{name}", "weapon class")] = Count(xp, $"{at}.weaponXp.{name}");
        member.HpXp = Count(row.HpXp, $"{at}.hpXp");
        member.ManaXp = Count(row.ManaXp, $"{at}.manaXp");
        member.Innate = Modifiers(row.Innate, $"{at}.innate", _ => (ContentBounds.DeepestStack, DeepestStackWhy));
        member.Attunement = row.Attunement is { } attunement ? EnumOf<DamageType>(attunement, $"{at}.attunement", "damage type") : null;
        // A member's maximum is its bar plus a point for each HP XP at most (each
        // point costs one or more), and no bar opens larger than Bar: summed as a
        // long, so an XP near the largest whole number cannot wrap the ceiling.
        Body(member, Required(row.Body, $"{at}.body"), $"{at}.body", ContentBounds.Bar + (long)member.HpXp, MemberHpWhy);

        for (int slot = 0; slot < bag.Count; slot++)
            member.Inventory[slot] = bag[slot] is { } carried ? Weapon(carried, content, $"{at}.inventory[{slot}]") : null;

        // A turn's start cashes the bank into the whole (the budget plus up to half
        // of it again), Mire cuts the cap from the whole and never adds, and moving
        // and acting only spend from the cap: so each is no more than the one
        // before it, and the whole no more than the longest budget any tuning
        // gives and half of it again.
        member.BudgetBeforeMire = Movement(row.BudgetBeforeMire, ContentBounds.Distance * 1.5f, $"{at}.budgetBeforeMire", BudgetBeforeMireWhy);
        member.EffectiveMax = Movement(row.EffectiveMax, member.BudgetBeforeMire, $"{at}.effectiveMax",
            "a cap is the whole this turn cut it from less what Mire took (budgetBeforeMire)");
        member.DistLeft = Movement(row.DistLeft, member.EffectiveMax, $"{at}.distLeft",
            "movement left is what is left of the cap this turn (effectiveMax)");
        member.SavedMovement = Movement(row.SavedMovement, ContentBounds.Distance / 2f, $"{at}.savedMovement", SavedMovementWhy);
        return member;
    }

    /// <summary>A spread as saved: every stat a divisor the §2.1 table can use, and never re-asked the permutation rule, which is the roster's.</summary>
    private static InnateStats Stats(StatsDto? stats, string at)
    {
        var row = Required(stats, at);
        int Divisor(int value, string name)
            => value is >= InnateStats.Low and <= InnateStats.High
                ? value
                : throw new SaveFormatException($"{at}.{name}", $"{value} is no stat: a stat runs {InnateStats.Low} to {InnateStats.High}");
        return new InnateStats(Divisor(row.Str, "str"), Divisor(row.Dex, "dex"), Divisor(row.Con, "con"), Divisor(row.Int, "int"));
    }

    /// <summary>
    /// An actor's body onto <paramref name="actor"/>: where it stands, its pools as
    /// stored — its hit points no more than <paramref name="mostHp"/>, the most its
    /// maximum can be, which its kind hands in; mana has no ceiling, since it reads
    /// as its spendable ceiling at most whatever is stored — and its statuses
    /// landed again in their order.
    /// </summary>
    private static void Body(ActorState actor, ActorDto body, string at, long mostHp, string mostHpWhy)
    {
        actor.X = Coordinate(body.X, MapGenerator.Cols, $"{at}.x");
        actor.Y = Coordinate(body.Y, MapGenerator.Rows, $"{at}.y");
        actor.RawHp = body.Hp is { } hp ? AtMost(NotNegative(hp, $"{at}.hp"), mostHp, $"{at}.hp", mostHpWhy) : null;
        actor.RawMana = body.Mana is { } mana ? Count(mana, $"{at}.mana") : null;
        actor.Alive = body.Alive;
        actor.HeldShots = AtMost(NotNegative(body.HeldShots, $"{at}.heldShots"), ContentBounds.MostHeldShots, $"{at}.heldShots", HeldShotsWhy);

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
            AtMost(status.Levels, ContentBounds.MostLevels, $"{where}.levels", LevelsWhy);
            if (!landed.Add((type, element)))
                throw new SaveFormatException(where, $"{type}{(element is null ? "" : $" ({element})")} is already on the actor: one entry per status");

            actor.ApplyStatus(type, element, status.Levels);   // landed as play lands one, so the element rule is the actor's own
        }
    }

    /// <summary>
    /// A weapon as saved: the row by id, both spreads by name, the entries by id
    /// at their tiers, in attachment order, and its wear. A forged stack is no
    /// deeper than any forge goes, a live one no deeper than its forged count plus
    /// the most headroom any tuning gives, an entry's tier no deeper than its
    /// arithmetic stays whole at, the entries no more than a weapon holds, the wear
    /// cashed a count, and the wear no more than any tuning's capacity for what was
    /// cashed (<see cref="ContentBounds"/>).
    /// </summary>
    private static Weapon Weapon(WeaponDto row, GameContent content, string at)
    {
        if (!content.Weapons.TryGet(row.Id, out var def))
            throw new SaveFormatException($"{at}.id", $"'{row.Id}' is no weapon in the content");

        var forged = Modifiers(row.Forged, $"{at}.forged", _ => (ContentBounds.DeepestForge, DeepestForgeWhy));
        var live = Modifiers(row.Modifiers, $"{at}.modifiers", type => (forged.Stacks(type) + ContentBounds.MostAcquired, AcquiredWhy(forged.Stacks(type))));
        foreach (var (type, stacks) in forged.Entries)
            if (live.Stacks(type) < stacks)
                throw new SaveFormatException($"{at}.modifiers.{Name(type)}",
                    $"{live.Stacks(type)} is below the {stacks} forged: the live spread is the forged one plus what was acquired, never less");

        var rows = Rows(row.Enchantments, $"{at}.enchantments", "enchantment");
        if (rows.Count > ContentBounds.MostEnchantments)
            throw new SaveFormatException($"{at}.enchantments",
                $"{rows.Count} entries, more than the {ContentBounds.MostEnchantments} a weapon can hold: none arrives with more than {ContentValidator.MostArrivingEnchantments}, and the enchanter fills {ContentBounds.MostEnchantments} slots at most");
        var entries = new Enchantment[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            string where = $"{at}.enchantments[{i}]";
            var entry = rows[i];
            if (!content.Enchantments.TryGet(entry.Id, out var entryDef))
                throw new SaveFormatException($"{where}.id", $"'{entry.Id}' is no enchantment in the content");
            if (entry.Tier < 1)
                throw new SaveFormatException($"{where}.tier", $"{entry.Tier} is no tier: an entry is attached at tier 1 or deeper");
            AtMost(entry.Tier, ContentBounds.DeepestTier, $"{where}.tier", DeepestTierWhy);
            // The content's own def — the tuned one — so a unique's pin holds whatever the text says.
            entries[i] = new Enchantment(entryDef, entry.Tier) { Xp = Count(entry.Xp, $"{where}.xp") };
        }

        if (row.ForgedEnchantments < 0 || row.ForgedEnchantments > entries.Length)
            throw new SaveFormatException($"{at}.forgedEnchantments",
                $"{row.ForgedEnchantments} of {entries.Length} entries: the forged ones are a prefix of the list");

        // The wear banked is held to a ceiling no tuning in range can move, as a
        // member's hit points are (Body): a capacity opens on a bar no larger than
        // Bar and grows at most MostCapacityPerBar a point, each point costing a
        // bar of one wear cashed or more, and accrual never banks past capacity.
        // Summed as a long, so wear cashed at its own ceiling cannot wrap it. Wear
        // over today's capacity is not refused: capacity is today's tuning's.
        int wear = Count(row.Wear, $"{at}.wear");
        int wearCashed = Count(row.WearCashed, $"{at}.wearCashed");
        AtMost(wear, ContentBounds.Bar + (long)ContentBounds.MostCapacityPerBar * wearCashed, $"{at}.wear", WearWhy);

        return new Weapon(def, forged, live, entries, row.ForgedEnchantments, wear, wearCashed);
    }

    /// <summary>
    /// A modifier table by name: every key a <see cref="ModifierType"/>, every
    /// count whole, not negative, and no deeper than <paramref name="ceiling"/>
    /// gives for its modifier — the most it can be, and why.
    /// </summary>
    private static ModifierSet Modifiers(IReadOnlyDictionary<string, int>? table, string at, Func<ModifierType, (int Most, string Why)> ceiling)
    {
        var stacks = new List<(ModifierType, int)>();
        foreach (var (name, count) in Required(table, at))
        {
            var type = EnumOf<ModifierType>(name, $"{at}.{name}", "modifier");
            var (most, why) = ceiling(type);
            stacks.Add((type, AtMost(NotNegative(count, $"{at}.{name}"), most, $"{at}.{name}", why)));
        }
        return ModifierSet.Of(stacks.ToArray());
    }

    /// <summary>The locks, each on a dungeon the content has, each dungeon once, each from its floor alone (<see cref="LockedUntil"/>).</summary>
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
            locks[i] = (row.DungeonId, LockedUntil(row.LockedUntilRun, $"{where}.lockedUntilRun"));
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

    /// <summary>
    /// How the last run ended, as its summary reads it: the dungeon and the
    /// members it names are the content's and the party's, its floors and its
    /// counts in the range play writes them, its spoils weapons like any other.
    /// </summary>
    private static RunResult Result(RunResultDto row, GameContent content, IReadOnlyList<PartyMemberState> party, string at)
    {
        if (!content.Dungeons.TryGet(row.DungeonId, out _))
            throw new SaveFormatException($"{at}.dungeonId", $"'{row.DungeonId}' is no dungeon in the content");
        var outcome = EnumOf<RunOutcome>(row.Outcome, $"{at}.outcome", "run outcome");

        // The depth reached and the boss floor are floor numbers, the first floor
        // 1 (RunResult); the counter counts runs as the campaign's own does, held
        // to the campaign's ceiling, and the lock is a lock like the campaign's,
        // from its floor alone. How deep the party went, and the lock against the
        // counter, are asked of the rest of the summary with the other cross-checks
        // (SaveConsistency).
        int deepest = FloorNumber(row.DeepestFloor, $"{at}.deepestFloor");
        int? bossFloor = row.BossFloorRevealed is { } revealed ? FloorNumber(revealed, $"{at}.bossFloorRevealed") : null;
        int runCounter = Runs(row.RunCounter, $"{at}.runCounter");
        int? lockedUntil = row.LockedUntilRun is { } until ? LockedUntil(until, $"{at}.lockedUntilRun") : null;

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
            taken[i] = new XpClawback(clawback.MemberId, clawback.Pool, Count(clawback.Before, $"{where}.before"), NotNegative(clawback.After, $"{where}.after"));
        }

        return new RunResult(row.DungeonId, outcome, row.BossDefeated, deepest, bossFloor, row.ServiceTick, row.MercyTick,
            runCounter, lockedUntil, row.Retired, Spoils(row.SpoilsKept, $"{at}.spoilsKept"), Spoils(row.SpoilsLost, $"{at}.spoilsLost"), taken);
    }

    // ── Restore: the visit in progress ───────────────────────────────────────

    /// <summary>
    /// The deepest floor any content that loads can put a boss on —
    /// <c>BossFloorMax</c>'s clamp ceiling, which a pinned floor meets too
    /// (<see cref="ContentBounds.DeepestBossFloor"/>, the one accessor the
    /// content's pinned floors are held to, R3(a)): a tuning names no deeper
    /// floor, and a dungeon pins none. A save naming a deeper boss names a dungeon
    /// no content could have made — and would have a load build every floor of it.
    /// </summary>
    private static int DeepestBossFloor => ContentBounds.DeepestBossFloor;

    /// <summary>
    /// The visit a document's <c>campaign.visit</c> describes, on the restored
    /// party: the Run group's numbers as the save has them — the depth and the
    /// promise never rolled or re-derived — every visited floor stood back on its
    /// regenerated terrain with its saved rows (<see cref="Floor.Restore"/>), every
    /// floor below the deepest reached generated from the seed, the depth and the
    /// promise as an entry would (<see cref="Floor.Generate"/>) — placing enemies
    /// only there, the floors nobody has stood on — and then the spoils and the
    /// turn's counters, resolved against the bags and floors now rebuilt; and the
    /// visit's die at the count it had rolled, so it rolls on from there.
    /// </summary>
    private static DungeonState Visit(VisitDto row, GameContent content, PartyMemberState[] party, string at)
    {
        if (!content.Dungeons.TryGet(row.DungeonId, out var def))
            throw new SaveFormatException($"{at}.dungeonId", $"'{row.DungeonId}' is no dungeon in the content");
        if (row.BossFloor < 1 || row.BossFloor > DeepestBossFloor)
            throw new SaveFormatException($"{at}.bossFloor", $"{row.BossFloor} is no boss floor: a boss waits on floor 1 to {DeepestBossFloor}");
        NotNegative(row.PromisedStacks, $"{at}.promisedStacks");
        Count(row.Turn, $"{at}.turn");
        Count(row.DiceRolled, $"{at}.diceRolled");
        // The map seed is any long by design: every floor's seed and every stream
        // is mixed from it, so no value is out of range — a seed whose terrain the
        // saved rows and items do not stand on is refused by the floors it
        // rebuilds (Floor.Restore).
        if (row.DeepestFloorIndex < 0 || row.DeepestFloorIndex >= row.BossFloor)
            throw new SaveFormatException($"{at}.deepestFloorIndex",
                $"{row.DeepestFloorIndex} is no floor of a dungeon {row.BossFloor} deep: a floor index runs 0 to {row.BossFloor - 1}");
        if (row.FloorIndex < 0 || row.FloorIndex > row.DeepestFloorIndex)
            throw new SaveFormatException($"{at}.floorIndex",
                $"{row.FloorIndex} is no floor the party has reached: it has stood on floors 0 to {row.DeepestFloorIndex}");
        if (row.BossDefeated ? row.BossDefeatedAtTurn < 0 || row.BossDefeatedAtTurn > row.Turn : row.BossDefeatedAtTurn != -1)
            throw new SaveFormatException($"{at}.bossDefeatedAtTurn", row.BossDefeated
                ? $"{row.BossDefeatedAtTurn}: the boss fell on a turn the visit has had, 0 to {row.Turn}"
                : $"{row.BossDefeatedAtTurn}: a boss still standing fell at no turn (-1)");
        if (row.BossDefeated && row.DeepestFloorIndex != row.BossFloor - 1)
            throw new SaveFormatException($"{at}.bossDefeated",
                $"the boss fell on floor {row.BossFloor}, and the party has been no deeper than floor {row.DeepestFloorIndex + 1}");

        var snapshot = Snapshot(Required(row.EntrySnapshot, $"{at}.entrySnapshot"), party, $"{at}.entrySnapshot");

        var saved = Rows(row.Floors, $"{at}.floors", "floor");
        if (saved.Count != row.DeepestFloorIndex + 1)
            throw new SaveFormatException($"{at}.floors",
                $"{saved.Count} floors: a visit saves every floor it has stood on — 0 to {row.DeepestFloorIndex}, floors being adjacent — and no other");
        var floors = new Floor[row.BossFloor];
        for (int n = 0; n < saved.Count; n++)
            floors[n] = FloorOf(saved[n], n, row, def, content, $"{at}.floors[{n}]");
        for (int n = saved.Count; n < floors.Length; n++)
            floors[n] = Floor.Generate(row.MapSeed, n, bottom: n == floors.Length - 1, def, row.PromisedStacks);   // never stood on: placed as an entry places it

        var spoils = Spoils(row.Spoils, party, floors, row.DeepestFloorIndex, $"{at}.spoils");
        var carry = row.Carry is { } counters ? Carry(counters, party, floors, row, $"{at}.carry") : null;

        return DungeonState.Restore(def, row.MapSeed, row.BossFloor, row.PromisedStacks, snapshot, floors, row.FloorIndex, row.DeepestFloorIndex,
            row.Turn, row.BossDefeated, row.BossDefeatedAtTurn, carry, spoils, row.DiceRolled);
    }

    /// <summary>The party's XP at the door: the party's members, each once, in roster order, every pool a count play could have written.</summary>
    private static XpSnapshot Snapshot(XpSnapshotDto row, IReadOnlyList<PartyMemberState> party, string at)
    {
        var members = Rows(row.Members, $"{at}.members", "member");
        if (members.Count != party.Count)
            throw new SaveFormatException($"{at}.members", $"{members.Count} members: the party at the door is the party, {party.Count} of them");
        var classes = Enum.GetValues<WeaponClass>();
        var pools = new MemberXp[members.Count];
        for (int i = 0; i < members.Count; i++)
        {
            string where = $"{at}.members[{i}]";
            var member = members[i];
            if (member.MemberId != party[i].Id)
                throw new SaveFormatException($"{where}.memberId", $"'{member.MemberId}' where the party's member {i} is '{party[i].Id}': the snapshot holds the party in roster order");
            var weaponXp = new int[classes.Length];
            foreach (var (name, xp) in Required(member.WeaponXp, $"{where}.weaponXp"))
                weaponXp[Array.IndexOf(classes, EnumOf<WeaponClass>(name, $"{where}.weaponXp.{name}", "weapon class"))] = Count(xp, $"{where}.weaponXp.{name}");
            pools[i] = new MemberXp(member.MemberId, Array.AsReadOnly(weaponXp), Count(member.HpXp, $"{where}.hpXp"), Count(member.ManaXp, $"{where}.manaXp"));
        }
        return new XpSnapshot(pools);
    }

    /// <summary>
    /// A visited floor: its rows — each spawn index once, the boss's on the bottom
    /// floor and nowhere else — its fog, the current floor's sight and nobody
    /// else's, and its loose items, stood on the terrain the seed regenerates.
    /// </summary>
    private static Floor FloorOf(FloorDto row, int n, VisitDto visit, DungeonDef def, GameContent content, string at)
    {
        if (row.Index != n)
            throw new SaveFormatException($"{at}.index", $"{row.Index}: the floors are saved in order from the first, and this is floor index {n}");
        bool bottom = n == visit.BossFloor - 1;
        bool current = n == visit.FloorIndex;

        var saved = Rows(row.Enemies, $"{at}.enemies", "enemy");
        var rows = new EnemyState[saved.Count];
        var spawns = new HashSet<int>();
        EnemyState? boss = null;
        for (int i = 0; i < saved.Count; i++)
        {
            string where = $"{at}.enemies[{i}]";
            var enemy = saved[i];
            NotNegative(enemy.SpawnIndex, $"{where}.spawnIndex");
            if (enemy.SpawnIndex >= saved.Count)
                throw new SaveFormatException($"{where}.spawnIndex",
                    $"{enemy.SpawnIndex} is above {saved.Count - 1}: a floor numbers its rows from 0 in the order it placed them, the boss last, and never loses one");
            if (!spawns.Add(enemy.SpawnIndex))
                throw new SaveFormatException($"{where}.spawnIndex", $"{enemy.SpawnIndex} is already a row of this floor: each row has its own spawn index");
            if (enemy.Boss && !bottom)
                throw new SaveFormatException($"{where}.boss", $"floor {n + 1} is not the bottom of a dungeon {visit.BossFloor} deep: only the bottom floor holds the boss");
            if (enemy.Boss && boss is not null)
                throw new SaveFormatException($"{where}.boss", "the floor's boss is already saved: a dungeon has one boss");
            rows[i] = Enemy(enemy, def, visit, content, where);
            if (enemy.Boss)
                boss = rows[i];
        }
        if (bottom && boss is null)
            throw new SaveFormatException($"{at}.enemies", $"floor {n + 1} is the bottom, and no row is its boss: the bottom floor holds the dungeon's one boss");

        var seen = FogRows(row.Seen, $"{at}.seen");
        IReadOnlyList<string>? visible = null;
        if (row.Visible is { } sight)
        {
            if (!current)
                throw new SaveFormatException($"{at}.visible",
                    $"floor {n + 1} is not the floor the party stands on, {visit.FloorIndex + 1}: only the current floor's sight is saved, and an arrival resets every other's");
            visible = FogRows(sight, $"{at}.visible");
        }

        var items = Rows(row.LooseItems, $"{at}.looseItems", "loose item");
        var loose = new LooseItem[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            string where = $"{at}.looseItems[{i}]";
            var item = items[i];
            if (item.Row < 0 || item.Row >= MapGenerator.Rows || item.Col < 0 || item.Col >= MapGenerator.Cols)
                throw new SaveFormatException(where, $"({item.Row}, {item.Col}) is no tile of a map {MapGenerator.Rows} by {MapGenerator.Cols}");
            loose[i] = new LooseItem(Weapon(Required(item.Weapon, $"{where}.weapon"), content, $"{where}.weapon"), (item.Row, item.Col));
        }

        try
        {
            return Floor.Restore(visit.MapSeed, n, bottom, rows, boss, loose, seen, visible);
        }
        catch (ArgumentException refused)
        {
            throw new SaveFormatException(at, refused.Message.Split(" (Parameter", 2)[0], refused);   // a loose item on a wall: only the map knows, once it is built
        }
    }

    /// <summary>
    /// One enemy row: what it holds by id, its body and the farm's numbers as
    /// saved; its statline, name, movement, theme and attunement from its dungeon
    /// — a dummy's the rank and file's, the boss's its dungeon's boss's
    /// (<see cref="DungeonState.BossRow"/>), carrying the visit's promise. What it
    /// lies on is pinned (<see cref="EnemyState.PinnedDrop"/>), and only a row that
    /// lies on a drop may say it does.
    /// </summary>
    private static EnemyState Enemy(EnemyDto row, DungeonDef def, VisitDto visit, GameContent content, string at)
    {
        var weapon = Weapon(Required(row.Weapon, $"{at}.weapon"), content, $"{at}.weapon");
        var enemy = row.Boss
            ? DungeonState.BossRow(def, weapon, visit.PromisedStacks)   // the statline the content's, the fields the save's
            : new EnemyState { Weapon = weapon, Attunement = def.Attunement, Theme = def.Theme };
        enemy.SpawnIndex = row.SpawnIndex;
        var body = Required(row.Body, $"{at}.body");
        // A row's maximum is its base — a dummy's, or its boss's, none above Bar —
        // plus what its revivals added (a negative gain is refused below, at its
        // own path, and adds nothing here), summed as a long.
        Body(enemy, body, $"{at}.body", ContentBounds.Bar + Math.Max(0L, row.RevivalMaxHp), RowHpWhy);

        if (row.DefeatedAtTurn < -1 || row.DefeatedAtTurn > visit.Turn)
            throw new SaveFormatException($"{at}.defeatedAtTurn", $"{row.DefeatedAtTurn}: a row falls on a turn the visit has had, 0 to {visit.Turn}, or never (-1)");
        enemy.DefeatedAtTurn = row.DefeatedAtTurn;
        enemy.TurnsSinceSeen = Count(row.TurnsSinceSeen, $"{at}.turnsSinceSeen");
        enemy.DefeatCount = Count(row.DefeatCount, $"{at}.defeatCount");
        if (row.RevivalsRolled < 0 || row.RevivalsRolled > row.DefeatCount)
            throw new SaveFormatException($"{at}.revivalsRolled", $"{row.RevivalsRolled}: the cycles rolled run 0 to the defeats, {row.DefeatCount}");
        enemy.RevivalsRolled = row.RevivalsRolled;
        enemy.RevivalDamage = Count(row.RevivalDamage, $"{at}.revivalDamage");
        enemy.RevivalMaxHp = Count(row.RevivalMaxHp, $"{at}.revivalMaxHp");
        enemy.DropTaken = row.DropTaken;

        if (row.GroundItem is { } lying)
        {
            if (!visit.BossDefeated || body.Alive || row.DropTaken)
                throw new SaveFormatException($"{at}.groundItem", !visit.BossDefeated
                    ? "the boss stands, so nothing is handed over: no corpse lies on a drop while the dungeon still resurrects"
                    : body.Alive ? "the row is standing: only a corpse lies on a drop" : "the drop was taken: a corpse lies on it no more");
            // A dummy's drop was rolled off its farm depth, which no longer moves once the boss is down; the boss's own drop is always depth 0.
            enemy.PinnedDrop = new Drop(Weapon(Required(lying.Weapon, $"{at}.groundItem.weapon"), content, $"{at}.groundItem.weapon"),
                row.Boss ? 0 : row.DefeatCount, lying.WasUniqueRoll);
        }
        return enemy;
    }

    /// <summary>A fog grid in <see cref="FogState.Encode"/>'s encoding: one row per map row, each a map row long, nothing but the two characters.</summary>
    private static IReadOnlyList<string> FogRows(IReadOnlyList<string>? lines, string at)
    {
        var rows = Rows(lines, at, "fog row");
        if (rows.Count != MapGenerator.Rows)
            throw new SaveFormatException(at, $"{rows.Count} rows: a map has {MapGenerator.Rows}");
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Length != MapGenerator.Cols)
                throw new SaveFormatException($"{at}[{r}]", $"{rows[r].Length} tiles: a map row is {MapGenerator.Cols}");
            int other = rows[r].AsSpan().IndexOfAnyExcept(FogState.SetTile, FogState.ClearTile);
            if (other >= 0)
                throw new SaveFormatException($"{at}[{r}]", $"'{rows[r][other]}' at column {other}: a tile is '{FogState.SetTile}' or '{FogState.ClearTile}'");
        }
        return rows;
    }

    /// <summary>
    /// The spoils, each the very weapon its locator names now that the bags and
    /// the floors are rebuilt: a member's bag slot holding a weapon, or a visited
    /// floor's loose item — one or the other, never both — and no weapon twice.
    /// </summary>
    private static Weapon[] Spoils(IReadOnlyList<SpoilDto> dtos, IReadOnlyList<PartyMemberState> party, Floor[] floors, int deepest, string at)
    {
        var rows = Rows(dtos, at, "spoil");
        var spoils = new Weapon[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            string where = $"{at}[{i}]";
            var spoil = rows[i];
            bool inBag = spoil.Member is not null || spoil.Slot is not null;
            bool lying = spoil.Floor is not null || spoil.Loose is not null;
            if (inBag == lying)
                throw new SaveFormatException(where, "a spoil is in a bag — member and slot — or lying on a floor — floor and loose — and exactly one");

            Weapon found;
            if (inBag)
            {
                string id = spoil.Member ?? throw new SaveFormatException($"{where}.member", "missing: a spoil in a bag names the member carrying it");
                int slot = spoil.Slot ?? throw new SaveFormatException($"{where}.slot", "missing: a spoil in a bag names its slot");
                var member = party.FirstOrDefault(m => m.Id == id)
                    ?? throw new SaveFormatException($"{where}.member", $"'{id}' is no member of the party");
                if (slot < 0 || slot >= member.Inventory.Length)
                    throw new SaveFormatException($"{where}.slot", $"{slot} is no slot of member '{id}', who carries 0 to {member.Inventory.Length - 1}");
                found = member.Inventory[slot]
                    ?? throw new SaveFormatException($"{where}.slot", $"slot {slot} of member '{id}' is empty: a spoil is a weapon, carried or lying somewhere");
            }
            else
            {
                int floor = spoil.Floor ?? throw new SaveFormatException($"{where}.floor", "missing: a spoil lying on a floor names the floor");
                int loose = spoil.Loose ?? throw new SaveFormatException($"{where}.loose", "missing: a spoil lying on a floor names its place among the loose items");
                if (floor < 0 || floor > deepest)
                    throw new SaveFormatException($"{where}.floor", $"{floor} is no floor the party has stood on: 0 to {deepest}");
                var items = floors[floor].LooseItems;
                if (loose < 0 || loose >= items.Count)
                    throw new SaveFormatException($"{where}.loose", $"{loose}: floor {floor + 1} has {items.Count} loose items");
                found = items[loose].Weapon;
            }

            for (int earlier = 0; earlier < i; earlier++)
                if (ReferenceEquals(spoils[earlier], found))
                    throw new SaveFormatException(where, $"names the weapon spoil {earlier} does: a weapon is one spoil, once");
            spoils[i] = found;
        }
        return spoils;
    }

    /// <summary>
    /// The turn's counters, this turn's alone: every actor a member by id or a
    /// visited floor's row by spawn index, each named once in each list (once per
    /// modifier among the reaction uses), and only enemies among the seen.
    /// </summary>
    private static TurnCarry Carry(CarryDto row, IReadOnlyList<PartyMemberState> party, Floor[] floors, VisitDto visit, string at)
    {
        if (row.Turn != visit.Turn)
            throw new SaveFormatException($"{at}.turn", $"{row.Turn}: a visit carries this turn's counters alone, and its clock stands at {visit.Turn}");

        ActorState Actor(ActorRefDto? actor, string where)
        {
            var named = Required(actor, where);
            bool member = named.Member is not null;
            bool enemy = named.Floor is not null || named.Spawn is not null;
            if (member == enemy)
                throw new SaveFormatException(where, "an actor is a member — by roster id — or an enemy — by floor and spawn index — and exactly one");
            if (member)
                return party.FirstOrDefault(m => m.Id == named.Member)
                    ?? throw new SaveFormatException($"{where}.member", $"'{named.Member}' is no member of the party");

            int floor = named.Floor ?? throw new SaveFormatException($"{where}.floor", "missing: an enemy is named by its floor and its spawn index");
            int spawn = named.Spawn ?? throw new SaveFormatException($"{where}.spawn", "missing: an enemy is named by its floor and its spawn index");
            if (floor < 0 || floor > visit.DeepestFloorIndex)
                throw new SaveFormatException($"{where}.floor", $"{floor} is no floor the party has stood on: 0 to {visit.DeepestFloorIndex}");
            return floors[floor].Enemies.FirstOrDefault(e => e.SpawnIndex == spawn)
                ?? throw new SaveFormatException($"{where}.spawn", $"floor {floor + 1} has no row with spawn index {spawn}");
        }

        Dictionary<ActorState, int> Counts(IReadOnlyList<CountDto> dtos, string where)
        {
            var counts = new Dictionary<ActorState, int>(ReferenceEqualityComparer.Instance);
            var rows = Rows(dtos, where, "count");
            for (int i = 0; i < rows.Count; i++)
            {
                var actor = Actor(rows[i].Actor, $"{where}[{i}].actor");
                if (!counts.TryAdd(actor, Count(rows[i].Count, $"{where}[{i}].count")))
                    throw new SaveFormatException($"{where}[{i}].actor", "already counted: an actor is named once");
            }
            return counts;
        }

        var reactions = new Dictionary<(ActorState Actor, ModifierType Type), int>();
        var uses = Rows(row.ReactionUses, $"{at}.reactionUses", "reaction use");
        for (int i = 0; i < uses.Count; i++)
        {
            string where = $"{at}.reactionUses[{i}]";
            var actor = Actor(uses[i].Actor, $"{where}.actor");
            var type = EnumOf<ModifierType>(uses[i].Modifier, $"{where}.modifier", "modifier");
            if (!reactions.TryAdd((actor, type), Count(uses[i].Count, $"{where}.count")))
                throw new SaveFormatException(where, $"{type} is already counted for this actor: an actor's reaction is named once");
        }

        var seen = new HashSet<EnemyState>(ReferenceEqualityComparer.Instance);
        var sighted = Rows(row.Seen, $"{at}.seen", "actor");
        for (int i = 0; i < sighted.Count; i++)
        {
            string where = $"{at}.seen[{i}]";
            if (Actor(sighted[i], where) is not EnemyState enemy)
                throw new SaveFormatException(where, "a member: the seen are the enemies a member has laid eyes on");
            if (!seen.Add(enemy))
                throw new SaveFormatException(where, "already seen: an enemy is named once");
        }

        return new TurnCarry(row.Turn, Counts(row.Attacks, $"{at}.attacks"), Counts(row.ZoneUses, $"{at}.zoneUses"), reactions, seen);
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

    /// <summary>
    /// A count no larger than <paramref name="most"/>, a ceiling off
    /// <see cref="ContentBounds"/> (R3(b)): past it the arithmetic that reads the
    /// count would wrap, and no play writes it. Refused at the count's own path,
    /// the value and the ceiling in the first clause — what the notice of a save
    /// set aside shows — and <paramref name="why"/> after it, in the HUD's font.
    /// </summary>
    private static int AtMost(int value, long most, string at, string why)
        => value <= most ? value : throw new SaveFormatException(at, $"{value} is above {most}: {why}");

    /// <summary>
    /// A count play grows a bounded step at a time — a turn, a die, a defeat, an
    /// XP gain — not negative, and no larger than <see cref="ContentBounds.MostCount"/>,
    /// the sanity bound below which the next step cannot wrap.
    /// </summary>
    private static int Count(int value, string at)
        => AtMost(NotNegative(value, at), ContentBounds.MostCount, at, CountWhy);

    /// <summary>
    /// A float play writes finite: never infinite and never not a number, which
    /// the next save could not write — JSON has no word for either — asked before
    /// any range, so the refusal says which it is.
    /// </summary>
    private static void Finite(float value, string at)
    {
        if (!float.IsFinite(value))   // named in words: the HUD's font has no glyph for infinity
            throw new SaveFormatException(at, $"{(float.IsNaN(value) ? "not a number" : value > 0 ? "infinity" : "minus infinity")} is no number play writes: a position or a budget is a finite number");
    }

    /// <summary>Where a body stands on one axis, in logic units: on the map, from its near edge at 0 to its far edge <paramref name="tiles"/> tiles on.</summary>
    private static float Coordinate(float value, int tiles, string at)
    {
        Finite(value, at);
        float most = tiles * GameConstants.Tile;
        return value is >= 0f && value <= most
            ? value
            : throw new SaveFormatException(at, $"{value} is off the map, 0 to {most}: a map is {tiles} tiles of {GameConstants.Tile} that way, and a body stands on it");
    }

    /// <summary>A movement budget, in logic units: finite, not negative, and no more than <paramref name="most"/>, and why.</summary>
    private static float Movement(float value, float most, string at, string why)
    {
        Finite(value, at);
        if (value < 0f)
            throw new SaveFormatException(at, $"{value} is below 0: movement is spent to nothing at most");
        return value <= most ? value : throw new SaveFormatException(at, $"{value} is above {most}: {why}");
    }

    /// <summary>
    /// The run counter — the campaign's, or its last run's copy of it — not
    /// negative, and below <see cref="ContentBounds.MostRuns"/>: a run's end adds
    /// one to it and locks a clear until it plus up to 99 × 99 runs, so from there
    /// on either could wrap.
    /// </summary>
    private static int Runs(int value, string at)
        => AtMost(NotNegative(value, at), ContentBounds.MostRuns - 1L, at,
            "at the end of a run the counter, and the lock a clear writes on it, would pass the largest whole number - and no player counts two billion runs");

    /// <summary>
    /// A lock — the run a cleared dungeon is locked until, the campaign's or its
    /// last run's — not negative, and with no ceiling of its own: nothing reads a
    /// lock but against the counter, so no lock wraps anything. Entering and the
    /// list compare the two (<see cref="CampaignState.IsSelectable"/>,
    /// <see cref="CampaignState.EarlyOffer"/>), and the two countdowns take the
    /// counter from a lock above it — the list's
    /// (<see cref="DungeonListing.RunsUntilAvailable"/>, for a dungeon not yet
    /// selectable) and the summary's, whose lock <see cref="SaveConsistency"/>
    /// holds at or above its own counter. And a clear writes the counter plus up to
    /// 99 × 99 runs, past the counter's own ceiling whenever the counter is that
    /// close to it, so a lock held to that ceiling would refuse a save play wrote.
    /// </summary>
    private static int LockedUntil(int value, string at) => NotNegative(value, at);

    // Why each ceiling is where it is: said after the value and the ceiling, so in
    // the HUD's font — no apostrophe, quote, brace, backslash or semicolon.

    private const string DeepestForgeWhy = "no forge goes deeper (MaxForged at its most)";

    private const string DeepestStackWhy = "no stack grows deeper than the deepest forge plus the most headroom (MaxForged and AcquiredHeadroom at their most)";

    private const string CountWhy = "a count play grows a turn or a gain at a time never nears it, and past it the next step would pass the largest whole number";

    private const string LevelsWhy = "levels are uncapped and land a hit at a time, but past it the levels times their largest effect (EffectPerLevel at its most) would pass the largest whole number";

    private const string HeldShotsWhy = "a hold is the Overwatch value of the wielder, no more than its offset plus a stack worth for the deepest stack (Offset, PerStack, MaxForged and AcquiredHeadroom at their most)";

    private const string BudgetBeforeMireWhy = "a turn opens on the budget plus the bank, at most half the budget again, and no budget is longer than tuning.json measures (MovementBudget at its most)";

    private const string SavedMovementWhy = "a turn banks half its budget at most, and no budget is longer than tuning.json measures (MovementBudget at its most)";

    private const string DeepestTierWhy = "tier is uncapped and play never climbs near this, but past it the lock and the levels of the entry would pass the largest whole number";

    private static readonly string MemberHpWhy =
        $"a member holds no more hit points than the largest bar a pool opens on, {ContentBounds.Bar} (StartingPool at its most), plus a point for each HP XP";

    private static readonly string RowHpWhy =
        $"a row holds no more hit points than the largest bar a pool opens on, {ContentBounds.Bar} (StartingPool at its most), plus what its revivals added";

    private static readonly string WearWhy =
        $"a weapon banks no more wear than its capacity, which opens on the largest bar a pool opens on, {ContentBounds.Bar} (StartingPool at its most), and grows {ContentBounds.MostCapacityPerBar} at most (WearCapacityPerBar at its most) for every full bar cashed, each bar one wear or more";

    /// <summary>Why a live stack stops at its forged count plus the most headroom any tuning gives.</summary>
    private static string AcquiredWhy(int forged)
        => $"acquisition stops at the {forged} forged plus the most headroom any tuning gives, {ContentBounds.MostAcquired} (AcquiredHeadroom at its most)";

    /// <summary>A floor by the number a run's summary gives it: the party starts on floor 1, and a boss floor range starts there too.</summary>
    private static int FloorNumber(int value, string at)
        => value >= 1 ? value : throw new SaveFormatException(at, $"{value} is no floor: floors are numbered from 1");

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
