using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2 displacement: Push, Drag and Rout move a target tile by tile through
/// the same move path a walk takes, so every threat zone crossed fires on the
/// way — and the per-turn brace budget is what stops the one cascade in the
/// design.
/// </summary>
public class DisplacementTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        var ch = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        ch.Inventory[0] = TestWeapons.Get(weaponId);
        return ch;
    }

    /// <summary>A dummy on a tile's centre, with enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

    private static (int R, int C) TileOf(ActorState actor) => Displacer.TileOf(actor);

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void SwordHit_PushesOneTilePerStack_AlongDominantAxis()
    {
        // The Tower Guard's Push x1: a hit shoves the dummy one tile straight
        // back along the attacker-to-target axis, set down on the tile's centre.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var displaced = new List<(ActorState Actor, int Tiles)>();
        turns.ActorDisplaced += (actor, tiles) => displaced.Add((actor, tiles));

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal((5, 7), TileOf(enemy));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
        Assert.Equal(200 - 7, enemy.Hp);   // 10 into Block 3: the shove rides the hit and changes nothing about it
        var (who, tiles) = Assert.Single(displaced);
        Assert.Same(enemy, who);
        Assert.Equal(1, tiles);

        // Mostly sideways, a little up: the dominant axis wins and the step is 4-directional.
        enemy.X = At(5, 6).X;
        enemy.Y = At(5, 6).Y - 10f;
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));

        // Below rather than beside: the shove goes down the column.
        (enemy.X, enemy.Y) = At(6, 5);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(7, 5), (enemy.X, enemy.Y));

        // A tile per stack: Push x3 is three tiles, in one shove.
        a.EquippedWeapon!.Acquire(Push, 2);
        Assert.Equal(3, a.Value(Push));
        (enemy.X, enemy.Y) = At(5, 6);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 9), (enemy.X, enemy.Y));
        Assert.Equal(3, displaced[^1].Tiles);
    }

    [Fact]
    public void Halberd_PushesOnBrace()
    {
        // A braced hit shoves the target back out of the spear's own reach and
        // ends its walk: the Halberd breaks contact.
        var grid = new int[20, 30];
        var a = Char("A", 5, 5, "halberd");
        var enemy = new EnemyState { X = a.X + 250f, Y = a.Y };   // outside reach (128 + 28 = 156); its 100-unit approach crosses in
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        int braces = 0, hits = 0;
        turns.BraceTriggered += _ => braces++;
        turns.EnemyHit += (_, _) => hits++;

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(1, braces);
        Assert.Equal(1, hits);
        Assert.Equal(GameConstants.DummyHp - 4, enemy.Hp);   // 7 into Block 3
        // It crossed into reach on tile 10 of the row and was set down on tile 11: out of reach again.
        Assert.Equal((5, 11), TileOf(enemy));
        Assert.Equal(At(5, 11), (enemy.X, enemy.Y));
        Assert.False(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));
    }

    [Fact]
    public void Drag_PullsTowardThrower_NeverOntoIt()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "harpoon");   // range 190, Drag x1
        var enemy = Enemy(5, 9);              // four tiles off: 100 surface units
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 8), (enemy.X, enemy.Y));   // one tile toward the thrower
        Assert.Equal(200 - 6, enemy.Hp);              // 9 into Block 3

        // Drag x5 from three tiles off: it stops on the tile beside the thrower, never on top.
        a.EquippedWeapon!.Acquire(Drag, 4);
        var tiles = new List<int>();
        turns.ActorDisplaced += (_, n) => tiles.Add(n);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 6), (enemy.X, enemy.Y));
        Assert.Equal(new[] { 2 }, tiles);

        // Adjacent already: nothing to pull it onto, and no event for a shove that moved nothing.
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 6), (enemy.X, enemy.Y));
        Assert.Equal(new[] { 2 }, tiles);
    }

    [Fact]
    public void Rout_PushesEveryCleaveTarget()
    {
        // The handler alone, FromCleave set by hand: Rout is Push applied to
        // everything a cleave caught, the primary target included, a tile per
        // stack away from the axe. (The cleave that fans the hits out is
        // sub-step 6's; this pins what each fanned hit settles.)
        var a = Char("A", 5, 5, "routing_axe");   // Rout x1
        var enemy = Enemy(5, 6);
        var caught = DamagePayload.Initial(a.EquippedWeapon!, roll: 10, distanceUnits: 4) with { FromCleave = true };

        var settled = DisplacementBehaviours.Rout(caught, a, enemy);
        Assert.Equal(new Displacement(1, 0, 1), settled.Displace);
        Assert.True(settled.FromCleave);

        var primary = DisplacementBehaviours.Rout(caught with { FromCleave = false }, a, enemy);
        Assert.Equal(new Displacement(1, 0, 1), primary.Displace);

        // Only the axe's own modifier: Push and Drag settle nothing for it, and Rout nothing for a sword.
        Assert.Null(DisplacementBehaviours.Push(caught, a, enemy).Displace);
        Assert.Null(DisplacementBehaviours.Drag(caught, a, enemy).Displace);
        var sword = Char("B", 5, 5, "tower_guard");
        Assert.Null(DisplacementBehaviours.Rout(DamagePayload.Initial(sword.EquippedWeapon!, 10, 4), sword, enemy).Displace);

        // Rout x2 through the whole chain and the applier: the primary target is shoved two tiles like any cleave target would be.
        a.EquippedWeapon!.Acquire(Rout, 1);
        var turns = new TurnSystem(new int[20, 20], new[] { a }, new[] { enemy }, () => 10);
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 8), (enemy.X, enemy.Y));
        Assert.Equal(200 - 15, enemy.Hp);   // 18 into Block 3
    }

    [Fact]
    public void StopsAtWall_AndAtOccupiedTile_NoOverlap()
    {
        var grid = new int[20, 20];
        grid[5, 8] = 1;                          // a wall two tiles behind the dummy
        var a = Char("A", 5, 5, "tower_guard");
        a.EquippedWeapon!.Acquire(Push, 2);      // Push x3
        var enemy = Enemy(5, 6);
        var bystander = Enemy(7, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy, bystander }, () => 10);
        var tiles = new List<int>();
        turns.ActorDisplaced += (_, n) => tiles.Add(n);

        // Three tiles asked for, one taken: the wall stops it short, and it is never inside the wall.
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
        Assert.Equal(new[] { 1 }, tiles);

        // An occupied tile stops it the same way, and nobody overlaps: shoved down the column into the bystander, it stays put.
        (enemy.X, enemy.Y) = At(6, 6);
        (a.X, a.Y) = At(5, 6);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(6, 6), (enemy.X, enemy.Y));
        Assert.Equal(At(7, 6), (bystander.X, bystander.Y));
        Assert.Equal(new[] { 1 }, tiles);   // no event for a shove that moved nothing

        // The edge of the map is a wall too.
        (a.X, a.Y) = At(5, 18);
        (enemy.X, enemy.Y) = At(5, 19);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 19), (enemy.X, enemy.Y));

        // The tile test itself: the edge, a wall and an occupied tile each answer null; a free tile is the next step.
        var shove = new Displacement(1, 0, 1);
        Assert.Null(Displacer.NextTile(enemy, shove, grid, new HashSet<(int, int)>()));                  // off the edge
        (enemy.X, enemy.Y) = At(5, 7);
        Assert.Null(Displacer.NextTile(enemy, shove, grid, new HashSet<(int, int)>()));                  // into the wall at (5,8)
        (enemy.X, enemy.Y) = At(5, 5);
        Assert.Null(Displacer.NextTile(enemy, shove, grid, new HashSet<(int, int)> { (5, 6) }));        // into someone
        Assert.Equal((5, 6), Displacer.NextTile(enemy, shove, grid, new HashSet<(int, int)>())!.Value);
    }

    [Fact]
    public void PushIntoSpearReach_FiresBrace()
    {
        // Sword shoves, spear pokes: A drives the dummy a tile back into S's
        // reach, and S gets a free attack it spent no movement on, on the
        // party's own turn.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        var s = Char("S", 5, 11, "skirmishers_pike");   // reach covers tiles 7..10 of the row: the dummy on 6 is 132 surface units off
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, s }, new[] { enemy }, () => 10);
        Assert.False(EnemyAi.CanHit(s, enemy, s.EquippedWeapon!, grid));
        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += c => braced.Add(c);
        int hits = 0;
        turns.EnemyHit += (_, _) => hits++;

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
        Assert.Equal(new[] { s }, braced);
        Assert.Equal(2, hits);
        Assert.Equal(200 - 7 - 4, enemy.Hp);   // the sword's 10 and the spear's 7, each into Block 3
        Assert.Equal(GameConstants.MaxDistance, s.DistLeft);
        Assert.Equal(GameConstants.MaxDistance - a.EquippedWeapon!.ResolvedCost, a.DistLeft);
    }

    [Fact]
    public void ThroughTwoZones_FiresBoth()
    {
        // Push x2 carries the dummy through S1's reach (tiles 7..10) and into
        // S2's (8..11): displaced through two zones on one shove, both fire.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        a.EquippedWeapon!.Acquire(Push, 1);
        var s1 = Char("S1", 5, 11, "skirmishers_pike");
        var s2 = Char("S2", 5, 12, "skirmishers_pike");
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, s1, s2 }, new[] { enemy }, () => 10);
        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += c => braced.Add(c);

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(At(5, 8), (enemy.X, enemy.Y));
        Assert.Equal(new[] { s1, s2 }, braced);
        Assert.Equal(200 - 7 - 4 - 4, enemy.Hp);
    }

    [Fact]
    public void PushedFurtherAway_DoesNotFire()
    {
        // S's reach covers tiles 3..6 of the row. Already inside and shoved
        // deeper (4 to 5): no entry. Inside at the edge and shoved out (6 to 7):
        // leaving, not entering. Neither is a brace.
        var grid = new int[20, 20];
        var s = Char("S", 5, 2, "skirmishers_pike");
        var a = Char("A", 5, 3, "tower_guard");
        var enemy = Enemy(5, 4);
        var turns = new TurnSystem(grid, new[] { s, a }, new[] { enemy }, () => 10);
        int braces = 0;
        turns.BraceTriggered += _ => braces++;

        Assert.True(EnemyAi.CanHit(s, enemy, s.EquippedWeapon!, grid));
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 5), (enemy.X, enemy.Y));
        Assert.True(EnemyAi.CanHit(s, enemy, s.EquippedWeapon!, grid));   // still inside
        Assert.Equal(0, braces);

        (a.X, a.Y) = At(5, 5);
        (enemy.X, enemy.Y) = At(5, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);   // set down at the edge of S's reach, still inside it
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
        Assert.False(EnemyAi.CanHit(s, enemy, s.EquippedWeapon!, grid));  // shoved out
        Assert.Equal(0, braces);
    }

    [Fact]
    public void AllyShove_DoesNotFireOwnSideBrace()
    {
        // Threat zones only face the other side. A dummy's sword shoves A into
        // S's reach: S is A's ally and does not brace. A's sword shoves a dummy
        // into a spear dummy's reach: that spear is the dummy's ally and does
        // not brace either.
        var grid = new int[20, 20];
        var s = Char("S", 5, 0, "skirmishers_pike");   // reach covers tiles 1..4
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var sword = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { s, a }, new[] { sword }, () => 10);
        turns.NotifyEnemyVisible(sword, true);
        int braces = 0;
        turns.BraceTriggered += _ => braces++;
        turns.EnemyBraceTriggered += _ => braces++;

        CombatRules.Resolve(turns.Events, sword, a, sword.Weapon, CombatRules.SurfaceDistanceUnits(sword, a), () => 10);
        Assert.Equal(At(5, 4), (a.X, a.Y));
        Assert.True(EnemyAi.CanHit(s, a, s.EquippedWeapon!, grid));
        Assert.Equal(0, braces);
        Assert.Equal(GameConstants.PlayerHp - 10, a.Hp);

        var grid2 = new int[20, 20];
        var b = Char("B", 5, 5, "tower_guard");
        var dummy = Enemy(5, 6);
        var spear = Enemy(5, 11, "skirmishers_pike");   // reach covers tiles 7..10
        var turns2 = new TurnSystem(grid2, new[] { b }, new[] { dummy, spear }, () => 10);
        turns2.NotifyEnemyVisible(dummy, true);
        turns2.NotifyEnemyVisible(spear, true);
        turns2.BraceTriggered += _ => braces++;
        turns2.EnemyBraceTriggered += _ => braces++;

        Assert.True(turns2.TryAttack(b, dummy));
        Assert.Equal(At(5, 7), (dummy.X, dummy.Y));
        Assert.True(EnemyAi.CanHit(spear, dummy, spear.Weapon, grid2));
        Assert.Equal(0, braces);
        Assert.Equal(200 - 7, dummy.Hp);
    }

    [Fact]
    public void ShoveTriggeredBrace_SpendsAUse()
    {
        // The brace a shove triggers comes out of the same per-turn pool a walk
        // would draw on: S (Brace x1) pokes the first dummy shoved in, not the
        // second, and is restored the next turn.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        var s = Char("S", 5, 11, "skirmishers_pike");   // reach covers tiles 7..10
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, s }, new[] { enemy }, () => 10);
        int braces = 0;
        turns.BraceTriggered += _ => braces++;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(1, braces);

        // Back out of reach (a forced move: the pair is released), then shoved in again: the use is spent.
        (enemy.X, enemy.Y) = At(5, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        a.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));
        Assert.Equal(1, braces);

        // A new turn restores it. The dummy sits the enemy phase out far from everyone and unseen.
        (enemy.X, enemy.Y) = At(15, 15);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        (enemy.X, enemy.Y) = At(5, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(2, braces);
    }

    [Fact]
    public void HalberdChain_TerminatesWhenBracePoolEmpties_UnderMaxCascadeDepth()
    {
        // The one cascade in the design. Two Halberds (Brace x3 each once
        // acquired, Push x1) face each other down a row: H1's reach covers
        // tiles 3..6, H2's 7..10. A's sword shoves the dummy from 6 to 7 — into
        // H2's reach — and H2's brace shoves it back to 6, out of H2's reach
        // and into H1's, whose brace shoves it to 7 again. Every link spends a
        // brace use, so the ping-pong runs exactly six links and stops on its
        // own, nowhere near the dispatcher's depth guard.
        var grid = new int[20, 30];
        var h1 = Char("H1", 5, 2, "halberd");
        var h2 = Char("H2", 5, 11, "halberd");
        h1.EquippedWeapon!.Acquire(Brace, 2);
        h2.EquippedWeapon!.Acquire(Brace, 2);
        var a = Char("A", 5, 5, "tower_guard");
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { h1, h2, a }, new[] { enemy }, () => 10);
        var braced = new List<string>();
        turns.BraceTriggered += c => braced.Add(c.Id);
        int hits = 0;
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 9), "probe", (p, _, _) => { hits++; return p; });

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(new[] { "H2", "H1", "H2", "H1", "H2", "H1" }, braced);
        Assert.Equal(7, hits);   // the sword and six braces
        Assert.Equal(200 - 7 - 6 * 4, enemy.Hp);
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));   // the last link, H1's, left it in H2's exhausted reach
        Assert.True(enemy.Alive);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemySword_ShovesCharacterIntoEnemySpearReach()
    {
        // It runs both ways: a sword dummy shoves A a tile west, into a spear
        // dummy's reach (tiles 1..4 of the row), and the spear dummy braces.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var sword = Enemy(5, 6);
        var spear = Enemy(5, 0, "skirmishers_pike");
        var turns = new TurnSystem(grid, new[] { a }, new[] { sword, spear }, () => 10);
        turns.NotifyEnemyVisible(sword, true);
        turns.NotifyEnemyVisible(spear, true);
        var braced = new List<EnemyState>();
        turns.EnemyBraceTriggered += e => braced.Add(e);
        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        Assert.False(EnemyAi.CanHit(spear, a, spear.Weapon, grid));

        CombatRules.Resolve(turns.Events, sword, a, sword.Weapon, CombatRules.SurfaceDistanceUnits(sword, a), () => 10);

        Assert.Equal(At(5, 4), (a.X, a.Y));
        Assert.Equal(new[] { spear }, braced);
        Assert.Equal(2, hits);
        Assert.Equal(GameConstants.PlayerHp - 10 - 7, a.Hp);   // no shield on a dagger: the sword's 10 and the spear's 7 in full
    }
}
