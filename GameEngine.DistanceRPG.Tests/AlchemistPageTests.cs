using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestPotions;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The ALCHEMIST page between runs (§6.6, Phase 6b.4), the hub's third TAB page: as
/// with <c>BetweenRunsHudTests</c>, the drawing is untested, but what it draws is —
/// the line before the alchemist arrives (PHASE 6b DECISION 10); the farm, the batch,
/// what spoils and the quality; each member's room and each potion in its bag; the
/// quality held to the batch; the choice over the rows and the window that scrolls
/// with it; what each mix would restore; the choice kept by place across a mix and a
/// pour-back; a bag's room, read in one place for this page and WEAPONS; and every
/// string in its column, in the HUD's font. On the numbers the runs were written
/// against (<see cref="Premise"/>), with the farm's rate, the potions' shares and a
/// drink's cost stated here as these tests' own input.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class AlchemistPageTests : IDisposable
{
    /// <summary>The units a worker yields a counted run: this suite's input.</summary>
    private const int PerClear = 7;

    private static Tuning Mixing(Tuning t) => t with { FarmYieldPerClear = PerClear, HealthPotionPercent = 20, ManaPotionPercent = 20, PotionUseCost = 40 };

    private readonly IDisposable _premise = TestContent.UsePremise(tuning: Mixing);

    public void Dispose() => _premise.Dispose();

    private static string[] Texts(IEnumerable<BetweenRunsHud.ListRow> rows) => rows.Select(row => row.Text).ToArray();

    /// <summary>The HUD's glyphs: capitals, digits, the space and the punctuation the font draws — no apostrophe, quote or percent sign.</summary>
    private static void Glyphs(string text) => Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]*$", text);

    [Fact]
    public void TheClosedPageSaysWhenTheAlchemistArrives()
    {
        // PHASE 6b DECISION 10: the page is there from the start, and until the first
        // clear of a dungeon besides the tutorial it says when the alchemist arrives,
        // naming the tutorial as the enchanter's gate does — one grey line in the left
        // column, nothing to choose, and TAB and ESC its only keys. It opens with his
        // arrival, and closes again with him under a dungeons.json that takes the
        // farm's one worker (PHASE 6b DECISION 11), naming the content's tutorial then.
        var campaign = CampaignState.Start(GameContent.Current);
        var page = new AlchemistPage(campaign);
        Assert.False(page.Open);
        Assert.Equal("THE ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND THE TUTORIAL", AlchemistPage.ClosedLine);
        Assert.Equal(Alchemist.NotYet(GameContent.Current.Dungeons.Tutorial), AlchemistPage.ClosedLine);
        Assert.True(AlchemistPage.ClosedLine.Length <= 64, "the closed line is past the 64-character column");
        Assert.Equal(("TAB DUNGEONS - ESC MENU", DungeonHud.Grey), BetweenRunsHud.BottomLine(BetweenRunsScene.HubMode.Alchemist, alchemistOpen: false));
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.False(page.Open);
        CampaignRuns.DeepRetreat(campaign, "spear_hall");
        Assert.False(page.Open);
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.FiveDeep);
        Assert.True(page.Open);
        Assert.Equal(BetweenRunsHud.AlchemistKeysLine, BetweenRunsHud.BottomLine(BetweenRunsScene.HubMode.Alchemist, alchemistOpen: page.Open).Text);

        // The farm's one worker retired by a moved retiresOnClear (no cooldown, so the
        // Spear Hall's lock lifts as its clear writes it): he goes, and the page says
        // when he comes, naming the content's tutorial, the Spear Hall now.
        static Tuning Quick(Tuning t) => Mixing(t) with { CooldownRunsPerFloor = 0 };
        using var quick = TestContent.UsePremise(tuning: Quick);
        var moved = CampaignState.Start(GameContent.Current);
        CampaignRuns.Clear(moved, "tutorial", CampaignRuns.GoldenSeed);
        CampaignRuns.Clear(moved, "spear_hall", CampaignRuns.FiveDeep);
        var movedPage = new AlchemistPage(moved);
        Assert.True(movedPage.Open);
        using (TestContent.UsePremise(tuning: Quick, dungeons: TestShop.TutorialMoved))
        {
            CampaignRuns.Clear(moved, "spear_hall", CampaignRuns.FiveDeep);
            Assert.False(movedPage.Open);
            Assert.Equal("THE ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND THE SPEAR HALL", AlchemistPage.ClosedLine);
            Assert.True(AlchemistPage.ClosedLine.Length <= 64);
        }
    }

    [Fact]
    public void ThePageListsTheFarmTheBatchAndEveryBag()
    {
        // Once he has arrived, down the left: what the farm yields a run that counts,
        // the batch and what is left of it, that the unmixed spoils on the descent, and
        // the quality — then each member and the room its bag has, and each potion in
        // its bag in slot order, numbered as the dungeon's bag numbers it (the hand 1).
        var campaign = AlchemistTests.Arrived();
        var page = new AlchemistPage(campaign);
        Assert.Equal(
        [
            "THE FARM - 1 DUNGEON BEATEN - 7 UNITS A COUNTED RUN",
            "THE BATCH - 7 OF 7 UNITS LEFT",
            "UNMIXED UNITS SPOIL ON THE DESCENT - A POTION KEEPS ONE TRIP",
            "QUALITY 1 - 1 UNIT A POTION - A/D CHANGES IT",
        ], Texts(page.Lines()));
        Assert.Equal([DungeonHud.Grey, DungeonHud.Cyan, DungeonHud.Grey, DungeonHud.Yellow], page.Lines().Select(line => line.Color));
        Assert.Equal(["[A] - 4 BAG SLOTS FREE", "[B] - 4 BAG SLOTS FREE", "[C] - 4 BAG SLOTS FREE", "[D] - 4 BAG SLOTS FREE"], page.Rows().Select(row => row.Text));

        Alchemist.Mix(campaign, "A", PotionType.Health, 3);
        Alchemist.Mix(campaign, "A", PotionType.Mana, 1);
        Alchemist.Mix(campaign, "C", PotionType.Mana, 2);
        Assert.Equal("THE BATCH - 1 OF 7 UNITS LEFT", page.Lines()[1].Text);
        Assert.Equal(
        [
            "[A] - 2 BAG SLOTS FREE", "  BAG 3  HEALTH POTION Q3", "  BAG 4  MANA POTION Q1",
            "[B] - 4 BAG SLOTS FREE",
            "[C] - 3 BAG SLOTS FREE", "  BAG 3  MANA POTION Q2",
            "[D] - 4 BAG SLOTS FREE",
        ], page.Rows().Select(row => row.Text));
        Assert.Equal([AlchemistPage.RowKind.Member, AlchemistPage.RowKind.Potion, AlchemistPage.RowKind.Potion], page.Rows().Take(3).Select(row => row.Kind));
        var rows = page.Rows();
        Assert.Equal([new DungeonHud.Run("[A] - 2 BAG SLOTS FREE", DungeonHud.White)], AlchemistPage.Runs(rows[0]));
        Assert.Equal([new DungeonHud.Run("  BAG 3  HEALTH POTION Q3", DungeonHud.Green)], AlchemistPage.Runs(rows[1]));
        Assert.Equal([new DungeonHud.Run("  BAG 4  MANA POTION Q1", DungeonHud.Cyan)], AlchemistPage.Runs(rows[2]));
        Assert.Equal([new DungeonHud.Run("> [A] - 2 BAG SLOTS FREE <", DungeonHud.Yellow)], AlchemistPage.Runs(rows[0], chosen: true));
        Assert.Equal([new DungeonHud.Run("> BAG 3  HEALTH POTION Q3 <", DungeonHud.Yellow)], AlchemistPage.Runs(rows[1], chosen: true));

        Alchemist.Mix(campaign, "A", PotionType.Health, 1);
        Assert.Equal(("THE BATCH - ALL 7 UNITS MIXED", "[A] - 1 BAG SLOT FREE"), (page.Lines()[1].Text, page.Rows()[0].Text));
        using (TestContent.UsePremise(tuning: t => Mixing(t) with { FarmYieldPerClear = 1 }))
        {
            Assert.Equal("THE FARM - 1 DUNGEON BEATEN - 1 UNIT A COUNTED RUN", page.Lines()[0].Text);
            Alchemist.PourBack(campaign, "C", 2);
            Alchemist.Mix(campaign, "A", PotionType.Mana, 1);
            Assert.Equal("[A] - THE BAG IS FULL", page.Rows()[0].Text);
        }

        // The farm grows with a second dungeon beaten, and a run that counts brings a
        // fresh batch, every potion carried spoiled.
        CampaignRuns.Clear(campaign, "long_gallery", CampaignRuns.FiveDeep);
        Assert.Equal(["THE FARM - 2 DUNGEONS BEATEN - 14 UNITS A COUNTED RUN", "THE BATCH - 14 OF 14 UNITS LEFT"], Texts(page.Lines()).Take(2));

        // Its zero forms: a farm at a rate of 0, and no batch, and why.
        using (TestContent.UsePremise(tuning: t => Mixing(t) with { FarmYieldPerClear = 0 }))
            Assert.Equal("THE FARM - 2 DUNGEONS BEATEN - IT YIELDS NOTHING", page.Lines()[0].Text);
        CampaignRuns.ShallowRetreat(campaign, "knife_warrens");
        Assert.Equal("NO BATCH - THE LAST RUN DID NOT COUNT", page.Lines()[1].Text);
        Assert.Equal(Alchemist.NoBatch(campaign), AlchemistPage.BatchLine(campaign));
        Assert.Equal(Alchemist.NoBatchNotCounted, Alchemist.NoBatch(campaign));

        // The batch's line built from its numbers alone, as the fit test measures it:
        // there is no batch line for nothing yielded — that line is why.
        Assert.Equal(("THE BATCH - 9 OF 14 UNITS LEFT", "THE BATCH - 1 OF 1 UNIT LEFT", "THE BATCH - ALL 14 UNITS MIXED"),
            (AlchemistPage.BatchLine(9, 14), AlchemistPage.BatchLine(1, 1), AlchemistPage.BatchLine(0, 14)));
        Assert.Throws<ArgumentOutOfRangeException>(() => AlchemistPage.BatchLine(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AlchemistPage.BatchLine(-1, 14));
    }

    [Fact]
    public void TheQualityIsHeldToTheBatch()
    {
        // A/D set the quality a unit at a time, from 1 — every showing starts there —
        // up to the units left, and never past either end; a batch spent holds it at
        // 1, since a potion is a unit at least. A mix that leaves fewer units than the
        // quality brings it down to them; a pour-back keeps it where it stands.
        var campaign = AlchemistTests.Arrived();
        var page = new AlchemistPage(campaign);
        Assert.Equal(1, page.Quality);
        page.Shift(-1);
        Assert.Equal(1, page.Quality);
        for (int i = 0; i < 10; i++)
            page.Shift(+1);
        Assert.Equal(PerClear, page.Quality);
        Assert.Equal("QUALITY 7 - 7 UNITS A POTION - A/D CHANGES IT", page.Lines()[3].Text);
        page.Shift(-1);
        page.Shift(-1);
        Assert.Equal(5, page.Quality);

        page.AfterMix(Alchemist.Mix(campaign, "A", PotionType.Health, page.Quality));
        Assert.Equal(2, page.Quality);   // two units left
        page.AfterPourBack(Alchemist.PourBack(campaign, "A", 2));
        Assert.Equal((PerClear, 2), (Farm.Batch(campaign), page.Quality));   // held where the mix brought it: it never springs back to 5
        page.AfterMix(Alchemist.Mix(campaign, "A", PotionType.Health, 5));
        page.AfterMix(Alchemist.Mix(campaign, "B", PotionType.Mana, page.Quality));
        Assert.Equal((0, 1), (Farm.Batch(campaign), page.Quality));
        page.Shift(+1);
        Assert.Equal(1, page.Quality);
        page.AfterPourBack(Alchemist.PourBack(campaign, "A", 2));
        Assert.Equal((5, 1), (Farm.Batch(campaign), page.Quality));
        page.Shift(+1);
        Assert.Equal(2, page.Quality);

        // A fresh page — the next showing — starts at 1 again.
        Assert.Equal(1, new AlchemistPage(campaign).Quality);
    }

    [Fact]
    public void ChoosingSkipsHeadings_AndTheWindowScrolls()
    {
        // The lines over the rows — the farm, the batch, what spoils, the quality —
        // are read, never chosen, and stay in view: W/S go over the members and the
        // potions alone, round the ends, in a window under them that scrolls with the
        // choice, a potion's member in view over it where there is room. The window
        // fits between the most notice rows the screen can be handed and the refusal
        // row over the keys; and without notices it holds the most rows the shipped
        // party can have — every member with a weapon in hand and five potions.
        const float h = 720f;
        int most = NoticeText.MaxWholeRows + NoticeText.MaxRows + 1;
        var layout = BetweenRunsHud.AlchemistPlace(h, most);
        Assert.True(layout.Top >= BetweenRunsHud.NoticeTop + most * BetweenRunsHud.NoticePitch, "the page runs into the notices");
        Assert.True(layout.RowsTop >= layout.Top + BetweenRunsHud.AlchemistLineCount * BetweenRunsHud.WeaponRowPitch, "the rows run into the lines over them");
        Assert.True(layout.RowsTop + (layout.Rows - 1) * BetweenRunsHud.WeaponRowPitch + 7f * BetweenRunsHud.WeaponRowScale <= layout.Bottom);
        Assert.True(layout.Bottom <= h - BetweenRunsHud.RefusalFromBottom, "the page's rows run into the refusal row");
        Assert.InRange(layout.Rows, 5, 40);
        int shipped = GameConstants.PartySize * PartyMemberState.SlotsFor(ShippedContent.Tuning);
        Assert.True(BetweenRunsHud.AlchemistPlace(h).Rows >= shipped, $"a 720 window holds {BetweenRunsHud.AlchemistPlace(h).Rows} rows, and a full party {shipped}");

        // Every bag's four empty slots filled with potions: sixteen potions under four
        // members, twenty rows.
        var campaign = CampaignState.Start(GameContent.Current);
        foreach (var member in campaign.Party)
            Carrying(campaign, member.Id, member.EmptyBagSlots.Select(slot => (slot, Health(1))).ToArray());
        var page = new AlchemistPage(campaign);
        var rows = page.Rows();
        Assert.Equal(4 + 16, rows.Count);
        Assert.True(rows.Count > layout.Rows, "the window does not scroll here");

        // Walked a frame at a time, as the HUD asks it: every row is chosen once a lap,
        // always in the window, and a potion's member row with it.
        var met = new HashSet<int>();
        for (int step = 0; step < rows.Count; step++)
        {
            int at = page.Chosen;
            met.Add(at);
            int top = page.Window(layout.Rows);
            Assert.InRange(at, top, top + layout.Rows - 1);
            int member = at;
            while (rows[member].Kind == AlchemistPage.RowKind.Potion)
                member--;
            Assert.True(member >= top, $"'{rows[at].Text}' chosen with its member out of view");
            page.Step(+1);
        }
        Assert.Equal(rows.Count, met.Count);
        Assert.Equal(0, page.Chosen);   // round the end, to the first again
        page.Step(-1);
        Assert.Equal(rows.Count - 1, page.Chosen);   // and up round the start, to the last
        Assert.Equal(rows.Count - layout.Rows, page.Window(layout.Rows));

        // Up from the bottom of a window of six: [A] and its four potions at 0-4, [B]
        // at 5, [C] at 10, [D] at 15 and its potions at 16-19. On [D] the window still
        // starts at 14, C's last potion; up onto that potion, [C] comes in over it.
        const int fit = 6;
        var walked = new AlchemistPage(campaign);
        while (walked.Chosen != rows.Count - 1)
        {
            walked.Step(+1);
            walked.Window(fit);
        }
        Assert.Equal(14, walked.Window(fit));
        while (walked.Chosen != 15)
        {
            walked.Step(-1);
            Assert.Equal(14, walked.Window(fit));
        }
        walked.Step(-1);
        Assert.Equal((new AlchemistPage.Place("C", 5), 10), (walked.ChosenRow!.Place, walked.Window(fit)));
        Assert.Equal("[C] - THE BAG IS FULL", rows[10].Text);
    }

    [Fact]
    public void TheDetailSaysWhatEachMixWouldRestore()
    {
        // On the right, for a member: its maxima, what a health and a mana potion at
        // the quality would restore it now — in the bag's own words, the exact whole
        // pool and the overshoot included — the quality that restores a whole pool,
        // and what a drink costs in a dungeon (PHASE 6b DECISION 2). For a potion: what
        // it is and where, what it restores its member now, and what T gives back.
        var campaign = AlchemistTests.Arrived();
        var page = new AlchemistPage(campaign);
        var a = campaign.Party[0];
        int hp = a.MaxHp, mana = a.MaxMana;
        string Restores(int share, int max, string pool)
            => share < max ? $"RESTORES {share} OF {max} {pool}" : share == max ? $"RESTORES ALL {max} {pool}" : $"RESTORES ALL {max} {pool} - {share - max} OVER";
        Assert.Equal(
        [
            $"[A] - MAX HP {hp} - MAX MANA {mana}",
            $"H  HEALTH POTION Q1 - {Restores(hp / 5, hp, "HP")}",
            $"M  MANA POTION Q1 - {Restores(mana / 5, mana, "MANA")}",
            "QUALITY 5 RESTORES A WHOLE POOL",
            "A DRINK COSTS 40 MOVE IN A DUNGEON",
        ], Texts(page.Detail()));
        Assert.Equal([DungeonHud.White, DungeonHud.Green, DungeonHud.Cyan, DungeonHud.Grey, DungeonHud.Grey], page.Detail().Select(row => row.Color));
        Assert.Equal(BetweenRunsHud.DetailNameScale, page.Detail()[0].Scale);

        // At 3, 5 and 7: a share, a whole pool exactly, and the overshoot.
        for (int i = 0; i < 2; i++)
            page.Shift(+1);
        Assert.Equal($"H  HEALTH POTION Q3 - {DungeonHud.PotionDetail(Health(3), a)}", page.Detail()[1].Text);
        Assert.Equal(30, hp);   // the starting pool, nothing earned: §6.6's worked numbers
        Assert.Equal("H  HEALTH POTION Q3 - RESTORES 18 OF 30 HP", page.Detail()[1].Text);
        var fixture = TestPools.Holding("B", TestWeapons.Get("arming_sword"));
        Assert.Equal(TestPools.FixtureHp, fixture.MaxHp);
        Assert.Equal(["H  HEALTH POTION Q3 - RESTORES 60 OF 100 HP", "H  HEALTH POTION Q5 - RESTORES ALL 100 HP", "H  HEALTH POTION Q7 - RESTORES ALL 100 HP - 40 OVER"],
            new[] { 3, 5, 7 }.Select(q => AlchemistPage.MixLines(PotionType.Health, q, fixture).Single()));

        // The percentages apart; a drink that is free.
        using (TestContent.UsePremise(tuning: t => Mixing(t) with { ManaPotionPercent = 25, PotionUseCost = 0 }))
            Assert.Equal(["HEALTH IS WHOLE AT QUALITY 5 - MANA AT 4", "DRINKING IS FREE IN A DUNGEON"], Texts(page.Detail()).Skip(3));

        // A potion: what it is and where, what it restores now, what T gives back.
        Alchemist.Mix(campaign, "A", PotionType.Health, 3);
        Alchemist.Mix(campaign, "A", PotionType.Mana, 1);
        page.Step(+1);
        Assert.Equal(
        [
            "HEALTH POTION Q3 - BAG SLOT 3 OF [A]",
            DungeonHud.PotionDetail(Health(3), a),
            "T POURS IT BACK - 3 UNITS RETURN TO THE BATCH",
        ], Texts(page.Detail()));
        Assert.Equal([DungeonHud.Green, DungeonHud.Green, DungeonHud.Grey], page.Detail().Select(row => row.Color));
        page.Step(+1);
        Assert.Equal(["MANA POTION Q1 - BAG SLOT 4 OF [A]", DungeonHud.PotionDetail(Mana(1), a), "T POURS IT BACK - 1 UNIT RETURNS TO THE BATCH"], Texts(page.Detail()));
        Assert.Equal(DungeonHud.Cyan, page.Detail()[0].Color);

        // Nothing for no row.
        Assert.Empty(AlchemistPage.DetailRows(campaign, null, 1));
    }

    [Fact]
    public void TheChoiceKeepsItsPlace_AcrossAMixAndAPourBack()
    {
        // The choice is a member or a member's bag slot, never a bare index: a row
        // added or taken above it never moves it. After a mix it stays on the row it
        // was on — the member, or the potion it was on. After a pour-back it goes to
        // the next potion in that member's bag, and with none after it, to the member.
        var campaign = AlchemistTests.Arrived();
        var page = new AlchemistPage(campaign);
        Assert.Equal(new AlchemistPage.Place("A", 0), page.ChosenRow!.Place);

        // On [B]: potions mixed into A above it add rows over it; the choice stays on [B].
        page.Step(+1);
        Assert.Equal("[B] - 4 BAG SLOTS FREE", page.ChosenRow!.Text);
        page.AfterMix(Alchemist.Mix(campaign, "A", PotionType.Health, 1));
        page.AfterMix(Alchemist.Mix(campaign, "A", PotionType.Mana, 2));
        Assert.Equal(("[B] - 4 BAG SLOTS FREE", 3), (page.ChosenRow!.Text, page.Chosen));

        // On A's first potion, a mix into A adds a row under it: the choice stays on it.
        while (page.ChosenRow!.Place != new AlchemistPage.Place("A", 2))
            page.Step(-1);
        page.AfterMix(Alchemist.Mix(campaign, "A", PotionType.Health, 2));
        Assert.Equal(("  BAG 3  HEALTH POTION Q1", 1), (page.ChosenRow!.Text, page.Chosen));

        // Poured back: to A's next potion, then the next, then — the last poured — to [A].
        var place = page.ChosenRow!.Place;
        page.AfterPourBack(Alchemist.PourBack(campaign, place.MemberId, place.Slot));
        Assert.Equal("  BAG 4  MANA POTION Q2", page.ChosenRow!.Text);
        place = page.ChosenRow!.Place;
        page.AfterPourBack(Alchemist.PourBack(campaign, place.MemberId, place.Slot));
        Assert.Equal("  BAG 5  HEALTH POTION Q2", page.ChosenRow!.Text);
        place = page.ChosenRow!.Place;
        page.AfterPourBack(Alchemist.PourBack(campaign, place.MemberId, place.Slot));
        Assert.Equal(("[A] - 4 BAG SLOTS FREE", 0), (page.ChosenRow!.Text, page.Chosen));

        // A potion gone from under the choice by any other door — the run's end
        // spoils them all — leaves it on its member's row.
        page.AfterMix(Alchemist.Mix(campaign, "D", PotionType.Mana, 1));
        while (page.ChosenRow!.Place != new AlchemistPage.Place("D", 2))
            page.Step(+1);
        CampaignRuns.ShallowRetreat(campaign, "long_gallery");
        Assert.Equal("[D] - 4 BAG SLOTS FREE", page.ChosenRow!.Text);
    }

    [Fact]
    public void TheBagsRoomIsReadInOnePlace()
    {
        // A bag's room is said on two pages between runs — ALCHEMIST's "[A] - 2 BAG
        // SLOTS FREE", and WEAPONS' "  BAG   - ROOM FOR 2 MORE -", where a weapon put
        // down takes the first of them — and the mix's gate and its door ask it too.
        // One reader answers all four (PartyMemberState.EmptyBagSlots), read off the
        // IL, so the pages cannot disagree: the 6b.4 build counted WEAPONS' room with a
        // loop of its own (its review). A pickup in a dungeon asks another question,
        // the first empty slot with the hand among them (Pickup.FirstEmptySlot).
        Assert.Equal(["Alchemist.MixRefusal", "AlchemistPage.MemberText", "CampaignState.Stow", "HubWeapons.PartyRows"],
            TestIl.CallersOf(typeof(PartyMemberState), "get_EmptyBagSlots"));

        // And the two pages say the same room, bag by bag, as potions go in and come out.
        var campaign = AlchemistTests.Arrived();
        void Agree()
        {
            var weapons = HubWeapons.Rows(campaign);
            foreach (var member in campaign.Party)
            {
                var free = member.EmptyBagSlots.ToList();
                var room = weapons.Where(row => row is { Kind: HubWeapons.RowKind.Room, Place: { Index: > 0 } place } && place.MemberId == member.Id).ToList();
                if (free.Count == 0)
                {
                    Assert.Empty(room);
                    Assert.Equal($"[{member.Id}] - THE BAG IS FULL", AlchemistPage.MemberText(member));
                    continue;
                }
                Assert.Equal((HubWeapons.BagRoom(free.Count), new WeaponPlace(member.Id, free[0])), (Assert.Single(room).Text, room[0].Place!.Value));
                Assert.Equal($"[{member.Id}] - {free.Count} {(free.Count == 1 ? "BAG SLOT" : "BAG SLOTS")} FREE", AlchemistPage.MemberText(member));
            }
        }
        Agree();
        for (int i = 0; i < 4; i++)
            Alchemist.Mix(campaign, "A", PotionType.Health, 1);
        Alchemist.Mix(campaign, "B", PotionType.Mana, 2);
        Agree();
        Alchemist.PourBack(campaign, "A", 3);
        Agree();
        Assert.Equal(new WeaponPlace("A", 3), HubWeapons.Rows(campaign).Single(row => row.Text == HubWeapons.BagRoom(1)).Place);
    }

    [Fact]
    public void EveryAlchemistStringFitsItsColumn_InTheHudsFont()
    {
        // Every string the page draws fits its own column at its own scale, in the
        // HUD's glyphs: the left column's — the lines and the rows, marked as chosen —
        // within 580 px at the rows' scale, 64 characters, at their worst numbers (a
        // yield and a quality of the sanity bound, 99 dungeons beaten, the deepest bag
        // slot), the closed line with the shipped tutorial's name; the detail within
        // 580 px at its scales, on a member whose pools are as large as a save holds;
        // and the refusals and the keys lines across the window, 1240 px.
        static void Fits(string text, float scale, float width, string what)
            => Assert.True(text.Length * 6f * scale <= width, $"{what}: '{text}' is {text.Length * 6f * scale} px, past {width}");
        const int Most = ContentBounds.MostCount;
        Assert.Equal(64, (int)(BetweenRunsHud.ColumnWidth / (6f * BetweenRunsHud.WeaponRowScale)));

        var bag = TestPools.Holding("A", TestWeapons.Get("arming_sword"));
        var rooms = new List<string> { AlchemistPage.MemberText(bag) };
        for (int slot = 1; slot < bag.Inventory.Length; slot++)
        {
            bag.Inventory[slot] = Mana(1);
            rooms.Add(AlchemistPage.MemberText(bag));
        }
        Assert.Equal(["[A] - 5 BAG SLOTS FREE", "[A] - 4 BAG SLOTS FREE", "[A] - 3 BAG SLOTS FREE", "[A] - 2 BAG SLOTS FREE", "[A] - 1 BAG SLOT FREE", "[A] - THE BAG IS FULL"], rooms);

        string[] left =
        [
            AlchemistPage.ClosedLine,
            AlchemistPage.FarmLine(99, Most), AlchemistPage.FarmLine(1, 1), AlchemistPage.FarmLine(99, 0),
            AlchemistPage.SpoilNote,
            AlchemistPage.QualityLine(Most), AlchemistPage.QualityLine(1),
            Alchemist.NoBatchNotCounted, Alchemist.NoBatchFarmIdle, Alchemist.NoBatchYet,
            AlchemistPage.BatchLine(Most, Most), AlchemistPage.BatchLine(0, Most),
            .. rooms,
        ];
        Assert.Equal((62, 62, 63), (AlchemistPage.ClosedLine.Length, AlchemistPage.FarmLine(99, Most).Length, AlchemistPage.QualityLine(Most).Length));
        foreach (string text in left)
        {
            Fits(text, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth, "the left column");
            Glyphs(text);
        }
        var rows = new[]
        {
            new AlchemistPage.Row(AlchemistPage.RowKind.Member, new AlchemistPage.Place("A", 0), rooms[0]),
            new AlchemistPage.Row(AlchemistPage.RowKind.Potion, new AlchemistPage.Place("A", GameConstants.MaxInventorySlots - 1),
                AlchemistPage.PotionText(GameConstants.MaxInventorySlots - 1, Health(Most)), Health(Most)),
        };
        foreach (var row in rows)
            foreach (bool chosen in new[] { false, true })
            {
                string text = Assert.Single(AlchemistPage.Runs(row, chosen)).Text;
                Fits(text, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth, "a row");
                Glyphs(text);
            }

        // The detail, on a member whose pools are as large as a save holds, at the
        // largest quality: the mix lines break where one line would run past the
        // column, the potion's line and what it restores under it.
        var vast = TestPools.Holding("A", TestWeapons.Get("staff_of_renewal"));
        vast.HpXp = ContentBounds.MostCount;
        vast.ManaXp = ContentBounds.MostCount;
        vast.Inventory[^1] = Health(Most);
        var campaign = new CampaignState([vast]);
        var details = new List<BetweenRunsHud.ListRow>();
        foreach (int quality in new[] { 1, 5, Most })
            details.AddRange(AlchemistPage.DetailRows(campaign, AlchemistPage.Rows(campaign)[0], quality));
        details.AddRange(AlchemistPage.DetailRows(campaign, AlchemistPage.Rows(campaign)[1], Most));
        Assert.Contains(details, row => row.Text.StartsWith("   RESTORES ALL", StringComparison.Ordinal));   // the worst breaks
        foreach (var row in details)
        {
            Fits(row.Text, row.Scale, BetweenRunsHud.ColumnWidth, "the detail");
            Glyphs(row.Text);
        }
        foreach (string text in new[] { AlchemistPage.PotionHeading(Health(Most), new AlchemistPage.Place("A", GameConstants.MaxInventorySlots - 1)), AlchemistPage.MemberHeading(vast) })
            Fits(text, BetweenRunsHud.DetailNameScale, BetweenRunsHud.ColumnWidth, "a detail heading");
        foreach (string text in new[] { AlchemistPage.PourLine(Most), AlchemistPage.DrinkLine(10_000), AlchemistPage.DrinkLine(0) })
        {
            Fits(text, BetweenRunsHud.DetailScale, BetweenRunsHud.ColumnWidth, "the detail");
            Glyphs(text);
        }
        using (TestContent.UsePremise(tuning: t => Mixing(t) with { HealthPotionPercent = 1, ManaPotionPercent = 3 }))
        {
            Assert.Equal("HEALTH IS WHOLE AT QUALITY 100 - MANA AT 34", AlchemistPage.WholeLine());
            Fits(AlchemistPage.WholeLine(), BetweenRunsHud.DetailScale, BetweenRunsHud.ColumnWidth, "the detail");
        }

        // The refusals, said in the refusal row across the window, and the keys lines.
        foreach (string line in new[]
                 {
                     Alchemist.Spent, Alchemist.Short(Most, Most - 1), Alchemist.NoRoom("A"), AlchemistPage.ClosedLine,
                     BetweenRunsHud.AlchemistKeysLine, BetweenRunsHud.AlchemistClosedKeysLine, BetweenRunsHud.WeaponsKeysLine,
                 })
        {
            Fits(line, BetweenRunsHud.KeysLineScale, BetweenRunsHud.FullWidth, "a line across the window");
            Glyphs(line);
        }
        Assert.Equal(117, BetweenRunsHud.AlchemistKeysLine.Length);
    }
}
