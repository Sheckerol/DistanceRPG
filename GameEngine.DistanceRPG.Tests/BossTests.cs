using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.3's boss: the one actor whose death stops resurrection. Every dungeon has
/// exactly one, on its bottom floor, built to its dungeon's statline — the
/// tutorial's Stone Golem is 80 HP, a movement budget of 40 and Block x3 on
/// itself whatever it holds — and wielding a weapon like a dummy does, rolled off
/// the visit's boss stream. Before it falls the dungeon is a farm; after, it is
/// a finite clear the party fights its way back out of (§4.4).
/// <para>
/// The scenarios run on real floors, on the shipped seed's tutorial — whose
/// golem holds a Pinning Lance in its lair at (59, 41) — and every fact a test
/// leans on is pinned by an assertion before it is used. In the content
/// collection, because one test edits the relations the boss's hands are
/// computed from.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class BossTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The shipped seed: its tutorial golem holds a Pinning Lance in the lair at (59, 41).</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>A tutorial seed whose bottom floor has a thrower (spawn 15, <c>bandolier</c>) standing in the lair at (58, 28).</summary>
    private const long HeldLairSeed = 1697034193;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static DungeonDef Tutorial => Dungeons.Tutorial;

    private static WeaponCatalogue Weapons => GameContent.Current.Weapons;

    /// <summary>A roll that neither crits nor fumbles.</summary>
    private static int Ten() => 10;

    /// <summary>A blow that goes through any Block a golem stands behind and all 80 of it: one swing, any enemy here.</summary>
    private static Weapon Sledge() => TestWeapons.Make("Sledge", range: 60, damage: 120, cost: 30);

    /// <summary>The sledge's blow with a cleave on it: one extra body the swing reaches, nearest first.</summary>
    private static Weapon Reaper() => TestWeapons.Make("Reaper", range: 60, damage: 120, cost: 30, (Cleave, 1));

    /// <summary>A blow that never clears a dummy's constitution, so every kill with it is worth exactly one cycle.</summary>
    private static Weapon Chisel() => TestWeapons.Make("Chisel", range: 60, damage: 30, cost: 10);

    /// <summary>Four fixture members in roster order, each holding the weapon at its place — a Weakspot Stiletto where none is named.</summary>
    private static PartyMemberState[] Party(params Weapon[] held)
        => Ids.Select((id, i) => TestPools.Holding(id, i < held.Length ? held[i] : TestWeapons.Get("weakspot_stiletto"), colorIndex: i))
            .ToArray();

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

    /// <summary>Everything about a drop a seed fixes: the item, its live spread, its entries, and the farm facts beside it.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] {string.Join(",", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}"))} x{drop.DefeatCount} {drop.WasUniqueRoll}";

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

    /// <summary>The tutorial on the shipped seed, the party on its bottom floor with that floor open; the first floor's system is returned too.</summary>
    private static (DungeonState Visit, TurnSystem First, TurnSystem Bottom) AtTheGolem(PartyMemberState[] party)
    {
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.OpenTurns(party, Ten);
        var bottom = Down(visit, party);
        Assert.Equal((2, 1), (visit.BossFloor, visit.FloorIndex));
        var golem = visit.Current.Boss!;
        Assert.Equal(("Stone Golem", "pinning_lance", (59, 41)), (golem.Title, golem.Weapon.Id, TileOf(golem)));
        return (visit, first, bottom);
    }

    // ── The golem, and what it teaches ───────────────────────────────────────

    [Fact]
    public void TheGolemTeachesBlock_Sword1Dagger6Axe9_TheDaggersCrit45()
    {
        // PHASE4-087/-088/-089/-091: the starting party spans the Block range
        // deliberately — the sword's 10, the dagger's 15, the axe's 18 — and the
        // golem's innate Block x3 absorbs 9 of every hit, never below 1: 1, 6, 9.
        // Then the dagger crits and lands 45, Block skipped, one roll in ten.
        // "The player discovers that by swinging": through the one damage chain
        // every swing takes, into a golem built from the tutorial's own statline,
        // holding a hatchet — one of the four fieldable classes with no Block of
        // its own, so every Block here is the golem's.
        var golem = DungeonState.BossRow(Tutorial, TestWeapons.Get("hatchet"));
        Assert.Equal(("stone_golem", "Stone Golem"), (Tutorial.Boss.Id, golem.Title));
        Assert.Equal((3, 0, 9), (golem.Innate.Stacks(Block), golem.Weapon.Modifiers.Stacks(Block), golem.Value(Block)));

        // The roster's own starting weapons, read off the roster.
        var starting = GameContent.Current.Party.All.Select(m => m.StartingWeaponId).ToArray();
        Assert.Contains("tower_guard", starting);
        Assert.Contains("weakspot_stiletto", starting);
        Assert.Contains("great_axe", starting);
        var sword = TestPools.Holding("B", TestWeapons.Get("tower_guard"));
        var dagger = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
        var axe = TestPools.Holding("C", TestWeapons.Get("great_axe"));
        Assert.Equal((10, 15, 18), (sword.EquippedWeapon!.Damage, dagger.EquippedWeapon!.Damage, axe.EquippedWeapon!.Damage));

        AttackResolution Hit(PartyMemberState attacker, int roll)
            => CombatRules.ResolveAttack(attacker, golem, attacker.EquippedWeapon!, distanceUnits: 0, () => roll);

        Assert.Equal((1, 6, 9), (Hit(sword, 10).Taken, Hit(dagger, 10).Taken, Hit(axe, 10).Taken));
        Assert.Equal((9, 9, 9), (Hit(sword, 10).Blocked, Hit(dagger, 10).Blocked, Hit(axe, 10).Blocked));

        // The crit: x3 on the dagger's 15, and the armour stops existing.
        var crit = Hit(dagger, 20);
        Assert.Equal((RollOutcome.Crit, 0, 45), (crit.Roll.Outcome, crit.Blocked, crit.Taken));

        // One roll in ten: the dagger crits on 19 and 20 and nothing else.
        Assert.Equal(19, CombatRules.CritThreshold(dagger));
        var crits = Enumerable.Range(1, 20).Where(roll => Hit(dagger, roll).Roll.Outcome == RollOutcome.Crit).ToArray();
        Assert.Equal(new[] { 19, 20 }, crits);
        Assert.All(crits, roll => Assert.Equal(45, Hit(dagger, roll).Taken));

        // Resolving wrote nothing: the golem is as it spawned.
        Assert.Equal((80, 80), (golem.Hp, golem.MaxHp));
    }

    [Fact]
    public void AGolemHoldingASwordStandsBehindTwelve_ATowerGuardFifteen()
    {
        // Weapon plus innate is literal: a golem that rolled a sword stands behind
        // its sword's Block and its own. An Arming Sword is Block x1 — with the
        // golem's x3, 12 — and a Tower Guard x2 — 15. The teaching numbers hold
        // on the four fieldable classes that forge no Block.
        var armed = DungeonState.BossRow(Tutorial, TestWeapons.Get("arming_sword"));
        Assert.Equal((1, 3, 4, 12), (armed.Weapon.Modifiers.Stacks(Block), armed.Innate.Stacks(Block), armed.Stacks(Block), armed.Value(Block)));
        var guarded = DungeonState.BossRow(Tutorial, TestWeapons.Get("tower_guard"));
        Assert.Equal((2, 3, 5, 15), (guarded.Weapon.Modifiers.Stacks(Block), guarded.Innate.Stacks(Block), guarded.Stacks(Block), guarded.Value(Block)));

        // The dagger's 15 into each: 3 through the Arming Sword's 12, and the
        // floor of 1 through the Tower Guard's 15.
        var dagger = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
        Assert.Equal(3, CombatRules.ResolveAttack(dagger, armed, dagger.EquippedWeapon!, 0, Ten).Taken);
        Assert.Equal(1, CombatRules.ResolveAttack(dagger, guarded, dagger.EquippedWeapon!, 0, Ten).Taken);

        foreach (var id in new[] { "flensing_knife", "halberd", "hatchet", "darts" })
            Assert.Equal(9, DungeonState.BossRow(Tutorial, TestWeapons.Get(id)).Value(Block));

        // In play: a tutorial seed whose golem rolled an Arming Sword stands behind 12.
        const long SwordSeed = 2084311110;
        var roll = DungeonState.RollBoss(SwordSeed, Tutorial);
        Assert.Equal((Sword, VariantRole.Efficiency), (roll.Class, roll.Role));
        var golem = Floor.Generate(SwordSeed, 1, bottom: true, Tutorial).Boss!;
        Assert.Equal(("arming_sword", 12), (golem.Weapon.Id, golem.Value(Block)));
    }

    [Fact]
    public void TheBossCarriesItsThemeWhateverItHolds()
    {
        // PHASE4-033, re-pinning -096: the golem has Block x3 whatever it holds —
        // every variant of every class an enemy fields — on the actor, never on
        // the weapon: the weapon is its variant exactly, and its ceiling is its
        // own forge's, the innate three nowhere in it.
        var wieldable = Dungeons.WieldClasses(Tutorial);
        Assert.Equal(EnemyPlacer.PlacedClasses, wieldable);   // Block takes every class, so its boss may hold any an enemy fields
        int headroom = GameContent.Current.Modifiers.AcquiredHeadroom;

        foreach (var cls in wieldable)
            foreach (var role in Enum.GetValues<VariantRole>())
            {
                var def = Weapons.Variant(cls, role);
                var golem = DungeonState.BossRow(Tutorial, Weapons.Instantiate(def.Id));
                int forged = def.Forged.GetValueOrDefault(Block);

                Assert.Equal(ModifierSet.Of((Block, 3)), golem.Innate);
                Assert.Equal(ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray()), golem.Weapon.Forged);
                Assert.Equal(forged, golem.Weapon.Modifiers.Stacks(Block));
                Assert.Equal(forged + 3, golem.Stacks(Block));
                Assert.Equal(forged + headroom, GameContent.Current.Modifiers.Cap(Block, golem.Weapon.Forged.Stacks(Block)));
            }

        // Worked on its own sword, the weapon tops out at its forge plus the
        // headroom — 1 + 5 — and the golem's three sit on top of that, never
        // inside it.
        var golemWithSword = DungeonState.BossRow(Tutorial, Weapons.Instantiate("arming_sword"));
        golemWithSword.Weapon.Acquire(Block, 20);
        Assert.Equal(1 + headroom, golemWithSword.Weapon.Modifiers.Stacks(Block));
        Assert.Equal(1 + headroom + 3, golemWithSword.Stacks(Block));

        // Every shipped boss carries its own dungeon's theme, whatever it rolled.
        foreach (var dungeon in Dungeons.All)
        {
            var boss = Floor.Generate(GoldenSeed, 0, bottom: true, dungeon).Boss!;
            Assert.Equal(dungeon.Boss.Innate[dungeon.Theme], boss.Innate.Stacks(dungeon.Theme));
            Assert.Equal(0, boss.Weapon.Forged.Stacks(dungeon.Theme) - Weapons[boss.Weapon.Id].Forged.GetValueOrDefault(dungeon.Theme));
        }
    }

    // ── One boss, in its lair ────────────────────────────────────────────────

    [Fact]
    public void ExactlyOneBossWaitsInTheLairOfTheBottomFloor()
    {
        // PHASE4-019: every dungeon has exactly one boss. It is the bottom
        // floor's, appended after every placed row — its spawn index follows every
        // dummy's, so no dummy's stream moves — built to its dungeon's statline and
        // holding the weapon the boss stream rolled. It stands in the lair, or on
        // the nearest free tile to it when a dummy was placed there first.
        foreach (var def in Dungeons.All)
        {
            var party = Party();
            var visit = DungeonState.Enter(def, GoldenSeed, party);
            var bottom = Assert.Single(visit.Floors, f => f.Boss is not null);
            Assert.Same(visit.Floors[^1], bottom);
            Assert.Single(visit.Floors.SelectMany(f => f.Enemies), e => e.Title is not null);

            var boss = bottom.Boss!;
            Assert.Same(boss, bottom.Enemies[^1]);
            Assert.Equal(bottom.Enemies.Count - 1, boss.SpawnIndex);
            Assert.All(bottom.Enemies.SkipLast(1), e => Assert.True(e.SpawnIndex < boss.SpawnIndex));

            Assert.Equal((def.Boss.Name, def.Boss.MaxHp, def.Boss.MaxHp, (float)def.Boss.MovementBudget),
                (boss.Title, boss.MaxHp, boss.Hp, boss.MoveBudget));
            Assert.Equal(def.Attunement, boss.Attunement);
            Assert.Equal((0, 2, true), (boss.DefeatCount, boss.TurnsSinceSeen, boss.Alive));   // waiting in its lair until seen

            var roll = DungeonState.RollBoss(GoldenSeed, def);
            Assert.Equal(Weapons.Variant(roll.Class, roll.Role).Id, boss.Weapon.Id);
            Assert.Contains(boss.Weapon.Class, Dungeons.WieldClasses(def));

            var placed = bottom.Enemies.SkipLast(1).Select(Displacer.TileOf).ToHashSet();
            Assert.Equal(GridUtils.NearestFreeTile(bottom.Map.Grid, bottom.Lair!.Value, placed.Contains), TileOf(boss));
        }

        // The shipped tutorial: the lair is free, and the golem stands in it.
        var golden = Floor.Generate(GoldenSeed, 1, bottom: true, Tutorial);
        Assert.Equal((59, 41), golden.Lair);
        Assert.Equal(golden.Lair, TileOf(golden.Boss!));

        // A thrower was placed in this seed's lair: it stays where the placer put
        // it, and the golem takes the first free floor tile from the lair — up,
        // down, left, right, breadth-first — which is the one above it.
        Assert.Equal(2, DungeonState.RollBossFloor(HeldLairSeed, Tutorial));
        var held = Floor.Generate(HeldLairSeed, 1, bottom: true, Tutorial);
        Assert.Equal((58, 28), held.Lair);
        var squatter = Assert.Single(held.Enemies, e => TileOf(e) == held.Lair!.Value);
        Assert.Equal((15, "bandolier"), (squatter.SpawnIndex, squatter.Weapon.Id));
        long floorSeed = DungeonState.FloorSeed(HeldLairSeed, 1);
        var bare = MapGenerator.Generate(new Mulberry32(floorSeed));
        var spawned = EnemyPlacer.PlaceEnemies(bare, floorSeed)[squatter.SpawnIndex];
        Assert.Equal((spawned.X, spawned.Y), (squatter.X, squatter.Y));

        var golem = held.Boss!;
        Assert.Equal((57, 28), TileOf(golem));
        Assert.Equal(0, held.Map.Grid[57, 28]);
        Assert.DoesNotContain(held.Enemies.SkipLast(1), e => TileOf(e) == TileOf(golem));
        Assert.Equal(Centre((57, 28)), (golem.X, golem.Y));
    }

    [Fact]
    public void TheBossRollIsFloorThenClassThenRole()
    {
        // The boss stream draws the floor, then the class the boss holds — over
        // the compiled placed classes, redrawn while its dungeon does not allow it
        // — then the role. Verbatim on the tutorial, whose Block allows every
        // class an enemy fields, so no draw is ever redrawn there.
        var placed = EnemyPlacer.PlacedClasses;
        foreach (long seed in new long[] { GoldenSeed, HeldLairSeed, 1, 7, 4294967295 })
        {
            var rng = new Mulberry32(seed ^ 0x165667B1);
            int floor = 2 + rng.NextInt(0, 0);
            var cls = placed[rng.NextInt(0, placed.Count - 1)];
            var role = (VariantRole)rng.NextInt(0, 3);
            Assert.Equal(new BossRoll(floor, cls, role), DungeonState.RollBoss(seed, Tutorial));
        }

        // Over the Spear Hall, whose Brace only daggers, swords and spears take:
        // deterministic, the floor never moved by the draws after it, the class
        // always one its dungeon allows — and uniform over those three. A
        // re-steer from a refused class to the next allowed one would put a dagger
        // in its hands three times in five.
        var hall = Dungeons["spear_hall"];
        var allowed = Dungeons.WieldClasses(hall);
        Assert.Equal(new[] { Dagger, Sword, Spear }, allowed);
        var counts = new Dictionary<WeaponClass, int>();
        var roles = new HashSet<VariantRole>();
        const int Seeds = 3000;
        for (int i = 0; i < Seeds; i++)
        {
            long seed = (uint)(i * 2654435761u);
            var roll = DungeonState.RollBoss(seed, hall);
            Assert.Equal(roll, DungeonState.RollBoss(seed, hall));
            Assert.Equal(DungeonState.RollBossFloor(seed, hall), roll.Floor);
            Assert.Contains(roll.Class, allowed);
            counts[roll.Class] = counts.GetValueOrDefault(roll.Class) + 1;
            roles.Add(roll.Role);
        }
        Assert.All(allowed, cls => Assert.InRange(counts[cls] / (double)Seeds, 1 / 3.0 - 0.03, 1 / 3.0 + 0.03));
        Assert.Equal(Enum.GetValues<VariantRole>(), roles.Order());
    }

    [Fact]
    public void ARelationsEditThatKeepsTheRolledClassKeepsTheBoss()
    {
        // No draw's range depends on content: content decides only whether a
        // class draw is kept. So with Brace out of the threat-zone group — which
        // admits the axe to the Brace tables, and to the Spear Hall boss's hands —
        // a seed whose class draws never touched the axe keeps its class and its
        // role, and only a seed with a class draw on the axe moves: to the axe, the
        // first draw that now stands.
        var braceFreed = ContentDefaults.Restricted with
        {
            Excludes = ContentDefaults.Restricted.Excludes
                .Select(g => g.Contains(nameof(Brace)) ? g.Where(id => id != nameof(Brace)).ToArray() : g)
                .ToList(),
        };
        var seeds = Enumerable.Range(0, 600).Select(i => (long)(uint)(i * 2654435761u)).ToArray();
        var hall = Dungeons["spear_hall"];
        var before = seeds.Select(s => DungeonState.RollBoss(s, hall)).ToArray();

        BossRoll[] after;
        using (TestContent.Use(restricted: braceFreed))
        {
            var edited = GameContent.Current.Dungeons["spear_hall"];
            Assert.Equal(new[] { Dagger, Sword, Spear, Axe }, GameContent.Current.Dungeons.WieldClasses(edited));
            after = seeds.Select(s => DungeonState.RollBoss(s, edited)).ToArray();
        }

        // The class draws each seed made before the edit, up to the one kept.
        WeaponClass[] ClassDraws(long seed)
        {
            var rng = new Mulberry32(seed ^ DungeonState.BossSalt);
            rng.NextInt(0, 5);   // the floor
            var draws = new List<WeaponClass>();
            do draws.Add(EnemyPlacer.PlacedClasses[rng.NextInt(0, EnemyPlacer.PlacedClasses.Count - 1)]);
            while (draws[^1] is not (Dagger or Sword or Spear));
            return draws.ToArray();
        }

        int kept = 0, moved = 0, firstDrawKept = 0;
        for (int i = 0; i < seeds.Length; i++)
        {
            var draws = ClassDraws(seeds[i]);
            Assert.Equal(draws[^1], before[i].Class);
            Assert.Equal(before[i].Floor, after[i].Floor);
            if (draws.Contains(Axe))
            {
                Assert.Equal(Axe, after[i].Class);   // the first axe draw stands now
                Assert.NotEqual(Axe, before[i].Class);
                moved++;
            }
            else
            {
                Assert.Equal(before[i], after[i]);   // class and role
                kept++;
                if (draws.Length == 1) firstDrawKept++;
            }
        }
        Assert.True(kept > 0 && moved > 0 && firstDrawKept > 0, $"kept {kept}, moved {moved}, first draw kept {firstDrawKept}");
    }

    // ── Killing it stops resurrection ────────────────────────────────────────

    [Fact]
    public void KillingTheBossStopsResurrection_HereAndOnEveryFloorAfter()
    {
        // PHASE4-027/-028/-128: once the boss is down the dummies stop reviving,
        // for the rest of the visit — on the floor it fell on, and on every floor
        // opened after. Two dummies put down at turn 0, one on each floor, each due
        // back at turn 8; the golem falls at turn 0; twelve turns on each floor
        // later, both are still corpses.
        var party = Party(Sledge());
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.Current;
        var turns = visit.OpenTurns(party, Ten);
        var above = Row(first, 23, "hatchet");
        Kill(turns, first, party[0], above);
        Assert.Equal(8, above.DefeatedAtTurn + FarmLadder.ResurrectTurns(above.DefeatCount));

        var bottom = Down(visit, party);
        var floor = visit.Current;
        var golem = floor.Boss!;
        var below = Row(floor, 22, "hatchet");
        Kill(bottom, floor, party[0], below);
        Assert.Equal(8, below.DefeatedAtTurn + FarmLadder.ResurrectTurns(below.DefeatCount));
        Assert.True(bottom.ResurrectionActive);

        Kill(bottom, floor, party[0], golem);
        Assert.False(bottom.ResurrectionActive);
        Assert.Equal((true, 0), (visit.BossDefeated, visit.BossDefeatedAtTurn));

        Home(visit, party);
        PassTurns(bottom, 12);
        Assert.False(below.Alive);
        Assert.False(golem.Alive);
        Assert.NotNull(bottom.GroundItemOf(below));   // finite and clearable: what is down stays down, lying on its weapon

        var back = Up(visit, party);
        Assert.False(back.ResurrectionActive);
        Assert.Equal(12, back.TurnCount);
        Assert.False(above.Alive);                     // its time came at 8; the boss fell at 0
        Home(visit, party);
        PassTurns(back, 12);
        Assert.False(above.Alive);
        Assert.NotNull(back.GroundItemOf(above));
    }

    [Fact]
    public void BeforeTheBossTheFarmRuns()
    {
        // PHASE4-029: while the boss stands, the dungeon is an infinite and an
        // accelerating farm — its own floor included. A dummy put down three times
        // beside the golem's lair comes back every time, quicker each time and
        // stronger for it; DefeatCount climbs only because it does come back; and
        // nothing is handed over while it runs.
        var party = Party(Chisel());
        var (visit, _, turns) = AtTheGolem(party);
        var floor = visit.Current;
        var dummy = Row(floor, 22, "hatchet");
        var dropped = new List<EnemyState>();
        turns.EnemyDropped += (corpse, _) => dropped.Add(corpse);

        var waits = new List<int>();
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            Kill(turns, floor, party[0], dummy);
            Assert.Equal(cycle, dummy.DefeatCount);
            Assert.Null(turns.GroundItemOf(dummy));   // carrying, not offering
            Home(visit, party);
            int waited = 0;
            while (!dummy.Alive)
            {
                Assert.True(waited < 12, "the dummy never came back");
                PassTurns(turns, 1);
                waited++;
            }
            waits.Add(waited);
            Assert.Equal(cycle, dummy.RevivalsRolled);
        }

        Assert.Equal(new[] { 10 - 1, 10 - 2, 10 - 3 }, waits);   // resurrectTurns = max(3, 10 - DefeatCount)
        Assert.True(dummy.RevivalDamage + dummy.RevivalMaxHp > 0);
        Assert.Empty(dropped);
        Assert.True(turns.ResurrectionActive);
        Assert.False(visit.BossDefeated);
        Assert.True(floor.Boss!.Alive);
    }

    [Fact]
    public void WhatWasDownWhenTheBossFellStaysDownAndDrops()
    {
        // PHASE4-101/-129: loot materialises on permanent death, and the boss's
        // death is what makes a death permanent. A reaver put down before it is
        // lying on its weapon the moment the boss falls; so is the hatchet that
        // fell earlier in the very swing that felled the boss, whose own defeat ran
        // with resurrection still on and handed nothing over — its item is the
        // re-derivation's, never its own defeat's. The boss hands its own over
        // before BossDefeated is raised; and every kill after it drops at once.
        var party = Party(Sledge(), Reaper());
        var (visit, _, turns) = AtTheGolem(party);
        var floor = visit.Current;
        var golem = floor.Boss!;
        var events = new List<string>();
        turns.EnemyDefeated += e => events.Add($"defeated {e.SpawnIndex}");
        turns.EnemyDropped += (e, _) => events.Add($"dropped {e.SpawnIndex}");
        turns.BossDefeated += e => events.Add($"boss {e.SpawnIndex}, resurrection {turns.ResurrectionActive}, its item down {turns.GroundItemOf(e) is not null}, recorded {visit.BossDefeated}");

        var earlier = Row(floor, 21, "reaver");
        Kill(turns, floor, party[0], earlier);
        Assert.Null(turns.GroundItemOf(earlier));

        // One swing from between the hatchet and the golem: the hatchet first, and
        // the cleave carries it on into the golem.
        var hatchet = Row(floor, 22, "hatchet");
        Assert.Equal((57, 43), TileOf(hatchet));
        Put(party[1], (58, 42));
        Assert.True(EnemyAi.CanHit(party[1], hatchet, party[1].EquippedWeapon!, floor.Map.Grid));
        Assert.True(EnemyAi.CanHit(party[1], golem, party[1].EquippedWeapon!, floor.Map.Grid));
        Assert.True(turns.TryAttack(party[1], hatchet));
        Assert.False(hatchet.Alive);
        Assert.False(golem.Alive);

        Assert.Equal(new[]
        {
            "defeated 21",
            "defeated 22",
            "defeated 32",
            "dropped 32",
            "boss 32, resurrection False, its item down True, recorded True",
        }, events);

        // Everything down is lying on what it carried now — the floor's own table's
        // answer off each row, re-derived and never handed over.
        foreach (var corpse in new[] { earlier, hatchet, golem })
        {
            var item = turns.GroundItemOf(corpse);
            Assert.NotNull(item);
            Assert.Equal(Describe(LootTable.Roll(corpse, floor.Seed)), Describe(item));
        }

        // A kill after it is permanent at once.
        var later = Row(floor, 19, "pinning_lance");
        var drops = new List<(EnemyState, Drop)>();
        turns.EnemyDropped += (e, d) => drops.Add((e, d));
        Kill(turns, floor, party[0], later);
        var (corpseLater, dropLater) = Assert.Single(drops);
        Assert.Same(later, corpseLater);
        Assert.Equal(Describe(LootTable.Roll(later, floor.Seed)), Describe(dropLater));

        // And all of it stays down.
        Home(visit, party);
        PassTurns(turns, 15);
        Assert.All(new[] { earlier, hatchet, golem, later }, e => Assert.False(e.Alive));
    }

    [Fact]
    public void AReturnDueBeforeTheBossDiedStillHappens_OneDueAfterDoesNot()
    {
        // A floor nobody is on is frozen but for its resurrection timers, and the
        // boss's death stops the clock they run on: on the way back up, a dummy
        // whose return fell due before the boss fell is standing, and one due after
        // never gets up. Down at 0 (due 8) and at 3 (due 11) on the first floor;
        // the golem falls at 10 on the second; the party climbs back at 13.
        var party = Party(Sledge());
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.Current;
        var turns = visit.OpenTurns(party, Ten);

        var dueBefore = Row(first, 23, "hatchet");
        Kill(turns, first, party[0], dueBefore);
        Home(visit, party);
        PassTurns(turns, 3);
        var dueAfter = Row(first, 9, "hatchet");
        Kill(turns, first, party[0], dueAfter);
        Assert.Equal((8, 11),
            (dueBefore.DefeatedAtTurn + FarmLadder.ResurrectTurns(dueBefore.DefeatCount),
             dueAfter.DefeatedAtTurn + FarmLadder.ResurrectTurns(dueAfter.DefeatCount)));

        var bottom = Down(visit, party);
        PassTurns(bottom, 7);
        var floor = visit.Current;
        Kill(bottom, floor, party[0], floor.Boss!);
        Assert.Equal(10, visit.BossDefeatedAtTurn);
        Home(visit, party);
        PassTurns(bottom, 3);

        var back = Up(visit, party);
        Assert.Equal(13, back.TurnCount);
        Assert.True(dueBefore.Alive);
        Assert.False(dueAfter.Alive);
        Assert.Null(back.GroundItemOf(dueBefore));
        Assert.NotNull(back.GroundItemOf(dueAfter));

        Home(visit, party);
        PassTurns(back, 5);
        Assert.True(dueBefore.Alive);
        Assert.False(dueAfter.Alive);
    }

    // ── And the run goes on ──────────────────────────────────────────────────

    [Fact]
    public void TheBossDoesNotEndTheRun()
    {
        // PHASE4-098: killing the boss turns the party round; the run ends only
        // when the party leaves by the top, or wipes. The turn goes on, the next
        // turn comes, the visit stands in the campaign, and the way out is back up
        // through the first floor.
        var party = Party(Sledge());
        var campaign = new CampaignState(party);
        var visit = campaign.Enter(Tutorial.Id, GoldenSeed);
        visit.OpenTurns(party, Ten);
        var bottom = Down(visit, party);
        int gameOvers = 0;
        bottom.GameOver += () => gameOvers++;

        var floor = visit.Current;
        Kill(bottom, floor, party[0], floor.Boss!);
        Assert.Equal(TurnPhase.Player, bottom.Phase);
        Assert.Equal(0, gameOvers);
        Assert.Same(visit, campaign.Visit);
        Assert.All(party, m => Assert.True(m.Alive));

        bottom.EndTurn();
        Advance(bottom);
        Assert.Equal((TurnPhase.Player, 1), (bottom.Phase, bottom.TurnCount));

        Assert.Null(floor.DownStairs);
        Up(visit, party);
        Gather(visit, party, visit.Current.UpStairs);
        Assert.Null(visit.CanUse(new StairUse(Stair.Exit, visit.Current.UpStairs), party, party[0]));
        Assert.Same(visit, campaign.Visit);
        Assert.Equal(0, gameOvers);
    }

    [Fact]
    public void TheGauntletThinsInNumberNotInQuality()
    {
        // PHASE4-105: after the boss nothing revives, so the way out thins in
        // number — but not in quality. The dummy farmed on the way down is standing
        // when the party climbs back, as strong as the farm made it; put down, it
        // stays down, and the floor's standing count only ever falls.
        var party = Party(Sledge());
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var first = visit.Current;
        var turns = visit.OpenTurns(party, Ten);
        var farmed = Row(first, 23, "hatchet");
        Kill(turns, first, party[0], farmed);   // a clean kill: two cycles owing, back at 8
        Home(visit, party);
        PassTurns(turns, 8);
        Assert.True(farmed.Alive);
        Assert.True(farmed.RevivalDamage + farmed.RevivalMaxHp > 0);
        var statline = (farmed.DefeatCount, farmed.RevivalsRolled, farmed.RevivalDamage, farmed.RevivalMaxHp, farmed.MaxHp, farmed.BonusDamage);
        int standing = first.Enemies.Count(e => e.Alive);

        var bottom = Down(visit, party);
        var floor = visit.Current;
        Kill(bottom, floor, party[0], floor.Boss!);
        Assert.True(visit.BossDefeated);

        var back = Up(visit, party);
        Assert.True(farmed.Alive);
        Assert.Equal(statline, (farmed.DefeatCount, farmed.RevivalsRolled, farmed.RevivalDamage, farmed.RevivalMaxHp, farmed.MaxHp, farmed.BonusDamage));
        Assert.Equal(standing, first.Enemies.Count(e => e.Alive));

        Kill(back, first, party[0], farmed);
        Home(visit, party);
        var counts = new List<int>();
        for (int turn = 0; turn < 12; turn++)
        {
            PassTurns(back, 1);
            counts.Add(first.Enemies.Count(e => e.Alive));
        }
        Assert.All(counts, n => Assert.Equal(standing - 1, n));
        Assert.False(farmed.Alive);
        Assert.Equal(statline.RevivalsRolled, farmed.RevivalsRolled);   // no cycle is ever rolled again
    }

    [Fact]
    public void TheBossNeverRevives_AndIsSlowAndTough()
    {
        // The golem is tough — 80 HP, full at spawn, behind its Block — and slow:
        // a budget of 40 where a dummy has 100. Only its budget reads that number;
        // the attack-cost scale stays the standard enemy's, so 40 buys fewer steps
        // and fewer swings alike. And it never revives: its death is what stops
        // resurrection, its own included, and its DefeatCount advances once.
        var golem = DungeonState.BossRow(Tutorial, TestWeapons.Get("hatchet"));
        Assert.Equal((80, 80, 40f), (golem.MaxHp, golem.Hp, golem.MoveBudget));
        var dummy = new EnemyState { Weapon = TestWeapons.Get("hatchet") };
        Assert.Equal((GameConstants.DummyHp, GameConstants.EnemyMove), (dummy.MaxHp, dummy.MoveBudget));

        // Fewer steps: seen, chasing a member twelve tiles off along an open row.
        float Walked(EnemyState enemy)
        {
            var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
            Put(member, (5, 2));
            Put(enemy, (5, 14));
            var turns = new TurnSystem(new int[12, 20], new[] { member }, new[] { enemy }, Ten);
            turns.NotifyEnemyVisible(enemy, true);
            float from = enemy.X;
            turns.EndTurn();
            Advance(turns);
            Assert.Equal(TurnPhase.Player, turns.Phase);
            return from - enemy.X;
        }
        float golemWalked = Walked(DungeonState.BossRow(Tutorial, TestWeapons.Get("hatchet")));
        float dummyWalked = Walked(new EnemyState { Weapon = TestWeapons.Get("hatchet") });
        Assert.InRange(golemWalked, 39.5f, 40.5f);
        Assert.InRange(dummyWalked, 99.5f, 100.5f);

        // Fewer swings: seen, a member in reach. A hatchet's 54 costs a standard
        // enemy 33.75 of its budget: two swings out of 100, one out of 40.
        int Swings(EnemyState enemy)
        {
            var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
            Put(member, (5, 5));
            Put(enemy, (5, 6));
            var turns = new TurnSystem(new int[12, 20], new[] { member }, new[] { enemy }, Ten);
            int hits = 0;
            turns.CharacterHit += (_, _) => hits++;
            turns.NotifyEnemyVisible(enemy, true);
            turns.EndTurn();
            Advance(turns);
            Assert.Equal(TurnPhase.Player, turns.Phase);
            return hits;
        }
        Assert.Equal(54, dummy.MovementCost(dummy.Weapon));
        Assert.Equal(1, Swings(DungeonState.BossRow(Tutorial, TestWeapons.Get("hatchet"))));
        Assert.Equal(2, Swings(new EnemyState { Weapon = TestWeapons.Get("hatchet") }));

        // Tough, and it never gets up: two 60s into Block 9 — 51 and 51 — put it
        // down, not cleanly, so its count advances once; twenty turns later it is
        // still down and nothing has been rolled for it.
        var killer = TestPools.Holding("A", TestWeapons.Make("Maul", range: 60, damage: 60, cost: 30));
        Put(killer, (5, 5));
        Put(golem, (5, 6));
        var system = new TurnSystem(new int[12, 20], new[] { killer }, new[] { golem }, Ten, boss: golem);
        Assert.True(system.TryAttack(killer, golem));
        Assert.Equal(29, golem.Hp);
        Assert.True(system.TryAttack(killer, golem));
        Assert.False(golem.Alive);
        Assert.Equal((1, 0), (golem.DefeatCount, golem.RevivalsRolled));
        Assert.False(system.ResurrectionActive);
        for (int turn = 0; turn < 20; turn++)
        {
            system.EndTurn();
            Advance(system);
        }
        Assert.False(golem.Alive);
        Assert.Equal((1, 0, 0), (golem.DefeatCount, golem.RevivalsRolled, golem.RevivalMaxHp));

        // And a boss is one of its floor's enemies, or the system refuses it.
        Assert.Throws<ArgumentException>(() => new TurnSystem(new int[12, 20], new[] { killer }, Array.Empty<EnemyState>(), Ten, boss: golem));
    }
}
