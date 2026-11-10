using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Phase 7.5, save version 4: a wipe's outcome renamed <c>PartyWipe</c> — and a
/// version-3 save's <c>Death</c> lifted to it — and the run's death record: every
/// death a visit pays for, kept in order on the visit, handed to the run's result,
/// and saved on both, with the cross-checks that hold it to the pools (checks 36
/// and 37). A member saved down mid-encounter comes back down, and gets up when the
/// encounter ends after the load.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class DeathRecordSaveTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>A seed the Spear Hall rolls five floors on.</summary>
    private const long FiveDeep = 4;

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    private static JsonObject Tree(string text) => JsonNode.Parse(text)!.AsObject();

    private static JsonObject CampaignOf(JsonObject tree) => tree["campaign"]!.AsObject();

    /// <summary><paramref name="text"/> after <paramref name="edit"/> to its tree, restored: refused, and where.</summary>
    private static SaveFormatException RefusedAfter(string text, Action<JsonObject> edit)
    {
        var tree = Tree(text);
        edit(tree);
        return Assert.Throws<SaveFormatException>(() => Load(tree.ToJsonString()));
    }

    /// <summary>
    /// The Spear Hall entered, B dead twice on its first floor as the turn system
    /// charges a death: 40 HP XP gained and half of it cut, then 6 more, and half of
    /// the run gain left (26) cut again — so the visit holds two deaths, each a cut
    /// of B's HP pool, and B keeps 13 of the 46 it gained.
    /// </summary>
    private static (VisitScript Script, int Door) TwoDeaths()
    {
        var campaign = NewCampaign();
        campaign.Party[1].Credit(new XpCredit(XpPool.Health, null, 9));   // something at the door, so a pool can be put below it
        var script = VisitScript.Enter(campaign, "spear_hall", FiveDeep);
        var b = script.Member("B");
        int door = script.Visit.EntrySnapshot.Members.Single(m => m.MemberId == "B").HpXp;
        Assert.Equal(50, GameContent.Current.Tuning.DeathXpLossPercent);   // the halves below are the shipped share's
        b.HpXp = door + 40;
        script.Turns.DeathCost!(b);
        b.HpXp += 6;
        script.Turns.DeathCost!(b);
        Assert.Equal(door + 13, b.HpXp);
        return (script, door);
    }

    // ── The record ───────────────────────────────────────────────────────────

    [Fact]
    public void EveryDeathPaid_IsKeptInOrder_AndSavedOnTheVisit()
    {
        var (script, door) = TwoDeaths();
        var deaths = script.Visit.Deaths;
        Assert.Equal(2, deaths.Count);
        Assert.Equal(new XpClawback("B", XpSnapshot.HpPool, door + 40, door + 20), Assert.Single(deaths[0].Clawbacks));
        Assert.Equal(new XpClawback("B", XpSnapshot.HpPool, door + 26, door + 13), Assert.Single(deaths[1].Clawbacks));

        // Saved as the member and its cuts, and back the same.
        string text = Save(script.Campaign);
        var row = CampaignOf(Tree(text))["visit"]!["deaths"]!.AsArray();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse($$"""{ "memberId": "B", "cuts": [{ "pool": "HP", "before": {{door + 40}}, "after": {{door + 20}} }] }"""), row[0]));
        var back = Load(text).Campaign;
        Assert.Equal(deaths.Select(Describe), back.Visit!.Deaths.Select(Describe));
        Assert.Equal(text, Save(back));

        // A visit nobody has died in writes none: null, as the shop and the potions are.
        var quiet = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        Assert.Null(CampaignOf(Tree(Save(quiet.Campaign)))["visit"]!["deaths"]);
    }

    [Fact]
    public void TheRunsResult_CarriesEveryDeath_TheCarriedOutLast_AndSavesIt()
    {
        // B's two deaths on the way, then C carried out the way out: C gets up and
        // pays as it leaves (§7.5), and the result tells all three, in that order.
        var (script, _) = TwoDeaths();
        var c = script.Member("C");
        c.ManaXp += 9;
        c.Hp = 0;
        c.Alive = false;

        var result = CampaignRuns.Exit(script.Campaign);

        Assert.Equal(RunOutcome.Retreat, result.Outcome);
        Assert.Equal(["B", "B", "C"], result.Deaths!.Select(death => death.MemberId));
        Assert.Equal(XpSnapshot.ManaPool, Assert.Single(result.Deaths![2].Clawbacks).Pool);
        Assert.Empty(result.Clawbacks);   // a wipe's reset only

        string text = Save(script.Campaign);
        var back = Load(text).Campaign;
        Assert.Equal(result.Deaths!.Select(Describe), back.LastResult!.Deaths!.Select(Describe));
        Assert.Equal(text, Save(back));
    }

    [Fact]
    public void AWipe_IsAPartyWipe_AndKeepsTheDeathsBeforeIt()
    {
        var (script, door) = TwoDeaths();
        var result = CampaignRuns.Wipe(script.Campaign, script.Turns);

        Assert.Equal(RunOutcome.PartyWipe, result.Outcome);
        Assert.Equal(2, result.Deaths!.Count);
        Assert.Equal(door, script.Member("B").HpXp);   // the reset takes the rest back to the door

        string text = Save(script.Campaign);
        Assert.Equal("PartyWipe", (string)CampaignOf(Tree(text))["lastResult"]!["outcome"]!);
        Assert.Equal(text, Save(Load(text).Campaign));
    }

    private static string Describe(RunDeath death)
        => $"{death.MemberId}: {string.Join(", ", death.Clawbacks.Select(c => $"{c.MemberId} {c.Pool} {c.Before}>{c.After}"))}";

    // ── A member saved down ──────────────────────────────────────────────────

    [Fact]
    public void AMemberSavedDownMidEncounter_StaysDownAfterTheLoad_AndGetsUpWhenTheEncounterEnds()
    {
        // PHASE 7 DECISION 3: a save in a fight with a member down is valid, and the
        // member is revived by the next check after the load — the turn's start
        // with no live enemy in view.
        // The Spear Hall's first floor on seed 4: three enemies in sight from the
        // door down its long halls — the fight on from the first turn — and B down.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        bool InSight(VisitScript s, EnemyState row) => row.Alive && s.Floor.Fog.Visible[Displacer.TileOf(row).R, Displacer.TileOf(row).C];
        Assert.Equal([18, 19, 23], script.Floor.Enemies.Where(row => InSight(script, row)).Select(row => row.SpawnIndex));
        Assert.True(script.Turns.AnyLiveEnemyInView);
        var b = script.Member("B");
        int door = script.Visit.EntrySnapshot.Members.Single(m => m.MemberId == "B").HpXp;
        b.HpXp = door + 40;
        b.Hp = 0;
        b.Alive = false;

        var loaded = Load(Save(script.Campaign)).Campaign;
        var resumed = new VisitScript(loaded);
        resumed.Resume();
        var down = resumed.Member("B");

        // The load brought the fight back with it: enemies in view, so a turn passes
        // and B stays down.
        Assert.False(down.Alive);
        Assert.True(resumed.Turns.AnyLiveEnemyInView);
        resumed.EndTurn();
        Assert.False(down.Alive);
        Assert.Empty(resumed.Visit.Deaths);
        int before = down.HpXp;

        // Every enemy in sight put down, and the party back at the door: the next
        // turn's start finds no live enemy in view and ends the encounter.
        string[] killers = ["A", "D", "A"];   // C casts, and B is down
        var foes = resumed.Floor.Enemies.Where(row => InSight(resumed, row)).ToList();
        Assert.InRange(foes.Count, 1, killers.Length);
        for (int i = 0; i < foes.Count; i++)
            resumed.Kill(killers[i], foes[i]);
        resumed.Home();
        resumed.EndTurn();

        Assert.True(down.Alive);
        Assert.Equal(1, down.Hp);
        var death = Assert.Single(resumed.Visit.Deaths);
        Assert.Equal("B", death.MemberId);
        Assert.True(down.HpXp < before);   // the cut, against the door the save kept
        Assert.True(down.HpXp >= door);
    }

    // ── Version 4's step ─────────────────────────────────────────────────────

    /// <summary><paramref name="text"/>, a current document, as version 3 wrote it: version 3, a wipe called <c>Death</c>, and no death record anywhere.</summary>
    private static JsonObject AsVersion3(string text)
    {
        var tree = Tree(text);
        tree["version"] = 3;
        var campaign = CampaignOf(tree);
        if (campaign["lastResult"] is JsonObject last)
        {
            if ((string?)last["outcome"] == "PartyWipe")
                last["outcome"] = "Death";
            Assert.True(last.Remove("deaths"));
        }
        if (campaign["visit"] is JsonObject visit)
            Assert.True(visit.Remove("deaths"));
        return tree;
    }

    /// <summary>A campaign whose last run wiped, and nobody died before the wipe: what version 3 could have written.</summary>
    private static string Wiped()
    {
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        CampaignRuns.Wipe(script.Campaign, script.Turns);
        return Save(script.Campaign);
    }

    [Fact]
    public void AVersion3Death_IsLiftedToAPartyWipe()
    {
        string text = Wiped();
        var v3 = AsVersion3(text);
        Assert.Equal("Death", (string)CampaignOf(v3)["lastResult"]!["outcome"]!);

        var stepped = SaveCodec.LiftToVersion4(v3.DeepClone().AsObject());
        Assert.Equal("PartyWipe", (string)CampaignOf(stepped)["lastResult"]!["outcome"]!);
        Assert.Equal(3, (int)stepped["version"]!);   // the codec stamps the version

        var read = SaveCodec.Read(v3.ToJsonString());
        Assert.Equal(SaveCodec.CurrentVersion, read.Version);
        Assert.Equal(text, SaveCodec.Write(read));
        Assert.Equal(RunOutcome.PartyWipe, Load(v3.ToJsonString()).Campaign.LastResult!.Outcome);

        // Every other outcome is left as written.
        foreach (string outcome in new[] { "BossKill", "Retreat" })
        {
            var other = v3.DeepClone().AsObject();
            CampaignOf(other)["lastResult"]!["outcome"] = outcome;
            Assert.Equal(outcome, (string)CampaignOf(SaveCodec.LiftToVersion4(other))["lastResult"]!["outcome"]!);
        }
    }

    [Fact]
    public void TheVersion3StepRemovesAStrayDeathRecord()
    {
        // A death record a hand gave a version-3 document meant nothing to version 3,
        // where a death ended the run: removed from the visit and the last result,
        // whatever it is.
        var (script, _) = TwoDeaths();
        string mid = Save(script.Campaign);
        var v3 = AsVersion3(mid);
        foreach (string stray in new[] { """[{ "memberId": "B", "cuts": [] }]""", "4", "\"x\"", "{}" })
        {
            var hand = v3.DeepClone().AsObject();
            CampaignOf(hand)["visit"]!["deaths"] = JsonNode.Parse(stray);
            var lifted = SaveCodec.LiftToVersion4(hand.DeepClone().AsObject());
            Assert.False(CampaignOf(lifted)["visit"]!.AsObject().ContainsKey("deaths"), stray);
            Assert.Empty(Load(hand.ToJsonString()).Campaign.Visit!.Deaths);
        }

        string ended = Wiped();
        var hand3 = AsVersion3(ended);
        CampaignOf(hand3)["lastResult"]!["deaths"] = JsonNode.Parse("""[{ "memberId": "B", "cuts": [] }]""");
        Assert.Null(Load(hand3.ToJsonString()).Campaign.LastResult!.Deaths);
        Assert.Equal(ended, SaveCodec.Write(SaveCodec.Read(hand3.ToJsonString())));
    }

    [Fact]
    public void TheVersion3StepPassesOverWhatItCannotRead()
    {
        // Never throws on a hand edit: what it cannot walk it passes over, and the
        // reader refuses it where it is.
        var v3 = AsVersion3(Wiped());

        var number = v3.DeepClone().AsObject();
        CampaignOf(number)["lastResult"]!["outcome"] = 4;
        SaveCodec.LiftToVersion4(number.DeepClone().AsObject());
        Assert.Equal("campaign.lastResult.outcome", Assert.Throws<SaveFormatException>(() => SaveCodec.Read(number.ToJsonString())).Where);

        var notAResult = v3.DeepClone().AsObject();
        CampaignOf(notAResult)["lastResult"] = 4;
        SaveCodec.LiftToVersion4(notAResult.DeepClone().AsObject());
        Assert.Equal("campaign.lastResult", Assert.Throws<SaveFormatException>(() => SaveCodec.Read(notAResult.ToJsonString())).Where);

        var noCampaign = v3.DeepClone().AsObject();
        noCampaign["campaign"] = "x";
        SaveCodec.LiftToVersion4(noCampaign.DeepClone().AsObject());
        Assert.Equal("campaign", Assert.Throws<SaveFormatException>(() => SaveCodec.Read(noCampaign.ToJsonString())).Where);
    }

    // ── The checks ───────────────────────────────────────────────────────────

    [Fact]
    public void ADeathNamingNoMember_OrNoPool_OrAPoolTwice_IsRefusedWhereItIs()
    {
        string text = Save(TwoDeaths().Script.Campaign);
        JsonObject Death(JsonObject tree, int i) => CampaignOf(tree)["visit"]!["deaths"]![i]!.AsObject();

        Assert.Equal("campaign.visit.deaths[0].memberId", RefusedAfter(text, tree => Death(tree, 0)["memberId"] = "Z").Where);
        Assert.Equal("campaign.visit.deaths[1].cuts[0].pool", RefusedAfter(text, tree => Death(tree, 1)["cuts"]![0]!["pool"] = "Wibble").Where);
        var twice = RefusedAfter(text, tree => Death(tree, 0)["cuts"]!.AsArray().Add(Death(tree, 0)["cuts"]![0]!.DeepClone()));
        Assert.Equal("campaign.visit.deaths[0].cuts[1].pool", twice.Where);
        Assert.Equal("campaign.visit.deaths[0].cuts[0].before", RefusedAfter(text, tree => Death(tree, 0)["cuts"]![0]!["before"] = -1).Where);
    }

    [Fact]
    public void Check36_TheVisitsDeaths_StandOnTheDoor_AndThePoolsNow()
    {
        var (script, door) = TwoDeaths();
        string text = Save(script.Campaign);
        Assert.Equal(text, Save(Load(text).Campaign));   // what play wrote loads
        JsonObject Cut(JsonObject tree, int i) => CampaignOf(tree)["visit"]!["deaths"]![i]!["cuts"]![0]!.AsObject();

        // A cut that took nothing is no cut.
        var nothing = RefusedAfter(text, tree => Cut(tree, 0)["after"] = door + 40);
        Assert.Equal("campaign.visit.deaths[0].cuts[0].after", nothing.Where);

        // A cut below the door takes what the member walked in with.
        var belowDoor = RefusedAfter(text, tree => Cut(tree, 0)["after"] = door - 1);
        Assert.Equal("campaign.visit.deaths[0].cuts[0].after", belowDoor.Where);
        Assert.Contains("at the door", belowDoor.Problem, StringComparison.Ordinal);

        // The next death finds less than the last left: between deaths a visit only adds.
        var broken = RefusedAfter(text, tree =>
        {
            Cut(tree, 1)["before"] = door + 19;
            Cut(tree, 1)["after"] = door + 18;
        });
        Assert.Equal("campaign.visit.deaths[1].cuts[0].before", broken.Where);

        // The pool holds less than its last death left.
        var below = RefusedAfter(text, tree => CampaignOf(tree)["party"]![1]!["hpXp"] = door + 12);
        Assert.Equal("campaign.visit.deaths[1].cuts[0]", below.Where);
    }

    [Fact]
    public void Check37_TheLastRunsDeaths_EachTookSomething_ChainedPoolByPool()
    {
        var (script, door) = TwoDeaths();
        CampaignRuns.Exit(script.Campaign);
        string text = Save(script.Campaign);
        Assert.Equal(text, Save(Load(text).Campaign));
        JsonObject Cut(JsonObject tree, int i) => CampaignOf(tree)["lastResult"]!["deaths"]![i]!["cuts"]![0]!.AsObject();

        Assert.Equal("campaign.lastResult.deaths[1].cuts[0].after", RefusedAfter(text, tree => Cut(tree, 1)["after"] = door + 30).Where);
        var broken = RefusedAfter(text, tree =>
        {
            Cut(tree, 1)["before"] = door + 10;
            Cut(tree, 1)["after"] = door + 5;
        });
        Assert.Equal("campaign.lastResult.deaths[1].cuts[0].before", broken.Where);
    }
}
