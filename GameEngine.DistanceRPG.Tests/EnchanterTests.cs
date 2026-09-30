using System.Reflection;
using System.Reflection.Emit;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The enchanter's queue and its clock (§6.2–§6.4, §6.7; Phase 6a.3a), under the
/// user's deposit ruling of 2026-09-25 (R4). A deposit, between runs, cashes the
/// weapon's whole pool — its capacity growing on the cash alike whatever the
/// service — and queues it for its count plus one runs; slot n opens at 30 + 5(n −
/// 1), and attaching enchantment #n needs a free slot and a deposit of 30 + 5(n −
/// 1). Every run's end ticks the clock: a run that counts works the two newest
/// deposits a run on, and a finished Enchant attaches its enchantment last at tier
/// 1; a run that does not count works nothing. A take-back reverses the cash
/// exactly. The ruling's own climb is pinned against the master
/// (<see cref="TheRulingsClimb_WearDepositsAndRuns"/>, listed in
/// <c>ShippedDataTests</c>' doc); every other test states the ruling's numbers as
/// its own input over the numbers the runs were written against
/// (<see cref="Premise"/>), so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class EnchanterTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise(tuning: TestShop.Ruled);

    public void Dispose() => _premise.Dispose();

    private static Tuning Ruled(Tuning t) => TestShop.Ruled(t);

    private static CampaignState Opened() => TestShop.Opened();

    private static Weapon Worn(string id, int wear, int cashed = 0) => TestShop.Worn(id, wear, cashed);

    private static WeaponPlace Give(CampaignState campaign, string memberId, int slot, Weapon weapon) => TestShop.Give(campaign, memberId, slot, weapon);

    private static RunResult Counted(CampaignState campaign) => TestShop.Counted(campaign);

    private static RunResult NotCounted(CampaignState campaign) => TestShop.NotCounted(campaign);

    private static Deposit Queued(CampaignState campaign, Weapon weapon) => campaign.AtTheEnchanter.Single(deposit => ReferenceEquals(deposit.Weapon, weapon));

    // ── The ruling's numbers ─────────────────────────────────────────────────

    /// <summary>
    /// A weapon's climb to its last slot, cashing a full bar at every deposit (R4's
    /// worked example): as each slot opens it attaches — the slot open at the
    /// capacity it is deposited at and the price met — and otherwise it refines;
    /// or, <paramref name="leastFirst"/>, it refines until its capacity is the last
    /// slot's less the attaches still to make, then attaches on every deposit (R4's
    /// consequence 3: "the order that takes the least"). The arithmetic of
    /// <see cref="Wear"/>, and at every deposit the enchanter's own gate asked of
    /// the weapon as it stands (<see cref="Enchanter.Refusal"/>, in
    /// <paramref name="asked"/>'s B's bag): it takes an Enchant exactly when the
    /// arithmetic says the slot is open and the price met, and otherwise refuses
    /// with the gate the arithmetic names — the slot first, read at the capacity
    /// before the cash, then the price. On a full bar under R4's numbers the two
    /// fall together, so which one refuses is what pins the order.
    /// </summary>
    private static (int Deposits, int Runs, IReadOnlyList<int> AttachedAt) Climb(int arriving, bool leastFirst, CampaignState asked)
    {
        int slots = GameContent.Current.Tuning.EnchantmentSlots;
        int cashed = 0, count = arriving, deposits = 0, runs = 0;
        var attachedAt = new List<int>();
        var made = TestWeapons.Get("tower_guard");
        var soul = GameContent.Current.Enchantments["vampiric"];
        while (count < slots)
        {
            int capacity = Wear.Capacity(cashed).Max;
            int wear = capacity;   // a full bar
            int n = count + 1;
            long opensAt = Wear.SlotOpensAt(n), price = Wear.Price(n);
            string? gate = opensAt > capacity ? Enchanter.NoFreeSlot(n, opensAt, capacity) : wear < price ? Enchanter.Short(n, price, wear) : null;
            bool can = gate is null;
            var weapon = new Weapon(GameContent.Current.Weapons["tower_guard"], made.Forged, made.Modifiers,
                Enumerable.Range(0, count).Select(_ => new Enchantment(soul, 1)).ToArray(), 0, wear, cashed);
            Give(asked, "B", 2, weapon);
            Assert.Equal(gate, Enchanter.Refusal(asked, weapon, Service.Enchant, soul.Id));   // the slot first, at the capacity before the cash; then the price
            bool attach = can && (!leastFirst || capacity >= Wear.SlotOpensAt(slots) - (slots - n));
            runs += Wear.ServiceRuns(count);
            cashed += wear;
            deposits++;
            if (!attach)
                continue;
            count++;
            attachedAt.Add(cashed);
        }
        return (deposits, runs, attachedAt);
    }

    [Fact]
    public void TheRulingsClimb_WearDepositsAndRuns()
    {
        // R4 (settled.md, S:1653–1732), the user's own numbers, pinned as the
        // ruling states them against the master (REWORK DECISION 3; ShippedDataTests
        // lists the pin): an edit to a slot, a price or a start fails here by name.
        // Every climb asks the enchanter's own gate at each deposit, so an edit to
        // its order or to the capacity it reads moves these numbers too.
        using (TestContent.Use())
        {
            var asked = Opened();
            var t = GameContent.Current.Tuning;
            Assert.Equal((30, 1, 30, 5, 5, 30, 5), (t.StartingPool, t.WearCapacityPerBar, t.FirstSlotCapacity, t.SlotCapacityStep, t.EnchantmentSlots,
                t.FirstEnchantmentWear, t.EnchantmentWearStep));
            Assert.Equal(2, t.EnchanterWorksOn);   // "the two most recently deposited" (S:97–98)

            // Slot n opens at 30 + 5(n − 1), and attaching #n needs a deposit of the same.
            Assert.Equal([30L, 35, 40, 45, 50], Enumerable.Range(1, 5).Select(Wear.SlotOpensAt));
            Assert.Equal([30L, 35, 40, 45, 50], Enumerable.Range(1, 5).Select(Wear.Price));
            Assert.Equal([0, 1, 1, 2, 5, 5], new[] { 29, 30, 34, 35, 50, 1000 }.Select(Wear.SlotsOpen));

            // Service time is the count plus one: 1 to 5, cumulatively 1, 3, 6, 10, 15 (§6.2).
            var times = Enumerable.Range(0, 5).Select(Wear.ServiceRuns).ToArray();
            Assert.Equal([1, 2, 3, 4, 5], times);
            Assert.Equal([1, 3, 6, 10, 15], times.Select((_, i) => times.Take(i + 1).Sum()));

            // Worked: "#1 at 30 hits, capacity 30 → 31; bars of 31, 32, 33 and 34 take
            // capacity to 35, at 160 hits in all; and #2 at 195 … #3 at 385, #4 at 600
            // and #5 at 840 — twenty-one deposits from a fresh weapon, five of them
            // attaching" — and 71 runs of downtime (consequence 3).
            var climb = Climb(arriving: 0, leastFirst: false, asked);
            Assert.Equal([30, 195, 385, 600, 840], climb.AttachedAt);
            Assert.Equal((21, 71), (climb.Deposits, climb.Runs));
            Assert.Equal(35, Wear.Capacity(160).Max);

            // And no top: past the fifth slot's 50 the capacity goes on rising.
            Assert.Equal(51, Wear.Capacity(840).Max);
            Assert.True(Wear.Capacity(1335).Max > Wear.Capacity(840).Max);

            // "Refining first and attaching all five on the last five deposits, at
            // capacities 46 to 50, takes the least there is: 31 runs."
            var least = Climb(arriving: 0, leastFirst: true, asked);
            Assert.Equal(31, least.Runs);
            Assert.Equal(21, least.Deposits);

            // Consequence 5: what a weapon arrives with is a head start in souls, never
            // on the climb — an innate staff 72 and 48, a unique arriving with two souls
            // 78 and 66, one arriving with three 89 and 85, against a fresh 71 and 31.
            Assert.Equal([(72, 48), (78, 66), (89, 85)],
                new[] { 1, 2, 3 }.Select(arriving => (Climb(arriving, leastFirst: false, asked).Runs, Climb(arriving, leastFirst: true, asked).Runs)));
        }
    }

    // ── The gates ────────────────────────────────────────────────────────────

    [Fact]
    public void ABreathingRoomDepositAttachesTheFirst()
    {
        // R4: "capacity above the price is breathing room: at capacity 35 with no
        // enchantment, cashing 30 buys the first". A deposit still spends the whole
        // pool, so a Tower Guard at capacity 35 holding 30 cashes all 30.
        var campaign = Opened();
        var guard = Worn("tower_guard", wear: 30, cashed: 160);
        Assert.Equal((35, 0), (guard.WearCapacity, guard.Enchantments.Count));
        var at = Give(campaign, "B", 2, guard);

        Assert.Null(Enchanter.Refusal(campaign, guard, Service.Enchant, "vampiric"));
        var made = Enchanter.Deposit(campaign, at, Service.Enchant, "vampiric");
        Assert.Equal((30, 1, "vampiric"), (made.Entry.WearAtDeposit, made.Entry.Runs, made.Entry.EnchantmentId));
        Assert.Equal((35, 0, 190), (made.Before.Max, guard.Wear, guard.WearCashed));

        Counted(campaign);
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Same(guard, campaign.Stable[^1]);
        Assert.Equal(["vampiric"], guard.Enchantments.Select(entry => entry.Id));
    }

    [Fact]
    public void TheFreeSlotIsReadAtTheCapacityBeforeTheDeposit()
    {
        // The slot enchantment #n needs is asked of the capacity the weapon is
        // deposited at, before this deposit's cash. A Staff of Renewal — one soul —
        // at capacity 34 and full would lift its capacity to 35 by cashing, but slot
        // 2 is not open as it goes in: refused. Refined instead, it comes back at 35,
        // and the next full deposit buys its second.
        var campaign = Opened();
        var staff = Worn("staff_of_renewal", wear: 34, cashed: 126);
        Assert.Equal((34, 1), (staff.WearCapacity, staff.Enchantments.Count));
        var at = Give(campaign, "B", 2, staff);

        Assert.Equal("NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 34", Enchanter.Refusal(campaign, staff, Service.Enchant, "vampiric"));
        Assert.Equal(Enchanter.NoFreeSlot(2, 35, 34), Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, at, Service.Enchant, "vampiric")).Message.Split(": ")[^1]);
        Assert.Equal((34, 126), (staff.Wear, staff.WearCashed));   // refused: nothing cashed, nothing moved
        Assert.Same(staff, campaign.Party[1].Inventory[2]);

        Enchanter.Deposit(campaign, at, Service.Refine);
        Assert.Equal(35, staff.WearCapacity);   // the cash grows it at once
        Counted(campaign);
        Counted(campaign);
        Assert.Same(staff, campaign.Stable[^1]);

        for (int hit = 0; hit < 35; hit++)
            Wear.Accrue(staff);
        Assert.True(staff.WornFull);
        Assert.Null(Enchanter.Refusal(campaign, staff, Service.Enchant, "vampiric"));
    }

    [Fact]
    public void AFreeSlotIsReadLiterally_ArrivingSoulsFillFromTheFirst()
    {
        // R4's consequence 4: what a weapon arrives with fills its slots from the
        // first. A fresh staff's innate holds slot 1, so its second waits for slot 2,
        // at 35; a unique arriving with three souls waits for slot 4, at 45 — though
        // each carries the price.
        var campaign = Opened();
        var fresh = Worn("staff_of_renewal", wear: 30);
        Give(campaign, "B", 2, fresh);
        Assert.Equal("NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 30", Enchanter.Refusal(campaign, fresh, Service.Enchant, "vampiric"));
        var grown = Worn("staff_of_renewal", wear: 35, cashed: 160);
        Give(campaign, "B", 3, grown);
        Assert.Null(Enchanter.Refusal(campaign, grown, Service.Enchant, "vampiric"));

        var knife = Worn("flensing_knife_unique", wear: 40, cashed: 345);
        Assert.Equal((3, 40), (knife.Enchantments.Count, knife.WearCapacity));
        Give(campaign, "C", 2, knife);
        Assert.Equal("NO FREE SLOT - SLOT 4 OPENS AT CAPACITY 45 - IT IS 40", Enchanter.Refusal(campaign, knife, Service.Enchant, "flaming"));
        var ready = Worn("flensing_knife_unique", wear: 45, cashed: 555);
        Assert.Equal(45, ready.WearCapacity);
        Give(campaign, "C", 3, ready);
        Assert.Null(Enchanter.Refusal(campaign, ready, Service.Enchant, "flaming"));

        // The price is its own gate: an open slot and too little wear is refused on
        // the price. Tuned apart from the slots, the two conditions part (consequence 4).
        var short20 = Worn("staff_of_renewal", wear: 20, cashed: 160);
        Give(campaign, "D", 2, short20);
        Assert.Equal("ENCHANTMENT 2 NEEDS A DEPOSIT OF 35 WEAR - IT CARRIES 20", Enchanter.Refusal(campaign, short20, Service.Enchant, "vampiric"));
        using (TestContent.UsePremise(tuning: t => Ruled(t) with { SlotCapacityStep = 0 }))
            Assert.Equal("ENCHANTMENT 2 NEEDS A DEPOSIT OF 35 WEAR - IT CARRIES 30", Enchanter.Refusal(campaign, fresh, Service.Enchant, "vampiric"));
        using (TestContent.UsePremise(tuning: t => Ruled(t) with { EnchantmentWearStep = 0 }))
            Assert.Equal("NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 30", Enchanter.Refusal(campaign, fresh, Service.Enchant, "vampiric"));
    }

    [Fact]
    public void EveryDepositSpendsTheWholePool_AndGrowsCapacityAlike()
    {
        // §6.1 and R4: a deposit empties the pool whatever it buys — never part of it,
        // never carried over — and the capacity grows on what it cashed, a point a full
        // bar, the same for an Enchant and a Refine (R4's consequence 1; the per-service
        // growth is superseded). Two Tower Guards alike, one enchanted and one refined,
        // come out of the deposit alike.
        var campaign = Opened();
        var (enchanted, refined) = (Worn("tower_guard", wear: 30), Worn("tower_guard", wear: 30));
        var enchant = Enchanter.Deposit(campaign, Give(campaign, "B", 2, enchanted), Service.Enchant, "vampiric");
        var refine = Enchanter.Deposit(campaign, Give(campaign, "B", 3, refined), Service.Refine);
        foreach (var (made, weapon) in new[] { (enchant, enchanted), (refine, refined) })
        {
            Assert.Equal((0, 30, 31), (weapon.Wear, weapon.WearCashed, weapon.WearCapacity));
            Assert.Equal((Wear.Capacity(0), Wear.Capacity(30)), (made.Before, made.After));
            Assert.Equal(30, made.Entry.WearAtDeposit);
        }

        // Capacity above the price is spent too: the whole pool, not the price.
        var roomy = Worn("tower_guard", wear: 35, cashed: 160);
        var spent = Enchanter.Deposit(campaign, Give(campaign, "C", 2, roomy), Service.Enchant, "flaming");
        Assert.Equal((35, 0, 195, 36), (spent.Entry.WearAtDeposit, roomy.Wear, roomy.WearCashed, roomy.WearCapacity));

        // A partial bar counts toward the next point, and carries.
        var partial = Worn("tower_guard", wear: 20, cashed: 30);
        Enchanter.Deposit(campaign, Give(campaign, "C", 3, partial), Service.Refine);
        Assert.Equal(new PoolProgress(1, 31, 20, 31), partial.WearPool);
    }

    [Fact]
    public void AnUnwornRefineIsAccepted_AndComesBackAsItWent()
    {
        // "A fresh drop refines for nothing. No wear, no roll" (P6:501–503; settled:
        // "there is no 0% to explain"): a Refine of a weapon carrying no wear is
        // accepted, costs its count plus one runs, grows nothing, and comes back as it
        // went — never refused.
        var campaign = Opened();
        var staff = campaign.Party[0].Inventory[1]!;
        Assert.Equal((0, 0, 1), (staff.Wear, staff.WearCashed, staff.Enchantments.Count));
        var enchantments = staff.Enchantments.ToArray();

        Assert.Null(Enchanter.Refusal(campaign, staff, Service.Refine));
        var made = Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine);
        Assert.Equal((0, 2), (made.Entry.WearAtDeposit, made.Entry.Runs));
        Assert.Equal(made.Before, made.After);

        Counted(campaign);
        var back = Counted(campaign);
        Assert.True(Assert.Single(back.Services!).Finished);
        Assert.Same(staff, campaign.Stable[^1]);
        Assert.Equal(enchantments, staff.Enchantments);
        Assert.Equal((0, 0, Wear.Capacity(0)), (staff.Wear, staff.WearCashed, staff.WearPool));
    }

    [Fact]
    public void ServiceTakesCountPlusOneRuns_RefiningIncluded()
    {
        // §6.2: "service costs runs — the current enchantment count + 1", for every
        // deposit, a refinement too, and the count includes what the weapon arrived
        // with. Fixed at deposit.
        var campaign = Opened();
        var (bare, staff, knife) = (Worn("tower_guard", 0), Worn("staff_of_renewal", 0), Worn("flensing_knife_unique", 0));
        Assert.Equal(1, Enchanter.Deposit(campaign, Give(campaign, "B", 2, bare), Service.Refine).Entry.Runs);
        Assert.Equal(2, Enchanter.Deposit(campaign, Give(campaign, "B", 3, staff), Service.Refine).Entry.Runs);
        Assert.Equal(4, Enchanter.Deposit(campaign, Give(campaign, "B", 4, knife), Service.Refine).Entry.Runs);
        var enchanting = Worn("tower_guard", wear: 30);
        Assert.Equal(1, Enchanter.Deposit(campaign, Give(campaign, "C", 2, enchanting), Service.Enchant, "cold").Entry.Runs);

        // Refused below nothing: a count is never negative.
        Assert.Throws<ArgumentOutOfRangeException>(() => Wear.ServiceRuns(-1));
    }

    // ── The clock ────────────────────────────────────────────────────────────

    [Fact]
    public void OnlyTheNewestAreWorked_AThirdStallsTheOldest()
    {
        // §6.2: "the enchanter only progresses the two most recently deposited
        // weapons: depositing a third stalls the oldest". Three staves refined, two
        // runs each: the first counted run works the second and third, the first
        // stalls; the next brings those two back, and only then is the first worked.
        var campaign = Opened();
        var staves = new[] { ("A", 1), ("B", 1), ("C", 1) }.Select(place => campaign.Party.Single(m => m.Id == place.Item1).Inventory[place.Item2]!).ToArray();
        foreach (var (member, slot) in new[] { ("A", 1), ("B", 1), ("C", 1) })
            Enchanter.Deposit(campaign, new WeaponPlace(member, slot), Service.Refine);
        Assert.Equal([0, 1, 2], campaign.AtTheEnchanter.Select(deposit => deposit.Ordinal));
        Assert.Equal([1, 2], Enchanter.Worked(campaign.AtTheEnchanter).Select(deposit => deposit.Ordinal));

        var first = Counted(campaign).Services!;
        Assert.Equal([(true, 0), (false, 1), (false, 1)], first.Select(report => (report.Stalled, report.RunsWorked)));
        Assert.Equal(staves.Select(staff => staff.Id), first.Select(report => report.WeaponId));
        Assert.Equal([0, 1, 1], campaign.AtTheEnchanter.Select(deposit => deposit.RunsWorked));

        var second = Counted(campaign).Services!;
        Assert.Equal([(true, 0, false), (false, 2, true), (false, 2, true)], second.Select(report => (report.Stalled, report.RunsWorked, report.Finished)));
        Assert.Same(staves[0], Assert.Single(campaign.AtTheEnchanter).Weapon);
        Assert.Equal(staves[1..], campaign.Stable.TakeLast(2));   // back in deposit order

        var third = Assert.Single(Counted(campaign).Services!);
        Assert.Equal((false, 1), (third.Stalled, third.RunsWorked));

        // With the enchanter working on one at a time, a second deposit stalls the first.
        using (TestContent.UsePremise(tuning: t => Ruled(t) with { EnchanterWorksOn = 1 }))
        {
            Enchanter.Deposit(campaign, new WeaponPlace("D", 1), Service.Refine);
            Assert.Equal([3], Enchanter.Worked(campaign.AtTheEnchanter).Select(deposit => deposit.Ordinal));
            Assert.Equal([(true, 1), (false, 1)], Counted(campaign).Services!.Select(report => (report.Stalled, report.RunsWorked)));
        }
    }

    [Fact]
    public void OnlyACountedRunTicks_EnteringAndFailingShallowTickNothing()
    {
        // §6.3: entering ticks nothing, a failure shallower than the mercy floor
        // counts nothing, and only a run that counts moves the service clock. A run
        // that does not count reports every deposit stalled with its runs as they
        // were — a fresh one at 0 — and moves nothing.
        var campaign = Opened();
        var staff = campaign.Party[0].Inventory[1]!;
        var guard = Worn("tower_guard", wear: 30);
        Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine);
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, guard), Service.Enchant, "vampiric");

        var visit = campaign.Enter("long_gallery", CampaignRuns.FiveDeep);
        Assert.Equal([0, 0], campaign.AtTheEnchanter.Select(deposit => deposit.RunsWorked));
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var shallow = CampaignRuns.Exit(campaign);
        Assert.False(shallow.ServiceTick);
        Assert.Equal([(staff.Id, true, 0, 2), (guard.Id, true, 0, 1)],
            shallow.Services!.Select(report => (report.WeaponId, report.Stalled, report.RunsWorked, report.Runs)));
        Assert.Equal(2, campaign.AtTheEnchanter.Count);
        Assert.Empty(guard.Enchantments);

        // A wipe shallow is a failure that counts nothing too.
        var wiping = campaign.Enter("long_gallery", CampaignRuns.FiveDeep);
        var wiped = CampaignRuns.Wipe(campaign, wiping.OpenTurns(campaign.Party, CampaignRuns.Ten));
        Assert.False(wiped.ServiceTick);
        Assert.All(wiped.Services!, report => Assert.Equal((true, 0), (report.Stalled, report.RunsWorked)));

        // And a run that counts works both: the guard's one run brings it back.
        var counted = Counted(campaign);
        Assert.Equal([(false, 1, false), (false, 1, true)], counted.Services!.Select(report => (report.Stalled, report.RunsWorked, report.Finished)));
        Assert.Equal(["vampiric"], guard.Enchantments.Select(entry => entry.Id));

        // With nothing in the shop, a run's end reports nothing: the result's services are null.
        Counted(campaign);
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Null(NotCounted(campaign).Services);
    }

    [Fact]
    public void AFinishedEnchantAttachesLast_AtTier1_AndReturnsToTheStable()
    {
        // §6.2: the enchanter sells breadth — the chosen enchantment is attached at
        // tier 1 with no XP, never a tier — and it goes last, never sorted in
        // (§3.3). It is acquired, not forged. The weapon comes back to the end of the
        // stable (PHASE 6a DECISION 7). The service finishes inside the run's end,
        // the visit already gone: through the campaign's own path, while the hub's
        // doors stay shut for the whole visit.
        var campaign = Opened();
        var staff = Worn("staff_of_renewal", wear: 35, cashed: 160);
        var innate = staff.Enchantments[0];
        Enchanter.Deposit(campaign, Give(campaign, "D", 2, staff), Service.Enchant, "vampiric");
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));

        var visit = campaign.Enter("long_gallery", CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var shop = Queued(campaign, staff);
        Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, new WeaponPlace("B", 1), Service.Refine));
        Assert.Throws<InvalidOperationException>(() => Enchanter.TakeBack(campaign, shop));
        Assert.Throws<InvalidOperationException>(() => campaign.Move(WeaponPlace.InTheStable(0), new WeaponPlace("A", 1)));
        var bStaff = campaign.Party[1].Inventory[1]!;   // the hub's door itself, handed a deposit it would take between runs
        Assert.Throws<InvalidOperationException>(() => campaign.Enqueue(new WeaponPlace("B", 1), new Deposit(campaign.DepositsMade, bStaff, Service.Refine, 0, 2)));
        Assert.Same(bStaff, campaign.Party[1].Inventory[1]);
        Assert.Equal((1, 1), (campaign.AtTheEnchanter.Count, campaign.DepositsMade));
        CampaignRuns.Exit(campaign);   // shallow: nothing worked

        Weapon? stabled = null;
        void Heard(RunResult result) => stabled = campaign.Visit is null ? campaign.Stable.LastOrDefault() : null;
        campaign.RunEnded += Heard;
        Counted(campaign);
        Counted(campaign);
        campaign.RunEnded -= Heard;

        Assert.Same(staff, stabled);
        Assert.Same(staff, campaign.Stable[^1]);
        Assert.Equal(2, staff.Enchantments.Count);
        Assert.Same(innate, staff.Enchantments[0]);   // what it had stays where it was
        var attached = staff.Enchantments[^1];
        Assert.Equal(("vampiric", Enchanter.AttachedTier, 0), (attached.Id, attached.Tier, attached.Xp));
        Assert.Same(GameContent.Current.Enchantments["vampiric"], attached.Def);
        Assert.False(staff.IsForged(1));
        Assert.Equal(1, staff.ForgedEnchantmentCount);
    }

    [Fact]
    public void AMidRunSaveOneRunFromDoneCompletesOnTheBossKill()
    {
        // A deposit one run from done, carried through a visit a save was made in:
        // the load brings the shop back before it holds the visit, and the boss kill
        // that ends the loaded run ticks it home, as the run would have without the
        // quit.
        var campaign = Opened();
        var staff = Worn("staff_of_renewal", wear: 35, cashed: 160);
        Enchanter.Deposit(campaign, Give(campaign, "D", 2, staff), Service.Enchant, "cold");
        Counted(campaign);
        Assert.Equal((1, 2), (Queued(campaign, staff).RunsWorked, Queued(campaign, staff).Runs));

        var visit = campaign.Enter("spear_hall", CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        string text = SaveCodec.Write(SaveGame.Capture(campaign));
        var loaded = SaveGame.Restore(SaveCodec.Read(text), GameContent.Current).Campaign;
        Assert.NotNull(loaded.Visit);
        var queued = Assert.Single(loaded.AtTheEnchanter);
        Assert.Equal((0, 1, 2, "cold"), (queued.Ordinal, queued.RunsWorked, queued.Runs, queued.EnchantmentId));

        var lair = loaded.Visit!;
        lair.OpenTurns(loaded.Party, CampaignRuns.Ten);
        while (lair.Current.DownStairs is not null)
            CampaignRuns.Down(lair, loaded.Party);
        lair.RecordBossDefeated();
        var kill = CampaignRuns.Exit(loaded);
        Assert.Equal(RunOutcome.BossKill, kill.Outcome);
        Assert.True(Assert.Single(kill.Services!).Finished);
        Assert.Empty(loaded.AtTheEnchanter);
        var back = loaded.Stable[^1];
        Assert.Equal(("staff_of_renewal", "cold"), (back.Id, back.Enchantments[^1].Id));
    }

    [Fact]
    public void RefiningNeverRaisesTheNextPrice()
    {
        // §6.4: refinement costs its count plus one runs and never changes the next
        // enchantment's price — the price is n's alone — while the capacity it grows
        // opens the slots all the same.
        var campaign = Opened();
        var guard = Worn("tower_guard", wear: 0);
        var at = Give(campaign, "B", 2, guard);
        for (int refine = 0; refine < 6; refine++)
        {
            int wear = guard.WearCapacity;
            for (int hit = 0; hit < wear; hit++)
                Wear.Accrue(guard);
            Enchanter.Deposit(campaign, at, Service.Refine);
            Counted(campaign);
            Assert.Empty(guard.Enchantments);
            campaign.Move(WeaponPlace.InTheStable(campaign.Stable.Count - 1), at);
        }
        Assert.Equal(36, guard.WearCapacity);   // 30 to 35 cashed: six points
        Assert.Equal(2, Wear.SlotsOpen(guard.WearCapacity));
        Assert.Equal("ENCHANTMENT 1 NEEDS A DEPOSIT OF 30 WEAR - IT CARRIES 0", Enchanter.Refusal(campaign, guard, Service.Enchant, "cold"));
    }

    [Fact]
    public void AFullSetLeavesRefiningOnly()
    {
        // §6.2: at a full set of enchantments — EnchantmentSlots, the fifth slot the
        // last — Enchant is refused and Refine is the only service left.
        var campaign = Opened();
        var made = TestWeapons.Get("tower_guard");
        var souls = new[] { "vampiric", "flaming", "cold", "vampiric", "cold" }.Select(id => new Enchantment(GameContent.Current.Enchantments[id], 1)).ToArray();
        var full = new Weapon(GameContent.Current.Weapons["tower_guard"], made.Forged, made.Modifiers, souls, 0, wear: 60, wearCashed: 1335);
        Give(campaign, "B", 2, full);
        Assert.Equal("ALL 5 SLOTS ARE FILLED", Enchanter.Refusal(campaign, full, Service.Enchant, "cold"));
        Assert.Null(Enchanter.Refusal(campaign, full, Service.Refine));

        // The key sets the set: at three slots, a unique arriving with three is full.
        using (TestContent.UsePremise(tuning: t => Ruled(t) with { EnchantmentSlots = 3 }))
        {
            var knife = Worn("flensing_knife_unique", wear: 60, cashed: 1335);
            Give(campaign, "C", 2, knife);
            Assert.Equal("ALL 3 SLOTS ARE FILLED", Enchanter.Refusal(campaign, knife, Service.Enchant, "cold"));
        }
    }

    [Fact]
    public void AnEnchantNeedsASeenId()
    {
        // §6.2: "you choose the enchantment", from what the campaign has met. An id it
        // has not met is refused (the screen lists only what it has); met, it is
        // allowed. An Enchant names one, and a refinement none.
        var campaign = Opened();
        var guard = Worn("tower_guard", wear: 30);
        var at = Give(campaign, "B", 2, guard);
        Assert.False(campaign.HasSeen("shocking"));
        Assert.Equal("SHOCKING HAS NOT BEEN MET - ONLY WHAT THE PARTY HAS CARRIED IS OFFERED", Enchanter.Refusal(campaign, guard, Service.Enchant, "shocking"));
        Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, at, Service.Enchant, "shocking"));
        Assert.Equal(30, guard.Wear);

        campaign.See(TestWeapons.Enchanted("Spark", 40, 10, 30, "shocking"));
        Assert.Null(Enchanter.Refusal(campaign, guard, Service.Enchant, "shocking"));

        Assert.Throws<ArgumentException>(() => Enchanter.Refusal(campaign, guard, Service.Enchant));
        Assert.Throws<ArgumentException>(() => Enchanter.Refusal(campaign, guard, Service.Refine, "shocking"));
        Assert.Throws<ArgumentException>(() => Enchanter.Refusal(campaign, TestWeapons.Get("tower_guard"), Service.Refine));   // no weapon the party owns
    }

    [Fact]
    public void TheEnchanterIsClosedDuringTheTutorial()
    {
        // §6.3: the enchanter is not open until the tutorial is beaten (S:411–419), so
        // no deposit is taken before then — for any service — and the refusal names
        // the tutorial as the hub's gate does.
        var campaign = CampaignState.Start(GameContent.Current);
        var staff = campaign.Party[0].Inventory[1]!;
        const string closed = "THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN";
        Assert.Equal(closed, Enchanter.Closed(GameContent.Current.Dungeons.Tutorial));
        Assert.Equal(closed, Enchanter.Refusal(campaign, staff, Service.Refine));
        Assert.Equal(closed, Enchanter.Refusal(campaign, staff, Service.Enchant, "regeneration"));
        Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine));
        Assert.Empty(campaign.AtTheEnchanter);

        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.Null(Enchanter.Refusal(campaign, staff, Service.Refine));
    }

    [Fact]
    public void TakingBackReversesTheCash_WearAndPoolAsBefore()
    {
        // PHASE 6a DECISION 8: a queued weapon is taken back between runs, its service
        // forfeit and its runs worked lost — and the deposit's cash reversed exactly,
        // so its wear and its capacity with the progress toward the next point are what
        // they were before it went in. So a deposit and a take-back are never a way to
        // grow a capacity with no downtime. The ordinal is never reused.
        var campaign = Opened();
        foreach (var (service, id) in new[] { (Service.Enchant, (string?)"vampiric"), (Service.Refine, null) })
        {
            var staff = Worn("staff_of_renewal", wear: 35, cashed: 170);   // capacity 35, 10 cashed toward 36: its second is two runs in the shop
            var at = Give(campaign, "B", 2, staff);
            var (wear, pool, enchantments) = (staff.Wear, staff.WearPool, staff.Enchantments.ToArray());
            int ordinal = campaign.DepositsMade;
            var made = Enchanter.Deposit(campaign, at, service, id);
            Assert.Equal((ordinal, 2), (made.Entry.Ordinal, made.Entry.Runs));
            Assert.NotEqual(pool, staff.WearPool);
            Counted(campaign);   // a run worked: lost with the take-back
            Assert.Equal(1, made.Entry.RunsWorked);

            Enchanter.TakeBack(campaign, made.Entry);
            Assert.Equal((wear, pool), (staff.Wear, staff.WearPool));
            Assert.Equal(enchantments, staff.Enchantments);
            Assert.Same(staff, campaign.Stable[^1]);
            Assert.Empty(campaign.AtTheEnchanter);
            Assert.Equal(ordinal + 1, campaign.DepositsMade);   // never reused

            Assert.Throws<InvalidOperationException>(() => Enchanter.TakeBack(campaign, made.Entry));   // no longer in the shop
        }

        // Mid-run, the shop is shut: nothing is taken back.
        var aStaff = campaign.Party[0].Inventory[1]!;
        var entry = Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine).Entry;
        campaign.Enter("long_gallery", CampaignRuns.FiveDeep).OpenTurns(campaign.Party, CampaignRuns.Ten);
        Assert.Throws<InvalidOperationException>(() => Enchanter.TakeBack(campaign, entry));
        Assert.Same(entry, Queued(campaign, aStaff));
    }

    [Fact]
    public void ADepositNeverLeavesAMemberUnarmed()
    {
        // PHASE 6a DECISION 7: no deposit leaves a member carrying no weapon — the
        // refusal names the member, and the deposit throws, moving and cashing nothing.
        // A hand a deposit empties takes up the first weapon in its bag, as a move's
        // does; and a weapon from the stable disarms nobody.
        var campaign = Opened();
        var stiletto = Worn("weakspot_stiletto", wear: 30);
        Give(campaign, "A", 0, stiletto);
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        Assert.Equal("[A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST", Enchanter.Refusal(campaign, stiletto, Service.Enchant, "vampiric"));
        Assert.Throws<InvalidOperationException>(() => Enchanter.Deposit(campaign, new WeaponPlace("A", 0), Service.Refine));
        Assert.Equal((30, 0), (stiletto.Wear, stiletto.WearCashed));
        Assert.Same(stiletto, campaign.Party[0].EquippedWeapon);

        // B carries two: its hand in, and its staff taken up into the hand.
        var (guard, bStaff) = (campaign.Party[1].Inventory[0]!, campaign.Party[1].Inventory[1]!);
        var made = Enchanter.Deposit(campaign, new WeaponPlace("B", 0), Service.Refine);
        Assert.Same(bStaff, made.Armed);
        Assert.Same(bStaff, campaign.Party[1].EquippedWeapon);
        Assert.Null(campaign.Party[1].Inventory[1]);
        Assert.Same(guard, made.Entry.Weapon);

        // From the stable: nobody carries it, so nobody is left unarmed.
        var stabled = campaign.Stable[0];
        Assert.Null(Enchanter.Refusal(campaign, stabled, Service.Refine));
        Assert.Null(Enchanter.Deposit(campaign, WeaponPlace.InTheStable(0), Service.Refine).Armed);
        Assert.Empty(campaign.Stable);

        // A weapon in the shop already is refused as such, before anything else.
        Assert.Equal("TOWER GUARD IS IN THE SHOP ALREADY", Enchanter.Refusal(campaign, guard, Service.Refine));
        Assert.Equal("TOWER GUARD IS IN THE SHOP ALREADY", Enchanter.InTheShop(guard));
    }

    [Fact]
    public void TheShopsDoorAsksItsOwnQuestions()
    {
        // CampaignState.Enqueue, the hub's door into the shop, holds its own gates
        // whatever its one caller asked before it (Enchanter.Deposit asks the
        // enchanter's first): the weapon at the place is the deposit's, the ordinal
        // is the next, and no member is left carrying none. Refused, it moves
        // nothing; handed a deposit whole, it takes it — that deposit, by reference.
        // (Its mid-run gate is AFinishedEnchantAttachesLast_AtTier1_AndReturnsToTheStable's.)
        var campaign = Opened();
        var (bGuard, bStaff) = (campaign.Party[1].Inventory[0]!, campaign.Party[1].Inventory[1]!);
        var at = new WeaponPlace("B", 1);
        Deposit Of(Weapon weapon, int ordinal) => new(ordinal, weapon, Service.Refine, 0, Wear.ServiceRuns(weapon.Enchantments.Count));

        Assert.Throws<ArgumentException>(() => campaign.Enqueue(at, Of(bGuard, 0)));   // the weapon at the place is another
        Assert.Throws<ArgumentException>(() => campaign.Enqueue(at, Of(bStaff, 1)));   // not the next ordinal
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        var stiletto = campaign.Party[0].Inventory[0]!;
        Assert.Throws<InvalidOperationException>(() => campaign.Enqueue(new WeaponPlace("A", 0), Of(stiletto, 0)));   // A's last weapon
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Equal(0, campaign.DepositsMade);
        Assert.Same(bStaff, campaign.Party[1].Inventory[1]);
        Assert.Same(stiletto, campaign.Party[0].Inventory[0]);

        var entry = Of(bStaff, 0);
        Assert.NotEqual(Of(bStaff, 0), entry);   // a deposit is the one it is, never equal to another by its values
        Assert.Null(campaign.Enqueue(at, entry));
        Assert.Same(entry, Assert.Single(campaign.AtTheEnchanter));
        Assert.Equal(1, campaign.DepositsMade);
        Assert.Null(campaign.Party[1].Inventory[1]);
    }

    [Fact]
    public void AttachRefusesPastTheMostEnchantments()
    {
        // A weapon holds at most ContentBounds.MostEnchantments — EnchantmentSlots at
        // its clamp's top — and attaching past it throws, so no path builds a weapon
        // the next load refuses. Below it, an attachment goes last.
        var made = TestWeapons.Get("tower_guard");
        var def = GameContent.Current.Weapons["tower_guard"];
        var cold = GameContent.Current.Enchantments["cold"];
        var nearly = new Weapon(def, made.Forged, made.Modifiers, Enumerable.Range(0, ContentBounds.MostEnchantments - 1).Select(_ => new Enchantment(cold, 1)).ToArray(), 0);
        var flaming = new Enchantment(GameContent.Current.Enchantments["flaming"], 1);
        nearly.Attach(flaming);
        Assert.Same(flaming, nearly.Enchantments[^1]);
        Assert.Equal(ContentBounds.MostEnchantments, nearly.Enchantments.Count);
        Assert.Throws<InvalidOperationException>(() => nearly.Attach(new Enchantment(cold, 1)));
        Assert.Equal(ContentBounds.MostEnchantments, nearly.Enchantments.Count);
    }

    [Fact]
    public void WhatARunsEndDidIsLoggedAtTheEdge()
    {
        // Logic never logs (LogicPurityTests), so what the enchanter did on a run's
        // end is handed over in the result (RunResult.Services) and written by the
        // game: whether the run counted, what it worked and left stalled with each
        // one's runs, and each service it finished — nothing for an empty shop. A
        // handler on the campaign's RunEnded beside the run's end's save request,
        // wired where that one is and let go where it is, read off the IL.
        var campaign = TestShop.Shop();
        var idle = TestShop.NotCounted(campaign);
        Assert.Equal(["[Enchanter] Run not counted: nothing worked; stalled Staff of Renewal 1/2, Staff of Renewal 1/2, Tower Guard 0/1"],
            DistanceRpgGame.EnchanterLines(idle));
        var back = Counted(campaign);
        Assert.Equal(new[]
        {
            "[Enchanter] Run counted: worked Staff of Renewal 2/2, Tower Guard 1/1; stalled Staff of Renewal 1/2",
            "[Enchanter] Staff of Renewal finished: attached vampiric",
            "[Enchanter] Tower Guard finished: refined",
        }, DistanceRpgGame.EnchanterLines(back));
        Assert.Empty(DistanceRpgGame.EnchanterLines(Counted(TestShop.Opened())));

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Same(MethodBase x, MethodBase y) => x.Module == y.Module && x.MetadataToken == y.MetadataToken;
        var game = typeof(DistanceRpgGame);
        var handler = game.GetMethod("LogTheEnchanter", Declared)!;
        Assert.Equal(["Adopt", "DropCampaign"], Edges(game.Assembly).Where(edge => IsDelegate(edge.Op) && Same(edge.Target, handler))
            .Select(edge => edge.From.Name).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(CallsIn(handler), called => called.DeclaringType?.Name == "Log" && called.Name == "Info");
        Assert.Contains(CallsIn(handler), called => Same(called, game.GetMethod(nameof(DistanceRpgGame.EnchanterLines), Declared)!));
        var adopt = CallsIn(game.GetMethod("Adopt", Declared)!).Select(called => called.Name).ToList();
        Assert.Equal(2, adopt.Count(name => name == "add_RunEnded"));   // the save request and the log, each once
    }

    [Fact]
    public void WearIsSpentAndReturnedInOnePlace()
    {
        // The enchanter is the single place wear is spent (P6:860–861), and the one
        // door each of its moves goes through, read off the IL: the pool is cashed by
        // a deposit alone and refunded by a take-back alone; a deposit enters the
        // shop by the hub's door and a take-back leaves it by another; a finished
        // service comes back by the campaign's own path, which the clock alone takes,
        // and the clock is asked by the run's end alone — after the visit is gone.
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Same(MethodBase x, MethodBase y) => x.Module == y.Module && x.MetadataToken == y.MetadataToken;
        string[] CallersOf(Type type, string name) => Calls(typeof(CampaignState).Assembly)
            .Where(call => Same(call.Target, type.GetMethod(name, Declared)!))
            .Select(call => $"{Root(call.From.DeclaringType)!.Name}.{call.From.Name}")
            .Distinct().Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Enchanter.Deposit"], CallersOf(typeof(Weapon), "CashWear"));
        Assert.Equal(["Enchanter.TakeBack"], CallersOf(typeof(Weapon), "RefundWear"));
        Assert.Equal(["Enchanter.Complete"], CallersOf(typeof(Weapon), "Attach"));
        Assert.Equal(["Enchanter.Deposit"], CallersOf(typeof(CampaignState), "Enqueue"));
        Assert.Equal(["Enchanter.TakeBack"], CallersOf(typeof(CampaignState), "TakeOut"));
        Assert.Equal(["Enchanter.Tick"], CallersOf(typeof(CampaignState), "Return"));
        Assert.Equal(["CampaignState.Return", "CampaignState.TakeOut"], CallersOf(typeof(CampaignState), "Release"));
        Assert.Equal(["CampaignState.EndRun"], CallersOf(typeof(Enchanter), "Tick"));

        // In the run's end, the clock is asked once the visit is gone.
        var endRun = typeof(CampaignState).GetMethod(nameof(CampaignState.EndRun), Declared)!;
        var calls = CallsIn(endRun).ToList();
        int gone = calls.FindLastIndex(called => called.Name == "set_Visit");
        int tick = calls.FindIndex(called => Same(called, typeof(Enchanter).GetMethod("Tick", Declared)!));
        Assert.True(gone >= 0 && tick > gone, "EndRun asks the enchanter's clock before the visit is gone");

        // The queue is touched by its view, the doors, the load, and nothing else.
        var queue = typeof(CampaignState).GetField("_enchanter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string[] Touching(params OpCode[] ops) => typeof(CampaignState).GetNestedTypes(Declared).Prepend(typeof(CampaignState))
            .SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            .Where(method => Body(method) is { } il && InstructionsAt(il).Any(i => ops.Contains(i.Op)
                && method.Module.ResolveField(BitConverter.ToInt32(il, i.At)) is { } field
                && field.Module == queue.Module && field.MetadataToken == queue.MetadataToken))
            .Select(method => method.Name).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Enqueue", "Release", "Restore", "get_AtTheEnchanter"], Touching(OpCodes.Ldfld, OpCodes.Ldflda));
        Assert.Equal([".ctor"], Touching(OpCodes.Stfld));
    }
}

/// <summary>
/// Campaigns with something at the enchanter, for the tests of the shop and what
/// reads it — the save, the hub, the summary: the game's own party, the tutorial
/// beaten, and deposits made as play makes them (<see cref="Enchanter.Deposit"/>),
/// worked by whole runs (<see cref="CampaignRuns"/>). The ruling's numbers are
/// stated as the caller's own input (<see cref="Ruled"/>), made current over the
/// premise's by whoever uses them.
/// </summary>
internal static class TestShop
{
    /// <summary>The ruling's numbers, stated as the tests' own: 30 to start and a point a full bar, slots every 5 from 30, the price 30 + 5(n − 1), five slots, and the two newest worked.</summary>
    public static Tuning Ruled(Tuning t) => t with
    {
        StartingPool = 30, WearPerHit = 1, WearCapacityPerBar = 1, FirstSlotCapacity = 30, SlotCapacityStep = 5, EnchantmentSlots = 5,
        FirstEnchantmentWear = 30, EnchantmentWearStep = 5, EnchanterWorksOn = 2,
    };

    /// <summary>The game's own party — A a Weakspot Stiletto and a Staff of Renewal, B a Tower Guard and one, C a Great Axe and one, D a Staff of Renewal and a Staff of Mire — with the tutorial beaten, so the enchanter is open, and Vampiric, Flaming and Cold met.</summary>
    public static CampaignState Opened()
    {
        var campaign = CampaignState.Start(GameContent.Current);
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        campaign.See(TestWeapons.Enchanted("Seer", 40, 10, 30, "vampiric", "flaming", "cold"));
        return campaign;
    }

    /// <summary><paramref name="id"/>'s row brought back as a save brings it, with <paramref name="wear"/> banked and <paramref name="cashed"/> ever cashed: the restore door.</summary>
    public static Weapon Worn(string id, int wear, int cashed = 0)
    {
        var made = TestWeapons.Get(id);
        return new Weapon(GameContent.Current.Weapons[id], made.Forged, made.Modifiers, made.Enchantments, made.ForgedEnchantmentCount, wear, cashed);
    }

    /// <summary>Lay <paramref name="weapon"/> in <paramref name="memberId"/>'s bag slot <paramref name="slot"/>, over what was there, and say where.</summary>
    public static WeaponPlace Give(CampaignState campaign, string memberId, int slot, Weapon weapon)
    {
        campaign.Party.Single(member => member.Id == memberId).Inventory[slot] = weapon;
        return new WeaponPlace(memberId, slot);
    }

    /// <summary>
    /// The dungeons as a <c>dungeons.json</c> beside the game may have them after
    /// deposits were made: <c>retiresOnClear</c> moved off the tutorial onto the
    /// Spear Hall, not beaten (a retiring dungeon pins its floor) — so today's
    /// tutorial is one the campaign has not retired, whatever it has.
    /// </summary>
    public static DungeonsData TutorialMoved(DungeonsData data) => new(data.Dungeons.Select(def => def.Id switch
    {
        "spear_hall" => def with { RetiresOnClear = true, BossFloorMin = 5, BossFloorMax = 5 },
        "tutorial" => def with { RetiresOnClear = false },
        _ => def,
    }).ToArray());

    /// <summary>A run that counts: three floors down the Long Gallery and back out, a mercy.</summary>
    public static RunResult Counted(CampaignState campaign) => CampaignRuns.DeepRetreat(campaign, "long_gallery");

    /// <summary>A run that counts nothing: in and straight back out of the Long Gallery's first floor.</summary>
    public static RunResult NotCounted(CampaignState campaign) => CampaignRuns.ShallowRetreat(campaign, "long_gallery");

    /// <summary>
    /// <see cref="Opened"/>, with three deposits in the shop, as play makes them:
    /// A's Staff of Renewal refined unworn (0: two runs), B's second Staff of Renewal
    /// enchanted Vampiric at capacity 35 (1: two runs, its wear cashed 160 before and
    /// 195 after), a counted run working both a run on, and then C's Tower Guard
    /// refined with 30 worn (2: one run, fresh). The last result reports 0 and 1
    /// worked; the next counted run works 1 and 2 and stalls 0.
    /// </summary>
    public static CampaignState Shop()
    {
        var campaign = Opened();
        Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine);
        Enchanter.Deposit(campaign, Give(campaign, "B", 2, Worn("staff_of_renewal", wear: 35, cashed: 160)), Service.Enchant, "vampiric");
        Counted(campaign);
        Enchanter.Deposit(campaign, Give(campaign, "C", 2, Worn("tower_guard", wear: 30)), Service.Refine);
        Assert.Equal([(0, 1, 2), (1, 1, 2), (2, 0, 1)], campaign.AtTheEnchanter.Select(deposit => (deposit.Ordinal, deposit.RunsWorked, deposit.Runs)));
        return campaign;
    }
}
