using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The HUD's text — presentation, but it encodes rules the docs fix: a cost
/// reads as the resolved number, never a percentage (§1.1, §1.3); modifiers
/// read as resolved values for the weapon in a wielder's hands, weapon plus
/// innate; a lingering element's status is named by its element; Mire past
/// the whole budget is paralysis; and the two crit riders carry their flavour
/// (§1.6). The drawing itself is untested; these are its strings.
/// </summary>
public class HudTextTests
{
    private static PartyMemberState Holding(string weaponId, DamageType? element = null)
    {
        var member = TestPools.Char("A");
        member.Inventory[0] = TestWeapons.Get(weaponId, element);
        return member;
    }

    private static string[] Badges(ActorState actor) => DungeonHud.StatusBadgeRuns(actor).Select(r => r.Text).ToArray();

    [Fact]
    public void WeaponStats_ShowTheResolvedCosts_NeverAPercentage()
    {
        Assert.Equal("DMG 15  RNG 40  COST 27", DungeonHud.WeaponStats(TestWeapons.Get("flensing_knife")));      // 30 less Light x1
        Assert.Equal("RNG 100  COST 40  MANA 13", DungeonHud.WeaponStats(TestWeapons.Get("staff_of_renewal")));  // 15 less Resonant x1
        Assert.Equal("DMG 8  NOVA  COST 45  MANA 18", DungeonHud.WeaponStats(TestWeapons.Get("wand_of_the_nova", DamageType.Flaming)));
    }

    [Fact]
    public void ModifierReadout_IsResolvedValuesInTableOrder_TheCritPairOneEntry()
    {
        Assert.Equal("CRIT 19+ x3  LIGHT", DungeonHud.ModifierReadout(TestWeapons.Get("flensing_knife")));
        Assert.Equal("BLOCK 6  PUSH 1", DungeonHud.ModifierReadout(TestWeapons.Get("tower_guard")));
        Assert.Equal("CLEAVE 1  SPLITTING 6  OPPORTUNIST 1", DungeonHud.ModifierReadout(TestWeapons.Get("reaver")));
        Assert.Equal("CRIT 17+ x3", DungeonHud.ModifierReadout(TestWeapons.Get("widowmaker")));
        Assert.Equal("RESONANT", DungeonHud.ModifierReadout(TestWeapons.Get("staff_of_mire")));
    }

    [Fact]
    public void ModifierReadout_CountsTheEquippedWeaponsChargesLeft_AndItsShotsHeld()
    {
        var thrower = Holding("bandolier");
        Assert.Equal("CRIT 20 x3  CHARGES 2/3", DungeonHud.ModifierReadout(thrower.EquippedWeapon!, thrower, attacksThisTurn: 1));
        Assert.Equal("CRIT 20 x3  CHARGES 3", DungeonHud.ModifierReadout(TestWeapons.Get("bandolier"), thrower));   // in the bag: the cap

        var archer = Holding("crossbow");
        Assert.Equal("CRIT 19+ x2  LONGSHOT 1  OVERWATCH 1", DungeonHud.ModifierReadout(archer.EquippedWeapon!, archer));
        archer.HeldShots = 1;
        Assert.Equal("CRIT 19+ x2  LONGSHOT 1  OVERWATCH 1 HELD", DungeonHud.ModifierReadout(archer.EquippedWeapon!, archer));
    }

    [Fact]
    public void ModifierReadout_ReadsWeaponPlusInnate()
    {
        var golem = Holding("tower_guard");
        golem.Innate = ModifierSet.Of((Block, 1));
        Assert.Equal("BLOCK 9  PUSH 1", DungeonHud.ModifierReadout(golem.EquippedWeapon!, golem));
    }

