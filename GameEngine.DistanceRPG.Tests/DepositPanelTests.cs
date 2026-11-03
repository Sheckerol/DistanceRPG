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
        // 100, whole when exact and otherwise to a place, cut short. MOVE (Phase
        // 6a.6) has nothing to take in the game's own party — every other soul is a
        // staff's only one, which would leave it casting nothing, and the sword's own
        // is last already — so it is dimmed, and says so.
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
            "3  MOVE AN ENCHANTMENT ONTO IT - NOTHING TO MOVE - NO ENCHANTMENT THE PARTY OWNS CAN GO ON IT",
        }, Texts(BetweenRunsHud.PanelRows(panel, 10)));
        Assert.Equal([DungeonHud.White, DungeonHud.Grey, DungeonHud.White, DungeonHud.White, DungeonHud.DarkGrey], BetweenRunsHud.PanelRows(panel, 10).Select(row => row.Color));
        Assert.All(panel.Services.Take(2), option => Assert.False(option.Dimmed));
        Assert.Equal([Service.Enchant, Service.Refine, Service.Transfer], panel.Services.Select(option => option.Service));
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
                certain.Services.Take(2).Select(option => option.Text));
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
        scene.OnKey(Keys.D4);   // no service is on 4: nothing, and the reason goes with the key
        Assert.Null(scene.Refusal);
        Assert.Equal(DepositPanel.Step.Services, scene.Panel!.Current);
        Assert.True(scene.HandleEscape());

        // A member's last weapon (PHASE 6a DECISION 7): every service refused alike.
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        scene.OnKey(Keys.D);
        Assert.Equal(new[]
        {
            "1  ENCHANT - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST",
            "2  REFINE - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST",
            "3  MOVE AN ENCHANTMENT ONTO IT - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST",
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
        // attached at. The choice starts on the first that can be chosen; W/S go a
        // line at a time round the ends, a refused one included, whose ENTER says why
        // and chooses nothing (the audit of Phase 6a: passed over, a refused line
        // beyond the window at either end of a long list was never shown).
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

        // The choice goes round every line, a line at a time, the refused one too.
        var chosen = new List<string>();
        for (int step = 0; step < 5; step++)
        {
            panel.Move(+1);
            chosen.Add(panel.Offers[panel.Chosen].Id);
        }
        Assert.Equal(["mire", "regeneration", "vampiric", "cold", "flaming"], chosen);
        panel.Move(-1);
        panel.Move(-1);
        Assert.Equal("vampiric", panel.Offers[panel.Chosen].Id);

        // Chosen, a refused line is marked and stays dimmed, a shade lighter; ENTER
        // on it hands back its reason as it names it, for the refusal row, and
        // chooses nothing.
        panel.Move(+1);
        Assert.Equal("cold", panel.Offers[panel.Chosen].Id);
        var onCold = BetweenRunsHud.PanelRows(panel, 10);
        Assert.Equal(("> COLD - CANNOT SIT WITH FLAMING <", DungeonHud.Grey), (onCold[1].Text, onCold[1].Color));
        Assert.Equal("COLD - CANNOT SIT WITH FLAMING", panel.ChooseOffer());
        Assert.Equal((DepositPanel.Step.Offer, (string?)null), (panel.Current, panel.Ask));
        panel.Move(+1);
        Assert.Equal("flaming", panel.Offers[panel.Chosen].Id);

        // ENTER chooses, and the confirm asks on the panel's last line: the plan's
        // own ask, the same enchantment again allowed (S:352–354).
        Assert.Null(panel.ChooseOffer());
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
        // it, the list runs past its window on a 720-pixel window, and every line —
        // each a choice W/S make in turn, a refused one too — is in view as it is
        // chosen; and the panel, the confirm's ask included, ends clear of the
        // refusal row.
        var campaign = TestShop.Opened();
        foreach (var chunk in GameContent.Current.Enchantments.All.Select(def => def.Id).Chunk(5))
            campaign.See(TestWeapons.Enchanted("Met", 40, 10, 30, chunk));
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, TestShop.Worn("tower_guard", wear: 30)));
        var place = BetweenRunsHud.PanelPlace(720f, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1);
        Assert.True(panel.Offers.Count > place.OfferRows, $"{panel.Offers.Count} offers fit the window of {place.OfferRows}: nothing scrolls");
        Assert.Equal(GameContent.Current.Enchantments.All.Count(def => !GameContent.Current.Enchantments.NeverRolled.Contains(def.Id) && !LootTable.IsInert(def)),
            panel.Offers.Count);

        Assert.Null(panel.Pick(1));
        int listed = panel.Offers.Count;
        var seen = new HashSet<int>();
        for (int step = 0; step < 2 * listed + 1; step++)
        {
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            int top = panel.Window(place.OfferRows);
            Assert.InRange(panel.Chosen, top, top + place.OfferRows - 1);
            Assert.Contains($"> {DepositPanel.OfferLine(panel.Offers[panel.Chosen])} <", Texts(rows));
            Assert.Equal(1 + Math.Min(place.OfferRows, panel.Offers.Count), rows.Count);
            Ends(rows);
            seen.Add(panel.Chosen);
            panel.Move(step < listed ? +1 : -1);
        }
        Assert.Equal(listed, seen.Count);

        while (panel.Offers[panel.Chosen].Refusal is not null)
            panel.Move(+1);
        Assert.Null(panel.ChooseOffer());
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
    public void TheMoveListSaysTheArrivingTierAndEveryRefusal()
    {
        // §6.5 (Phase 6a.6): 3 MOVE AN ENCHANTMENT ONTO IT lists every soul on every
        // other weapon the party owns out of the shop — each member's in roster and
        // slot order, then the stable's — and then the weapon's own, each saying where
        // it lies, the tier it arrives at and the runs; a refused one dimmed with its
        // reason, never hidden (PHASE 6a DECISION 12): a staff's or a wand's only soul
        // (it would cast nothing), a Flaming that would strand the Burning behind it,
        // a Cold that cannot sit with the Flaming the sword carries, and the sword's
        // own Flaming, last already. Where it lies names whose hand or bag, or the
        // stable, so the party's three Staffs of Renewal are told apart (the audit of
        // Phase 6a). W/S go a line at a time, a refused one included; ENTER chooses,
        // and ENTER again deposits both weapons as one, logged and saved.
        var campaign = TestShop.Opened();
        var sword = Sword(wear: 36, cashed: 195, "flaming");   // capacity 36: a second enchantment's slot and price met
        TestShop.Give(campaign, "B", 2, sword);
        var knife = TestWeapons.Get("flensing_knife_unique");
        TestShop.Give(campaign, "C", 2, knife);
        TestShop.Give(campaign, "C", 3, TestShop.Worn("wand_of_the_nova_unique", 0));
        TestShop.Give(campaign, "D", 2, TestWeapons.Get("rotwood"));
        TestShop.Give(campaign, "D", 3, TestShop.Souled("wand_of_the_cone", 0, 0, ("cold", 1)));
        var panel = DepositPanel.Open(campaign, new WeaponPlace("B", 2));
        Assert.Equal("3  MOVE AN ENCHANTMENT ONTO IT - ONE TIER LOST - 3 IN 100 TO IMPROVE", panel.Services[2].Text);
        Assert.Null(panel.Pick(3));
        Assert.Equal((DepositPanel.Step.Sources, Service.Transfer, DepositPanel.OfferKeys), (panel.Current, panel.Service, panel.KeysLine));
        var rows = BetweenRunsHud.PanelRows(panel, 20);
        Assert.Equal(new[]
        {
            "MOVE AN ENCHANTMENT ONTO THE ARMING SWORD - BOTH WEAPONS STAY IN THE SHOP",
            "REGENERATION ON THE STAFF OF RENEWAL IN THE BAG OF [A] - THE STAFF OF RENEWAL WOULD CAST NOTHING",
            "REGENERATION ON THE STAFF OF RENEWAL IN THE BAG OF [B] - THE STAFF OF RENEWAL WOULD CAST NOTHING",
            "REGENERATION ON THE STAFF OF RENEWAL IN THE BAG OF [C] - THE STAFF OF RENEWAL WOULD CAST NOTHING",
            "> SERRATED ON THE NAMELESS KNIFE IN THE BAG OF [C] - ARRIVES AT T1 - 2 RUNS <",
            "VAMPIRIC T3 ON THE NAMELESS KNIFE IN THE BAG OF [C] - ARRIVES AT T2 - 2 RUNS",
            "OVERHEAL ON THE NAMELESS KNIFE IN THE BAG OF [C] - ARRIVES AT T1 - 2 RUNS",
            "FLAMING ON THE NAMELESS NOVA IN THE BAG OF [C] - BURNING NEEDS FLAMING AHEAD OF IT",
            "BURNING ON THE NAMELESS NOVA IN THE BAG OF [C] - ARRIVES AT T1 - 2 RUNS",
            "REGENERATION ON THE STAFF OF RENEWAL IN THE HAND OF [D] - THE STAFF OF RENEWAL WOULD CAST NOTHING",
            "MIRE ON THE STAFF OF MIRE IN THE BAG OF [D] - THE STAFF OF MIRE WOULD CAST NOTHING",
            "POISON T3 ON THE ROTWOOD IN THE BAG OF [D] - THE ROTWOOD WOULD CAST NOTHING",
            "COLD ON THE WAND OF THE CONE IN THE BAG OF [D] - CANNOT SIT WITH FLAMING",
            "FLAMING - ITS OWN - IT IS LAST ALREADY",
        }, Texts(rows));
        Assert.Equal(DungeonHud.Yellow, rows[4].Color);
        Assert.All(rows.Skip(1).Where(row => row.Text.Contains("WOULD", StringComparison.Ordinal) || row.Text.Contains("CANNOT", StringComparison.Ordinal)),
            row => Assert.Equal(DungeonHud.DarkGrey, row.Color));
        Assert.Equal(DungeonHud.White, rows[5].Color);

        // A soul that would never fire on the weapon is chosen all the same, noted.
        Assert.Equal("REGENERATION ON THE STAFF OF RENEWAL IN THE BAG OF [A] - ARRIVES AT T1 - 2 RUNS - NEVER FIRES ON A SWORD",
            DepositPanel.MoveLine(new TransferOffer(new WeaponPlace("A", 1), campaign.Party[0].WeaponIn(1), 0, campaign.Party[0].WeaponIn(1)!.Enchantments[0], 1, 2, null, Enchanter.NeverFiresOn(WeaponClass.Sword))));
        // A reorder moves one of two souls at the least (the last is last already), so
        // it takes 2 runs at the least (PHASE 6a DECISION 11).
        var own = TestShop.Opened();
        var two = TestShop.Give(own, "B", 2, Sword(wear: 35, cashed: 160, "flaming", "vampiric"));
        Assert.Equal("FLAMING - ITS OWN - MOVES TO THE END - ARRIVES AT T1 - 2 RUNS",
            DepositPanel.MoveLine(Enchanter.TransferOffers(own, own.WeaponAt(two)!).Single(move => move.Source is null && move.Index == 0)));

        // The heading's tail is the choice's (the audit of Phase 6a): both weapons
        // stay in the shop while the soul chosen is on another weapon, and a reorder,
        // which puts the weapon in the shop alone, is headed with the weapon alone —
        // over the list and over its confirm.
        var reorder = DepositPanel.Open(own, two);
        Assert.Null(reorder.Pick(3));
        Assert.Equal((null, "flaming"), (reorder.ChosenMove!.Source, reorder.ChosenMove.Soul.Id));   // the party's staffs cast nothing without theirs
        Assert.Equal("MOVE AN ENCHANTMENT ONTO THE ARMING SWORD", BetweenRunsHud.PanelRows(reorder, 20)[0].Text);
        reorder.Move(-1);
        Assert.NotNull(reorder.ChosenMove!.Source);
        Assert.Equal("MOVE AN ENCHANTMENT ONTO THE ARMING SWORD - BOTH WEAPONS STAY IN THE SHOP", BetweenRunsHud.PanelRows(reorder, 20)[0].Text);
        reorder.Move(+1);
        Assert.Null(reorder.ChooseOffer());
        var reordering = BetweenRunsHud.PanelRows(reorder, 20);
        Assert.Equal(("MOVE AN ENCHANTMENT ONTO THE ARMING SWORD", "DEPOSIT THE ARMING SWORD TO MOVE FLAMING TO THE END - 2 RUNS - ITS 35 WEAR IS SPENT"),
            (reordering[0].Text, reordering[^1].Text));
        Assert.Equal("MOVE AN ENCHANTMENT ONTO THE ARMING SWORD - BOTH WEAPONS STAY IN THE SHOP", DepositPanel.SourcesHeading(sword, null));

        // A soul of a kind that does nothing yet moves all the same — the enchanter
        // moves what it never offers to copy (PHASE 6a DECISION 13) — and says so, as
        // one that would never fire on the weapon says that: a deposit and its runs
        // would buy a lock that does nothing (the audit of Phase 6a).
        var inert = TestShop.Opened();
        var target = TestShop.Give(inert, "B", 2, Sword(wear: 36, cashed: 195, "flaming"));
        inert.Move(TestShop.Give(inert, "C", 2, TestWeapons.Get("feathered_death")), WeaponPlace.InTheStable(0));
        inert.Move(TestShop.Give(inert, "C", 2, TestWeapons.Get("shieldbreaker")), WeaponPlace.InTheStable(1));
        var souls = Enchanter.TransferOffers(inert, inert.WeaponAt(target)!).Where(move => LootTable.IsInert(move.Soul.Def)).ToArray();
        Assert.Equal(["weightless", "momentum"], souls.Select(move => move.Soul.Id));
        Assert.All(souls, move => Assert.Equal((null, Enchanter.DoesNothingYetNote), (move.Refusal, move.Note)));
        Assert.Equal(["WEIGHTLESS ON THE FEATHERED DEATH IN THE STABLE - ARRIVES AT T1 - 2 RUNS - DOES NOTHING YET",
                "MOMENTUM ON THE SHIELDBREAKER IN THE STABLE - ARRIVES AT T1 - 2 RUNS - DOES NOTHING YET"],
            souls.Select(DepositPanel.MoveLine));
        Assert.DoesNotContain(Enchanter.TransferOffers(inert, inert.WeaponAt(target)!), move => !LootTable.IsInert(move.Soul.Def) && move.Note == Enchanter.DoesNothingYetNote);

        // The choice goes round a line at a time, a refused one included: ENTER on it
        // says why, where the soul lies first, and chooses nothing. ENTER on one that
        // can be chosen chooses, and the confirm asks, the list's heading over it
        // saying both stay.
        var chosen = new List<string>();
        for (int step = 0; step < 4; step++)
        {
            panel.Move(+1);
            chosen.Add(panel.Moves[panel.Chosen].Soul.Id);
        }
        Assert.Equal(["vampiric", "overheal", "flaming", "burning"], chosen);
        panel.Move(-1);
        Assert.Equal(("> FLAMING ON THE NAMELESS NOVA IN THE BAG OF [C] - BURNING NEEDS FLAMING AHEAD OF IT <", DungeonHud.Grey),
            (BetweenRunsHud.PanelRows(panel, 20)[7].Text, BetweenRunsHud.PanelRows(panel, 20)[7].Color));
        Assert.Equal("FLAMING ON THE NAMELESS NOVA IN THE BAG OF [C] - BURNING NEEDS FLAMING AHEAD OF IT", panel.ChooseOffer());
        Assert.Equal(DepositPanel.Step.Sources, panel.Current);
        panel.Move(-1);
        panel.Move(-1);
        Assert.Null(panel.ChooseOffer());
        Assert.Equal((DepositPanel.Step.Confirm, "vampiric", knife), (panel.Current, panel.ChosenMove!.Soul.Id, panel.ChosenMove.Source));
        Assert.Equal("DEPOSIT THE ARMING SWORD TO TAKE VAMPIRIC FROM THE NAMELESS KNIFE - 2 RUNS - ITS 36 WEAR IS SPENT", panel.Ask);
        var confirm = BetweenRunsHud.PanelRows(panel, 20);
        Assert.Equal((panel.Ask, DungeonHud.Yellow), (confirm[^1].Text, confirm[^1].Color));
        Assert.StartsWith("MOVE AN ENCHANTMENT ONTO THE ARMING SWORD", confirm[0].Text, StringComparison.Ordinal);
        Assert.False(panel.Back());
        Assert.Equal(DepositPanel.Step.Sources, panel.Current);
        Assert.False(panel.Back());
        Assert.Equal(DepositPanel.Step.Services, panel.Current);

        // Walked: D, 3, down to Vampiric, ENTER, ENTER — one deposit, both weapons.
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  BAG   ARMING SWORD - WEAR 36/36 FULL");
        foreach (var key in new[] { Keys.D, Keys.D3, Keys.S, Keys.Enter })
            scene.OnKey(key);
        Assert.Equal("DEPOSIT THE ARMING SWORD TO TAKE VAMPIRIC FROM THE NAMELESS KNIFE - 2 RUNS - ITS 36 WEAR IS SPENT", scene.Panel!.Ask);
        scene.OnKey(Keys.Enter);
        var deposit = Assert.Single(campaign.AtTheEnchanter);
        Assert.Equal((sword, knife, (int?)1, 2), (deposit.Weapon, deposit.Source, deposit.SourceIndex, deposit.Runs));
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, "  WORKING  ARMING SWORD - 0 OF 2 RUNS"), (scene.Mode, scene.Weapons.ChosenRow.Text));
        Assert.Contains("  HELD     NAMELESS KNIFE", scene.Weapons.Rows().Select(row => row.Text));
        Assert.Equal(1, host.HubSavesAsked);
        Assert.Contains("[Enchanter] Deposited Arming Sword (arming_sword) from B's bag slot 3: Transfer vampiric (T3) from Nameless Knife (flensing_knife_unique) in C's bag slot 3,"
            + " 36 wear cashed (capacity 36 -> 37), 2 runs, deposit #0", Log.RecentEntries.Select(entry => entry.Message));

        // The confirm's ENTER asks again (6a.5's review, extended): the source gone
        // from where the list found it, the soul gone from the source, and the gates
        // for the soul — each said over the keys, nothing deposited.
        Enchanter.TakeBack(campaign, deposit);   // both to the stable
        var blade = TestShop.Worn("riposte_blade", wear: 30);
        TestShop.Give(campaign, "B", 3, blade);
        var from = new WeaponPlace("C", 0);
        campaign.Move(TestShop.InTheStable(campaign, knife), from);   // the knife into C's hand, its Great Axe to the stable
        void Confirm()
        {
            ChooseRow(scene, "  BAG   RIPOSTE BLADE - WEAR 30/30 FULL");
            foreach (var key in new[] { Keys.D, Keys.D3, Keys.S, Keys.Enter })
                scene.OnKey(key);
            Assert.Equal((DepositPanel.Step.Confirm, knife, 1), (scene.Panel!.Current, scene.Panel.ChosenMove!.Source, scene.Panel.ChosenMove.Index));
        }
        Confirm();
        campaign.Move(from, WeaponPlace.InTheStable(campaign.Stable.Count));   // C's hand, emptied, takes up its staff
        scene.OnKey(Keys.Enter);
        Assert.Equal("THE NAMELESS KNIFE HAS MOVED - NOTHING DEPOSITED", scene.Refusal);
        campaign.Move(TestShop.InTheStable(campaign, knife), from);
        Confirm();
        knife.CreditEnchantment(1, 1, 1);   // a new entry where the list found the old
        scene.OnKey(Keys.Enter);
        Assert.Equal("VAMPIRIC IS NOT ON THE NAMELESS KNIFE NOW - NOTHING DEPOSITED", scene.Refusal);
        Confirm();
        campaign.Move(new WeaponPlace("C", 3), WeaponPlace.InTheStable(campaign.Stable.Count));   // C left carrying the knife alone
        Assert.Equal(1, campaign.Party[2].Inventory.Count(carried => carried is not null));
        scene.OnKey(Keys.Enter);
        Assert.Equal("VAMPIRIC T3 ON THE NAMELESS KNIFE IN THE HAND OF [C] - [C] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST", scene.Refusal);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (DepositPanel?)null), (scene.Mode, scene.Panel));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Equal(1, host.HubSavesAsked);   // the deposit's alone: nothing refused was saved
    }

    [Fact]
    public void TheMoveListScrolls_AStableOf20WithSoulsFits720()
    {
        // A late stable holds many souls, so the source list is a window that
        // scrolls with the choice, as the offer list does: with twenty weapons of
        // three souls each in the stable, and the most notice rows the screen can lead
        // over it, the list runs past its window on a 720-pixel window, every line —
        // each chosen in turn, a refused one too — is in view as it is chosen, and the
        // panel, the confirm's ask included, ends clear of the refusal row.
        var start = CampaignState.Start(GameContent.Current);
        var stable = Enumerable.Range(0, 20).Select(_ => TestWeapons.Get("flensing_knife_unique")).ToArray();
        var campaign = CampaignState.Restore(start.Party, start.SeenInOrder, 0, [], [], [GameContent.Current.Dungeons.Tutorial.Id], lastResult: null, visit: null, stable: stable);
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, Sword(wear: 36, cashed: 195, "flaming")));
        var place = BetweenRunsHud.PanelPlace(720f, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1);
        Assert.True(panel.Moves.Count > place.OfferRows, $"{panel.Moves.Count} souls fit the window of {place.OfferRows}: nothing scrolls");

        Assert.Null(panel.Pick(3));
        Assert.Equal(60, panel.Moves.Count(move => move.Refusal is null));
        int listed = panel.Moves.Count;
        var seen = new HashSet<int>();
        for (int step = 0; step < 2 * listed + 1; step++)
        {
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            int top = panel.Window(place.OfferRows);
            Assert.InRange(panel.Chosen, top, top + place.OfferRows - 1);
            Assert.Contains($"> {DepositPanel.MoveLine(panel.Moves[panel.Chosen])} <", Texts(rows));
            Assert.Equal(1 + Math.Min(place.OfferRows, panel.Moves.Count), rows.Count);
            Ends(rows);
            seen.Add(panel.Chosen);
            panel.Move(step < listed ? +1 : -1);
        }
        Assert.Equal(listed, seen.Count);

        while (panel.Moves[panel.Chosen].Refusal is not null)
            panel.Move(+1);
        Assert.Null(panel.ChooseOffer());
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
    public void EveryLineOfALongListIsChosenAndSeen_RefusedRunsAtBothEndsIncluded()
    {
        // PHASE 6a DECISION 12: a refused line is listed with its reason, never
        // hidden. The audit of Phase 6a found the window, which follows the choice,
        // never reached the refused lines past the ends of what could be chosen, W/S
        // passing over them: on a weapon with every slot filled, whose reorders are
        // all a MOVE may choose, the souls of a late stable ran on over them further
        // than the window reaches, and the last reorder's own IT IS LAST ALREADY hid
        // under it. W/S now go a line at a time, a refused one included — its ENTER
        // saying why and choosing nothing — so every line is chosen in turn, and
        // shown as it is.
        var start = CampaignState.Start(GameContent.Current);
        var stable = Enumerable.Range(0, 10).Select(_ => TestWeapons.Get("flensing_knife_unique")).ToArray();
        var campaign = CampaignState.Restore(start.Party, start.SeenInOrder, 0, [], [], [GameContent.Current.Dungeons.Tutorial.Id], lastResult: null, visit: null, stable: stable);
        var full = Sword(wear: 50, cashed: 790, "flaming", "vampiric", "arcane", "shattering", "vampiric");   // capacity 50: slot 5 open, its reorder's price met
        Assert.Equal((50, 5), (full.WearCapacity, full.Enchantments.Count));
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, full));
        Assert.Null(panel.Pick(3));
        var place = BetweenRunsHud.PanelPlace(720f);
        var moves = panel.Moves;
        Assert.True(moves.Count >= 30, $"{moves.Count} lines");
        int first = panel.Chosen;
        Assert.Equal((null, 0), (moves[first].Source, moves[first].Index));   // the first reorder: every soul on another weapon is refused
        Assert.All(moves.Take(first), move => Assert.NotNull(move.Refusal));
        Assert.True(first >= place.OfferRows, $"the {first} refused lines over the reorders fit the window of {place.OfferRows}");
        Assert.Equal("VAMPIRIC - ITS OWN - IT IS LAST ALREADY", DepositPanel.MoveLine(moves[^1]));

        // W from the first reorder, a line at a time, round the list and back: every
        // line is chosen, and every one is in the window as it is.
        var chosen = new HashSet<int>();
        var shown = new HashSet<int>();
        for (int step = 0; step < moves.Count; step++)
        {
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            int top = panel.Window(place.OfferRows);
            Assert.InRange(panel.Chosen, top, top + place.OfferRows - 1);
            Assert.Contains($"> {DepositPanel.MoveLine(moves[panel.Chosen])} <", Texts(rows));
            chosen.Add(panel.Chosen);
            for (int i = top; i < Math.Min(moves.Count, top + place.OfferRows); i++)
                shown.Add(i);
            panel.Move(-1);
        }
        Assert.Equal(moves.Count, chosen.Count);
        Assert.Equal(moves.Count, shown.Count);
        Assert.Equal(first, panel.Chosen);

        // Walked: D, 3 opens on the first reorder; W onto the refused line over it
        // and ENTER says why over the keys, where the soul lies first, as the log
        // does; the list stays up, nothing is deposited and nothing is saved. S back
        // and ENTER chooses the reorder.
        var (scene, host) = Weapons(campaign);
        ChooseRow(scene, "  BAG   ARMING SWORD - WEAR 50/50 FULL");
        foreach (var key in new[] { Keys.D, Keys.D3, Keys.W, Keys.Enter })
            scene.OnKey(key);
        string said = $"{DepositPanel.SoulPlace(moves[first - 1])} - {moves[first - 1].Refusal}";
        Assert.Equal(said, scene.Refusal);
        Assert.StartsWith("OVERHEAL ON THE NAMELESS KNIFE IN THE STABLE - ", said, StringComparison.Ordinal);
        Assert.Contains($"[Hub] Refused: {said}", Log.RecentEntries.Select(entry => entry.Message));
        Assert.Equal((BetweenRunsScene.HubMode.Deposit, DepositPanel.Step.Sources), (scene.Mode, scene.Panel!.Current));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Equal(0, host.HubSavesAsked);
        foreach (var key in new[] { Keys.S, Keys.Enter })
            scene.OnKey(key);
        Assert.Equal(("DEPOSIT THE ARMING SWORD TO MOVE FLAMING TO THE END - 5 RUNS - ITS 50 WEAR IS SPENT", (string?)null), (scene.Panel!.Ask, scene.Refusal));
    }

    [Fact]
    public void ThePanelIsDrawnOnASolidBacking()
    {
        // The panel's lines were drawn straight over the WEAPONS page, on the detail
        // column's own lines: a refused service's grey on the page's dimmed grey read
        // as neither (the audit of Phase 6a). A solid backing goes under them first
        // (BetweenRunsHud.PanelBacking): on every question the panel asks, every
        // line's glyphs lie inside it with room all round, and it lies inside the
        // window.
        var campaign = TestShop.Opened();
        var panel = DepositPanel.Open(campaign, TestShop.Give(campaign, "B", 2, Sword(wear: 36, cashed: 195, "flaming")));
        var place = BetweenRunsHud.PanelPlace(720f, 2);
        void Backed()
        {
            var rows = BetweenRunsHud.PanelRows(panel, place.OfferRows);
            var (x, y, width, height) = BetweenRunsHud.PanelBacking(rows, place.Top, 1280f);
            Assert.True(x >= 0f && x + width <= 1280f && y >= 0f && y + height <= 720f, $"{panel.Current}: the backing runs off the window");
            float top = place.Top;
            foreach (var row in rows)
            {
                float glyphs = row.Text.Length * 6f * row.Scale;
                Assert.True(640f - glyphs / 2f >= x + BetweenRunsHud.PanelBackingPad - 0.01f && 640f + glyphs / 2f <= x + width - BetweenRunsHud.PanelBackingPad + 0.01f, $"'{row.Text}' runs past its backing's sides");
                Assert.True(top >= y + BetweenRunsHud.PanelBackingPad - 0.01f && top + 7f * row.Scale <= y + height - BetweenRunsHud.PanelBackingPad + 0.01f, $"'{row.Text}' runs past its backing's top or bottom");
                top += row.Pitch;
            }
        }

        Backed();                          // the services
        Assert.Null(panel.Pick(1));
        Backed();                          // the offer list
        Assert.Null(panel.ChooseOffer());
        Backed();                          // its confirm
        var made = Enchanter.Deposit(campaign, panel.From!.Value, panel.Service, panel.EnchantmentId);
        panel = DepositPanel.Asking(made.Entry);
        Backed();                          // the take-back's ask
        Assert.Equal((1280f / 2f, 100f, 0f, 0f), BetweenRunsHud.PanelBacking([], 100f, 1280f));
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

        // A MOVE's (Phase 6a.6): its service's line with every reason, the source
        // list's heading, a soul's line at a two-digit tier — arriving, reordered,
        // noted, and refused with every reason a transfer gives, the widest names on
        // both weapons — the confirm's ask and the take-back's, what the shop and the
        // refusal row say, and the summary's two lines at the dungeon's scale.
        foreach (string reason in (string[])[.. reasons, Enchanter.NothingToMove])
            Line(new DepositPanel.Option(3, Service.Transfer, DepositPanel.TransferDoes, reason, null, null).Text, BetweenRunsHud.PanelOptionScale);
        Line($"> {new DepositPanel.Option(3, Service.Transfer, DepositPanel.TransferDoes, null, "99.9 IN 100 TO IMPROVE", null).Text} <", BetweenRunsHud.PanelOptionScale);
        Line(DepositPanel.SourcesHeading(nine, null), BetweenRunsHud.PanelOfferHeadingScale);
        var deepSoul = new Enchantment(soul, 99);
        string[] moveReasons =
        [
            Enchanter.WouldCarryNone("A"), Enchanter.LastAlready, Enchanter.AllFilled(10), Enchanter.NoFreeSlot(10, Wear.SlotOpensAt(10), 9999),
            Enchanter.Short(10, Wear.Price(10), 9999), Enchanter.InTheShop(nine), Enchanter.CannotSitWith(soul), Enchanter.NeedsAhead(soul.Name),
            Enchanter.WandCastsNoStatusWhy, Enchanter.StaffCastsItOn(TargetSide.Ally), Enchanter.StaffCastsItOn(TargetSide.Enemy),
            Enchanter.Strands(soul, soul.Name), Enchanter.WouldCastNothing(nine), Enchanter.WouldCastOn(nine, soul, TargetSide.Enemy),
        ];
        foreach (var source in new[] { nine, null })
        {
            // Where a source lies, at its longest: in a member's hand (the audit of Phase 6a).
            WeaponPlace? at = source is null ? null : new WeaponPlace("A", 0);
            foreach (string? note in Enum.GetValues<WeaponClass>().Select(Enchanter.NeverFiresOn).Prepend(Enchanter.DoesNothingYetNote).Prepend(null))
                Line($"> {DepositPanel.MoveLine(new TransferOffer(at, source, 0, deepSoul, 98, ContentBounds.MostServiceRuns, null, note))} <", BetweenRunsHud.PanelOfferScale);
            foreach (string reason in moveReasons)
                Line($"> {DepositPanel.MoveLine(new TransferOffer(at, source, 0, deepSoul, 98, ContentBounds.MostServiceRuns, reason, Enchanter.NeverFiresOn(WeaponClass.Throwing)))} <", BetweenRunsHud.PanelOfferScale);
            Line(DepositPanel.DepositAsk(nine, Service.Transfer, null, new TransferOffer(null, source, 0, deepSoul, 98, ContentBounds.MostServiceRuns, null, null)), BetweenRunsHud.PanelAskScale);
            var moving = new Deposit(999, nine, Service.Transfer, 9999, ContentBounds.MostServiceRuns, null, source, 0) { RunsWorked = ContentBounds.MostServiceRuns - 1 };
            Line(DepositPanel.TakeBackAsk(moving), BetweenRunsHud.PanelAskScale);
            Line(BetweenRunsHud.ServiceLine(moving), BetweenRunsHud.PanelStateScale);
            Line(DepositPanel.SoulMoved(new TransferOffer(at, source, 0, deepSoul, 98, 1, null, null)), BetweenRunsHud.KeysLineScale);
            foreach (string reason in moveReasons)   // a refusal the confirm's ENTER says, or a refused line's own
                Line($"{DepositPanel.SoulPlace(new TransferOffer(at, source, 0, deepSoul, 98, 1, null, null))} - {reason}", BetweenRunsHud.KeysLineScale);
        }
        Assert.Contains(lines, line => line.Text.Contains(" T99 ON THE ", StringComparison.Ordinal)
            && line.Text.Contains($"IN THE HAND OF [A] - ARRIVES AT T98 - {ContentBounds.MostServiceRuns} RUNS - NEVER FIRES ON A THROWING WEAPON", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("IN THE HAND OF [A] - [A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST", StringComparison.Ordinal));
        var held = new Deposit(999, nine, Service.Transfer, 9999, ContentBounds.MostServiceRuns, null, nine, 0);
        Assert.True(BetweenRunsHud.HeldLine(held).Length * 6f * BetweenRunsHud.DetailScale <= BetweenRunsHud.ColumnWidth, BetweenRunsHud.HeldLine(held));
        Assert.True(BetweenRunsHud.ServiceLine(held).Length * 6f * BetweenRunsHud.DetailScale <= BetweenRunsHud.ColumnWidth, BetweenRunsHud.ServiceLine(held));
        foreach (string? source in new[] { longest.Id, null })
        {
            var back = new ServiceReport(longest.Id, Service.Transfer, 10, 10, EnchantmentId: soul.Id, Tier: 98, SourceId: source,
                Improved: new Improvement(ImprovementKind.Deepen, Enum.GetValues<ModifierType>().Where(t => !ModifierRules.NeverStacked.Contains(t)).MaxBy(t => DungeonHud.ModifierName(t).Length)));
            foreach (string summary in new[] { BetweenRunsHud.BackFromTheEnchanter(back), BetweenRunsHud.SourceBack(back) }.OfType<string>())
                Assert.True(summary.Length * 6f * 2f <= BetweenRunsHud.FullWidth, $"'{summary}' runs off the dungeon's end panel");
        }

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
