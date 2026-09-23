using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The drop as a stream (§3.5): seeded per enemy, replayable from saved state
/// alone, and nobody else's. These are the assertions the per-enemy seeding is
/// bought for — a map-wide stream would pass every test in
/// <see cref="LootTableTests"/> and fail every one of these, because what it
/// would break is the property that collection order is the player's and the
/// drop is not.
/// </summary>
public class LootDeterminismTests
{
    /// <summary>The prototype's own map seed; it overflows int32, which is the interesting case for a stream mix.</summary>
    private const long Seed = 2762136374;

    private static EnemyState Dummy(string weaponId, int defeatCount = 0, int spawnIndex = 0)
        => new() { Weapon = TestWeapons.Get(weaponId), DefeatCount = defeatCount, SpawnIndex = spawnIndex };

    /// <summary>Everything about a drop a seed is supposed to fix: the item, its live spread, its entries and the unique outcome.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] " +
           string.Join(", ", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}+{e.Xp}")) +
           $" unique={drop.WasUniqueRoll} count={drop.DefeatCount}";

    /// <summary>Ten dummies of mixed classes and mixed farm depth: a floor on the way out.</summary>
    private static List<EnemyState> Floor() =>
    [
        Dummy("arming_sword", 0, 0),
        Dummy("assassins_fang", 3, 1),
        Dummy("great_axe", 12, 2),
        Dummy("phalanx_spear", 1, 3),
        Dummy("bandolier", 20, 4),
        Dummy("tower_guard", 7, 5),
        Dummy("staff_of_blight", 40, 6),
        Dummy("longbow", 2, 7),
        Dummy("disarming_kris", 30, 8),
        Dummy("hatchet", 5, 9),
    ];

    [Fact]
    public void ASeedReproducesTheWholeDropSequence()
    {
        // The test that makes the per-enemy stream load-bearing: a dungeon's
        // drop table is computed rather than authored, so the same floor at the
        // same seed yields the same ten weapons — and the order the party walks
        // the corpses in is not an input. On one map-wide stream the second pass
        // below would hand every dummy its neighbour's weapon.
        var forwards = Floor().Select(e => Describe(LootTable.Roll(e, Seed))).ToList();

        var backwards = new List<string>();
        var floor = Floor();
        for (int i = floor.Count - 1; i >= 0; i--)
            backwards.Insert(0, Describe(LootTable.Roll(floor[i], Seed)));

        Assert.Equal(forwards, backwards);

        // Collecting one dummy twice, or skipping one entirely, changes nothing
        // about the rest either.
        var shuffled = Floor();
        var scattered = new[] { 4, 4, 9, 0, 7, 2, 2, 1, 8, 3, 5, 6 }
            .Select(i => (Index: i, Text: Describe(LootTable.Roll(shuffled[i], Seed))))
            .ToList();
        Assert.All(scattered, x => Assert.Equal(forwards[x.Index], x.Text));

        // And a different floor is a different table.
        Assert.NotEqual(forwards, Floor().Select(e => Describe(LootTable.Roll(e, Seed + 1))).ToList());
    }

    [Fact]
    public void ADropIsReplayableFromSavedStateAlone()
    {
        // Roll reads four things: the weapon's class, the defeat count, the spawn
        // index and the map seed. §5.1 stores all four, so a save needs no drop
        // table of its own — which is the Phase 5 seam, asserted here rather than
        // discovered there.
        var farmed = Dummy("assassins_fang", defeatCount: 18, spawnIndex: 5);
        farmed.Hp = 3;
        farmed.DefeatedAtTurn = 41;
        farmed.RevivalDamage = 27;
        farmed.RevivalMaxHp = 14;
        farmed.RevivalsRolled = 18;

        // The save's row and nothing else: a fresh dummy carrying the same four
        // numbers, none of the rest of the history, and a weapon instance of its
        // own rather than the farmed one.
        var loaded = Dummy("assassins_fang", defeatCount: 18, spawnIndex: 5);

        Assert.Equal(Describe(LootTable.Roll(farmed, Seed)), Describe(LootTable.Roll(loaded, Seed)));

        // And all four are load-bearing: moving any one of them moves the floor's
        // table, which is what makes them exactly the state that has to be saved.
        // Swept over the spawn index rather than asserted on one pair, because a
        // pair can coincide — two streams that both win the unique roll hand over
        // the same artifact, the one outcome the farm's own history is discarded
        // for.
        List<string> Table(string weaponId, int defeatCount, long mapSeed) => Enumerable.Range(0, 16)
            .Select(spawn => Describe(LootTable.Roll(Dummy(weaponId, defeatCount, spawn), mapSeed)))
            .ToList();

        var table = Table("assassins_fang", 18, Seed);
        Assert.True(table.Distinct().Count() > 8, "the spawn index is what makes a floor's drops differ from each other");
        Assert.NotEqual(table, Table("assassins_fang", 19, Seed));
        Assert.NotEqual(table, Table("assassins_fang", 18, Seed + 1));
        Assert.NotEqual(table, Table("tower_guard", 18, Seed));
    }

