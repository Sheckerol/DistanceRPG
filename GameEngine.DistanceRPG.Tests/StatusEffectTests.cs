using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class StatusEffectTests
{
    private const float Tile = GameConstants.Tile;
    private static Weapon Staff => GameConstants.Weapons[GameConstants.StaffWeaponIdx];

    private static PartyMemberState Char(string id, float x, float y, int weaponIdx = 0)
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = GameConstants.Weapons[weaponIdx];
        return c;
    }

    private static (TurnSystem turns, PartyMemberState a, PartyMemberState b) Scene(float bOffsetX = 30f)
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, GameConstants.StaffWeaponIdx);
        var b = Char("B", 5 * Tile + bOffsetX, 5 * Tile);
        var enemy = new EnemyState { X = 18 * Tile, Y = 18 * Tile };
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        return (turns, a, b);
    }

    [Fact]
    public void Cast_AppliesRegen_AndSpendsMovementAndMana()
    {
        var (turns, a, b) = Scene();

        StatusEffect? buffed = null;
        turns.CharacterBuffed += (_, e) => buffed = e;

        Assert.True(turns.CanCast(a, b));
        Assert.True(turns.TryCast(a, b));

        Assert.Equal(1, b.StatusLevel(StatusEffectType.Regeneration));
        Assert.Equal(GameConstants.MaxDistance - Staff.Cost, a.DistLeft);
        Assert.Equal(GameConstants.MaxMana - Staff.ManaCost, a.Mana);
        Assert.NotNull(buffed);
        Assert.Equal(1, buffed!.Level);
    }

    [Fact]
    public void Cast_StacksLevelOnRepeat()
    {
        var (turns, a, b) = Scene();

        Assert.True(turns.TryCast(a, b));
        Assert.True(turns.TryCast(a, b));
        Assert.True(turns.TryCast(a, b));

        Assert.Equal(3, b.StatusLevel(StatusEffectType.Regeneration));
        Assert.Equal(GameConstants.MaxMana - 3 * Staff.ManaCost, a.Mana);
    }

    [Fact]
    public void Cast_CanTargetSelf()
    {
        var (turns, a, _) = Scene();
        Assert.True(turns.CanCast(a, a));
        Assert.True(turns.TryCast(a, a));
        Assert.Equal(1, a.StatusLevel(StatusEffectType.Regeneration));
    }

    [Fact]
    public void Cast_BlockedWithoutMana()
    {
        var (turns, a, b) = Scene();
        a.Mana = Staff.ManaCost - 1;
        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.TryCast(a, b));
        Assert.Equal(0, b.StatusLevel(StatusEffectType.Regeneration));
    }

    [Fact]
    public void Cast_BlockedOutOfRange()
    {
        // 200px apart, surface-to-surface well beyond the staff's 100 range.
        var (turns, a, b) = Scene(bOffsetX: 200f);
        Assert.False(turns.CanCast(a, b));
    }

    [Fact]
    public void Staff_CannotAttackEnemies()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, GameConstants.StaffWeaponIdx);
        var enemy = new EnemyState { X = 5 * Tile + 30f, Y = 5 * Tile }; // well within staff range
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.False(turns.CanAttack(a, enemy));
        Assert.False(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.DummyHp, enemy.Hp);       // untouched
        Assert.Equal(GameConstants.MaxDistance, a.DistLeft);  // no movement spent
    }

    [Fact]
    public void RegularWeapon_CannotCast()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, 0); // Dagger
        var b = Char("B", 5 * Tile + 30f, 5 * Tile);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { new EnemyState { X = 18 * Tile, Y = 18 * Tile } }, () => 10);

        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.TryCast(a, b));
        Assert.Equal(0, b.StatusLevel(StatusEffectType.Regeneration));
    }

    [Fact]
    public void EndTurn_RegenHealsByLevelThenDecays()
    {
        var (turns, _, b) = Scene();
        b.Hp = 50;
        b.ApplyStatusEffect(StatusEffectType.Regeneration, 3);

        var healed = new List<int>();
        turns.CharacterHealed += (c, amount) => { if (c == b) healed.Add(amount); };

        turns.EndTurn();

        Assert.Equal(53, b.Hp);
        Assert.Equal(2, b.StatusLevel(StatusEffectType.Regeneration));
        Assert.Equal(new[] { 3 }, healed);
    }

    [Fact]
    public void EndTurn_RegenIsRemovedAtExpiry()
    {
        var (turns, _, b) = Scene();
        b.Hp = 50;
        b.ApplyStatusEffect(StatusEffectType.Regeneration, 1);

        turns.EndTurn();

        Assert.Equal(51, b.Hp);
        Assert.Empty(b.StatusEffects);
    }

    [Fact]
    public void EndTurn_RegenNeverOverheals()
    {
        var (turns, _, b) = Scene();
        b.Hp = b.MaxHp - 2;
        b.ApplyStatusEffect(StatusEffectType.Regeneration, 5);

        var healed = new List<int>();
        turns.CharacterHealed += (c, amount) => { if (c == b) healed.Add(amount); };

        turns.EndTurn();

        Assert.Equal(b.MaxHp, b.Hp);
        Assert.Equal(new[] { 2 }, healed);      // capped at missing HP
        Assert.Equal(4, b.StatusLevel(StatusEffectType.Regeneration)); // still decays
    }

    [Fact]
    public void EndTurn_DeadMemberShedsEffects()
    {
        var (turns, _, b) = Scene();
        b.Hp = 0;
        b.Alive = false;
        b.ApplyStatusEffect(StatusEffectType.Regeneration, 3);

        turns.EndTurn();

        Assert.Empty(b.StatusEffects);
        Assert.Equal(0, b.Hp); // regen can't resurrect
    }

    [Fact]
    public void EndTurn_FullyIdleTurnRegensOneTenthOfMana()
    {
        var (turns, a, _) = Scene();
        a.Mana = 0;
        // a.DistLeft is a full budget (nothing moved) → full regen slice.
        turns.EndTurn();
        Assert.Equal(GameConstants.MaxMana / GameConstants.ManaRegenTurns, a.Mana);
    }

    [Fact]
    public void EndTurn_ManaRegenScalesWithUnusedMovement()
    {
        var (turns, a, _) = Scene();
        a.Mana = 0;
        a.DistLeft = GameConstants.MaxDistance / 2f; // half the budget left unspent
        turns.EndTurn();
        Assert.Equal(GameConstants.MaxMana / GameConstants.ManaRegenTurns / 2, a.Mana);
    }
}
