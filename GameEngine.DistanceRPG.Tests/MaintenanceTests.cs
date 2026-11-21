using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// An enchantment's tier rises at the enchanter (ruled after play, 2026-11-19):
/// use fills the bar of the tier it is on and no more, and a finished service on
/// its weapon — any service, whatever wear it cashed — raises every full one a
/// tier, one a service at the most. Nothing rises at the deposit, on a take-back,
/// or on a transfer's source, whose wear is never cashed; and a reorder moves its
/// soul as the maintenance left it. The ladder's own arithmetic is
/// <see cref="EnchantmentLadderTests"/>', and the dungeon's side of it
/// <see cref="EnchantmentXpTests"/>'. The ruling's numbers are stated as the
/// tests' own (<see cref="TestShop.Ruled"/>) over the premise's.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class MaintenanceTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise(tuning: TestShop.Ruled);

    public void Dispose() => _premise.Dispose();

    private static (string Id, int Tier, int Xp) Read(Enchantment entry) => (entry.Id, entry.Tier, entry.Xp);

    /// <summary>Fill the bar of the entry at <paramref name="index"/>, as use would: more than it holds, of which it banks the bar.</summary>
    private static int Fill(Weapon weapon, int index)
    {
        int bar = weapon.Enchantments[index].TierCost();
        weapon.CreditEnchantment(index, bar * 7);
        Assert.Equal(bar, weapon.Enchantments[index].Xp);
        Assert.True(weapon.Enchantments[index].BarFull);
        return bar;
    }

    /// <summary>Run counted runs until the shop is empty, and hand back the last run's result.</summary>
    private static RunResult Finish(CampaignState campaign)
    {
        RunResult? last = null;
        for (int run = 0; run < 10 && campaign.AtTheEnchanter.Count > 0; run++)
            last = TestShop.Counted(campaign);
        Assert.Empty(campaign.AtTheEnchanter);
        return last!;
    }

    [Fact]
    public void AFinishedServiceRaisesEveryFullEnchantmentOneTier_AndNoOther()
    {
        var campaign = TestShop.Opened();
        var sword = TestShop.Souled("arming_sword", 35, 160, ("flaming", 1), ("vampiric", 2), ("cold", 1));
        Fill(sword, 0);
        Fill(sword, 1);
        sword.CreditEnchantment(2, 3);   // part of the way: no tier for it
        var place = TestShop.Give(campaign, "A", 2, sword);

        // The page and the panel say which a service would raise, before it is asked.
        Assert.Equal("FLAMING XP FULL  VAMPIRIC T2 XP FULL  COLD", DungeonHud.EnchantmentReadout(sword));
        Assert.Equal("2 ENCHANTMENTS ARE FULL - ANY SERVICE RAISES EACH A TIER", DepositPanel.FullLine(sword));
        var panel = DepositPanel.Open(campaign, place);
        Assert.Equal(DepositPanel.FullLine(sword), BetweenRunsHud.PanelRows(panel, 10)[2].Text);

        // The deposit raises nothing: the service does, as it finishes.
        Enchanter.Deposit(campaign, place, Service.Refine);
        Assert.Equal([1, 2, 1], sword.Enchantments.Select(entry => entry.Tier));
        Assert.All(sword.Enchantments.Take(2), entry => Assert.True(entry.BarFull));

        var result = Finish(campaign);
        Assert.Equal([("flaming", 2, 0), ("vampiric", 3, 0), ("cold", 1, 3)], sword.Enchantments.Select(Read));
        var report = Assert.Single(result.Services!, service => service.Finished);
        Assert.Equal([("flaming", 2, 0), ("vampiric", 3, 0)], report.Raised!.Select(Read));
        Assert.Contains(DistanceRpgGame.EnchanterLines(result), line => line.Contains("finished: refined; flaming to T2, vampiric to T3; ", StringComparison.Ordinal));
        Assert.Equal("FLAMING T2  VAMPIRIC T3  COLD", DungeonHud.EnchantmentReadout(sword));
        Assert.Null(DepositPanel.FullLine(sword));

        // One tier a service: the next finds nothing full, and raises nothing.
        Enchanter.Deposit(campaign, TestShop.InTheStable(campaign, sword), Service.Refine);
        var second = Finish(campaign);
        Assert.Equal([("flaming", 2, 0), ("vampiric", 3, 0), ("cold", 1, 3)], sword.Enchantments.Select(Read));
        Assert.Null(Assert.Single(second.Services!, service => service.Finished).Raised);
    }

    [Fact]
    public void TheSummarySaysWhatRose_AndStillDoesAfterALoad()
    {
        // The player is told (the user's ask, 2026-11-19): one green line under the
        // enchanter's, naming the enchantment, its weapon and its new tier — and the
        // save keeps what rose with the result, so the hub says it after a load too.
        var campaign = TestShop.Opened();
        var sword = TestShop.Souled("arming_sword", 30, 0, ("vampiric", 2));
        Fill(sword, 0);
        Enchanter.Deposit(campaign, TestShop.Give(campaign, "A", 2, sword), Service.Refine);
        var result = Finish(campaign);

        const string rose = "VAMPIRIC ON THE BUCKLER AND SWORD ROSE TO T3";
        var lines = BetweenRunsHud.HubSummaryLines(result);
        Assert.Equal(DungeonHud.Green, lines.Single(line => line.Text == rose).Color);
        int back = lines.ToList().FindIndex(line => line.Text.StartsWith("BUCKLER AND SWORD IS BACK FROM THE ENCHANTER - REFINED - ", StringComparison.Ordinal));
        Assert.Equal(rose, lines[back + 1].Text);                    // under the weapon's own line
        Assert.Contains(rose, DungeonHud.RunSummaryLines(result).Select(line => line.Text));   // and on the run's end panel

        var restored = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign;
        var report = Assert.Single(restored.LastResult!.Services!);
        Assert.Equal(("vampiric", 3, 0), Read(Assert.Single(report.Raised!)));
        Assert.Contains(rose, BetweenRunsHud.HubSummaryLines(restored.LastResult!).Select(line => line.Text));

        // A run that raises nothing says nothing of it.
        Enchanter.Deposit(campaign, TestShop.InTheStable(campaign, sword), Service.Refine);
        Assert.DoesNotContain(BetweenRunsHud.HubSummaryLines(Finish(campaign)), line => line.Text.Contains(" ROSE ", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRaisedLineIsOneLineWhateverCameBack_AndFitsTheWindow()
    {
        var names = GameContent.Current.Enchantments.All.Where(def => !def.Unique).OrderByDescending(def => def.Name.Length).ToArray();
        var longest = GameContent.Current.Weapons.All.MaxBy(def => def.Name.Length)!;
        Enchantment At(int i, int tier) => new(names[i], tier);
        ServiceReport Back(params Enchantment[] raised) => new(longest.Id, Service.Refine, 1, 1, Raised: raised.Length > 0 ? raised : null);
        string weapon = $"THE {longest.Name.ToUpperInvariant()}";
        string a = names[0].Name.ToUpperInvariant(), b = names[1].Name.ToUpperInvariant();

        Assert.Null(BetweenRunsHud.RaisedLine([Back()]));
        Assert.Null(BetweenRunsHud.RaisedLine([new ServiceReport(longest.Id, Service.Refine, 0, 1, Stalled: true)]));
        string[] forms =
        [
            BetweenRunsHud.RaisedLine([Back(At(0, ContentBounds.DeepestTier))])!,
            BetweenRunsHud.RaisedLine([Back(At(0, 2), At(1, 3))])!,
            BetweenRunsHud.RaisedLine([Back(Enumerable.Repeat(At(0, 2), ContentBounds.MostEnchantments).ToArray())])!,
            BetweenRunsHud.RaisedLine(Enumerable.Repeat(Back(Enumerable.Repeat(At(0, 2), ContentBounds.MostEnchantments).ToArray()), ContentBounds.MostWorked).ToArray())!,
        ];
        Assert.Equal(
        [
            $"{a} ON {weapon} ROSE TO T{ContentBounds.DeepestTier}",
            $"{a} AND {b} ON {weapon} ROSE A TIER",
            $"{ContentBounds.MostEnchantments} ENCHANTMENTS ON {weapon} ROSE A TIER",
            $"{ContentBounds.MostEnchantments * ContentBounds.MostWorked} ENCHANTMENTS ROSE A TIER AT THE ENCHANTER",
        ], forms);
        Assert.All(forms, form => Assert.True(form.Length * 6f * 2f <= 1240f, $"'{form}' runs off the window"));   // the summary's own bound, at its scale
    }

    [Fact]
    public void AnUnwornWeaponsServiceStillRaisesAFullEnchantment_ThoughItRollsNothing()
    {
        // A soul that fires without the weapon striking fills its bar on a weapon
        // that banks no wear. "No wear, no roll" is the improvement roll's rule and
        // not the maintenance's: the service was still done.
        var campaign = TestShop.Opened();
        var sword = TestShop.Souled("arming_sword", 0, 0, ("vampiric", 1));
        Fill(sword, 0);
        Enchanter.Deposit(campaign, TestShop.Give(campaign, "A", 2, sword), Service.Refine);

        var result = Finish(campaign);
        Assert.Equal(("vampiric", 2, 0), Read(Assert.Single(sword.Enchantments)));
        var report = Assert.Single(result.Services!, service => service.Finished);
        Assert.Null(report.Roll);
        Assert.Contains(DistanceRpgGame.EnchanterLines(result), line => line.EndsWith("finished: refined; vampiric to T2; no wear, no roll", StringComparison.Ordinal));
    }

    [Fact]
    public void ATakeBackRaisesNothing()
    {
        // A take-back leaves a weapon as its deposit found it (PHASE 6a DECISION
        // 8): the service was never done, so the full bar is still a full bar.
        var campaign = TestShop.Opened();
        var sword = TestShop.Souled("arming_sword", 30, 0, ("vampiric", 1));
        int bar = Fill(sword, 0);
        var made = Enchanter.Deposit(campaign, TestShop.Give(campaign, "A", 2, sword), Service.Refine);

        Enchanter.TakeBack(campaign, made.Entry);
        Assert.Equal(("vampiric", 1, bar), Read(Assert.Single(sword.Enchantments)));
        Assert.Equal(30, sword.Wear);
    }

    [Fact]
    public void ATransfersSourceIsNotMaintained_AndItsSoulArrivesAsTheListSaid()
    {
        // The source's wear is never cashed (PHASE 6a DECISION 13), so nothing on
        // it is raised: the soul moves a tier down off the tier it lay at, full bar
        // or not, and what the source keeps stays full for its own next service.
        var campaign = TestShop.Opened();
        var source = TestShop.Souled("arming_sword", 0, 0, ("flaming", 1), ("vampiric", 3));
        int kept = Fill(source, 0);
        Fill(source, 1);
        var blade = TestShop.Worn("riposte_blade", wear: 30);
        var onto = TestShop.Give(campaign, "B", 2, blade);
        var from = TestShop.Give(campaign, "C", 2, source);

        var offer = Enchanter.TransferOffers(campaign, blade).Single(move => ReferenceEquals(move.Source, source) && move.Index == 1);
        Assert.Equal((null, 2), (offer.Refusal, offer.ArrivesAt));
        Enchanter.Deposit(campaign, onto, Service.Transfer, source: from, sourceIndex: 1);

        var result = Finish(campaign);
        Assert.Equal(("vampiric", 2, 0), Read(Assert.Single(blade.Enchantments)));
        Assert.Equal(("flaming", 1, kept), Read(Assert.Single(source.Enchantments)));
        var report = Assert.Single(result.Services!, service => service.Finished);
        Assert.Equal((2, null), (report.Tier, report.Raised));
    }

    [Theory]
    [InlineData(1, 1)]   // a full tier 1 gains its tier and the move takes it back: tier 1, from no XP
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void AReorderMovesItsSoulAsTheMaintenanceLeftIt(int tier, int arrives)
    {
        // The weapon is maintained first and its soul moved after, so a full soul
        // reordered gains the tier its bar bought and loses the move's: the tier it
        // had, from no XP — and the source list says so before it is chosen.
        var campaign = TestShop.Opened();
        var sword = TestShop.Souled("arming_sword", 35, 160, ("flaming", tier), ("vampiric", 1));
        Fill(sword, 0);
        Fill(sword, 1);
        var place = TestShop.Give(campaign, "A", 2, sword);

        var offer = Enchanter.TransferOffers(campaign, sword).Single(move => move.Source is null && move.Index == 0);
        Assert.Equal((null, arrives), (offer.Refusal, offer.ArrivesAt));
        Enchanter.Deposit(campaign, place, Service.Transfer, sourceIndex: 0);

        var result = Finish(campaign);
        Assert.Equal([("vampiric", 2, 0), ("flaming", arrives, 0)], sword.Enchantments.Select(Read));
        var report = Assert.Single(result.Services!, service => service.Finished);
        Assert.Equal(arrives, report.Tier);
        Assert.Equal([("flaming", tier + 1, 0), ("vampiric", 2, 0)], report.Raised!.Select(Read));

        // Short of its bar it moves as it always did: a tier down, 1 the floor.
        var plain = TestShop.Souled("arming_sword", 35, 160, ("flaming", tier), ("vampiric", 1));
        TestShop.Give(campaign, "B", 2, plain);
        Assert.Equal(Math.Max(1, tier - 1), Enchanter.TransferOffers(campaign, plain).Single(move => move.Source is null && move.Index == 0).ArrivesAt);
    }

    [Fact]
    public void InHandAFullBarReadsFull_AndAUniqueNeverDoes()
    {
        var sword = TestShop.Souled("arming_sword", 0, 0, ("vampiric", 1));
        var member = TestPools.Char("A");
        member.Inventory[0] = sword;
        var entry = sword.Enchantments[0];
        int bar = entry.TierCost();

        sword.CreditEnchantment(0, bar - 1);
        Assert.Equal($"VAMPIRIC LOCK {entry.EffectiveLock} XP {bar - 1}/{bar}", DungeonHud.EnchantmentReadout(sword, member));
        sword.CreditEnchantment(0, 1);
        Assert.Equal($"VAMPIRIC LOCK {entry.EffectiveLock} XP {bar}/{bar} FULL", DungeonHud.EnchantmentReadout(sword, member));
        Assert.Equal("VAMPIRIC XP FULL", DungeonHud.BarFilled(sword.Enchantments[0]));

        // A unique has no bar to fill: it banks what it spends, is never full, and
        // no service raises it.
        var knife = TestWeapons.Get("flensing_knife_unique");
        knife.CreditEnchantment(0, 100_000);
        Assert.Equal(100_000, knife.Enchantments[0].Xp);
        Assert.False(knife.Enchantments[0].BarFull);
        Assert.Empty(knife.Maintain());
        Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(knife));
    }
}
