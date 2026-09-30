using System.Reflection;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The deposit at the hub (§6.2, §6.7; Phase 6a.5): D on a weapon on the WEAPONS
/// page opens the deposit panel — ENCHANT or REFINE, each with the odds its return
/// improves the weapon anyway, a refused one dimmed with its reason, which its key
/// says; ENCHANT's offer list, refused offers dimmed with their reasons and never
/// hidden, in a window that scrolls with the choice; and the confirm, where only
/// ENTER deposits — and T on a weapon in the shop asks whether to take it back,
/// its wear coming back with it (PHASE 6a DECISION 8). Each change asks for a save
/// (PHASE 6a DECISION 10) and is logged with its deposit's ordinal. The panel's
/// strings and its steps are pure (<see cref="DepositPanel"/>), and the screen is
/// walked key by key with no window. The ruling's numbers are stated as the tests'
/// own (<see cref="TestShop.Ruled"/>) over the premise's.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class DepositPanelTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise(tuning: TestShop.Ruled);

    public void Dispose() => _premise.Dispose();

    /// <summary>An Arming Sword brought back carrying <paramref name="souls"/> at tier 1, acquired, with <paramref name="wear"/> banked and <paramref name="cashed"/> ever cashed.</summary>
    private static Weapon Sword(int wear, int cashed, params string[] souls)
    {
        var made = TestWeapons.Get("arming_sword");
        return new Weapon(GameContent.Current.Weapons["arming_sword"], made.Forged, made.Modifiers,
            souls.Select(soul => new Enchantment(GameContent.Current.Enchantments[soul], 1)).ToArray(), 0, wear, cashed);
    }

    /// <summary>A screen between runs walked with no window over <paramref name="campaign"/>, on its WEAPONS page, and its recording host.</summary>
    private static (BetweenRunsScene Scene, RecordingHost Host) Weapons(CampaignState campaign)
    {
        var host = new RecordingHost();
        var scene = new BetweenRunsScene(game: null!, campaign, host, shownWhole: 0);
        scene.OnKey(Keys.Tab);
        return (scene, host);
    }

    /// <summary>Step the page's choice down until its row reads <paramref name="text"/>, failing if a lap never meets it.</summary>
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

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static string[] Texts(IEnumerable<BetweenRunsHud.ListRow> rows) => rows.Select(row => row.Text).ToArray();

    [Fact]
    public void TheServicesSayTheirOddsAndTheirReasons()
    {
        // The plan's §1g example, R4-legal: an Arming Sword carrying Flaming, 195
        // wear cashed — capacity 36 — and full. Slot 2 opened at 35 and the price of
        // a second enchantment is 35, so both services are open: ENCHANT at
        // GraftBaseChance's flat 3 in 100, REFINE at 3 x (30 + 36) / 3000 — 6.6 in
        // 100, whole when exact and otherwise to a place, cut short.
        var campaign = TestShop.Opened();
        var sword = Sword(wear: 36, cashed: 195, "flaming");
        Assert.Equal((36, 36), (sword.Wear, sword.WearCapacity));
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, sword));
        Assert.Equal((DepositPanel.Step.Services, sword), (panel.Current, panel.Weapon));
        Assert.Equal(new[]
        {
            "DEPOSIT THE ARMING SWORD",
            "WEAR 36 OF 36 - ENCHANTMENTS 1 OF 5 - SLOTS OPEN 2 - 2 RUNS IN THE SHOP",
            "1  ENCHANT - A CHOSEN ENCHANTMENT - 3 IN 100 TO IMPROVE",
            "2  REFINE - NOTHING ATTACHED - 6.6 IN 100 TO IMPROVE",
        }, Texts(BetweenRunsHud.PanelRows(panel, 10)));
        Assert.Equal([DungeonHud.White, DungeonHud.Grey, DungeonHud.White, DungeonHud.White], BetweenRunsHud.PanelRows(panel, 10).Select(row => row.Color));
        Assert.All(panel.Services, option => Assert.False(option.Dimmed));
        Assert.Equal([Service.Enchant, Service.Refine], panel.Services.Select(option => option.Service));
        Assert.Null(panel.Ask);

        // At capacity 31 — 30 cashed — slot 2 is not open: ENCHANT is dimmed, its
        // reason where its odds were; REFINE reads 6.1 in 100 on the 31 it cashes.
        var early = Sword(wear: 31, cashed: 30, "flaming");
        var shut = DepositPanel.Open(campaign, TestShop.Give(campaign, "C", 2, early));
        Assert.Equal("WEAR 31 OF 31 - ENCHANTMENTS 1 OF 5 - SLOTS OPEN 1 - 2 RUNS IN THE SHOP", DepositPanel.StateLine(early));
        Assert.Equal("1  ENCHANT - NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 31", shut.Services[0].Text);
        Assert.Equal((true, Enchanter.NoFreeSlot(2, 35, 31)), (shut.Services[0].Dimmed, shut.Services[0].Refusal));
        Assert.Equal("2  REFINE - NOTHING ATTACHED - 6.1 IN 100 TO IMPROVE", shut.Services[1].Text);
        Assert.Equal(DungeonHud.DarkGrey, BetweenRunsHud.PanelRows(shut, 10)[2].Color);

        // The chance as the panel says it: whole when exact, else a place, cut short
        // and never rounded up; certain at or past the denominator.
        Assert.Equal(new[] { "0", "3", "6", "6.6", "6.1", "4.5", "6.6", "6.9", "100", "100" }, new[]
        {
            DepositPanel.Chance(0, 3000), DepositPanel.Chance(90, 3000), DepositPanel.Chance(180, 3000), DepositPanel.Chance(198, 3000),
            DepositPanel.Chance(183, 3000), DepositPanel.Chance(135, 3000), DepositPanel.Chance(1998, 30000), DepositPanel.Chance(2097, 30000),
            DepositPanel.Chance(3000, 3000), DepositPanel.Chance(6000, 3000),
        });

        // The odds are the tuning's: at GraftBaseChance 100 a refinement is certain.
        using (TestContent.UsePremise(tuning: TestShop.Landing))
        {
            var certain = DepositPanel.Open(campaign, new WeaponPlace("B", 2));
            Assert.Equal(["1  ENCHANT - A CHOSEN ENCHANTMENT - 100 IN 100 TO IMPROVE", "2  REFINE - NOTHING ATTACHED - 100 IN 100 TO IMPROVE"],
                certain.Services.Select(option => option.Text));
        }
    }

    [Fact]
    public void AnUnwornRefineIsANoteNotARefusal()
    {
        // §6.4, settled.md: "a fresh drop refines for nothing … no wear, no roll" —
        // an unworn weapon may be refined, and it comes back as it went. REFINE is
        // not dimmed; in its odds' place it says so. ENCHANT is refused: its price
        // is a wear at least.
        var campaign = TestShop.Opened();
        var stiletto = campaign.Party[0].EquippedWeapon!;
        var panel = DepositPanel.Open(campaign, new WeaponPlace("A", 0));
        Assert.Equal("1  ENCHANT - ENCHANTMENT 1 NEEDS A DEPOSIT OF 30 WEAR - IT CARRIES 0", panel.Services[0].Text);
        Assert.True(panel.Services[0].Dimmed);
        var refine = panel.Services[1];
        Assert.Equal(((string?)null, (string?)null, DepositPanel.NothingToRepair, false), (refine.Refusal, refine.Odds, refine.Note, refine.Dimmed));
        Assert.Equal("2  REFINE - NOTHING ATTACHED - NOTHING TO REPAIR - IT WILL COME BACK AS IT WENT", refine.Text);
        Assert.Equal(DungeonHud.White, BetweenRunsHud.PanelRows(panel, 10)[3].Color);

        // Picked, it asks — naming no wear to spend — its line marked on the drawn
        // panel, the ask last, in yellow.
        Assert.Null(panel.Pick(2));
        Assert.Equal((DepositPanel.Step.Confirm, Service.Refine, (string?)null), (panel.Current, panel.Service, panel.EnchantmentId));
        Assert.Equal("DEPOSIT THE WEAKSPOT STILETTO TO REFINE - 1 RUN - IT CARRIES NO WEAR", panel.Ask);
        var rows = BetweenRunsHud.PanelRows(panel, 10);
        Assert.Equal(new BetweenRunsHud.ListRow("> 2  REFINE - NOTHING ATTACHED - NOTHING TO REPAIR - IT WILL COME BACK AS IT WENT <", DungeonHud.Yellow,
            BetweenRunsHud.PanelOptionScale, rows[3].Pitch), rows[3]);
        Assert.Equal((panel.Ask, DungeonHud.Yellow), (rows[^1].Text, rows[^1].Color));

        // Deposited through the screen: accepted — its count plus one runs, nothing
        // cashed, its capacity as it was — and its take-back says it carried none.
        var (scene, _) = Weapons(campaign);
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        var deposit = Assert.Single(campaign.AtTheEnchanter);
        Assert.Equal((stiletto, Service.Refine, 0, 1), (deposit.Weapon, deposit.Service, deposit.WearAtDeposit, deposit.Runs));
        Assert.Equal((0, 0, 30), (stiletto.Wear, stiletto.WearCashed, stiletto.WearCapacity));
        Assert.Equal("TAKE BACK THE WEAKSPOT STILETTO - ITS SERVICE IS LOST - IT CARRIED NO WEAR", DepositPanel.TakeBackAsk(deposit));
    }

    [Fact]
    public void ARefusedOptionIsDimmedAndSaysWhyOnItsKey()
    {
        // PHASE 6a DECISION 12's presentation: a service the enchanter refuses is
        // listed, dimmed, its reason where its odds would be — the gates' own
        // strings — and its number says the reason over the keys until the next key,
        // picking nothing, depositing nothing, asking for no save.
        var campaign = TestShop.Opened();
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        scene.OnKey(Keys.D);
        Assert.Equal(BetweenRunsScene.HubMode.Deposit, scene.Mode);
        scene.OnKey(Keys.D1);
        Assert.Equal("ENCHANTMENT 1 NEEDS A DEPOSIT OF 30 WEAR - IT CARRIES 0", scene.Refusal);
        Assert.Equal((BetweenRunsScene.HubMode.Deposit, DepositPanel.Step.Services), (scene.Mode, scene.Panel!.Current));
        scene.OnKey(Keys.D3);   // no service is on 3: nothing, and the reason goes with the key
        Assert.Null(scene.Refusal);
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        Assert.True(scene.HandleEscape());

        // A member's last weapon (PHASE 6a DECISION 7): both services refused alike.
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        scene.OnKey(Keys.D);
        Assert.Equal(new[]
        {
            "1  ENCHANT - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST",
            "2  REFINE - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST",
        }, scene.Panel!.Services.Select(option => option.Text));
        Assert.All(BetweenRunsHud.PanelRows(scene.Panel, 10).Skip(2), row => Assert.Equal(DungeonHud.DarkGrey, row.Color));
        scene.OnKey(Keys.D2);
        Assert.Equal(Enchanter.WouldCarryNone("A"), scene.Refusal);
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        Assert.True(scene.HandleEscape());

        // Every slot filled: ALL 5 SLOTS ARE FILLED, and refining is left.
        var full = Sword(wear: 50, cashed: 840, "vampiric", "vampiric", "vampiric", "vampiric", "vampiric");
        var filled = DepositPanel.Open(campaign, TestShop.Give(campaign, "C", 2, full));
        Assert.Equal("1  ENCHANT - ALL 5 SLOTS ARE FILLED", filled.Services[0].Text);
        Assert.False(filled.Services[1].Dimmed);
        Assert.Equal(Enchanter.AllFilled(5), filled.Pick(1));
        Assert.Equal(DepositPanel.Step.Services, filled.Current);

        // Nothing to choose (R-none): a Flaming sword, open to a second enchantment,
        // in a campaign that has met Cold alone — which cannot sit with Flaming, so
        // every offer is refused — and in one that has met nothing. ENCHANT is dimmed
        // with the reason, its key says it, and the offer list never opens; refining
        // is left.
        var start = CampaignState.Start(GameContent.Current);
        foreach (string[] seen in new[] { new[] { "cold" }, [] })
        {
            var met = CampaignState.Restore(start.Party, seen, 0, [], [], [GameContent.Current.Dungeons.Tutorial.Id], lastResult: null, visit: null, stable: []);
            var none = DepositPanel.Open(met, TestShop.Give(met, "B", 2, Sword(wear: 36, cashed: 195, "flaming")));
            Assert.Equal(seen.Length, none.Offers.Count(offer => offer.Refusal is not null));
            Assert.Equal(none.Offers.Count, none.Offers.Count(offer => offer.Refusal is not null));
            Assert.Equal("1  ENCHANT - NOTHING TO OFFER - NO ENCHANTMENT MET YET CAN GO ON IT", none.Services[0].Text);
            Assert.Equal(DungeonHud.DarkGrey, BetweenRunsHud.PanelRows(none, 10)[2].Color);
            Assert.Equal(Enchanter.NothingToOffer, none.Pick(1));
            Assert.Equal((DepositPanel.Step.Services, -1), (none.Current, none.Chosen));
            Assert.False(none.Services[1].Dimmed);
        }

        Assert.Equal(0, host.HubSavesAsked);
        Assert.Empty(campaign.AtTheEnchanter);
    }

    [Fact]
    public void TheOfferListsTheSeen_RefusedOnesDimmedWithTheirReason()
    {
        // §6.2: "you pick the enchantment, from everything your campaign has seen" —
        // the enchanter's offer (Enchanter.Offers), sorted by id as SeenInOrder hands
        // them out (the same on every machine), not in the order they were met: what
        // restricted.json never copies and what does nothing yet are not offered at
        // all; a refused offer is listed, dimmed, its reason naming it (PHASE 6a
        // DECISION 12); one that never fires on the weapon is noted, and may be
        // chosen. Each unrefused one says the mana it locks, at the tier it is
        // attached at. W/S stop only on what can be chosen, round the ends. The
        // choice starts on the first that can be.
        var campaign = TestShop.Opened();   // met: the party's Regeneration and Mire, and Vampiric, Flaming, Cold
        campaign.See(TestWeapons.Enchanted("Met", 40, 10, 30, "serrated", "echoing"));
        var sword = Sword(wear: 36, cashed: 195, "flaming");
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, sword));
        Assert.Equal(["cold", "flaming", "mire", "regeneration", "vampiric"], panel.Offers.Select(offer => offer.Id));
        Assert.Null(panel.Pick(1));
        Assert.Equal((DepositPanel.Step.Offer, 1), (panel.Current, panel.Chosen));
        var rows = BetweenRunsHud.PanelRows(panel, 10);
        Assert.Equal(new[]
        {
            "ENCHANT THE ARMING SWORD - ENCHANTMENT 2 - 2 RUNS IN THE SHOP",
            "COLD - CANNOT SIT WITH FLAMING",
            "> FLAMING - LOCK 20 <",
            "MIRE - LOCK 25 - NEVER FIRES ON A SWORD",
            "REGENERATION - LOCK 15 - NEVER FIRES ON A SWORD",
            "VAMPIRIC - LOCK 20",
        }, Texts(rows));
        Assert.Equal([DungeonHud.White, DungeonHud.DarkGrey, DungeonHud.Yellow, DungeonHud.White, DungeonHud.White, DungeonHud.White], rows.Select(row => row.Color));

        // A refusal and a note are asked apart, and a line carries both.
        Assert.Equal("MIRE - THIS STAFF WOULD CAST IT ON AN ALLY - NEVER FIRES ON A STAFF",
            DepositPanel.OfferLine(new Offer("mire", Enchanter.StaffCastsItOn(GameContent.Current.Enchantments["mire"], TargetSide.Ally), Enchanter.NeverFiresOn(WeaponClass.Staff))));

        // The choice goes round what can be chosen, over the refused.
        var chosen = new List<string>();
        for (int step = 0; step < 4; step++)
        {
            panel.Move(+1);
            chosen.Add(panel.Offers[panel.Chosen].Id);
        }
        Assert.Equal(["mire", "regeneration", "vampiric", "flaming"], chosen);
        panel.Move(-1);
        panel.Move(-1);
        Assert.Equal("regeneration", panel.Offers[panel.Chosen].Id);
        panel.Move(+1);
        panel.Move(+1);
        Assert.Equal("flaming", panel.Offers[panel.Chosen].Id);

        // ENTER chooses, and the confirm asks on the panel's last line: the plan's
        // own ask, the same enchantment again allowed (S:352–354).
        panel.ChooseOffer();
        Assert.Equal((DepositPanel.Step.Confirm, Service.Enchant, "flaming"), (panel.Current, panel.Service, panel.EnchantmentId));
        Assert.Equal("DEPOSIT THE ARMING SWORD TO ENCHANT FLAMING - 2 RUNS - ITS 36 WEAR IS SPENT", panel.Ask);
        var confirm = BetweenRunsHud.PanelRows(panel, 10);
        Assert.Equal(["> FLAMING - LOCK 20 <", panel.Ask], Texts(confirm).Where(text => text.StartsWith('>') || text.StartsWith("DEPOSIT", StringComparison.Ordinal)));
        Assert.Equal(DungeonHud.Yellow, confirm[^1].Color);
        Assert.Equal("DEPOSIT THE ARMING SWORD TO REFINE - 2 RUNS - ITS 36 WEAR IS SPENT", DepositPanel.DepositAsk(sword, Service.Refine, null));

        // Deposited as asked: the offer chosen is what the shop holds.
        var made = Enchanter.Deposit(campaign, panel.From!.Value, panel.Service, panel.EnchantmentId);
        Assert.Equal((Service.Enchant, "flaming", 36), (made.Entry.Service, made.Entry.EnchantmentId, made.Entry.WearAtDeposit));
    }

    [Fact]
    public void TheOfferListScrollsWithTheChoiceInView()
    {
        // What a campaign has met only grows, so the offer list is a window that
        // scrolls with the choice, as the WEAPONS page's rows do: with every entry
        // of the catalogue met, and the most notice rows the screen can lead over
        // it, the list runs past its window on a 720-pixel window, and every choice
        // W/S make is in view — and the panel, the confirm's ask included, ends
        // clear of the refusal row.
        var campaign = TestShop.Opened();
        foreach (var chunk in GameContent.Current.Enchantments.All.Select(def => def.Id).Chunk(5))
            campaign.See(TestWeapons.Enchanted("Met", 40, 10, 30, chunk));
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, TestShop.Worn("tower_guard", wear: 30)));
        var place = BetweenRunsHud.PanelPlace(720f, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1);
        Assert.True(panel.Offers.Count > place.OfferRows, $"{panel.Offers.Count} offers fit the window of {place.OfferRows}: nothing scrolls");
        Assert.Equal(GameContent.Current.Enchantments.All.Count(def => !GameContent.Current.Enchantments.NeverRolled.Contains(def.Id) && !LootTable.IsInert(def)),
            panel.Offers.Count);

        Assert.Null(panel.Pick(1));
        int choosable = panel.Offers.Count(offer => offer.Refusal is null);
        var seen = new HashSet<int>();
        for (int step = 0; step < 2 * choosable + 1; step++)
        {
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            int top = panel.Window(place.OfferRows);
            Assert.InRange(panel.Chosen, top, top + place.OfferRows - 1);
            Assert.Contains($"> {DepositPanel.OfferLine(panel.Offers[panel.Chosen])} <", Texts(rows));
            Assert.Equal(1 + Math.Min(place.OfferRows, panel.Offers.Count), rows.Count);
            Ends(rows);
            seen.Add(panel.Chosen);
            panel.Move(step < choosable ? +1 : -1);
        }
        Assert.Equal(choosable, seen.Count);

        panel.ChooseOffer();
        var confirm = BetweenRunsHud.PanelRows(panel, place.OfferRows);
        Assert.Equal(panel.Ask, confirm[^1].Text);
        Ends(confirm);

        void Ends(IReadOnlyList<BetweenRunsHud.ListRow> rows)
        {
            float y = place.Top + rows.Take(rows.Count - 1).Sum(row => row.Pitch);
            Assert.True(y + 7f * rows[^1].Scale <= place.Bottom, $"the panel's last line ends at {y + 7f * rows[^1].Scale}, past {place.Bottom}");
        }
    }

    [Fact]
    public void OnlyEnterDeposits_EscBacksOutAStepAtATime()
    {
        // The panel is the screen's alone, like a carry: nothing is deposited until
        // the confirm's ENTER — no other key, on no step, changes the campaign or
        // asks for a save — and ESC goes back a step at a time: from the confirm to
        // the question it answered, from the offer list to the services, and from
        // the services out, nothing done.
        var campaign = TestShop.Opened();
        var guard = TestShop.Worn("tower_guard", wear: 30);
        TestShop.Give(campaign, "B", 2, guard);
        string before = Save(campaign);
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  BAG   TOWER GUARD - WEAR 30/30 FULL");
        Keys[] idle = [Keys.Tab, Keys.D, Keys.T, Keys.D3, Keys.D0, Keys.N, Keys.Space, Keys.Backspace];

        scene.OnKey(Keys.D);
        foreach (var key in idle.Concat([Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S]))
            scene.OnKey(key);
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        scene.OnKey(Keys.D1);
        Assert.Equal(DepositPanel.Step.Offer, scene.Panel!.Current);
        foreach (var key in idle.Concat([Keys.D1, Keys.D2]))
            scene.OnKey(key);
        scene.OnKey(Keys.S);
        int chosen = scene.Panel!.Chosen;
        scene.OnKey(Keys.Enter);
        Assert.Equal(DepositPanel.Step.Confirm, scene.Panel!.Current);
        foreach (var key in idle.Concat([Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D1, Keys.D2]))
            scene.OnKey(key);
        Assert.Equal((DepositPanel.Step.Confirm, chosen), (scene.Panel!.Current, scene.Panel.Chosen));
        Assert.Equal((before, 0), (Save(campaign), host.HubSavesAsked));

        // ESC: to the offer list, the choice kept; to the services; REFINE's confirm
        // backs out to the services; and from the services the panel closes, the
        // page as it was, the menu not opened.
        Assert.True(scene.HandleEscape());
        Assert.Equal((DepositPanel.Step.Offer, chosen), (scene.Panel!.Current, scene.Panel.Chosen));
        Assert.True(scene.HandleEscape());
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        scene.OnKey(Keys.D2);
        Assert.Equal(DepositPanel.Step.Confirm, scene.Panel!.Current);
        Assert.True(scene.HandleEscape());
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null, false), (scene.Mode, scene.Panel, scene.MenuOpen));
        Assert.Equal("  BAG   TOWER GUARD - WEAR 30/30 FULL", scene.Weapons.ChosenRow.Text);
        Assert.Equal((before, 0), (Save(campaign), host.HubSavesAsked));

        // ENTER on the confirm deposits: the pool cashed whole, the capacity grown on
        // it, the weapon in the shop, the choice on its row there, a save asked for.
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        Assert.Equal(BetweenRunsScene.HubMode.Weapons, scene.Mode);
        var deposit = Assert.Single(campaign.AtTheEnchanter);
        Assert.Equal((guard, Service.Refine, 30, 1, 0), (deposit.Weapon, deposit.Service, deposit.WearAtDeposit, deposit.Runs, deposit.Ordinal));
        Assert.Equal((0, 31), (guard.Wear, guard.WearCapacity));
        Assert.Null(campaign.Party[1].Inventory[2]);
        Assert.Equal("  WORKING  TOWER GUARD - 0 OF 1 RUN", scene.Weapons.ChosenRow.Text);
        Assert.Equal(1, host.HubSavesAsked);
        Assert.NotEqual(before, Save(campaign));
    }

    [Fact]
    public void TakingBackAsksFirst_AndSaysTheWearComesBack()
    {
        // PHASE 6a DECISION 8: T on a weapon in the shop asks — its service lost,
        // the wear its deposit cashed coming back — and only ENTER takes it back:
        // to the stable's end, its wear and its capacity as they were before it went
        // in. ESC answers no, and the weapon stays in the shop.
        var campaign = TestShop.Opened();
        var guard = TestShop.Worn("tower_guard", wear: 30);
        Enchanter.Deposit(campaign, TestShop.Give(campaign, "B", 2, guard), Service.Enchant, "vampiric");
        Assert.Equal((0, 31), (guard.Wear, guard.WearCapacity));
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  WORKING  TOWER GUARD - 0 OF 1 RUN");
        scene.OnKey(Keys.T);
        Assert.Equal((BetweenRunsScene.HubMode.Ask, DepositPanel.Step.Ask), (scene.Mode, scene.Panel!.Current));
        Assert.Equal("TAKE BACK THE TOWER GUARD - ITS SERVICE IS LOST - ITS 30 WEAR COMES BACK", scene.Panel.Ask);
        Assert.Equal(new[]
        {
            ("TOWER GUARD", DungeonHud.White),
            ("ENCHANT VAMPIRIC - 0 OF 1 RUN", DungeonHud.Cyan),
            ("TAKE BACK THE TOWER GUARD - ITS SERVICE IS LOST - ITS 30 WEAR COMES BACK", DungeonHud.Yellow),
        }, BetweenRunsHud.PanelRows(scene.Panel, 10).Select(row => (row.Text, row.Color)));
        Assert.Equal("ENTER: TAKE IT BACK - ESC: NO", BetweenRunsHud.BottomLine(scene.Mode, panel: scene.Panel).Text);

        // No: the weapon stays in the shop, nothing asked of the host.
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Same(guard, Assert.Single(campaign.AtTheEnchanter).Weapon);
        Assert.Equal(0, host.HubSavesAsked);

        // Yes: back in the stable, full again at 30 of 30, the choice on it.
        scene.OnKey(Keys.T);
        scene.OnKey(Keys.Enter);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Same(guard, Assert.Single(campaign.Stable));
        Assert.Equal((30, 0, 30), (guard.Wear, guard.WearCashed, guard.WearCapacity));
        Assert.Equal("  TOWER GUARD - WEAR 30/30 FULL", scene.Weapons.ChosenRow.Text);
        Assert.Equal(1, host.HubSavesAsked);

        // A stalled deposit is taken back the same way: T on any row in the shop.
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        TestShop.Give(campaign, "C", 2, TestShop.Worn("tower_guard", wear: 12));
        ChooseRow(scene, "  BAG   TOWER GUARD - WEAR 12/30");
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        ChooseRow(scene, "  STALLED  TOWER GUARD - 0 OF 1 RUN");
        Assert.True(scene.Weapons.ChosenRow.Stalled);
        scene.OnKey(Keys.T);
        Assert.Equal("TAKE BACK THE TOWER GUARD - ITS SERVICE IS LOST - ITS 30 WEAR COMES BACK", scene.Panel!.Ask);
        scene.OnKey(Keys.Enter);
        Assert.Equal(2, campaign.AtTheEnchanter.Count);
        Assert.Equal([2, 3], campaign.AtTheEnchanter.Select(deposit => deposit.Ordinal));   // the two worked stay; 0 and 1, taken back, are never reused
        Assert.Same(guard, campaign.Stable[^1]);
        Assert.Equal(30, guard.Wear);
    }

    [Fact]
    public void AnEnterOnAPanelTheCampaignChangedUnder_SaysWhyAndDoesNothing()
    {
        // The panel reads the campaign as it opens, and the ENTER that answers it
        // asks again before it acts (DepositPanel.RefusalNow; 6a.5's review).
        // Nothing changes the campaign while a panel is up yet, but should a later
        // hub action, a confirm or an ask left up over the change says why over the
        // keys and closes — nothing deposited, nothing taken back, no save asked for,
        // and nothing thrown through the enchanter's doors, which refuse by throwing.
        var campaign = TestShop.Opened();
        var guard = TestShop.Worn("tower_guard", wear: 30);
        TestShop.Give(campaign, "B", 2, guard);
        var (scene, host) = Weapons(campaign);

        // The weapon gone from where the panel found it.
        ChooseRow(scene, "  BAG   TOWER GUARD - WEAR 30/30 FULL");
        foreach (var key in new[] { Keys.D, Keys.D1, Keys.Enter })
            scene.OnKey(key);
        Assert.Equal(DepositPanel.Step.Confirm, scene.Panel!.Current);
        campaign.Move(new WeaponPlace("B", 2), WeaponPlace.InTheStable(0));
        scene.OnKey(Keys.Enter);
        Assert.Equal("THE TOWER GUARD HAS MOVED - NOTHING DEPOSITED", scene.Refusal);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Equal((30, 30), (guard.Wear, guard.WearCapacity));

        // Where it lay still, but a gate shut since: B's bag emptied into the stable,
        // the Tower Guard in B's hand is B's last weapon, and the enchanter's own
        // reason is said.
        ChooseRow(scene, "  HAND  TOWER GUARD - WEAR 0/30");
        foreach (var key in new[] { Keys.D, Keys.D2 })
            scene.OnKey(key);
        Assert.Equal(DepositPanel.Step.Confirm, scene.Panel!.Current);
        campaign.Move(new WeaponPlace("B", 1), WeaponPlace.InTheStable(campaign.Stable.Count));
        scene.OnKey(Keys.Enter);
        Assert.Equal(Enchanter.WouldCarryNone("B"), scene.Refusal);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Empty(campaign.AtTheEnchanter);

        // The deposit an ask is about gone from the shop.
        var deposit = Enchanter.Deposit(campaign, WeaponPlace.InTheStable(0), Service.Refine).Entry;
        ChooseRow(scene, "  WORKING  TOWER GUARD - 0 OF 1 RUN");
        scene.OnKey(Keys.T);
        Assert.Equal(BetweenRunsScene.HubMode.Ask, scene.Mode);
        Enchanter.TakeBack(campaign, deposit);
        scene.OnKey(Keys.Enter);
        Assert.Equal("THE TOWER GUARD IS NOT IN THE SHOP - NOTHING TAKEN BACK", scene.Refusal);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Single(campaign.Stable, weapon => ReferenceEquals(weapon, guard));
        Assert.Equal((30, 30), (guard.Wear, guard.WearCapacity));
        Assert.Equal(0, host.HubSavesAsked);

        // Asked of the campaign as it stands, a panel nothing changed under lets its
        // ENTER through; and it is asked only where an ENTER answers the panel.
        var open = DepositPanel.Open(campaign, WeaponPlace.InTheStable(0));
        Assert.Throws<InvalidOperationException>(() => open.RefusalNow(campaign));
        open.Pick(2);
        Assert.Null(open.RefusalNow(campaign));
    }

    [Fact]
    public void ADepositAndATakeBackEachAskForASave()
    {
        // PHASE 6a DECISION 10: every change the hub makes to the campaign asks the
        // host for a save, at the frame's end, into the campaign's own slot — a
        // deposit once, a take-back once — and nothing else the panel or the ask does
        // asks for one: opening it, a service or an offer chosen, a dimmed key, ESC.
        var campaign = TestShop.Opened();
        TestShop.Give(campaign, "B", 2, TestShop.Worn("tower_guard", wear: 30));
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  BAG   TOWER GUARD - WEAR 30/30 FULL");
        foreach (var key in new[] { Keys.D, Keys.D1, Keys.S, Keys.Enter })
            scene.OnKey(key);
        Assert.True(scene.HandleEscape());
        Assert.True(scene.HandleEscape());
        Assert.True(scene.HandleEscape());
        Assert.Equal(0, host.HubSavesAsked);

        foreach (var key in new[] { Keys.D, Keys.D1, Keys.Enter, Keys.Enter })
            scene.OnKey(key);
        Assert.Equal((1, 1), (host.HubSavesAsked, campaign.AtTheEnchanter.Count));
        scene.OnKey(Keys.T);
        Assert.True(scene.HandleEscape());
        Assert.Equal(1, host.HubSavesAsked);
        scene.OnKey(Keys.T);
        scene.OnKey(Keys.Enter);
        Assert.Equal((2, 0), (host.HubSavesAsked, campaign.AtTheEnchanter.Count));
        Assert.Equal(0, host.HandSaves);
    }

    [Fact]
    public void TheDepositAndTheTakeBackAreLoggedWithTheirOrdinal()
    {
        // The plan's 1h: Logic never logs, so the screen writes what the enchanter
        // hands back — a deposit with the wear it cashed, the capacity before and
        // after, its runs and its ordinal (so a finished service's improvement roll,
        // logged at the run's end, can be keyed again), and a hand it emptied; a
        // take-back with what it forfeited and what came back, the capacity on either
        // side of it as the enchanter hands them back (TakenBack). Written by the two
        // hub doors that make them, read off the IL.
        var campaign = TestShop.Opened();
        var guard = TestShop.Worn("tower_guard", wear: 30);
        var hand = TestShop.Give(campaign, "B", 0, guard);
        var made = Enchanter.Deposit(campaign, hand, Service.Enchant, "vampiric");
        Assert.Equal("[Enchanter] Deposited Tower Guard (tower_guard) from B's hand: Enchant vampiric, 30 wear cashed (capacity 30 -> 31), 1 run, deposit #0;"
            + " B's hand, emptied, took up Staff of Renewal from the bag", BetweenRunsScene.DepositedLine(made, hand, "B's hand"));
        var taken = Enchanter.TakeBack(campaign, made.Entry);
        Assert.Equal((made.Entry, 31, 30), (taken.Entry, taken.Before.Max, taken.After.Max));
        Assert.Equal("[Enchanter] Took back Tower Guard (tower_guard) to the stable's end: Enchant vampiric forfeited, 0 of 1 run worked lost, 30 wear returned (capacity 31 -> 30), deposit #0",
            BetweenRunsScene.TookBackLine(taken));

        // A refinement from the stable, worked a run of two before it is taken back.
        var staff = TestShop.Worn("staff_of_renewal", wear: 35, cashed: 160);
        var bag = TestShop.Give(campaign, "C", 2, staff);
        var refined = Enchanter.Deposit(campaign, bag, Service.Refine);
        Assert.Equal("[Enchanter] Deposited Staff of Renewal (staff_of_renewal) from C's bag slot 3: Refine, 35 wear cashed (capacity 35 -> 36), 2 runs, deposit #1",
            BetweenRunsScene.DepositedLine(refined, bag, "C's bag slot 3"));
        TestShop.Counted(campaign);
        var worked = Assert.Single(campaign.AtTheEnchanter);
        Assert.Equal(1, worked.RunsWorked);
        Assert.Equal("[Enchanter] Took back Staff of Renewal (staff_of_renewal) to the stable's end: Refine forfeited, 1 of 2 runs worked lost, 35 wear returned (capacity 36 -> 35), deposit #1",
            BetweenRunsScene.TookBackLine(Enchanter.TakeBack(campaign, worked)));

        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static bool Calls(string method, string target)
            => CallsIn(typeof(BetweenRunsScene).GetMethod(method, Declared)!).Any(called => called.Name == target && called.DeclaringType == typeof(BetweenRunsScene));
        Assert.True(Calls("MakeDeposit", nameof(BetweenRunsScene.DepositedLine)));
        Assert.True(Calls("TakeBack", nameof(BetweenRunsScene.TookBackLine)));

        // Walked through the screen, the lines are what it logs: a deposit from the
        // stable's middle names the place as it stood before the deposit closed the
        // stable up — 2 of 3, never 2 of 2 — and its take-back the capacity on either
        // side, 31 to 30.
        var walked = TestShop.Opened();
        walked.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        walked.Move(TestShop.Give(walked, "C", 2, TestShop.Worn("tower_guard", wear: 30)), WeaponPlace.InTheStable(1));
        walked.Move(new WeaponPlace("D", 1), WeaponPlace.InTheStable(2));
        var (scene, _) = Weapons(walked);
        ChooseRow(scene, "  TOWER GUARD - WEAR 30/30 FULL");
        foreach (var key in new[] { Keys.D, Keys.D2, Keys.Enter })
            scene.OnKey(key);
        Assert.Contains("[Enchanter] Deposited Tower Guard (tower_guard) from the stable (2 of 3): Refine, 30 wear cashed (capacity 30 -> 31), 1 run, deposit #0", Logged());
        foreach (var key in new[] { Keys.T, Keys.Enter })
            scene.OnKey(key);
        Assert.Contains("[Enchanter] Took back Tower Guard (tower_guard) to the stable's end: Refine forfeited, 0 of 1 run worked lost, 30 wear returned (capacity 31 -> 30), deposit #0", Logged());

        static IEnumerable<string> Logged() => Log.RecentEntries.Select(entry => entry.Message);
    }

    [Fact]
    public void EveryPanelStringFitsA1280Window_InTheHudsFont()
    {
        // Every string the deposit panel draws runs across the window, centred over
        // the dimmed page, and fits the shipped 1280 with 20 px either side at its
        // own scale, in the engine's glyphs (no percent sign, no apostrophe). The
        // worst case: the longest weapon and enchantment names the content ships,
        // four-digit wear and capacity, ten slots, the two-digit runs a ninth
        // enchantment takes and the one run a first does, every gate's reason and
        // every offer's — a staff's side, a throwing weapon's note, a refusal and a
        // note on one line — and the asks.
        using var numbers = TestContent.UsePremise(tuning: t => TestShop.Ruled(t) with
        {
            StartingPool = 9999, EnchantmentSlots = 10, FirstSlotCapacity = 9999, SlotCapacityStep = 9999, FirstEnchantmentWear = 9999, EnchantmentWearStep = 9999,
        });
        var content = GameContent.Current;
        var longest = content.Weapons.All.MaxBy(def => def.Name.Length)!;
        var soul = content.Enchantments.All.MaxBy(def => def.Name.Length)!;
        var made = content.Weapons.Instantiate(longest.Id, longest.Class == WeaponClass.Wand ? DamageType.Shocking : null);
        Weapon Laden(int count) => new(longest, made.Forged, made.Modifiers, Enumerable.Range(0, count).Select(_ => new Enchantment(soul, 1)).ToArray(), 0, wear: 9999, wearCashed: 9998);
        var nine = Laden(9);
        var ten = Laden(10);
        var bare = Laden(0);
        Assert.Equal((9999, 9999), (nine.Wear, nine.WearCapacity));

        var lines = new List<(string Text, float Scale)>();
        void Line(string text, float scale) => lines.Add((text, scale));

        foreach (var weapon in new[] { nine, ten, bare })
        {
            Line(DepositPanel.Heading(weapon), BetweenRunsHud.PanelHeadingScale);
            Line(DepositPanel.StateLine(weapon), BetweenRunsHud.PanelStateScale);
            Line(DepositPanel.OfferHeading(weapon), BetweenRunsHud.PanelOfferHeadingScale);
            foreach (var service in new[] { Service.Enchant, Service.Refine })
                Line($"> {DepositPanel.DepositAsk(weapon, service, service == Service.Enchant ? soul.Id : null)} <", BetweenRunsHud.PanelAskScale);
        }
        Assert.Contains(lines, line => line.Text.Contains(" - 1 RUN ", StringComparison.Ordinal) || line.Text.EndsWith(" - 1 RUN IN THE SHOP", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains(" - 10 RUNS", StringComparison.Ordinal));
        Line(DepositPanel.DepositAsk(content.Weapons.Instantiate(longest.Id, longest.Class == WeaponClass.Wand ? DamageType.Shocking : null), Service.Refine, null), BetweenRunsHud.PanelAskScale);

        string[] reasons =
        [
            Enchanter.Closed(content.Dungeons.Tutorial), Enchanter.InTheShop(nine), Enchanter.WouldCarryNone("A"), Enchanter.AllFilled(10),
            Enchanter.NoFreeSlot(10, Wear.SlotOpensAt(10), 9999), Enchanter.Short(10, Wear.Price(10), 9999), Enchanter.NothingToOffer,
        ];
        foreach (var service in new[] { Service.Enchant, Service.Refine })
        {
            string does = service == Service.Enchant ? DepositPanel.EnchantDoes : DepositPanel.RefineDoes;
            foreach (string reason in reasons)
                Line(new DepositPanel.Option(1, service, does, reason, null, null).Text, BetweenRunsHud.PanelOptionScale);
            foreach (string odds in new[] { "99.9 IN 100 TO IMPROVE", DepositPanel.Odds(service, 9999) })
                Line($"> {new DepositPanel.Option(2, service, does, null, odds, null).Text} <", BetweenRunsHud.PanelOptionScale);
            Line($"> {new DepositPanel.Option(2, service, does, null, null, DepositPanel.NothingToRepair).Text} <", BetweenRunsHud.PanelOptionScale);
        }

        string?[] refusals =
        [
            null, Enchanter.CannotSitWith(soul, soul), Enchanter.NeedsAhead(soul, soul.Name), Enchanter.WandCastsNoStatus(soul),
            Enchanter.StaffCastsItOn(soul, TargetSide.Ally), Enchanter.StaffCastsItOn(soul, TargetSide.Enemy),
        ];
        foreach (string? refusal in refusals)
            foreach (string? note in Enum.GetValues<WeaponClass>().Select(Enchanter.NeverFiresOn).Prepend(null))
                Line($"> {DepositPanel.OfferLine(new Offer(soul.Id, refusal, note))} <", BetweenRunsHud.PanelOfferScale);
        Assert.Contains(lines, line => line.Text.Contains("THIS STAFF WOULD CAST IT ON AN ENEMY - NEVER FIRES ON A THROWING WEAPON", StringComparison.Ordinal));

        var deposit = new Deposit(999, nine, Service.Enchant, 9999, ContentBounds.MostServiceRuns, soul.Id) { RunsWorked = ContentBounds.MostServiceRuns - 1 };
        Line(DepositPanel.TakeBackAsk(deposit), BetweenRunsHud.PanelAskScale);
        Line(DepositPanel.TakeBackAsk(new Deposit(0, bare, Service.Refine, 0, 1)), BetweenRunsHud.PanelAskScale);
        Line(BetweenRunsHud.ServiceLine(deposit), BetweenRunsHud.PanelStateScale);
        Line(HubWeapons.Name(nine), BetweenRunsHud.PanelHeadingScale);
        foreach (string keys in new[] { DepositPanel.ServicesKeys, DepositPanel.OfferKeys, DepositPanel.ConfirmKeys, DepositPanel.AskKeys, BetweenRunsHud.WeaponsKeysLine })
            Line(keys, BetweenRunsHud.KeysLineScale);
        foreach (string refused in new[] { DepositPanel.Moved(nine), DepositPanel.NotInTheShop(nine) })   // the refusal row's, over the keys
            Line(refused, BetweenRunsHud.KeysLineScale);

        foreach (var (text, scale) in lines)
        {
            Assert.True(text.Length * 6f * scale <= BetweenRunsHud.FullWidth, $"'{text}' is {text.Length * 6f * scale} px, past {BetweenRunsHud.FullWidth}");
            Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]*$", text);
        }

        // The panels as the screen builds them, from these weapons, fit as the lines
        // do, and their height the room under the most notices.
        var campaign = TestShop.Opened();
        var place = BetweenRunsHud.PanelPlace(720f, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1);
        foreach (var weapon in new[] { nine, ten, bare })
        {
            var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, weapon));
            panel.Pick(2);
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            Assert.All(rows, row => Assert.True(row.Text.Length * 6f * row.Scale <= BetweenRunsHud.FullWidth, row.Text));
            Assert.True(place.Top + rows.Take(rows.Count - 1).Sum(row => row.Pitch) + 7f * rows[^1].Scale <= place.Bottom);
        }
    }
}
