using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Which dungeon the game boots into. A campaign begins in the tutorial; the
/// <c>DISTANCERPG_DUNGEON</c> override is the dev route into any other — how a
/// five- to ten-floor dungeon is played before a screen exists to choose one —
/// and a typo in it must boot the tutorial rather than stop the game at the
/// door.
/// </summary>
public class RunBootTests
{
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
}
