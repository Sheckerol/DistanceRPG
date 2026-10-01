using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The stable (§6.2, Phase 6a.2; PHASE 6a DECISION 7): the weapons the party owns
/// and is not carrying, part of the campaign and in the order they were put there.
/// Weapons move between the bags, the hands and the stable by hand between runs,
/// through the campaign's one door (<see cref="CampaignState.Move"/>) — a swap, or
/// a weapon put where none lies — which never leaves a member carrying no weapon
/// and arms a hand it empties from the bag. It is saved with the campaign, written
/// null when empty, and a save from before it reads as an empty stable. The
/// campaigns are the game's own (<see cref="CampaignState.Start"/>: every member
/// carries two weapons), on the premise's numbers (<see cref="Premise"/>).
/// </summary>
[Collection(TestContent.Collection)]
public sealed class StableTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    private static WeaponPlace Slot(string memberId, int slot) => new(memberId, slot);

    private static WeaponPlace Stable(int index) => WeaponPlace.InTheStable(index);

    private static string Save(CampaignState campaign, ViewDto? view = null) => SaveCodec.Write(SaveGame.Capture(campaign, view));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    [Fact]
    public void AWeaponMovesBetweenBagsHandsAndTheStable()
    {
        // §6.2: the stable holds the weapons the party owns and is not carrying, in
        // the order they were put there, and a new campaign's is empty. A weapon put
        // where none lies leaves its old place empty; put onto a weapon it swaps with
        // it; and equipping is a move into the hand, slot 0 — "re-equipping from the
        // stable".
        var campaign = NewCampaign();
        var (a, b, c) = (campaign.Party[0], campaign.Party[1], campaign.Party[2]);
        var (stiletto, aStaff) = (a.Inventory[0]!, a.Inventory[1]!);
        var (guard, bStaff) = (b.Inventory[0]!, b.Inventory[1]!);
        var axe = c.Inventory[0]!;
        Assert.Empty(campaign.Stable);

        // A's bag staff to the stable's end, then B's: in the order put there.
        Assert.Equal((aStaff, (Weapon?)null, (Weapon?)null), campaign.Move(Slot("A", 1), Stable(0)));
        Assert.Null(a.Inventory[1]);
        campaign.Move(Slot("B", 1), Stable(1));
        Assert.Equal([aStaff, bStaff], campaign.Stable);

        // Equipped from the stable: onto C's hand, the axe going where the staff was.
        Assert.Equal((aStaff, (Weapon?)axe, (Weapon?)null), campaign.Move(Stable(0), Slot("C", 0)));
        Assert.Same(aStaff, c.EquippedWeapon);
        Assert.Equal([axe, bStaff], campaign.Stable);

        // Into another member's bag, where nothing lies: the stable closes up behind it.
        campaign.Move(Stable(1), Slot("A", 3));
        Assert.Same(bStaff, a.Inventory[3]);
        Assert.Equal([axe], campaign.Stable);

        // Hand to hand, between two members: a swap.
        Assert.Equal((stiletto, (Weapon?)guard, (Weapon?)null), campaign.Move(Slot("A", 0), Slot("B", 0)));
        Assert.Equal((guard, stiletto), (a.EquippedWeapon, b.EquippedWeapon));

        // Within the stable: onto another of its weapons, a swap; to its end, a move
        // there, the rest closing up.
        campaign.Move(Slot("C", 1), Stable(1));
        var renewal = campaign.Stable[1];
        campaign.Move(Stable(0), Stable(1));
        Assert.Equal([renewal, axe], campaign.Stable);
        campaign.Move(Stable(0), Stable(2));
        Assert.Equal([axe, renewal], campaign.Stable);

        // Onto its own place: nothing moves.
        var before = campaign.Party.Select(member => member.Inventory.ToArray()).ToArray();
        Assert.Equal((guard, (Weapon?)null, (Weapon?)null), campaign.Move(Slot("A", 0), Slot("A", 0)));
        Assert.Equal((axe, (Weapon?)null, (Weapon?)null), campaign.Move(Stable(0), Stable(0)));
        Assert.Equal(before, campaign.Party.Select(member => member.Inventory.ToArray()));
        Assert.Equal([axe, renewal], campaign.Stable);

        // A place nobody has, or nothing to move, is refused and moves nothing.
        Assert.Throws<ArgumentException>(() => campaign.Move(Slot("Z", 0), Stable(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.Move(Slot("A", a.Inventory.Length), Stable(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.Move(Stable(0), Stable(3)));
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Stable(2), Slot("A", 1)));   // the stable's end holds nothing
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 2), Stable(0)));   // an empty slot
        Assert.Equal(before, campaign.Party.Select(member => member.Inventory.ToArray()));
        Assert.Equal([axe, renewal], campaign.Stable);

        // Nothing is met by a move: every weapon moved was the party's already.
        Assert.Equal(NewCampaign().SeenInOrder, campaign.SeenInOrder);
    }

    [Fact]
    public void AMoveNeverLeavesAMemberUnarmed_AndAnEmptyHandIsArmed()
    {
        // PHASE 6a DECISION 7: no move leaves a member carrying no weapon — only the
        // member a weapon is taken from can be left with none, and only when nothing
        // comes back: the weapon put where none lies, in another member's bag or at
        // the stable's end. The hub asks first (WouldDisarm), and the door itself
        // refuses, moving nothing.
        var campaign = NewCampaign();
        var (a, b) = (campaign.Party[0], campaign.Party[1]);
        var stiletto = a.Inventory[0]!;
        campaign.Move(Slot("A", 1), Stable(0));
        Assert.Equal(1, a.Inventory.Count(carried => carried is not null));

        Assert.Same(a, campaign.WouldDisarm(Slot("A", 0), Stable(1)));
        Assert.Same(a, campaign.WouldDisarm(Slot("A", 0), Slot("B", 2)));
        var refused = Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 0), Stable(1)));
        Assert.Contains("A carrying no weapon", refused.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 0), Slot("B", 2)));
        Assert.Same(stiletto, a.EquippedWeapon);
        Assert.Single(campaign.Stable);
        Assert.Null(b.Inventory[2]);

        // A swap always hands one back; a weapon put down in its own member's bag
        // stays carried; a weapon taken from the stable disarms nobody.
        Assert.Null(campaign.WouldDisarm(Slot("A", 0), Stable(0)));
        Assert.Null(campaign.WouldDisarm(Slot("A", 0), Slot("B", 0)));
        Assert.Null(campaign.WouldDisarm(Slot("A", 0), Slot("A", 4)));
        Assert.Null(campaign.WouldDisarm(Stable(0), Slot("B", 2)));

        // A hand a move empties is armed from the bag, as the rest before a dungeon
        // arms it: the first weapon carried in the bag, moved into the hand. Put
        // down in its own bag, the only weapon comes straight back to the hand — a
        // move that changes nothing, which the hub asks first (ComesStraightBack)
        // and refuses, saying why, so no move, log line or save is made of it (the
        // audit of Phase 6a).
        Assert.True(campaign.ComesStraightBack(Slot("A", 0), Slot("A", 4)));
        Assert.True(campaign.ComesStraightBack(Slot("A", 0), Slot("A", 1)));
        Assert.Equal((stiletto, (Weapon?)null, (Weapon?)stiletto), campaign.Move(Slot("A", 0), Slot("A", 4)));
        Assert.Same(stiletto, a.EquippedWeapon);
        Assert.All(a.Inventory.Skip(1), Assert.Null);

        // B carries two: its hand put down past the staff in its bag is a real
        // move — the hand takes the staff up — and so is one onto the staff, a swap;
        // put down ahead of it, it would come straight back. Nothing but a hand's
        // weapon put where nothing lies in its own bag is asked.
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 2)));
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 1)));
        campaign.Move(Slot("B", 1), Slot("B", 3));
        Assert.True(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 1)));
        Assert.True(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 2)));
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 4)));
        campaign.Move(Slot("B", 3), Slot("B", 1));
        Assert.False(campaign.ComesStraightBack(Slot("B", 1), Slot("B", 2)));
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("C", 2)));
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Stable(1)));
        Assert.False(campaign.ComesStraightBack(Stable(0), Slot("B", 2)));

        // B carries two: its hand to the stable's end is allowed, and its staff is
        // taken up into the emptied hand.
        var (guard, bStaff) = (b.Inventory[0]!, b.Inventory[1]!);
        Assert.Equal((guard, (Weapon?)null, (Weapon?)bStaff), campaign.Move(Slot("B", 0), Stable(1)));
        Assert.Same(bStaff, b.EquippedWeapon);
        Assert.Null(b.Inventory[1]);
        Assert.Same(guard, campaign.Stable[1]);

        // A hand filled again by the move is left as it is: a swap arms nothing.
        Assert.Equal((bStaff, (Weapon?)guard, (Weapon?)null), campaign.Move(Slot("B", 0), Stable(1)));
        Assert.Same(guard, b.EquippedWeapon);

        // The rest before a dungeon arms an empty hand through the same helper: the
        // first weapon in the bag, and a hand holding one is left alone.
        var member = TestPools.Holding("E", null);
        var bow = TestWeapons.Get("hunting_bow");
        var sword = TestWeapons.Get("arming_sword");
        (member.Inventory[3], member.Inventory[5]) = (bow, sword);
        member.Rest();
        Assert.Same(bow, member.EquippedWeapon);
        Assert.Null(member.Inventory[3]);
        Assert.Null(member.ArmFromBag());
        Assert.Same(bow, member.EquippedWeapon);

        // A member left carrying nothing — a wipe can take every spoil it held — is
        // refused nothing given to it, and nothing is taken from it.
        var bare = new CampaignState([TestPools.Holding("A", null), TestPools.Holding("B", TestWeapons.Get("longbow"))]);
        bare.Party[1].Inventory[1] = TestWeapons.Get("darts");
        Assert.Null(bare.WouldDisarm(Slot("B", 1), Slot("A", 0)));
        bare.Move(Slot("B", 1), Slot("A", 0));
        Assert.Equal("darts", bare.Party[0].EquippedWeapon!.Id);
    }

    [Fact]
    public void TheStableIsWrittenOnlyBetweenRuns()
    {
        // The stable and the bags change by hand only between runs: during a visit
        // the door refuses, and nothing moves.
        var campaign = NewCampaign();
        campaign.Move(Slot("A", 1), Stable(0));
        var visit = campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
        var bags = campaign.Party.Select(member => member.Inventory.ToArray()).ToArray();
        var refused = Assert.Throws<InvalidOperationException>(() => campaign.Move(Stable(0), Slot("A", 1)));
        Assert.Contains("between runs", refused.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("B", 1), Stable(1)));
        Assert.Equal(bags, campaign.Party.Select(member => member.Inventory.ToArray()));
        Assert.Single(campaign.Stable);
        Assert.Same(visit, campaign.Visit);

        // Read off the IL: the stable is touched by its view, the lookup, the door
        // and its two helpers, the load, and the one path the enchanter's shop gives
        // a weapon back by (Release, Phase 6a.3a: a take-back's and a finished
        // service's) — nothing else in CampaignState — and the door's one caller is
        // the hub's put-down. A deposit takes a weapon out through the door's helper
        // (Take, from Enqueue). So no entry and no load writes a weapon into it or
        // takes one out behind the hub's back, and a run's end only brings the shop's
        // back (EnchanterTests.WearIsSpentAndReturnedInOnePlace).
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var stable = typeof(CampaignState).GetField("_stable", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string[] Touching(params OpCode[] ops) => typeof(CampaignState).GetNestedTypes(Declared).Prepend(typeof(CampaignState))
            .SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            .Where(method => Body(method) is { } il && InstructionsAt(il).Any(i => ops.Contains(i.Op)
                && method.Module.ResolveField(BitConverter.ToInt32(il, i.At)) is { } field
                && field.Module == stable.Module && field.MetadataToken == stable.MetadataToken))
            .Select(method => method.Name).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Move", "Put", "Release", "Restore", "Take", "WeaponAt", "get_Stable"], Touching(OpCodes.Ldfld, OpCodes.Ldflda));
        Assert.Equal([".ctor"], Touching(OpCodes.Stfld));

        Assert.Equal(["BetweenRunsScene.PutDown"], CallersOf(typeof(CampaignState), nameof(CampaignState.Move)));   // TestIl.CallersOf
        Assert.Equal(["CampaignState.Move"], CallersOf(typeof(CampaignState), "Put"));
        Assert.Equal(["CampaignState.Enqueue", "CampaignState.Move"], CallersOf(typeof(CampaignState), "Take"));

        // And a hand is armed from its bag in the three places that empty one: the
        // rest before a dungeon, the door, and a deposit (Phase 6a.3a).
        Assert.Equal(["CampaignState.Enqueue", "CampaignState.Move", "PartyMemberState.Rest"], CallersOf(typeof(PartyMemberState), nameof(PartyMemberState.ArmFromBag)));
    }

    [Fact]
    public void AMoveNeverLandsOnAPotion_AndThePageNeverOffersOne()
    {
        // A potion lies in a member's bag and is never moved by hand (PHASE 6b
        // DECISION 1): the door refuses a move onto its slot — a slot holds one
        // thing — and a move from it, from the stable or a bag alike, and nothing
        // moves. The stable holds weapons alone.
        var campaign = NewCampaign();
        var a = TestPotions.Carrying(campaign, "A", (2, TestPotions.Health(2)));
        campaign.Move(Slot("B", 1), Stable(0));
        var bags = campaign.Party.Select(member => member.Inventory.ToArray()).ToArray();
        var onto = Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 1), Slot("A", 2)));
        Assert.Contains("PHASE 6b DECISION 1", onto.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Stable(0), Slot("A", 2)));
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 2), Stable(1)));
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 2), Slot("C", 3)));
        Assert.Equal(bags, campaign.Party.Select(member => member.Inventory.ToArray()));
        Assert.Single(campaign.Stable);
        Assert.Equal(((Weapon?)null, (IItem?)TestPotions.Health(2)), (campaign.WeaponAt(Slot("A", 2)), campaign.ItemAt(Slot("A", 2))));

        // The page lists it, grey, under its member — never chosen and never put down
        // on — and the room left in A's bag counts the empty slots alone, from the
        // first of them past the potion.
        var rows = HubWeapons.Rows(campaign);
        var potion = Assert.Single(rows, row => row.Kind == HubWeapons.RowKind.Potion);
        Assert.Equal(("  BAG   HEALTH POTION Q2", Slot("A", 2)), (potion.Text, potion.Place));
        Assert.Equal((false, false), (potion.Choosable, potion.Target));
        Assert.Equal([new DungeonHud.Run("  BAG   HEALTH POTION Q2", DungeonHud.Grey)], HubWeapons.Runs(potion));
        int at = rows.ToList().IndexOf(potion);
        Assert.Equal(["[A]", "  HAND  WEAKSPOT STILETTO", "  BAG   STAFF OF RENEWAL", "  BAG   HEALTH POTION Q2", HubWeapons.BagRoom(3)],
            rows.Skip(at - 3).Take(5).Select(row => row.Kind == HubWeapons.RowKind.Weapon ? $"{row.Label}{HubWeapons.Name(row.Weapon!)}" : row.Text));
        Assert.Equal(Slot("A", 3), rows[at + 1].Place);
        Assert.Same(a, campaign.Party[0]);

        // Every step passes over it: browsing, and carrying a weapon to put down.
        var page = new HubWeapons(campaign);
        for (int step = 0; step < 2 * rows.Count; step++)
        {
            page.Step(+1);
            Assert.NotEqual(HubWeapons.RowKind.Potion, page.ChosenRow.Kind);
        }
        while (page.ChosenRow.Kind != HubWeapons.RowKind.Weapon)
            page.Step(+1);
        Assert.NotNull(page.TakeUp());
        for (int step = 0; step < 2 * rows.Count; step++)
        {
            page.Step(-1);
            Assert.True(page.ChosenRow.Target, page.ChosenRow.Text);
        }
        Assert.NotNull(page.PutBack());
    }

    [Fact]
    public void TheStableRoundTrips()
    {
        // §5.1, §6.7: the stable is saved with the campaign, each weapon whole — its
        // spreads, its entries, its wear — and in its order, and comes back field
        // for field; captured again, the same text.
        var campaign = NewCampaign();
        var def = GameContent.Current.Weapons["arming_sword"];
        var sword = TestWeapons.Get("arming_sword");
        var worn = new Weapon(def, sword.Forged, sword.Modifiers, sword.Enchantments, sword.ForgedEnchantmentCount, wear: 21, wearCashed: 195);
        campaign.Party[2].Inventory[4] = worn;
        campaign.Move(Slot("C", 4), Stable(0));
        campaign.Move(Slot("D", 1), Stable(1));
        campaign.Move(Slot("A", 1), Stable(0));   // onto the sword: a swap, the sword to A's bag, A's staff first
        campaign.Move(Slot("A", 1), Stable(2));
        Assert.Equal(["staff_of_renewal", "staff_of_mire", "arming_sword"], campaign.Stable.Select(weapon => weapon.Id));

        string text = Save(campaign);
        var loaded = Load(text);
        Assert.False(loaded.TuningChanged);
        StateAssert.Equivalent(campaign, loaded.Campaign, "campaign");
        Assert.Equal(text, Save(loaded.Campaign));
        var back = loaded.Campaign.Stable[2];
        Assert.NotSame(worn, back);
        Assert.Equal((21, 195, 36), (back.Wear, back.WearCashed, back.WearCapacity));
        var rows = JsonNode.Parse(text)!["campaign"]!["stable"]!.AsArray();
        Assert.Equal(["staff_of_renewal", "staff_of_mire", "arming_sword"], rows.Select(row => row!["id"]!.GetValue<string>()));

        // An empty stable is written null, as no visit is between runs; null and []
        // read alike, as empty, and captured again it is null.
        string empty = Save(NewCampaign());
        var emptyTree = JsonNode.Parse(empty)!.AsObject();
        Assert.True(emptyTree["campaign"]!.AsObject().ContainsKey("stable"));
        Assert.Null(emptyTree["campaign"]!["stable"]);
        emptyTree["campaign"]!["stable"] = new JsonArray();
        var fromBrackets = Load(emptyTree.ToJsonString());
        Assert.Empty(fromBrackets.Campaign.Stable);
        Assert.Equal(empty, Save(fromBrackets.Campaign));

        // Each row is read as every weapon is, refused at its own path; a null row
        // is no weapon.
        (Action<JsonArray> Edit, string Where)[] refusals =
        [
            (stable => stable.Insert(1, null), "campaign.stable[1]"),
            (stable => stable[0]!["id"] = "no_such_weapon", "campaign.stable[0].id"),
            (stable => stable[2]!["wear"] = 999_999, "campaign.stable[2].wear"),
            (stable => stable[1]!["wearCashed"] = -1, "campaign.stable[1].wearCashed"),
        ];
        foreach (var (edit, where) in refusals)
        {
            var tree = JsonNode.Parse(text)!.AsObject();
            edit(tree["campaign"]!["stable"]!.AsArray());
            Assert.Equal(where, Assert.Throws<SaveFormatException>(() => Load(tree.ToJsonString())).Where);
        }

        // And the door a load comes back through refuses what no stable holds: a
        // null, or one weapon in two places.
        var party = NewCampaign().Party;
        var bow = TestWeapons.Get("hunting_bow");
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [bow, null!]));
        Assert.Throws<ArgumentException>(() => CampaignState.Restore(party, [], 0, [], [], [], null, null, [bow, bow]));
    }

    [Fact]
    public void TheStableIsEmptyOnANewCampaign_AndOnAVersion1Save()
    {
        // A new campaign has nothing in the stable, and a save from before it — a
        // version-1 save, lifted, or an earlier Phase 6a step's version-2 one, with
        // no stable member — reads as a campaign with an empty stable: the member is
        // appended with its default (SaveFile.cs' header rule), and the migration
        // writes none.
        Assert.Empty(NewCampaign().Stable);

        string v1 = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", "pre-rework-between-runs.json"));
        Assert.False(JsonNode.Parse(v1)!["campaign"]!.AsObject().ContainsKey("stable"));
        Assert.False(SaveCodec.LiftToVersion2(JsonNode.Parse(v1)!.AsObject())["campaign"]!.AsObject().ContainsKey("stable"));
        var file = SaveCodec.Read(v1);
        Assert.Null(file.Campaign.Stable);
        var restored = SaveGame.Restore(file, GameContent.Current).Campaign;
        Assert.Empty(restored.Stable);
        Assert.Equal(6, restored.Party[1].Inventory.Count(carried => carried is not null));   // B's bag as it was: nothing moved to the stable on the way

        var earlier = JsonNode.Parse(Save(NewCampaign()))!.AsObject();
        Assert.True(earlier["campaign"]!.AsObject().Remove("stable"));
        Assert.Empty(Load(earlier.ToJsonString()).Campaign.Stable);

        // A version-1 save a hand gave a stable (the 6a.2 review): the version-1
        // reader ignored the member, so to version 1 there was none, and the step
        // removes it rather than read weapons the campaign never owned — or refuse,
        // at the row's missing wear, a save version 1 loaded. Whatever kind of thing
        // it is.
        var bRow = JsonNode.Parse(v1)!["campaign"]!["party"]![1]!["inventory"]![2]!;
        foreach (JsonNode stray in new JsonNode[] { new JsonArray(bRow.DeepClone()), JsonValue.Create(4) })
        {
            var grafted = JsonNode.Parse(v1)!.AsObject();
            grafted["campaign"]!["stable"] = stray;
            var lifted = SaveGame.Restore(SaveCodec.Read(grafted.ToJsonString()), GameContent.Current).Campaign;
            Assert.Empty(lifted.Stable);
            Assert.Equal(6, lifted.Party[1].Inventory.Count(carried => carried is not null));
        }

        // And the enchanter's shop, the same way (Phase 6a.3a): a version-1 save a hand
        // gave a queue — a deposit of B's Riposte Blade, a count, a run's report on it —
        // reads as a campaign with nothing in the shop and no deposit ever made, and
        // its last run as one with nothing at the enchanter.
        var deposit = new JsonObject
        {
            ["ordinal"] = 0, ["service"] = "Refine", ["weapon"] = bRow.DeepClone(), ["wearAtDeposit"] = 0, ["runs"] = 1, ["runsWorked"] = 0,
        };
        foreach (var (queue, count, reports) in new (JsonNode Queue, JsonNode Count, JsonNode Reports)[]
                 {
                     (new JsonArray(deposit), JsonValue.Create(1), new JsonArray(JsonNode.Parse("""{ "weaponId": "riposte_blade", "service": "Refine", "runsWorked": 1, "runs": 1 }"""))),
                     (JsonValue.Create("a shop"), JsonValue.Create("one"), JsonValue.Create(4)),
                 })
        {
            var grafted = JsonNode.Parse(v1)!.AsObject();
            grafted["campaign"]!["enchanter"] = queue;
            grafted["campaign"]!["depositsMade"] = count;
            grafted["campaign"]!["lastResult"]!["services"] = reports;
            var lifted = SaveGame.Restore(SaveCodec.Read(grafted.ToJsonString()), GameContent.Current).Campaign;
            Assert.Equal((0, 0), (lifted.AtTheEnchanter.Count, lifted.DepositsMade));
            Assert.Null(lifted.LastResult!.Services);
            Assert.Equal(6, lifted.Party[1].Inventory.Count(carried => carried is not null));
        }
        Assert.Empty(NewCampaign().AtTheEnchanter);
        Assert.Equal(0, NewCampaign().DepositsMade);
    }

    [Fact]
    public void AMidRunSaveWithAStableRestores()
    {
        // A stable filled between runs is carried through the run, and a save made
        // mid-run holds it: the load fills the stable itself, never through the hub's
        // door, so it comes back whole with the visit held — and the door stays shut
        // until the run ends.
        var campaign = NewCampaign();
        campaign.Move(Slot("A", 1), Stable(0));
        campaign.Move(Slot("D", 1), Stable(1));
        var visit = campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);

        string text = Save(campaign);
        var loaded = Load(text).Campaign;
        Assert.NotNull(loaded.Visit);
        Assert.Equal(["staff_of_renewal", "staff_of_mire"], loaded.Stable.Select(weapon => weapon.Id));
        Assert.Equal(text, Save(loaded));
        Assert.Throws<InvalidOperationException>(() => loaded.Move(Stable(0), Slot("A", 1)));

        // The run's end leaves the stable as it was, and the hub's door opens again.
        loaded.Visit!.OpenTurns(loaded.Party, CampaignRuns.Ten);
        Assert.Equal(RunOutcome.Retreat, CampaignRuns.Exit(loaded).Outcome);
        Assert.Equal(["staff_of_renewal", "staff_of_mire"], loaded.Stable.Select(weapon => weapon.Id));
        loaded.Move(Stable(0), Slot("A", 1));
        Assert.Equal("staff_of_renewal", loaded.Party[0].WeaponIn(1)!.Id);
    }
}