    [Fact]
    public void EnchantmentReadout_KeepsAttachmentOrder_AndShowsTiersAboveOne()
        => Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(TestWeapons.Get("flensing_knife_unique")));

    [Fact]
    public void StatusBadges_NameALingeringElementByItsElement_HideThePool_AndLeadWithParalysis()
    {
        var enemy = new EnemyState();
        enemy.ApplyStatus(Searing, DamageType.Flaming, 3);
        enemy.ApplyStatus(Searing, DamageType.Cold, 2);
        enemy.ApplyStatus(Regeneration, null, 4);
        enemy.ApplyStatus(OverhealPool, null, 3);
        Assert.Equal(new[] { "BURN 3", "FRST 2", "+4" }, Badges(enemy));
        Assert.All(Badges(enemy), badge => Assert.True(badge.Split(' ')[0].Length <= 4, badge));   // four letters fit the 5-px font

        enemy.ApplyStatus(Mire, null, 10);   // ten levels at 10% each: no budget left at all
        Assert.True(DungeonHud.IsParalysed(enemy));
        Assert.Equal(new[] { "PARA", "BURN 3", "FRST 2", "+4", "MIRE 10" }, Badges(enemy));
    }

    [Fact]
    public void StatusLegend_ResolvesEachStatus_AndGivesTheRidersTheirFlavour()
    {
        var enemy = new EnemyState();
        enemy.ApplyStatus(Sundered, null, 2);
        enemy.ApplyStatus(Weakened, null, 1);
        enemy.ApplyStatus(Mire, null, 4);
        enemy.ApplyStatus(Poison, null, 3);
        enemy.ApplyStatus(Searing, DamageType.Flaming, 2);
        Assert.Equal(new[]
        {
            "SUNDERED 2: GETTING CRIT OPENS YOU UP - TAKES +2 A HIT",
            "WEAKENED 1: GETTING CRIT RATTLES YOUR SWING - DEALS -1 A HIT",
            "MIRE 4: MOVEMENT 60 OF 100",   // an enemy's 100 cut 10% a level: the budget left, never the percentage
            "POISON 3: 3 DAMAGE AT TURN END",
            "BURNING 2: 2 DAMAGE AT TURN END",
        }, DungeonHud.StatusLegend(enemy, GameConstants.EnemyMove).Select(r => r.Text));
    }

    [Fact]
    public void MireLegend_ForAMember_MatchesTheCapItsTurnStartSet()
    {
        var member = Holding("tower_guard");
        member.ApplyStatus(Mire, null, 4);
        member.SavedMovement = 40;   // banked last turn: this turn's whole is 200, and Mire cuts all of it
        member.StartTurn();
        Assert.Equal(120f, member.EffectiveMax);   // the MOVE readout's cap
        Assert.Equal("MIRE 4: MOVEMENT 120 OF 200",
            DungeonHud.StatusLegend(member, GameConstants.MaxDistance + 40).Single().Text);
    }

    [Fact]
    public void StatusName_IsWhatBeatsAndTicksSay()
    {
        Assert.Equal("SUNDERED", DungeonHud.StatusName(Sundered, null));
        Assert.Equal("WEAKENED", DungeonHud.StatusName(Weakened, null));
        Assert.Equal("BURNING", DungeonHud.StatusName(Searing, DamageType.Flaming));
        Assert.Equal("FROSTBITE", DungeonHud.StatusName(Searing, DamageType.Cold));
        Assert.Equal("REGEN", DungeonHud.StatusName(Regeneration, null));
    }

    [Fact]
    public void HelpLine_FollowsTheHeldWeapon()
    {
        Assert.Contains("CLICK ENEMY: ATTACK", DungeonHud.HelpLine(Holding("tower_guard")));
        Assert.Contains("CLICK ENEMY: CAST MIRE", DungeonHud.HelpLine(Holding("staff_of_mire")));
        Assert.Contains("CLICK ALLY: CAST REGENERATION", DungeonHud.HelpLine(Holding("staff_of_renewal")));
        Assert.Contains("CLICK A SPOT IN 5 TILES: BLAST", DungeonHud.HelpLine(Holding("wand_of_the_blast", DamageType.Cold)));
        Assert.Contains("CLICK A DIRECTION: BEAM", DungeonHud.HelpLine(Holding("wand_of_the_beam", DamageType.Shocking)));
        Assert.Contains("N: NOVA AROUND YOU", DungeonHud.HelpLine(Holding("wand_of_the_nova", DamageType.Acidic)));
        Assert.Contains("O: HOLD FIRE", DungeonHud.HelpLine(Holding("crossbow")));
        Assert.DoesNotContain("O: HOLD FIRE", DungeonHud.HelpLine(Holding("longbow")));
    }
}
