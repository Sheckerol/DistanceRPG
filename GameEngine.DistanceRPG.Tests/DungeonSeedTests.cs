using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.2 and §4.3's seed arithmetic: the per-floor seed, the three new salts and
/// the streams they keep apart, and the boss floor's roll — the doc's formulas
/// verbatim, floor 0 resolving to the map seed exactly so the golden dungeon is
/// untouched, and the tutorial's pinned depth. In the content collection because
/// one test swaps the tuning the roll reads its range from.
/// </summary>
[Collection(TestContent.Collection)]
public class DungeonSeedTests
{
    /// <summary>The shipped seed, which does not fit in an int.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>
    /// A thousand map seeds: the golden one, the edges of the 32-bit range, and a
    /// multiplicative spread across the rest, most of them past <see cref="int.MaxValue"/>
    /// — the game rolls a map seed as any unsigned 32-bit value.
    /// </summary>
    private static IEnumerable<long> Seeds()
    {
        yield return GoldenSeed;
        yield return 0;
        yield return 1;
        yield return int.MaxValue;
        yield return int.MaxValue + 1L;
        yield return uint.MaxValue;
        for (uint i = 1; i <= 994; i++)
            yield return i * 2654435761u;   // Knuth's multiplier: wraps into a spread over the whole range
    }

    private static DungeonDef Dungeon(string id) => GameContent.Current.Dungeons[id];

    /// <summary>
    /// The first two outputs of a stream as one 64-bit number. Equal seeds give
    /// equal fingerprints, so distinct fingerprints prove distinct streams — which
    /// is how a finalized stream, whose seed is private to it, is told apart.
    /// </summary>
    private static ulong Fingerprint(Mulberry32 rng)
    {
        // NextDouble is t / 2^32 for an unsigned t, so this recovers t exactly.
        ulong first = (uint)(rng.NextDouble() * 4294967296.0);
        ulong second = (uint)(rng.NextDouble() * 4294967296.0);
        return (first << 32) | second;
    }

    [Fact]
    public void FloorSeedIsTheDocsFormula_AndFloorZeroIsTheMapSeedExactly()
    {
        // PHASE4-015: floorSeed(n) = mapSeed ^ (FloorSalt * n), verbatim, with the
        // value the plan fixes for the salt the doc leaves open.
        Assert.Equal(0x85EBCA6B, DungeonState.FloorSalt);

        foreach (long seed in Seeds())
        {
            // PHASE4-017: n = 0 resolves to exactly today's seed, so floor 0 is the
            // dungeon the game has always generated, on the stream it always drew.
            Assert.Equal(seed, DungeonState.FloorSeed(seed, 0));
            var today = new Mulberry32(seed);
            var floorZero = new Mulberry32(DungeonState.FloorSeed(seed, 0));
            Assert.Equal(today.NextDouble(), floorZero.NextDouble());

            for (int n = 1; n < 10; n++)
                Assert.Equal(seed ^ (DungeonState.FloorSalt * n), DungeonState.FloorSeed(seed, n));
        }

        // A multiplier past 2^32 is harmless: Mulberry32 keeps the low 32 bits,
        // so the stream is the one the low half seeds, deterministically.
        long deep = DungeonState.FloorSeed(GoldenSeed, 9);
        Assert.True(DungeonState.FloorSalt * 9 > uint.MaxValue);
        Assert.Equal(new Mulberry32((uint)deep).NextDouble(), new Mulberry32(deep).NextDouble());
    }

