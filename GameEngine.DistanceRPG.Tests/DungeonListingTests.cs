using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The hub's dungeon list (§4.5, §6.7): what the screen between runs offers,
/// read off the campaign's own numbers (<see cref="CampaignState.Listings"/>).
/// Before the tutorial is beaten it offers the tutorial alone, since the
/// tutorial is what opens the hub (§4.3). After, it offers every dungeon not
/// retired, in the content's file order. A locked one is listed with its
/// countdown and never hidden (PHASE4-133). With every one locked, the one whose
/// lock lifts soonest is listed as the early offer.
/// <para>
/// These run whole runs through the campaign, entered and left by the stairs
/// (<see cref="CampaignRuns"/>). Every list is checked against the invariants
/// every list keeps (<see cref="Listed"/>) before a test reads it. Each seed's
/// depth is pinned by an assertion before a test leans on it. Every run stands on
/// the numbers it was written against — the boss floor range, the mercy floor,
/// the lock a floor (<see cref="Premise"/>) — made current for each test, so the
/// content collection holds it.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public sealed class DungeonListingTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static DungeonDef SpearHall => Dungeons["spear_hall"];

    /// <summary>
    /// The campaign's list, checked against what every list keeps before a test
    /// reads it. The lines are in the content's file order, and no retired
    /// dungeon is among them. A line can be entered exactly when its countdown is
    /// 0, and a locked line's countdown is its lock less the run counter. The
    /// early offer, when there is one, is the campaign's and can be entered. The
    /// campaign's default next dungeon is a line that can be entered, which is
    /// where the screen's choice starts.
    /// </summary>
    private static IReadOnlyList<DungeonListing> Listed(CampaignState campaign)
    {
        var listed = campaign.Listings();
        var order = Dungeons.All.Select(d => d.Id).ToList();
        var indices = listed.Select(l => order.IndexOf(l.Def.Id)).ToArray();
        Assert.All(indices, i => Assert.True(i >= 0, "a listed dungeon is not in the content"));
        Assert.Equal(indices.Order(), indices);
        Assert.Equal(indices.Distinct().Count(), indices.Length);

        foreach (var line in listed)
        {
            Assert.False(campaign.IsRetired(line.Def.Id), $"{line.Def.Id} is retired and still listed");
            Assert.Equal(line.Selectable, line.RunsUntilAvailable == 0);
            if (!line.Selectable)
                Assert.Equal(campaign.LockedUntilRun(line.Def.Id) - campaign.RunCounter, line.RunsUntilAvailable);
            Assert.Equal(line.Early, line.Def.Id == campaign.EarlyOffer());
            if (line.Early)
                Assert.True(line.Selectable);
        }

        if (listed.Count > 0)
            Assert.True(listed.Single(l => l.Def.Id == campaign.DefaultNext()).Selectable);
        return listed;
    }

    private static string[] IdsOf(IEnumerable<DungeonListing> listed) => listed.Select(l => l.Def.Id).ToArray();

    [Fact]
    public void BeforeTheTutorialIsBeatenOnlyTheTutorialIsListed()
    {
        // The tutorial is the thing that opens the hub (§4.3, PHASE4-051), so a
        // new campaign's list is the tutorial alone. It can be entered, it never
        // counts down (it is never locked: beating it retires it instead), and the
        // screen's choice starts on it.
        var campaign = CampaignRuns.Campaign();
        var tutorial = Dungeons.Tutorial;
        Assert.Equal("tutorial", tutorial.Id);
        Assert.Equal(new DungeonListing(tutorial, Selectable: true, RunsUntilAvailable: 0, Early: false), Assert.Single(Listed(campaign)));
        Assert.Equal("tutorial", campaign.DefaultNext());

        // Failing it leaves it where it was, and alone (PHASE4-081): a retreat from
        // its first floor, a retreat from the golem's own floor, a death.
        CampaignRuns.ShallowRetreat(campaign, "tutorial");
        Assert.Equal(new[] { "tutorial" }, IdsOf(Listed(campaign)));

        var visit = campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
        Assert.Equal(2, visit.BossFloor);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        CampaignRuns.Down(visit, campaign.Party);
        Assert.Equal(RunOutcome.Retreat, CampaignRuns.Exit(campaign).Outcome);
        Assert.Equal(new[] { "tutorial" }, IdsOf(Listed(campaign)));

        visit = campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
        Assert.Equal(RunOutcome.Death, CampaignRuns.Wipe(campaign, visit.OpenTurns(campaign.Party, CampaignRuns.Ten)).Outcome);
        Assert.Equal(new DungeonListing(tutorial, true, 0, false), Assert.Single(Listed(campaign)));

        // Not even a dungeon entered by other means and cleared opens the hub early
        // (the dev override boots straight into one). It is locked and counting all
        // the same, and it is listed with its countdown the moment the tutorial falls.
        Assert.Equal(5, DungeonState.RollBossFloor(CampaignRuns.FiveDeep, SpearHall));
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.FiveDeep);   // counter 1, locked until 6
        Assert.Equal(new[] { "tutorial" }, IdsOf(Listed(campaign)));
        Assert.Equal("tutorial", campaign.DefaultNext());

        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);   // counter 2
        var hub = Listed(campaign);
        Assert.Equal(new[] { "spear_hall", "long_gallery", "knife_warrens", "quiver_pits" }, IdsOf(hub));
        Assert.Equal(new DungeonListing(SpearHall, false, 4, false), hub[0]);
    }

    [Fact]
    public void ALockedDungeonIsListedWithItsCountdown_NeverHidden()
    {
        // PHASE4-133: "Available in 4 runs" is information the player plans
        // around, and a dungeon that silently vanishes reads as a bug. A cleared
        // dungeon stays on the list, locked, with the runs that count until its
        // lock lifts. It is counted on the same run counter the lock was written
        // against, so a run that does not count (a shallow retreat) leaves the
        // number where it was and a run that counts takes one off. Then it can be
        // entered again, still on the list, in the same place.
        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);   // counter 1
        Assert.Equal(5, DungeonState.RollBossFloor(CampaignRuns.FiveDeep, SpearHall));
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.FiveDeep);   // counter 2, locked until 2 + 5 = 7

        string[] hub = ["spear_hall", "long_gallery", "knife_warrens", "quiver_pits"];
        DungeonListing Hall()
        {
            var listed = Listed(campaign);
            Assert.Equal(hub, IdsOf(listed));   // never hidden, never moved
            return listed[0];
        }

        Assert.Equal(new DungeonListing(SpearHall, Selectable: false, RunsUntilAvailable: 5, Early: false), Hall());
        Assert.Equal((2, 7), (campaign.RunCounter, campaign.LockedUntilRun("spear_hall")));

        CampaignRuns.ShallowRetreat(campaign, "long_gallery");   // counts nothing
        Assert.Equal(5, Hall().RunsUntilAvailable);

        for (int left = 4; left >= 1; left--)
        {
            CampaignRuns.DeepRetreat(campaign, "knife_warrens");   // a mercy: counts
            var hall = Hall();
            Assert.Equal((false, left), (hall.Selectable, hall.RunsUntilAvailable));
            Assert.Throws<InvalidOperationException>(() => campaign.Enter("spear_hall", CampaignRuns.SixDeep));
        }

        CampaignRuns.DeepRetreat(campaign, "knife_warrens");
        Assert.Equal(new DungeonListing(SpearHall, true, 0, false), Hall());   // lifted: 7 runs counted
        Assert.Same(campaign.Enter("spear_hall", CampaignRuns.SixDeep), campaign.Visit);
    }

    [Fact]
    public void TheRetiredTutorialIsGoneForGood()
    {
        // PHASE4-050, -081: beating the golem retires the tutorial, and the list
        // loses it for good. That is a flag, not a lock, so it has no countdown and
        // no line. The hub it opens is every other dungeon, in file order, all of
        // them open. Nothing brings the tutorial back: not runs that count, not
        // clears, not the early offer when every dungeon is locked.
        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.True(campaign.IsRetired("tutorial"));

        var hub = Listed(campaign);
        Assert.Equal(Dungeons.All.Where(d => !d.RetiresOnClear).Select(d => d.Id), IdsOf(hub));
        Assert.DoesNotContain("tutorial", IdsOf(hub));
        Assert.All(hub, line => Assert.Equal((true, 0, false), (line.Selectable, line.RunsUntilAvailable, line.Early)));
        Assert.Equal("spear_hall", campaign.DefaultNext());

        CampaignRuns.DeepRetreat(campaign, "long_gallery");
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.SixDeep);
        Assert.DoesNotContain("tutorial", IdsOf(Listed(campaign)));

        foreach (var (id, seed) in new[] { ("long_gallery", CampaignRuns.FiveDeep), ("knife_warrens", CampaignRuns.SevenDeep), ("quiver_pits", CampaignRuns.SevenDeep) })
            CampaignRuns.Clear(campaign, id, seed);
        Assert.NotNull(campaign.EarlyOffer());   // every dungeon locked: one is offered early, and it is not the tutorial
        Assert.DoesNotContain("tutorial", IdsOf(Listed(campaign)));
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("tutorial", CampaignRuns.GoldenSeed));
    }

    [Fact]
    public void WhenEveryDungeonIsLockedTheEarlyOfferIsListedSelectable()
    {
        // With the shipped four, four straight clears lock every one of them, and
        // with nothing to enter no run could tick and no lock could lift. The
        // campaign offers the dungeon whose lock lifts soonest early (ties in file
        // order), and the list shows it as the one line that can be entered,
        // marked early. Every other line goes on counting down. Early is the early
        // offer's alone. The offer is the user's ruling (settled.md, Phase 4 pass).
        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);                  // counter 1
        Assert.Equal(6, DungeonState.RollBossFloor(CampaignRuns.SixDeep, SpearHall));
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.SixDeep);                    // counter 2, until 8
        CampaignRuns.Clear(campaign, "long_gallery", CampaignRuns.FiveDeep);                 // counter 3, until 8
        CampaignRuns.Clear(campaign, "knife_warrens", CampaignRuns.SevenDeep);               // counter 4, until 11

        // One still open: nothing is early, and the open one is the default.
        var oneOpen = Listed(campaign);
        Assert.DoesNotContain(oneOpen, line => line.Early);
        Assert.Equal(new[] { "quiver_pits" }, IdsOf(oneOpen.Where(line => line.Selectable)));

        CampaignRuns.Clear(campaign, "quiver_pits", CampaignRuns.SevenDeep);                 // counter 5, until 12
        Assert.Equal(5, campaign.RunCounter);
        var locked = Listed(campaign);
        Assert.Equal(new[]
        {
            new DungeonListing(SpearHall, Selectable: true, RunsUntilAvailable: 0, Early: true),   // 8 and 8: the first in the file
            new DungeonListing(Dungeons["long_gallery"], false, 3, false),
            new DungeonListing(Dungeons["knife_warrens"], false, 6, false),
            new DungeonListing(Dungeons["quiver_pits"], false, 7, false),
        }, locked);
        Assert.Equal("spear_hall", campaign.DefaultNext());

        // A line can be entered exactly when the campaign would enter it: every
        // counting line is refused, and entering nothing rests nobody.
        foreach (var line in locked.Where(line => !line.Selectable))
            Assert.Throws<InvalidOperationException>(() => campaign.Enter(line.Def.Id, CampaignRuns.FiveDeep));
        Assert.Null(campaign.Visit);

        // The early line is entered, and left at once (a run that counts nothing).
        // Its lock now stands at the counter, so it is an ordinary open line, not
        // an early one, and the rest have not moved.
        var visit = campaign.Enter("spear_hall", CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        Assert.False(CampaignRuns.Exit(campaign).ServiceTick);
        Assert.Equal(new[]
        {
            new DungeonListing(SpearHall, true, 0, false),
            new DungeonListing(Dungeons["long_gallery"], false, 3, false),
            new DungeonListing(Dungeons["knife_warrens"], false, 6, false),
            new DungeonListing(Dungeons["quiver_pits"], false, 7, false),
        }, Listed(campaign));
    }
}