    [Fact]
    public void ADropReDerivesIdenticallyAfterAReload()
    {
        // A dummy killed permanently and not yet picked up leaves a weapon on the
        // floor. Nothing stores it: the enemy row already fixes it, so a floor
        // re-entry re-derives the identical item — same variant, same stacks,
        // same tiers, same unique outcome — rather than either re-rolling it (a
        // different weapon) or dropping it (a lost one).
        //
        // The ground item, the DropTaken flag that retires it at pickup and the
        // scene that re-derives on entry are all collection's (§3.5's next
        // sub-step); what has to be true for any of them to work is this.
        foreach (var dead in Floor())
        {
            var first = LootTable.Roll(dead, Seed);
            dead.Alive = false;

            var reloaded = Dummy(dead.Weapon.Id, dead.DefeatCount, dead.SpawnIndex);
            reloaded.Alive = false;

            Assert.Equal(Describe(first), Describe(LootTable.Roll(reloaded, Seed)));
        }
    }

    [Fact]
    public void TheDropDoesNotConsumeThePlacementStream()
    {
        // New randomness gets its own stream, derived as mapSeed ^ a new salt,
        // never a continuation of an existing one. Re-calling PlaceEnemies around
        // a drop could not show this — it is a pure function of (map, seed) and
        // would reproduce whatever the drop drew on — so the assertion with power
        // is that a hundred drops are exactly what StreamFor predicts and are not
        // what a running placer-salted stream would give.
        Assert.NotEqual(EnemyPlacer.SeedSalt, LootTable.LootSalt);
        Assert.NotEqual(FarmLadder.ReviveSalt, LootTable.LootSalt);

        var catalogue = GameContent.Current.Weapons;
        var predicted = new List<string>();
        var running = new List<string>();
        var placerStream = new Mulberry32(Seed ^ EnemyPlacer.SeedSalt);

        for (int spawn = 0; spawn < 100; spawn++)
        {
            var drop = LootTable.Roll(Dummy("arming_sword", 0, spawn), Seed);
            predicted.Add(drop.Weapon.Id);

            // The variant is the first draw, so the stream it came off is
            // identifiable from the outside.
            Assert.Equal(
                catalogue.Variant(WeaponClass.Sword, (VariantRole)LootTable.StreamFor(Seed, spawn).NextInt(0, 3)).Id,
                drop.Weapon.Id);

            running.Add(catalogue.Variant(WeaponClass.Sword, (VariantRole)placerStream.NextInt(0, 3)).Id);
        }

        Assert.NotEqual(predicted, running);
    }

    [Fact]
    public void NeighbouringSpawnIndicesAreNotBanded()
    {
        // Each dummy's drop is a *first* draw off a freshly seeded stream, and
        // mulberry32's first output is a weak function of its state while the
        // seeds differ by a thin linear term in the index. The splitmix32
        // finalizer is what stops one floor's corpses running in blocks. Swept
        // over the index at one fixed seed, deliberately: sweeping seeds would
        // average the banding away, since the banding is exactly a within-floor
        // correlation.
        var variants = Enumerable.Range(0, 64)
            .Select(spawn => LootTable.Roll(Dummy("arming_sword", 0, spawn), Seed).Weapon.Id)
            .ToList();

        Assert.Equal(4, variants.Distinct().Count());
        foreach (var id in variants.Distinct())
            Assert.InRange(variants.Count(v => v == id), 5, 32);

        int longest = 1, run = 1;
        for (int i = 1; i < variants.Count; i++)
        {
            run = variants[i] == variants[i - 1] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }
        Assert.InRange(longest, 1, 5);
    }
}
