using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The attack-shape modifiers — what a swing <em>is</em>: Longshot pricing the
/// distance, Cleave fanning out, Charges capping the throws, Splitting and
/// Softening against Block, Pin's Mire on any hit — on both sides of the fight.
/// </summary>
public class AttackShapeTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        var ch = TestPools.Char(id, x: x, y: y);
        ch.Inventory[0] = TestWeapons.Get(weaponId);
        return ch;
    }

    private static PartyMemberState Member(Weapon? weapon, string id = "A")
    {
        var c = TestPools.Char(id);
        c.Inventory[0] = weapon;
        return c;
    }

    /// <summary>A dummy on a tile's centre, with enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

    /// <summary>The settled payload through a fresh compiled chain with no applier: the numbers alone, nothing written.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll, int distanceUnits)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits), attacker, defender);
    }

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    // ── Longshot ─────────────────────────────────────────────────────────────

    [Fact]
    public void Longshot_SpearOnlyPaysAtTheFourthTile()
    {
        // Longshot x1 on a four-tile spear: +1 per tile beyond three, so only
        // the final tile qualifies — 7 anywhere up to 96 surface units, 8 from
        // the first unit past them to full extension at 128.
        Assert.Equal(3, CombatRules.TilesSpanned(96));
        Assert.Equal(4, CombatRules.TilesSpanned(97));
        Assert.Equal(4, CombatRules.TilesSpanned(128));
        Assert.Equal(0, CombatRules.TilesSpanned(0));

        var pike = Member(TestWeapons.Get("skirmishers_pike"));   // 7, Brace x1, Longshot x1
        var target = Member(null, "B");
        Assert.Equal(7, Settle(pike, target, 10, distanceUnits: 0).Dealt);
        Assert.Equal(7, Settle(pike, target, 10, distanceUnits: 96).Dealt);
        Assert.Equal(8, Settle(pike, target, 10, distanceUnits: 97).Dealt);
        var reach = Settle(pike, target, 10, distanceUnits: 128);
        Assert.Equal(8, reach.WeaponShare);   // the distance is the weapon's own figure: it teaches the spear
        Assert.Equal(0, reach.EnchantmentShare);
        Assert.Equal(8, reach.Dealt);
        Assert.Equal(8, reach.Taken);

        // Through the turn system, the distance read off the map: at full
        // extension the poke pays, a step in and it does not.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "skirmishers_pike");
        var enemy = new EnemyState { X = a.X + 28f + 100f, Y = a.Y, Hp = 200 };   // surface 100: the fourth tile
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        Assert.Equal(100, CombatRules.SurfaceDistanceUnits(a, enemy));
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(200 - 5, enemy.Hp);   // 8 into Block 3

        enemy.X = a.X + 28f + 90f;   // surface 90: the third tile, nothing to pay
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(200 - 5 - 4, enemy.Hp);   // 7 into Block 3
    }

    [Fact]
    public void Longbow_AtTenTiles_Deals19_Crits38_BlockIgnored()
    {
        // The Longbow's Longshot x2 at ten tiles: 5 + (10 - 3) x 2 = 19, and a
        // crit multiplies the resolved number — distance included — to 38 with
        // Block ignored entirely. Crit damage is a function of where you stand.
        var archer = Member(TestWeapons.Get("longbow"));   // 5, Longshot x2, CritWindow x1: crits on 19+
        var shield = Member(null, "B");
        shield.Innate = ModifierSet.Of((Block, 1));

        var shot = Settle(archer, shield, 10, distanceUnits: 320);
        Assert.Equal(19, shot.WeaponShare);
        Assert.Equal(3, shot.Absorbed);
        Assert.Equal(16, shot.Dealt);

        var crit = Settle(archer, shield, 19, distanceUnits: 320);
        Assert.True(crit.IsCrit);
        Assert.Equal(38, crit.WeaponShare);
        Assert.Equal(0, crit.Absorbed);
        Assert.False(crit.Blocked);
        Assert.Equal(38, crit.Dealt);
        Assert.Equal(38, crit.Taken);

        // The natural-1 halving acts on the resolved number the same way: (5 + 14) / 2 = 9.
        var weak = Settle(archer, shield, 1, distanceUnits: 320);
        Assert.Equal(RollOutcome.Weak, weak.Outcome);
        Assert.Equal(9, weak.WeaponShare);

        // At knife range the bow is the 5 it looks like: the answer is a different weapon, not a stronger bow.
        Assert.Equal(5, Settle(archer, shield, 10, distanceUnits: 4).WeaponShare);
        Assert.Equal(10, Settle(archer, shield, 19, distanceUnits: 4).WeaponShare);

        // Through the turn system, ten tiles of floor away.
        var grid = new int[20, 30];
        var a = Char("A", 5, 2, "longbow");
        var enemy = new EnemyState { X = a.X + 28f + 320f, Y = a.Y, Hp = 200 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 19);
        Assert.Equal(320, CombatRules.SurfaceDistanceUnits(a, enemy));
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(RollOutcome.Crit, hit!.Value.Roll.Outcome);
        Assert.Equal(0, hit.Value.Blocked);
        Assert.Equal(200 - 38, enemy.Hp);
    }

    [Fact]
    public void Longshot_WorkedSpear_13AtReach_7InMelee()
    {
        // A spear worked to Longshot x6 — the forged x1 plus the five acquired
        // stacks of headroom — deals 13 at reach and 7 in melee: the farm turns
        // the rounding error into the whole reason to hold the distance.
        var pike = TestWeapons.Get("phalanx_spear");
        pike.Acquire(Longshot, 5);
        Assert.Equal(6, pike.Stacks(Longshot));
        var a = Member(pike);
        var target = Member(null, "B");

        Assert.Equal(13, Settle(a, target, 10, distanceUnits: 128).Dealt);
        Assert.Equal(7, Settle(a, target, 10, distanceUnits: 20).Dealt);
        Assert.Equal(7, Settle(a, target, 10, distanceUnits: 96).Dealt);

        // The cap: a sixth acquired stack lands on nothing.
        pike.Acquire(Longshot, 1);
        Assert.Equal(6, pike.Stacks(Longshot));
        Assert.Equal(13, Settle(a, target, 10, distanceUnits: 128).Dealt);

        // Innate stacks resolve with the weapon's: a wielder carrying one of its own pays one more per tile.
        a.Innate = ModifierSet.Of((Longshot, 1));
        Assert.Equal(14, Settle(a, target, 10, distanceUnits: 128).Dealt);
    }

    // ── Cleave and Rout ──────────────────────────────────────────────────────

    [Fact]
    public void Cleave_HitsExtraTargetsNearestFirst_PayingOnce()
    {
        // The Hatchet's Cleave x1 fans one swing out to one extra target: the
        // nearest other dummy in reach, whichever the swing was aimed at. The
        // movement is paid once, and the fanned hit carries the cleave mark.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "hatchet");   // reach 60, 18 damage, Cleave x1
        var e1 = Enemy(5, 6);   // 32 units off
        var e2 = Enemy(6, 6);   // 45 units off
        var e3 = Enemy(5, 7);   // 64 units off: surface 36, in reach
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2, e3 }, () => 10);
        var hits = new List<(ActorState Target, bool FromCleave)>();
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 9), "probe", (p, _, o) =>
        {
            hits.Add((o, p.FromCleave));
            return p;
        });

        Assert.True(turns.TryAttack(a, e3));   // aimed at the farthest

        Assert.Equal(new (ActorState, bool)[] { (e3, false), (e1, true) }, hits);
        Assert.Equal(200 - 15, e3.Hp);   // 18 into Block 3
        Assert.Equal(200 - 15, e1.Hp);
        Assert.Equal(200, e2.Hp);
        Assert.Equal(GameConstants.MaxDistance - a.EquippedWeapon!.ResolvedCost, a.DistLeft);   // 54, once
        Assert.Equal(1, turns.AttacksThisTurn(a));

        // Without Cleave the swing is one hit: a dagger beside the same crowd lands on its target alone.
        var b = Char("B", 5, 5, "weakspot_stiletto");
        var d1 = Enemy(5, 6);
        var d2 = Enemy(6, 5);
        var lone = new TurnSystem(grid, new[] { b }, new[] { d1, d2 }, () => 10);
        Assert.True(lone.TryAttack(b, d1));
        Assert.Equal(200 - 12, d1.Hp);
        Assert.Equal(200, d2.Hp);
    }

    [Fact]
    public void GreatAxe_TwoExtra()
    {
        // The Great Axe's Cleave x2 — two extra targets — takes three of the
        // four dummies around it: the aimed one and the two nearest others in
        // reach; the fourth stands a tile too far. Worked to x5 it takes five.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "great_axe");
        var e1 = Enemy(5, 6);
        var e2 = Enemy(6, 6);
        var e3 = Enemy(5, 7);
        var e4 = Enemy(5, 8);   // 96 off: surface 68, past the axe's 60
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2, e3, e4 }, () => 10);
        Assert.Equal(2, a.Value(Cleave));
        Assert.False(EnemyAi.CanHit(a, e4, a.EquippedWeapon!, grid));

        Assert.True(turns.TryAttack(a, e1));

        Assert.Equal(200 - 15, e1.Hp);
        Assert.Equal(200 - 15, e2.Hp);
        Assert.Equal(200 - 15, e3.Hp);
        Assert.Equal(200, e4.Hp);
        Assert.Equal(GameConstants.MaxDistance - 60, a.DistLeft);

        // Worked to x5, the same swing takes five more: every dummy in reach
        // here, the movement still paid once. It also crosses the axe's first
        // ladder step mid-swing (§2.2): the three bodies above and the first
        // four of these five come to 105 XP, past the 100 a neutral spread pays
        // for level 2, so the fifth body caught takes the +1 the level buys.
        // Asserted rather than hidden -- the proficiency bonus is part of the
        // weapon's own damage, so it feeds the very ladder that bought it.
        a.EquippedWeapon!.Acquire(Cleave, 3);
        Assert.Equal(5, a.Value(Cleave));
        var e5 = Enemy(4, 5);
        var e6 = Enemy(5, 4);
        var crowd = new TurnSystem(grid, new[] { a }, new[] { e1, e2, e3, e4, e5, e6 }, () => 10);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(crowd.TryAttack(a, e1));
        Assert.Equal(200 - 30, e1.Hp);
        Assert.Equal(200 - 30, e2.Hp);
        Assert.Equal(200 - 31, e3.Hp);   // the fifth body caught: 18 + 1 into Block 3
        Assert.Equal(200, e4.Hp);
        Assert.Equal(200 - 15, e5.Hp);
        Assert.Equal(200 - 15, e6.Hp);
        Assert.Equal(2, a.WeaponLevel(a.EquippedWeapon!));   // the swing that crossed the bar
        Assert.Equal(GameConstants.MaxDistance - 60, a.DistLeft);   // charged before it crossed, and floor(2 / 3) is nothing anyway
    }

    [Fact]
    public void Rout_DisplacesEveryTargetCaught()
    {
        // The Routing Axe's Rout x1 shoves everything its cleave caught a tile
        // away from the axe — the aimed target and the fanned one alike — and
        // one swing beside a spear line fires a brace for each body it drives
        // into the spear's reach.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "routing_axe");     // Cleave x1, Rout x1
        var s = Char("S", 5, 11, "phalanx_spear");   // Brace x2; its reach covers tile 7 of the row and the tile above it
        var e1 = Enemy(5, 6);   // aimed at: shoved east along the row
        var e2 = Enemy(4, 6);   // caught: a tie between the axes goes to the column, so shoved east as well
        var turns = new TurnSystem(grid, new[] { a, s }, new[] { e1, e2 }, () => 10);
        Assert.False(EnemyAi.CanHit(s, e1, s.EquippedWeapon!, grid));
        Assert.False(EnemyAi.CanHit(s, e2, s.EquippedWeapon!, grid));
        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += c => braced.Add(c);
        var displaced = new List<(ActorState Actor, int Tiles)>();
        turns.ActorDisplaced += (actor, tiles) => displaced.Add((actor, tiles));

        Assert.True(turns.TryAttack(a, e1));

        Assert.Equal(At(5, 7), (e1.X, e1.Y));
        Assert.Equal(At(4, 7), (e2.X, e2.Y));
        Assert.Equal(new (ActorState, int)[] { (e1, 1), (e2, 1) }, displaced);
        Assert.Equal(new[] { s, s }, braced);   // both were driven into the spear line
        Assert.Equal(200 - 15 - 5, e1.Hp);   // the axe's 18 and the spear's 7 + 1 at the fourth tile, each into Block 3
        Assert.Equal(200 - 15 - 5, e2.Hp);
        Assert.Equal(GameConstants.MaxDistance, s.DistLeft);
        Assert.Equal(GameConstants.MaxDistance - 60, a.DistLeft);
    }

    [Fact]
    public void EnemyAxe_Cleaves()
    {
        // Same core, other side: a Hatchet dummy's beat takes both members in
        // its reach, two beats out of its budget (100 / 160 x 54 = 33.75 each),
        // and the fanned hits carry the cleave mark.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var b = Char("B", 4, 6, "weakspot_stiletto");
        var axe = Enemy(5, 6, "hatchet");
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { axe }, () => 10);
        var hits = new List<(ActorState Target, bool FromCleave)>();
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 9), "probe", (p, _, o) =>
        {
            hits.Add((o, p.FromCleave));
            return p;
        });
        int characterHits = 0;
        turns.CharacterHit += (_, _) => characterHits++;

        turns.NotifyEnemyVisible(axe, true);
        turns.EndTurn();
        Advance(turns, 6f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(4, characterHits);
        Assert.Equal(new (ActorState, bool)[] { (a, false), (b, true), (a, false), (b, true) }, hits);
        Assert.Equal(TestPools.FixtureHp - 36, a.Hp);   // no shield on a dagger
        Assert.Equal(TestPools.FixtureHp - 36, b.Hp);
    }

    // ── Charges ──────────────────────────────────────────────────────────────

    [Fact]
    public void Charges_X1_PermitsTwoThrowsRefusesThird_X2_Three()
    {
        // Charges is a cap: Darts (Charges x1) permit exactly two throws a turn
        // and refuse a third with plenty of movement left; the Bandolier
        // (Charges x2) permits three. A new turn restores them.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "darts");   // cost 15 less Light: 13
        var enemy = Enemy(5, 8);            // 96 off: well inside 190
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        Assert.Equal(2, turns.AttacksPerTurn(a));

        Assert.True(turns.TryAttack(a, enemy));
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.MaxDistance - 2 * 13, a.DistLeft);
        Assert.False(turns.CanAttack(a, enemy));   // movement to spare, throws spent
        Assert.False(turns.TryAttack(a, enemy));
        Assert.Equal(2, turns.AttacksThisTurn(a));
        Assert.Equal(200 - 2 * 6, enemy.Hp);   // 9 into Block 3, twice

        // The next turn restores the throws. (The dummy sits the enemy phase out far from everyone and unseen.)
        (enemy.X, enemy.Y) = At(15, 15);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        (enemy.X, enemy.Y) = At(5, 8);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        Assert.Equal(0, turns.AttacksThisTurn(a));
        Assert.True(turns.CanAttack(a, enemy));

        // Charges x2: three throws, then no more.
        var b = Char("B", 5, 5, "bandolier");
        var dummy = Enemy(5, 8);
        var purity = new TurnSystem(grid, new[] { b }, new[] { dummy }, () => 10);
        Assert.Equal(3, purity.AttacksPerTurn(b));
        for (int i = 0; i < 3; i++)
            Assert.True(purity.TryAttack(b, dummy));
        Assert.False(purity.TryAttack(b, dummy));
        Assert.Equal(GameConstants.MaxDistance - 3 * 15, b.DistLeft);

        // Without Charges only the budget binds: a dagger swings until its movement is gone.
        var c = Char("C", 5, 5, "weakspot_stiletto");
        var target = Enemy(5, 6);
        var free = new TurnSystem(grid, new[] { c }, new[] { target }, () => 10);
        Assert.Null(free.AttacksPerTurn(c));
        int swings = 0;
        while (free.TryAttack(c, target)) swings++;
        Assert.Equal(5, swings);   // 160 / 30
        Assert.Equal(10f, c.DistLeft);
    }

    [Fact]
    public void EnemyThrower_Capped()
    {
        // A Darts dummy's beats are capped the same way: its 100-unit budget
        // would buy twelve throws at 100 / 160 x 13, and Charges x1 allows two.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");   // no shield on a dagger
        var thrower = Enemy(5, 8, "darts");
        var turns = new TurnSystem(grid, new[] { a }, new[] { thrower }, () => 10);
        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;

        turns.NotifyEnemyVisible(thrower, true);
        turns.EndTurn();
        Advance(turns, 8f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(2, hits);
        Assert.Equal(TestPools.FixtureHp - 18, a.Hp);

        // A Bandolier dummy: three.
        var b = Char("B", 5, 5, "weakspot_stiletto");
        var bandolier = Enemy(5, 8, "bandolier");
        var purity = new TurnSystem(grid, new[] { b }, new[] { bandolier }, () => 10);
        int hits2 = 0;
        purity.CharacterHit += (_, _) => hits2++;
        purity.NotifyEnemyVisible(bandolier, true);
        purity.EndTurn();
        Advance(purity, 8f);
        Assert.Equal(3, hits2);
        Assert.Equal(TestPools.FixtureHp - 27, b.Hp);
    }

    [Theory]
    [InlineData(1, 0, 2, 30, 18, 130)]
    [InlineData(2, 0, 3, 45, 27, 115)]
    [InlineData(3, 2, 6, 90, 54, 70)]    // x5
    [InlineData(3, 5, 9, 135, 81, 25)]   // x8: Feathered Death at its ceiling, the deepest Charges in the game
    public void Charges_LadderMovementSpent(int forged, int acquired, int throws, int spent, int damage, int left)
    {
        // The doc's ladder against a 160 budget at 15 a throw and 9 damage: the
        // per-stack value is chosen so the cap stays inside the budget at every
        // reachable stack count, the deepest spread (a unique's forged x3 plus
        // five acquired) landing at nine throws for 135.
        var knives = TestWeapons.Make("Knives", 190, 9, 15, manaCost: 0, WeaponClass.Throwing, (Charges, forged));
        knives.Acquire(Charges, acquired);
        Assert.Equal(forged + acquired, knives.Stacks(Charges));
        var grid = new int[20, 20];
        var (x, y) = At(5, 5);
        var a = TestPools.Char("A", x: x, y: y);
        a.Inventory[0] = knives;
        var enemy = new EnemyState { X = a.X + 96f, Y = a.Y, Weapon = TestWeapons.Make("Fists", 40, 1, 0), Hp = 1000 };   // no Block
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.Equal(throws, turns.AttacksPerTurn(a));
        int thrown = 0;
        while (turns.TryAttack(a, enemy)) thrown++;

        Assert.Equal(throws, thrown);
        Assert.Equal((float)spent, GameConstants.MaxDistance - a.DistLeft);
        Assert.Equal((float)left, a.DistLeft);
        Assert.Equal(1000 - damage, enemy.Hp);
    }

    // ── Splitting, Softening, Pin ────────────────────────────────────────────

    [Fact]
    public void Splitting_IgnoresThreeBlockPerStack_Reaver6()
    {
        // The Reaver's Splitting x2 ignores 6 of the target's Block, stack for
        // stack against the shield it counters: Block x1 and x2 are nothing to
        // it, Block x3 keeps 3, and worked to x7 it ignores 21 of the Bulwark's
        // 24. The minimum-1 and the crit skip are untouched.
        var reaver = Member(TestWeapons.Get("reaver"));   // 18, Splitting x2
        Assert.Equal(6, reaver.Value(Splitting));

        DamagePayload Against(int blockStacks, int roll = 10)
        {
            var shield = Member(null, "B");
            shield.Innate = ModifierSet.Of((Block, blockStacks));
            return Settle(reaver, shield, roll, distanceUnits: 4);
        }

        var one = Against(1);
        Assert.Equal((18, 0, false), (one.Dealt, one.Absorbed, one.Blocked));   // 3 - 6: nothing left to block with
        Assert.Equal(18, Against(2).Dealt);                                      // 6 - 6
        var three = Against(3);
        Assert.Equal((15, 3, true), (three.Dealt, three.Absorbed, three.Blocked));   // 9 - 6
        var bulwark = Against(8);
        Assert.Equal(17, bulwark.Absorbed);   // 24 - 6 = 18 would take it all: never below 1 of the weapon's share
        Assert.Equal(1, bulwark.Dealt);

        reaver.EquippedWeapon!.Acquire(Splitting, 5);   // x7: 21 ignored
        Assert.Equal(21, reaver.Value(Splitting));
        var worked = Against(8);
        Assert.Equal(3, worked.Absorbed);
        Assert.Equal(15, worked.Dealt);
        var crit = Against(8, roll: 20);
        Assert.Equal(0, crit.Absorbed);   // a crit skips Block entirely, split or not
        Assert.Equal(36, crit.Dealt);

        // Splitting lets *your* weapon through: beside the Reaver, a dagger meets the Tower Guard's full Block 6.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "reaver");
        var b = Char("B", 5, 7, "weakspot_stiletto");
        var guard = Enemy(5, 6, "tower_guard");   // Block x2: 6 absorbed
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { guard }, () => 10);
        Assert.True(turns.TryAttack(a, guard));
        Assert.Equal(200 - 18, guard.Hp);   // the Reaver's 18 in full
        Assert.True(turns.TryAttack(b, guard));
        Assert.Equal(200 - 18 - 9, guard.Hp);   // the dagger's 15 into the same Block 6
    }

    [Fact]
    public void Softening_StripsForOneRound_ForEveryone()
    {
        // A Softening Javelin hit strips 3 of the target's Block for the round
        // — for everyone: the javelin itself is blocked as usual, the dagger
        // after it goes through unblocked, and once the round ends the Block
        // is back. Same per-stack value as Splitting, opposite beneficiary.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "softening_javelins");   // 9, Softening x1, Charges x1
        var b = Char("B", 5, 7, "weakspot_stiletto");    // 15, nothing of its own against Block
        var enemy = Enemy(5, 6);                         // Arming Sword: Block x1
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        var hits = new List<AttackResolution>();
        turns.EnemyHit += (_, r) => hits.Add(r);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(3, hits[^1].Blocked);   // the marking throw meets the Block it is about to strip
        Assert.Equal(200 - 6, enemy.Hp);
        Assert.Equal(3, enemy.StatusLevel(Softened));
        Assert.Equal(0, CombatBehaviours.BlockAgainst(b, enemy));

        Assert.True(turns.TryAttack(b, enemy));
        Assert.Equal(0, hits[^1].Blocked);   // B's dagger goes through: everyone's swing does
        Assert.Equal(200 - 6 - 15, enemy.Hp);

        Assert.True(turns.TryAttack(a, enemy));   // the second throw lands on a stripped shield and marks it again
        Assert.Equal(0, hits[^1].Blocked);
        Assert.Equal(200 - 6 - 15 - 9, enemy.Hp);
        Assert.Equal(6, enemy.StatusLevel(Softened));

        // The round ends (the dummy sits the enemy phase out far from everyone and unseen): the status resets whole and the Block is back.
        (enemy.X, enemy.Y) = At(15, 15);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(0, enemy.StatusLevel(Softened));
        (enemy.X, enemy.Y) = At(5, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        Assert.True(turns.TryAttack(b, enemy));
        Assert.Equal(3, hits[^1].Blocked);
        Assert.Equal(200 - 6 - 15 - 9 - 12, enemy.Hp);

        // Stacks are levels: Softening x3 strips 9 — the Tower Guard's 6 and then some, never below zero.
        var c = Char("C", 5, 5, "softening_javelins");
        c.EquippedWeapon!.Acquire(Softening, 2);
        var guard = Enemy(5, 6, "tower_guard");
        var deep = new TurnSystem(grid, new[] { c }, new[] { guard }, () => 10);
        Assert.True(deep.TryAttack(c, guard));
        Assert.Equal(9, guard.StatusLevel(Softened));
        Assert.Equal(0, CombatBehaviours.BlockAgainst(c, guard));
    }

    [Fact]
    public void Pin_AppliesMirePerStack_OnHitAndOnBrace()
    {
        // The Pinning Bow's hit lands a Mire level per stack — the existing
        // Mire, accumulating — and the Pinning Lance's brace lands the same on
        // whatever walks or is driven into its reach: the two halves of the
        // kiting pair, delivered by a hit and by a brace.
        var grid = new int[20, 20];
        var a = Char("A", 5, 2, "pinning_bow");   // Pin x1, four tiles from the dummy
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(1, enemy.StatusLevel(Mire));
        Assert.Equal(new StatusApplication(Mire, null, 1), Assert.Single(hit!.Value.Riders));
        Assert.Equal(200 - 3, enemy.Hp);   // 5 + 1 at the fourth tile, into Block 3: the pin rides the hit and changes nothing about it

        a.EquippedWeapon!.Acquire(Pin, 2);   // x3
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(4, enemy.StatusLevel(Mire));
        Assert.Equal(200 - 6, enemy.Hp);
        Assert.Equal(60f, StatusBehaviours.MiredBudget(enemy, GameConstants.EnemyMove));   // four levels: 40% of its budget gone

        // The lance's brace: B's sword drives the dummy into L's reach, and the poke pins it.
        var b = Char("B", 5, 5, "tower_guard");
        var l = Char("L", 5, 11, "pinning_lance");   // reach covers tiles 7..10 of the row
        var walker = Enemy(5, 6);
        var line = new TurnSystem(new int[20, 20], new[] { b, l }, new[] { walker }, () => 10);
        int braces = 0;
        line.BraceTriggered += _ => braces++;
        Assert.True(line.TryAttack(b, walker));
        Assert.Equal(At(5, 7), (walker.X, walker.Y));
        Assert.Equal(1, braces);
        Assert.Equal(1, walker.StatusLevel(Mire));
        Assert.Equal(200 - 7 - 5, walker.Hp);   // the sword's 10 and the lance's 7 + 1 at the fourth tile, each into Block 3

        // And a seen lance dummy pins a member walking into its reach.
        var c = Char("C", 5, 5, "weakspot_stiletto");
        var lancer = new EnemyState { X = c.X + 200f, Y = c.Y, Weapon = TestWeapons.Get("pinning_lance") };
        var enemyLine = new TurnSystem(new int[20, 20], new[] { c }, new[] { lancer }, () => 10);
        enemyLine.NotifyEnemyVisible(lancer, true);
        c.X = lancer.X - 150f;
        enemyLine.NotifyCharacterMoved(c);
        Assert.Equal(1, c.StatusLevel(Mire));
        Assert.Equal(TestPools.FixtureHp - 8, c.Hp);   // 7 + 1 at the fourth tile, no shield on a dagger
        Assert.Equal(GameConstants.MaxDistance * 90 / 100, StatusBehaviours.MiredBudget(c, GameConstants.MaxDistance));
    }
}