/// <summary>
/// Whole runs through a campaign, for the tests of what happens between them:
/// entered, walked down and back up by the stairs, and ended by the first
/// floor's way out or by a wipe inside a turn system. The boss's fall is
/// recorded on the bottom floor as its death records it
/// (<see cref="DungeonState.RecordBossDefeated"/>). These tests are about the
/// campaign's numbers, and the killing is <c>RunOutcomeTests</c>' to pin. The
/// seeds' depths, the mercy a deep retreat earns and the poison a wipe dies of
/// are the premise's (<see cref="Premise"/>): a caller runs them with it current.
/// </summary>
internal static class CampaignRuns
{
    /// <summary>The shipped seed: the tutorial is two floors deep on it.</summary>
    public const long GoldenSeed = 2762136374;

    /// <summary>Seeds a dungeon on the tuning's 5–10 range rolls five, six and seven floors on, every such dungeon alike: the roll reads only the seed and the range.</summary>
    public const long FiveDeep = 4;
    public const long SixDeep = 1;
    public const long SevenDeep = 7;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    /// <summary>A roll that neither crits nor fumbles.</summary>
    public static int Ten() => 10;

    /// <summary>A campaign of four fixture members in roster order, each holding a Weakspot Stiletto.</summary>
    public static CampaignState Campaign()
        => new(Ids.Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i)).ToArray());

    /// <summary>Stand the party round <paramref name="tile"/> as a party that walked there would: around living enemies, never on them.</summary>
    public static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) tile)
    {
        var floor = visit.Current;
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, tile.R, tile.C, party.Count, taken);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = Displacer.CentreOf(tiles[i].R, tiles[i].C);
    }

    /// <summary>Gather at the down-stairs, take them, and open the floor below.</summary>
    public static TurnSystem Down(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        return visit.OpenTurns(party, Ten);
    }

    /// <summary>Leave the one way there is: back up through every floor, gathered at the first floor's way out, and out.</summary>
    public static RunResult Exit(CampaignState campaign)
    {
        var visit = campaign.Visit!;
        var party = campaign.Party;
        while (visit.FloorIndex > 0)
        {
            Gather(visit, party, visit.Current.UpStairs);
            visit.Ascend(party);
            visit.OpenTurns(party, Ten);
        }
        Gather(visit, party, visit.Current.UpStairs);
        return campaign.EndRun(RunEnding.Exited);
    }

    /// <summary>Enter <paramref name="dungeonId"/> on <paramref name="seed"/>, go down to the bottom, see its boss fall, and walk out: a clear.</summary>
    public static RunResult Clear(CampaignState campaign, string dungeonId, long seed)
    {
        var visit = campaign.Enter(dungeonId, seed);
        visit.OpenTurns(campaign.Party, Ten);
        while (visit.Current.DownStairs is not null)
            Down(visit, campaign.Party);
        visit.RecordBossDefeated();
        var result = Exit(campaign);
        Assert.Equal(RunOutcome.BossKill, result.Outcome);
        return result;
    }

    /// <summary>Enter <paramref name="dungeonId"/> five floors deep, go three floors down and walk back out: a retreat that counts, as a mercy.</summary>
    public static RunResult DeepRetreat(CampaignState campaign, string dungeonId)
    {
        var visit = campaign.Enter(dungeonId, FiveDeep);
        Assert.Equal(5, visit.BossFloor);
        visit.OpenTurns(campaign.Party, Ten);
        Down(visit, campaign.Party);
        Down(visit, campaign.Party);
        var result = Exit(campaign);
        Assert.Equal((RunOutcome.Retreat, 3, true, true), (result.Outcome, result.DeepestFloor, result.ServiceTick, result.MercyTick));
        return result;
    }

    /// <summary>Enter <paramref name="dungeonId"/> and walk straight back out of the first floor: a retreat that counts nothing.</summary>
    public static RunResult ShallowRetreat(CampaignState campaign, string dungeonId)
    {
        campaign.Enter(dungeonId, FiveDeep).OpenTurns(campaign.Party, Ten);
        var result = Exit(campaign);
        Assert.Equal((RunOutcome.Retreat, 1, false), (result.Outcome, result.DeepestFloor, result.ServiceTick));
        return result;
    }

    /// <summary>
    /// Wipe the party on <paramref name="turns"/>' floor, with no scene anywhere:
    /// every member at 1 HP carrying Poison 5, and the turn ended. The ticks at the
    /// party's turn end take the lot, the turn system's game over reaches the
    /// visit's Wiped, and the campaign ends the run. Returns the result it raised.
    /// </summary>
    public static RunResult Wipe(CampaignState campaign, TurnSystem turns)
    {
        RunResult? ended = null;
        void Heard(RunResult result) => ended = result;
        campaign.RunEnded += Heard;
        foreach (var member in campaign.Party)
        {
            member.Hp = 1;
            member.ApplyStatus(StatusEffectType.Poison, null, 5);
        }
        turns.EndTurn();
        campaign.RunEnded -= Heard;

        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        return ended ?? throw new Xunit.Sdk.XunitException("The wipe ended no run.");
    }
}
