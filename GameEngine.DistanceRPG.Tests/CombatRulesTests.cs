using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class CombatRulesTests
{
    private static Weapon Dagger => GameConstants.Weapons[0]; // dmg 15, crit +4
    private static Weapon Sword => GameConstants.Weapons[1];  // dmg 10, block 3
    private static Weapon Spear => GameConstants.Weapons[2];  // dmg 7, brace

    [Fact]
    public void NormalRoll_DealsBaseDamage()
    {
        var roll = CombatRules.RollAttack(Sword, () => 10);
        Assert.Equal(RollOutcome.Normal, roll.Outcome);
        Assert.Equal(10, roll.Damage);
    }

    [Fact]
    public void NaturalTwenty_AlwaysCrits()
    {
        var roll = CombatRules.RollAttack(Sword, () => 20);
        Assert.Equal(RollOutcome.Crit, roll.Outcome);
        Assert.Equal(20, roll.Damage); // doubled
    }

    [Theory]
    [InlineData(16, RollOutcome.Crit)]   // dagger crit window is 20-4 = 16+
    [InlineData(15, RollOutcome.Normal)]
    public void CritRangeAbility_WidensCritWindow(int roll, RollOutcome expected)
    {
        var result = CombatRules.RollAttack(Dagger, () => roll);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == RollOutcome.Crit ? 30 : 15, result.Damage);
    }

    [Fact]
    public void NaturalOne_DealsHalfDamageMinimumOne()
    {
        var roll = CombatRules.RollAttack(Spear, () => 1);
        Assert.Equal(RollOutcome.Weak, roll.Outcome);
        Assert.Equal(3, roll.Damage); // floor(7 / 2)

        var oneDamage = new Weapon("Pin", 10, 1, 0, []);
        Assert.Equal(1, CombatRules.RollAttack(oneDamage, () => 1).Damage);
    }

    [Fact]
    public void Block_AbsorbsUpToItsValue()
    {
        // Spear (7 dmg) into Sword (block 3) â†’ 4 through, 3 absorbed
        var res = CombatRules.ResolveAttack(Spear, Sword, () => 10);
        Assert.Equal(4, res.Damage);
        Assert.Equal(3, res.Blocked);
    }

    [Fact]
    public void Block_AlwaysLetsOneDamageThrough()
    {
        var feather = new Weapon("Feather", 10, 2, 0, []);
        // 2 dmg into block 3: absorb is capped at damage-1 = 1
        var res = CombatRules.ResolveAttack(feather, Sword, () => 10);
        Assert.Equal(1, res.Damage);
        Assert.Equal(1, res.Blocked);
    }

    [Fact]
    public void NoBlockAbility_NothingAbsorbed()
    {
        var res = CombatRules.ResolveAttack(Sword, Dagger, () => 10);
        Assert.Equal(10, res.Damage);
        Assert.Equal(0, res.Blocked);

        var noDefender = CombatRules.ResolveAttack(Sword, null, () => 10);
        Assert.Equal(10, noDefender.Damage);
    }

    [Fact]
    public void InAttackRange_SubtractsBothRadii()
    {
        // Surface-to-surface exactly at range â†’ in range
        float d = Dagger.Range + 14f + 14f;
        Assert.True(CombatRules.InAttackRange(0, 0, 14f, d, 0, 14f, Dagger));
        Assert.False(CombatRules.InAttackRange(0, 0, 14f, d + 0.1f, 0, 14f, Dagger));
    }

    [Fact]
    public void StartingWeapons_MatchPrototype()
    {
        Assert.Equal(3, GameConstants.Weapons.Count);
        Assert.Equal(new[] { "Dagger", "Sword", "Spear" }, GameConstants.Weapons.Select(w => w.Name));
        Assert.Equal(new[] { 0, 1, 2, 2 }, GameConstants.CharStartingWeaponIdx);
    }
}
