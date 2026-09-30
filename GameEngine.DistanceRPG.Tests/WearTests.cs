using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §6.1, wear is earned (Phase 6a.1): a weapon banks <c>WearPerHit</c> for every
/// hit it lands — per body, never per swing, per turn or scaled by damage — up to
/// its capacity, which is replayed from the wear it has ever cashed under the
/// user's ruling of 2026-09-25 (R4). A staff's landed cast is a hit, a wand's
/// cast is the hits its shape lands (PHASE 6a DECISION 1). Nothing else wears a
/// weapon: no status tick, no hit on the dead, no enemy, and no arrival comes
/// worn. A full weapon plays exactly as a fresh one, and says so once. Wear
/// accrues in one place, which the IL pins. The exchanges stand on the numbers
/// they were written against (<see cref="Premise"/>), made current for each test;
/// the ruling's own numbers are the shipped master's, pinned as the ruling states
/// them; so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class WearTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, Weapon weapon)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, weapon, x: x, y: y);
    }

    private static PartyMemberState Char(string id, int r, int c, string weaponId) => Char(id, r, c, TestWeapons.Get(weaponId));

    /// <summary>A dummy on a tile's centre, with enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

    /// <summary>One hit through the turn system's table, whoever swings: the applier writes, and whatever it queues drains.</summary>
    private static AttackResolution Hit(TurnSystem turns, ActorState attacker, ActorState defender, int roll = 10)
        => CombatRules.Resolve(turns.Events, attacker, defender, attacker.EquippedWeapon!,
            CombatRules.SurfaceDistanceUnits(attacker, defender), () => roll);

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary><paramref name="weapon"/> brought back as a save would bring it, with <paramref name="wear"/> banked and <paramref name="cashed"/> ever cashed: the restore door, which a load goes through.</summary>
    private static Weapon Worn(Weapon weapon, int wear, int cashed = 0)
        => new(GameContent.Current.Weapons[weapon.Id], weapon.Forged, weapon.Modifiers, weapon.Enchantments, weapon.ForgedEnchantmentCount, wear, cashed);

    // ── The ruling's numbers ─────────────────────────────────────────────────

    [Fact]
    public void ACapacityReplaysFullBars_TheRulingsNumbers()
    {
        // R4 (settled.md, S:1655–1676): a weapon's capacity starts at 30,
        // StartingPool, and rises by 1 for every full bar's worth of wear cashed,
        // the bar being the capacity at the time, the progress carrying. These are
        // the ruling's own numbers, so they are pinned as the ruling states them,
        // against the master (REWORK DECISION 3; ShippedDataTests lists the pin):
        // an edit to StartingPool or WearCapacityPerBar fails here by name.
        using (TestContent.Use())
        {
            Assert.Equal((30, 1), (GameContent.Current.Tuning.StartingPool, GameContent.Current.Tuning.WearCapacityPerBar));
            Assert.Equal(new PoolProgress(0, 30, 0, 30), Wear.Capacity(0));
            Assert.Equal(30, Wear.Capacity(29).Max);
            Assert.Equal(new PoolProgress(1, 31, 0, 31), Wear.Capacity(30));   // "#1 at 30 hits, capacity 30 → 31"
            Assert.Equal(new PoolProgress(1, 31, 15, 31), Wear.Capacity(45));  // a partial deposit counts toward the next point
            Assert.Equal(new PoolProgress(5, 35, 0, 35), Wear.Capacity(160));  // "bars of 31, 32, 33 and 34 take capacity to 35, at 160 hits in all"
            Assert.Equal(36, Wear.Capacity(195).Max);   // #2 at 195
            Assert.Equal(41, Wear.Capacity(385).Max);   // #3 at 385
            Assert.Equal(46, Wear.Capacity(600).Max);   // #4 at 600
            Assert.Equal(51, Wear.Capacity(840).Max);   // #5 at 840, and on past the fifth slot's 50: no top
            Assert.Equal(new PoolProgress(30, 60, 0, 60), Wear.Capacity(1335));   // the sum of 30 to 59: consequence 2's capacity 60

            // A weapon reads its capacity off what it has cashed, through the same replay.
            var cashed = Worn(TestWeapons.Get("arming_sword"), wear: 12, cashed: 195);
            Assert.Equal((12, 195, 36, false), (cashed.Wear, cashed.WearCashed, cashed.WearCapacity, cashed.WornFull));
            Assert.Equal(Wear.Capacity(195), cashed.WearPool);
        }

        // WearCapacityPerBar 0 freezes the capacity: "a dial rather than an error"
        // (S:1757), worked out in closed form, so no billion bars are replayed.
        using (TestContent.Use(tuning: ShippedContent.Tuning with { WearCapacityPerBar = 0 }))
        {
            Assert.Equal(new PoolProgress(333, 30, 10, 30), Wear.Capacity(10_000));
            Assert.Equal(30, Wear.Capacity(ContentBounds.MostCount).Max);
        }

        // 2 a bar: a first full bar of 30 lifts it two, and the next bar is 32.
        using (TestContent.Use(tuning: ShippedContent.Tuning with { WearCapacityPerBar = 2 }))
        {
            Assert.Equal(new PoolProgress(1, 32, 0, 32), Wear.Capacity(30));
            Assert.Equal(new PoolProgress(2, 34, 0, 34), Wear.Capacity(62));
        }

        // The worst a save can hold replays whole: the most wear any count reaches,
        // on the smallest bar the smallest growth climbs from — the replay's most
        // steps, about 46 000 — and a capacity nowhere near overflow.
        using (TestContent.Use(tuning: ShippedContent.Tuning with { StartingPool = 1, WearCapacityPerBar = 1 }))
        {
            var most = Wear.Capacity(ContentBounds.MostCount);
            Assert.InRange(most.Max, 46_000, 47_000);
            Assert.InRange(most.XpIntoNext, 0, most.XpToNext - 1);
        }

        // Wear cashed is never negative.
        Assert.Throws<ArgumentOutOfRangeException>(() => Wear.Capacity(-1));
    }

    // ── What is a hit ────────────────────────────────────────────────────────

    [Fact]
    public void EveryHitBanksWearPerHit_PerBody()
    {
        // S:785–788: WearPerHit is "per hit rather than per swing, per turn or
        // scaled by damage". The Great Axe's Cleave x2 swing lands on three bodies:
        // three hits, three wear — one swing, one movement charge.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "great_axe");
        var axe = a.EquippedWeapon!;
        var e1 = Enemy(5, 6);
        var e2 = Enemy(6, 6);
        var e3 = Enemy(5, 7);
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2, e3 }, () => 10);
        Assert.Equal((0, 30), (axe.Wear, axe.WearCapacity));
        Assert.True(turns.TryAttack(a, e1));
        Assert.Equal((200 - 15, 200 - 15, 200 - 15), (e1.Hp, e2.Hp, e3.Hp));
        Assert.Equal(3, axe.Wear);

        // A counter is a hit: the Riposte Blade answers a blocked swing, and the
        // counter wears the blade — taking the blow wears nothing.
        var b = Char("B", 5, 5, "riposte_blade");
        var blade = b.EquippedWeapon!;
        var sword = Enemy(5, 6);
        var counter = new TurnSystem(grid, new[] { b }, new[] { sword }, () => 10);
        int countered = 0;
        counter.RiposteTriggered += _ => countered++;
        Hit(counter, sword, b);
        Assert.Equal(1, countered);
        Assert.Equal(1, blade.Wear);

        // A brace is a hit: the pike's free swing at an enemy walking into its reach.
        var c = TestPools.Holding("C", TestWeapons.Get("skirmishers_pike"), x: 5 * Tile + 16, y: 5 * Tile + 16);
        var pike = c.EquippedWeapon!;
        var walker = new EnemyState { X = c.X + 250f, Y = c.Y };
        var braces = new TurnSystem(new int[20, 30], new[] { c }, new[] { walker }, () => 10);
        int braced = 0;
        braces.BraceTriggered += _ => braced++;
        braces.NotifyEnemyVisible(walker, true);
        braces.EndTurn();
        Advance(braces, 10f);
        Assert.Equal(1, braced);
        Assert.Equal(1, pike.Wear);

        // Never scaled by damage: a blow a Ward swallows all but the last point of
        // wears the weapon one, and so does a crit for three times as much.
        var d = Char("D", 5, 5, "weakspot_stiletto");
        var dagger = d.EquippedWeapon!;
        var warded = Enemy(5, 6);
        var plain = Enemy(5, 4);
        warded.ApplyStatus(StatusEffectType.Ward, null, 50);
        var blocked = new TurnSystem(grid, new[] { d }, new[] { warded, plain }, () => 10);
        var swallowed = Hit(blocked, d, warded);
        Assert.Equal(1, swallowed.Taken);
        Assert.True(swallowed.WardSpent > 0);
        Assert.Equal(1, dagger.Wear);
        var crit = Hit(blocked, d, plain, roll: 20);
        Assert.Equal(RollOutcome.Crit, crit.Roll.Outcome);
        Assert.True(crit.Taken > 3 * swallowed.Taken);
        Assert.Equal(2, dagger.Wear);

        // WearPerHit is the tuning's: at 2, the same fan banks 6; at 0, nothing wears.
        foreach (int perHit in new[] { 2, 0 })
        {
            using (TestContent.UsePremise(tuning: t => t with { WearPerHit = perHit }))
            {
                var fan = Char("A", 5, 5, "great_axe");
                var three = new[] { Enemy(5, 6), Enemy(6, 6), Enemy(5, 7) };
                var crowd = new TurnSystem(grid, new[] { fan }, three, () => 10);
                Assert.True(crowd.TryAttack(fan, three[0]));
                Assert.Equal(3 * perHit, fan.EquippedWeapon!.Wear);
            }
        }
    }

    [Fact]
    public void AStaffsCastIsAHit_AWandsCastIsItsHits()
    {
        // §1.3, "a cast is a hit": a staff's landed cast wears it once, on an ally
        // or an enemy alike — a heal and a curse (PHASE 6a DECISION 1).
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "staff_of_renewal");
        var renewal = a.EquippedWeapon!;
        var b = Char("B", 5, 6, "weakspot_stiletto");
        var c = Char("C", 6, 5, "staff_of_blight");
        var blight = c.EquippedWeapon!;
        var enemy = Enemy(5, 7);
        var turns = new TurnSystem(grid, new[] { a, b, c }, new[] { enemy }, () => 10);
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(1, renewal.Wear);
        Assert.True(turns.TryCast(c, enemy));
        Assert.Equal(1, blight.Wear);
        Assert.Equal(0, b.EquippedWeapon!.Wear);   // being healed wears nothing

        // A wand's Cast is not a hit: its hits are the bodies its shape strikes,
        // each worn as a swing's are. A Flaming Nova catching three: three wear.
        var w = Char("W", 5, 5, TestWeapons.Get("wand_of_the_nova", DamageType.Flaming));
        var wand = w.EquippedWeapon!;
        var ring = new[] { Enemy(5, 6), Enemy(6, 5), Enemy(4, 5) };
        var cast = new TurnSystem(grid, new[] { w }, ring, () => 10);
        Assert.Equal(3, cast.AreaTargets(w, (w.X, w.Y)).Count);
        Assert.True(cast.TryCastArea(w, (w.X, w.Y)));
        Assert.All(ring, e => Assert.True(e.Hp < 200));
        Assert.Equal(3, wand.Wear);

        // A wand whose first entry is no element — none instantiation makes, and
        // one only a hand-edited save could hold — casts nothing: the cast reads
        // its element off that entry, so it is refused before it is made.
        var soulless = new Weapon(GameContent.Current.Weapons["wand_of_the_nova"], wand.Forged, wand.Modifiers, [], 0);
        var s = Char("S", 5, 5, soulless);
        var dud = new TurnSystem(grid, new[] { s }, new[] { Enemy(5, 6) }, () => 10);
        Assert.False(dud.CanCastArea(s, (s.X, s.Y)));
        Assert.False(dud.TryCastArea(s, (s.X, s.Y)));
        Assert.Equal(0, soulless.Wear);
    }

    [Fact]
    public void NoEnemyWeaponEverWears()
    {
        // An enemy carries no pools, and wears nothing (§6.1, item 4): the roster
        // fixed its wear feed to nothing, so an exchange of blows — its strikes, and
        // a counter it takes — leaves its weapon as it was made, while the member's
        // counter wears the member's blade.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "riposte_blade");
        var enemy = Enemy(5, 6, "riposte_blade");
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        for (int swing = 0; swing < 3; swing++)
        {
            Hit(turns, enemy, a);
            Hit(turns, a, enemy);
            (a.X, a.Y) = At(5, 5);
            (enemy.X, enemy.Y) = At(5, 6);
        }
        Assert.Equal((0, 0), (enemy.Weapon.Wear, enemy.Weapon.WearCashed));
        Assert.True(a.EquippedWeapon!.Wear >= 3);

        // Its whole turn, walked and struck by the AI, and a caster's too: an enemy
        // Staff of Blight casting four times on the nearest member wears nothing.
        var caster = new EnemyState { X = 5 * Tile + 16, Y = 5 * Tile + 16, Weapon = TestWeapons.Get("staff_of_blight") };
        var near = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"), x: caster.X + 60f, y: caster.Y);
        var casting = new TurnSystem(new int[30, 30], new[] { near }, new[] { caster }, () => 10);
        casting.NotifyEnemyVisible(caster, true);
        casting.EndTurn();
        Advance(casting, 8f);
        Assert.True(near.StatusLevel(StatusEffectType.Poison) > 0);
        Assert.Equal((0, 0), (caster.Weapon.Wear, caster.Weapon.WearCashed));
        Assert.Equal(0, near.EquippedWeapon!.Wear);
    }

    [Fact]
    public void AFullWeaponBanksNothing_AndPlaysIdentically()
    {
        // S:794–799, "a fully worn weapon is not broken": nothing in combat reads
        // wear, so a full weapon hits for the same, costs the same and reaches as
        // far as a fresh copy of it, the same swing for the same roll — and banks
        // nothing more. The fresh one banks its hit.
        var grid = new int[20, 20];
        var fresh = TestWeapons.Get("weakspot_stiletto");
        var full = Worn(TestWeapons.Get("weakspot_stiletto"), wear: 30);
        Assert.True(full.WornFull);
        Assert.Equal((fresh.Damage, fresh.Range, fresh.ResolvedCost, fresh.Modifiers, fresh.Enchantments.Count),
            (full.Damage, full.Range, full.ResolvedCost, full.Modifiers, full.Enchantments.Count));

        // What a swing did, as values: the resolution (its riders as the statuses
        // they are, not the array holding them), the enemy's HP, the movement left
        // and the XP it taught.
        (string Resolution, int EnemyHp, float DistLeft, int Xp) Swing(Weapon weapon, int roll)
        {
            var a = Char("A", 5, 5, weapon);
            var enemy = Enemy(5, 6);
            var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => roll);
            AttackResolution? landed = null;
            turns.EnemyHit += (_, r) => landed = r;
            Assert.True(turns.TryAttack(a, enemy));
            var r = landed!.Value;
            string resolution = $"{r.Roll} dealt {r.Dealt} taken {r.Taken} blocked {r.Blocked} ward {r.WardSpent} damage {r.Damage} riders [{string.Join(", ", r.Riders)}]";
            return (resolution, enemy.Hp, a.DistLeft, a.WeaponXp[WeaponClass.Dagger]);
        }

        foreach (int roll in new[] { 10, 20, 1 })   // a plain hit, a crit, a fumble's halving
            Assert.Equal(Swing(TestWeapons.Get("weakspot_stiletto"), roll), Swing(Worn(TestWeapons.Get("weakspot_stiletto"), wear: 30), roll));

        var again = Worn(TestWeapons.Get("weakspot_stiletto"), wear: 30);
        Swing(again, 10);
        Assert.Equal(30, again.Wear);
        var once = TestWeapons.Get("weakspot_stiletto");
        Swing(once, 10);
        Assert.Equal(1, once.Wear);

        // Past a capacity a tuning has since lowered: still nothing banked, and none taken away.
        using (TestContent.UsePremise(tuning: t => t with { StartingPool = 20 }))
        {
            var over = Worn(TestWeapons.Get("weakspot_stiletto"), wear: 30);
            Assert.Equal((30, 20, true), (over.Wear, over.WearCapacity, over.WornFull));
            Swing(over, 10);
            Assert.Equal(30, over.Wear);
        }
    }

    [Fact]
    public void TheCrossingRaisesWornFullOnce()
    {
        // At the cap a weapon says so plainly (§6.1, item 9): the turn system
        // raises WeaponWornFull on the hit that fills it — the member and the
        // weapon — and never again while it stays full.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, Worn(TestWeapons.Get("weakspot_stiletto"), wear: 28));
        var knife = a.EquippedWeapon!;
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var full = new List<(PartyMemberState Member, Weapon Weapon, int Wear)>();
        turns.WeaponWornFull += (member, weapon) => full.Add((member, weapon, weapon.Wear));

        Hit(turns, a, enemy);
        Assert.Equal(29, knife.Wear);
        Assert.Empty(full);
        Hit(turns, a, enemy);
        Assert.Equal((a, knife, 30), Assert.Single(full));
        Hit(turns, a, enemy);
        Hit(turns, a, enemy);
        Assert.Equal(30, knife.Wear);
        Assert.Single(full);

        // A hit worth more than is left fills it to the cap exactly, and says so once.
        using (TestContent.UsePremise(tuning: t => t with { WearPerHit = 5 }))
        {
            var b = Char("B", 5, 5, Worn(TestWeapons.Get("weakspot_stiletto"), wear: 28));
            var dummy = Enemy(5, 6);
            var big = new TurnSystem(grid, new[] { b }, new[] { dummy }, () => 10);
            int said = 0;
            big.WeaponWornFull += (_, _) => said++;
            Hit(big, b, dummy);
            Assert.Equal((30, 1), (b.EquippedWeapon!.Wear, said));
        }

        // A weapon already over a lowered capacity crosses nothing.
        using (TestContent.UsePremise(tuning: t => t with { StartingPool = 20 }))
        {
            var c = Char("C", 5, 5, Worn(TestWeapons.Get("weakspot_stiletto"), wear: 25));
            var dummy = Enemy(5, 6);
            var lowered = new TurnSystem(grid, new[] { c }, new[] { dummy }, () => 10);
            int said = 0;
            lowered.WeaponWornFull += (_, _) => said++;
            Hit(lowered, c, dummy);
            Assert.Equal((25, 0), (c.EquippedWeapon!.Wear, said));
        }
    }

    [Fact]
    public void StatusTicksAndTheDeadBankNothing()
    {
        // A status's tick is no hit — it names no weapon — and an enchantment's
        // discharge rides the hit that already counted: the Staff of Blight's cast
        // wears it once, and the Poison it left ticking through the enemy's turn
        // end wears nothing more.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "staff_of_blight");
        var staff = a.EquippedWeapon!;
        var enemy = new EnemyState { X = a.X + 60f, Y = a.Y, Weapon = TestWeapons.Get("arming_sword") };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        Assert.True(turns.TryCast(a, enemy));
        Assert.Equal(1, staff.Wear);
        int ticks = 0;
        turns.ActorStatusTicked += (_, _) => ticks++;
        turns.EndTurn();
        Advance(turns, 6f);
        Assert.True(ticks > 0);
        Assert.Equal(1, staff.Wear);

        // A hit on the dead lands on nothing, and wears nothing.
        var b = Char("B", 5, 5, "weakspot_stiletto");
        var corpse = Enemy(5, 6);
        var after = new TurnSystem(grid, new[] { b }, new[] { corpse }, () => 10);
        corpse.Alive = false;
        Hit(after, b, corpse);
        Assert.Equal(0, b.EquippedWeapon!.Wear);
    }

    [Fact]
    public void AWipeKeepsTheWear()
    {
        // "A wipe forfeits spoils, not gear" (S:1431): the claw-back takes back a
        // share of the run's XP (§4.4), and wear is no XP pool — the weapons the
        // party wiped with come out of the dungeon as worn as they went down
        // (PHASE 6a DECISION 1).
        var campaign = CampaignRuns.Campaign();
        var visit = campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
        var turns = visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var a = campaign.Party[0];
        var knife = a.EquippedWeapon!;
        var dummy = visit.Current.Enemies[0];
        int xpAtTheDoor = a.WeaponXp[WeaponClass.Dagger];
        Hit(turns, a, dummy);
        Assert.Equal(1, knife.Wear);
        Assert.True(a.WeaponXp[WeaponClass.Dagger] > xpAtTheDoor);

        var result = CampaignRuns.Wipe(campaign, turns);
        Assert.Equal(RunOutcome.Death, result.Outcome);
        Assert.Contains(result.Clawbacks, clawback => clawback.MemberId == a.Id && clawback.Pool == nameof(WeaponClass.Dagger));
        Assert.Same(knife, a.Inventory[0]);
        Assert.Equal((1, 0), (knife.Wear, knife.WearCashed));
    }

    // ── One place, and no arrival ────────────────────────────────────────────

    [Fact]
    public void WearAccruesInOnePlace()
    {
        // §6.7: "the unified attack resolver is the single place it accrues". The
        // weapon's own mutator has one caller, Wear.Accrue; Wear.Accrue has one,
        // the wear feed the turn system's constructor fixes for a party member;
        // and the feeds are reached from the two hit appliers alone, through
        // WearHit — read off the game assembly's IL, so a second door anywhere
        // fails here.
        var game = typeof(Weapon).Assembly;
        static bool Same(MethodBase a, MethodBase b) => a.Module == b.Module && a.MetadataToken == b.MetadataToken;
        (Type? Root, string Name)[] CallersOf(MethodBase target)
            => Calls(game).Where(c => Same(c.Target, target)).Select(c => (Root(c.From.DeclaringType), c.From.Name)).Distinct().ToArray();

        var accrueWear = typeof(Weapon).GetMethod("AccrueWear", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var accrue = typeof(Wear).GetMethod(nameof(Wear.Accrue), BindingFlags.NonPublic | BindingFlags.Static)!;
        var wearHit = typeof(TurnSystem).GetMethod("WearHit", BindingFlags.NonPublic | BindingFlags.Instance)!;

        Assert.Equal([(typeof(Wear), nameof(Wear.Accrue))], CallersOf(accrueWear));
        var feed = Assert.Single(CallersOf(accrue));
        Assert.Equal(typeof(TurnSystem), feed.Root);
        Assert.Contains(".ctor", feed.Name, StringComparison.Ordinal);   // a lambda the constructor made: the roster's feed
        Assert.Equal(["ApplyCast", "ApplyDamage"], CallersOf(wearHit).Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.All(CallersOf(wearHit), c => Assert.Equal(typeof(TurnSystem), c.Root));
    }

    [Fact]
    public void NoArrivalCarriesWear()
    {
        // "You cannot enchant a weapon you have not fought with" (P6:30–32, item 4):
        // every weapon arrives unworn, on a fresh capacity — a dummy's drop, a
        // boss's drop, the weapons enemies hold, and the starting kit.
        void Unworn(Weapon weapon, string what)
            => Assert.True((0, 0, GameContent.Current.Tuning.StartingPool) == (weapon.Wear, weapon.WearCashed, weapon.WearCapacity),
                $"{what}: {weapon.Name} arrives worn {weapon.Wear}, cashed {weapon.WearCashed}, capacity {weapon.WearCapacity}");

        var start = CampaignState.Start(GameContent.Current);
        foreach (var member in start.Party)
            foreach (var weapon in member.Inventory.OfType<Weapon>())
                Unworn(weapon, "the starting kit");

        var visit = start.Enter("tutorial", CampaignRuns.GoldenSeed);
        foreach (var floor in visit.Floors)
            foreach (var row in floor.Enemies)
            {
                Unworn(row.Weapon, "an enemy's weapon");
                Unworn(LootTable.Roll(row, floor.Seed).Weapon, "a dummy's drop");
            }
        var boss = visit.Floors[^1].Boss!;
        Unworn(BossLoot.Roll(boss, visit.Floors[^1].Seed).Weapon, "the boss's drop");

        // Under another bar, the fresh capacity is that bar.
        using (TestContent.UsePremise(tuning: t => t with { StartingPool = 45 }))
            Unworn(TestWeapons.Get("longbow"), "a drop under StartingPool 45");
    }
}
