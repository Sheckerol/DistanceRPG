using System.Reflection;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.1's Party group and §6.7's campaign, saved between runs (PHASE5-009–015,
/// -017, -018, -081–083, -094, -149): a campaign becomes a versioned document
/// and comes back field for field — the party, what they carry with both
/// spreads, their XP, statuses and raw pools, what the campaign has met, the
/// run counter, the locks, the cleared and retired sets, the last run's
/// summary — through internal doors, with the tuning's hash recorded and a
/// different tuning reported rather than refused. Campaigns are the game's own
/// (<see cref="CampaignState.Start"/>), played through whole runs on real floors
/// with catalogue weapons only: a save names content by id, and an ad-hoc test
/// weapon is in no content. The runs stand on the numbers they were written
/// against — the seeds' depths, the mercy floor, the carry, the budget
/// (<see cref="Premise"/>) — made current for each test; a restore runs under it,
/// and several cases swap it (another tuning, another carry, a reordered file), so
/// the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class CampaignSaveTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>A seed a dungeon on the tuning's 5–10 range rolls five floors on, every such dungeon alike (pinned where a test leans on it).</summary>
    private const long FiveDeep = 4;

    private static int Ten() => 10;

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    // ── Playing runs, with no fighting in them ───────────────────────────────

    /// <summary>Stand the party round <paramref name="tile"/> as a party that walked there would — around living enemies, never on them.</summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) tile)
    {
        var floor = visit.Current;
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, tile.R, tile.C, party.Count, taken);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = Displacer.CentreOf(tiles[i].R, tiles[i].C);
    }

    private static void Down(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        visit.OpenTurns(party, Ten);
    }

    private static void Up(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.UpStairs);
        visit.Ascend(party);
        visit.OpenTurns(party, Ten);
    }

    /// <summary>Back up to the first floor and out by its way out.</summary>
    private static RunResult Out(CampaignState campaign)
    {
        var visit = campaign.Visit!;
        while (visit.FloorIndex > 0)
            Up(visit, campaign.Party);
        Gather(visit, campaign.Party, visit.Current.UpStairs);
        return campaign.EndRun(RunEnding.Exited);
    }

    private static DungeonState In(CampaignState campaign, string dungeonId, long seed)
    {
        var visit = campaign.Enter(dungeonId, seed);
        visit.OpenTurns(campaign.Party, Ten);
        return visit;
    }

    /// <summary>
    /// A clear: down every floor, the boss put down as its fall records it, its
    /// own drop taken into <paramref name="taker"/>'s bag when a taker is named
    /// (§4.3: forged with the theme, and the floor's promised stacks on it), and
    /// out by the way out.
    /// </summary>
    private static RunResult Clear(CampaignState campaign, string dungeonId, long seed = FiveDeep, PartyMemberState? taker = null)
    {
        var visit = In(campaign, dungeonId, seed);
        while (visit.Current.DownStairs is not null)
            Down(visit, campaign.Party);
        var boss = visit.Current.Boss!;
        boss.Alive = false;
        visit.RecordBossDefeated();
        if (taker is not null)
            Assert.NotEqual(Pickup.BagFull, Pickup.Take(campaign, boss, BossLoot.Roll(boss, visit.Current.Seed), taker, visit));
        return Out(campaign);
    }

    /// <summary>A retreat: <paramref name="floors"/> floors down with the boss standing, and back out.</summary>
    private static RunResult Retreat(CampaignState campaign, string dungeonId, int floors, long seed = FiveDeep)
    {
        var visit = In(campaign, dungeonId, seed);
        while (visit.FloorIndex < floors - 1)
            Down(visit, campaign.Party);
        return Out(campaign);
    }

    /// <summary>A wipe on the first floor: a weapon taken off a corpse into B's bag, XP gained in two pools, and every member down.</summary>
    private static RunResult Wipe(CampaignState campaign, string dungeonId, long seed = FiveDeep)
    {
        var visit = In(campaign, dungeonId, seed);
        var corpse = visit.Current.Enemies[0];
        var spoil = new Drop(TestWeapons.Get("hatchet"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(campaign, corpse, spoil, campaign.Party[1], visit));
        campaign.Party[0].Credit(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 40));
        campaign.Party[2].Credit(new XpCredit(XpPool.Health, null, 9));
        foreach (var member in campaign.Party)
            member.Alive = false;
        return campaign.EndRun(RunEnding.Wiped);
    }

    // ── Saving ───────────────────────────────────────────────────────────────

    private static string Save(CampaignState campaign, ViewDto? view = null) => SaveCodec.Write(SaveGame.Capture(campaign, view));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    /// <summary>
    /// Capture, write, read, restore — and the restored campaign is the same state
    /// and writes the same text. A run's report's improvement roll as it was drawn
    /// (<see cref="ServiceReport.Roll"/>) is left out: the save declares it does not
    /// keep it — it is the log's, written at the run's end (Phase 6a.4) — while what
    /// the roll did is kept and compared.
    /// </summary>
    private static CampaignState RoundTrip(CampaignState campaign)
    {
        string text = Save(campaign);
        var loaded = Load(text);
        Assert.False(loaded.TuningChanged);
        StateAssert.Equivalent(campaign, loaded.Campaign, "campaign",
            ignore: path => path.StartsWith("campaign.LastResult.Services[", StringComparison.Ordinal) && path.EndsWith("].Roll", StringComparison.Ordinal));
        Assert.Equal(text, Save(loaded.Campaign));
        return loaded.Campaign;
    }

    /// <summary>The restore of <paramref name="text"/> after <paramref name="edit"/> to its tree: refused, and where.</summary>
    private static SaveFormatException RefusedAfter(string text, Action<JsonObject> edit)
    {
        var tree = JsonNode.Parse(text)!.AsObject();
        edit(tree);
        var file = SaveCodec.Read(tree.ToJsonString());
        return Assert.Throws<SaveFormatException>(() => SaveGame.Restore(file, GameContent.Current));
    }

    private static JsonNode Party(JsonObject tree, int member) => tree["campaign"]!["party"]![member]!;

    // ── The round trip ───────────────────────────────────────────────────────

    [Fact]
    public void ACampaignBetweenRunsRoundTripsFieldForField()
    {
        // The flagship (PHASE5-009–015, -017, -018): a campaign some way in — the
        // tutorial beaten and retired, the Spear Hall cleared and locked with its
        // boss's own drop in B's bag, a retreat from three floors down the Long
        // Gallery that counted as a mercy — and then everything a member can carry
        // out of a run.
        var campaign = NewCampaign();
        var (a, b, c, d) = (campaign.Party[0], campaign.Party[1], campaign.Party[2], campaign.Party[3]);

        Assert.Equal(RunOutcome.BossKill, Clear(campaign, "tutorial").Outcome);
        var hall = Clear(campaign, "spear_hall", taker: b);
        Assert.Equal((RunOutcome.BossKill, 5, 1), (hall.Outcome, hall.DeepestFloor, hall.SpoilsKept.Count));
        var retreat = Retreat(campaign, "long_gallery", floors: 3);
        Assert.Equal((RunOutcome.Retreat, true), (retreat.Outcome, retreat.MercyTick));
        Assert.Equal(3, campaign.RunCounter);

        // The boss's drop: forged with the Spear Hall's Brace on top of its row's
        // own spread, and the floor's promised stack acquired on top of that.
        var drop = hall.SpoilsKept[0];
        Assert.Same(drop, b.Inventory.Single(w => ReferenceEquals(w, drop)));
        Assert.Equal(1, drop.Forged.Stacks(ModifierType.Brace));
        Assert.NotEqual(TestWeapons.Get(drop.Id).Forged, drop.Forged);
        Assert.NotEqual(drop.Forged, drop.Modifiers);

        // A takes up a Nameless Knife: three souls, vampiric at tier 3 with XP into
        // it, and a unique soul whose spend banked and bought nothing.
        var knife = TestWeapons.Get("flensing_knife_unique");
        Assert.Equal(["serrated", "vampiric", "overheal"], knife.Enchantments.Select(e => e.Id));
        knife.CreditEnchantment(1, 7, stat: 1);
        knife.CreditEnchantment(0, 4, stat: 1);
        Assert.Equal([(1, 4), (3, 7), (1, 0)], knife.Enchantments.Select(e => (e.Tier, e.Xp)));
        a.Inventory[2] = knife;
        campaign.See(knife);

        // XP in all three pools, weapon XP in two classes.
        a.Credit(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 140));
        a.Credit(new XpCredit(XpPool.Weapon, WeaponClass.Staff, 3));
        c.Credit(new XpCredit(XpPool.Health, null, 12));
        d.Credit(new XpCredit(XpPool.Mana, null, 30));

        // C walks out carrying statuses — the hidden Overheal remainder among them
        // — and a turn's movement cut by Mire from a banked bonus.
        c.ApplyStatus(StatusEffectType.Poison, null, 2);
        c.ApplyStatus(StatusEffectType.OverhealPool, null, 3);
        c.ApplyStatus(StatusEffectType.Searing, DamageType.Flaming, 1);
        c.ApplyStatus(StatusEffectType.Mire, null, 3);
        c.SavedMovement = 40;
        c.StartTurn();
        Assert.Equal((200f, 140f), (c.BudgetBeforeMire, c.EffectiveMax));
        c.DistLeft -= 37.123457f;   // a walk is its frames' steps: no short decimal form
        c.X += 3.1415927f;
        c.Y -= 0.3333333f;
        c.EndTurnSaveMovement();

        // D has spent and been hurt and holds its shots; B fell, carries an innate
        // and an attunement no member has yet, and lies where it fell.
        d.Mana = 3;
        d.Hp = 11;
        d.HeldShots = 2;
        b.Alive = false;
        b.Innate = ModifierSet.Of((ModifierType.Block, 2));
        b.Attunement = DamageType.Cold;

        var view = new ViewDto("C");
        var file = SaveGame.Capture(campaign, view);
        string text = SaveCodec.Write(file);
        var loaded = SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);
        Assert.False(loaded.TuningChanged);
        Assert.Equal(view, loaded.View);
        StateAssert.Equivalent(campaign, loaded.Campaign, "campaign");
        Assert.Equal(text, SaveCodec.Write(SaveGame.Capture(loaded.Campaign, loaded.View)));

        // Each thing §5.1 names, in the document as plain values.
        var saved = file.Campaign.Party;
        Assert.Equal((c.X, c.Y), (saved[2].Body.X, saved[2].Body.Y));                              // PHASE5-009: position
        Assert.Equal((11, 3), (saved[3].Body.Hp, saved[3].Body.Mana));                              // PHASE5-010, -011: HP, mana
        Assert.Equal(new StatsDto(4, 2, 3, 1), saved[2].Stats);                                     // PHASE5-012: innate stats
        Assert.Equal(new[] { ("Dagger", 140), ("Staff", 3) }, saved[0].WeaponXp.Select(x => (x.Key, x.Value)));   // PHASE5-013: by class name
        Assert.Equal((12, 30), (saved[2].HpXp, saved[3].ManaXp));
        var knifeRow = saved[0].Inventory[2]!;                                                     // PHASE5-014: both spreads
        Assert.Equal(new[] { ("CritWindow", 2), ("CritMultiplier", 1), ("Light", 1) }, knifeRow.Forged.Select(x => (x.Key, x.Value)));
        var dropRow = saved[1].Inventory.Single(w => w?.Id == drop.Id && w.Forged.ContainsKey("Brace"))!;
        Assert.NotEqual(dropRow.Forged.ToArray(), dropRow.Modifiers.ToArray());
        Assert.Equal(new[] { ("serrated", 1, 4), ("vampiric", 3, 7), ("overheal", 1, 0) },        // PHASE5-015: enchantments
            knifeRow.Enchantments.Select(e => (e.Id, e.Tier, e.Xp)));
        Assert.Equal(new[] { ("Poison", (string?)null, 2), ("OverhealPool", null, 3), ("Searing", "Flaming", 1), ("Mire", null, 3) },
            saved[2].Body.Statuses.Select(s => (s.Type, s.Element, s.Levels)));                     // PHASE5-017, -018
        Assert.Equal(6, saved[0].Inventory.Count);
    }

    [Fact]
    public void TheFieldForFieldComparisonSeesOneUlpAndOneStack()
    {
        // The round trips here lean on StateAssert, so it is held to finding the
        // smallest differences a restore could make: a position one ulp off, one
        // acquired stack, one entry's XP, one status, the Mire whole, an XP pool —
        // each named by its path — and an entry that reads alike but is not the
        // content's own row. (What sits below the public state, the raw pools, is
        // the captures' texts' to compare, and every round trip compares them.)
        string text = Save(NewCampaign());
        void Differs(Action<CampaignState> nudge, string path)
        {
            var original = Load(text).Campaign;
            var nudged = Load(text).Campaign;
            StateAssert.Equivalent(original, nudged, "campaign");
            nudge(nudged);
            var failure = Assert.ThrowsAny<Exception>(() => StateAssert.Equivalent(original, nudged, "campaign"));
            Assert.StartsWith(path, failure.Message, StringComparison.Ordinal);
        }

        Differs(c => c.Party[2].X = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(c.Party[2].X) + 1), "campaign.Party[2].X");
        Differs(c => c.Party[0].Inventory[0]!.Acquire(ModifierType.CritWindow, 1), "campaign.Party[0].EquippedWeapon.Modifiers");
        Differs(c => c.Party[3].Inventory[0]!.CreditEnchantment(0, 1, stat: 1), "campaign.Party[3].EquippedWeapon.Enchantments[0].Xp");
        Differs(c => c.Party[1].ApplyStatus(StatusEffectType.Poison, null, 1), "campaign.Party[1].StatusEffects");
        Differs(c => c.Party[1].BudgetBeforeMire = 161f, "campaign.Party[1].BudgetBeforeMire");
        Differs(c => c.Party[1].HpXp = 30, "campaign.Party[1].HealthPool");

        var catalogue = GameContent.Current.Enchantments;
        Differs(c =>
        {
            var staff = c.Party[3].Inventory[0]!;
            var copy = catalogue["regeneration"] with { };   // reads alike, and is not the catalogue's row
            c.Party[3].Inventory[0] = new Weapon(GameContent.Current.Weapons[staff.Id], staff.Forged, staff.Modifiers,
                [new Enchantment(copy, staff.Enchantments[0].Tier)], staff.ForgedEnchantmentCount);
        }, "campaign.Party[3].EquippedWeapon.Enchantments[0].Def");
    }

    [Fact]
    public void AWeaponKeepsItsForgedAndItsLiveSpreads()
    {
        // PHASE5-014: "each weapon carrying both its forged and its live modifier
        // sets". The forged spread is what the ceiling is read off, so a boss drop
        // forged with its theme must come back forged with it — not re-read off its
        // catalogue row, which never had it — and keep deepening to the same cap.
        var campaign = NewCampaign();
        var drop = GameContent.Current.Weapons.Instantiate("weakspot_stiletto", forgedGraft: ModifierType.Block);
        drop.Acquire(ModifierType.CritWindow, 2);
        drop.Acquire(ModifierType.Block, 1);
        campaign.Party[0].Inventory[2] = drop;

        var restored = RoundTrip(campaign).Party[0].Inventory[2]!;
        Assert.NotSame(drop, restored);
        Assert.Equal(drop.Forged, restored.Forged);
        Assert.Equal(drop.Modifiers, restored.Modifiers);
        Assert.Equal(1, restored.Forged.Stacks(ModifierType.Block));
        Assert.Equal(0, TestWeapons.Get("weakspot_stiletto").Forged.Stacks(ModifierType.Block));
        Assert.Equal((drop.ResolvedCost, drop.ResolvedManaCost), (restored.ResolvedCost, restored.ResolvedManaCost));

        // After the restore, Acquire clamps against the saved forged count: forged
        // Block x1 caps at one more than a row with none, exactly as the live item does.
        int headroom = GameContent.Current.Tuning.AcquiredHeadroom;
        drop.Acquire(ModifierType.Block, 50);
        restored.Acquire(ModifierType.Block, 50);
        Assert.Equal(1 + headroom, restored.Stacks(ModifierType.Block));
        Assert.Equal(drop.Modifiers, restored.Modifiers);
        var plain = TestWeapons.Get("weakspot_stiletto");
        plain.Acquire(ModifierType.Block, 50);
        Assert.Equal(headroom, plain.Stacks(ModifierType.Block));

        // A live spread below the forged one is no state play reaches: refused, where.
        string text = Save(campaign);
        var below = RefusedAfter(text, tree => Party(tree, 0)["inventory"]![2]!["modifiers"]!["Block"] = 0);
        Assert.Equal("campaign.party[0].inventory[2].modifiers.Block", below.Where);

        // And the restore door itself refuses both states no play reaches,
        // whoever calls it: a live spread below the forged one, and a forged
        // count that is not a prefix of the entries.
        var def = GameContent.Current.Weapons["weakspot_stiletto"];
        var regeneration = new Enchantment(GameContent.Current.Enchantments["regeneration"], 1);
        Assert.Throws<ArgumentException>(() => new Weapon(def, drop.Forged, ModifierSet.Empty, [], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(def, drop.Forged, drop.Modifiers, [regeneration], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(def, drop.Forged, drop.Modifiers, [regeneration], -1));
        var door = new Weapon(def, drop.Forged, drop.Modifiers, [regeneration], 1);
        Assert.Equal((def.Name, def.Damage, 1), (door.Name, door.Damage, door.ForgedEnchantmentCount));   // the statline is the row's
    }

    [Fact]
    public void TheQueueRoundTrips_OrdinalsAndRunsWorked()
    {
        // §6.7 (Phase 6a.3a): the enchanter's queue is saved with the campaign, in
        // deposit order, each deposit whole — its ordinal, its service by name, its
        // weapon, what it cashed, its runs taken and worked, its enchantment by id —
        // with how many deposits were ever made, and the last run's reports at the
        // enchanter with its result; and each comes back field for field, captured
        // again the same text. A take-back leaves a gap in the ordinals, which comes
        // back as it was.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        var campaign = TestShop.Shop();
        Enchanter.TakeBack(campaign, campaign.AtTheEnchanter[0]);
        var restored = RoundTrip(campaign);
        Assert.Equal([(1, 1, 2, "vampiric"), (2, 0, 1, null)],
            restored.AtTheEnchanter.Select(deposit => (deposit.Ordinal, deposit.RunsWorked, deposit.Runs, deposit.EnchantmentId)));
        Assert.Equal(3, restored.DepositsMade);
        Assert.Equal(campaign.AtTheEnchanter.Select(deposit => deposit.Weapon.Id), restored.AtTheEnchanter.Select(deposit => deposit.Weapon.Id));
        Assert.Equal((35, 0, 195), (restored.AtTheEnchanter[0].WearAtDeposit, restored.AtTheEnchanter[0].Weapon.Wear, restored.AtTheEnchanter[0].Weapon.WearCashed));

        var tree = JsonNode.Parse(Save(campaign))!.AsObject();
        var queue = tree["campaign"]!["enchanter"]!.AsArray();
        Assert.Equal(["ordinal", "service", "weapon", "wearAtDeposit", "runs", "runsWorked", "enchantment", "source", "sourceIndex"],   // a transfer's source, Phase 6a.6
            queue[0]!.AsObject().Select(member => member.Key));
        Assert.Equal(("Enchant", "vampiric"), (queue[0]!["service"]!.GetValue<string>(), queue[0]!["enchantment"]!.GetValue<string>()));
        Assert.Equal(3, tree["campaign"]!["depositsMade"]!.GetValue<int>());
        var reports = tree["campaign"]!["lastResult"]!["services"]!.AsArray();
        Assert.Equal(2, reports.Count);
        Assert.Equal(["weaponId", "service", "runsWorked", "runs", "stalled", "enchantment", "improved", "modifier", "tier", "sourceId"],   // the roll's outcome, Phase 6a.4; a transfer's, Phase 6a.6
            reports[1]!.AsObject().Select(member => member.Key));

        // The restored shop works on as the one saved: the next run that counts
        // brings both back.
        TestShop.Counted(restored);
        Assert.Empty(restored.AtTheEnchanter);
        Assert.Equal(["regeneration", "vampiric"], restored.Stable.Single(weapon => weapon.Enchantments.Count == 2).Enchantments.Select(entry => entry.Id));

        // An empty shop and a run's end with nothing in it write null, as the stable
        // does — the count stays — and null and [] read alike, as empty.
        var empty = TestShop.Opened();
        Assert.Null(empty.LastResult!.Services);
        string text = Save(empty);
        var emptyTree = JsonNode.Parse(text)!.AsObject();
        Assert.True(emptyTree["campaign"]!.AsObject().ContainsKey("enchanter"));
        Assert.Null(emptyTree["campaign"]!["enchanter"]);
        Assert.Equal(0, emptyTree["campaign"]!["depositsMade"]!.GetValue<int>());
        Assert.True(emptyTree["campaign"]!["lastResult"]!.AsObject().ContainsKey("services"));
        Assert.Null(emptyTree["campaign"]!["lastResult"]!["services"]);
        emptyTree["campaign"]!["enchanter"] = new JsonArray();
        emptyTree["campaign"]!["lastResult"]!["services"] = new JsonArray();
        var bracketed = Load(emptyTree.ToJsonString()).Campaign;
        Assert.Empty(bracketed.AtTheEnchanter);
        Assert.Null(bracketed.LastResult!.Services);
        Assert.Equal(text, Save(bracketed));
        Assert.Equal(3, RoundTrip(restored).DepositsMade);

        // And the door a load comes back through refuses what no shop holds: a
        // deposit out of order, one at or past the count, a weapon in two places.
        var party = NewCampaign().Party;
        var guard = TestWeapons.Get("tower_guard");
        Deposit Of(int ordinal, Weapon weapon) => new(ordinal, weapon, Service.Refine, 0, 1);
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [], [Of(1, guard), Of(0, TestWeapons.Get("tower_guard"))], 2));
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [], [Of(2, guard)], 2));
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [guard], [Of(0, guard)], 1));
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [], [null!], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [], [], -1));
    }

    [Fact]
    public void WearAndWhatWasCashedRoundTrip()
    {
        // §6.1, §6.7 (Phase 6a.1): a weapon's wear and the wear it has ever cashed
        // are the item's own, saved with it and brought back — in a hand, in a bag,
        // on a spoil — and its capacity comes back replayed from what was cashed,
        // under the tuning loaded now (PHASE 6a DECISION 2): no capacity is in the
        // document, so a StartingPool or WearCapacityPerBar edit moves it.
        var campaign = NewCampaign();
        Clear(campaign, "tutorial");
        var def = GameContent.Current.Weapons["arming_sword"];
        var sword = TestWeapons.Get("arming_sword");
        var serviced = new Weapon(def, sword.Forged, sword.Modifiers, sword.Enchantments, sword.ForgedEnchantmentCount, wear: 17, wearCashed: 195);
        Assert.Equal((17, 195, 36), (serviced.Wear, serviced.WearCashed, serviced.WearCapacity));
        campaign.Party[0].Inventory[2] = serviced;
        var hand = campaign.Party[1].Inventory[0]!;
        for (int hit = 0; hit < 40; hit++)
            Wear.Accrue(hand);
        Assert.Equal((30, true), (hand.Wear, hand.WornFull));

        var restored = RoundTrip(campaign);
        var back = restored.Party[0].Inventory[2]!;
        Assert.NotSame(serviced, back);
        Assert.Equal((17, 195, 36, serviced.WearPool), (back.Wear, back.WearCashed, back.WearCapacity, back.WearPool));
        Assert.Equal((30, 0, true), (restored.Party[1].Inventory[0]!.Wear, restored.Party[1].Inventory[0]!.WearCashed, restored.Party[1].Inventory[0]!.WornFull));

        // The document holds the two counts and no capacity.
        var row = Party(JsonNode.Parse(Save(campaign))!.AsObject(), 0)["inventory"]![2]!.AsObject();
        Assert.Equal((17, 195), (row["wear"]!.GetValue<int>(), row["wearCashed"]!.GetValue<int>()));
        Assert.DoesNotContain(row, member => member.Key.Contains("capacity", StringComparison.OrdinalIgnoreCase));

        // Under another bar the same save replays another capacity, and the wear
        // is kept as it was, full or not: the numbers changed, and it says so.
        string text = Save(campaign);
        using (TestContent.UsePremise(tuning: t => t with { StartingPool = 40, WearCapacityPerBar = 2 }))
        {
            var loaded = Load(text);
            Assert.True(loaded.TuningChanged);
            var under = loaded.Campaign.Party[0].Inventory[2]!;
            Assert.Equal((17, 195, Wear.Capacity(195).Max), (under.Wear, under.WearCashed, under.WearCapacity));
            Assert.Equal(48, under.WearCapacity);   // bars of 40, 42, 44 and 46 of the 195 cashed: 40 + 2 x 4, and 23 toward the next
            Assert.False(loaded.Campaign.Party[1].Inventory[0]!.WornFull);
        }

        // A negative count is none play reaches: the restore door refuses it, whoever calls it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(def, sword.Forged, sword.Modifiers, [], 0, wear: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(def, sword.Forged, sword.Modifiers, [], 0, wearCashed: -1));
    }

    [Fact]
    public void ATuningHashVersionOutsideItsDocumentIsRefused()
    {
        // PHASE 6a DECISION 9: a save names the version whose keys its tuning hash
        // is of — the current version from a capture, 1 from the step that lifts a
        // version-1 document. From 1 to the document's own version: a hash is never
        // of a key set newer than its document, and version 0 hashed nothing. Raw
        // text a hand can edit, so anything else is refused, naming the member; and
        // a version-2 document without it is refused by the reader, as any missing
        // member is.
        var campaign = NewCampaign();
        Clear(campaign, "tutorial");
        string text = Save(campaign);
        var file = SaveCodec.Read(text);
        Assert.Equal(SaveCodec.CurrentVersion, file.TuningHashVersion);
        Assert.False(SaveGame.Restore(file, GameContent.Current).TuningChanged);

        foreach (int version in new[] { 0, -1, SaveCodec.CurrentVersion + 1, int.MaxValue })
        {
            var refused = Assert.Throws<SaveFormatException>(() => SaveGame.Restore(file with { TuningHashVersion = version }, GameContent.Current));
            Assert.Equal("tuningHashVersion", refused.Where);
            Assert.StartsWith($"{version} is no version of this document", refused.Problem, StringComparison.Ordinal);
            Assert.All(NoticeText.Wrap(refused.Message), line => Assert.Matches(@"^[A-Z0-9 .,:\-_!?()/<>\[\]+=#*]+$", line));   // the notice of a save set aside shows it
        }

        // Version 1's keys, the hash of the same numbers on them: nothing changed.
        var ofVersion1 = file with { TuningHash = TuningJson.Hash(GameContent.Current.Tuning, version: 1), TuningHashVersion = 1 };
        Assert.False(SaveGame.Restore(ofVersion1, GameContent.Current).TuningChanged);

        var tree = JsonNode.Parse(text)!.AsObject();
        Assert.True(tree.Remove("tuningHashVersion"));
        var missing = Assert.Throws<SaveFormatException>(() => SaveCodec.Read(tree.ToJsonString()));
        Assert.Equal("$", missing.Where);
        Assert.Contains("tuningHashVersion", missing.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void EnchantmentsKeepTheirOrderTierAndXp()
    {
        // PHASE5-015, settled.md: "Weapon.Enchantments is a list because order is
        // gameplay, so it must never be normalised, sorted or deduped. A migration
        // that rebuilds it in a different sequence is a silent balance change." Each
        // entry comes back at its tier with the XP into it, as the content's own
        // entry — the tuned def, so a unique's pin holds — and the forged ones stay
        // the forged prefix.
        var campaign = NewCampaign();
        var catalogue = GameContent.Current.Enchantments;
        var knife = TestWeapons.Get("flensing_knife_unique");
        knife.CreditEnchantment(1, 7, stat: 1);
        knife.CreditEnchantment(2, 5, stat: 1);
        var sword = new Weapon(GameContent.Current.Weapons["arming_sword"],
            [new Enchantment(catalogue["vampiric"], 2) { Xp = 5 }, new Enchantment(catalogue["shattering"], 4) { Xp = 9 }, new Enchantment(catalogue["arcane"], 1)],
            forgedCount: 1);
        var wand = TestWeapons.Get("wand_of_the_nova_unique");
        campaign.Party[0].Inventory[2] = knife;
        campaign.Party[0].Inventory[3] = sword;
        campaign.Party[3].Inventory[2] = wand;

        var restored = RoundTrip(campaign);
        foreach (var (was, now) in new[] { (knife, restored.Party[0].Inventory[2]!), (sword, restored.Party[0].Inventory[3]!), (wand, restored.Party[3].Inventory[2]!) })
        {
            Assert.Equal(was.Enchantments.Select(e => (e.Id, e.Tier, e.Xp)), now.Enchantments.Select(e => (e.Id, e.Tier, e.Xp)));
            Assert.Equal(was.ForgedEnchantmentCount, now.ForgedEnchantmentCount);
            Assert.All(now.Enchantments, e => Assert.Same(catalogue[e.Id], e.Def));
        }

        // Never sorted: the sword's entries come back in the order they were
        // attached, the forged one first and two acquired behind it.
        var restoredSword = restored.Party[0].Inventory[3]!;
        Assert.Equal(["vampiric", "shattering", "arcane"], restoredSword.Enchantments.Select(e => e.Id));
        Assert.Equal([true, false, false], Enumerable.Range(0, 3).Select(restoredSword.IsForged));
        Assert.Equal(sword.TotalLock, restoredSword.TotalLock);

        // A unique's tier is pinned whatever a text says (§3.3): the pin is the
        // entry's own, so an edited tier cannot lift it.
        string text = Save(campaign);
        var tree = JsonNode.Parse(text)!.AsObject();
        Party(tree, 0)["inventory"]![2]!["enchantments"]![0]!["tier"] = 3;   // serrated, a unique soul
        var pinned = SaveGame.Restore(SaveCodec.Read(tree.ToJsonString()), GameContent.Current).Campaign;
        Assert.Equal(Enchantment.UniqueTier, pinned.Party[0].Inventory[2]!.Enchantments[0].Tier);
    }

    [Fact]
    public void UnwrittenHpAndManaStayUnwritten()
    {
        // A pool nobody has written reads as its ceiling, and follows the ceiling
        // as it moves; a pool written full stays where it was written. A new
        // campaign's members have written neither, and a save keeps that: the
        // document says null, and the restored pool still follows its ceiling.
        var campaign = NewCampaign();
        var d = campaign.Party[3];
        Assert.Equal("staff_of_renewal", d.EquippedWeapon!.Id);
        Assert.Equal((null, null), (d.RawHp, d.RawMana));

        string text = Save(campaign);
        var tree = JsonNode.Parse(text)!.AsObject();
        Assert.Null(Party(tree, 3)["body"]!["hp"]);
        Assert.Null(Party(tree, 3)["body"]!["mana"]);
        var restored = Load(text).Campaign.Party[3];
        Assert.Equal((null, null), (restored.RawHp, restored.RawMana));

        // The same member written full at the same number, for the contrast.
        var written = Load(text).Campaign.Party[3];
        written.Mana = written.Mana;
        Assert.Equal(restored.Mana, written.Mana);

        // The lock deepens past the pool: Renewal climbs to tier 3, locks 45 of 30,
        // and sleeps — the whole pool is spendable again. The unwritten pool reads
        // the new ceiling, as the live member's does; the written one does not.
        foreach (var member in new[] { d, restored, written })
            member.EquippedWeapon!.CreditEnchantment(0, 15 + 30, stat: 1);
        Assert.Equal(3, restored.EquippedWeapon!.Enchantments[0].Tier);
        Assert.Equal((d.UsableMaxMana, d.UsableMaxMana), (d.Mana, restored.Mana));
        Assert.True(restored.Mana > written.Mana, $"the unwritten pool reads {restored.Mana}, the written one {written.Mana}");

        // HP the same way: a raw XP write moves the maximum, and an unwritten pool reads it.
        d.HpXp = restored.HpXp = 60;
        Assert.Equal((d.MaxHp, d.MaxHp), (d.Hp, restored.Hp));
        Assert.True(restored.Hp > GameContent.Current.Tuning.StartingPool);
    }

    [Fact]
    public void TheWholeMireCutFromIsKeptOnTheMember()
    {
        // BudgetBeforeMire moves into Logic (1b): the base budget plus the bank a
        // turn's start cashed in, before Mire's cut — what a Mire legend states
        // beside the MOVE readout's cap. The bank is spent by the time anyone
        // reads it, so the member keeps the whole, and a save carries it.
        var member = NewCampaign().Party[0];
        int budget = GameContent.Current.Tuning.MovementBudget;
        Assert.Equal((float)budget, member.BudgetBeforeMire);   // no turn has started: the budget alone

        member.SavedMovement = 30;
        member.ApplyStatus(StatusEffectType.Mire, null, 5);
        member.StartTurn();
        Assert.Equal((budget + 30f, (budget + 30f) / 2), (member.BudgetBeforeMire, member.EffectiveMax));
        Assert.Equal(0f, member.SavedMovement);

        // The rest before a dungeon starts the turn with nothing banked.
        member.Rest();
        Assert.Equal((float)budget, member.BudgetBeforeMire);

        // And under a tuned budget, the whole is the tuned one plus the bank.
        using (TestContent.UsePremise(tuning: t => t with { MovementBudget = 200 }))
        {
            member.SavedMovement = 12;
            member.StartTurn();
            Assert.Equal(212f, member.BudgetBeforeMire);
        }
    }

    [Fact]
    public void AMemberKeepsTheSlotsItWasBuiltWith()
    {
        // A bag is the size it was built with (PartyMemberState.InventorySlots is
        // read when a member is built), and a save carries the length: a changed
        // PartyCarrySlots gives new members the new count and never resizes a bag
        // that exists — a six-slot bag stays six under a tuning of 36, and a
        // nine-slot one stays nine under 24.
        string six = Save(NewCampaign());
        string nine;
        using (TestContent.UsePremise(tuning: t => t with { PartyCarrySlots = 36 }))
        {
            Assert.Equal(9, PartyMemberState.InventorySlots);
            var loaded = Load(six);
            Assert.True(loaded.TuningChanged);
            Assert.All(loaded.Campaign.Party, member => Assert.Equal(6, member.Inventory.Length));

            var built = NewCampaign();
            Assert.All(built.Party, member => Assert.Equal(9, member.Inventory.Length));
            nine = Save(built);
        }

        Assert.Equal(6, PartyMemberState.InventorySlots);
        Assert.All(Load(nine).Campaign.Party, member => Assert.Equal(9, member.Inventory.Length));
        Assert.Equal(nine.Replace(TuningJson.Hash(Premise.Tuning with { PartyCarrySlots = 36 }), TuningJson.Hash(GameContent.Current.Tuning), StringComparison.Ordinal),
            Save(Load(nine).Campaign));

        // A bag no member can carry — no hand, or more slots than there are keys — is refused.
        Assert.Equal("campaign.party[1].inventory", RefusedAfter(six, tree => Party(tree, 1)["inventory"] = new JsonArray()).Where);
        Assert.Equal("campaign.party[1].inventory", RefusedAfter(six, tree =>
            Party(tree, 1)["inventory"] = new JsonArray(Enumerable.Range(0, GameConstants.MaxInventorySlots + 1).Select(_ => (JsonNode?)null).ToArray())).Where);
    }

    [Fact]
    public void TheSeenSetIsSavedAsIdsInOrder()
    {
        // PHASE5-149: "the enchanter's catalogue is a HashSet of enchantment ids in
        // CampaignState. If ids shift, a player's accumulated catalogue ...
        // silently becomes a set of different enchantments, or of nothing." The
        // save holds the ids themselves, in the one order the campaign hands them
        // out (SeenInOrder), never the set's enumeration and never a position in a
        // file.
        var campaign = NewCampaign();
        campaign.See(TestWeapons.Get("flensing_knife_unique"));
        campaign.See(TestWeapons.Get("wand_of_the_nova", DamageType.Cold));
        var file = SaveGame.Capture(campaign);
        Assert.Equal(campaign.SeenInOrder, file.Campaign.Seen);
        Assert.Equal(file.Campaign.Seen.Order(StringComparer.Ordinal), file.Campaign.Seen);
        Assert.Contains("overheal", file.Campaign.Seen);

        // The same entries met in the opposite order write the same list.
        var backwards = NewCampaign();
        backwards.See(TestWeapons.Get("wand_of_the_nova", DamageType.Cold));
        backwards.See(TestWeapons.Get("flensing_knife_unique"));
        Assert.Equal(Save(campaign), Save(backwards));

        // The restored campaign has met exactly those ids...
        string text = Save(campaign);
        var restored = Load(text).Campaign;
        Assert.Equal(campaign.SeenInOrder, restored.SeenInOrder);
        Assert.All(campaign.SeenInOrder, id => Assert.True(restored.HasSeen(id), id));

        // ...and so it does under a catalogue whose entries run in the opposite
        // order (a code edit, simulated): an id means its entry wherever the
        // catalogue puts it.
        var reversed = new EnchantmentsData(ShippedContent.Enchantments.Enchantments.Reverse().ToArray());
        using (TestContent.UsePremise(enchantments: reversed))
        {
            Assert.NotEqual(ShippedContent.Enchantments.Enchantments[0].Id, GameContent.Current.Enchantments.All[0].Id);
            var underReversed = Load(text);
            Assert.False(underReversed.TuningChanged);
            Assert.Equal(campaign.SeenInOrder, underReversed.Campaign.SeenInOrder);
            Assert.Equal(text, Save(underReversed.Campaign));
        }

        // An id the content does not have is refused, never dropped.
        var unknown = RefusedAfter(text, tree => tree["campaign"]!["seen"]![1] = "frostbite");
        Assert.Equal(("campaign.seen[1]", "'frostbite' is no enchantment in the content"), (unknown.Where, unknown.Problem));
        Assert.Equal("campaign.seen[1]", RefusedAfter(text, tree => tree["campaign"]!["seen"]![1] = tree["campaign"]!["seen"]![0]!.GetValue<string>()).Where);
    }

    [Fact]
    public void TheRunsEndRecordsComeBack_WithNoPublicSetter()
    {
        // 4D:278: the run counter, the locks, the cleared set and the retired set
        // come back through an internal door, never a public setter or a public
        // Retire. The campaign here has run the rotation dry: the tutorial retired,
        // all four dungeons cleared and locked, and the early offer taken — its lock
        // rewritten to the counter (settled.md, kept by the user) — and left again.
        var campaign = NewCampaign();
        Clear(campaign, "tutorial");
        foreach (string id in new[] { "spear_hall", "long_gallery", "knife_warrens", "quiver_pits" })
            Assert.Equal(5, Clear(campaign, id).DeepestFloor);
        Assert.Equal(5, campaign.RunCounter);
        string early = campaign.EarlyOffer()!;
        Assert.NotNull(early);

        In(campaign, early, FiveDeep);
        var left = Out(campaign);
        Assert.Equal((RunOutcome.Retreat, false), (left.Outcome, left.ServiceTick));
        Assert.Contains((early, campaign.RunCounter), campaign.LockedInOrder);

        var restored = RoundTrip(campaign);
        Assert.Equal(campaign.RunCounter, restored.RunCounter);
        Assert.Equal(campaign.LockedInOrder, restored.LockedInOrder);
        Assert.Equal(campaign.ClearedInOrder, restored.ClearedInOrder);
        Assert.Equal(["tutorial"], restored.RetiredInOrder);
        Assert.Equal(campaign.EarlyOffer(), restored.EarlyOffer());
        Assert.Equal(campaign.DefaultNext(), restored.DefaultNext());
        Assert.Equal(campaign.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)),
            restored.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)));

        // And the restored campaign plays on: the next run goes in and ends as it would have.
        var next = Retreat(restored, restored.DefaultNext(), floors: 1);
        Assert.Equal(RunOutcome.Retreat, next.Outcome);

        // Still no public way to write what a run's end writes.
        var type = typeof(CampaignState);
        Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => Assert.Null(p.GetSetMethod()));
        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            m => m.Name is "Retire" or nameof(CampaignState.Restore));
        var door = type.GetMethod(nameof(CampaignState.Restore), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True(door.IsAssembly);
        Assert.True(typeof(PartyMemberState).GetMethod(nameof(PartyMemberState.Restore), BindingFlags.NonPublic | BindingFlags.Static)!.IsAssembly);
        Assert.Null(typeof(PartyMemberState).GetProperty(nameof(PartyMemberState.Inventory))!.GetSetMethod());
    }

    [Fact]
    public void TheLastRunsSummaryComesBack()
    {
        // The screen between runs shows the last run's summary above the list; a
        // relaunch shows the same lines. Each ending in turn — a retiring clear, a
        // clear carrying its boss's drop home, a mercy retreat, a wipe that lost a
        // spoil and clawed XP back — saved and restored, and the campaign that
        // plays the next run is the restored one.
        var campaign = NewCampaign();
        Func<CampaignState, RunResult>[] runs =
        [
            c => Clear(c, "tutorial"),
            c => Clear(c, "spear_hall", taker: c.Party[1]),
            c => Retreat(c, "long_gallery", floors: 3),
            c => Wipe(c, "knife_warrens"),
        ];
        var outcomes = new List<RunResult>();
        foreach (var run in runs)
        {
            var result = run(campaign);
            outcomes.Add(result);
            var restored = RoundTrip(campaign);
            Assert.Equal(BetweenRunsHud.SummaryLines(campaign.LastResult!), BetweenRunsHud.SummaryLines(restored.LastResult!));
            campaign = restored;
        }

        Assert.Equal([RunOutcome.BossKill, RunOutcome.BossKill, RunOutcome.Retreat, RunOutcome.Death], outcomes.Select(r => r.Outcome));
        Assert.True(outcomes[0].Retired);
        Assert.Single(outcomes[1].SpoilsKept);
        Assert.NotNull(outcomes[1].LockedUntilRun);
        Assert.True(outcomes[2].MercyTick);
        Assert.Equal("hatchet", Assert.Single(outcomes[3].SpoilsLost).Id);
        Assert.NotEmpty(outcomes[3].Clawbacks);
        Assert.Contains(outcomes[3].Clawbacks, c => c.Pool == nameof(WeaponClass.Dagger));
        Assert.Contains(outcomes[3].Clawbacks, c => c.Pool == XpSnapshot.HpPool);
    }

    [Fact]
    public void TheSaveHoldsTheTuningsHash_NeverItsValues()
    {
        // PHASE5-081, -083: "a save records the hash of the tuning values it was
        // played under ... The full values are not stored." The hash is the
        // effective tuning's, and nowhere in the document is a tuning key.
        var campaign = NewCampaign();
        Clear(campaign, "tutorial");
        var file = SaveGame.Capture(campaign);
        Assert.Equal(TuningJson.Hash(GameContent.Current.Tuning), file.TuningHash);
        Assert.Matches("^[0-9a-f]{16}$", file.TuningHash);

        string text = SaveCodec.Write(file);
        var names = new List<string>();
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (name, value) in obj)
                    {
                        names.Add(name);
                        Walk(value);
                    }
                    break;
                case JsonArray array:
                    foreach (var item in array)
                        Walk(item);
                    break;
            }
        }
        Walk(JsonNode.Parse(text));
        var keys = TuningSchema.Keys.Select(k => k.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(names);
        Assert.DoesNotContain(names, keys.Contains);

        // The same campaign under other numbers writes the same document but for
        // the hash: the numbers themselves are in no line of it.
        using (TestContent.UsePremise(tuning: t => t with { MovementBudget = 200, StartingPool = 40, ArcanePotency = 9 }))
        {
            string other = Save(campaign);
            Assert.NotEqual(text, other);
            Assert.Equal(text.Replace(file.TuningHash, TuningJson.Hash(GameContent.Current.Tuning), StringComparison.Ordinal), other);
        }
    }

    [Fact]
    public void ADifferentTuningWarnsAndStillLoads()
    {
        // PHASE5-082: "loading under different numbers warns rather than refuses".
        // Restored under another tuning, the campaign comes back whole — the same
        // state, as the document it writes shows — and says the numbers changed;
        // it plays on under the new ones. Under the same tuning it says nothing.
        var campaign = NewCampaign();
        Clear(campaign, "tutorial");
        Retreat(campaign, "spear_hall", floors: 2);
        string text = Save(campaign);
        Assert.False(Load(text).TuningChanged);

        var tuned = Premise.Tuning with { MovementBudget = 200, StartingPool = 40 };
        using (TestContent.UsePremise(tuning: _ => tuned))
        {
            var loaded = Load(text);
            Assert.True(loaded.TuningChanged);
            Assert.Equal(text.Replace(TuningJson.Hash(Premise.Tuning), TuningJson.Hash(tuned), StringComparison.Ordinal),
                Save(loaded.Campaign));
            Assert.Equal(campaign.SeenInOrder, loaded.Campaign.SeenInOrder);
            Assert.Equal(campaign.LockedInOrder, loaded.Campaign.LockedInOrder);

            // The new numbers are the ones read: every pool replays from its XP against the new start.
            Assert.All(loaded.Campaign.Party, member => Assert.Equal(40 + member.HealthPool.Points, member.MaxHp));

            // And the next save records the new hash, so the change is said once.
            Assert.False(Load(Save(loaded.Campaign)).TuningChanged);
        }
    }

    [Fact]
    public void AnIdTheContentNoLongerHasRefusesTheSaveNamingWhere()
    {
        // PHASE5-094: "saves, drops, enemy states and the enchanter's
        // seen-catalogue all store ids, never indices or positions" — and an id
        // the content no longer has refuses the whole save, naming where, rather
        // than dropping the item or reading another in its place.
        var campaign = NewCampaign();
        Clear(campaign, "spear_hall", taker: campaign.Party[1]);
        campaign.Party[0].ApplyStatus(StatusEffectType.Poison, null, 2);   // carried out of the run
        string text = Save(campaign);

        (Action<JsonObject> Edit, string Where, string Problem)[] gone =
        [
            (tree => Party(tree, 2)["inventory"]![0]!["id"] = "no_such_axe",
                "campaign.party[2].inventory[0].id", "'no_such_axe' is no weapon in the content"),
            (tree => Party(tree, 3)["inventory"]![1]!["enchantments"]![0]!["id"] = "mud",
                "campaign.party[3].inventory[1].enchantments[0].id", "'mud' is no enchantment in the content"),
            (tree => Party(tree, 0)["body"]!["statuses"]![0]!["type"] = "Venom",
                "campaign.party[0].body.statuses[0].type", "'Venom' is no status effect"),
            (tree => tree["campaign"]!["cleared"]![0] = "spear_hal",
                "campaign.cleared[0]", "'spear_hal' is no dungeon in the content"),
            (tree => tree["campaign"]!["locks"]![0]!["dungeonId"] = "spear_hal",
                "campaign.locks[0].dungeonId", "'spear_hal' is no dungeon in the content"),
            (tree => tree["campaign"]!["lastResult"]!["spoilsKept"]![0]!["id"] = "no_such_blade",
                "campaign.lastResult.spoilsKept[0].id", "'no_such_blade' is no weapon in the content"),
            (tree => Party(tree, 0)["inventory"]![0]!["modifiers"]!["Blok"] = 1,
                "campaign.party[0].inventory[0].modifiers.Blok", "'Blok' is no modifier"),
            (tree => Party(tree, 0)["weaponXp"]!["Dagr"] = 1,
                "campaign.party[0].weaponXp.Dagr", "'Dagr' is no weapon class"),
            (tree => tree["campaign"]!["lastResult"]!["outcome"] = "Victory",
                "campaign.lastResult.outcome", "'Victory' is no run outcome"),
        ];
        foreach (var (edit, where, problem) in gone)
        {
            var refused = RefusedAfter(text, edit);
            Assert.Equal((where, problem), (refused.Where, refused.Problem));
            Assert.Equal($"{where}: {problem}", refused.Message);
        }

        // And a content file that really lost the dungeon: the campaign cleared the
        // Quiver Pits and a dungeons.json without them no longer names what its
        // locks and cleared set do.
        Clear(campaign, "quiver_pits");
        string cleared = Save(campaign);
        using (TestContent.UsePremise(dungeons: data => new DungeonsData(data.Dungeons.Where(def => def.Id != "quiver_pits").ToArray())))
        {
            var refused = Assert.Throws<SaveFormatException>(() => Load(cleared));
            Assert.Equal("'quiver_pits' is no dungeon in the content", refused.Problem);
            Assert.StartsWith("campaign.locks[", refused.Where, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AReorderedContentFileRestoresTheSameCampaign()
    {
        // PHASE5-094's other half: "reordering a file is then free and appending
        // to one costs nothing". The same save, restored under a weapons.json and
        // an enchantment catalogue whose entries run backwards, is the same campaign
        // item for item — every weapon its own row, every entry its own soul — as
        // the document it writes shows.
        var campaign = NewCampaign();
        Clear(campaign, "spear_hall", taker: campaign.Party[1]);
        campaign.Party[0].Inventory[2] = TestWeapons.Get("flensing_knife_unique");
        campaign.Party[2].Inventory[2] = TestWeapons.Get("wand_of_the_beam", DamageType.Shocking);
        string text = Save(campaign);

        var enchantments = new EnchantmentsData(ShippedContent.Enchantments.Enchantments.Reverse().ToArray());
        using (TestContent.UsePremise(weapons: w => w with { Weapons = w.Weapons.Reverse().ToArray() }, enchantments: enchantments))
        {
            Assert.NotEqual(ShippedContent.Weapons.Weapons[0].Id, GameContent.Current.Weapons.All[0].Id);
            var loaded = Load(text);
            Assert.False(loaded.TuningChanged);
            Assert.Equal(text, Save(loaded.Campaign));
            Assert.Equal("flensing_knife_unique", loaded.Campaign.Party[0].Inventory[2]!.Id);
            Assert.Equal(["shocking"], loaded.Campaign.Party[2].Inventory[2]!.Enchantments.Select(e => e.Id));
        }
    }

    [Fact]
    public void ACountNoPlayReachesIsRefusedNamingWhere()
    {
        // A save never limps (1d): a count out of range, a party that is not a
        // party, or two answers to one question is refused whole, naming the path,
        // rather than restored into a state no play could reach — a stat that
        // divides by nothing, a hand with no slot, a tier below the first.
        string text = Save(NewCampaign());
        (Action<JsonObject> Edit, string Where)[] impossible =
        [
            (tree => ((JsonArray)tree["campaign"]!["party"]!).RemoveAt(3), "campaign.party"),
            (tree => Party(tree, 1)["id"] = "A", "campaign.party[1].id"),
            (tree => Party(tree, 1)["id"] = " ", "campaign.party[1].id"),
            (tree => Party(tree, 1)["colorIndex"] = GameConstants.PartySize, "campaign.party[1].colorIndex"),
            (tree => Party(tree, 1)["stats"]!["con"] = 0, "campaign.party[1].stats.con"),
            (tree => Party(tree, 1)["stats"]!["int"] = 5, "campaign.party[1].stats.int"),
            (tree => Party(tree, 1)["hpXp"] = -1, "campaign.party[1].hpXp"),
            (tree => Party(tree, 1)["weaponXp"]!["Sword"] = -5, "campaign.party[1].weaponXp.Sword"),
            (tree => Party(tree, 1)["body"]!["hp"] = -2, "campaign.party[1].body.hp"),
            (tree => Party(tree, 1)["body"]!["statuses"] = JsonNode.Parse("""[{ "type": "Searing", "element": null, "levels": 1 }]"""),
                "campaign.party[1].body.statuses[0].element"),
            (tree => Party(tree, 1)["body"]!["statuses"] = JsonNode.Parse("""[{ "type": "Poison", "element": "Cold", "levels": 1 }]"""),
                "campaign.party[1].body.statuses[0].element"),
            (tree => Party(tree, 1)["body"]!["statuses"] = JsonNode.Parse("""[{ "type": "Poison", "element": null, "levels": 0 }]"""),
                "campaign.party[1].body.statuses[0].levels"),
            (tree => Party(tree, 1)["body"]!["statuses"] = JsonNode.Parse("""[{ "type": "Poison", "element": null, "levels": 1 }, { "type": "Poison", "element": null, "levels": 2 }]"""),
                "campaign.party[1].body.statuses[1]"),
            (tree => Party(tree, 1)["body"]!["statuses"] = JsonNode.Parse("[null]"), "campaign.party[1].body.statuses[0]"),
            (tree => Party(tree, 1)["inventory"]![0]!["enchantments"] = JsonNode.Parse("""[{ "id": "vampiric", "tier": 0, "xp": 0 }]"""),
                "campaign.party[1].inventory[0].enchantments[0].tier"),
            (tree => Party(tree, 1)["inventory"]![0]!["forgedEnchantments"] = 1, "campaign.party[1].inventory[0].forgedEnchantments"),
            (tree => Party(tree, 1)["inventory"]![0]!["forged"]!["Block"] = -1, "campaign.party[1].inventory[0].forged.Block"),
            (tree => tree["campaign"]!["runCounter"] = -1, "campaign.runCounter"),
            (tree => tree["campaign"]!["locks"] = JsonNode.Parse("""[{ "dungeonId": "spear_hall", "lockedUntilRun": 3 }, { "dungeonId": "spear_hall", "lockedUntilRun": 4 }]"""),
                "campaign.locks[1].dungeonId"),
            (tree => tree["campaign"]!["retired"] = JsonNode.Parse("""["tutorial", "tutorial"]"""), "campaign.retired[1]"),
            (tree => tree["campaign"]!["seen"] = JsonNode.Parse("[null]"), "campaign.seen[0]"),
        ];
        foreach (var (edit, where) in impossible)
            Assert.Equal(where, RefusedAfter(text, edit).Where);

        // The last run's summary is held to the same, since the screen between runs
        // shows its numbers: its floors are numbered from 1, and its run counter
        // and its lock are counts of runs, never below 0. A clear's summary names
        // both the boss floor and the lock, and each is edited out of range.
        var ended = NewCampaign();
        var hall = Clear(ended, "spear_hall");
        Assert.NotNull(hall.BossFloorRevealed);
        Assert.NotNull(hall.LockedUntilRun);
        string summary = Save(ended);
        (Action<JsonObject> Edit, string Where)[] unplayed =
        [
            (tree => tree["campaign"]!["lastResult"]!["deepestFloor"] = 0, "campaign.lastResult.deepestFloor"),
            (tree => tree["campaign"]!["lastResult"]!["bossFloorRevealed"] = 0, "campaign.lastResult.bossFloorRevealed"),
            (tree => tree["campaign"]!["lastResult"]!["runCounter"] = -1, "campaign.lastResult.runCounter"),
            (tree => tree["campaign"]!["lastResult"]!["lockedUntilRun"] = -1, "campaign.lastResult.lockedUntilRun"),
        ];
        foreach (var (edit, where) in unplayed)
            Assert.Equal(where, RefusedAfter(summary, edit).Where);

        // The capture is at a safe point — between runs, or at a player turn with
        // nothing in flight (Phase 5.6) — and the restore is under the current content.
        var campaign = NewCampaign();
        In(campaign, "tutorial", FiveDeep).Turns!.EndTurn();   // the banner is up: the enemies' turn has begun
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(campaign));
        var other = TestContent.Load(ShippedContent.Tuning, ShippedContent.Restricted, ShippedContent.Enchantments, ShippedContent.Weapons);
        Assert.Throws<ArgumentException>(() => SaveGame.Restore(SaveCodec.Read(text), other));
        Assert.Throws<SaveFormatException>(() => SaveGame.Restore(SaveCodec.Read(text) with { Version = 0 }, GameContent.Current));
    }
}
