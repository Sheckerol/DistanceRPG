using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.1's visit (<see cref="DungeonState"/>): every floor built at entry and
/// held for as long as the party is inside, the party moving between them by
/// stairs — the fallen with it, and the turn's counters with it — on the visit's
/// one clock, and a floor re-entered found exactly as it was left, except for the
/// dummies whose time came while the party was elsewhere.
/// <para>
/// These run on real floors. Each fact a test leans on — which floor is the
/// bottom, where a stair is, which dummy stands where — is pinned by an assertion
/// before it is used. Where a scenario needs an actor somewhere the placer did
/// not put one, it moves the row: the rows are the floor's stored state, exactly
/// what a save restores, and the test says so. The party waits out turns in a
/// floor's arrival room, where the placer puts nobody and nothing reaches.
/// </para>
/// </summary>
public class DungeonStateTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The shipped seed: its first floor is the golden dungeon, and the Spear Hall rolls five floors on it.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>A tutorial seed whose first floor has a Phalanx Spear dummy (spawn 9) standing on its down-stairs at (19, 4), pinned where it is used.</summary>
    private const long GuardedStairsSeed = 2084311110;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    private static DungeonDef Tutorial => GameContent.Current.Dungeons.Tutorial;

    private static DungeonDef SpearHall => GameContent.Current.Dungeons["spear_hall"];

    /// <summary>A roll that neither crits nor fumbles.</summary>
    private static int Ten() => 10;

    /// <summary>A blow that clears a fresh dummy's whole 50 through any Block a dummy holds: one swing, a clean kill, two cycles owing.</summary>
    private static Weapon Maul() => TestWeapons.Make("Maul", range: 60, damage: 60, cost: 30);

    /// <summary>Four fixture members in roster order, each holding the weapon at its place — a Weakspot Stiletto where none is named.</summary>
    private static PartyMemberState[] Party(params Weapon[] held)
        => Ids.Select((id, i) => TestPools.Holding(id, i < held.Length ? held[i] : TestWeapons.Get("weakspot_stiletto"), colorIndex: i))
            .ToArray();

    private static (float X, float Y) Centre((int R, int C) tile) => (tile.C * Tile + Tile / 2f, tile.R * Tile + Tile / 2f);

    private static void Put(ActorState actor, (int R, int C) tile) => (actor.X, actor.Y) = Centre(tile);

    private static (int R, int C) TileOf(ActorState actor) => Displacer.TileOf(actor);

    private static HashSet<(int R, int C)> LivingEnemyTiles(Floor floor)
        => floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();

    private static string GridText(int[,] grid)
    {
        var chars = new char[grid.Length];
        int i = 0;
        foreach (int v in grid) chars[i++] = (char)('0' + v);
        return new string(chars);
    }

    /// <summary>The row at <paramref name="spawnIndex"/>, pinned to the weapon the seed gave it before anything leans on it.</summary>
    private static EnemyState Row(Floor floor, int spawnIndex, string weaponId)
    {
        var row = floor.Enemies[spawnIndex];
        Assert.Equal((spawnIndex, weaponId), (row.SpawnIndex, row.Weapon.Id));
        return row;
    }

    /// <summary>Everything a floor keeps of a row between visits to it.</summary>
    private static (float X, float Y, int Hp, bool Alive, int DefeatCount, int DefeatedAtTurn, (int Rolled, int Damage, int MaxHp) Farm, string Weapon) Snapshot(EnemyState e)
        => (e.X, e.Y, e.Hp, e.Alive, e.DefeatCount, e.DefeatedAtTurn, (e.RevivalsRolled, e.RevivalDamage, e.RevivalMaxHp), e.Weapon.Id);

    /// <summary>
    /// Bring the party to <paramref name="stair"/>, each member on a tile around
    /// it as a party that walked there would stand — around living enemies, never
    /// on them — without the walk: this file is about what the stairs do.
    /// </summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) stair)
    {
        var floor = visit.Current;
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, stair.R, stair.C, party.Count, LivingEnemyTiles(floor));
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

    /// <summary>Back to the current floor's arrival room, where the placer puts nobody and no dummy reaches: where the party waits turns out.</summary>
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

    /// <summary>Stand <paramref name="actor"/> on a free floor tile beside <paramref name="target"/>.</summary>
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
        Assert.Fail($"No free floor tile beside the dummy at {(r, c)}.");
    }

    /// <summary>Stand <paramref name="killer"/> beside <paramref name="target"/> and put it down with one swing, through the floor's turn system.</summary>
    private static void Kill(TurnSystem turns, Floor floor, PartyMemberState killer, EnemyState target)
    {
        StandBeside(floor, killer, target);
        Assert.True(turns.TryAttack(killer, target));
        Assert.False(target.Alive);
    }

    // ── Entering, and the floors held ────────────────────────────────────────

    [Fact]
    public void EnteringBuildsEveryFloorOnce_AndPutsThePartyAtFloorOnesArrival()
    {
        // PHASE4-125, -126: the visit holds Floor[] — each with its map, its rows
        // and its fog — and the boss floor and the flag beside it. Every floor is
        // built at entry, down to the rolled bottom, and never again inside the
        // visit: going back to one is a pointer move.
        var party = Party();
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);

        Assert.Equal(5, DungeonState.RollBossFloor(GoldenSeed, SpearHall));
        Assert.Equal(5, visit.BossFloor);
        Assert.Equal(visit.BossFloor, visit.Floors.Length);
        Assert.Same(SpearHall, visit.Def);
        Assert.Equal(GoldenSeed, visit.MapSeed);
        Assert.Equal((0, 0, 0, false, -1), (visit.FloorIndex, visit.DeepestFloorIndex, visit.Turn, visit.BossDefeated, visit.BossDefeatedAtTurn));
        Assert.Null(visit.Turns);
        Assert.Same(visit.Floors[0], visit.Current);

        for (int n = 0; n < visit.BossFloor; n++)
        {
            var floor = visit.Floors[n];
            bool bottom = n == visit.BossFloor - 1;
            var fresh = Floor.Generate(GoldenSeed, n, bottom, SpearHall);
            Assert.Equal(n, floor.Index);
            Assert.Equal(GridText(fresh.Map.Grid), GridText(floor.Map.Grid));
            Assert.Equal(
                fresh.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)),
                floor.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)));
            Assert.Equal((fresh.UpStairs, fresh.DownStairs, fresh.Lair), (floor.UpStairs, floor.DownStairs, floor.Lair));
            Assert.Equal(bottom, floor.DownStairs is null);
            Assert.Equal(n == 0, floor.Visited);   // the party stands on the first; nobody has seen the rest
        }

        // Standing where the one-floor game always stood it: the arrival tile and
        // its ring, in roster order, each member on a tile's centre.
        var first = visit.Current;
        Assert.Equal(first.Map.PlayerStart, first.UpStairs);
        var tiles = GridUtils.FindPartySpawnTiles(first.Map.Grid, first.UpStairs.R, first.UpStairs.C, party.Length);
        Assert.Equal(tiles, party.Select(TileOf));
        Assert.All(party.Zip(tiles), placed => Assert.Equal(Centre(placed.Second), (placed.First.X, placed.First.Y)));

        // Held, not rebuilt: to the bottom and back out, twice, and every floor,
        // map and row list is the one the entry built.
        var floors = visit.Floors.ToArray();
        var maps = floors.Select(f => f.Map).ToArray();
        var rows = floors.Select(f => f.Enemies).ToArray();
        visit.OpenTurns(party, Ten);
        for (int trip = 0; trip < 2; trip++)
        {
            while (visit.Current.DownStairs is not null)
                Down(visit, party);
            while (visit.FloorIndex > 0)
                Up(visit, party);
        }

        Assert.Equal(visit.BossFloor - 1, visit.DeepestFloorIndex);
        for (int n = 0; n < floors.Length; n++)
        {
            Assert.Same(floors[n], visit.Floors[n]);
            Assert.Same(maps[n], visit.Floors[n].Map);
            Assert.Same(rows[n], visit.Floors[n].Enemies);
            Assert.True(visit.Floors[n].Visited);
        }
    }

    // ── The floor you come back to ───────────────────────────────────────────

    [Fact]
    public void ReturningToAFloorFindsItAsItWasLeft()
    {
        // PHASE4-001/-002/-003/-005/-106: "retreat upstairs, come back for a
        // half-cleared room". The floor found is the floor left: its rows where
        // they stood, as hurt as they were, the dead still dead and farmed as far
        // as they were farmed, and its explored fog still explored. No
        // re-generation and no re-fog: navigation is not the challenge.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var floor = visit.Current;
        var turns = visit.OpenTurns(party, Ten);

        // A half-cleared floor: one dummy put down (a clean kill: two cycles
        // owing), another hurt and left standing.
        var dead = Row(floor, 23, "hatchet");
        var hurt = Row(floor, 9, "hatchet");
        Kill(turns, floor, party[0], dead);
        StandBeside(floor, party[1], hurt);
        Assert.True(turns.TryAttack(party[1], hurt));
        Assert.True(hurt.Alive);
        Assert.True(hurt.Hp < hurt.MaxHp);

        // And explored where the party has stood.
        foreach (var member in party)
        {
            var (r, c) = TileOf(member);
            floor.Fog.RevealAt(r, c, floor.FogBoxes.FogBoxes);
        }
        var seen = (bool[,])floor.Fog.Seen.Clone();
        Assert.Contains(true, seen.Cast<bool>());

        var map = floor.Map;
        var fog = floor.Fog;
        var enemies = floor.Enemies;
        var before = floor.Enemies.Select(Snapshot).ToArray();

        // Down and straight back up, inside the same turn.
        Down(visit, party);
        Up(visit, party);

        Assert.Equal(0, visit.Turn);
        Assert.Same(floor, visit.Current);
        Assert.Same(map, visit.Current.Map);
        Assert.Same(fog, visit.Current.Fog);
        Assert.Same(enemies, visit.Current.Enemies);
        Assert.Equal(seen.Cast<bool>(), visit.Current.Fog.Seen.Cast<bool>());
        Assert.Equal(before, visit.Current.Enemies.Select(Snapshot));
        Assert.Equal((false, 2), (dead.Alive, dead.DefeatCount));
    }

    [Fact]
    public void TheFarmedStatlineSurvivesTheClimbOut()
    {
        // PHASE4-104: every revival added damage or health, so the dummy carrying
        // the deepest drop is the strongest thing on the floor. The statline is
        // floor state like a position: still on the row after any number of trips
        // between floors, so the climb out meets the version the farm made.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var floor = visit.Current;
        var turns = visit.OpenTurns(party, Ten);

        var farmed = Row(floor, 23, "hatchet");
        Kill(turns, floor, party[0], farmed);   // clean: two cycles owing, back in eight turns
        Home(visit, party);
        PassTurns(turns, FarmLadder.ResurrectTurns(farmed.DefeatCount));
        Assert.True(farmed.Alive);
        Assert.Equal((2, 2), (farmed.DefeatCount, farmed.RevivalsRolled));
        Assert.True(farmed.RevivalDamage + farmed.RevivalMaxHp > 0);

        var statline = (farmed.DefeatCount, farmed.RevivalsRolled, farmed.RevivalDamage, farmed.RevivalMaxHp, farmed.MaxHp, farmed.Hp);

        // Two floors down, back up, down again, and out to the first floor.
        Down(visit, party);
        Down(visit, party);
        Up(visit, party);
        Down(visit, party);
        Up(visit, party);
        Up(visit, party);

        Assert.Equal(0, visit.FloorIndex);
        Assert.Same(farmed, visit.Current.Enemies[23]);
        Assert.Equal(statline, (farmed.DefeatCount, farmed.RevivalsRolled, farmed.RevivalDamage, farmed.RevivalMaxHp, farmed.MaxHp, farmed.Hp));
        Assert.Equal(farmed.RevivalDamage, farmed.BonusDamage);   // on the actor, where every swing it makes reads it
    }

    // ── One clock ────────────────────────────────────────────────────────────

    [Fact]
    public void TheVisitRunsOnOneClock()
    {
        // The visit has one turn count. Each floor's system opens on it and hands
        // it back, so a corpse's DefeatedAtTurn means one turn on every floor: a
        // dummy put down on the second floor at turn 3 is due at turn 11 wherever
        // the party spends the turns between, and the second floor's reopened
        // system raises it on exactly that turn.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var first = visit.OpenTurns(party, Ten);
        PassTurns(first, 3);
        Assert.Equal(3, visit.Turn);

        var second = Down(visit, party);
        Assert.Equal(3, second.TurnCount);
        Assert.Equal(3, visit.Turn);

        var floor = visit.Current;
        var dummy = Row(floor, 10, "hatchet");
        Kill(second, floor, party[0], dummy);
        Assert.Equal(3, dummy.DefeatedAtTurn);
        Assert.Equal(11, dummy.DefeatedAtTurn + FarmLadder.ResurrectTurns(dummy.DefeatCount));

        Home(visit, party);
        PassTurns(second, 2);                      // turns 4 and 5, on the second floor
        var back = Up(visit, party);
        Assert.Equal(5, back.TurnCount);
        Home(visit, party);
        PassTurns(back, 5);                        // turns 6 to 10, on the first
        Assert.Equal(10, visit.Turn);

        var again = Down(visit, party);            // the second floor, reopened on the clock: one turn short
        Assert.Equal(10, again.TurnCount);
        Assert.False(dummy.Alive);

        var risen = new List<(EnemyState, int)>();
        again.EnemyResurrected += e => risen.Add((e, again.TurnCount));
        PassTurns(again, 1);
        Assert.Equal(new[] { (dummy, 11) }, risen);
        Assert.True(dummy.Alive);
    }

    [Fact]
    public void ADummyLeftDownIsUpWhenItsTimeHasPassedElsewhere()
    {
        // A floor nobody is on is frozen, except for resurrection timers. The
        // spear dummy on this seed's down-stairs is put down there at turn 0 — a
        // clean kill, two cycles owing, due in eight turns — and the party spends
        // the eight below. Back up, it is standing: both cycles rolled in order
        // off the floor's own stream, HP full after them, and awake beside the
        // stairs rather than inside the member standing on its remnant.
        var party = Party(Maul());
        var visit = DungeonState.Enter(Tutorial, GuardedStairsSeed, party);
        var floor = visit.Current;
        var turns = visit.OpenTurns(party, Ten);
        var stairs = floor.DownStairs!.Value;
        Assert.Equal((19, 4), stairs);
        var guard = Row(floor, 9, "phalanx_spear");
        Assert.Equal(stairs, TileOf(guard));

        Kill(turns, floor, party[0], guard);
        Assert.Equal((2, 0), (guard.DefeatCount, guard.DefeatedAtTurn));
        int wait = FarmLadder.ResurrectTurns(guard.DefeatCount);
        Assert.Equal(8, wait);

        var below = Down(visit, party);            // over the corpse: a body is walked over
        PassTurns(below, wait);
        var back = Up(visit, party);                // turn 8: its time came while the party was below
        Assert.Equal(8, back.TurnCount);

        Assert.True(guard.Alive);
        int damage = 0, health = 0;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            var gain = FarmLadder.RollRevival(FarmLadder.ReviveStream(floor.Seed, guard.SpawnIndex, cycle));
            if (gain is RevivalGain.Damage or RevivalGain.Both)
                damage += FarmLadder.Step(guard.Weapon.Damage + damage);
            if (gain is RevivalGain.Health or RevivalGain.Both)
                health += FarmLadder.Step(GameConstants.DummyHp + health);
        }
        Assert.Equal((2, damage, health), (guard.RevivalsRolled, guard.RevivalDamage, guard.RevivalMaxHp));
        Assert.Equal((GameConstants.DummyHp + health, GameConstants.DummyHp + health), (guard.MaxHp, guard.Hp));
        Assert.Equal(2, guard.TurnsSinceSeen);     // passive until it is seen again

        // A member stands on the stairs it lay on; it woke on a free floor tile beside them.
        Assert.Contains(party, m => TileOf(m) == stairs);
        Assert.DoesNotContain(party, m => TileOf(m) == TileOf(guard));
        var woke = TileOf(guard);
        Assert.Equal(0, floor.Map.Grid[woke.R, woke.C]);
        Assert.Equal(Centre(woke), (guard.X, guard.Y));

        // And the zones were re-read around it: the member it woke beside is
        // marked inside its reach, so their first step is no entry and draws no
        // brace, even with its zone armed by the arrival's sight of it.
        var inside = party.First(m => EnemyAi.CanHit(guard, m, guard.Weapon, floor.Map.Grid));
        int braces = 0;
        back.EnemyBraceTriggered += _ => braces++;
        back.NotifyEnemyVisible(guard, true);
        inside.X += 4f;
        back.NotifyCharacterMoved(inside);
        Assert.True(EnemyAi.CanHit(guard, inside, guard.Weapon, floor.Map.Grid));
        Assert.Equal(0, braces);

        // Walking out and back in is an entry, and the armed zone answers it.
        var (x, y) = (inside.X, inside.Y);
        Put(inside, floor.UpStairs);
        back.NotifyCharacterMoved(inside);
        (inside.X, inside.Y) = (x, y);
        back.NotifyCharacterMoved(inside);
        Assert.Equal(1, braces);
    }

    [Fact]
    public void CatchUpWakesInSpawnOrder()
    {
        // Two corpses lying on the second floor's arrival tile (the rows moved
        // there and put down at turn 0 — stored state), both due by the time the
        // party comes back down onto it. The member placed on the tile holds it,
        // and the tile above holds another, so both dummies want the tile below:
        // the wake-up searches up, down, left, right. The earlier spawn takes it,
        // because catch-up walks the rows in spawn order exactly as a turn's start
        // does, and the later one takes the next free tile, to the left.
        var party = Party();
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        visit.OpenTurns(party, Ten);

        var second = Down(visit, party);
        var floor = visit.Current;
        var arrival = floor.UpStairs;
        Assert.Equal((37, 10), arrival);
        var (earlier, later) = (floor.Enemies[0], floor.Enemies[1]);
        Assert.Equal((0, 1), (earlier.SpawnIndex, later.SpawnIndex));
        foreach (var corpse in new[] { earlier, later })
        {
            Put(corpse, arrival);
            corpse.Alive = false;
            corpse.DefeatCount = 1;
            corpse.DefeatedAtTurn = second.TurnCount;
        }

        var first = Up(visit, party);
        Home(visit, party);
        PassTurns(first, FarmLadder.ResurrectTurns(1));   // nine turns on the first floor

        Down(visit, party);
        Assert.Equal(arrival, TileOf(party[0]));
        Assert.Equal((arrival.R - 1, arrival.C), TileOf(party[2]));
        Assert.True(earlier.Alive);
        Assert.True(later.Alive);
        Assert.Equal((arrival.R + 1, arrival.C), TileOf(earlier));
        Assert.Equal((arrival.R, arrival.C - 1), TileOf(later));
    }

    [Fact]
    public void OpenTurnsReturnsWithTheCatchUpDone()
    {
        // The system OpenTurns hands back is finished: the floor's clock caught
        // up, every due dummy already standing where it woke and its zone read
        // around the party. Nothing waits for a subscriber — a presentation built
        // from the rows after this call draws the dummy standing, which is how it
        // learns of a revival EnemyResurrected announced to nobody.
        var party = Party();
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        visit.OpenTurns(party, Ten);

        Down(visit, party);
        var floor = visit.Current;
        var pike = Row(floor, 9, "skirmishers_pike");
        Put(pike, floor.UpStairs);                  // lying on the arrival tile (a moved row: stored state)
        pike.Alive = false;
        pike.DefeatCount = 1;
        pike.DefeatedAtTurn = visit.Turn;
        var first = Up(visit, party);
        Home(visit, party);
        PassTurns(first, FarmLadder.ResurrectTurns(1));
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        Assert.Null(visit.Turns);
        Assert.False(pike.Alive);                    // placing the party wakes nothing: opening does

        var turns = visit.OpenTurns(party, Ten);

        Assert.Same(turns, visit.Turns);
        Assert.Equal((TurnPhase.Player, 9, 9, true), (turns.Phase, turns.TurnCount, visit.Turn, turns.ResurrectionActive));
        Assert.True(pike.Alive);
        Assert.Equal(pike.MaxHp, pike.Hp);
        Assert.DoesNotContain(party, m => TileOf(m) == TileOf(pike));
        Assert.Throws<InvalidOperationException>(() => visit.OpenTurns(party, Ten));

        // Its zone is already read around the party: whoever it woke beside is
        // inside, and their first step is not an entry.
        var inside = party.First(m => EnemyAi.CanHit(pike, m, pike.Weapon, floor.Map.Grid));
        int braces = 0;
        turns.EnemyBraceTriggered += _ => braces++;
        turns.NotifyEnemyVisible(pike, true);
        inside.Y += 4f;
        turns.NotifyCharacterMoved(inside);
        Assert.Equal(0, braces);
    }

    [Fact]
    public void ADummyLeftDownStaysDownUntilItsTimeComes()
    {
        // Back early, a corpse is still a corpse: catch-up raises only what is
        // due. The ordinary turn start raises it when its time does come — the
        // same one revival, on the same clock.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var floor = visit.Current;
        var turns = visit.OpenTurns(party, Ten);
        var dummy = Row(floor, 23, "hatchet");
        Kill(turns, floor, party[0], dummy);        // turn 0: due at 8
        Assert.Equal(8, dummy.DefeatedAtTurn + FarmLadder.ResurrectTurns(dummy.DefeatCount));

        var below = Down(visit, party);
        PassTurns(below, 5);
        var back = Up(visit, party);                 // turn 5: three short
        Assert.Equal(5, back.TurnCount);
        Assert.False(dummy.Alive);

        var risen = new List<int>();
        back.EnemyResurrected += e =>
        {
            if (e == dummy) risen.Add(back.TurnCount);
        };
        Home(visit, party);
        PassTurns(back, 2);
        Assert.False(dummy.Alive);
        Assert.Empty(risen);

        PassTurns(back, 1);
        Assert.True(dummy.Alive);
        Assert.Equal(new[] { 8 }, risen);
    }

    [Fact]
    public void AStairHopRevivesNothing()
    {
        // Down and back up inside one turn moves no clock, so it wakes nobody —
        // not the dummy one turn short of due, not the one put down this very
        // turn. Were the stairs a respawn, a hop would be a free farm accelerator.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var floor = visit.Current;
        var turns = visit.OpenTurns(party, Ten);

        var early = Row(floor, 23, "hatchet");
        Kill(turns, floor, party[0], early);        // turn 0: due at 8
        Home(visit, party);
        PassTurns(turns, 7);                        // turn 7: one short
        var late = Row(floor, 9, "hatchet");
        Kill(turns, floor, party[0], late);         // turn 7: due at 15
        Assert.Equal((0, 7), (early.DefeatedAtTurn, late.DefeatedAtTurn));

        var below = Down(visit, party);
        Assert.Equal(7, below.TurnCount);
        Assert.All(visit.Current.Enemies, e => Assert.True(e.Alive));
        var back = Up(visit, party);
        Assert.Equal((7, 7), (back.TurnCount, visit.Turn));
        Assert.False(early.Alive);
        Assert.False(late.Alive);

        // The turn that does pass wakes the one whose time has come, and only it.
        Home(visit, party);
        PassTurns(back, 1);
        Assert.True(early.Alive);
        Assert.False(late.Alive);
    }

    // ── Using the stairs ─────────────────────────────────────────────────────

    [Fact]
    public void TheWholeLivingPartyMustGatherAtTheStairs()
    {
        // The party moves as one, so nobody is ever on another floor: every living
        // member within three tiles of the stair, in the player phase of an open
        // floor. The count is the living; the fallen are carried, not gathered.
        var party = Party();
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var floor = visit.Current;
        var down = new StairUse(Stair.Down, floor.DownStairs!.Value);
        Gather(visit, party, down.Tile);

        // No floor open yet: it is nobody's turn.
        Assert.Equal(StairRefusal.NotThePlayersTurn, visit.CanUse(down, party));

        var turns = visit.OpenTurns(party, Ten);
        Assert.Null(visit.CanUse(down, party));
        Assert.Equal(4, visit.Gathered(party, down.Tile));

        // One straggler: three of four, refused, and the count says so.
        Put(party[3], floor.UpStairs);
        Assert.Equal(StairRefusal.PartyNotGathered, visit.CanUse(down, party));
        Assert.Equal(3, visit.Gathered(party, down.Tile));
        Assert.Throws<InvalidOperationException>(() => visit.Descend(party));
        Assert.Equal(0, visit.FloorIndex);
        Assert.Same(turns, visit.Turns);

        // Fallen where they stand: never counted, so the three are the whole living party.
        party[3].Alive = false;
        Assert.Null(visit.CanUse(down, party));
        Assert.Equal(3, visit.Gathered(party, down.Tile));
        party[3].Alive = true;

        // The radius is three tiles in logic units, centre to centre: exactly 96
        // from the stair's centre is gathered, a unit more is not.
        var (x, y) = Centre(down.Tile);
        (party[3].X, party[3].Y) = (x, y + 3 * GameConstants.LogicUnitsPerTile);
        Assert.Equal(96f, 3 * GameConstants.LogicUnitsPerTile);
        Assert.Null(visit.CanUse(down, party));
        party[3].Y += 1f;
        Assert.Equal(StairRefusal.PartyNotGathered, visit.CanUse(down, party));

        // Outside the player phase nobody takes the stairs, gathered or not.
        Gather(visit, party, down.Tile);
        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.Equal(StairRefusal.NotThePlayersTurn, visit.CanUse(down, party));
        Advance(turns);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Gather(visit, party, down.Tile);
        Assert.Null(visit.CanUse(down, party));

        // A stair this floor does not have is no question the visit answers —
        // the first floor's arrival tile is the way out, never a way up.
        Assert.Throws<ArgumentException>(() => visit.CanUse(new StairUse(Stair.Up, floor.UpStairs), party));
        Assert.Throws<ArgumentException>(() => visit.CanUse(new StairUse(Stair.Down, floor.UpStairs), party));

        // And between the stairs and the next floor's opening, no system is open to take them.
        visit.Descend(party);
        Assert.Null(visit.Turns);
        Assert.Equal(StairRefusal.NotThePlayersTurn, visit.CanUse(new StairUse(Stair.Up, visit.Current.UpStairs), party));
    }

    [Fact]
    public void TheStairsAreTakenFromTheStairTile()
    {
        // The stairs are used with the active member standing on them and the
        // living party gathered. Handed the active member, the visit holds the
        // first half as well as the second, so a stair is never taken from beside
        // it: this seed's spear dummy stands on the first floor's down-stairs, and
        // a party gathered round it is gathered, but nobody stands on the stairs,
        // so they wait until the dummy falls — it guards them and never seals them.
        var party = Party(Maul());
        var visit = DungeonState.Enter(Tutorial, GuardedStairsSeed, party);
        var first = visit.Current;
        var stairs = first.DownStairs!.Value;
        Assert.Equal((19, 4), stairs);
        var guard = Row(first, 9, "phalanx_spear");
        Assert.Equal(stairs, TileOf(guard));
        var down = new StairUse(Stair.Down, stairs);
        var turns = visit.OpenTurns(party, Ten);

        // Gathered round the live guard: all four within reach, none on the stairs.
        Gather(visit, party, stairs);
        Assert.Equal(party.Length, visit.Gathered(party, stairs));
        Assert.All(party, m => Assert.Null(visit.StairUnder(m)));
        Assert.Null(visit.CanUse(down, party));             // the party's half alone is met...
        foreach (var member in party)                       // ...and nobody stands on them to take them
            Assert.Throws<ArgumentException>(() => visit.CanUse(down, party, member));
        Assert.Throws<ArgumentException>(() => visit.Descend(party, party[0]));
        Assert.Equal(0, visit.FloorIndex);
        Assert.Same(turns, visit.Turns);                     // refused before anything moved: the floor is still open

        // Put down, it is only a corpse: a member steps over it onto the stairs,
        // and they are that member's to take — not the one beside them, nor a
        // stranger standing on them, nor a member who has fallen there.
        Kill(turns, first, party[0], guard);
        Put(party[0], stairs);
        Assert.Equal(down, visit.StairUnder(party[0]));
        Assert.Null(visit.CanUse(down, party, party[0]));
        Assert.Throws<ArgumentException>(() => visit.CanUse(down, party, party[1]));
        var stranger = TestPools.Holding("X", TestWeapons.Get("weakspot_stiletto"));
        Put(stranger, stairs);
        Assert.Equal(down, visit.StairUnder(stranger));
        Assert.Throws<ArgumentException>(() => visit.CanUse(down, party, stranger));
        party[0].Alive = false;
        Assert.Throws<ArgumentException>(() => visit.CanUse(down, party, party[0]));
        party[0].Alive = true;

        visit.Descend(party, party[0]);
        Assert.Equal(1, visit.FloorIndex);

        // Up the same way: the member who arrived on the up-stairs takes them,
        // and the one beside cannot.
        var second = visit.Current;
        visit.OpenTurns(party, Ten);
        var up = new StairUse(Stair.Up, second.UpStairs);
        Assert.Equal(up, visit.StairUnder(party[0]));
        Assert.Throws<ArgumentException>(() => visit.Ascend(party, party[1]));
        Assert.Equal(1, visit.FloorIndex);
        visit.Ascend(party, party[0]);
        Assert.Equal(0, visit.FloorIndex);

        // And the way out asks the same of whoever would take it.
        visit.OpenTurns(party, Ten);
        Gather(visit, party, first.UpStairs);
        var exit = new StairUse(Stair.Exit, first.UpStairs);
        Assert.Equal(exit, visit.StairUnder(party[0]));
        Assert.Null(visit.CanUse(exit, party, party[0]));
        Assert.Throws<ArgumentException>(() => visit.CanUse(exit, party, party[1]));
    }

    [Fact]
    public void TheFallenTravelWithTheParty()
    {
        // Nobody is left on another floor: a dead member is carried, placed on the
        // new floor's spawn tiles with the living, in roster order — down, and
        // back up again.
        var party = Party();
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.Current;
        visit.OpenTurns(party, Ten);

        var fallen = party[2];
        fallen.Alive = false;
        fallen.Hp = 0;
        Gather(visit, party, first.DownStairs!.Value);
        Put(fallen, first.UpStairs);                // it fell far from the stairs
        visit.Descend(party);

        var second = visit.Current;
        var tiles = GridUtils.FindPartySpawnTiles(second.Map.Grid, second.UpStairs.R, second.UpStairs.C, party.Length, LivingEnemyTiles(second));
        Assert.Equal(tiles, party.Select(TileOf));
        Assert.Equal(tiles[2], TileOf(fallen));
        Assert.False(fallen.Alive);

        visit.OpenTurns(party, Ten);
        Put(fallen, second.Lair!.Value);            // wherever the body lies
        visit.Ascend(party);

        var down = first.DownStairs!.Value;
        tiles = GridUtils.FindPartySpawnTiles(first.Map.Grid, down.R, down.C, party.Length, LivingEnemyTiles(first));
        Assert.Equal(tiles, party.Select(TileOf));
        Assert.False(fallen.Alive);
    }

    [Fact]
    public void ARecordedBossDeathHoldsOnEveryFloorOfTheVisit()
    {
        // The boss's death stops resurrection for the rest of the visit, on every
        // floor — not only the one it happened on, whose system is dropped at the
        // next stairs. Recorded on the second floor at turn 10, it opens the first
        // and the third with resurrection off, and a floor caught up afterwards is
        // caught up only to turn 10: a return due before then still happens (due
        // at 8), one due after never does (due at 11, though the party is back at
        // 13). The seam the boss's death calls, through the subscription OpenTurns
        // makes; this calls it directly, on a floor that is not the boss's.
        var party = Party(Maul());
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var first = visit.Current;
        var turns = visit.OpenTurns(party, Ten);

        var dueBefore = Row(first, 23, "hatchet");
        Kill(turns, first, party[0], dueBefore);    // turn 0: due at 8
        Home(visit, party);
        PassTurns(turns, 3);
        var dueAfter = Row(first, 9, "hatchet");
        Kill(turns, first, party[0], dueAfter);     // turn 3: due at 11

        var second = Down(visit, party);
        PassTurns(second, 7);                        // turn 10
        visit.RecordBossDefeated();
        Assert.Equal((true, 10), (visit.BossDefeated, visit.BossDefeatedAtTurn));
        Assert.False(second.ResurrectionActive);
        PassTurns(second, 3);                        // turn 13
        visit.RecordBossDefeated();                  // one-way: a second call moves nothing
        Assert.Equal(10, visit.BossDefeatedAtTurn);

        var back = Up(visit, party);
        Assert.Equal(13, back.TurnCount);
        Assert.False(back.ResurrectionActive);
        Assert.True(dueBefore.Alive);
        Assert.False(dueAfter.Alive);
        Assert.NotNull(back.GroundItemOf(dueAfter));  // a permanent kill now: lying on what it carried
        Assert.Null(back.GroundItemOf(dueBefore));

        Home(visit, party);
        PassTurns(back, 2);
        Assert.False(dueAfter.Alive);

        Down(visit, party);
        var third = Down(visit, party);
        Assert.Equal(2, visit.FloorIndex);
        Assert.False(third.ResurrectionActive);
    }

    [Fact]
    public void TheBottomFloorsSystemHasItsBoss_AndItsDeathIsTheVisits()
    {
        // The boss is a row of the bottom floor like any other, and the system the
        // visit opens there rosters it as the boss: its death — and nothing short
        // of its death — is recorded by the visit, in Logic, before anything else
        // hears of it. After that the floor it lay on is a floor like any other:
        // left and come back to, the boss is still down where it fell, lying on the
        // same weapon, and nothing records it again.
        var party = Party(Maul());
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        visit.OpenTurns(party, Ten);
        Assert.Null(visit.Current.Boss);

        var below = Down(visit, party);
        var bottom = visit.Current;
        var boss = bottom.Boss!;
        Assert.Same(bottom.Enemies[^1], boss);
        Assert.True(below.ResurrectionActive);

        // Hurt, it has recorded nothing: a Maul's 60 into its Block 9 leaves 29 of its 80.
        StandBeside(bottom, party[0], boss);
        Assert.True(below.TryAttack(party[0], boss));
        Assert.Equal((true, 29), (boss.Alive, boss.Hp));
        Assert.False(visit.BossDefeated);

        bool recordedWhenHeard = false;
        below.BossDefeated += _ => recordedWhenHeard = visit.BossDefeated;
        Assert.True(below.TryAttack(party[0], boss));
        Assert.False(boss.Alive);
        Assert.True(recordedWhenHeard);
        Assert.Equal((true, 0), (visit.BossDefeated, visit.BossDefeatedAtTurn));
        Assert.False(below.ResurrectionActive);
        var drop = below.GroundItemOf(boss);
        Assert.NotNull(drop);

        Home(visit, party);
        PassTurns(below, 3);
        var above = Up(visit, party);
        Assert.False(above.ResurrectionActive);
        var again = Down(visit, party);
        Assert.False(again.ResurrectionActive);
        Assert.False(boss.Alive);
        Assert.Equal(TileOf(party[0]), bottom.UpStairs);
        Assert.Equal((true, 0), (visit.BossDefeated, visit.BossDefeatedAtTurn));
        var still = again.GroundItemOf(boss);
        Assert.NotNull(still);
        Assert.Equal((drop.Weapon.Id, drop.Weapon.Modifiers, drop.DefeatCount), (still.Weapon.Id, still.Weapon.Modifiers, still.DefeatCount));
    }

    [Fact]
    public void ThePartyMayLeaveAFloorWithEveryEnemyStanding()
    {
        // PHASE4-118, -120, -123: no gate. A floor can be run rather than fought —
        // a percentage-of-enemies requirement was considered and rejected — so
        // the stairs ask two things and never a third: whose turn it is, and
        // whether the party is together.
        Assert.Equal(new[] { StairRefusal.NotThePlayersTurn, StairRefusal.PartyNotGathered }, Enum.GetValues<StairRefusal>());

        var party = Party();
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        visit.OpenTurns(party, Ten);

        // All the way down with every enemy on every floor standing...
        while (visit.Current.DownStairs is { } down)
        {
            Assert.All(visit.Current.Enemies, e => Assert.True(e.Alive));
            Gather(visit, party, down);
            Assert.Null(visit.CanUse(new StairUse(Stair.Down, down), party));
            visit.Descend(party);
            visit.OpenTurns(party, Ten);
        }
        Assert.Equal(visit.BossFloor - 1, visit.FloorIndex);

        // ...and all the way back out the same way.
        while (visit.FloorIndex > 0)
        {
            Gather(visit, party, visit.Current.UpStairs);
            Assert.Null(visit.CanUse(new StairUse(Stair.Up, visit.Current.UpStairs), party));
            visit.Ascend(party);
            visit.OpenTurns(party, Ten);
        }
        Gather(visit, party, visit.Current.UpStairs);
        Assert.Null(visit.CanUse(new StairUse(Stair.Exit, visit.Current.UpStairs), party));
        Assert.All(visit.Floors.SelectMany(f => f.Enemies), e => Assert.True(e.Alive));
    }

    [Fact]
    public void FloorsAreAdjacentOnly_TheWayOutIsUpThroughEveryFloor()
    {
        // PHASE4-031, -099: no shortcut exit. One floor per transit, down or up;
        // the bottom has no way down; and the only way out is the first floor's
        // up-stairs, so a party leaves by climbing back through every floor it
        // came down. Every arrival is announced, one floor at a time, and says
        // whether it is the party's first time on that floor.
        var party = Party();
        var walker = party[0];
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var reached = new List<(int Index, bool FirstVisit)>();
        visit.FloorReached += (floor, firstVisit) => reached.Add((floor.Index, firstVisit));
        visit.OpenTurns(party, Ten);

        for (int n = 0; n < visit.BossFloor - 1; n++)
        {
            var floor = visit.Current;
            Assert.Equal(n, visit.FloorIndex);
            Put(walker, floor.UpStairs);
            Assert.Equal(new StairUse(n == 0 ? Stair.Exit : Stair.Up, floor.UpStairs), visit.StairUnder(walker));
            Put(walker, floor.DownStairs!.Value);
            Assert.Equal(new StairUse(Stair.Down, floor.DownStairs!.Value), visit.StairUnder(walker));

            Down(visit, party);
            Assert.Equal((n + 1, n + 1), (visit.FloorIndex, visit.DeepestFloorIndex));
        }

        // The bottom: no way down, and the lair is no stair.
        var bottom = visit.Current;
        Assert.Null(bottom.DownStairs);
        Put(walker, bottom.Lair!.Value);
        Assert.Null(visit.StairUnder(walker));
        Assert.Throws<InvalidOperationException>(() => visit.Descend(party));
        Assert.Throws<ArgumentException>(() => visit.CanUse(new StairUse(Stair.Down, bottom.Lair!.Value), party));
        Put(walker, bottom.UpStairs);
        Assert.Equal(new StairUse(Stair.Up, bottom.UpStairs), visit.StairUnder(walker));

        // Up, one floor per transit, through every floor on the way.
        for (int n = visit.BossFloor - 1; n > 0; n--)
        {
            Up(visit, party);
            Assert.Equal(n - 1, visit.FloorIndex);
        }
        Assert.Equal(visit.BossFloor - 1, visit.DeepestFloorIndex);

        // The first floor's up-stairs are the way out, and never a way up.
        Put(walker, visit.Current.UpStairs);
        Assert.Equal(new StairUse(Stair.Exit, visit.Current.UpStairs), visit.StairUnder(walker));
        Assert.Throws<InvalidOperationException>(() => visit.Ascend(party));
        Assert.Equal(0, visit.FloorIndex);

        // The dead use no stairs.
        walker.Alive = false;
        Assert.Null(visit.StairUnder(walker));
        walker.Alive = true;

        Assert.Equal(new[] { (1, true), (2, true), (3, true), (4, true), (3, false), (2, false), (1, false), (0, false) }, reached);
    }

    [Fact]
    public void FloorReachedTellsAFirstArrivalFromAReturn()
    {
        // Every transit is announced, and each says whether the party has stood on
        // that floor before in this visit — the one thing a subscriber cannot read
        // off the visit when it hears, since by then the floor is Visited and the
        // deepest index counts it. A return is a return whichever way the party
        // came: down onto a floor already stood on is no first arrival.
        var party = Party();
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        var heard = new List<(int Index, bool FirstVisit, bool Visited, int Deepest)>();
        visit.FloorReached += (floor, firstVisit) => heard.Add((floor.Index, firstVisit, floor.Visited, visit.DeepestFloorIndex));
        visit.OpenTurns(party, Ten);

        Down(visit, party);   // the second floor, for the first time
        Up(visit, party);     // the first, again
        Down(visit, party);   // the second again: a descent, and still a return
        Down(visit, party);   // the third, for the first time

        Assert.Equal(new[] { (1, true, true, 1), (0, false, true, 1), (1, false, true, 1), (2, true, true, 2) }, heard);
    }

    // ── The turn that travels ────────────────────────────────────────────────

    [Fact]
    public void TransitKeepsTheTurnAndTheBudgets()
    {
        // Transit keeps the turn — all of it. Members arrive with the movement
        // they left with, carrying their statuses, in the same player phase, with
        // nothing ticked. And the turn's counters go with them: a down-and-up hop
        // is free (the party arrives back where it left), so were the counters
        // zeroed, one hop would hand the thrower a fresh Charges allowance and
        // every reactor fresh uses inside a single turn. Instead the thrower is
        // still out of throws, the pike's brace and the sword's counter stay
        // spent, and the enemies seen before the hop are still seen on the floor
        // returned to. The next turn is fresh, as every turn is.
        var thrower = TestPools.Holding("T", TestWeapons.Get("darts"), colorIndex: 0);
        var blade = TestPools.Holding("B", TestWeapons.Get("weakspot_stiletto"), colorIndex: 1);
        var sick = TestPools.Holding("S", TestWeapons.Get("tower_guard"), colorIndex: 2);
        var walker = TestPools.Holding("M", TestWeapons.Get("weakspot_stiletto"), colorIndex: 3);
        PartyMemberState[] party = [thrower, blade, sick, walker];
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.Current;
        int swapCost = GameContent.Current.Tuning.WeaponSwapCost;

        // Two of the first floor's dummies, stood in its arrival room (moved rows:
        // stored state): a pike, Brace x1, in the room's top-left corner, and a
        // Riposte Blade two tiles below the thrower.
        Assert.Equal((37, 30), first.UpStairs);
        Assert.Equal((37, 30), TileOf(thrower));
        var pike = first.Enemies.First(e => e.Weapon.Id == "skirmishers_pike");
        var sword = first.Enemies.First(e => e.Weapon.Id == "riposte_blade");
        Put(pike, (34, 26));
        Put(sword, (39, 30));

        var turns = visit.OpenTurns(party, Ten);
        int braces = 0, ripostes = 0;
        turns.EnemyBraceTriggered += _ => braces++;
        turns.RiposteTriggered += _ => ripostes++;
        turns.NotifyEnemyVisible(pike, true);
        turns.NotifyEnemyVisible(sword, true);
        Assert.Equal(swapCost, turns.SwapCost);

        // The thrower spends its cap on the sword: the first throw blocked and
        // countered, the second blocked and not — the blade counters once a turn.
        Assert.Equal(2, turns.AttacksPerTurn(thrower));
        Assert.True(turns.TryAttack(thrower, sword));
        Assert.True(turns.TryAttack(thrower, sword));
        Assert.False(turns.HasAttackLeft(thrower));
        Assert.Equal(1, ripostes);

        // The walker steps into the pike's reach from outside it: braced, and the
        // pike's one brace this turn is spent.
        Assert.False(EnemyAi.CanHit(pike, walker, pike.Weapon, first.Map.Grid));
        Put(walker, (35, 28));
        Assert.True(EnemyAi.CanHit(pike, walker, pike.Weapon, first.Map.Grid));
        turns.NotifyCharacterMoved(walker);
        Assert.Equal(1, braces);

        sick.ApplyStatus(StatusEffectType.Poison, null, 3);

        // Down to the second floor.
        Gather(visit, party, first.DownStairs!.Value);
        var carried = party.Select(m => (m.DistLeft, m.Hp, m.StatusLevel(StatusEffectType.Poison))).ToArray();
        var below = Down(visit, party);

        Assert.Equal((TurnPhase.Player, 0), (below.Phase, below.TurnCount));
        Assert.Equal(carried, party.Select(m => (m.DistLeft, m.Hp, m.StatusLevel(StatusEffectType.Poison))));
        Assert.Equal(2, below.AttacksThisTurn(thrower));
        Assert.False(below.HasAttackLeft(thrower));
        Assert.False(below.AnyLiveEnemySeenThisTurn);   // nothing here seen yet this turn
        Assert.Equal(0, below.SwapCost);

        // And straight back up: the floor left this turn, counters and all.
        var back = Up(visit, party);
        Assert.Equal((TurnPhase.Player, 0), (back.Phase, back.TurnCount));
        Assert.Equal(carried, party.Select(m => (m.DistLeft, m.Hp, m.StatusLevel(StatusEffectType.Poison))));
        Assert.False(back.HasAttackLeft(thrower));
        Assert.True(back.AnyLiveEnemySeenThisTurn);      // seen before the hop: still seen
        Assert.Equal(swapCost, back.SwapCost);

        int bracesBack = 0, ripostesBack = 0;
        back.EnemyBraceTriggered += _ => bracesBack++;
        back.RiposteTriggered += _ => ripostesBack++;

        // The very entry the pike braced before the hop, made again: its zone is
        // armed (seen), and its brace is gone.
        Assert.False(EnemyAi.CanHit(pike, walker, pike.Weapon, first.Map.Grid));
        Put(walker, (35, 28));
        back.NotifyCharacterMoved(walker);
        Assert.Equal(0, bracesBack);

        // A blocked hit on the sword, and its counter is gone too.
        Put(blade, (39, 29));
        back.NotifyCharacterMoved(blade);
        int hp = sword.Hp;
        Assert.True(back.TryAttack(blade, sword));
        Assert.True(sword.Alive && sword.Hp < hp);
        Assert.Equal(0, ripostesBack);

        // What the floor's system holds is the turn's: the throws and the blade's
        // swing, the pike's brace, the sword's counter, both dummies seen.
        var carry = back.ExportCarry();
        Assert.Equal(0, carry.Turn);
        Assert.Equal((2, 1), (carry.Attacks[thrower], carry.Attacks[blade]));
        Assert.Equal(1, carry.ZoneUses[pike]);
        Assert.Equal(1, carry.ReactionUses[(sword, ModifierType.Riposte)]);
        Assert.Contains(pike, carry.Seen);
        Assert.Contains(sword, carry.Seen);

        // A system takes a carry up only on the turn it was spent in: opened on
        // turn 0 it inherits every counter, opened on turn 1 none of them.
        var sameTurn = new TurnSystem(first.Map.Grid, party, first.Enemies, Ten, first.Seed, firstTurn: 0, carry: carry);
        Assert.Equal((2, true, swapCost), (sameTurn.AttacksThisTurn(thrower), sameTurn.AnyLiveEnemySeenThisTurn, sameTurn.SwapCost));
        var laterTurn = new TurnSystem(first.Map.Grid, party, first.Enemies, Ten, first.Seed, firstTurn: 1, carry: carry);
        Assert.Equal((0, false, 0), (laterTurn.AttacksThisTurn(thrower), laterTurn.AnyLiveEnemySeenThisTurn, laterTurn.SwapCost));
        Assert.Empty(laterTurn.ExportCarry().ZoneUses);

        // The next player turn is fresh, as any turn's start makes it.
        back.EndTurn();
        Advance(back);
        Assert.Equal((TurnPhase.Player, 1), (back.Phase, back.TurnCount));
        Assert.True(back.HasAttackLeft(thrower));
        Assert.False(back.AnyLiveEnemySeenThisTurn);
        var fresh = back.ExportCarry();
        Assert.Equal(1, fresh.Turn);
        Assert.Empty(fresh.Attacks);
        Assert.Empty(fresh.ZoneUses);
        Assert.Empty(fresh.ReactionUses);
        Assert.Empty(fresh.Seen);

        // And the spent turn stays behind: a hop in the new turn leaves the
        // thrower its throws — a carry outliving its turn would starve them.
        var next = Down(visit, party);
        Assert.Equal(1, next.TurnCount);
        Assert.Equal(0, next.AttacksThisTurn(thrower));
        Assert.True(next.HasAttackLeft(thrower));
    }

    [Fact]
    public void ArrivalNeverStandsAMemberOnALiveEnemy()
    {
        // The stair draw never looks at the rows, so a dummy can stand on the very
        // stairs the party arrives by — this seed's first floor has a spear dummy
        // on its down-stairs. Climbing back up onto them, the party is placed
        // around it, never inside it, and it is not moved for them. (In play the
        // stairs are taken with the active member standing on them — CanUse,
        // handed that member: TheStairsAreTakenFromTheStairTile — which a live
        // dummy there prevents; this descent omits the member, and the placement
        // does not rely on the rule.) A corpse there is another matter: it is
        // walked over, and a member stands on the stairs.
        var party = Party();
        var visit = DungeonState.Enter(Tutorial, GuardedStairsSeed, party);
        var first = visit.Current;
        var stairs = first.DownStairs!.Value;
        var guard = Row(first, 9, "phalanx_spear");
        Assert.Equal(stairs, TileOf(guard));
        Assert.True(guard.Alive);

        visit.OpenTurns(party, Ten);
        Down(visit, party);
        Up(visit, party);

        var tiles = GridUtils.FindPartySpawnTiles(first.Map.Grid, stairs.R, stairs.C, party.Length,
            new HashSet<(int R, int C)> { stairs });
        Assert.Equal(new[] { (18, 4), (18, 5), (19, 5), (20, 4) }, tiles.Select(t => (t.R, t.C)));
        Assert.Equal(tiles, party.Select(TileOf));
        Assert.DoesNotContain(party, m => TileOf(m) == TileOf(guard));
        Assert.Equal(party.Length, party.Select(TileOf).Distinct().Count());
        Assert.All(party, m => Assert.Equal(0, first.Map.Grid[TileOf(m).R, TileOf(m).C]));
        Assert.Equal(stairs, TileOf(guard));
        Assert.Equal(party.Length, visit.Gathered(party, stairs));   // arrived gathered: the stairs are usable from here

        // Down on its tile, it is only a corpse: the party arrives onto the stairs.
        guard.Alive = false;
        guard.DefeatCount = 1;
        guard.DefeatedAtTurn = visit.Turn;
        Down(visit, party);
        Up(visit, party);
        Assert.Equal(stairs, TileOf(party[0]));
        Assert.False(guard.Alive);                   // and a hop inside the turn raises nothing
    }

    [Fact]
    public void ABraceStillFiresOnAnotherFloor()
    {
        // PHASE4-121, re-pinned below the first floor: Brace fires from every zone
        // crossed, whichever floor's system is running. That is the price of
        // running a floor rather than fighting it: a member crossing the second
        // floor's arrival room past two seen spears is braced by each, in the
        // order it walks into them. (The two spears are the second floor's rows,
        // stood in its arrival room before the party comes down: stored state.)
        var party = Party();
        var walker = party[0];
        var visit = DungeonState.Enter(SpearHall, GoldenSeed, party);
        visit.OpenTurns(party, Ten);

        var second = visit.Floors[1];
        Assert.Equal((37, 10), second.UpStairs);
        var spears = second.Enemies.Where(e => e.Weapon.Id is "skirmishers_pike" or "phalanx_spear").Take(2).ToArray();
        Assert.Equal(2, spears.Length);
        Put(spears[0], (40, 9));
        Put(spears[1], (40, 14));

        var turns = Down(visit, party);
        Assert.Same(second, visit.Current);
        var braced = new List<EnemyState>();
        turns.EnemyBraceTriggered += braced.Add;
        turns.NotifyEnemyVisible(spears[0], true);
        turns.NotifyEnemyVisible(spears[1], true);

        // Along row 36 from the room's west wall to its east, a step at a time.
        Put(walker, (36, 4));
        turns.NotifyCharacterMoved(walker);
        Assert.Empty(braced);
        for (float x = walker.X; x <= 15 * Tile + Tile / 2f; x += 8f)
        {
            (walker.X, walker.Y) = (x, 36 * Tile + Tile / 2f);
            turns.NotifyCharacterMoved(walker);
        }

        Assert.Equal(spears, braced);
        Assert.True(walker.Hp < TestPools.FixtureHp);
    }
}
