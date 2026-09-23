using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Collection (§3.2, §3.5): while the dungeon still resurrects its dummies a
/// defeat hands over nothing at all, and once resurrection has stopped the next
/// defeat is the permanent kill that yields the weapon. <em>What</em> it yields
/// is <see cref="LootTableTests"/>'; that it yields it once, on that kill, off
/// the floor's own seed, and that it is still there after a reload, is this.
/// <para>
/// The switch is <c>TurnSystem.ResurrectionActive</c>, the seam Phase 4's boss
/// takes over (§4.3). Nothing in the logic flips it, so every test below throws it
/// by hand exactly where the boss will.
/// </para>
/// </summary>
public class DropCollectionTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The prototype's own map seed; it overflows int32, which is the interesting case for a stream mix.</summary>
    private const long Seed = 2762136374;

    /// <summary>A map seed whose first five revival cycles at spawn index 0 pay both axes, so a farmed dummy is measurably stronger.</summary>
    private const long Mixed = 1;

    /// <summary>A map seed a hatchet dummy's drop wins the unique roll on at <c>DefeatCount</c> 40 (pinned in the test that uses it).</summary>
    private const long UniqueWins = 11;

    /// <summary>The seed next door, which it loses on at the same count and the same spawn index: the roll is the floor's, not a constant.</summary>
    private const long UniqueLoses = 10;

    /// <summary>
    /// A blow that never clears a dummy's constitution, so every kill here is
    /// worth exactly one cycle, and cheap enough that a dummy grown fat on
    /// revivals still falls inside one turn.
    /// </summary>
    private static Weapon Chisel() => TestWeapons.Make("Chisel", range: 60, damage: 40, cost: 10);

    private sealed record Farm(TurnSystem Turns, PartyMemberState A, EnemyState Enemy, List<(EnemyState Corpse, Drop Drop)> Dropped);

    /// <summary>
    /// One member in reach of one dummy, with every drop the turn system raises
    /// recorded. The dummy is never made visible, so the farm is the only thing
    /// moving: it is killed in the player phase and dead for the enemy phase that
    /// follows, every cycle.
    /// </summary>
    private static Farm Build(string dummyWeaponId = "hatchet", int defeatCount = 0, int spawnIndex = 0, long mapSeed = Seed)
    {
        var a = TestPools.Char("A", x: 5 * Tile + 16, y: 5 * Tile + 16);
        a.Inventory[0] = Chisel();
        var enemy = new EnemyState
        {
            X = 5 * Tile + 16 + 40f,
            Y = 5 * Tile + 16,
            Weapon = TestWeapons.Get(dummyWeaponId),
            SpawnIndex = spawnIndex,
            DefeatCount = defeatCount,
            RevivalsRolled = defeatCount,   // a preset count is a history already paid: these tests are about the drop, not the statline
        };
        var turns = new TurnSystem(new int[20, 20], new[] { a }, new[] { enemy }, () => 10, mapSeed);
        var dropped = new List<(EnemyState, Drop)>();
        turns.EnemyDropped += (corpse, drop) => dropped.Add((corpse, drop));
        return new Farm(turns, a, enemy, dropped);
    }

    private static void Advance(TurnSystem turns, float seconds = 3f, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>Swing until it falls. The cap is a guard against a fixture that cannot finish rather than a rule.</summary>
    private static void Kill(Farm farm)
    {
        for (int swing = 0; farm.Enemy.Alive; swing++)
        {
            Assert.True(swing < 10, "the fixture's weapon never finished the dummy");
            Assert.True(farm.Turns.TryAttack(farm.A, farm.Enemy));
        }
    }

    /// <summary>End turns until it is back up, and answer with how many that took.</summary>
    private static int WaitForRevival(Farm farm)
    {
        int waited = 0;
        while (!farm.Enemy.Alive)
        {
            Assert.True(waited < 20, "the dummy never came back");
            farm.Turns.EndTurn();
            Advance(farm.Turns);
            waited++;
        }
        return waited;
    }

    /// <summary>End turns without expecting anything of them: what a stopped dungeon is asked to do is nothing.</summary>
    private static void WaitOut(Farm farm, int turns)
    {
        for (int turn = 0; turn < turns; turn++)
        {
            farm.Turns.EndTurn();
            Advance(farm.Turns);
        }
    }

    /// <summary>Everything about a drop a seed is supposed to fix: the item, its live spread, its entries and the unique outcome.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] " +
           string.Join(", ", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}+{e.Xp}")) +
           $" unique={drop.WasUniqueRoll} count={drop.DefeatCount}";

    private static (int R, int C) TileOf(ActorState actor)
        => ((int)MathF.Floor(actor.Y / Tile), (int)MathF.Floor(actor.X / Tile));

    // ── While the dungeon still raises them ──────────────────────────────────

    [Fact]
    public void NothingDropsWhileResurrectionRuns()
    {
        // "Nothing is handed over at the time": ten cycles of a farm bank ten
        // cycles of quality into the dummy and hand the party not one weapon.
        // Between the kill and the revival there is nothing on the floor either
        // - the corpse is carrying, not offering.
        var farm = Build();
        Assert.True(farm.Turns.ResurrectionActive);   // the dungeon opens as an infinite farm

        for (int cycle = 0; cycle < 10; cycle++)
        {
            Kill(farm);
            Assert.Null(farm.Turns.GroundItemOf(farm.Enemy));
            WaitForRevival(farm);
        }

        Assert.Equal(10, farm.Enemy.DefeatCount);
        Assert.Empty(farm.Dropped);
        Assert.True(farm.Enemy.Alive);
    }

    // ── The permanent kill ───────────────────────────────────────────────────

    [Fact]
    public void TheLastKillCollectsIt()
    {
        var farm = Build();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Kill(farm);
            WaitForRevival(farm);
        }
        Assert.Equal(3, farm.Enemy.DefeatCount);
        Assert.Empty(farm.Dropped);

        // The boss is Phase 4's; the switch it will throw is here.
        farm.Turns.ResurrectionActive = false;
        Kill(farm);

        var (corpse, drop) = Assert.Single(farm.Dropped);
        Assert.Same(farm.Enemy, corpse);

        // The killing blow is a defeat like every other, so the quality the drop
        // is stamped with is the one the dummy's own plate now reads: what the
        // player was looking at is what they are handed.
        Assert.Equal(4, farm.Enemy.DefeatCount);
        Assert.Equal(farm.Enemy.DefeatCount, drop.DefeatCount);

        // And it is the floor's table's answer rather than a roll of its own: the
        // seed the turn system was built with, and this dummy's row.
        Assert.Equal(Describe(LootTable.Roll(farm.Enemy, Seed)), Describe(drop));
        Assert.Equal(farm.Enemy.Weapon.Class, drop.Weapon.Class);

        // It does not get up again, and there is no second drop to be had: the
        // timer it would have come back on is six turns, and the party waits out
        // twice that.
        Assert.Equal(6, FarmLadder.ResurrectTurns(farm.Enemy.DefeatCount));
        WaitOut(farm, 12);
        Assert.False(farm.Enemy.Alive);
        Assert.Single(farm.Dropped);
    }

    [Fact]
    public void TheUniqueRollResolvesOnThatKillAndNoOther()
    {
        // The seeds' own answers first, so what follows is a claim about the kill
        // rather than about which way a draw fell: at DefeatCount 40 one floor's
        // roll for this dummy wins and the next floor's loses.
        static EnemyState AtForty() => new() { Weapon = TestWeapons.Get("hatchet"), DefeatCount = 40 };
        Assert.True(LootTable.Roll(AtForty(), UniqueWins).WasUniqueRoll);
        Assert.False(LootTable.Roll(AtForty(), UniqueLoses).WasUniqueRoll);

        // A dummy the killing blow farms to 40. The roll fires once, on that
        // kill, and never per defeat: the count set the odds, and the extraction
        // is where the player finds out.
        var won = Build(defeatCount: 39, mapSeed: UniqueWins);
        won.Turns.ResurrectionActive = false;
        Kill(won);

        var drop = Assert.Single(won.Dropped).Drop;
        Assert.Equal(40, won.Enemy.DefeatCount);
        Assert.True(drop.WasUniqueRoll);
        Assert.True(drop.Weapon.Unique);
        Assert.Equal(won.Enemy.Weapon.Class, drop.Weapon.Class);

        // Re-killing is not a second roll, because there is nothing to re-kill:
        // the dummy stays down however long the party waits.
        WaitOut(won, 15);
        Assert.False(won.Enemy.Alive);
        Assert.Single(won.Dropped);

        // The same farm on the floor next door loses the roll and is handed the
        // ordinary drop, at the same depth: the gamble is real either way.
        var lost = Build(defeatCount: 39, mapSeed: UniqueLoses);
        lost.Turns.ResurrectionActive = false;
        Kill(lost);

        var ordinary = Assert.Single(lost.Dropped).Drop;
        Assert.False(ordinary.WasUniqueRoll);
        Assert.False(ordinary.Weapon.Unique);
        Assert.Equal(40, ordinary.DefeatCount);
    }

    [Fact]
    public void AFarmedFloorStaysStrongWhenResurrectionStops()
    {
        // Stopping resurrection ends the ladder and refunds none of it. That is
        // the retreat gauntlet's whole cost: the corridor behind the party is
        // full of dummies that are stronger for having died, and killing the
        // boss does not soften one of them.
        var farm = Build(mapSeed: Mixed);
        var waits = new List<int>();
        for (int cycle = 0; cycle < 5; cycle++)
        {
            Kill(farm);
            waits.Add(WaitForRevival(farm));
        }

        int damage = farm.Enemy.BonusDamage;
        int maxHp = farm.Enemy.MaxHp;
        Assert.Equal(5, farm.Enemy.RevivalsRolled);
        Assert.True(damage > 0, "five cycles bought the dummy no damage at all");
        Assert.True(maxHp > GameConstants.DummyHp, "five cycles bought the dummy no constitution at all");

        // Stronger, and quicker back on its feet each time: both halves of what a
        // party that turns back without a boss kill has to climb through.
        Assert.True(waits[^1] < waits[0], $"the timer never shortened: {string.Join(", ", waits)}");
        Assert.Equal(5, FarmLadder.ResurrectTurns(farm.Enemy.DefeatCount));   // five turns, where a fresh dummy takes ten

        farm.Turns.ResurrectionActive = false;

        // Still standing, still that dangerous: the switch takes the future
        // cycles, not the ones already paid for.
        Assert.True(farm.Enemy.Alive);
        Assert.Equal(damage, farm.Enemy.BonusDamage);
        Assert.Equal(maxHp, farm.Enemy.MaxHp);

        // Put down for the last time, it keeps the statline a save would store,
        // and no sixth cycle is ever rolled however long the party waits.
        Kill(farm);
        WaitOut(farm, 15);
        Assert.False(farm.Enemy.Alive);
        Assert.Equal(damage, farm.Enemy.BonusDamage);
        Assert.Equal(maxHp, farm.Enemy.MaxHp);
        Assert.Equal(5, farm.Enemy.RevivalsRolled);
        Assert.Equal(6, farm.Enemy.DefeatCount);   // the last kill counted; nothing was paid out for it
    }

    // ── The reload ───────────────────────────────────────────────────────────

    [Fact]
    public void ADropSurvivesAReload()
    {
        // A save taken after the permanent kill and before the pickup stores the
        // enemy row and no ground item (§5.1). That is enough: Roll reads the row
        // and the floor's seed and nothing else, so the floor re-derives the
        // identical weapon on the identical tile - never a re-roll (a different
        // weapon) and never a shrug (a lost one).
        var farm = Build(defeatCount: 11, spawnIndex: 4);
        farm.Turns.ResurrectionActive = false;
        Kill(farm);

        var dropped = Assert.Single(farm.Dropped).Drop;
        var tile = TileOf(farm.Enemy);

        // Before the reload: the same item is still being offered by the corpse
        // it fell from, because the offer is derived rather than remembered.
        var standing = farm.Turns.GroundItemOf(farm.Enemy);
        Assert.NotNull(standing);
        Assert.Equal(Describe(dropped), Describe(standing));

        // The reload: the row, and none of the session that produced it.
        var loaded = new EnemyState
        {
            X = farm.Enemy.X,
            Y = farm.Enemy.Y,
            Weapon = TestWeapons.Get(farm.Enemy.Weapon.Id),
            SpawnIndex = farm.Enemy.SpawnIndex,
            DefeatCount = farm.Enemy.DefeatCount,
            RevivalsRolled = farm.Enemy.RevivalsRolled,
            DefeatedAtTurn = farm.Enemy.DefeatedAtTurn,
            Alive = false,
        };
        var reloaded = new TurnSystem(new int[20, 20], new[] { TestPools.Char("A") }, new[] { loaded }, () => 10, Seed)
        {
            ResurrectionActive = false,
        };

        var reDerived = reloaded.GroundItemOf(loaded);
        Assert.NotNull(reDerived);
        Assert.Equal(Describe(dropped), Describe(reDerived));
        Assert.Equal(tile, TileOf(loaded));

        // Picked up, it is retired. The flag on the row is the only thing that
        // stops the floor offering the same weapon on every entry forever.
        loaded.DropTaken = true;
        Assert.Null(reloaded.GroundItemOf(loaded));

        // And a corpse in a dungeon that still resurrects offers nothing at all,
        // taken or not: it is going to get up.
        loaded.DropTaken = false;
        reloaded.ResurrectionActive = true;
        Assert.Null(reloaded.GroundItemOf(loaded));
    }
}
