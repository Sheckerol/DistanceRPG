using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The farm as a dummy lives it (§3.2): how many times it has been put down,
/// what a blow that cleared its constitution is worth, and the statline it comes
/// back with. Nothing collects anything here — <c>DefeatCount</c> is the quality
/// of a drop nobody has rolled yet, and the only thing this file can see of it
/// is the other half of the same number: how dangerous the dummy has become.
/// <para>
/// Every revival roll is drawn on <c>mapSeed ^ ReviveSalt</c>, seeded per enemy
/// and per cycle, so the fixtures here name a seed and the sequence it produces
/// is a fact about that seed rather than about the order the test does things
/// in. <see cref="AllDamage"/> and <see cref="Mixed"/> are the two the file
/// leans on, each pinned by an assertion before it is used.
/// </para>
/// </summary>
public class FarmingTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>A map seed whose first five revival cycles at spawn index 0 all roll Damage, so the damage ladder is visible without a Health roll moving the bar under it.</summary>
    private const long AllDamage = 201;

    /// <summary>A map seed whose first five cycles at spawn index 0 roll Damage, Both, Health, Health, Both — all three outcomes, and both axes compounding.</summary>
    private const long Mixed = 1;

    /// <summary>The prototype's own map seed, for the one test that also places enemies.</summary>
    private const long PlacementSeed = 2762136374;

    // ── Fixtures ─────────────────────────────────────────────────────────────

    /// <summary>A blow that clears a fresh dummy's whole 50: one swing, one clean kill.</summary>
    private static Weapon Maul() => TestWeapons.Make("Maul", range: 60, damage: 50, cost: 30);

    /// <summary>A blow that never clears it, however long the farm runs: the ordinary kill, worth exactly one cycle.</summary>
    private static Weapon Chisel() => TestWeapons.Make("Chisel", range: 60, damage: 25, cost: 20);

    private sealed record Farm(TurnSystem Turns, PartyMemberState A, EnemyState Enemy);

    private static EnemyState Dummy(string weaponId, float x, float y, int spawnIndex = 0)
        => new() { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), SpawnIndex = spawnIndex };

    /// <summary>
    /// One member in reach of one dummy. The dummy is never made visible, so it
    /// stays passive and the only thing moving in these tests is the farm; the
    /// two tests that want it to swing say so.
    /// </summary>
    private static Farm Build(Weapon? held = null, string dummyWeaponId = "hatchet",
        Func<int>? roll = null, long mapSeed = 0)
    {
        var a = TestPools.Char("A", x: 5 * Tile + 16, y: 5 * Tile + 16);
        a.Inventory[0] = held ?? Chisel();
        var enemy = Dummy(dummyWeaponId, 5 * Tile + 16 + 40f, 5 * Tile + 16);
        var turns = new TurnSystem(new int[20, 20], new[] { a }, new[] { enemy }, roll ?? (() => 10), mapSeed);
        return new Farm(turns, a, enemy);
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
            Assert.True(swing < 8, "the fixture's weapon never finished the dummy");
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

    /// <summary>One farm cycle: put it down, wait it out, and it is standing again with its ladder resolved.</summary>
    private static void FarmOnce(Farm farm)
    {
        Kill(farm);
        WaitForRevival(farm);
    }

    // ── The count, and what a clean kill is worth ────────────────────────────

    [Fact]
    public void ADefeatAdvancesTheCount_ACleanKillAdvancesItTwice()
    {
        // A dummy already down to 3 HP takes a blow it was never going to
        // survive; that is a kill, not a clean one.
        var chipped = Build();
        chipped.Enemy.Hp = 3;
        Kill(chipped);
        Assert.Equal(1, chipped.Enemy.DefeatCount);

        // The same dummy's whole constitution in one blow counts twice.
        var cleaned = Build(held: Maul());
        Kill(cleaned);
        Assert.Equal(GameContent.Current.Tuning.CleanKillBonus, cleaned.Enemy.DefeatCount);
        Assert.Equal(2, cleaned.Enemy.DefeatCount);
    }

    [Fact]
    public void TheBarIsMaxHp_SoSofteningTheTargetNeverBuysACleanKill()
    {
        // The bar does not move, so nothing done to a target beforehand brings a
        // clean kill closer: softening makes the kill easier and the CLEAN kill
        // no more likely. The same weapon against the same dummy, once at full
        // and once at 3 HP, is worth exactly the same both times — both ways.
        var full = Build();
        var softened = Build();
        softened.Enemy.Hp = 3;
        Kill(full);
        Kill(softened);
        Assert.Equal(1, full.Enemy.DefeatCount);
        Assert.Equal(softened.Enemy.DefeatCount, full.Enemy.DefeatCount);

        var fullClean = Build(held: Maul());
        var softenedClean = Build(held: Maul());
        softenedClean.Enemy.Hp = 3;
        Kill(fullClean);
        Kill(softenedClean);
        Assert.Equal(2, fullClean.Enemy.DefeatCount);
        Assert.Equal(softenedClean.Enemy.DefeatCount, fullClean.Enemy.DefeatCount);
    }

    [Fact]
    public void ACritThroughBlockCanCleanKillWhereTheOrdinarySwingCannot()
    {
        // The bar is measured after mitigation and a crit skips Block entirely,
        // so the same weapon can clear a threshold its ordinary swing cannot.
        // That is not a leak to be closed: a clean kill you did not plan hands
        // the dummy two cycles of statline and brings it back sooner, on the
        // thing that was already beating you.
        var blocked = Build(held: Maul(), dummyWeaponId: EnemyState.DefaultWeaponId, roll: () => 10);
        Kill(blocked);
        Assert.Equal(1, blocked.Enemy.DefeatCount);   // 50 less the shield's 3 is 47, under a 50-HP constitution

        var crit = Build(held: Maul(), dummyWeaponId: EnemyState.DefaultWeaponId, roll: () => 20);
        Kill(crit);
        Assert.Equal(2, crit.Enemy.DefeatCount);      // the crit ignores the shield and doubles: 100, clean
    }

    [Fact]
    public void ATickDeathAdvancesByOne()
    {
        // Poison finishes it at the enemy phase's end. The kill names no weapon
        // and no wielder, and still counts: the dummy died, and nothing in §3.2
        // says by what.
        var farm = Build();
        farm.Enemy.Hp = 3;
        farm.Enemy.ApplyStatus(StatusEffectType.Poison, null, 5);

        farm.Turns.EndTurn();
        Advance(farm.Turns);

        Assert.False(farm.Enemy.Alive);
        Assert.Equal(1, farm.Enemy.DefeatCount);
    }

    // ── What comes back ──────────────────────────────────────────────────────

    [Fact]
    public void EveryRevivalRollsDamageHealthOrBoth_AndCompounds()
    {
        // The seed's own sequence first, so the statline below is a claim about
        // the ladder rather than about which way a die fell.
        var rolled = Enumerable.Range(0, 5)
            .Select(cycle => FarmLadder.RollRevival(FarmLadder.ReviveStream(Mixed, spawnIndex: 0, cycle)))
            .ToArray();
        Assert.Equal(
            new[] { RevivalGain.Damage, RevivalGain.Both, RevivalGain.Health, RevivalGain.Health, RevivalGain.Both },
            rolled);

        var farm = Build(mapSeed: Mixed);
        for (int cycle = 0; cycle < 5; cycle++)
            FarmOnce(farm);

        // Damage: 18 -> 21 -> 24 -> 28 across the two Damage-bearing rolls it
        // took. Health: 50 -> 58 -> 67 -> 77 -> 89, each step a percentage of
        // the number the last one produced, which is what compounding means. A
        // flat step would have given 4 x 5 on one axis and 3 x 5 on the other.
        Assert.Equal(5, farm.Enemy.RevivalsRolled);
        Assert.Equal(5, farm.Enemy.DefeatCount);
        Assert.Equal(10, farm.Enemy.RevivalDamage);
        Assert.Equal(39, farm.Enemy.RevivalMaxHp);
        Assert.Equal(89, farm.Enemy.MaxHp);

        // A Health roll is "restored full" at the maximum it just raised.
        Assert.Equal(farm.Enemy.MaxHp, farm.Enemy.Hp);

        // And it puts the clean-kill bonus out of business by the most direct
        // route available: the bar IS max HP, so the blow that cleanly killed
        // this dummy at DefeatCount 0 no longer clears it.
        Assert.Equal(2, FarmLadder.DefeatAdvance(dealt: 50, maxHp: GameConstants.DummyHp));
        Assert.Equal(1, FarmLadder.DefeatAdvance(dealt: 50, farm.Enemy.MaxHp));
    }

    [Fact]
    public void TheStepReadsTheWholeCurrentDamage_NotTheBonusAlone()
    {
        Assert.All(Enumerable.Range(0, 5),
            cycle => Assert.Equal(RevivalGain.Damage,
                FarmLadder.RollRevival(FarmLadder.ReviveStream(AllDamage, spawnIndex: 0, cycle))));

        // An 18-damage axe dummy: 18 -> 21 -> 24 -> 28 -> 32 -> 37. The step is a
        // percentage of the dummy's WHOLE current damage, its weapon's plus what
        // it has already banked; a step off the accumulated bonus alone would
        // start at zero, floor at 1 and read 1, 2, 3, 4, 5 forever.
        var farm = Build(mapSeed: AllDamage);
        var afterEachCycle = new List<int>();
        for (int cycle = 0; cycle < 5; cycle++)
        {
            FarmOnce(farm);
            afterEachCycle.Add(farm.Enemy.Weapon.Damage + farm.Enemy.BonusDamage);
        }

        Assert.Equal(new[] { 21, 24, 28, 32, 37 }, afterEachCycle);
        Assert.Equal(19, farm.Enemy.RevivalDamage);
        Assert.Equal(GameConstants.DummyHp, farm.Enemy.MaxHp);   // no Health roll: the bar never moved
    }

    [Fact]
    public void ACleanKillResolvesTwoRevivalsInOrder()
    {
        // Two cycles fall due at once, and the second must read the statline the
        // first produced. A 15-damage dagger dummy: 15 -> 17 -> 20, so the bonus
        // is 2 + 3. Both rolls reading the pre-kill 15 would have given 2 + 2,
        // which is the whole of what "in order" buys.
        var farm = Build(held: Maul(), dummyWeaponId: "weakspot_stiletto", mapSeed: AllDamage);
        Kill(farm);
        int waited = WaitForRevival(farm);

        Assert.Equal(2, farm.Enemy.DefeatCount);
        Assert.Equal(2, farm.Enemy.RevivalsRolled);
        Assert.Equal(5, farm.Enemy.RevivalDamage);
        Assert.NotEqual(4, farm.Enemy.RevivalDamage);
        Assert.Equal(20, farm.Enemy.Weapon.Damage + farm.Enemy.BonusDamage);

        // Two cycles of statline, and the timer advanced twice with them: it
        // came back in eight turns rather than the nine one defeat would buy.
        // You are not skipping the cost, you are paying it faster.
        Assert.Equal(8, waited);
    }

    [Fact]
    public void TheCountIsWrittenByTheKilledApplier_NeverByAHandler()
    {
        // The defeat attaches through the event table like everything else: a
        // handler on Killed transforms a payload and sees a world nothing has
        // written yet, and the applier — the event's one world-write — advances
        // the count from the payload's own unclamped figure. The advance sits
        // ahead of the applier's mana guard, or an ordinary kill, which settles
        // no mana at all, would never count.
        var farm = Build(held: Maul());
        var seen = new List<(int Dealt, int CountAtHandlerTime)>();
        farm.Turns.Events.On<KillPayload>(GameEvent.Killed, new HandlerPriority(9, 9), "probe",
            (payload, _, other) =>
            {
                seen.Add((payload.Dealt, ((EnemyState)other).DefeatCount));
                return payload;
            });

        Kill(farm);

        Assert.Equal(new[] { (50, 0) }, seen);          // the handler saw the blow, and a count nobody had touched
        Assert.Equal(2, farm.Enemy.DefeatCount);        // the applier wrote it, from that same figure
    }

    [Fact]
    public void TheTimerShortensWithTheCount_AndFloorsAtThree()
    {
        // End to end through the turn loop, at three points on the ladder. The
        // two deeper ones are handed their history the way a load will (§5.1):
        // the count is the whole of what the timer reads.
        Assert.Equal(9, WaitedAt(previousDefeats: 0));
        Assert.Equal(6, WaitedAt(previousDefeats: 3));
        Assert.Equal(3, WaitedAt(previousDefeats: 8));

        static int WaitedAt(int previousDefeats)
        {
            var farm = Build();
            farm.Enemy.DefeatCount = previousDefeats;
            farm.Enemy.RevivalsRolled = previousDefeats;
            Kill(farm);
            Assert.Equal(previousDefeats + 1, farm.Enemy.DefeatCount);
            int waited = WaitForRevival(farm);
            Assert.Equal(FarmLadder.ResurrectTurns(farm.Enemy.DefeatCount), waited);
            return waited;
        }
    }

    [Fact]
    public void TheBonusesLandOnTheActorNotTheWeapon()
    {
        var farm = Build(mapSeed: AllDamage);
        for (int cycle = 0; cycle < 5; cycle++)
            FarmOnce(farm);

        // The dummy hits for 37 where a fresh one hits for 18 — and the item it
        // is holding is the item it was handed. Drop quality comes from the
        // ladder, never from the statline the ladder armed it with.
        var fresh = TestWeapons.Get("hatchet");
        Assert.Equal(19, farm.Enemy.RevivalDamage);
        Assert.Equal(fresh.Damage, farm.Enemy.Weapon.Damage);
        foreach (var type in Enum.GetValues<ModifierType>())
            Assert.Equal(fresh.Modifiers.Stacks(type), farm.Enemy.Weapon.Modifiers.Stacks(type));
        Assert.Equal(37, CombatRules.BaseDamage(farm.Enemy, farm.Enemy.Weapon));

        // And it reaches play: seen at last, it swings for the whole number.
        var hits = new List<AttackResolution>();
        farm.Turns.CharacterHit += (_, resolution) => hits.Add(resolution);
        farm.Turns.NotifyEnemyVisible(farm.Enemy, true);
        farm.Turns.EndTurn();
        Advance(farm.Turns, 6f);

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.Equal(37, hit.Dealt));
    }

    // ── Determinism: the ladder is the dummy's, not the session's ───────────

    [Fact]
    public void KillOrderDoesNotChangeAnyDummysLadder()
    {
        // Per-enemy, per-cycle seeding is bought precisely for this: which
        // corpse the party walks to first is the player's business and may not
        // change what either dummy becomes.
        Assert.Equal(TwoDummies(nearestFirst: true), TwoDummies(nearestFirst: false));

        static (int, int, int, int) TwoDummies(bool nearestFirst)
        {
            var a = TestPools.Char("A", x: 5 * Tile + 16, y: 5 * Tile + 16);
            a.Inventory[0] = Chisel();
            var first = Dummy("hatchet", 5 * Tile + 16 + 40f, 5 * Tile + 16, spawnIndex: 0);
            var second = Dummy("hatchet", 5 * Tile + 16, 5 * Tile + 16 + 40f, spawnIndex: 1);
            var turns = new TurnSystem(new int[20, 20], new[] { a }, new[] { first, second }, () => 10, Mixed);

            for (int round = 0; round < 2; round++)
            {
                foreach (var enemy in nearestFirst ? new[] { first, second } : new[] { second, first })
                    for (int swing = 0; enemy.Alive; swing++)
                    {
                        Assert.True(swing < 8, "the fixture's weapon never finished the dummy");
                        Assert.True(turns.TryAttack(a, enemy));
                    }

                for (int waited = 0; !first.Alive || !second.Alive; waited++)
                {
                    Assert.True(waited < 20, "a dummy never came back");
                    turns.EndTurn();
                    Advance(turns);
                }
            }

            Assert.True(first.RevivalDamage + first.RevivalMaxHp > 0);
            Assert.True(second.RevivalDamage + second.RevivalMaxHp > 0);
            return (first.RevivalDamage, first.RevivalMaxHp, second.RevivalDamage, second.RevivalMaxHp);
        }
    }

    [Fact]
    public void TheRevivalRollDoesNotConsumeThePlacementStream()
    {
        // The revival roll fires from inside the turn loop, which is where a
        // stream mix-up is easiest and least visible. Placement is identical
        // either side of a hundred revivals on the same seed.
        var before = EnemyPlacer.PlaceEnemies(MapGenerator.Generate(new Mulberry32(PlacementSeed)), PlacementSeed);

        var farm = Build(mapSeed: PlacementSeed);
        Kill(farm);
        farm.Enemy.DefeatCount = 100;    // a hundred cycles fall due at once, and resolve in order
        WaitForRevival(farm);

        // A hundred cycles in, and the scaling has not stopped: nothing caps
        // either axis, which is what lets the drop ladder run forever too — every
        // further cycle is strictly more dangerous, and the player is the only
        // one who decides where that stops being worth it.
        Assert.Equal(100, farm.Enemy.RevivalsRolled);
        Assert.True(farm.Enemy.MaxHp > 20 * GameConstants.DummyHp, $"max HP after a hundred revivals: {farm.Enemy.MaxHp}");
        Assert.True(farm.Enemy.BonusDamage > 20 * farm.Enemy.Weapon.Damage, $"bonus damage after a hundred revivals: {farm.Enemy.BonusDamage}");

        var after = EnemyPlacer.PlaceEnemies(MapGenerator.Generate(new Mulberry32(PlacementSeed)), PlacementSeed);
        Assert.Equal(before, after);
    }

    [Fact]
    public void TwoTurnSystemsOnTheSameSeedAndKillSequenceAgree()
    {
        Assert.Equal(Ladder(Mixed), Ladder(Mixed));
        Assert.NotEqual(Ladder(Mixed), Ladder(AllDamage));

        static (int Damage, int MaxHp) Ladder(long mapSeed)
        {
            var farm = Build(mapSeed: mapSeed);
            for (int cycle = 0; cycle < 4; cycle++)
                FarmOnce(farm);
            return (farm.Enemy.RevivalDamage, farm.Enemy.RevivalMaxHp);
        }
    }

    [Fact]
    public void AccumulatedValuesAreTheWholeState()
    {
        // The Phase 5 seam: a save stores the accumulated values rather than a
        // stream position, so a dummy handed them back is the dummy it was.
        var farmed = Build(mapSeed: Mixed);
        for (int cycle = 0; cycle < 3; cycle++)
            FarmOnce(farmed);

        var reloaded = Build(mapSeed: Mixed);
        reloaded.Enemy.SpawnIndex = farmed.Enemy.SpawnIndex;
        reloaded.Enemy.DefeatCount = farmed.Enemy.DefeatCount;
        reloaded.Enemy.RevivalsRolled = farmed.Enemy.RevivalsRolled;
        reloaded.Enemy.RevivalDamage = farmed.Enemy.RevivalDamage;
        reloaded.Enemy.RevivalMaxHp = farmed.Enemy.RevivalMaxHp;
        reloaded.Enemy.Hp = farmed.Enemy.Hp;

        Assert.Equal(farmed.Enemy.MaxHp, reloaded.Enemy.MaxHp);
        Assert.Equal(farmed.Enemy.BonusDamage, reloaded.Enemy.BonusDamage);
        Assert.Equal(CombatRules.BaseDamage(farmed.Enemy, farmed.Enemy.Weapon),
            CombatRules.BaseDamage(reloaded.Enemy, reloaded.Enemy.Weapon));

        // And it dies and comes back the same way: one more cycle apiece.
        FarmOnce(farmed);
        FarmOnce(reloaded);
        Assert.Equal(
            (farmed.Enemy.DefeatCount, farmed.Enemy.RevivalsRolled, farmed.Enemy.RevivalDamage, farmed.Enemy.RevivalMaxHp),
            (reloaded.Enemy.DefeatCount, reloaded.Enemy.RevivalsRolled, reloaded.Enemy.RevivalDamage, reloaded.Enemy.RevivalMaxHp));
    }

    [Fact]
    public void NoWasAtFullHpFlagExists()
    {
        // "From full HP to zero in one hit" was the first draft of the clean
        // kill, and it is the same rule stated worse. The general version needs
        // no flag and stops caring whether the enemy had already acted, so there
        // is nothing on the dummy or in the payload to keep in step.
        foreach (var type in new[] { typeof(EnemyState), typeof(DamagePayload), typeof(KillPayload) })
        {
            var flags = type
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(m => m.Name)
                .Where(name => name.Contains("FullHp", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("AtFull", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("WasFull", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            Assert.Equal([], flags);
        }

        // One comparison rather than a piece of tracked state: the unclamped
        // figure is already on the payload, and the bar is the dummy's own.
        Assert.NotNull(typeof(KillPayload).GetProperty(nameof(KillPayload.Dealt)));
        Assert.Equal(2, FarmLadder.DefeatAdvance(dealt: 40, maxHp: 24));
    }
}
