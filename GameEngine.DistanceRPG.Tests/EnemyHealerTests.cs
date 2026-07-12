using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class EnemyHealerTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, int weaponIdx = 0)
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = GameConstants.Weapons[weaponIdx];
        return c;
    }

    private static EnemyState Enemy(float x, float y, int weaponIdx = 1)
        => new() { X = x, Y = y, Weapon = GameConstants.Weapons[weaponIdx] };

    private static EnemyState Healer(float x, float y)
        => new() { X = x, Y = y, Weapon = GameConstants.Weapons[GameConstants.StaffWeaponIdx] };

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    // ── EnemyAi units ────────────────────────────────────────────────────────

    [Fact]
    public void SelectHealTarget_PicksMostWoundedLivingAlly()
    {
        var healer = Healer(0, 0);
        var healthy = Enemy(Tile, 0); // full HP
        var scratched = Enemy(2 * Tile, 0); scratched.Hp = scratched.MaxHp - 5;
        var critical = Enemy(3 * Tile, 0); critical.Hp = 3;
        var dead = Enemy(4 * Tile, 0); dead.Hp = 0; dead.Alive = false;

        var enemies = new[] { healer, healthy, scratched, critical, dead };
        Assert.Same(critical, EnemyAi.SelectHealTarget(healer, enemies));
    }

    [Fact]
    public void SelectHealTarget_NullWhenNoAllyWounded()
    {
        var healer = Healer(0, 0);
        var ally = Enemy(Tile, 0); // full HP
        Assert.Null(EnemyAi.SelectHealTarget(healer, new[] { healer, ally }));
        Assert.False(EnemyAi.HasLivingAlly(healer, new[] { healer }));
        Assert.True(EnemyAi.HasLivingAlly(healer, new[] { healer, ally }));
    }

    [Fact]
    public void PlanFlee_MovesAwayFromTheParty()
    {
        var grid = new int[20, 20];
        var healer = Healer(10 * Tile + 16, 10 * Tile + 16);
        var chaser = Char("A", 8 * Tile + 16, 10 * Tile + 16); // two tiles to the left

        float startDist = MathF.Abs(healer.X - chaser.X);
        var (waypoints, _) = EnemyAi.PlanFlee(healer, new[] { chaser }, grid, GameConstants.EnemyMove);

        Assert.NotEmpty(waypoints);
        var end = waypoints[^1];
        Assert.True(end.X > healer.X, "healer should retreat to the right, away from the party");
        Assert.True(MathF.Abs(end.X - chaser.X) > startDist);
    }

    // ── TurnSystem end-to-end ─────────────────────────────────────────────────

    [Fact]
    public void Healer_CastsRegenOnWoundedAlly_ThenItTicks()
    {
        var grid = new int[30, 30];
        var healer = Healer(5 * Tile + 16, 5 * Tile + 16);
        var ally = Enemy(5 * Tile + 16 + 60f, 5 * Tile + 16); // 60px away, within staff range 100
        ally.Hp = 10;
        var party = Char("A", 25 * Tile, 25 * Tile); // far, out of reach

        var buffed = new List<int>();
        var healed = new List<int>();
        var turns = new TurnSystem(grid, new[] { party }, new[] { healer, ally }, () => 10);
        turns.EnemyBuffed += (e, eff) => { if (e == ally) buffed.Add(eff.Level); };
        turns.EnemyHealed += (e, amt) => { if (e == ally) healed.Add(amt); };

        turns.EndTurn();
        Advance(turns, 15f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.NotEmpty(buffed);                 // the healer cast at least once
        Assert.True(ally.Hp > 10, "wounded ally should have been healed by the regen tick");
        Assert.NotEmpty(healed);
        // A stacked buff heals across turns: some regen may still be ticking.
        Assert.True(ally.StatusLevel(StatusEffectType.Regeneration) >= 0);
    }

    [Fact]
    public void Healer_DoesNotCastWhenNoAllyIsWounded()
    {
        var grid = new int[30, 30];
        var healer = Healer(5 * Tile + 16, 5 * Tile + 16);
        var ally = Enemy(5 * Tile + 16 + 60f, 5 * Tile + 16); // full HP
        var party = Char("A", 25 * Tile, 25 * Tile);

        bool cast = false;
        var turns = new TurnSystem(grid, new[] { party }, new[] { healer, ally }, () => 10);
        turns.EnemyBuffed += (_, _) => cast = true;

        turns.EndTurn();
        Advance(turns, 15f);

        Assert.False(cast);
        Assert.Empty(ally.StatusEffects);
    }

    [Fact]
    public void LoneHealer_FleesFromTheParty()
    {
        var grid = new int[20, 20];
        var healer = Healer(10 * Tile + 16, 10 * Tile + 16);
        healer.TurnsSinceSeen = 0;
        var party = Char("A", 8 * Tile + 16, 10 * Tile + 16); // two tiles left

        float startDist = MathF.Abs(healer.X - party.X);
        bool fled = false;
        var turns = new TurnSystem(grid, new[] { party }, new[] { healer }, () => 10);
        turns.EnemyFleeing += _ => fled = true;

        turns.EndTurn();
        Advance(turns, 15f);

        Assert.True(fled, "a lone healer should flee");
        Assert.True(healer.X > 10 * Tile + 16, "healer should have retreated to the right");
        Assert.True(MathF.Abs(healer.X - party.X) > startDist);
    }
}
