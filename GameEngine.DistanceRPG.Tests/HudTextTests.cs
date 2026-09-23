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
/// (§1.6). The drawing itself is untested — bar the one arithmetic the
/// inventory panel does, which decides whether what it draws fits the window
/// it is drawn in (§2.2); these are its strings.
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

    private static string[] Plate(EnemyState enemy) => DungeonHud.NameplateRuns(enemy).Select(r => r.Text).ToArray();

    private static string[] Item(Drop drop) => DungeonHud.GroundItemRuns(drop).Select(r => r.Text).ToArray();

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
    public void Nameplate_ReadsAsAThreatAndARewardAtOnce()
    {
        // A dummy nobody has put down yet reads as it always did.
        Assert.Equal(new[] { "50/50" }, Plate(new EnemyState()));

        // A farmed one carries its DefeatCount, and that one number is honest
        // about being two things: the depth of the drop it is holding and how
        // much harder it hits than the dummy beside it (3.2). The maximum it is
        // read against is its own, revivals included.
        var farmed = new EnemyState { DefeatCount = 4, RevivalMaxHp = 17, RevivalDamage = 6 };
        Assert.Equal(new[] { "67/67", "x4" }, Plate(farmed));

        // It sits between the pool and the badges, so what is left of it and how
        // many times this has happened are adjacent.
        farmed.Hp = 12;
        farmed.ApplyStatus(Sundered, null, 2);
        Assert.Equal(new[] { "12/67", "x4", "SUND 2" }, Plate(farmed));
    }

    [Fact]
    public void AGroundItemNamesTheWeaponAndItsQuality()
    {
        // A weapon lying where its carrier fell says which weapon it is and the
        // farm's depth it was collected at - the same xN, in the same shape, the
        // plate of the dummy carrying it wore, so what the player farmed for and
        // what they are being handed read as one thing (3.2, 3.5).
        var plain = new Drop(TestWeapons.Get("arming_sword"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.Equal(new[] { "Arming Sword" }, Item(plain));   // nothing farmed it: no quality to print

        var farmed = new Drop(TestWeapons.Get("tower_guard"), DefeatCount: 7, WasUniqueRoll: false);
        Assert.Equal(new[] { "Tower Guard", "x7" }, Item(farmed));

        // A unique is named in the gold every other readout gives it, rather than
        // by a word the label would have to find room for.
        var won = new Drop(TestWeapons.Get("widowmaker"), DefeatCount: 22, WasUniqueRoll: true);
        Assert.Equal(new[] { "Widowmaker", "x22" }, Item(won));
        Assert.NotEqual(DungeonHud.GroundItemRuns(farmed)[0].Color, DungeonHud.GroundItemRuns(won)[0].Color);
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

    [Fact]
    public void PoolRows_ShowTheBarAndItsXp()
    {
        // The HP row carries the bar and the XP into its next point; the mana
        // row carries the XP alone, because the pool itself is already on the
        // budget line at the top of the screen and no number is printed twice.
        // A fixture's bars are the suite's 100, and at no innate nature a point
        // costs a whole bar.
        var member = Holding("tower_guard");
        member.HpXp += 4;
        member.ManaXp += 12;
        Assert.Equal(new[] { "HP 100/100   XP 4/100", "MANA XP 12/100" }, DungeonHud.PoolRows(member));

        member.Hp -= 8;
        Assert.Equal("HP 92/100   XP 4/100", DungeonHud.PoolRows(member)[0]);
    }

    [Fact]
    public void ProficiencyRows_ShowLevelXpAndTheBonus()
    {
        // A class's row is the level, how far into the next, and what the level
        // pays: +floor(L / 2) damage and -floor(L / 3) movement, each appearing
        // once it is something. Numbers, not drawn bars - at 5 px a bar is a
        // three-pixel smear, and a string is what this file can hold to.
        var member = Holding("weakspot_stiletto");
        Assert.Equal("DAGGER 1   XP 0/100", Assert.Single(DungeonHud.ProficiencyRows(member)));

        member.WeaponXp[WeaponClass.Dagger] = 145;   // 100 leaves level 1, 45 into the 200 that leaves level 2
        Assert.Equal("DAGGER 2   XP 45/200   DMG +1", Assert.Single(DungeonHud.ProficiencyRows(member)));

        member.WeaponXp[WeaponClass.Dagger] = 345;   // level 3, where the movement discount joins it
        Assert.Equal("DAGGER 3   XP 45/300   DMG +1   COST -1", Assert.Single(DungeonHud.ProficiencyRows(member)));
    }

    [Fact]
    public void ProficiencyRows_ListTheClassesTheMemberIsLevellingOrHolding()
    {
        // A class earns a row by being practised or by being carried, in
        // WeaponClass order - the order the XP book and a save both enumerate.
        var member = Holding("staff_of_renewal");
        member.Inventory[1] = TestWeapons.Get("great_axe");
        member.WeaponXp[WeaponClass.Dagger] = 40;

        Assert.Equal(new[] { "DAGGER 1   XP 40/100", "AXE 1   XP 0/100", "STAFF 1   XP 0/100" },
            DungeonHud.ProficiencyRows(member));
        Assert.DoesNotContain(DungeonHud.ProficiencyRows(member), row => row.StartsWith("WAND", StringComparison.Ordinal));
    }

    [Fact]
    public void WeaponStats_PrintsTheItemsCost_TheProficiencyRowPrintsTheDiscount()
    {
        // Two numbers, and the panel says which is which. The statline is the
        // item as it reads in anyone's hands - the same string for a weapon in
        // the bag as for one equipped - so it keeps the weapon's own resolved
        // cost; the wielder's discount reads on the proficiency row, and what
        // the gate and the charge actually take is the two together.
        var member = Holding("flensing_knife");
        member.WeaponXp[WeaponClass.Dagger] = 345;   // level 3: one off the cost
        var knife = member.EquippedWeapon!;

        Assert.Equal("DMG 15  RNG 40  COST 27", DungeonHud.WeaponStats(knife));
        Assert.Contains("COST -1", Assert.Single(DungeonHud.ProficiencyRows(member)));
        Assert.Equal(26, member.MovementCost(knife));
    }

    [Fact]
    public void LevelUpLabel_NamesWhatTheCreditBought()
    {
        // The beat over whoever earned it: the class and the level now wielded
        // at, or the pool and the ceiling now carried. Every number is read back
        // off the member, so the callout states progression and decides none of
        // it - no XP rule lives in the HUD or the scene.
        var member = Holding("weakspot_stiletto");
        member.WeaponXp[WeaponClass.Dagger] = 100;

        Assert.Equal("DAGGER 2!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 100)));
        Assert.Equal("MAX HP 100!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Health, null, 100)));
        Assert.Equal("MAX MANA 100!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Mana, null, 100)));
    }

    [Fact]
    public void InventoryLayout_KeepsTheSwapLineAndTheKeyHintOnScreen()
    {
        // The panel grows downward by a row per class a member is levelling or
        // holding, so the lines a short window loses are the ones under the
        // rows - what a swap costs, and which key equips. It opens where it
        // always has while the whole of it fits, slides up when it does not,
        // and only past that drops rows off the end of the list.
        const int slots = 3;
        const int most = 2 + 8;   // the two pools and every weapon class at once

        var roomy = DungeonHud.LayoutInventory(720f, slots, most);
        Assert.Equal(720f / 2f - 150f, roomy.Top);   // where the panel has always opened
        Assert.Equal(most, roomy.Rows);

        var tight = DungeonHud.LayoutInventory(520f, slots, most);
        Assert.True(tight.Top < 520f / 2f - 150f);   // slid up to make the room
        Assert.Equal(most, tight.Rows);              // and every row still printed

        var cramped = DungeonHud.LayoutInventory(420f, slots, most);
        Assert.True(cramped.Rows < most);            // the tail of the list goes before the key hint does

        // The invariant, over every window the settings allow (320 x 240 up) and
        // every row count a member can reach: what is drawn clears the help line
        // at h - 24, or the panel has already dropped every row it could.
        for (int h = 240; h <= 1600; h += 2)
            for (int rows = 0; rows <= most; rows++)
            {
                var layout = DungeonHud.LayoutInventory(h, slots, rows);
                Assert.InRange(layout.Rows, 0, rows);
                Assert.True(layout.Top >= 56f, $"h={h} rows={rows}: over the top readouts");
                Assert.True(layout.Bottom <= h - 24f || layout.Rows == 0, $"h={h} rows={rows}: past the help line");
            }
    }
}
