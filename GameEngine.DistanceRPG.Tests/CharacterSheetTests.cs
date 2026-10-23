using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// After play (2026-10): a member capped by its staff's reserved mana read as a
/// member out of mana. The party selector now draws each member's HP and mana
/// as bars - the reserved share of the mana apart from what a rest brings back -
/// and C opens a character sheet with the real numbers.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class CharacterSheetTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>A member holding a staff of renewal: its Regeneration reserves 15 of the pool.</summary>
    private static PartyMemberState Healer(int mana)
    {
        var member = TestPools.Holding("D", TestWeapons.Get("staff_of_renewal"));
        member.Mana = mana;
        return member;
    }

    [Fact]
    public void TheManaBar_ShowsCurrent_ThenWhatARestBringsBack_ThenWhatIsReserved()
    {
        var member = Healer(mana: 4);
        Assert.Equal((115, 100, 15), (member.MaxMana, member.UsableMaxMana, member.PaidLocks));

        var bar = DungeonHud.ManaBar(member);
        Assert.Equal(new DungeonHud.BarPart(0f, 4f / 115, DungeonHud.Cyan), bar[0]);
        Assert.Equal(new DungeonHud.BarPart(4f / 115, 100f / 115, DungeonHud.ManaEmpty), bar[1]);
        Assert.Equal(new DungeonHud.BarPart(100f / 115, 1f, DungeonHud.Reserved), bar[2]);

        // Nothing reserved, nothing drawn reserved; a full pool, nothing to bring back.
        var plain = TestPools.Char("A");
        Assert.All(DungeonHud.ManaBar(plain).Skip(1), part => Assert.Equal(part.From, part.To));
    }

    [Fact]
    public void TheHpBar_IsWhatIsLeft_AndEmptyForTheDead()
    {
        var member = TestPools.Char("A");
        member.Hp = member.MaxHp / 4;
        Assert.Equal(new DungeonHud.BarPart(0f, 0.25f, DungeonHud.Green), DungeonHud.HpBar(member)[0]);

        member.Hp = 0;
        member.Alive = false;
        Assert.Equal(0f, DungeonHud.HpBar(member)[0].To);
    }

    [Fact]
    public void TheSheet_SaysTheRealManaNumbers_AndWhatReservesThem()
    {
        var member = Healer(mana: 4);
        var texts = DungeonHud.CharacterSheetLines(member).Select(line => line.Text).ToArray();

        Assert.Equal("[D] CHARACTER", texts[0]);
        Assert.Contains($"STR {member.Stats.STR}   DEX {member.Stats.DEX}   CON {member.Stats.CON}   INT {member.Stats.INT}", texts);
        Assert.Contains("MANA 4 CURRENT - 100 SPENDABLE - 15 RESERVED - 115 MAX", texts);
        Assert.Contains("  REGENERATION RESERVES 15", texts);
        Assert.Contains(texts, text => text.StartsWith("STAFF LV 1", StringComparison.Ordinal));   // in hand, so listed though untrained
        Assert.DoesNotContain(texts, text => text.StartsWith("AXE LV", StringComparison.Ordinal));   // neither trained nor held
        Assert.All(texts, text => Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]*$", text));   // every glyph the font has
    }

    [Fact]
    public void CTogglesTheSheet_AndANumberKeyLeavesItUp()
    {
        var campaign = CampaignState.Start(GameContent.Current);
        campaign.Enter("spear_hall", CampaignRuns.FiveDeep, EnemyPlacer.Unprotected);
        var scene = new DungeonScene(camera: null!, game: null!, campaign, new RecordingHost());

        scene.ToggleCharacter();
        Assert.True(scene.CharacterOpen);
        scene.AnyKey(Keys.D3);   // a number key picks the member shown, and leaves the sheet up
        Assert.Equal(GameMenu.Character, scene.ActiveMenu);
        scene.ToggleCharacter();
        Assert.Equal(GameMenu.None, scene.ActiveMenu);

        // ESC closes it as it closes every menu; and it opens over no other menu.
        scene.ToggleCharacter();
        Assert.True(scene.HandleEscape());
        Assert.Equal(GameMenu.None, scene.ActiveMenu);
        scene.HandleEscape();
        scene.ToggleCharacter();
        Assert.Equal(GameMenu.Pause, scene.ActiveMenu);
    }
}
