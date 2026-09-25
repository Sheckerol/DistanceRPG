using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Where the game boots. The game opens on the title (the user's ruling of
/// 2026-09-25 on the save model, R2(a)), where a campaign is continued or begun;
/// a campaign begins on the screen between runs, whose list offers the tutorial
/// alone until it is beaten, so its first run is the tutorial. The
/// <c>DISTANCERPG_DUNGEON</c> override is the dev route straight into any dungeon,
/// past the title and that screen. It is how a five- to ten-floor dungeon is
/// played without first beating the tutorial that opens the hub. A typo in it
/// must open the title as usual rather than stop the game at the door.
/// </summary>
public class RunBootTests
{
    [Fact]
    public void WithoutTheOverrideTheGameOpensOnTheTitle()
    {
        // Unset, blank, or naming nothing: the title, and past it a campaign's
        // screen between runs, where its first choice is the tutorial. The override
        // is a dev route, and a typo in it is no reason to skip them or to stop the
        // game.
        var dungeons = GameContent.Current.Dungeons;
        foreach (string? notADungeon in new[] { null, "", "   ", "spear_hal", "The Spear Hall", "SPEAR_HALL" })
            Assert.False(DistanceRpgGame.BootsIntoADungeon(notADungeon, dungeons), $"'{notADungeon}' boots past the screen");

        // Naming one, whitespace round it ignored: straight into that dungeon, and
        // the one it boots is the one it named.
        foreach (var def in dungeons.All)
        {
            Assert.True(DistanceRpgGame.BootsIntoADungeon(def.Id, dungeons));
            Assert.Equal(def.Id, DistanceRpgGame.BootDungeonId(def.Id, dungeons));
        }
        Assert.True(DistanceRpgGame.BootsIntoADungeon("  spear_hall\n", dungeons));

        // Without it, the first run is still the tutorial, chosen on the screen:
        // a new campaign's list offers nothing else, and its choice starts there.
        var campaign = CampaignRuns.Campaign();
        var listed = Assert.Single(campaign.Listings());
        Assert.Equal(DistanceRpgGame.BootDungeonId(null, dungeons), listed.Def.Id);
        Assert.Equal(0, BetweenRunsScene.FirstChoice(campaign.Listings(), campaign.DefaultNext()));
    }

    [Fact]
    public void TheGameBootsTheTutorial_UnlessTheOverrideNamesADungeon()
    {
        var dungeons = GameContent.Current.Dungeons;
        string tutorial = dungeons.Tutorial.Id;
        Assert.Equal("tutorial", tutorial);
        Assert.Equal("DISTANCERPG_DUNGEON", DistanceRpgGame.DungeonOverrideVariable);

        // Unset or blank: where a campaign begins.
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId(null, dungeons));
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId("", dungeons));
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId("   ", dungeons));

        // Every shipped dungeon is reachable by its id, whitespace round it ignored.
        foreach (var def in dungeons.All)
            Assert.Equal(def.Id, DistanceRpgGame.BootDungeonId(def.Id, dungeons));
        Assert.Equal("spear_hall", DistanceRpgGame.BootDungeonId("  spear_hall\n", dungeons));

        // An id that names nothing - a typo, a name, another case - boots the
        // tutorial: ids are stable strings, matched exactly.
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId("spear_hal", dungeons));
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId("The Spear Hall", dungeons));
        Assert.Equal(tutorial, DistanceRpgGame.BootDungeonId("SPEAR_HALL", dungeons));
    }

    [Fact]
    public void TheBootSeedIsTheOverridesWhenItIsAMapSeed()
    {
        // The DISTANCERPG_SEED override plays a seed a log or a test names - 7
        // is a tutorial whose golem drops The Bulwark (BossLootTests) - and it
        // takes exactly the values a run's seed is rolled from: 0 to 2^32 - 1.
        // Anything else rolls a seed, as an unset override does.
        Assert.Equal("DISTANCERPG_SEED", DistanceRpgGame.SeedOverrideVariable);
        Assert.Equal(7L, DistanceRpgGame.BootSeed("7"));
        Assert.Equal(2762136374L, DistanceRpgGame.BootSeed("  2762136374\n"));
        Assert.Equal(0L, DistanceRpgGame.BootSeed("0"));
        Assert.Equal((long)uint.MaxValue, DistanceRpgGame.BootSeed("4294967295"));

        foreach (string? notASeed in new[] { null, "", "   ", "-1", "4294967296", "7.0", "+7", "seven", "0x7" })
            Assert.Null(DistanceRpgGame.BootSeed(notASeed));
    }

    [Fact]
    public void TheSaveFolderIsTheOverridesWhenSet()
    {
        // 1e, 1f: the campaign is saved in the player's data folder, not beside
        // the executable, and DISTANCERPG_SAVE_DIR moves the whole save folder —
        // the dev overrides' slot inside it. Strings only: the game reads the
        // variable and the data folder, and hands them in.
        string local = Path.Combine("home", "someone", ".local", "share");
        string mine = Path.Combine(local, "DistanceRPG", "saves");
        string moved = Path.Combine("scratch", "playtest-saves");
        Assert.Equal("DISTANCERPG_SAVE_DIR", SaveStore.DirectoryVariable);
        Assert.Equal(mine, SaveStore.SaveDirectory(null, local));
        Assert.Equal(mine, SaveStore.SaveDirectory("   ", local));
        Assert.Equal(moved, SaveStore.SaveDirectory(moved, local));
        Assert.Equal(moved, SaveStore.SaveDirectory($"  {moved}  ", local));

        // Whichever folder it is, a launch plays in it — or, with a dev override
        // set (a dungeon, a seed, or both), in its override slot. --new-campaign
        // alone is no override: it sets the campaign's own save and backup aside.
        foreach (string saves in new[] { mine, moved })
        {
            string slot = Path.Combine(saves, SaveStore.OverrideSlot);
            Assert.Equal(saves, SaveStore.SlotDirectory(saves, new BootRequest().DevOverride));
            Assert.Equal(saves, SaveStore.SlotDirectory(saves, new BootRequest(NewCampaign: true).DevOverride));
            Assert.Equal(slot, SaveStore.SlotDirectory(saves, new BootRequest("spear_hall").DevOverride));
            Assert.Equal(slot, SaveStore.SlotDirectory(saves, new BootRequest(Seed: 7).DevOverride));
            Assert.Equal(slot, SaveStore.SlotDirectory(saves, new BootRequest("spear_hall", 7, NewCampaign: true).DevOverride));
        }
        Assert.Equal("override", SaveStore.OverrideSlot);
    }
}
