using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The game's real configuration can be played (the user's ask of 2026-11-20): the
/// rest of the suite runs under a frozen copy (<see cref="FixtureContent"/>) so
/// that the data files and the starting party may change for play and fail
/// nothing — which would also let a change that breaks the game pass unseen. So
/// this, with <see cref="ShippedDataTests"/>, is what reads the real ones
/// (<see cref="ShippedData"/>, <see cref="StartingParty"/>): never for what they
/// say, only for whether a game started on them works. In the content collection
/// because it makes the real content current.
/// </summary>
[Collection(TestContent.Collection)]
public class ShippedFilesTests
{
    /// <summary>Make the game's own built-in content current until disposed.</summary>
    private static IDisposable UseTheGamesOwn()
    {
        var previous = GameContent.Current;
        GameContent.Use(ShippedData.Content);
        return new Restore(previous);
    }

    private sealed class Restore(GameContent previous) : IDisposable
    {
        public void Dispose() => GameContent.Use(previous);
    }

    [Fact]
    public void TheSuiteRunsUnderItsFrozenCopy_NeverTheGamesOwnFiles()
    {
        // What a test reads when it swaps nothing is the fixture, one instance —
        // not the content the game would open on.
        Assert.Same(FixtureContent.Content, GameContent.Current);
        Assert.NotSame(ShippedData.Content, GameContent.Current);
        Assert.Equal(FixtureContent.Party.Members, GameContent.Current.Party.All);
    }

    [Fact]
    public void ANewCampaignStartsOnTheGamesOwnFilesAndRoster()
    {
        // NEW CAMPAIGN, on the real files and the real roster: the party is made —
        // every weapon the roster names instantiated, a wand with the element named
        // for it — met, and walked into the tutorial. Nothing here says which
        // weapons or how many floors: only that it works.
        using var real = UseTheGamesOwn();
        var content = GameContent.Current;
        Assert.Equal(StartingParty.Data.Members, content.Party.All);

        var campaign = CampaignState.Start(content);
        Assert.Equal(StartingParty.Data.Members.Select(member => member.Id), campaign.Party.Select(member => member.Id));
        foreach (var (def, member) in StartingParty.Data.Members.Zip(campaign.Party))
        {
            Assert.Equal(def.BagWeaponIds.Prepend(def.StartingWeaponId), member.Inventory.OfType<Weapon>().Select(weapon => weapon.Id));
            Assert.All(member.Inventory.OfType<Weapon>(), weapon => Assert.True(weapon.CastsWith(weapon.Enchantments.FirstOrDefault()?.Def), $"{def.Id}'s {weapon.Id} casts nothing"));
        }
        Assert.NotEmpty(campaign.SeenInOrder);

        var floor = campaign.Enter(content.Dungeons.Tutorial.Id, mapSeed: 1);
        Assert.NotNull(floor);
        Assert.NotNull(campaign.Visit);
    }

    [Fact]
    public void EveryDungeonOfTheGamesOwnFilesCanBeEntered()
    {
        // Each on a campaign of its own, so nothing about which is open when gets in
        // the way: the map is made and the enemies placed, on a few seeds.
        using var real = UseTheGamesOwn();
        foreach (var dungeon in GameContent.Current.Dungeons.All)
            foreach (long seed in new long[] { 1, 2, 3 })
            {
                var campaign = CampaignState.Start(GameContent.Current);
                Assert.NotNull(campaign.Enter(dungeon.Id, seed));
            }
    }
}
