using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// 4.3's other half: every drop in a dungeon that is not the boss's rolls for
/// the theme. The theme joins the farm's roll pool as one more entry - last,
/// after everything Phase 3 listed - so an ordinary drop may grow it, acquired
/// and shallower than the boss's forged graft, and a weapon the relations will
/// not let hold it simply grows something else. Which table a corpse rolls on is
/// the roster's to say, never the row's.
/// <para>
/// The drops are worked in the numbers they were written against — the forge
/// headroom, the farm's odds (<see cref="Premise"/>) — made current for each test,
/// and the tutorial's floors are generated under it, so the content collection
/// holds it.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public sealed class ThemedDropTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The prototype's own map seed, and the shipped tutorial's.</summary>
    private const long Seed = 2762136374;

    private static WeaponCatalogue Weapons => GameContent.Current.Weapons;

    private static ModifierRules Rules => GameContent.Current.Modifiers;

    private static DungeonDef Tutorial => GameContent.Current.Dungeons.Tutorial;

    private static EnemyState Dummy(string weaponId, int defeatCount = 0, int spawnIndex = 0, ModifierType? theme = null, DamageType? element = null)
        => new() { Weapon = TestWeapons.Get(weaponId, element), DefeatCount = defeatCount, SpawnIndex = spawnIndex, Theme = theme };

    private static PartyMemberState[] Party()
        => new[] { "A", "B", "C", "D" }.Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i)).ToArray();

    /// <summary>A weapon row's forged spread as a set.</summary>
    private static ModifierSet Spread(WeaponDef def) => ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());

    /// <summary>LootTableTests' own description of a drop, so the pinned table below reads in its words.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] " +
           string.Join(", ", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}+{e.Xp}")) +
           $" unique={drop.WasUniqueRoll} count={drop.DefeatCount}";

    private static List<ModifierType?> EligibleFor(Weapon weapon, ModifierType? theme, int bankedTiers = 0)
    {
        var list = new List<ModifierType?>();
        LootTable.Eligible(weapon, bankedTiers, theme, list);
        return list;
    }

    // ── Weighted, never handed over ──────────────────────────────────────────

    [Fact]
    public void AnOrdinaryDropMayGrowTheTheme_Acquired()
    {
        // PHASE4-035, -040, -041: every other drop in the dungeon rolls for the
        // theme - it joins the graft pool, weighting it - and when it does roll
        // it, it arrives acquired: never forged, never past the acquired headroom's
        // five. What drops is untouched: the class, the variant and the entry it
        // arrives with are drawn before the theme is read at all.
        foreach (var weaponId in new[] { "hatchet", "assassins_fang", "phalanx_spear", "bandolier", "longbow" })
        {
            int grew = 0;
            for (int spawn = 0; spawn < 200; spawn++)
            {
                var themed = LootTable.Roll(Dummy(weaponId, 20, spawn, Block), Seed);
                var plain = LootTable.Roll(Dummy(weaponId, 20, spawn), Seed);

                Assert.Equal(0, themed.Weapon.Forged.Stacks(Block));
                Assert.InRange(themed.Weapon.Stacks(Block), 0, Rules.Cap(Block, 0));
                Assert.Equal(5, Rules.Cap(Block, 0));
                if (themed.Weapon.Stacks(Block) > 0) grew++;

                Assert.Equal(plain.Weapon.Class, themed.Weapon.Class);
                if (!themed.WasUniqueRoll && !plain.WasUniqueRoll)
                {
                    Assert.Equal(plain.Weapon.Id, themed.Weapon.Id);
                    Assert.Equal(plain.Weapon.Forged, themed.Weapon.Forged);
                    Assert.Equal(plain.Weapon.Enchantments.Select(e => e.Id), themed.Weapon.Enchantments.Select(e => e.Id));
                }

                // Nothing farmed, nothing grown: the drop is the one it always was.
                Assert.Equal(Describe(LootTable.Roll(Dummy(weaponId, 0, spawn), Seed)), Describe(LootTable.Roll(Dummy(weaponId, 0, spawn, Block), Seed)));
            }
            Assert.True(grew > 0, $"no {weaponId} drop in 200 ever grew Block");
        }
    }

    [Fact]
    public void AnAxeInABraceDungeonNeverGrowsBrace()
    {
        // PHASE4-054, the ordinary half: no table bends for an ordinary drop. The
        // theme is only in its roll pool, and Allowed already skips a modifier a
        // weapon cannot hold - an axe's forged Opportunist shares Brace's group, a
        // bow and a throwing weapon are not melee - so the stack lands on
        // something else. A dagger in the same dungeon grows Brace as readily as
        // anything.
        var axe = TestWeapons.Get("great_axe");
        Assert.DoesNotContain(Brace, EligibleFor(axe, Brace));
        Assert.Equal<ModifierType?>(Brace, EligibleFor(TestWeapons.Get("assassins_fang"), Brace)[^1]);

        foreach (var weaponId in new[] { "hatchet", "longbow", "bandolier" })
            for (int spawn = 0; spawn < 300; spawn++)
                Assert.Equal(0, LootTable.Roll(Dummy(weaponId, 40, spawn, Brace), Seed).Weapon.Stacks(Brace));

        int daggers = Enumerable.Range(0, 300).Count(spawn => LootTable.Roll(Dummy("assassins_fang", 40, spawn, Brace), Seed).Weapon.Stacks(Brace) > 0);
        Assert.True(daggers > 0, "no dagger in a Brace dungeon ever grew Brace");
    }

    [Fact]
    public void TheThemeIsTheLastEntryOfTheFarmsRoll()
    {
        // The draw is a uniform index into the eligible list, so the list's order
        // is gameplay: the forged modifiers in ModifierType order, the enchantment
        // entry, then the theme appended after everything Phase 3 listed - which
        // keeps what every existing index means.
        var blight = TestWeapons.Get("staff_of_blight");
        Assert.Equal(new ModifierType?[] { Resonant, null, Block }, EligibleFor(blight, Block));
        Assert.Equal(new ModifierType?[] { Resonant, null }, EligibleFor(blight, null));
        Assert.Equal(new ModifierType?[] { CritWindow, CritMultiplier, CritWeaken, Block }, EligibleFor(TestWeapons.Get("disarming_kris"), Block));
        Assert.Equal(new ModifierType?[] { Block, Push }, EligibleFor(TestWeapons.Get("tower_guard"), Block));   // forged with it: listed once, as its own depth

        // Seed 3 rolls a Staff of Blight whose one farm cycle pays, then draws
        // the last index of the list in front of it: read off the stream here, so
        // this is a claim about the list and not about which way a draw fell.
        var rng = LootTable.StreamFor(3, 0);
        var staves = GameContent.Current.Weapons;
        Assert.Equal("staff_of_blight", staves.Class(Staff).DropOrder![rng.NextInt(0, LootTable.VariantCount(staves, Staff) - 1)]);
        Assert.True(FarmLadder.RollsAStack(rng));
        double d = rng.NextDouble();
        Assert.Equal(2, (int)Math.Floor(d * 3));   // the last of three
        Assert.Equal(1, (int)Math.Floor(d * 2));   // and the last of Phase 3's two

        var themed = LootTable.Roll(Dummy("staff_of_blight", 1, theme: Block), 3);
        Assert.Equal("staff_of_blight", themed.Weapon.Id);
        Assert.Equal((1, 1, 1), (themed.Weapon.Stacks(Block), themed.Weapon.Stacks(Resonant), themed.Weapon.Enchantments[0].Tier));   // landed on the theme, acquired

        var plain = LootTable.Roll(Dummy("staff_of_blight", 1), 3);
        Assert.Equal((0, 1, 2), (plain.Weapon.Stacks(Block), plain.Weapon.Stacks(Resonant), plain.Weapon.Enchantments[0].Tier));     // Phase 3's last: the entry

        // Appending moves where a given draw lands, not what an index means: seed
        // 25's one stack is the entry in a Block dungeon and the Resonant stack
        // outside one. No themed row is expected to match a Phase 3 fixture.
        var themed25 = LootTable.Roll(Dummy("staff_of_blight", 1, theme: Block), 25);
        var plain25 = LootTable.Roll(Dummy("staff_of_blight", 1), 25);
        Assert.Equal((1, 2), (themed25.Weapon.Stacks(Resonant), themed25.Weapon.Enchantments[0].Tier));
        Assert.Equal((2, 1), (plain25.Weapon.Stacks(Resonant), plain25.Weapon.Enchantments[0].Tier));
    }

    /// <summary>
    /// Phase 3's drops, as its code rolled them, for every fixture seed its tests
    /// name — LootTableTests' pinned indices and unique wins, DropCollectionTests'
    /// farms — and every class farmed twenty deep: captured before Phase 4.6
    /// touched the table, and never to be regenerated from it.
    /// </summary>
    public static readonly TheoryData<string, int, int, long, DamageType?, string> PhaseThreeDrops = new()
    {
        { "staff_of_blight", 1, 0, 4, null, "staff_of_renewal [Resonant x2] regeneration@1+0 unique=False count=1" },
        { "staff_of_blight", 1, 0, 3, null, "staff_of_blight [Resonant x1] poison@2+0 unique=False count=1" },
        { "disarming_kris", 1, 0, 63, null, "disarming_kris [CritWindow x2, CritMultiplier x1, CritWeaken x1]  unique=False count=1" },
        { "disarming_kris", 1, 0, 39, null, "disarming_kris [CritWindow x1, CritMultiplier x2, CritWeaken x1]  unique=False count=1" },
        { "disarming_kris", 1, 0, 10, null, "disarming_kris [CritWindow x1, CritMultiplier x1, CritWeaken x2]  unique=False count=1" },
        { "assassins_fang", 40, 0, 11, null, "widowmaker [CritWindow x3, CritMultiplier x1] siphon@1+0 unique=True count=40" },
        { "hatchet", 40, 0, 11, null, "shieldbreaker [Cleave x1, Splitting x3, Opportunist x1] momentum@1+0 unique=True count=40" },
        { "hatchet", 40, 0, 10, null, "reaver [Cleave x6, Splitting x7, Opportunist x6]  unique=False count=40" },
        { "hatchet", 4, 0, 2762136374, null, "great_axe [Cleave x5, Opportunist x2]  unique=False count=4" },
        { "hatchet", 12, 4, 2762136374, null, "great_axe [Cleave x2, Opportunist x5]  unique=False count=12" },
        { "staff_of_blight", 3, 0, 2762136374, null, "staff_of_warding [Resonant x3] ward@1+0 unique=False count=3" },
        { "assassins_fang", 20, 0, 2762136374, null, "assassins_fang [CritWindow x7, CritMultiplier x6]  unique=False count=20" },
        { "assassins_fang", 20, 1, 2762136374, null, "assassins_fang [CritWindow x3, CritMultiplier x3]  unique=False count=20" },
        { "tower_guard", 20, 0, 2762136374, null, "tower_guard [Block x7, Push x6]  unique=False count=20" },
        { "tower_guard", 20, 1, 2762136374, null, "tower_guard [Block x3, Push x3]  unique=False count=20" },
        { "phalanx_spear", 20, 0, 2762136374, null, "phalanx_spear [Brace x7, Longshot x6]  unique=False count=20" },
        { "phalanx_spear", 20, 1, 2762136374, null, "phalanx_spear [Brace x3, Longshot x3]  unique=False count=20" },
        { "great_axe", 20, 0, 2762136374, null, "great_axe [Cleave x7, Opportunist x6]  unique=False count=20" },
        { "great_axe", 20, 1, 2762136374, null, "great_axe [Cleave x3, Opportunist x3]  unique=False count=20" },
        { "longbow", 20, 0, 2762136374, null, "longbow [CritWindow x6, Longshot x7]  unique=False count=20" },
        { "longbow", 20, 1, 2762136374, null, "longbow [CritWindow x2, Longshot x4]  unique=False count=20" },
        { "bandolier", 20, 0, 2762136374, null, "bandolier [CritMultiplier x6, Charges x7]  unique=False count=20" },
        { "bandolier", 20, 1, 2762136374, null, "bandolier [CritMultiplier x2, Charges x4]  unique=False count=20" },
        { "staff_of_blight", 20, 0, 2762136374, null, "staff_of_warding [Resonant x6] ward@6+0 unique=False count=20" },
        { "staff_of_blight", 20, 1, 2762136374, null, "staff_of_warding [Resonant x2] ward@4+0 unique=False count=20" },
        { "wand_of_the_blast", 20, 0, 2762136374, DamageType.Cold, "wand_of_the_cone [Resonant x6] flaming@6+0 unique=False count=20" },
        { "wand_of_the_blast", 20, 1, 2762136374, DamageType.Cold, "wand_of_the_cone [Resonant x2] cold@3+0 unique=False count=20" },
    };

    [Theory]
    [MemberData(nameof(PhaseThreeDrops))]
    public void AnUnthemedDropIsTheOneItAlwaysWas(string weaponId, int defeatCount, int spawnIndex, long seed, DamageType? element, string phaseThree)
    {
        // A row no dungeon themed builds Phase 3's eligible list exactly, entry
        // for entry, so it rolls bit-identically: no Phase 3 fixture moves. (A
        // themed row is a deliberate new distribution; nothing here expects one
        // to match these.)
        Assert.Equal(phaseThree, Describe(LootTable.Roll(Dummy(weaponId, defeatCount, spawnIndex, theme: null, element), seed)));
    }

    // ── The roster decides ───────────────────────────────────────────────────

    [Fact]
    public void OnlyTheBossRowReachesBossLoot()
    {
        // Plan 1f: nothing asks a corpse its kind when it is collected. The
        // roster fixed a drop feed for every row - LootTable.Roll for each dummy,
        // BossLoot.Roll for the one it named as the boss - and GroundItemOf calls
        // it. A dummy handed a promise by hand still rolls the ordinary drop, and
        // the boss's own row, on a system that does not name it the boss, rolls
        // an ordinary one too: the feed decides, not the field.
        var floor = Floor.Generate(Seed, 1, bottom: true, Tutorial);
        var boss = floor.Boss!;
        var dummies = floor.Enemies.Where(e => e != boss).ToArray();
        Assert.All(dummies, e => Assert.Null(e.PromisedStacks));
        Assert.Equal<int?>(0, boss.PromisedStacks);

        var promisedDummy = dummies[0];
        promisedDummy.PromisedStacks = 6;
        foreach (var e in floor.Enemies)
            e.Alive = false;

        var turns = new TurnSystem(floor.Map.Grid, Party(), floor.Enemies, () => 10, floor.Seed, boss: boss) { ResurrectionActive = false };
        foreach (var dummy in dummies)
            Assert.Equal(Describe(LootTable.Roll(dummy, floor.Seed)), Describe(turns.GroundItemOf(dummy)!));
        Assert.Equal(Describe(BossLoot.Roll(boss, floor.Seed)), Describe(turns.GroundItemOf(boss)!));
        Assert.NotEqual(Describe(LootTable.Roll(boss, floor.Seed)), Describe(turns.GroundItemOf(boss)!));
        Assert.NotEqual(Describe(BossLoot.Roll(promisedDummy, floor.Seed)), Describe(turns.GroundItemOf(promisedDummy)!));

        var unnamed = new TurnSystem(floor.Map.Grid, Party(), floor.Enemies, () => 10, floor.Seed) { ResurrectionActive = false };
        Assert.Equal(Describe(LootTable.Roll(boss, floor.Seed)), Describe(unnamed.GroundItemOf(boss)!));

        // And a row that is not this floor's has no answer here: its drop is its own floor's.
        var stranger = Floor.Generate(Seed, 0, bottom: false, Tutorial).Enemies[0];
        stranger.Alive = false;
        Assert.Throws<InvalidOperationException>(() => turns.GroundItemOf(stranger));
    }

    [Fact]
    public void TheThreeDepthSourcesStayApart()
    {
        // PHASE4-060: three sources, three depths, all three kept. The farm buys
        // depth in the weapon's own forged modifiers - its class signature at
        // their head - out of its acquired budget; the boss's graft is a chosen
        // off-class modifier, forged, so it runs a stack deeper; the enchanter's
        // (Phase 6a) is an off-class modifier the player did not pick, grafted
        // acquired - its seam, CanGraft asked as acquired, is the relations alone -
        // and shallow by construction.
        var farmed = LootTable.Roll(Dummy("tower_guard", 20), Seed).Weapon;
        Assert.Equal("tower_guard", farmed.Id);
        Assert.Equal(farmed.Forged.Entries.Select(e => e.Type), farmed.Modifiers.Entries.Select(e => e.Type));   // depth, never breadth
        Assert.Equal((7, 6), (farmed.Stacks(Block), farmed.Stacks(Push)));                                   // Block, the sword's signature: forged 2 + 5

        var bow = BossLoot.Roll(DungeonState.BossRow(Tutorial, TestWeapons.Get("hatchet")), DungeonState.FloorSeed(18, 1)).Weapon;
        Assert.Equal(("hunting_bow", 1), (bow.Id, bow.Forged.Stacks(Block)));   // Block on a bow: off-class, forged
        Assert.Equal(6, Rules.Cap(Block, bow.Forged.Stacks(Block)));

        var serviced = TestWeapons.Get("hunting_bow");
        Assert.True(Rules.CanGraft(Block, serviced.Kind, serviced.Modifiers, serviced.Forged, asForged: false));
        serviced.Acquire(Block, 1);
        Assert.Equal((0, 1, 5), (serviced.Forged.Stacks(Block), serviced.Stacks(Block), Rules.Cap(Block, serviced.Forged.Stacks(Block))));

        Assert.True(Rules.Cap(Block, farmed.Forged.Stacks(Block)) > Rules.Cap(Block, bow.Forged.Stacks(Block)));
        Assert.True(Rules.Cap(Block, bow.Forged.Stacks(Block)) > Rules.Cap(Block, serviced.Forged.Stacks(Block)));
    }

    [Fact]
    public void EverythingInTheTutorialRollsForBlock()
    {
        // PHASE4-073: the golem has Block innate whatever it holds, the weapon it
        // drops comes away forged with Block, and everything else killed down
        // there rolls for Block on top of its usual graft pool.
        var visit = DungeonState.Enter(Tutorial, Seed, Party());
        Assert.Equal(Block, Tutorial.Theme);
        var rows = visit.Floors.SelectMany(f => f.Enemies.Select(e => (Floor: f, Row: e))).ToList();
        Assert.All(rows, r => Assert.Equal<ModifierType?>(Block, r.Row.Theme));

        var golem = visit.Floors[^1].Boss!;
        Assert.Equal(3, golem.Innate.Stacks(Block));
        var golemsDrop = BossLoot.Roll(golem, visit.Floors[^1].Seed).Weapon;
        Assert.Equal(Spread(Weapons[golemsDrop.Id]).Stacks(Block) + 1, golemsDrop.Forged.Stacks(Block));

        // Nothing here has been farmed, and Block goes on every variant of all
        // eight classes (its table is the whole of it), so whether a drop is
        // forged with Block is the one question LootTable.Eligible leaves: its
        // theme entry asks nothing of a unique.
        foreach (var (floor, row) in rows.Where(r => r.Row != golem))
        {
            var drop = LootTable.Roll(row, floor.Seed).Weapon;
            var usual = EligibleFor(drop, theme: null);
            var inTheTutorial = EligibleFor(drop, row.Theme);
            if (drop.Forged.Stacks(Block) == 0)
                Assert.Equal(usual.Append(Block), inTheTutorial);   // the usual pool, plus Block
            else
                Assert.Equal(usual, inTheTutorial);               // a sword already carries it, as its own depth
        }
    }
}