    [Fact]
    public void TheSaltsAreTheirOwn_AndNoFloorAliasesAnotherStream()
    {
        // PHASE4-012, -016, -021: three new salts, each an odd 32-bit value and
        // none of them one already in the tree. FloorSalt being odd is what makes
        // n -> FloorSalt * n one-to-one modulo 2^32: no two floors of a dungeon
        // ever share a map stream, whatever the depth.
        long[] added = [DungeonState.FloorSalt, StairPlacer.StairSalt, DungeonState.BossSalt];
        long[] existing = [EnemyPlacer.SeedSalt, LootTable.LootSalt, FarmLadder.ReviveSalt];
        Assert.Equal((0x85EBCA6BL, 0xC2B2AE35L, 0x165667B1L), (added[0], added[1], added[2]));
        Assert.All(added, salt =>
        {
            Assert.InRange(salt, 1L, uint.MaxValue);
            Assert.Equal(1, salt & 1);
        });
        Assert.Equal(6, added.Concat(existing).Distinct().Count());

        foreach (long seed in new[] { GoldenSeed, 0L, uint.MaxValue })
        {
            // The raw families — a stream seeded with the value itself, no
            // finalizer: every floor's map, every floor's placer, every floor's
            // stairs, and the visit's boss. Two raw streams are one stream exactly
            // when their seeds agree in the low 32 bits, and every seed here is
            // the map seed XORed with a constant, so one map seed clean is every
            // map seed clean. Floors 0-31, the whole domain checked.
            var raw = new Dictionary<uint, string>();
            void Raw(long streamSeed, string name)
            {
                if (!raw.TryAdd((uint)streamSeed, name))
                    Assert.Fail($"{name} is the same stream as {raw[(uint)streamSeed]}");
            }
            for (int n = 0; n < 32; n++)
            {
                Raw(DungeonState.FloorSeed(seed, n), $"map {n}");
                Raw(DungeonState.FloorSeed(seed, n) ^ EnemyPlacer.SeedSalt, $"placer {n}");
                Raw(StairPlacer.SeedFor(seed, n), $"stairs {n}");
            }
            Raw(seed ^ DungeonState.BossSalt, "boss");
            Assert.Equal(97, raw.Count);
        }

        // The finalized families — Phase 3's drop and revival streams, which run
        // their composed seed through SplitMix32 and take this floor's seed as
        // their map seed. SplitMix32 is a bijection, so the check is again
        // map-seed-independent; the streams are told apart by fingerprint. The
        // domain verified is every floor a boss can sit on (index 0 to
        // BossFloorMax - 1), spawn index to 64 and cycle to 64 for the revivals,
        // and spawn index to 128 for the drops.
        //
        // It is not clean further out, and that is accepted (plan 1b): the mixes
        // are XOR-linear and floorSeed is the doc's raw formula, so terms can
        // cancel. Grow the spawn and cycle bounds together and the first
        // coincidence arrives at 78; it is pinned below, so widening the bound
        // that far makes this test fail, as it should.
        // BossLoot's drop stream joins these families when it lands (Phase 4.6).
        int floors = GameContent.Current.Tuning.BossFloorMax;
        Assert.Equal(10, floors);
        foreach (long seed in new[] { GoldenSeed, 0L })
        {
            var finalized = new Dictionary<ulong, string>();
            void Finalized(Mulberry32 stream, string name)
            {
                ulong print = Fingerprint(stream);
                if (!finalized.TryAdd(print, name))
                    Assert.Fail($"{name} is the same stream as {finalized[print]}");
            }
            for (int n = 0; n < floors; n++)
            {
                long floorSeed = DungeonState.FloorSeed(seed, n);
                for (int spawn = 0; spawn <= 128; spawn++)
                    Finalized(LootTable.StreamFor(floorSeed, spawn), $"drop: floor {n}, spawn {spawn}");
                for (int spawn = 0; spawn <= 64; spawn++)
                    for (int cycle = 0; cycle <= 64; cycle++)
                        Finalized(FarmLadder.ReviveStream(floorSeed, spawn, cycle), $"revival: floor {n}, spawn {spawn}, cycle {cycle}");
            }
            Assert.Equal(floors * (129 + 65 * 65), finalized.Count);

            // Just outside, at 78: floor 3 / spawn 69 / cycle 78 is floor 9 /
            // spawn 12 / cycle 69. Two dummies on different floors sharing one
            // revival roll — nothing crashes and nothing is exploitable.
            Assert.Equal(
                Fingerprint(FarmLadder.ReviveStream(DungeonState.FloorSeed(seed, 3), 69, 78)),
                Fingerprint(FarmLadder.ReviveStream(DungeonState.FloorSeed(seed, 9), 12, 69)));
        }
    }

