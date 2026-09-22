using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

public class CombatRulesTests
{
    private static Weapon Dagger => TestWeapons.Get("weakspot_stiletto"); // dmg 15, CritWindow x1 (19+), CritMultiplier x1 (x3)
    private static Weapon Sword => TestWeapons.Get("tower_guard");        // dmg 10, Block x2 (absorbs 6), Push x1
    private static Weapon Spear => TestWeapons.Get("skirmishers_pike");   // dmg 7, Brace x1, Longshot x1, Light x1

    /// <summary>A Block x1 sword: the shipped 3 absorbed, without Tower Guard's second stack or Riposte Blade's counter.</summary>
    private static Weapon BlockSword => TestWeapons.Make("Sword", 80, 10, 50, (Block, 1));

    /// <summary>Resolve through the compiled chain with a party member holding each weapon (null: unarmed).</summary>
    private static AttackResolution Resolve(Weapon attackerWeapon, Weapon? defenderWeapon, int roll)
    {
        var attacker = TestPools.Char("A");
        attacker.Inventory[0] = attackerWeapon;
        var defender = TestPools.Char("B");
        defender.Inventory[0] = defenderWeapon;
        return CombatRules.ResolveAttack(attacker, defender, attackerWeapon, distanceUnits: 0, () => roll);
    }

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
        Assert.Equal(20, roll.Damage); // doubled: CritMultiplier's offset alone on a sword
    }

    [Theory]
    [InlineData(19, RollOutcome.Crit)]   // dagger baseline CritWindow x1: crit on 19-20, not the prototype's 16+
    [InlineData(18, RollOutcome.Normal)]
    public void CritRangeAbility_WidensCritWindow(int roll, RollOutcome expected)
    {
        var result = CombatRules.RollAttack(Dagger, () => roll);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == RollOutcome.Crit ? 45 : 15, result.Damage);   // CritMultiplier x1: x3 when it lands
    }

    [Fact]
    public void NaturalOne_DealsHalfDamageMinimumOne()
    {
        var roll = CombatRules.RollAttack(Spear, () => 1);
        Assert.Equal(RollOutcome.Weak, roll.Outcome);
        Assert.Equal(3, roll.Damage); // floor(7 / 2)

        var oneDamage = TestWeapons.Make("Pin", 10, 1, 0);
        Assert.Equal(1, CombatRules.RollAttack(oneDamage, () => 1).Damage);
    }

    [Fact]
    public void Block_AbsorbsUpToItsValue()
    {
        // Spear (7 dmg) into a Block x1 sword (3) -> 4 through, 3 absorbed
        var res = Resolve(Spear, BlockSword, roll: 10);
        Assert.Equal(4, res.Damage);
        Assert.Equal(3, res.Blocked);
    }

    [Fact]
    public void Block_AlwaysLetsOneDamageThrough()
    {
        var feather = TestWeapons.Make("Feather", 10, 2, 0);
        // 2 dmg into block 3: absorb is capped at damage-1 = 1
        var res = Resolve(feather, BlockSword, roll: 10);
        Assert.Equal(1, res.Damage);
        Assert.Equal(1, res.Blocked);
    }

    [Fact]
    public void NoBlockAbility_NothingAbsorbed()
    {
        var res = Resolve(Sword, Dagger, roll: 10);
        Assert.Equal(10, res.Damage);
        Assert.Equal(0, res.Blocked);

        var noDefender = Resolve(Sword, null, roll: 10);
        Assert.Equal(10, noDefender.Damage);
    }

    [Fact]
    public void InAttackRange_SubtractsBothRadii()
    {
        // Surface-to-surface exactly at range -> in range
        var dagger = Dagger;
        float d = dagger.Range + 14f + 14f;
        Assert.True(CombatRules.InAttackRange(0, 0, 14f, d, 0, 14f, dagger));
        Assert.False(CombatRules.InAttackRange(0, 0, 14f, d + 0.1f, 0, 14f, dagger));
    }

    [Fact]
    public void StartingWeapons_MatchPrototype()
    {
        // The three prototype classes keep their places 0-2 (parity, now on the
        // class enum rather than a weapon list); the loadout is dagger, sword,
        // axe, staff by stable id.
        Assert.Equal(
            new[] { WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear },
            Enum.GetValues<WeaponClass>().Take(3));

        // Read off the roster, which is where the game reads it: the loadout was
        // stated twice -- once as content and once as a pair of arrays beside
        // this assertion -- and only the arrays spawned, so pinning them pinned
        // the copy that could drift. The arrays are gone; this is the statement.
        Assert.Equal(
            new[] { "weakspot_stiletto", "tower_guard", "great_axe", "staff_of_renewal" },
            GameContent.Current.Party.All.Select(m => m.StartingWeaponId));
        Assert.Equal(
            new[] { "staff_of_renewal", "staff_of_renewal", "staff_of_renewal", "staff_of_mire" },
            GameContent.Current.Party.All.Select(m => m.BagWeaponIds.Single()));
    }

    [Fact]
    public void Staff_IsACasterWithManaCost()
    {
        Assert.Equal("Staff of Renewal", GameContent.Current.Weapons["staff_of_renewal"].Name);
        var staff = TestWeapons.Get("staff_of_renewal");
        Assert.True(staff.IsCaster);
        Assert.Equal(15, staff.ManaCost);
        Assert.Equal("regeneration", Assert.Single(staff.Enchantments).Id);
    }
}
