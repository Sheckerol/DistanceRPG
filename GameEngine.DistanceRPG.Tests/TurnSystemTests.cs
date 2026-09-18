using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class TurnSystemTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, string weaponId = "weakspot_stiletto")
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = TestWeapons.Get(weaponId);
        return c;
    }

    /// <summary>Advance the state machine in small steps.</summary>
    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void EndTurn_BanksHalfUnspentMovement_AndRefillsNextTurn()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        a.DistLeft = 100f; // 60 spent
        float banked = -1f;
        turns.TurnEnded += saved => banked = saved;

        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.Equal(50f, banked); // floor(100/2)

        Advance(turns, 3f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(1, turns.TurnCount);
        Assert.Equal(GameConstants.MaxDistance + 50f, a.EffectiveMax);
        Assert.Equal(a.EffectiveMax, a.DistLeft);
    }

    [Fact]
    public void EndTurn_BankIsCappedAtHalfBaseBudget()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.DistLeft = 300f; // from a previous bonus turn
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float banked = -1f;
        turns.TurnEnded += saved => banked = saved;
        turns.EndTurn();

        Assert.Equal(GameConstants.MaxDistance / 2f, banked);
    }

    [Fact]
    public void TryAttack_SpendsCost_DamagesEnemy_AndRespectsRules()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, weaponId: "weakspot_stiletto"); // dagger: dmg 15, cost 30, range 40
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile }; // 50px away, surface 22 â‰¤ 40
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        AttackResolution? res = null;
        turns.EnemyHit += (_, r) => res = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.MaxDistance - a.EquippedWeapon!.ResolvedCost, a.DistLeft);   // the stiletto's 30: no Light
        Assert.NotNull(res);
        // Dagger 15 into sword block 3 â†’ 12
        Assert.Equal(12, res.Value.Damage);
        Assert.Equal(50 - 12, enemy.Hp);
    }

    [Fact]
    public void TryAttack_FailsWithoutBudgetOrRangeOrLos()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        a.DistLeft = a.EquippedWeapon!.ResolvedCost - 1; // one short of the dagger's 30
        Assert.False(turns.TryAttack(a, enemy));

        a.DistLeft = 160f;
        enemy.X = 15 * Tile; // out of range
        Assert.False(turns.TryAttack(a, enemy));

        // In range (spear) but a wall tile sits between attacker and enemy.
        a.Inventory[0] = TestWeapons.Get("skirmishers_pike"); // spear, range 128
        enemy.X = 7 * Tile + 16f;                             // 80px away, surface 52: within reach
        grid[5, 6] = 1;                            // wall in the middle column
        Assert.False(turns.TryAttack(a, enemy));

        grid[5, 6] = 0; // clear it: same shot now lands
        Assert.True(turns.TryAttack(a, enemy));
    }

    [Fact]
    public void KillingEnemy_MarksDefeat_AndResurrectsAfterConfiguredTurns()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Hp = 10 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        bool defeated = false, resurrected = false;
        turns.EnemyDefeated += _ => defeated = true;
        turns.EnemyResurrected += _ => resurrected = true;

        Assert.True(turns.TryAttack(a, enemy)); // 12 â‰¥ 10 HP
        Assert.True(defeated);
        Assert.False(enemy.Alive);
        Assert.Equal(0, enemy.DefeatedAtTurn);

        // The configured number of end-turn cycles later, the dummy comes back.
        for (int i = 0; i < GameConstants.DummyResurrectTurns; i++)
        {
            Assert.False(resurrected);
            turns.EndTurn();
            Advance(turns, 3f);
        }

        Assert.True(resurrected);
        Assert.True(enemy.Alive);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    [Fact]
    public void EnemyTurn_WhenSeenAndAdjacent_AttacksThreeTimesWithSword()
    {
        var grid = new int[20, 20];
        grid[5, 4] = 1; // a wall directly behind A: the sword's Push x1 has nowhere to shove it, so every beat lands in reach
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto"); // dagger defender: no block; on its tile's centre so the wall does not touch the sight line
        var enemy = new EnemyState { X = 5 * Tile + 16 + 60f, Y = 5 * Tile + 16 }; // sword range
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(enemy, true);

        turns.EndTurn();
        Advance(turns, 6f);

        // Budget 100, scaled sword cost 100/160*50 = 31.25 â†’ 3 attacks of 10.
        Assert.Equal(3, hits);
        Assert.Equal(GameConstants.PlayerHp - 30, a.Hp);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemyTurn_UnseenForTwoTurns_DoesNotMove()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 25 * Tile, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float startX = enemy.X;
        turns.EndTurn(); // never seen: TurnsSinceSeen 2 â†’ 3
        Advance(turns, 4f);

        Assert.Equal(startX, enemy.X);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemyTurn_WhenSeen_MovesTowardTarget()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile + 16, 5 * Tile + 16);
        var enemy = new EnemyState { X = 25 * Tile + 16, Y = 5 * Tile + 16 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float startX = enemy.X;
        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        // Moved its full 100-unit budget toward the character (straight line).
        Assert.Equal(startX - GameConstants.EnemyMove, enemy.X, 1);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void Brace_TriggersWhenEnemyWalksIntoSpearRange()
    {
        var grid = new int[20, 30];
        // Spear char; enemy 250px away: outside spear reach (128+28=156),
        // after its 100px approach it lands at 150px â†’ inside reach.
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponId: "skirmishers_pike");
        var enemy = new EnemyState { X = a.X + 250f, Y = a.Y };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        PartyMemberState? braced = null;
        int enemyHits = 0;
        turns.BraceTriggered += c => braced = c;
        turns.EnemyHit += (_, _) => enemyHits++;

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Same(a, braced);
        Assert.Equal(1, enemyHits); // the free brace attack landed
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void Brace_FiresFromBackRowSpears_WhenEnemyChasesTheLeader()
    {
        var grid = new int[20, 30];
        // Marching-line party, dagger leader nearest to the enemy: the enemy
        // targets the leader, but walking in crosses the back row's spear
        // reach. (The prototype only braced the enemy's own target, which a
        // formation's leader always eats — brace is a threat zone now.)
        var a = Char("A", 8 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto");
        var b = Char("B", 7 * Tile + 16, 5 * Tile + 16, weaponId: "tower_guard");
        var c = Char("C", 6 * Tile + 16, 5 * Tile + 16, weaponId: "skirmishers_pike");
        var d = Char("D", 5 * Tile + 16, 5 * Tile + 16, weaponId: "skirmishers_pike");
        var enemy = new EnemyState { X = a.X + 250f, Y = a.Y };
        var turns = new TurnSystem(grid, new[] { a, b, c, d }, new[] { enemy }, () => 10);

        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += ch => braced.Add(ch);

        // Two approach turns: the 100-unit budget leaves the enemy 150px from
        // the leader after the first; the second walks it into sword range of
        // A — crossing into C's spear reach on the way. D sits one tile
        // farther and stays out of reach.
        for (int i = 0; i < 2; i++)
        {
            turns.NotifyEnemyVisible(enemy, true);
            turns.EndTurn();
            Advance(turns, 10f);
        }

        Assert.Contains(c, braced);
        Assert.DoesNotContain(d, braced);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void Brace_UsesPerTurn_MatchTheWeaponsBraceValue()
    {
        var grid = new int[20, 40];
        // One char with a Brace-2 pike, one with the standard Brace-1 spear,
        // side by side; two enemies walk in from the same direction in one
        // turn. The pike retaliates against both, the spear only the first.
        var p = Char("P", 5 * Tile + 16, 5 * Tile + 16, weaponId: "phalanx_spear"); // Brace x2
        var s = Char("S", 5 * Tile + 16, 6 * Tile + 16, weaponId: "skirmishers_pike"); // spear

        var e1 = new EnemyState { X = p.X + 250f, Y = p.Y };
        var e2 = new EnemyState { X = p.X + 250f, Y = s.Y };
        var turns = new TurnSystem(grid, new[] { p, s }, new[] { e1, e2 }, () => 10);

        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += ch => braced.Add(ch);
        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);
        turns.EndTurn();
        Advance(turns, 15f);

        Assert.Equal(2, braced.Count(ch => ch == p)); // pike: both walkers
        Assert.Equal(1, braced.Count(ch => ch == s)); // spear: first only
    }

    [Fact]
    public void Brace_FiresFromEveryZoneCrossed_NotJustWhereTheWalkEnds()
    {
        var grid = new int[20, 30];
        // Dagger leader draws the enemy along row 5. S sits perpendicular to
        // the walk's midpoint: inside spear reach only mid-walk (out of reach
        // both where the enemy starts and where it stops). C covers the end
        // of the walk. Both must stab during the single approach.
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto");
        var s = Char("S", 376f, 5 * Tile + 16 + 154f, weaponId: "skirmishers_pike");
        var c = Char("C", 296f, 5 * Tile + 16 + 96f, weaponId: "skirmishers_pike");
        var enemy = new EnemyState { X = a.X + 250f, Y = a.Y };
        var turns = new TurnSystem(grid, new[] { a, s, c }, new[] { enemy }, () => 10);

        var braced = new List<PartyMemberState>();
        turns.BraceTriggered += ch => braced.Add(ch);
        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Contains(s, braced); // crossed mid-walk, endpoint out of reach
        Assert.Contains(c, braced); // in reach at the end of the walk
        Assert.Equal(2, braced.Count);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemyBrace_PokesACharacterWalkingIntoASeenSpearDummysReach()
    {
        var grid = new int[20, 30];
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto"); // dagger: no block
        var enemy = new EnemyState
        {
            X = a.X + 200f, Y = a.Y,
            Weapon = TestWeapons.Get("skirmishers_pike"), // spear, Brace x1
        };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        int enemyBraces = 0;
        turns.EnemyBraceTriggered += _ => enemyBraces++;
        turns.NotifyEnemyVisible(enemy, true);

        // Outside spear reach (162 surface > 128): stepping around is safe.
        a.X += 10f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(0, enemyBraces);
        Assert.Equal(GameConstants.PlayerHp, a.Hp);

        // Walk into reach: one free spear poke (7, plus Longshot x1's +1 for
        // a hit at the fourth tile — surface 122 — and no block from a dagger).
        a.X = enemy.X - 150f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(1, enemyBraces);
        Assert.Equal(GameConstants.PlayerHp - 8, a.Hp);

        // Deeper movement inside reach: no second trigger.
        a.X += 20f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(1, enemyBraces);

        // Out and back in: pair re-arms, but Brace 1 is spent this turn.
        a.X = enemy.X - 250f;
        turns.NotifyCharacterMoved(a);
        a.X = enemy.X - 150f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(1, enemyBraces);
    }

    [Fact]
    public void EnemyBrace_DoesNotTrigger_FromUnseenEnemies_OrWhenAlreadyInReach()
    {
        var grid = new int[20, 30];
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto");
        var enemy = new EnemyState
        {
            X = a.X + 150f, Y = a.Y, // already inside spear reach
            Weapon = TestWeapons.Get("skirmishers_pike"),
        };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        int enemyBraces = 0;
        turns.EnemyBraceTriggered += _ => enemyBraces++;

        // Unseen enemy: walking around inside its reach costs nothing.
        a.X += 10f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(0, enemyBraces);

        // Cycle a turn with A parked in reach, now seen: the turn-start
        // snapshot marks the pair as already-in-reach — still no trigger.
        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 6f);
        Assert.Equal(TurnPhase.Player, turns.Phase);

        turns.NotifyEnemyVisible(enemy, true);
        a.X += 10f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(0, enemyBraces);
    }

    [Fact]
    public void EnemyPhase_RearmsZones_ForWhateverIsEquippedWhenItBegins()
    {
        var grid = new int[20, 30];
        // S swaps dagger for pike (a raw inventory swap, unannounced) with the
        // dummy already four tiles off: outside the dagger's 40, inside the
        // pike's 128. Whatever is equipped when the enemy phase begins is what
        // reacts, and a mover already inside a reach when it arms never fires
        // on its first step: the dummy walks two tiles closer, unbraced.
        var s = Char("S", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto");
        s.Inventory[1] = TestWeapons.Get("skirmishers_pike");
        var enemy = new EnemyState { X = s.X + 4 * Tile, Y = s.Y };   // surface 100
        var turns = new TurnSystem(grid, new[] { s }, new[] { enemy }, () => 10);
        int braces = 0;
        turns.BraceTriggered += _ => braces++;

        (s.Inventory[0], s.Inventory[1]) = (s.Inventory[1], s.Inventory[0]);
        Assert.Equal("skirmishers_pike", s.EquippedWeapon!.Id);
        Assert.True(EnemyAi.CanHit(s, enemy, s.EquippedWeapon!, grid));

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.True(enemy.X < s.X + 4 * Tile);   // it walked, into sword reach
        Assert.Equal(0, braces);
        Assert.Equal(GameConstants.DummyHp, enemy.Hp);

        // The same swap with the dummy outside the pike's reach as well:
        // walking in is a real entry, and the pike answers it.
        var p = Char("P", 5 * Tile + 16, 5 * Tile + 16, weaponId: "weakspot_stiletto");
        p.Inventory[1] = TestWeapons.Get("skirmishers_pike");
        var walker = new EnemyState { X = p.X + 250f, Y = p.Y };   // outside 128 + 28
        var turns2 = new TurnSystem(grid, new[] { p }, new[] { walker }, () => 10);
        int braces2 = 0;
        turns2.BraceTriggered += _ => braces2++;
        (p.Inventory[0], p.Inventory[1]) = (p.Inventory[1], p.Inventory[0]);

        turns2.NotifyEnemyVisible(walker, true);
        turns2.EndTurn();
        Advance(turns2, 10f);

        Assert.Equal(TurnPhase.Player, turns2.Phase);
        Assert.Equal(1, braces2);
        Assert.Equal(GameConstants.DummyHp - 5, walker.Hp);   // the pike's 7 + 1 (Longshot x1 at the fourth tile, where it crossed in) into Block 3
    }

    [Fact]
    public void GameOver_WhenLastCharacterDies()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.Hp = 5; // one sword hit kills
        var enemy = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        bool over = false, died = false;
        turns.GameOver += () => over = true;
        turns.CharacterDied += _ => died = true;

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 6f);

        Assert.True(died);
        Assert.True(over);
        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.False(a.Alive);
    }

    [Fact]
    public void TwoAdjacentEnemies_BothActInSequence()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, weaponId: "weakspot_stiletto"); // dagger defender: no block
        var e1 = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile }; // sword range
        var e2 = new EnemyState { X = 5 * Tile - 60f, Y = 5 * Tile };
        // Each sword hit shoves A a tile toward the other enemy, whose own tile
        // then stops the next shove (an occupied tile ends a displacement, as a
        // wall would): every beat stays in reach.
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2 }, () => 10);

        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);

        turns.EndTurn();
        Advance(turns, 12f);

        // Each enemy lands its 3 scaled sword attacks (budget 100, cost 31.25).
        Assert.Equal(6, hits);
        Assert.Equal(GameConstants.PlayerHp - 60, a.Hp);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void Resurrection_WakesOnAFreeTile_WhenSomeoneStandsOnTheRemnant()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Hp = 10 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.False(enemy.Alive);

        // Park the character exactly on the remnant, then wait out the timer.
        a.X = enemy.X;
        a.Y = enemy.Y;
        for (int i = 0; i < GameConstants.DummyResurrectTurns; i++)
        {
            turns.EndTurn();
            Advance(turns, 3f);
        }

        Assert.True(enemy.Alive);
        var enemyTile = ((int)(enemy.Y / Tile), (int)(enemy.X / Tile));
        var charTile = ((int)(a.Y / Tile), (int)(a.X / Tile));
        Assert.NotEqual(charTile, enemyTile);

        // Nearest free tile: at most one step from where it fell.
        Assert.True(Math.Abs(enemyTile.Item1 - charTile.Item1)
            + Math.Abs(enemyTile.Item2 - charTile.Item2) <= 1);
    }

    [Fact]
    public void TwoEnemies_ApproachingTheSameTarget_DoNotStack()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile + 16, 5 * Tile + 16);
        // Same column, far beyond sword reach: both walk their full budget
        // toward A along the same row and would land on the same spot.
        var e1 = new EnemyState { X = 20 * Tile + 16, Y = 5 * Tile + 16 };
        var e2 = new EnemyState { X = 25 * Tile + 16, Y = 5 * Tile + 16 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2 }, () => 10);

        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);
        turns.EndTurn();
        Advance(turns, 15f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        float dx = e1.X - e2.X;
        float dy = e1.Y - e2.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        Assert.True(dist >= e1.Radius + e2.Radius,
            $"enemies ended {dist:0.0} apart, overlapping (radii sum {e1.Radius + e2.Radius})");
    }

    [Fact]
    public void PassiveEnemies_SkipInstantly_WithoutStallingTheTurn()
    {
        var grid = new int[20, 60];
        var a = Char("A", 2 * Tile, 5 * Tile);
        // Ten never-seen enemies scattered far away: none should cost a beat.
        var enemies = Enumerable.Range(0, 10)
            .Select(i => new EnemyState { X = (20 + 3 * i) * Tile, Y = 5 * Tile })
            .ToList();
        var turns = new TurnSystem(grid, new[] { a }, enemies, () => 10);

        turns.EndTurn();
        Advance(turns, 1.0f); // banner is 0.8s; idle enemies must add ~nothing

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.All(enemies, e => Assert.Equal((20 + 3 * enemies.IndexOf(e)) * Tile, e.X));
    }
}