    [Fact]
    public void BossFloorIsTheDocsFormulaVerbatim()
    {
        // PHASE4-020: bossFloor = 5 + new Mulberry32(mapSeed ^ BossSalt).NextInt(0, 5),
        // with the plan's value for the salt — written out here as a literal on
        // purpose, so this pins the formula rather than restating the code.
        Assert.Equal(0x165667B1, DungeonState.BossSalt);

        foreach (var def in GameContent.Current.Dungeons.All.Where(d => !d.RetiresOnClear))
            foreach (long seed in Seeds())
                Assert.Equal(5 + new Mulberry32(seed ^ 0x165667B1).NextInt(0, 5), DungeonState.RollBossFloor(seed, def));
    }

    [Fact]
    public void BossFloorsCoverFiveToTenAndNothingElse()
    {
        // PHASE4-020: every depth from 5 to 10 comes up, nothing outside it ever
        // does, and each is near a sixth — over six thousand consecutive seeds,
        // where a raw first draw would show banding between neighbours if it had any.
        var hall = Dungeon("spear_hall");
        var counts = new Dictionary<int, int>();
        const int Rolls = 6000;
        for (long seed = 0; seed < Rolls; seed++)
        {
            int floor = DungeonState.RollBossFloor(seed, hall);
            counts[floor] = counts.GetValueOrDefault(floor) + 1;
        }

        Assert.Equal(new[] { 5, 6, 7, 8, 9, 10 }, counts.Keys.Order());
        foreach (var (floor, count) in counts)
            Assert.True(Math.Abs(count / (double)Rolls - 1 / 6.0) <= 0.05, $"floor {floor}: {count} of {Rolls}");

        // PHASE4-023: the depth is decided at entry and fixed for the visit — a
        // pure function of the map seed, so asking again, after anything else has
        // drawn, is the same answer.
        foreach (long seed in Seeds().Take(10))
        {
            int first = DungeonState.RollBossFloor(seed, hall);
            Floor.Generate(seed, 0, bottom: false, hall);
            Assert.Equal(first, DungeonState.RollBossFloor(seed, hall));
        }
    }

    [Fact]
    public void TheTutorialIsAlwaysTwoFloorsDeep()
    {
        // PHASE4-077: the tutorial ignores the 5-10 roll. Its boss sits on floor
        // 2, whatever the seed.
        var tutorial = GameContent.Current.Dungeons.Tutorial;
        Assert.All(Seeds(), seed => Assert.Equal(2, DungeonState.RollBossFloor(seed, tutorial)));

        // And the draw is still taken: a pinned range draws NextInt(0, 0) all the
        // same, so a later draw on the boss stream sits at the same position for
        // the tutorial as for every other dungeon, and never depends on the range.
        var hall = Dungeon("spear_hall");
        foreach (long seed in Seeds().Take(100))
        {
            var reference = new Mulberry32(seed ^ DungeonState.BossSalt);
            reference.NextDouble();
            double second = reference.NextDouble();

            var pinned = DungeonState.BossStream(seed);
            Assert.Equal(2, DungeonState.RollBossFloor(pinned, tutorial));
            Assert.Equal(second, pinned.NextDouble());

            var rolled = DungeonState.BossStream(seed);
            Assert.Equal(DungeonState.RollBossFloor(seed, hall), DungeonState.RollBossFloor(rolled, hall));
            Assert.Equal(second, rolled.NextDouble());
        }
    }

    [Fact]
    public void TheRangeIsTuning_AndAPinnedDungeonIgnoresIt()
    {
        // The 5-10 is two tuning keys, read through the content that is current:
        // swap them and every unpinned dungeon rolls over the new range, by the
        // same one draw.
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { BossFloorMin = 3, BossFloorMax = 4 });
        var hall = Dungeon("spear_hall");
        var tutorial = GameContent.Current.Dungeons.Tutorial;

        var seen = new HashSet<int>();
        foreach (long seed in Seeds())
        {
            int floor = DungeonState.RollBossFloor(seed, hall);
            Assert.Equal(3 + new Mulberry32(seed ^ DungeonState.BossSalt).NextInt(0, 1), floor);
            seen.Add(floor);

            // The tutorial pins its own range, so the tuning never reaches it.
            Assert.Equal(2, DungeonState.RollBossFloor(seed, tutorial));
        }
        Assert.Equal(new[] { 3, 4 }, seen.Order());
    }
}
