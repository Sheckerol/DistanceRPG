using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.4's run's end: three outcomes, not two. A boss kill carries its loot home
/// and keeps its XP; a retreat keeps its XP and has no loot to keep; a death
/// loses the loot and claws back a share of each XP pool's gain since the entry
/// snapshot. The run counts once (§6.3) — a success, or a mercy for failing deep
/// — and the counter it moves is the clock every cooldown is counted on: a clear
/// locks its dungeon for as many runs as its boss's floor, the tutorial retires
/// for good instead, and with every dungeon locked the one that lifts soonest is
/// offered early.
/// <para>
/// These run whole runs on real floors, through the campaign that holds them:
/// entered, walked down and back up by the stairs, and left by the first floor's
/// way out or ended by a wipe inside a turn system. Each seed's depth is pinned
/// by an assertion before a test leans on it. In the content collection, because
/// one test turns <see cref="Tuning.DeathXpLossPercent"/>.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class RunOutcomeTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The shipped seed: the tutorial's golem holds a Pinning Lance on floor 2, and floor 1's spawn 23 is a hatchet dummy.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>Seeds a dungeon on the tuning's 5–10 range rolls five, six, seven and ten floors on — every such dungeon alike, since the roll reads only the seed and the range.</summary>
    private const long FiveDeep = 4;
    private const long SixDeep = 1;
    private const long SevenDeep = 7;
    private const long TenDeep = 6;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static DungeonDef SpearHall => Dungeons["spear_hall"];

    /// <summary>A roll that neither crits nor fumbles.</summary>
    private static int Ten() => 10;

    /// <summary>A blow that goes through any Block a boss stands behind: a dummy in one swing, any boss in two.</summary>
    private static Weapon Sledge() => TestWeapons.Make("Sledge", range: 60, damage: 120, cost: 30);

    /// <summary>Four fixture members in roster order, each holding the weapon at its place — a Weakspot Stiletto where none is named.</summary>
    private static PartyMemberState[] Party(params Weapon[] held)
        => Ids.Select((id, i) => TestPools.Holding(id, i < held.Length ? held[i] : TestWeapons.Get("weakspot_stiletto"), colorIndex: i))
            .ToArray();

    /// <summary>A campaign of four fixture members, A swinging the sledge that beats any boss.</summary>
    private static CampaignState Campaign() => new(Party(Sledge()));

    private static (float X, float Y) Centre((int R, int C) tile) => (tile.C * Tile + Tile / 2f, tile.R * Tile + Tile / 2f);

    private static void Put(ActorState actor, (int R, int C) tile) => (actor.X, actor.Y) = Centre(tile);

    private static (int R, int C) TileOf(ActorState actor) => Displacer.TileOf(actor);

    private static HashSet<(int R, int C)> LivingEnemyTiles(Floor floor)
        => floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();

    /// <summary>The row at <paramref name="spawnIndex"/>, pinned to the weapon the seed gave it before anything leans on it.</summary>
    private static EnemyState Row(Floor floor, int spawnIndex, string weaponId)
    {
        var row = floor.Enemies[spawnIndex];
        Assert.Equal((spawnIndex, weaponId), (row.SpawnIndex, row.Weapon.Id));
        return row;
    }

    /// <summary>Every member's three pools as numbers: every class's weapon XP, HP XP, mana XP.</summary>
    private static (string Id, string Weapons, int Hp, int Mana)[] Xp(IEnumerable<PartyMemberState> party)
        => party.Select(m => (m.Id, string.Join(",", Enum.GetValues<WeaponClass>().Select(c => m.WeaponXp[c])), m.HpXp, m.ManaXp)).ToArray();

    /// <summary>Stand the party round <paramref name="tile"/> as a party that walked there would — around living enemies, never on them.</summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) tile)
    {
        var floor = visit.Current;
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, tile.R, tile.C, party.Count, LivingEnemyTiles(floor));
        for (int i = 0; i < party.Count; i++)
            Put(party[i], tiles[i]);
    }

    /// <summary>Gather at the down-stairs, take them, and open the floor below.</summary>
    private static TurnSystem Down(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        return visit.OpenTurns(party, Ten);
    }

    /// <summary>Gather at the up-stairs, take them, and open the floor above.</summary>
    private static TurnSystem Up(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.UpStairs);
        visit.Ascend(party);
        return visit.OpenTurns(party, Ten);
    }

    /// <summary>Back to the current floor's arrival room, where the placer puts nobody and nothing reaches: where the party waits turns out.</summary>
    private static void Home(DungeonState visit, IReadOnlyList<PartyMemberState> party) => Gather(visit, party, visit.Current.UpStairs);

    private static void Advance(TurnSystem turns, float seconds = 10f, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>End <paramref name="count"/> turns, each one run to the next player phase.</summary>
    private static void PassTurns(TurnSystem turns, int count)
    {
        for (int i = 0; i < count; i++)
        {
            turns.EndTurn();
            Advance(turns);
            Assert.Equal(TurnPhase.Player, turns.Phase);
        }
    }

    /// <summary>Stand <paramref name="actor"/> on a free floor tile beside <paramref name="target"/>: left, right, above, below.</summary>
    private static void StandBeside(Floor floor, ActorState actor, EnemyState target)
    {
        var (r, c) = TileOf(target);
        var taken = LivingEnemyTiles(floor);
        foreach (var tile in new[] { (R: r, C: c - 1), (R: r, C: c + 1), (R: r - 1, C: c), (R: r + 1, C: c) })
        {
            if (floor.Map.Grid[tile.R, tile.C] != 0 || taken.Contains(tile)) continue;
            Put(actor, tile);
            return;
        }
        Assert.Fail($"No free floor tile beside the enemy at {(r, c)}.");
    }

    /// <summary>Stand <paramref name="killer"/> beside <paramref name="target"/> and swing until it falls, through the floor's turn system.</summary>
    private static void Kill(TurnSystem turns, Floor floor, PartyMemberState killer, EnemyState target)
    {
        StandBeside(floor, killer, target);
        for (int swing = 0; target.Alive; swing++)
        {
            Assert.True(swing < 5, "the fixture's weapon never finished the enemy");
            Assert.True(turns.TryAttack(killer, target));
        }
    }

    /// <summary>Down to the bottom floor and put its boss down with A's sledge; the bottom floor's system is returned.</summary>
    private static TurnSystem BeatTheBoss(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        var turns = visit.Turns!;
        while (visit.Current.DownStairs is not null)
            turns = Down(visit, party);
        Kill(turns, visit.Current, party[0], visit.Current.Boss!);
        Assert.True(visit.BossDefeated);
        return turns;
    }

    /// <summary>Leave the one way there is: back up through every floor, gathered at the first floor's way out, and out.</summary>
    private static RunResult Exit(CampaignState campaign)
    {
        var visit = campaign.Visit!;
        var party = campaign.Party;
        while (visit.FloorIndex > 0)
            Up(visit, party);
        Gather(visit, party, visit.Current.UpStairs);
        return campaign.EndRun(RunEnding.Exited);
    }

    /// <summary>
    /// Wipe the party on <paramref name="turns"/>' floor, with no scene anywhere:
    /// every member at 1 HP carrying Poison 5, and the turn ended — the ticks at
    /// the party's turn end take the lot, the turn system's GameOver goes to the
    /// visit's Wiped, and the campaign ends the run. The result it raised.
    /// </summary>
    private static RunResult Wipe(CampaignState campaign, TurnSystem turns)
    {
        RunResult? ended = null;
        void Heard(RunResult result) => ended = result;
        campaign.RunEnded += Heard;
        foreach (var member in campaign.Party)
        {
            member.Hp = 1;
            member.ApplyStatus(StatusEffectType.Poison, null, 5);
        }
        turns.EndTurn();
        campaign.RunEnded -= Heard;

        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.All(campaign.Party, m => Assert.False(m.Alive));
        return ended ?? throw new Xunit.Sdk.XunitException("The wipe ended no run.");
    }

    /// <summary>Enter <paramref name="dungeonId"/>, beat its boss and walk out: a clear.</summary>
    private static RunResult Clear(CampaignState campaign, string dungeonId, long seed)
    {
        var visit = campaign.Enter(dungeonId, seed);
        visit.OpenTurns(campaign.Party, Ten);
        BeatTheBoss(visit, campaign.Party);
        var result = Exit(campaign);
        Assert.Equal(RunOutcome.BossKill, result.Outcome);
        return result;
    }

    /// <summary>Enter <paramref name="dungeonId"/> five floors deep, go three floors down and walk back out: a retreat that counts, as a mercy.</summary>
    private static RunResult DeepRetreat(CampaignState campaign, string dungeonId)
    {
        var visit = campaign.Enter(dungeonId, FiveDeep);
        Assert.Equal(5, visit.BossFloor);
        visit.OpenTurns(campaign.Party, Ten);
        Down(visit, campaign.Party);
        Down(visit, campaign.Party);
        var result = Exit(campaign);
        Assert.Equal((RunOutcome.Retreat, 3, true, true), (result.Outcome, result.DeepestFloor, result.ServiceTick, result.MercyTick));
        return result;
    }

    /// <summary>Enter <paramref name="dungeonId"/> and walk straight back out of the first floor: a retreat that counts nothing.</summary>
    private static RunResult ShallowRetreat(CampaignState campaign, string dungeonId)
    {
        campaign.Enter(dungeonId, FiveDeep).OpenTurns(campaign.Party, Ten);
        var result = Exit(campaign);
        Assert.Equal((RunOutcome.Retreat, 1, false), (result.Outcome, result.DeepestFloor, result.ServiceTick));
        return result;
    }

    // ── The three outcomes ───────────────────────────────────────────────────

    [Fact]
    public void ABossKillCarriesItsSpoilsHome_KeepsItsXp_AndTicksOnce()
    {
        // PHASE4-107, -030, -046: a boss kill is the one success. What the party
        // collected on the way out goes home in its bags, every point of XP the run
        // earned is kept, and the run ticks once — as a win, not a mercy — at any
        // depth: the tick rewards the achievement, the drop rewards the depth, so a
        // ten-floor clear ticks exactly as a five-floor one does.
        foreach (var (seed, floors) in new[] { (FiveDeep, 5), (TenDeep, 10) })
        {
            var campaign = Campaign();
            var party = campaign.Party;
            var visit = campaign.Enter("spear_hall", seed);
            Assert.Equal(floors, visit.BossFloor);
            visit.OpenTurns(party, Ten);
            Assert.Equal(0, campaign.RunCounter);   // entering counts nothing

            // The boss, and its own drop taken: a spoil, into B's bag behind its dagger.
            var bottom = BeatTheBoss(visit, party);
            var boss = visit.Current.Boss!;
            var drop = bottom.GroundItemOf(boss)!;
            Assert.Equal(1, Pickup.Take(campaign, boss, drop, party[1], visit));
            Assert.Same(drop.Weapon, Assert.Single(visit.Spoils));

            party[2].WeaponXp[Dagger] += 45;   // and XP earned on the run besides the kill's
            party[3].HpXp += 13;
            var earned = Xp(party);

            var result = Exit(campaign);
            Assert.Equal((RunOutcome.BossKill, true, floors, (int?)floors), (result.Outcome, result.BossDefeated, result.DeepestFloor, result.BossFloorRevealed));
            Assert.Same(drop.Weapon, Assert.Single(result.SpoilsKept));
            Assert.Empty(result.SpoilsLost);
            Assert.Same(drop.Weapon, party[1].Inventory[1]);   // carried home, where it was put
            Assert.Equal(earned, Xp(party));                  // every pool kept
            Assert.Empty(result.Clawbacks);

            Assert.Equal((true, false), (result.ServiceTick, result.MercyTick));   // one tick, a success
            Assert.Equal((1, 1), (result.RunCounter, campaign.RunCounter));
            Assert.Same(result, campaign.LastResult);
            Assert.Null(campaign.Visit);
        }
    }

    [Fact]
    public void ARetreatKeepsItsXp_AndTicksMercyOnlyFromFloorThree()
    {
        // PHASE4-107, -108, -026: out alive with the boss unfought is a retreat.
        // The progression earned along the way is real and stays real — retreat
        // and death share the mercy tick and never an XP outcome — and there is no
        // loot to keep, because nothing drops while the dungeon resurrects. It
        // counts only for having got three floors down (§6.3): floors 1 and 2 are
        // walking in and out; floor 3 is a fighting retreat. Standing on the boss
        // floor and leaving without the fight is the same legitimate retreat — the
        // mercy tick and the XP, no lock, the dungeon there to be entered again.
        Assert.Equal(3, GameContent.Current.Tuning.MercyFloor);
        var campaign = Campaign();
        var party = campaign.Party;
        foreach (var (depth, counts) in new[] { (1, false), (2, false), (3, true), (5, true) })
        {
            int before = campaign.RunCounter;
            var visit = campaign.Enter("spear_hall", FiveDeep);
            Assert.Equal(5, visit.BossFloor);   // 5 is the boss floor: the last case stands on it
            visit.OpenTurns(party, Ten);
            while (visit.FloorIndex + 1 < depth)
                Down(visit, party);
            Assert.Equal(depth == 5, visit.Current.Boss is { Alive: true });

            foreach (var member in party)
            {
                member.WeaponXp[Dagger] += 10;
                member.HpXp += 3;
                member.ManaXp += 1;
            }
            var earned = Xp(party);

            var result = Exit(campaign);
            Assert.Equal((RunOutcome.Retreat, false, depth), (result.Outcome, result.BossDefeated, result.DeepestFloor));
            Assert.Equal(earned, Xp(party));
            Assert.Empty(result.Clawbacks);
            Assert.Empty(result.SpoilsKept);
            Assert.Empty(result.SpoilsLost);
            Assert.Equal((counts, counts), (result.ServiceTick, result.MercyTick));
            Assert.Equal(before + (counts ? 1 : 0), campaign.RunCounter);
            Assert.Equal(((int?)null, false), (result.LockedUntilRun, result.Retired));
            Assert.True(campaign.IsSelectable(SpearHall));
        }
        Assert.Equal(2, campaign.RunCounter);
    }

    [Fact]
    public void ADeathLosesTheSpoils_AndClawsBackAShareOfEachPoolsGain()
    {
        // PHASE4-107, -111: a wipe forfeits the run's loot and rolls each XP pool
        // back by DeathXpLossPercent of its gain since the entry snapshot — the
        // pools as they stood at the door are never touched. Spoils, not gear:
        // what is taken is exactly this run's pickups still in a bag, from
        // wherever they sit, the hand included; nothing the party carried in.
        Assert.Equal(50, GameContent.Current.Tuning.DeathXpLossPercent);
        var tower = TestWeapons.Get("tower_guard");
        var party = Party(Sledge());
        party[3].Inventory[1] = tower;          // D carries a Tower Guard in: gear, never a spoil
        party[2].WeaponXp[Axe] = 200;           // and C has swung an axe before this run
        var campaign = new CampaignState(party);
        var visit = campaign.Enter("tutorial", GoldenSeed);
        var gear = party.Select(m => m.Inventory.ToArray()).ToArray();
        visit.OpenTurns(party, Ten);

        // The golem, and its drop into B's bag.
        var bottom = BeatTheBoss(visit, party);
        var golem = visit.Current.Boss!;
        var golemDrop = bottom.GroundItemOf(golem)!;
        Assert.Equal(1, Pickup.Take(campaign, golem, golemDrop, party[1], visit));

        // Back on the first floor a dummy put down for good, and its weapon taken
        // up in D's hand: D had put its dagger away in the bag.
        var back = Up(visit, party);
        var dummy = Row(visit.Current, 23, "hatchet");
        Kill(back, visit.Current, party[0], dummy);
        var dummyDrop = back.GroundItemOf(dummy)!;
        var dagger = party[3].Inventory[0]!;
        (party[3].Inventory[0], party[3].Inventory[2]) = (null, dagger);
        Assert.Equal(0, Pickup.Take(campaign, dummy, dummyDrop, party[3], visit));
        Assert.Equal(new[] { golemDrop.Weapon, dummyDrop.Weapon }, visit.Spoils);

        // What the run earned, on top of what the kills credited A.
        party[2].WeaponXp[Axe] += 45;
        party[1].HpXp += 13;
        party[3].ManaXp += 7;
        var entry = visit.EntrySnapshot;
        var atWipe = party.ToDictionary(m => m.Id, m => (Weapons: Enum.GetValues<WeaponClass>().Select(c => m.WeaponXp[c]).ToArray(), m.HpXp, m.ManaXp));

        var result = Wipe(campaign, back);
        Assert.Equal((RunOutcome.Death, true), (result.Outcome, result.BossDefeated));

        // The spoils, out of the bags: lost, in the order they were taken.
        Assert.Equal(new[] { golemDrop.Weapon, dummyDrop.Weapon }, result.SpoilsLost);
        Assert.Empty(result.SpoilsKept);
        Assert.Null(party[1].Inventory[1]);
        Assert.Null(party[3].Inventory[0]);
        Assert.DoesNotContain(party.SelectMany(m => m.Inventory), w => w is not null && visit.Spoils.Contains(w));

        // Every pre-run weapon where the party left it: the gear is not the loot.
        Assert.Same(gear[0][0], party[0].Inventory[0]);
        Assert.Same(gear[1][0], party[1].Inventory[0]);
        Assert.Same(tower, party[3].Inventory[1]);
        Assert.Same(dagger, party[3].Inventory[2]);

        // Every pool at half its gain gone, truncated, and never below the door.
        foreach (var member in party)
        {
            var then = entry.Members.Single(m => m.MemberId == member.Id);
            var now = atWipe[member.Id];
            int Kept(int before, int at) => at - (at - before) * 50 / 100;
            Assert.Equal(then.WeaponXp.Zip(now.Weapons, Kept), Enum.GetValues<WeaponClass>().Select(c => member.WeaponXp[c]));
            Assert.Equal((Kept(then.HpXp, now.HpXp), Kept(then.ManaXp, now.ManaXp)), (member.HpXp, member.ManaXp));
        }
        Assert.Equal(200 + 45 - 22, party[2].WeaponXp[Axe]);   // 45 gained, 22 taken: never the 200 C walked in with
        Assert.Contains(new XpClawback("C", "Axe", 245, 223), result.Clawbacks);
        Assert.Contains(result.Clawbacks, c => c.MemberId == "A" && c.Pool == "Dagger");   // the kills' XP, clawed too
        Assert.Contains(new XpClawback("B", XpSnapshot.HpPool, entry.Members[1].HpXp + 13, entry.Members[1].HpXp + 13 - 6), result.Clawbacks);
        Assert.Contains(new XpClawback("D", XpSnapshot.ManaPool, entry.Members[3].ManaXp + 7, entry.Members[3].ManaXp + 7 - 3), result.Clawbacks);

        // The run's end rests nobody; the next entry does, and it arms the hand the
        // wipe emptied from the bag: D walks into the next dungeon holding its Tower Guard.
        Assert.All(party, m => Assert.False(m.Alive));
        campaign.Enter("spear_hall", FiveDeep);
        Assert.Same(tower, party[3].Inventory[0]);
        Assert.Null(party[3].Inventory[1]);
        Assert.Same(dagger, party[3].Inventory[2]);
        Assert.All(party, m => Assert.True(m.Alive));
    }

    [Fact]
    public void TheClawbackIsDeathXpLossPercentOfTheGain()
    {
        // PHASE4-109: the loss is a tunable percentage of this run's gain in each
        // pool — weapon XP per class, HP XP and mana XP — never a hard revert. At
        // 100 death erases the run's progress, at 0 it is a pure loot forfeiture,
        // and between them the share is taken in whole points, truncated.
        foreach (int percent in new[] { 0, 50, 100 })
        {
            using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { DeathXpLossPercent = percent });
            var party = Party();
            var a = party[0];
            a.WeaponXp[Dagger] = 300;   // before the run: never clawed
            var campaign = new CampaignState(party);
            var visit = campaign.Enter("tutorial", GoldenSeed);
            var turns = visit.OpenTurns(party, Ten);
            var (hp, mana) = (a.HpXp, a.ManaXp);
            Assert.Equal((300, 0, hp, mana), (visit.EntrySnapshot.Members[0].WeaponXp[(int)Dagger], visit.EntrySnapshot.Members[0].WeaponXp[(int)Axe],
                visit.EntrySnapshot.Members[0].HpXp, visit.EntrySnapshot.Members[0].ManaXp));

            a.WeaponXp[Dagger] += 45;
            a.WeaponXp[Axe] += 7;
            a.HpXp += 13;
            a.ManaXp += 1;
            var result = Wipe(campaign, turns);

            int Kept(int before, int gain) => before + gain - gain * percent / 100;
            Assert.Equal((Kept(300, 45), Kept(0, 7), Kept(hp, 13), Kept(mana, 1)), (a.WeaponXp[Dagger], a.WeaponXp[Axe], a.HpXp, a.ManaXp));
            Assert.All(party.Skip(1), m => Assert.Equal(0, m.WeaponXp.Total));   // nobody else gained, so nobody else lost
            Assert.All(result.Clawbacks, c => Assert.Equal("A", c.MemberId));

            XpClawback[] expected = percent switch
            {
                0 => Array.Empty<XpClawback>(),
                50 =>
                [
                    new XpClawback("A", "Dagger", 345, 323),            // 45 gained, 22 taken: 22.5 truncated
                    new XpClawback("A", "Axe", 7, 4),                   // 7 gained, 3 taken
                    new XpClawback("A", XpSnapshot.HpPool, hp + 13, hp + 7),
                    // mana gained 1, and half of one point is none: no clawback to record
                ],
                _ =>
                [
                    new XpClawback("A", "Dagger", 345, 300),
                    new XpClawback("A", "Axe", 7, 0),
                    new XpClawback("A", XpSnapshot.HpPool, hp + 13, hp),
                    new XpClawback("A", XpSnapshot.ManaPool, mana + 1, mana),
                ],
            };
            Assert.Equal(expected, result.Clawbacks);
        }
    }

    [Fact]
    public void TheSnapshotIsTakenAtEntry_AndHoldsNoInnateStat()
    {
        // PHASE4-110, -113: the party's XP pools — weapon proficiency in every
        // class, mana, HP growth — are captured the moment the party steps in, on
        // the visit. A copy of numbers, so nothing the run does moves it; and
        // innate stats never move (§2.1), so there is nothing of them to take.
        var party = Party();
        party[0].WeaponXp[Dagger] = 120;
        party[1].WeaponXp[Staff] = 30;
        party[2].WeaponXp[Wand] = 5;
        var campaign = new CampaignState(party);
        var visit = campaign.Enter("tutorial", GoldenSeed);
        var snapshot = visit.EntrySnapshot;

        Assert.Equal(Ids, snapshot.Members.Select(m => m.MemberId));
        foreach (var (member, taken) in party.Zip(snapshot.Members))
        {
            Assert.Equal(Enum.GetValues<WeaponClass>().Length, taken.WeaponXp.Count);
            Assert.Equal(Enum.GetValues<WeaponClass>().Select(c => member.WeaponXp[c]), taken.WeaponXp);   // in WeaponClass order
            Assert.Equal((member.HpXp, member.ManaXp), (taken.HpXp, taken.ManaXp));
        }
        Assert.Equal((120, 30, 5), (snapshot.Members[0].WeaponXp[(int)Dagger], snapshot.Members[1].WeaponXp[(int)Staff], snapshot.Members[2].WeaponXp[(int)Wand]));

        // What the run does after the door moves nothing in it.
        var (hp, mana) = (party[0].HpXp, party[0].ManaXp);
        visit.OpenTurns(party, Ten);
        party[0].WeaponXp[Dagger] += 50;
        party[0].HpXp += 9;
        party[0].ManaXp += 4;
        Assert.Same(snapshot, visit.EntrySnapshot);
        Assert.Equal((120, hp, mana), (snapshot.Members[0].WeaponXp[(int)Dagger], snapshot.Members[0].HpXp, snapshot.Members[0].ManaXp));

        // Ids and XP integers, and nothing else: no innate spread in its shape.
        Assert.All(typeof(MemberXp).GetProperties(), p =>
            Assert.Contains(p.PropertyType, new[] { typeof(string), typeof(int), typeof(IReadOnlyList<int>) }));
        Assert.DoesNotContain(typeof(MemberXp).GetProperties(), p => p.PropertyType == typeof(InnateStats));
        Assert.All(typeof(XpSnapshot).GetProperties(), p => Assert.Equal(typeof(IReadOnlyList<MemberXp>), p.PropertyType));
    }

    [Fact]
    public void AnyOtherEndingDiscardsTheSnapshotUntouched()
    {
        // PHASE4-112: a boss kill and a retreat leave every pool where the run
        // took it — nothing clawed — and the snapshot goes with the visit. The next
        // entry takes its own, at the XP the party walked out with.
        var campaign = Campaign();
        var party = campaign.Party;

        foreach (bool beatTheBoss in new[] { false, true })
        {
            var visit = campaign.Enter("spear_hall", FiveDeep);
            var snapshot = visit.EntrySnapshot;
            var atTheDoor = snapshot.Members.Select(m => (m.MemberId, string.Join(",", m.WeaponXp), m.HpXp, m.ManaXp)).ToArray();
            visit.OpenTurns(party, Ten);
            if (beatTheBoss)
                BeatTheBoss(visit, party);
            party[1].WeaponXp[Spear] += 60;
            party[2].ManaXp += 11;
            var earned = Xp(party);

            var result = Exit(campaign);
            Assert.Equal(beatTheBoss ? RunOutcome.BossKill : RunOutcome.Retreat, result.Outcome);
            Assert.Equal(earned, Xp(party));
            Assert.Empty(result.Clawbacks);
            Assert.Equal(atTheDoor, snapshot.Members.Select(m => (m.MemberId, string.Join(",", m.WeaponXp), m.HpXp, m.ManaXp)));   // untouched

            if (beatTheBoss) break;
            var next = campaign.Enter("long_gallery", FiveDeep);
            Assert.NotSame(snapshot, next.EntrySnapshot);
            Assert.Equal(earned, next.EntrySnapshot.Members.Select(m => (m.MemberId, string.Join(",", m.WeaponXp), m.HpXp, m.ManaXp)));
            next.OpenTurns(party, Ten);
            Exit(campaign);
        }
    }

    [Fact]
    public void TheClawbackHooksGameOver()
    {
        // PHASE4-115: the clawback hooks the turn system's GameOver. The last
        // member falling inside a TurnSystem — here to a poison tick at the party's
        // turn end, with no scene anywhere — ends the run in Logic: the visit raises
        // Wiped from the GameOver subscription OpenTurns made, and the campaign,
        // which subscribed it at Enter, ends the run with the wipe. The wiring runs
        // campaign to visit: no field of the visit is the campaign, and once the
        // run is over the campaign's subscription is gone from the visit's event.
        var party = Party();
        var campaign = new CampaignState(party);
        var visit = campaign.Enter("tutorial", GoldenSeed);
        var turns = visit.OpenTurns(party, Ten);
        foreach (var member in party)
            member.WeaponXp[Dagger] += 40;

        int wipes = 0;
        Action heard = () => wipes++;
        visit.Wiped += heard;
        var ended = new List<RunResult>();
        campaign.RunEnded += ended.Add;
        foreach (var member in party)
        {
            member.Hp = 1;
            member.ApplyStatus(StatusEffectType.Poison, null, 5);
        }
        turns.EndTurn();

        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.Equal(1, wipes);
        var result = Assert.Single(ended);
        Assert.Equal(RunOutcome.Death, result.Outcome);
        Assert.Same(result, campaign.LastResult);
        Assert.Null(campaign.Visit);
        Assert.All(party, m => Assert.Equal(20, m.WeaponXp[Dagger]));   // 40 gained, half taken back

        // The visit holds no reference to the campaign — no field of it, or of a
        // floor, is one — and after the run nothing of the campaign's is left on
        // its Wiped: only this test's own handler.
        var fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.DoesNotContain(typeof(DungeonState).GetFields(fields), f => typeof(CampaignState).IsAssignableFrom(f.FieldType));
        Assert.DoesNotContain(typeof(Floor).GetFields(fields), f => typeof(CampaignState).IsAssignableFrom(f.FieldType));
        var wiped = (Delegate?)typeof(DungeonState).GetField(nameof(DungeonState.Wiped), fields)!.GetValue(visit);
        Assert.Equal(new Delegate[] { heard }, wiped!.GetInvocationList());
    }

    [Fact]
    public void KillingTheBossThenDyingKeepsTheTickAndTheLock_LosesTheSpoils()
    {
        // §6.3's table — "killed the boss, died on the way out: 1 tick, no drops" —
        // and settled.md: "die in the stairwell and you keep the service tick but
        // none of the spoils". The clear stands: the success tick, the lock written
        // against the counter it moved, the clear recorded. The spoils, and a share
        // of the XP, go down with the party.
        var campaign = Campaign();
        var party = campaign.Party;
        var visit = campaign.Enter("spear_hall", FiveDeep);
        Assert.Equal(5, visit.BossFloor);
        visit.OpenTurns(party, Ten);
        var bottom = BeatTheBoss(visit, party);
        var boss = visit.Current.Boss!;
        var drop = bottom.GroundItemOf(boss)!;
        Assert.Equal(1, Pickup.Take(campaign, boss, drop, party[1], visit));

        // A floor up the way out, the party falls.
        var above = Up(visit, party);
        var result = Wipe(campaign, above);

        Assert.Equal((RunOutcome.Death, true, 5, (int?)5), (result.Outcome, result.BossDefeated, result.DeepestFloor, result.BossFloorRevealed));
        Assert.Equal((true, false), (result.ServiceTick, result.MercyTick));   // the success tick, not a mercy
        Assert.Equal((1, 1), (result.RunCounter, campaign.RunCounter));
        Assert.Equal(1 + 5, result.LockedUntilRun);
        Assert.Equal(6, campaign.LockedUntilRun("spear_hall"));
        Assert.True(campaign.HasCleared("spear_hall"));
        Assert.False(campaign.IsSelectable(SpearHall));

        Assert.Same(drop.Weapon, Assert.Single(result.SpoilsLost));
        Assert.Empty(result.SpoilsKept);
        Assert.Null(party[1].Inventory[1]);
        Assert.NotEmpty(result.Clawbacks);   // the kill's XP, half of it
    }

    // ── What counts ──────────────────────────────────────────────────────────

    [Fact]
    public void EnteringTicksNothing_FailingShallowTicksNothing()
    {
        // §6.3: entering ticks nothing — or a stack of twenty-second entries would
        // burn the service clock without a fight — and failing above floor 3 ticks
        // nothing, died or retreated. The tutorial, two floors deep, never reaches
        // the bar, so it never mercy-ticks: no exception is written anywhere.
        var campaign = Campaign();
        var party = campaign.Party;
        var results = new List<RunResult>();

        var visit = campaign.Enter("spear_hall", FiveDeep);
        Assert.Equal(0, campaign.RunCounter);
        visit.OpenTurns(party, Ten);
        results.Add(Exit(campaign));                    // straight back out

        visit = campaign.Enter("spear_hall", FiveDeep);
        visit.OpenTurns(party, Ten);
        results.Add(Wipe(campaign, Down(visit, party)));   // dead on floor 2

        foreach (bool die in new[] { false, true })
        {
            visit = campaign.Enter("tutorial", GoldenSeed);
            Assert.Equal(2, visit.BossFloor);
            visit.OpenTurns(party, Ten);
            var bottom = Down(visit, party);            // as deep as it goes, the golem standing
            results.Add(die ? Wipe(campaign, bottom) : Exit(campaign));
        }

        Assert.Equal(new[] { RunOutcome.Retreat, RunOutcome.Death, RunOutcome.Retreat, RunOutcome.Death }, results.Select(r => r.Outcome));
        Assert.Equal(new[] { 1, 2, 2, 2 }, results.Select(r => r.DeepestFloor));
        Assert.All(results, r => Assert.Equal((false, false, 0), (r.ServiceTick, r.MercyTick, r.RunCounter)));
        Assert.Equal(0, campaign.RunCounter);
        Assert.False(campaign.IsRetired("tutorial"));
    }

    [Fact]
    public void AClearLocksTheDungeonForItsBossFloorInRuns()
    {
        // PHASE4-047, -130, -135: beating a boss locks its dungeon for as many runs
        // as the floor it was beaten on, held by the campaign against its run
        // counter — never by the visit, which is gone. The clear's own tick lands
        // first, then the lock, so a floor-5 clear is available again after exactly
        // five more runs that count, and "available in n runs" is the lock less the
        // counter. The two numbers leaving carries out are the dungeon and the
        // floor: RunCounter + BossFloor x CooldownRunsPerFloor.
        Assert.Equal(1, GameContent.Current.Tuning.CooldownRunsPerFloor);
        var campaign = Campaign();
        var result = Clear(campaign, "spear_hall", FiveDeep);

        Assert.Equal((1, 6), (campaign.RunCounter, campaign.LockedUntilRun("spear_hall")));
        Assert.Equal((1, (int?)6), (result.RunCounter, result.LockedUntilRun));
        Assert.Equal(5, result.LockedUntilRun - result.RunCounter);   // available in 5 runs
        Assert.Equal(new[] { ("spear_hall", 6) }, campaign.LockedInOrder);

        for (int run = 1; run <= 5; run++)
        {
            Assert.False(campaign.IsSelectable(SpearHall));
            Assert.Throws<InvalidOperationException>(() => campaign.Enter("spear_hall", SixDeep));
            DeepRetreat(campaign, "long_gallery");   // a run that counts, somewhere else
            Assert.Equal(1 + run, campaign.RunCounter);
        }
        Assert.True(campaign.IsSelectable(SpearHall));
        var back = campaign.Enter("spear_hall", SixDeep);
        Assert.Same(back, campaign.Visit);
    }

    [Fact]
    public void TheLockCountsTicksNotEntries()
    {
        // PHASE4-048: the cooldown is counted by the same events that tick the
        // enchanter — the run counter — and there is no second clock. An entry is
        // not one, a shallow retreat is not one, a shallow death is not one: none
        // of them brings a locked dungeon a run closer. A run that counts does.
        var campaign = Campaign();
        var party = campaign.Party;
        Clear(campaign, "spear_hall", FiveDeep);
        int Waiting() => campaign.LockedUntilRun("spear_hall") - campaign.RunCounter;
        Assert.Equal(5, Waiting());

        ShallowRetreat(campaign, "long_gallery");
        ShallowRetreat(campaign, "tutorial");
        Assert.Equal(5, Waiting());

        var visit = campaign.Enter("knife_warrens", FiveDeep);
        Assert.Equal(5, Waiting());   // entering is not a run's end
        visit.OpenTurns(party, Ten);
        Wipe(campaign, Down(visit, party));
        Assert.Equal(5, Waiting());

        DeepRetreat(campaign, "long_gallery");
        Assert.Equal(4, Waiting());
    }

    [Fact]
    public void FailingLocksNothing()
    {
        // PHASE4-049: the cooldown starts on a clear only. A dungeon that beat the
        // party — a retreat from its very bottom, a death beside its standing boss
        // — is available straight away, and as many times as the player likes.
        var campaign = Campaign();
        var party = campaign.Party;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var visit = campaign.Enter("spear_hall", FiveDeep);
            visit.OpenTurns(party, Ten);
            TurnSystem bottom = visit.Turns!;
            while (visit.Current.DownStairs is not null)
                bottom = Down(visit, party);
            Assert.True(visit.Current.Boss!.Alive);

            var result = attempt == 0 ? Exit(campaign) : Wipe(campaign, bottom);
            Assert.False(result.BossDefeated);
            Assert.Equal(((int?)null, false), (result.LockedUntilRun, result.Retired));
            Assert.Equal(0, campaign.LockedUntilRun("spear_hall"));
            Assert.Empty(campaign.LockedInOrder);
            Assert.False(campaign.HasCleared("spear_hall"));
            Assert.True(campaign.IsSelectable(SpearHall));
        }
        Assert.NotNull(campaign.Enter("spear_hall", SevenDeep));   // and in again at once
    }

    // ── The tutorial, and the hub ────────────────────────────────────────────

    [Fact]
    public void TheTutorialRetires_NeverLocks_AndOpensTheHub()
    {
        // PHASE4-050, -051, -081, -083, -132: the tutorial is available exactly
        // once — fail it and it is still there; kill the golem and it is gone for
        // good. Gone is a flag, never a cooldown: it is never written to the locks.
        // It is also what opens the hub, so the party always has somewhere to go —
        // and rushing it has cost its Block farm for good: nothing brings it back.
        var campaign = Campaign();
        var party = campaign.Party;
        var tutorial = Dungeons.Tutorial;
        Assert.Equal("tutorial", tutorial.Id);
        Assert.Equal("tutorial", campaign.DefaultNext());   // a campaign begins in it

        // Failed, by retreat and by death: still there.
        ShallowRetreat(campaign, "tutorial");
        var visit = campaign.Enter("tutorial", GoldenSeed);
        visit.OpenTurns(party, Ten);
        Wipe(campaign, Down(visit, party));
        Assert.False(campaign.IsRetired("tutorial"));
        Assert.True(campaign.IsSelectable(tutorial));
        Assert.Equal("tutorial", campaign.DefaultNext());

        // Beaten: retired, and never locked.
        var result = Clear(campaign, "tutorial", GoldenSeed);
        Assert.Equal((true, (int?)null, true), (result.Retired, result.LockedUntilRun, result.ServiceTick));
        Assert.True(campaign.IsRetired("tutorial"));
        Assert.Equal(new[] { "tutorial" }, campaign.RetiredInOrder);
        Assert.Equal(0, campaign.LockedUntilRun("tutorial"));
        Assert.Empty(campaign.LockedInOrder);
        Assert.True(campaign.HasCleared("tutorial"));
        Assert.False(campaign.IsSelectable(tutorial));
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("tutorial", GoldenSeed));

        // And the hub is open: every other dungeon can be picked, and the next run
        // goes to the first of them.
        Assert.All(Dungeons.All.Where(d => !d.RetiresOnClear), d => Assert.True(campaign.IsSelectable(d)));
        Assert.Equal("spear_hall", campaign.DefaultNext());
        Assert.Null(campaign.EarlyOffer());

        // Nothing reopens it: not runs that count, not the early offer.
        DeepRetreat(campaign, "long_gallery");
        Assert.True(campaign.IsRetired("tutorial"));
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("tutorial", 1));
    }

    [Fact]
    public void ASelectableDungeonIsOneWhoseLockHasLifted_AbsentMeansAvailable()
    {
        // PHASE4-131: a dungeon is selectable when runCounter >= LockedUntilRun[id],
        // and absent means available — so a campaign that has cleared nothing has
        // no locks at all, and every dungeon reads 0 and can be picked. A cleared
        // one is locked while the counter is short of its lock, and selectable the
        // run the counter reaches it, not one later.
        var campaign = Campaign();
        Assert.Empty(campaign.LockedInOrder);
        foreach (var def in Dungeons.All)
        {
            Assert.Equal(0, campaign.LockedUntilRun(def.Id));
            Assert.True(campaign.IsSelectable(def));
        }

        Clear(campaign, "spear_hall", FiveDeep);
        int until = campaign.LockedUntilRun("spear_hall");
        Assert.Equal(6, until);
        while (campaign.RunCounter < until)
        {
            Assert.False(campaign.IsSelectable(SpearHall));
            DeepRetreat(campaign, "knife_warrens");
        }
        Assert.Equal(until, campaign.RunCounter);
        Assert.True(campaign.IsSelectable(SpearHall));
        Assert.Equal(new[] { ("spear_hall", 6) }, campaign.LockedInOrder);   // a lifted lock stays, reading a run already reached
    }

    [Fact]
    public void ALockedOrRetiredDungeonCannotBeEntered()
    {
        // PHASE4-052, -082: the cooldown is the only thing between the player and
        // farming the same boss for its one unique, so a locked dungeon cannot be
        // entered until its lock lifts — and the retired tutorial never again. A
        // refusal enters nothing and rests nobody. Every other dungeon is there to
        // be picked, and comes back after its cooldown.
        var campaign = Campaign();
        var party = campaign.Party;
        Clear(campaign, "tutorial", GoldenSeed);
        Clear(campaign, "spear_hall", SixDeep);

        party[0].Hp = 7;   // the way the last run left A
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("spear_hall", FiveDeep));
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("tutorial", FiveDeep));
        Assert.Null(campaign.Visit);
        Assert.Equal(7, party[0].Hp);   // nobody rested: nothing was entered

        var other = campaign.Enter("long_gallery", FiveDeep);
        Assert.Same(other, campaign.Visit);
        Assert.Equal(party[0].MaxHp, party[0].Hp);
    }

    [Fact]
    public void EveryEntryRollsItsDepthAfresh()
    {
        // PHASE4-025, -026: a failed dungeon re-rolls its depth, so the depth
        // cannot be scummed by re-entering: every entry is a fresh seed, rolled by
        // whoever enters, and the boss floor is that seed's. Finding the boss floor
        // and walking out without the fight is a legitimate retreat — the mercy
        // tick and the XP — and the next entry is simply a new dungeon.
        var campaign = Campaign();
        var party = campaign.Party;

        var first = campaign.Enter("spear_hall", FiveDeep);
        Assert.Equal(5, first.BossFloor);
        first.OpenTurns(party, Ten);
        while (first.Current.DownStairs is not null)
            Down(first, party);
        var declined = Exit(campaign);   // stood on the boss floor, and left it standing
        Assert.Equal((RunOutcome.Retreat, 5, (int?)5, true), (declined.Outcome, declined.DeepestFloor, declined.BossFloorRevealed, declined.MercyTick));
        Assert.True(campaign.IsSelectable(SpearHall));

        foreach (var (seed, floors) in new[] { (TenDeep, 10), (SevenDeep, 7), (SixDeep, 6) })
        {
            var again = campaign.Enter("spear_hall", seed);
            Assert.NotSame(first, again);
            Assert.Equal(floors, again.BossFloor);
            Assert.Equal(DungeonState.RollBossFloor(seed, SpearHall), again.BossFloor);
            again.OpenTurns(party, Ten);
            Exit(campaign);
        }
    }

    [Fact]
    public void AClearIsRecordedOnceAndForAll()
    {
        // §6.7's "the set of dungeons cleared at least once" is the campaign's own
        // record, never read back off the locks: every boss kill adds its dungeon —
        // the tutorial's included, a fact whatever later counts it — a failure adds
        // nothing, and nothing takes one out: not the lock lifting, not a second
        // clear, not the early offer rewriting a lock (WhenEveryDungeonIsLocked...).
        var campaign = Campaign();
        Assert.Empty(campaign.ClearedInOrder);

        Clear(campaign, "tutorial", GoldenSeed);
        Assert.Equal(new[] { "tutorial" }, campaign.ClearedInOrder);
        Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal(new[] { "spear_hall", "tutorial" }, campaign.ClearedInOrder);

        while (!campaign.IsSelectable(SpearHall))
            DeepRetreat(campaign, "long_gallery");   // failures: cleared nothing, and the lock lifts
        Assert.False(campaign.HasCleared("long_gallery"));
        Assert.True(campaign.HasCleared("spear_hall"));

        Clear(campaign, "spear_hall", SevenDeep);
        Assert.Equal(new[] { "spear_hall", "tutorial" }, campaign.ClearedInOrder);   // once, however many times
        Assert.Equal(campaign.RunCounter + 7, campaign.LockedUntilRun("spear_hall"));
    }

    [Fact]
    public void WhenEveryDungeonIsLockedTheSoonestIsOfferedEarly()
    {
        // With the shipped four, four straight clears lock every dungeon — the
        // first lock (five runs at least) outlasts the three ticks after it — and
        // with nothing to enter no run could ever tick and no lock could ever lift.
        // So the dungeon whose lock lifts soonest is offered early, ties in the
        // content's file order and never a dictionary's; the next run goes there;
        // and entering it sets its lock to the run counter and changes nothing else.
        // Provisional, and in open-questions.md: it bends settled.md's lock rule in
        // this one corner.
        var campaign = Campaign();
        Clear(campaign, "tutorial", GoldenSeed);
        Assert.Equal(6, DungeonState.RollBossFloor(SixDeep, SpearHall));
        Clear(campaign, "spear_hall", SixDeep);         // counter 2, locked until 2 + 6 = 8
        Clear(campaign, "long_gallery", FiveDeep);      // counter 3, locked until 3 + 5 = 8
        Clear(campaign, "knife_warrens", SevenDeep);    // counter 4, locked until 4 + 7 = 11
        Assert.Null(campaign.EarlyOffer());             // the Quiver Pits is still open
        Assert.Equal("quiver_pits", campaign.DefaultNext());
        Clear(campaign, "quiver_pits", SevenDeep);      // counter 5, locked until 5 + 7 = 12

        Assert.Equal(5, campaign.RunCounter);
        Assert.All(Dungeons.All, d => Assert.False(campaign.IsSelectable(d)));
        Assert.Equal(new[] { ("knife_warrens", 11), ("long_gallery", 8), ("quiver_pits", 12), ("spear_hall", 8) }, campaign.LockedInOrder);

        // 8 and 8: the Spear Hall comes first in the file, so it is the offer, and
        // the Long Gallery, lifting as soon, is still refused.
        Assert.Equal("spear_hall", campaign.EarlyOffer());
        Assert.Equal("spear_hall", campaign.DefaultNext());
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("long_gallery", FiveDeep));

        var cleared = campaign.ClearedInOrder;
        var retired = campaign.RetiredInOrder;
        var visit = campaign.Enter("spear_hall", FiveDeep);
        Assert.Same(visit, campaign.Visit);
        Assert.Equal(5, campaign.LockedUntilRun("spear_hall"));   // the counter: lifted now, and the entry stays
        Assert.Equal(new[] { ("knife_warrens", 11), ("long_gallery", 8), ("quiver_pits", 12), ("spear_hall", 5) }, campaign.LockedInOrder);
        Assert.Equal(5, campaign.RunCounter);
        Assert.Equal(cleared, campaign.ClearedInOrder);
        Assert.True(campaign.HasCleared("spear_hall"));
        Assert.Equal(retired, campaign.RetiredInOrder);
        Assert.Null(campaign.EarlyOffer());   // the Spear Hall is open now
    }

    // ── Leaving ──────────────────────────────────────────────────────────────

    [Fact]
    public void LeavingKeepsNothingOfTheDungeonButTheCampaignsNumbers()
    {
        // PHASE4-008, -122: leaving discards all of it — the dungeon is the
        // renewable half, the gear the permanent half — and only what §4.5 carries
        // out survives. A retreat: the farmed dummy's count and the explored fog go
        // with the visit, and the very same seed entered again finds them fresh. A
        // clear: the drops banked on a floor and never collected go with it too,
        // and all the campaign keeps is its numbers.
        var campaign = Campaign();
        var party = campaign.Party;

        var visit = campaign.Enter("tutorial", GoldenSeed);
        var turns = visit.OpenTurns(party, Ten);
        var farmed = Row(visit.Current, 23, "hatchet");
        Kill(turns, visit.Current, party[0], farmed);
        Assert.Equal(2, farmed.DefeatCount);
        var start = visit.Current.UpStairs;
        visit.Current.Fog.RevealAt(start.R, start.C, visit.Current.FogBoxes.FogBoxes);
        Exit(campaign);
        Assert.Null(campaign.Visit);

        var again = campaign.Enter("tutorial", GoldenSeed);
        Assert.Empty(again.Floors.Intersect(visit.Floors));
        var twin = Row(again.Current, 23, "hatchet");
        Assert.NotSame(farmed, twin);
        Assert.Equal((true, 0, -1), (twin.Alive, twin.DefeatCount, twin.DefeatedAtTurn));
        Assert.All(again.Floors, f => Assert.DoesNotContain(true, f.Fog.Seen.Cast<bool>()));
        again.OpenTurns(party, Ten);
        Exit(campaign);

        // A clear, leaving the boss's drop on its floor and a permanent kill's on the first.
        var hall = campaign.Enter("spear_hall", FiveDeep);
        hall.OpenTurns(party, Ten);
        var bottom = BeatTheBoss(hall, party);
        Assert.NotNull(bottom.GroundItemOf(hall.Current.Boss!));
        var back = hall.Turns!;
        while (hall.FloorIndex > 0)
            back = Up(hall, party);
        var dummy = hall.Current.Enemies.First(e => e.Alive);
        Kill(back, hall.Current, party[0], dummy);
        Assert.NotNull(back.GroundItemOf(dummy));
        var bags = party.Select(m => m.Inventory.ToArray()).ToArray();

        var result = Exit(campaign);
        Assert.Equal(RunOutcome.BossKill, result.Outcome);
        Assert.Empty(result.SpoilsKept);    // banked, never collected: forfeited with the dungeon
        Assert.Equal(bags, party.Select(m => m.Inventory.ToArray()));
        Assert.Null(campaign.Visit);
        Assert.Equal(1, campaign.RunCounter);
        Assert.Equal(new[] { ("spear_hall", 6) }, campaign.LockedInOrder);
        Assert.Equal(new[] { "spear_hall" }, campaign.ClearedInOrder);
        Assert.Empty(campaign.RetiredInOrder);

        // And the campaign's surface holds nothing of a dungeon: its numbers, its
        // party, what it has met, and how the last run went.
        string[] kept =
        [
            nameof(CampaignState.Party), nameof(CampaignState.Visit), nameof(CampaignState.RunCounter), nameof(CampaignState.LastResult),
            nameof(CampaignState.SeenInOrder), nameof(CampaignState.LockedInOrder), nameof(CampaignState.ClearedInOrder), nameof(CampaignState.RetiredInOrder),
        ];
        Assert.Equal(kept.Order(StringComparer.Ordinal),
            typeof(CampaignState).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheTutorialLoopCarriesWeaponsHome()
    {
        // PHASE4-078, -079: the tutorial is the whole loop in miniature — descend
        // once, fight the boss, extract back through a single floor — and two
        // floors of lightly farmed dummies seed the first stable: what the
        // permanent kills drop on the way out is carried home, with the golem's own.
        var campaign = Campaign();
        var party = campaign.Party;
        var visit = campaign.Enter("tutorial", GoldenSeed);
        Assert.Equal(2, visit.BossFloor);
        var first = visit.OpenTurns(party, Ten);

        // A light farm on the first floor: one dummy put down, back, and down again.
        var farmed = Row(visit.Current, 23, "hatchet");
        Kill(first, visit.Current, party[0], farmed);
        Home(visit, party);
        PassTurns(first, 8);
        Assert.True(farmed.Alive);
        Kill(first, visit.Current, party[0], farmed);
        Assert.Equal(4, farmed.DefeatCount);
        Home(visit, party);

        // Down once, and the golem; its drop into B's bag.
        var bottom = Down(visit, party);
        var golem = visit.Current.Boss!;
        Kill(bottom, visit.Current, party[0], golem);
        Assert.True(visit.BossDefeated);
        Assert.Equal(1, Pickup.Take(campaign, golem, bottom.GroundItemOf(golem)!, party[1], visit));

        // Back up the single floor: the farmed dummy is lying where it fell — its
        // wait ran past the golem's fall — on the weapon the farm deepened.
        var back = Up(visit, party);
        Assert.False(farmed.Alive);
        var harvest = back.GroundItemOf(farmed)!;
        Assert.Equal(4, harvest.DefeatCount);
        Assert.Equal(1, Pickup.Take(campaign, farmed, harvest, party[2], visit));

        // And one more put down for good on the way out, and taken.
        var straggler = visit.Current.Enemies.First(e => e.Alive);
        Kill(back, visit.Current, party[0], straggler);
        Assert.Equal(1, Pickup.Take(campaign, straggler, back.GroundItemOf(straggler)!, party[3], visit));

        var result = Exit(campaign);
        Assert.Equal((RunOutcome.BossKill, 2, (int?)2), (result.Outcome, result.DeepestFloor, result.BossFloorRevealed));
        Assert.Equal(3, result.SpoilsKept.Count);
        Assert.Equal(visit.Spoils, result.SpoilsKept);
        Assert.Contains(harvest.Weapon, result.SpoilsKept);
        Assert.All(result.SpoilsKept, w => Assert.Contains(party, m => m.Inventory.Contains(w)));   // home, in the bags
        Assert.Equal(1, campaign.RunCounter);
        Assert.True(result.Retired);
        Assert.True(campaign.IsRetired("tutorial"));
    }

    [Fact]
    public void TheDepthIsRevealedOnlyIfReached()
    {
        // PHASE4-024: the boss floor is the one number a run lets out, and only to
        // a party that stood on it — a retreat from above the bottom, or a death
        // there, tells nothing; a retreat from the bottom, or a clear, tells it.
        var campaign = Campaign();
        var party = campaign.Party;

        Assert.Null(DeepRetreat(campaign, "spear_hall").BossFloorRevealed);   // floor 3 of 5

        var visit = campaign.Enter("spear_hall", SevenDeep);
        visit.OpenTurns(party, Ten);
        var died = Wipe(campaign, Down(visit, party));
        Assert.Equal((2, (int?)null), (died.DeepestFloor, died.BossFloorRevealed));   // floor 2 of 7

        visit = campaign.Enter("spear_hall", FiveDeep);
        visit.OpenTurns(party, Ten);
        while (visit.Current.DownStairs is not null)
            Down(visit, party);
        Assert.Equal(5, Exit(campaign).BossFloorRevealed);   // stood on it, and left

        Assert.Equal(7, Clear(campaign, "spear_hall", SevenDeep).BossFloorRevealed);
    }

    // ── The doors a run ends by ──────────────────────────────────────────────

    [Fact]
    public void TheOnlyWayOutIsTheFirstFloorsExit()
    {
        // A run ends by one of two doors, and each has to be the real one: the way
        // out is the first floor's up-stairs, taken as the stairs are — in the
        // party's phase, gathered — with no shortcut from below (PHASE4-031), and a
        // wipe is the whole party down. A refused ending ends nothing.
        var campaign = Campaign();
        var party = campaign.Party;
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Exited));   // no run at all

        var visit = campaign.Enter("spear_hall", FiveDeep);
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Exited));   // no floor open yet
        visit.OpenTurns(party, Ten);
        Down(visit, party);
        Gather(visit, party, visit.Current.UpStairs);
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Exited));   // floor 2: no shortcut out
        Up(visit, party);
        Gather(visit, party, visit.Current.DownStairs!.Value);
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Exited));   // nowhere near the way out
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Wiped));    // everyone standing
        Assert.Same(visit, campaign.Visit);
        Assert.Equal(0, campaign.RunCounter);
        Assert.Null(campaign.LastResult);

        var result = Exit(campaign);
        Assert.Equal(RunOutcome.Retreat, result.Outcome);
        Assert.Throws<InvalidOperationException>(() => campaign.EndRun(RunEnding.Exited));   // over: nothing left to end
    }

    [Fact]
    public void OnlyThisRunsPickupsAreSpoils()
    {
        // Spoils are what Pickup.Take put into a bag this visit, handed the visit:
        // never the gear the party walked in with, recorded once, and only the
        // visit in progress's.
        var campaign = Campaign();
        var party = campaign.Party;
        var visit = campaign.Enter("tutorial", GoldenSeed);
        visit.OpenTurns(party, Ten);
        Assert.Empty(visit.Spoils);

        var corpse = Row(visit.Current, 23, "hatchet");
        corpse.Alive = false;
        var bow = new Drop(TestWeapons.Get("hunting_bow"), 0, false);
        Assert.Equal(1, Pickup.Take(campaign, corpse, bow, party[1], visit));
        Assert.Same(bow.Weapon, Assert.Single(visit.Spoils));
        Assert.DoesNotContain(visit.Spoils, w => party.Any(m => ReferenceEquals(m.Inventory[0], w)));

        // Not handed the visit, a pickup counts toward no run's end.
        var other = new Drop(TestWeapons.Get("arming_sword"), 0, false);
        Assert.Equal(2, Pickup.Take(campaign, corpse, other, party[1]));
        Assert.Single(visit.Spoils);

        // Another visit's is refused, and nothing is taken.
        var stranger = DungeonState.Enter(Dungeons.Tutorial, GoldenSeed, Party());
        var sword = new Drop(TestWeapons.Get("tower_guard"), 0, false);
        Assert.Throws<ArgumentException>(() => Pickup.Take(campaign, corpse, sword, party[2], stranger));
        Assert.Null(party[2].Inventory[1]);
        Assert.Single(visit.Spoils);

        // Out with it: kept, and the unrecorded one is simply gear now.
        var result = Exit(campaign);
        Assert.Same(bow.Weapon, Assert.Single(result.SpoilsKept));
        Assert.Same(other.Weapon, party[1].Inventory[2]);
    }
}
