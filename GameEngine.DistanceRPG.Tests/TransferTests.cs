using System.Reflection;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Moving an enchantment (§6.5, §6.7; Phase 6a.6): a deposit whose soul comes off a
/// weapon the party owns — or off the weapon itself, a reorder — rather than from
/// the offer. The weapon needs the free slot and the deposit of attaching it; both
/// weapons wait in the shop as one deposit, one of the two the enchanter works on,
/// the source's wear left in its pool; and when the service comes back the soul
/// lands last a tier down, tier 1 and every unique soul intact (PHASE 6a DECISIONS
/// 11 and 13). The enchanter refuses a move that would leave a member unarmed,
/// strand a lingering element, or leave a staff or a wand casting nothing (PHASE 6a
/// DECISION 12's caster clause); and a take-back returns both. The ruling's numbers
/// are stated as the tests' own (<see cref="TestShop.Ruled"/>) over the premise's.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class TransferTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise(tuning: TestShop.Ruled);

    public void Dispose() => _premise.Dispose();

    private static CampaignState Opened() => TestShop.Opened();

    private static Weapon Worn(string id, int wear, int cashed = 0) => TestShop.Worn(id, wear, cashed);

    private static Weapon Souled(string id, int wear, int cashed, params (string Id, int Tier)[] souls) => TestShop.Souled(id, wear, cashed, souls);

    private static WeaponPlace Give(CampaignState campaign, string memberId, int slot, Weapon weapon) => TestShop.Give(campaign, memberId, slot, weapon);

    private static RunResult Counted(CampaignState campaign) => TestShop.Counted(campaign);

    private static EnchantmentDef Def(string id) => GameContent.Current.Enchantments[id];

    /// <summary>The Nameless Knife as the content makes it: Serrated, Vampiric T3 and Overheal, all forged.</summary>
    private static Weapon Knife() => TestWeapons.Get("flensing_knife_unique");

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static CampaignState Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current).Campaign;

    /// <summary>Step the WEAPONS page's choice down until its row reads <paramref name="text"/>, failing if a lap never meets it.</summary>
    private static void ChooseRow(BetweenRunsScene scene, string text)
    {
        for (int step = 0; step <= scene.Weapons.Rows().Count; step++)
        {
            if (scene.Weapons.ChosenRow.Text == text)
                return;
            scene.OnKey(Keys.S);
        }
        Assert.Fail($"No row the choice stops on reads '{text}'");
    }

    [Fact]
    public void ASoulMovesDroppingATier_Tier1Intact_AUniqueIntact()
    {
        // §6.5: a move costs the soul a tier — "the only place a tier goes
        // backwards" — the new tier from no XP; tier 1 is the floor, so a tier-1
        // entry moves intact, its XP with it, and so does a unique soul, pinned there
        // (S:1101–1106). One deposit moves it: the weapon's pool cashed, a run for a
        // first enchantment, and both weapons back in the stable when it is done.
        var campaign = Opened();
        var knife = Knife();
        knife.CreditEnchantment(0, 5, 1);   // Serrated banks what it spends, and buys nothing with it
        knife.CreditEnchantment(1, 7, 1);   // Vampiric T3, part of the way to T4
        Assert.Equal((3, 7), (knife.Enchantments[1].Tier, knife.Enchantments[1].Xp));
        var blade = Worn("riposte_blade", wear: 30);
        var made = Enchanter.Deposit(campaign, Give(campaign, "B", 2, blade), Service.Transfer, source: Give(campaign, "C", 2, knife), sourceIndex: 1);
        var entry = made.Entry;
        Assert.Equal((Service.Transfer, 30, 1, (string?)null), (entry.Service, entry.WearAtDeposit, entry.Runs, entry.EnchantmentId));
        Assert.Equal((knife, (int?)1), (entry.Source, entry.SourceIndex));
        Assert.Same(knife.Enchantments[1], entry.Soul);
        Assert.Equal((0, 30, 31), (blade.Wear, blade.WearCashed, blade.WearCapacity));   // the weapon's pool cashed, as any deposit's

        var result = Counted(campaign);
        var report = Assert.Single(result.Services!);
        Assert.True(report.Finished);
        Assert.Equal((Service.Transfer, "vampiric", (int?)2, "flensing_knife_unique"), (report.Service, report.EnchantmentId, report.Tier, report.SourceId));
        var moved = Assert.Single(blade.Enchantments);
        Assert.Equal(("vampiric", 2, 0), (moved.Id, moved.Tier, moved.Xp));   // a tier down, the new tier from no XP
        Assert.False(blade.IsForged(0));
        Assert.Equal(["serrated", "overheal"], knife.Enchantments.Select(soul => soul.Id));
        Assert.Equal(5, knife.Enchantments[0].Xp);
        Assert.Equal([blade, knife], campaign.Stable.TakeLast(2));   // both back, the weapon first
        Assert.Empty(campaign.AtTheEnchanter);

        // The summary says where it went, and where from, a line a weapon.
        var lines = BetweenRunsHud.HubSummaryLines(result).Select(line => line.Text).ToArray();
        Assert.Contains("RIPOSTE BLADE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED ON AT T2 - NO CHANGE", lines);
        Assert.Contains("NAMELESS KNIFE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED OFF", lines);
        Assert.Equal(1, Array.IndexOf(lines, "NAMELESS KNIFE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED OFF") - Array.IndexOf(lines, "RIPOSTE BLADE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED ON AT T2 - NO CHANGE"));
        Assert.StartsWith("[Enchanter] Riposte Blade finished: vampiric moved on at T2 from Nameless Knife; improvement roll ",
            DistanceRpgGame.EnchanterLines(result)[1], StringComparison.Ordinal);

        // Tier 1 travels intact, XP and all — the very entry, since nothing about it changes.
        var sword = Souled("arming_sword", 0, 0, ("flaming", 1));
        sword.CreditEnchantment(0, 3, 1);
        var flaming = sword.Enchantments[0];
        Assert.Same(flaming, flaming.Transferred());
        Assert.Equal(3, flaming.Transferred().Xp);
        var deep = new Enchantment(Def("flaming"), 4) { Xp = 11 };
        Assert.Equal((3, 0), (deep.Transferred().Tier, deep.Transferred().Xp));

        // A unique soul, pinned at tier 1, moves intact with what it banked: the
        // knife's Serrated onto a Tower Guard, from the stable.
        var guard = Worn("tower_guard", wear: 30);
        Enchanter.Deposit(campaign, Give(campaign, "D", 2, guard), Service.Transfer, source: TestShop.InTheStable(campaign, knife), sourceIndex: 0);
        var serrated = knife.Enchantments[0];
        Counted(campaign);
        Assert.Same(serrated, Assert.Single(guard.Enchantments));
        Assert.Equal((1, 5), (guard.Enchantments[0].Tier, guard.Enchantments[0].Xp));
        Assert.Equal(["overheal"], knife.Enchantments.Select(soul => soul.Id));
    }

    [Fact]
    public void AMoveLandsLast_AndAReorderIsATransfer()
    {
        // A moved soul goes last, never sorted in (§3.3). Reordering a weapon's own is
        // a transfer (S:1181–1187): off and on again, last, a tier down. It takes the
        // runs of attaching the soul again as the last of the same count — the count,
        // not the count plus one — and is allowed at five, where only a move from
        // another weapon is refused (PHASE 6a DECISION 11). The entry already last is
        // not moved at all: that would change nothing but its tier.
        var campaign = Opened();
        var cold = Souled("arming_sword", 35, 160, ("cold", 1));
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, cold), Service.Transfer, source: Give(campaign, "C", 2, Knife()), sourceIndex: 1);
        var sword = Souled("arming_sword", 35, 160, ("flaming", 2), ("vampiric", 1));
        Assert.Equal((2, 3), (Enchanter.TransferRuns(sword, reorder: true), Enchanter.TransferRuns(sword, reorder: false)));
        var reorder = Enchanter.Deposit(campaign, Give(campaign, "D", 2, sword), Service.Transfer, sourceIndex: 0).Entry;
        Assert.Equal(((Weapon?)null, (int?)0, 2), (reorder.Source, reorder.SourceIndex, reorder.Runs));

        Assert.Equal(2, campaign.AtTheEnchanter[0].Runs);   // a second enchantment's: the count plus one
        Counted(campaign);
        Assert.Equal([("flaming", 2), ("vampiric", 1)], sword.Enchantments.Select(soul => (soul.Id, soul.Tier)));   // a run of two: nothing moved yet
        Assert.Equal(["cold"], cold.Enchantments.Select(soul => soul.Id));
        Counted(campaign);
        Assert.Equal([("cold", 1), ("vampiric", 2)], cold.Enchantments.Select(soul => (soul.Id, soul.Tier)));   // last, behind what it carried
        Assert.Equal([("vampiric", 1), ("flaming", 1)], sword.Enchantments.Select(soul => (soul.Id, soul.Tier)));   // its Flaming last, a tier down
        Assert.Equal(0, sword.ForgedEnchantmentCount);

        // At five: every slot filled, a reorder is left, and a move from another
        // weapon is refused. MOVE is open on the panel for the reorder alone.
        var full = Souled("arming_sword", 50, 840, [.. Enumerable.Repeat(("vampiric", 1), 4), ("cold", 1)]);
        Assert.Equal(51, full.WearCapacity);
        var knife = Knife();
        Give(campaign, "B", 3, full);
        Give(campaign, "C", 3, knife);
        Assert.Null(Enchanter.TransferRefusal(campaign, full, null, 0));
        Assert.Equal(Enchanter.AllFilled(5), Enchanter.TransferRefusal(campaign, full, knife, 1));
        Assert.Equal(Enchanter.LastAlready, Enchanter.TransferRefusal(campaign, full, null, 4));
        Assert.Null(Enchanter.ServiceRefusal(campaign, full, Service.Transfer));
        Assert.Equal(Enchanter.AllFilled(5), Enchanter.ServiceRefusal(campaign, full, Service.Enchant));
        Assert.Equal("ALL 5 SLOTS ARE FILLED - REFINING OR REORDERING IS LEFT", BetweenRunsHud.PriceLine(full));
        var fifth = Enchanter.Deposit(campaign, new WeaponPlace("B", 3), Service.Transfer, sourceIndex: 0).Entry;
        Assert.Equal(5, fifth.Runs);   // the count: one fewer than any other service at five
        Assert.Throws<ArgumentException>(() => Enchanter.Deposit(campaign, new WeaponPlace("C", 3), Service.Transfer, source: new WeaponPlace("C", 3), sourceIndex: 0));   // a reorder names no source
    }

    [Fact]
    public void ATransferNeedsTheReceiversFreeSlotAndPrice()
    {
        // A transfer is the Enchant service with the soul brought from an owned weapon
        // (PHASE 6a DECISION 13): the weapon it lands on needs the free slot and the
        // deposit of attaching it as its next (R4) — the slot read at the capacity
        // before the deposit's cash, then the price — and a reorder the same at its
        // count. Refused, the deposit is refused, and with nothing else to move the
        // service is dimmed with the gate's own reason.
        var campaign = Opened();
        var knife = Knife();
        Give(campaign, "C", 2, knife);

        var early = Souled("arming_sword", 31, 30, ("cold", 1));   // capacity 31: slot 2 opens at 35
        var at = Give(campaign, "B", 2, early);
        Assert.Equal(Enchanter.NoFreeSlot(2, 35, 31), Enchanter.TransferRefusal(campaign, early, knife, 1));
        Assert.Equal(Enchanter.NoFreeSlot(2, 35, 31), Enchanter.ServiceRefusal(campaign, early, Service.Transfer));   // nothing else to move: the knife's refused, its own last
        var ex = Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, at, Service.Transfer, source: new WeaponPlace("C", 2), sourceIndex: 1));
        Assert.Contains(Enchanter.NoFreeSlot(2, 35, 31), ex.Message, StringComparison.Ordinal);
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Same(knife, campaign.WeaponAt(new WeaponPlace("C", 2)));

        var short_ = Souled("arming_sword", 20, 195, ("cold", 1));   // capacity 36: slot 2 open, a deposit of 20
        Give(campaign, "B", 2, short_);
        Assert.Equal(Enchanter.Short(2, 35, 20), Enchanter.TransferRefusal(campaign, short_, knife, 1));
        var paid = Souled("arming_sword", 35, 195, ("cold", 1));
        Give(campaign, "B", 2, paid);
        Assert.Null(Enchanter.TransferRefusal(campaign, paid, knife, 1));
        Assert.Null(Enchanter.ServiceRefusal(campaign, paid, Service.Transfer));

        // A fresh weapon takes a first soul on a full first bar, and not short of it.
        var fresh = Worn("riposte_blade", wear: 29);
        Give(campaign, "B", 2, fresh);
        Assert.Equal(Enchanter.Short(1, 30, 29), Enchanter.TransferRefusal(campaign, fresh, knife, 0));

        // A reorder at its count: two souls need slot 2 and a deposit of 35.
        var two = Souled("arming_sword", 35, 30, ("cold", 1), ("vampiric", 1));   // capacity 31
        Give(campaign, "B", 2, two);
        Assert.Equal(Enchanter.NoFreeSlot(2, 35, 31), Enchanter.TransferRefusal(campaign, two, null, 0));
        var twoOpen = Souled("arming_sword", 34, 160, ("cold", 1), ("vampiric", 1));   // capacity 35, a deposit of 34
        Give(campaign, "B", 2, twoOpen);
        Assert.Equal(Enchanter.Short(2, 35, 34), Enchanter.TransferRefusal(campaign, twoOpen, null, 0));
    }

    [Fact]
    public void ATransferIsOneOfTheWorked_AndTheSourcesWearIsKept()
    {
        // Both weapons of a transfer are in the shop until it is done, as one deposit,
        // and it holds one of the EnchanterWorksOn newest places, not two (PHASE 6a
        // DECISION 13): with a refinement before it and one after, the next run that
        // counts works the transfer and the newer one, and stalls the oldest. The
        // source's wear is not cashed: it waits in its pool, and comes back with it.
        var campaign = Opened();
        Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine);   // 0: A's staff, unworn, two runs
        var knife = Worn("flensing_knife_unique", wear: 20, cashed: 45);
        var blade = Worn("riposte_blade", wear: 30);
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, blade), Service.Transfer, source: Give(campaign, "C", 2, knife), sourceIndex: 1);   // 1
        var guard = Worn("tower_guard", wear: 30);
        Enchanter.Deposit(campaign, Give(campaign, "D", 2, guard), Service.Refine);   // 2
        Assert.Equal([1, 2], Enchanter.Worked(campaign.AtTheEnchanter).Select(deposit => deposit.Ordinal));
        Assert.Equal((20, 45), (knife.Wear, knife.WearCashed));   // held, never cashed

        var result = Counted(campaign);
        Assert.Equal([(true, 0, false), (false, 1, true), (false, 1, true)], result.Services!.Select(report => (report.Stalled, report.RunsWorked, report.Finished)));
        Assert.Equal((20, 45), (knife.Wear, knife.WearCashed));
        Assert.Equal((0, 30), (blade.Wear, blade.WearCashed));
        Assert.Equal([blade, knife, guard], campaign.Stable.TakeLast(3));   // in deposit order, the source after its weapon
        Assert.Equal("STALLED AT THE ENCHANTER: STAFF OF RENEWAL", BetweenRunsHud.HubSummaryLines(result).Single(line => line.Text.StartsWith(BetweenRunsHud.StalledHeading, StringComparison.Ordinal)).Text);
    }

    [Fact]
    public void BothWeaponsWaitInTheShop_TakingBackReturnsBoth_TheReceiversWearBack()
    {
        // A transfer takes both weapons out of the party's hands (PHASE 6a DECISION
        // 13): neither can be deposited again, or moved from, while it is in the
        // shop. The WEAPONS page lists the weapon WORKING and its source HELD under
        // it, each saying what it is there for. T on either asks to take the whole
        // transfer back, naming both, and ENTER returns both to the stable's end —
        // the weapon's cash reversed, its capacity as before (PHASE 6a DECISION 8),
        // the source's souls and wear as they went in — logged and saved.
        var campaign = Opened();
        var knife = Worn("flensing_knife_unique", wear: 12);
        var blade = Worn("riposte_blade", wear: 30);
        var pool = blade.WearPool;
        var from = Give(campaign, "C", 2, knife);
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, blade), Service.Transfer, source: from, sourceIndex: 1);
        var deposit = Assert.Single(campaign.AtTheEnchanter);
        Assert.Null(campaign.WeaponAt(from));
        Assert.Null(campaign.WeaponAt(new WeaponPlace("B", 2)));
        Assert.Equal(Enchanter.InTheShop(knife), Enchanter.Refusal(campaign, knife, Service.Refine));
        var other = Worn("tower_guard", wear: 30);
        Give(campaign, "D", 2, other);
        Assert.Equal(Enchanter.InTheShop(knife), Enchanter.TransferRefusal(campaign, other, knife, 0));
        Assert.DoesNotContain(Enchanter.TransferOffers(campaign, other), move => ReferenceEquals(move.Source, knife));   // the list is of what the party holds

        var shop = HubWeapons.Rows(campaign).SkipWhile(row => row.Kind != HubWeapons.RowKind.Shop).ToArray();
        Assert.Equal(["  WORKING  RIPOSTE BLADE - 0 OF 1 RUN", "  HELD     NAMELESS KNIFE"], shop.Select(row => row.Text));
        Assert.All(shop, row => Assert.Same(deposit, row.Deposit));
        Assert.Equal((false, true), (shop[0].Held, shop[1].Held));
        Assert.Equal([DungeonHud.Grey, DungeonHud.Orange], HubWeapons.Runs(shop[1]).Select(run => run.Color));   // held, grey; a unique's name orange
        Assert.Equal("TAKE VAMPIRIC FROM THE NAMELESS KNIFE - 0 OF 1 RUN", BetweenRunsHud.DetailRows(shop[0])[2].Text);
        Assert.Equal("HELD - GIVING VAMPIRIC TO THE RIPOSTE BLADE", BetweenRunsHud.DetailRows(shop[1])[2].Text);
        Assert.Equal("WORKING - BACK AFTER 1 MORE RUN THAT COUNTS", BetweenRunsHud.DetailRows(shop[1])[3].Text);
        Assert.Equal("TAKE BACK THE RIPOSTE BLADE AND THE NAMELESS KNIFE - THE MOVE IS LOST - ITS 30 WEAR COMES BACK", DepositPanel.TakeBackAsk(deposit));

        // Walked: T on the HELD row, ENTER.
        var host = new RecordingHost();
        var scene = new BetweenRunsScene(game: null!, campaign, host, shownWhole: 0);
        scene.OnKey(Keys.Tab);
        ChooseRow(scene, "  HELD     NAMELESS KNIFE");
        scene.OnKey(Keys.T);
        Assert.Equal((BetweenRunsScene.HubMode.Ask, deposit), (scene.Mode, scene.Panel!.TakingBack));
        scene.OnKey(Keys.Enter);
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Equal([blade, knife], campaign.Stable.TakeLast(2));
        Assert.Equal((30, pool), (blade.Wear, blade.WearPool));
        Assert.Equal((12, 0), (knife.Wear, knife.WearCashed));
        Assert.Equal(["serrated", "vampiric", "overheal"], knife.Enchantments.Select(soul => soul.Id));
        Assert.Equal(3, knife.ForgedEnchantmentCount);
        Assert.Equal(1, host.HubSavesAsked);
        Assert.Same(blade, scene.Weapons.ChosenRow.Weapon);
        Assert.Contains("[Enchanter] Took back Riposte Blade (riposte_blade) and Nameless Knife (flensing_knife_unique) to the stable's end:"
            + " Transfer vampiric (T3) from Nameless Knife (flensing_knife_unique) forfeited, 0 of 1 run worked lost, 30 wear returned (capacity 31 -> 30), deposit #0",
            GameEngine.Core.Diagnostics.Log.RecentEntries.Select(entry => entry.Message));
    }

    [Fact]
    public void ATransferNeverLeavesAMemberUnarmed_BothWeaponsRemoved()
    {
        // No deposit leaves a member carrying no weapon (PHASE 6a DECISION 7), and a
        // transfer's is asked with both its weapons gone: a member carrying only the
        // weapon and the source is refused, and so is one carrying only the source —
        // the reason on the source's row of the list, naming its member. The door into
        // the shop asks it too, whatever asked before it.
        var campaign = Opened();
        var d = campaign.Party[3];
        campaign.Move(new WeaponPlace("D", 1), WeaponPlace.InTheStable(0));   // D's Staff of Mire to the stable: D carries its Staff of Renewal alone
        var guard = Worn("tower_guard", wear: 30);
        var knife = Knife();
        Give(campaign, "D", 0, guard);   // over the staff: D carries the guard and the knife, and nothing else
        var from = Give(campaign, "D", 1, knife);
        Assert.Equal(2, d.Inventory.Count(carried => carried is not null));
        Assert.Equal(Enchanter.WouldCarryNone("D"), Enchanter.TransferRefusal(campaign, guard, knife, 1));
        Assert.All(Enchanter.TransferOffers(campaign, guard).Where(move => ReferenceEquals(move.Source, knife)),
            move => Assert.Equal(Enchanter.WouldCarryNone("D"), move.Refusal));
        Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, new WeaponPlace("D", 0), Service.Transfer, source: from, sourceIndex: 1));
        Assert.Throws<InvalidOperationException>(() => campaign.Enqueue(new WeaponPlace("D", 0), new Deposit(0, guard, Service.Transfer, 30, 1, null, knife, 1), from));
        Assert.Equal([guard, knife], d.Inventory.Take(2));
        Assert.Empty(campaign.AtTheEnchanter);

        // The weapon elsewhere: D, carrying only the source, is still refused, on the source's row.
        var blade = Worn("riposte_blade", wear: 30);
        Give(campaign, "B", 2, blade);
        campaign.Move(new WeaponPlace("D", 0), WeaponPlace.InTheStable(campaign.Stable.Count));   // the guard to the stable: D keeps the knife alone
        Assert.Same(knife, d.EquippedWeapon);   // its hand armed from the bag
        var row = Enchanter.TransferOffers(campaign, blade).First(move => ReferenceEquals(move.Source, knife));
        Assert.Equal(Enchanter.WouldCarryNone("D"), row.Refusal);
        Assert.Equal("SERRATED ON THE NAMELESS KNIFE - [D] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST", DepositPanel.MoveLine(row));

        // D given another weapon to carry: the move is taken, and D's hand is armed.
        campaign.Move(TestShop.InTheStable(campaign, guard), new WeaponPlace("D", 1));
        Assert.Null(Enchanter.TransferRefusal(campaign, blade, knife, 1));
        var made = Enchanter.Deposit(campaign, new WeaponPlace("B", 2), Service.Transfer, source: new WeaponPlace("D", 0), sourceIndex: 1);
        Assert.Same(guard, made.SourceArmed);
        Assert.Same(guard, d.EquippedWeapon);
        Assert.Null(made.Armed);
    }

    [Fact]
    public void TheEnchanterRefusesToStrandALingeringElement()
    {
        // S:176–181: "the enchanter refuses a transfer that would strand a unique's
        // lingering effect … the service simply cannot be requested" — restricted.json's
        // requires row, burning on flaming. The Nameless Nova's Flaming cannot leave
        // its Burning behind, nor move behind it on the Nova itself; its Burning may go
        // where Flaming is ahead of it, and nowhere else.
        var campaign = Opened();
        var nova = Worn("wand_of_the_nova_unique", wear: 35, cashed: 160);   // Flaming, Burning; capacity 35
        var sword = Worn("arming_sword", wear: 30);
        Give(campaign, "B", 2, sword);
        var from = Give(campaign, "C", 2, nova);
        string stranded = Enchanter.Strands(Def("burning"), "Flaming");
        Assert.Equal("BURNING NEEDS FLAMING AHEAD OF IT", stranded);
        Assert.Equal(stranded, Enchanter.TransferRefusal(campaign, sword, nova, 0));
        Assert.Equal(stranded, Enchanter.TransferRefusal(campaign, nova, null, 0));   // a reorder putting Flaming behind Burning
        Assert.Equal("NEEDS FLAMING AHEAD OF IT", Enchanter.TransferRefusal(campaign, sword, nova, 1));
        var lit = Souled("arming_sword", 35, 160, ("flaming", 1));
        Give(campaign, "A", 2, lit);
        Assert.Null(Enchanter.TransferRefusal(campaign, lit, nova, 1));
        var ex = Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, new WeaponPlace("B", 2), Service.Transfer, source: from, sourceIndex: 0));
        Assert.Contains(stranded, ex.Message, StringComparison.Ordinal);

        var move = Enchanter.TransferOffers(campaign, sword).Single(offer => ReferenceEquals(offer.Source, nova) && offer.Index == 0);
        Assert.Equal("FLAMING ON THE NAMELESS NOVA - BURNING NEEDS FLAMING AHEAD OF IT", DepositPanel.MoveLine(move));

        // Only this move's doing is refused: a source whose entry lacked its
        // prerequisite already (a hand-edited save) still gives away another soul.
        var broken = Souled("wand_of_the_cone", 0, 0, ("shocking", 1), ("burning", 1), ("cold", 1));
        Give(campaign, "D", 2, broken);
        Assert.Null(Enchanter.TransferRefusal(campaign, sword, broken, 2));
    }

    [Fact]
    public void ACasterKeepsACastableFirstEntry()
    {
        // PHASE 6a DECISION 12's caster clause: a staff's first entry is its cast and
        // must apply a status, and a wand's must be an element — so no move leaves a
        // staff or a wand, the one it comes off or the one it lands on, with a first
        // entry it cannot cast, or none. A staff gives its effect to a dagger once it
        // has another status to cast (settled.md's "can be transferred onto a dagger",
        // narrowed); a wand's only element stays; and a wand's Cold becomes Flaming
        // through a third element, since Cold and Flaming cannot sit together.
        var campaign = Opened();
        var dagger = Worn("flensing_knife", wear: 30);
        Give(campaign, "B", 2, dagger);
        var staff = Souled("staff_of_renewal", 0, 0, ("regeneration", 1), ("ward", 1));
        Give(campaign, "C", 2, staff);
        Assert.Null(Enchanter.TransferRefusal(campaign, dagger, staff, 0));
        var lone = Souled("staff_of_renewal", 0, 0, ("regeneration", 1));
        Give(campaign, "C", 3, lone);
        Assert.Equal("THE STAFF OF RENEWAL WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, dagger, lone, 0));
        var rotwood = TestWeapons.Get("rotwood");
        Give(campaign, "C", 4, rotwood);
        Assert.Equal("THE ROTWOOD WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, dagger, rotwood, 0));
        var move = Enchanter.TransferOffers(campaign, dagger).Single(offer => ReferenceEquals(offer.Source, rotwood));
        Assert.Equal("POISON T3 ON THE ROTWOOD - THE ROTWOOD WOULD CAST NOTHING", DepositPanel.MoveLine(move));
        var cone = Souled("wand_of_the_cone", 0, 0, ("cold", 1));
        Give(campaign, "D", 2, cone);
        Assert.Equal("THE WAND OF THE CONE WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, dagger, cone, 0));

        // Cold to Flaming, through Shocking: the wand gives its Cold away and casts
        // Shocking; given Flaming, it gives its Shocking away and casts Flaming.
        var wand = Souled("wand_of_the_cone", 0, 0, ("cold", 1), ("shocking", 1));
        Give(campaign, "D", 2, wand);
        Assert.Null(Enchanter.TransferRefusal(campaign, dagger, wand, 0));
        var later = Souled("wand_of_the_cone", 0, 0, ("shocking", 1), ("flaming", 1));
        Give(campaign, "D", 4, later);
        Assert.Null(Enchanter.TransferRefusal(campaign, dagger, later, 0));

        // A wand whose element would leave an entry it cannot cast first: refused,
        // given away or reordered behind it.
        var mixed = Souled("wand_of_the_cone", 35, 160, ("cold", 1), ("vampiric", 1));
        Give(campaign, "D", 3, mixed);
        Assert.Equal("THE WAND OF THE CONE WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, dagger, mixed, 0));
        Assert.Equal("THE WAND OF THE CONE WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, mixed, null, 0));

        // And the weapon it lands on: a soulless wand, as only a hand-edited save
        // holds, takes an element it casts and nothing else.
        var soulless = Souled("wand_of_the_blast", 30, 0);
        Give(campaign, "A", 2, soulless);
        var knife = Knife();
        Give(campaign, "A", 3, knife);
        Assert.Equal("THE WAND OF THE BLAST WOULD CAST NOTHING", Enchanter.TransferRefusal(campaign, soulless, knife, 1));
        Assert.Null(Enchanter.TransferRefusal(campaign, soulless, wand, 0));
        Assert.True(BetweenRunsHud.CastsNothing(soulless));
        Assert.Equal((true, false, false), (soulless.CastsWith(Def("cold")), soulless.CastsWith(Def("vampiric")), soulless.CastsWith(null)));
        Assert.Equal((true, false), (staff.CastsWith(Def("ward")), staff.CastsWith(Def("cold"))));
        Assert.True(dagger.CastsWith(null));   // a weapon that casts nothing needs nothing to
    }

    [Fact]
    public void AForgedInnateArrivesAcquired_ItsForgedMarkLeavesWithIt()
    {
        // A staff's forged innate moved onto a dagger arrives acquired, last: the
        // forged mark stays with the staff that was forged with it, whose count of
        // forged entries drops — the forged ones still a prefix — so the staff, casting
        // its acquired second entry now, levels no staff proficiency on it (PHASE 6a
        // DECISION 13). Weapon.Detach is the one door an entry leaves a weapon by, and
        // the enchanter's clock, finishing a transfer, its one caller.
        var campaign = Opened();
        var made = TestWeapons.Get("staff_of_renewal");
        var staff = new Weapon(GameContent.Current.Weapons["staff_of_renewal"], made.Forged, made.Modifiers,
            [.. made.Enchantments, new Enchantment(Def("ward"), 1)], made.ForgedEnchantmentCount);
        Assert.Equal((1, true, false), (staff.ForgedEnchantmentCount, staff.IsForged(0), staff.IsForged(1)));
        var dagger = Worn("flensing_knife", wear: 30);
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, dagger), Service.Transfer, source: Give(campaign, "C", 2, staff), sourceIndex: 0);
        Counted(campaign);
        Assert.Equal(["ward"], staff.Enchantments.Select(soul => soul.Id));
        Assert.Equal(0, staff.ForgedEnchantmentCount);
        Assert.Equal("ward", staff.Innate!.Id);
        Assert.Equal(["regeneration"], dagger.Enchantments.Select(soul => soul.Id));
        Assert.Equal((0, false), (dagger.ForgedEnchantmentCount, dagger.IsForged(0)));

        // Detach closes the list up in its order, a forged place leaving with a forged entry.
        var knife = Knife();
        knife.Attach(new Enchantment(Def("cold"), 1));
        Assert.Equal("cold", knife.Detach(3).Id);
        Assert.Equal(3, knife.ForgedEnchantmentCount);
        Assert.Equal("vampiric", knife.Detach(1).Id);
        Assert.Equal((2, "serrated", "overheal"), (knife.ForgedEnchantmentCount, knife.Enchantments[0].Id, knife.Enchantments[1].Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => knife.Detach(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => knife.Detach(-1));

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Same(MethodBase x, MethodBase y) => x.Module == y.Module && x.MetadataToken == y.MetadataToken;
        var detach = typeof(Weapon).GetMethod(nameof(Weapon.Detach), Declared)!;
        Assert.Equal(["Enchanter.Complete"], Calls(typeof(Weapon).Assembly).Where(call => Same(call.Target, detach))
            .Select(call => $"{Root(call.From.DeclaringType)!.Name}.{call.From.Name}").Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AMovedSoulIsDormantWhereTheLockCannotBePaid()
    {
        // A lock the new wielder cannot afford is never a refusal (S:41–42): the soul
        // moves, and sleeps there, last in line for the pool, until the pool grows to
        // cover it — nothing refused, nothing half-paid.
        var campaign = Opened();
        var b = campaign.Party[1];
        var mire = Def("mire");
        int tier = b.MaxMana / mire.Lock;   // the sword's own entry reserves all but less than a lock of B's pool
        Assert.True(tier >= 1);
        var sword = Souled("arming_sword", 35, 160, ("mire", tier));
        var knife = Knife();
        Give(campaign, "B", 2, sword);
        Give(campaign, "C", 2, knife);
        Assert.Null(Enchanter.TransferRefusal(campaign, sword, knife, 1));
        Enchanter.Deposit(campaign, new WeaponPlace("B", 2), Service.Transfer, source: new WeaponPlace("C", 2), sourceIndex: 1);
        Counted(campaign);
        Counted(campaign);   // a second enchantment's two runs

        campaign.Move(TestShop.InTheStable(campaign, sword), new WeaponPlace("B", 0));
        Assert.Same(sword, b.EquippedWeapon);
        Assert.Equal(("vampiric", 2), (sword.Enchantments[1].Id, sword.Enchantments[1].Tier));
        Assert.True(b.MaxMana - sword.Enchantments[0].EffectiveLock < sword.Enchantments[1].EffectiveLock, "the premise: the moved soul's lock does not fit what is left");
        Assert.Equal((false, true), (b.IsDormant(0), b.IsDormant(1)));
        Assert.Equal(sword.Enchantments[0].EffectiveLock, b.PaidLocks);
    }

    [Fact]
    public void ATransferRoundTripsMidQueue()
    {
        // §6.7: a transfer in the shop is saved with the campaign — its source whole,
        // with its souls and its wear, and its soul's place; a reorder names none — and
        // a finished one's report with the tier its soul landed at and its source; each
        // comes back field for field, captured again the same text. Worked on after
        // the load, it moves the same souls as the campaign that was saved.
        var campaign = TestShop.Transfers();
        string text = Save(campaign);
        var loaded = Load(text);
        StateAssert.Equivalent(campaign, loaded, "campaign",
            ignore: path => path.StartsWith("campaign.LastResult.Services[", StringComparison.Ordinal) && path.EndsWith("].Roll", StringComparison.Ordinal));
        Assert.Equal(text, Save(loaded));

        var queue = loaded.AtTheEnchanter;
        Assert.Equal(("flensing_knife_unique", (int?)0, "serrated"), (queue[0].Source?.Id, queue[0].SourceIndex, queue[0].Soul?.Id));
        Assert.Equal(["overheal"], queue[0].Source!.Enchantments.Skip(1).Select(soul => soul.Id));
        Assert.Equal(((Weapon?)null, (int?)0, "flaming"), (queue[1].Source, queue[1].SourceIndex, queue[1].Soul?.Id));
        var report = Assert.Single(loaded.LastResult!.Services!);
        Assert.Equal((Service.Transfer, "vampiric", (int?)2, "flensing_knife_unique"), (report.Service, report.EnchantmentId, report.Tier, report.SourceId));

        var tree = JsonNode.Parse(text)!.AsObject();
        var shop = tree["campaign"]!["enchanter"]!.AsArray();
        Assert.Equal(("flensing_knife_unique", 0, (string?)null), (shop[0]!["source"]!["id"]!.GetValue<string>(), shop[0]!["sourceIndex"]!.GetValue<int>(), shop[0]!["enchantment"]?.GetValue<string>()));
        Assert.Null(shop[1]!["source"]);
        var told = tree["campaign"]!["lastResult"]!["services"]![0]!;
        Assert.Equal((2, "flensing_knife_unique"), (told["tier"]!.GetValue<int>(), told["sourceId"]!.GetValue<string>()));

        // A mid-run save holds the shop too, the source with it, and the run's end
        // that finishes both moves the same souls.
        var visit = campaign.Enter("long_gallery", CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var midRun = Load(Save(campaign));
        midRun.Visit!.OpenTurns(midRun.Party, CampaignRuns.Ten);
        Assert.Equal(2, midRun.AtTheEnchanter.Count);
        Assert.NotNull(midRun.AtTheEnchanter[0].Source);
        CampaignRuns.Down(visit, campaign.Party);
        CampaignRuns.Down(visit, campaign.Party);
        CampaignRuns.Exit(campaign);
        CampaignRuns.Down(midRun.Visit!, midRun.Party);
        CampaignRuns.Down(midRun.Visit!, midRun.Party);
        CampaignRuns.Exit(midRun);
        Assert.Equal(Save(campaign), Save(midRun));
        Assert.Equal(["overheal"], campaign.Stable[^1].Enchantments.Select(soul => soul.Id));   // the knife, the last back
        Counted(campaign);
        Counted(loaded);
        Counted(loaded);
        Assert.Equal([("vampiric", 1), ("flaming", 1)], loaded.Stable[^1].Enchantments.Select(soul => (soul.Id, soul.Tier)));
    }
}
