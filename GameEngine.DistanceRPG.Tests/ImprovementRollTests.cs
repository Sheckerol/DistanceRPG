using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The improvement roll (§6.4; Phase 6a.4): a service coming back from the
/// enchanter may improve the weapon anyway. It lands on the service's odds —
/// enchanting's <c>GraftBaseChance</c> in 100, flat, and refining's that times
/// <c>1 + wear ÷ StartingPool</c> on the whole deposit — and never when the deposit
/// cashed no wear; a landed roll deepens a modifier the weapon carries, biased
/// toward the class signature, or, rarer, grafts one it never carried, the
/// relations and the forged-plus-five ceiling binding both, one falling through to
/// the other when it has nothing to take (PHASE 6a DECISION 5). It rolls on a
/// stream of its own, keyed by the finishing run's map seed and the deposit's
/// ordinal, so a reload never rerolls it (R1). The ruling's own odds are pinned
/// against the master (<see cref="TheOddsAreTheRulings"/>, listed in
/// <c>ShippedDataTests</c>' doc); every other test states its numbers as its own
/// input (<see cref="TestShop.Ruled"/>, <see cref="TestShop.Landing"/>) over the
/// numbers the runs were written against (<see cref="Premise"/>), so the content
/// collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class ImprovementRollTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise(tuning: TestShop.Ruled);

    public void Dispose() => _premise.Dispose();

    /// <summary>The shipped seed, which does not fit in an int.</summary>
    private const long GoldenSeed = CampaignRuns.GoldenSeed;

    /// <summary><see cref="TestShop.Landing"/>, with what lands always a <paramref name="graftShare"/> in 100 a graft.</summary>
    private static Func<Tuning, Tuning> Landing(int graftShare) => t => TestShop.Landing(t) with { GraftSharePercent = graftShare };

    /// <summary>A fresh <paramref name="id"/>, a wand its element (Cold) where its row fixes none.</summary>
    private static Weapon Fresh(string id)
        => TestWeapons.Get(id, LootTable.NeedsAnElement(GameContent.Current.Weapons[id]) ? DamageType.Cold : null);

    /// <summary>A stream's first two outputs as one number: two streams with one fingerprint are one stream.</summary>
    private static ulong Fingerprint(Mulberry32 rng)
    {
        // NextDouble is t / 2^32 for an unsigned t, so this recovers t exactly.
        ulong first = (uint)(rng.NextDouble() * 4294967296.0);
        ulong second = (uint)(rng.NextDouble() * 4294967296.0);
        return (first << 32) | second;
    }

    [Fact]
    public void TheOddsAreTheRulings()
    {
        // §6.4 and settled.md (S:1149–1154; R4's consequence 2, S:1699–1703):
        // enchanting improves a weapon anyway at 3 in 100, flat, whatever the wear;
        // refining at 3 × (1 + wear ÷ StartingPool), on the whole deposit — 6 on a
        // fresh full bar of 30, 7 cashing a full bar at capacity 40, 9 at capacity
        // 60 (1,335 cashed from new) — with no top. The ruling's numbers, pinned as
        // it states them against the master (REWORK DECISION 3; ShippedDataTests
        // lists the pin): an edit to GraftBaseChance or StartingPool fails here by
        // name. Integers, so nothing is rounded: the numerator out of 100 ×
        // StartingPool.
        using (TestContent.Use())
        {
            Assert.Equal((3, 30), (GameContent.Current.Tuning.GraftBaseChance, GameContent.Current.Tuning.StartingPool));
            foreach (int wear in new[] { 1, 30, 35, 50, 840, int.MaxValue })
                Assert.Equal((90L, 3000L), Wear.ImproveOdds(Service.Enchant, wear));   // 3 in 100, flat

            foreach (var (cashed, capacity, inHundred) in new[] { (0, 30, 6), (345, 40, 7), (1335, 60, 9) })
            {
                Assert.Equal(capacity, Wear.Capacity(cashed).Max);   // a weapon grown to it
                var (numerator, denominator) = Wear.ImproveOdds(Service.Refine, capacity);   // cashing a full bar of it
                Assert.Equal(3000L, denominator);
                Assert.Equal(inHundred * denominator, numerator * 100);   // exactly, as a fraction of 100
            }
            Assert.Equal((135L, 3000L), Wear.ImproveOdds(Service.Refine, 15));   // half a fresh bar: 4.5 in 100, proportional

            // No top: a big enough deposit lands on every draw, and the arithmetic
            // never wraps, whatever wear a save holds.
            var (most, of) = Wear.ImproveOdds(Service.Refine, int.MaxValue);
            Assert.Equal(3L * (30L + int.MaxValue), most);
            Assert.True(most > of);
            Assert.Throws<ArgumentOutOfRangeException>(() => Wear.ImproveOdds(Service.Refine, -1));
        }

        // The odds are the tuning's: another chance or another bar moves them.
        using (TestContent.UsePremise(tuning: t => TestShop.Ruled(t) with { GraftBaseChance = 5, StartingPool = 40 }))
            Assert.Equal(((5L * 80, 4000L), (5L * 40, 4000L)), (Wear.ImproveOdds(Service.Refine, 40), Wear.ImproveOdds(Service.Enchant, 40)));
    }

    [Fact]
    public void NoWearNoRoll()
    {
        // P6:523–525: "a fresh drop refines for nothing. No wear, no roll — nothing
        // to repair." A deposit that cashed nothing rolls nothing and comes back as
        // it went, even at odds that land every time; one wear rolls. The only such
        // deposit play makes is a refinement of an unworn weapon — an enchantment's
        // price is a wear at least — and its report says no roll was made, its line
        // NO CHANGE.
        using var landing = TestContent.UsePremise(tuning: Landing(25));
        var guard = Fresh("tower_guard");
        var spread = guard.Modifiers;
        Assert.Equal(((Improvement?)null, (ImproveRoll?)null), Enchanter.Improve(guard, Service.Refine, 0, GoldenSeed, 0));
        Assert.Equal(spread, guard.Modifiers);
        var (improved, roll) = Enchanter.Improve(guard, Service.Refine, 1, GoldenSeed, 0);
        Assert.True(roll!.Landed);
        Assert.NotNull(improved);
        Assert.NotEqual(spread, guard.Modifiers);

        // Through the shop: A's Staff of Renewal, refined unworn, two runs.
        var campaign = TestShop.Opened();
        var staff = campaign.Party[0].WeaponIn(1)!;
        Assert.Equal((0, "staff_of_renewal"), (staff.Wear, staff.Id));
        var before = staff.Modifiers;
        Enchanter.Deposit(campaign, new WeaponPlace("A", 1), Service.Refine);
        TestShop.Counted(campaign);
        var report = Assert.Single(TestShop.Counted(campaign).Services!);
        Assert.True(report.Finished);
        Assert.Equal(((Improvement?)null, (ImproveRoll?)null), (report.Improved, report.Roll));
        Assert.Equal(before, staff.Modifiers);
        Assert.Equal("STAFF OF RENEWAL IS BACK FROM THE ENCHANTER - REFINED - NO CHANGE", BetweenRunsHud.BackFromTheEnchanter(report));
    }

    [Fact]
    public void RefiningChangesWhetherItLandsNeverWhat()
    {
        // S:782–784: "Refinement improves whether the roll lands, not what it lands
        // on." The first draw is whether it lands, and the only one the service
        // moves; which outcome and which modifier come after it, off the same
        // stream. So deposit by deposit, an Enchant and a Refine of the same weapon
        // draw the same number, and where both land they land on the same thing.
        // At the ruled 3 in 100 a refinement of a full bar of 30 lands wherever the
        // enchantment does, and more often; at odds that land every time the two
        // improve the weapon alike, a quarter of them grafts and the rest deepens.
        int enchantLands = 0, refineLands = 0;
        for (int ordinal = 0; ordinal < 3000; ordinal++)
        {
            var enchanted = Fresh("tower_guard");
            var refined = Fresh("tower_guard");
            var (e, eRoll) = Enchanter.Improve(enchanted, Service.Enchant, 30, GoldenSeed, ordinal);
            var (r, rRoll) = Enchanter.Improve(refined, Service.Refine, 30, GoldenSeed, ordinal);
            Assert.Equal(eRoll!.Draw, rRoll!.Draw);
            Assert.Equal((90L, 180L), (eRoll.Numerator, rRoll.Numerator));
            if (eRoll.Landed)
            {
                enchantLands++;
                Assert.True(rRoll.Landed);
                Assert.Equal(e, r);
                Assert.Equal(enchanted.Modifiers, refined.Modifiers);
            }
            if (rRoll.Landed)
                refineLands++;
        }
        Assert.InRange(enchantLands, 60, 120);        // about 3 in 100 of 3000
        Assert.InRange(refineLands, 2 * enchantLands - 60, 2 * enchantLands + 60);   // about twice as many

        using (TestContent.UsePremise(tuning: Landing(25)))
        {
            int grafts = 0;
            for (int ordinal = 0; ordinal < 400; ordinal++)
            {
                var enchanted = Fresh("tower_guard");
                var refined = Fresh("tower_guard");
                var e = Enchanter.Improve(enchanted, Service.Enchant, 30, GoldenSeed, ordinal).Improved;
                Assert.Equal(e, Enchanter.Improve(refined, Service.Refine, 30, GoldenSeed, ordinal).Improved);
                if (e!.Kind == ImprovementKind.Graft)
                    grafts++;
            }
            Assert.InRange(grafts, 70, 130);   // about a quarter of 400: deepen stays the common result
        }
    }

    [Fact]
    public void ADeepenRespectsForgedPlusFive_AndFavoursTheSignature()
    {
        // §6.4: a deepen is +1 on a modifier the weapon carries, "biased toward the
        // class signature" — the signature weighs DeepenSignatureWeight, the rest 1
        // (PHASE 6a DECISION 5) — and §1.1's ceiling binds it: forged plus the
        // acquired headroom, the one budget farming spends too. A Tower Guard, a
        // Sword forged Block x2 and Push x1, Block its class's signature.
        var guard = Fresh("tower_guard");
        Assert.Equal(Block, GameContent.Current.Weapons.Class(WeaponClass.Sword).Signature);
        Assert.Equal([(Block, 3), (Push, 1)], Enchanter.DeepenCandidates(guard));
        Assert.Equal([(Resonant, 1)], Enchanter.DeepenCandidates(Fresh("staff_of_renewal")));   // a caster has no signature
        using (TestContent.UsePremise(tuning: t => TestShop.Ruled(t) with { DeepenSignatureWeight = 1 }))
            Assert.Equal([(Block, 1), (Push, 1)], Enchanter.DeepenCandidates(Fresh("tower_guard")));   // 1 is no bias

        // What the farm already bought comes off the same budget: Block farmed to
        // its ceiling deepens no further.
        var farmed = Fresh("tower_guard");
        farmed.Acquire(Block, 5);
        Assert.Equal((7, 7), (farmed.Stacks(Block), GameContent.Current.Modifiers.Cap(Block, farmed.Forged.Stacks(Block))));
        Assert.Equal([(Push, 1)], Enchanter.DeepenCandidates(farmed));

        using var deepening = TestContent.UsePremise(tuning: Landing(0));
        int block = 0, push = 0;
        for (int ordinal = 0; ordinal < 2000; ordinal++)
        {
            var improved = Enchanter.Improve(Fresh("tower_guard"), Service.Enchant, 30, GoldenSeed, ordinal).Improved!;
            Assert.Equal(ImprovementKind.Deepen, improved.Kind);
            if (improved.Modifier == Block) block++; else push++;
        }
        Assert.Equal(2000, block + push);
        Assert.InRange(block / 2000.0, 0.70, 0.80);   // three in four: the signature at 3 against Push's 1

        // One weapon improved again and again deepens to forged + 5 and no further.
        var worked = Fresh("tower_guard");
        for (int ordinal = 0; ordinal < 10; ordinal++)
            Assert.Equal(ImprovementKind.Deepen, Enchanter.Improve(worked, Service.Refine, 30, GoldenSeed, ordinal).Improved!.Kind);
        Assert.Equal((7, 6), (worked.Stacks(Block), worked.Stacks(Push)));   // 2 + 5 and 1 + 5
        Assert.Empty(Enchanter.DeepenCandidates(worked));
    }

    [Fact]
    public void AGraftCapsAtFive_AndOnlyLegalOnesAreOffered()
    {
        // §6.4 and §1.1: a graft is +1 of a modifier the weapon never carried, and
        // every relation binds it — the kind, the exclusions, the prerequisites
        // (so a graft is order-dependent: Block first makes Riposte legal later)
        // and forged-only, so Charges, the one negative graft, is never grafted. A
        // graft is forged at 0, so it caps at the acquired headroom, 5: "a spear can
        // sprout Block, and it will never be a shield".
        Assert.Equal([Brace, CritWindow, CritMultiplier, Cleave, Longshot, Light, Riposte, Splitting, Opportunist, Pin, Softening, CritWeaken, CritSunder, BlockWeaken, Resonant],
            Enchanter.GraftCandidates(Fresh("tower_guard")));   // no Drag or Rout beside Push, no Overwatch on a sword, no Charges

        var stiletto = Fresh("weakspot_stiletto");   // CritWindow, CritMultiplier, CritSunder: no Block
        Assert.DoesNotContain(Riposte, Enchanter.GraftCandidates(stiletto));
        Assert.DoesNotContain(BlockWeaken, Enchanter.GraftCandidates(stiletto));
        Assert.DoesNotContain(CritWeaken, Enchanter.GraftCandidates(stiletto));   // beside CritSunder
        stiletto.Acquire(Block, 1);   // a graft of Block
        Assert.Contains(Riposte, Enchanter.GraftCandidates(stiletto));
        Assert.Contains(BlockWeaken, Enchanter.GraftCandidates(stiletto));

        var bow = Fresh("hunting_bow");
        Assert.Contains(Overwatch, Enchanter.GraftCandidates(bow));
        Assert.DoesNotContain(Brace, Enchanter.GraftCandidates(bow));
        Assert.DoesNotContain(Opportunist, Enchanter.GraftCandidates(bow));

        // Charges deepens where it is forged, and is grafted nowhere.
        Assert.Contains(Enchanter.DeepenCandidates(Fresh("bandolier")), candidate => candidate.Modifier == Charges);
        foreach (var def in GameContent.Current.Weapons.All)
            Assert.DoesNotContain(Charges, Enchanter.GraftCandidates(Fresh(def.Id)));

        // A graft caps at five: the stiletto's Block, grafted, deepens to the
        // headroom and no further, while its forged CritWindow goes to six.
        var rules = GameContent.Current.Modifiers;
        Assert.Equal((5, 6), (rules.Cap(Block, stiletto.Forged.Stacks(Block)), rules.Cap(CritWindow, stiletto.Forged.Stacks(CritWindow))));
        stiletto.Acquire(Block, 10);
        Assert.Equal(5, stiletto.Stacks(Block));
        Assert.DoesNotContain(Enchanter.DeepenCandidates(stiletto), candidate => candidate.Modifier == Block);

        // Every weapon of the catalogue, always grafting: what lands is one of its
        // candidates as it stood, and the relations hold after it (Acquire would
        // throw otherwise); a graft lands at one stack, forged at none.
        using var grafting = TestContent.UsePremise(tuning: Landing(100));
        foreach (var def in GameContent.Current.Weapons.All)
            for (int ordinal = 0; ordinal < 8; ordinal++)
            {
                var weapon = Fresh(def.Id);
                var offered = Enchanter.GraftCandidates(weapon);
                var improved = Enchanter.Improve(weapon, Service.Refine, 30, GoldenSeed, ordinal).Improved!;
                Assert.Equal(ImprovementKind.Graft, improved.Kind);
                Assert.Contains(improved.Modifier, offered);
                Assert.Equal((1, 0), (weapon.Stacks(improved.Modifier), weapon.Forged.Stacks(improved.Modifier)));
            }
    }

    [Fact]
    public void ABranchWithNothingFallsThrough_BothEmptyIsNoChange()
    {
        // PHASE 6a DECISION 5: an outcome with nothing it may take falls through to
        // the other, and a landed roll with neither changes nothing — "a weapon with
        // every modifier capped rolls nothing at all" (P6:610–611), read as every
        // modifier it may hold, since a weapon with every modifier it carries capped
        // "can still gain breadth through grafts" (P6:616–618).
        // Always deepening, a Tower Guard with Block and Push at their ceilings grafts.
        using (TestContent.UsePremise(tuning: Landing(0)))
        {
            var capped = Fresh("tower_guard");
            capped.Acquire(Block, 5);
            capped.Acquire(Push, 5);
            Assert.Empty(Enchanter.DeepenCandidates(capped));
            var improved = Enchanter.Improve(capped, Service.Refine, 30, GoldenSeed, 0).Improved!;
            Assert.Equal(ImprovementKind.Graft, improved.Kind);
            Assert.Equal(1, capped.Stacks(improved.Modifier));
        }

        // Always grafting, a dagger carrying one of every group — nothing left it
        // may take — deepens; capped too, the roll lands and changes nothing.
        using (TestContent.UsePremise(tuning: Landing(100)))
        {
            ModifierType[] carried = [Brace, Block, CritWindow, CritMultiplier, Cleave, Longshot, Light, Riposte, Push, Splitting, Pin, Softening, CritWeaken];
            var everything = TestWeapons.Make("Everything", 1, 1, 1, carried.Select(t => (t, 1)).ToArray());
            Assert.Empty(Enchanter.GraftCandidates(everything));
            Assert.Equal(carried.Length, Enchanter.DeepenCandidates(everything).Count);
            var improved = Enchanter.Improve(everything, Service.Refine, 30, GoldenSeed, 0).Improved!;
            Assert.Equal(ImprovementKind.Deepen, improved.Kind);
            Assert.Equal(2, everything.Stacks(improved.Modifier));

            foreach (var t in carried)
                everything.Acquire(t, 5);
            Assert.Empty(Enchanter.DeepenCandidates(everything));
            var spread = everything.Modifiers;
            var (nothing, roll) = Enchanter.Improve(everything, Service.Refine, 30, GoldenSeed, 1);
            Assert.True(roll!.Landed);
            Assert.Null(nothing);
            Assert.Equal(spread, everything.Modifiers);
        }
    }

    [Fact]
    public void EveryModifiersFirstStackIsANonDecrease()
    {
        // S:387–390: "Charges is the only negative graft … Still worth a test
        // asserting every modifier's first stack is a non-decrease, so a future
        // modifier cannot reintroduce the shape unnoticed." The measure is what the
        // rules read of a modifier on the weapon it is grafted to, at one stack
        // against none. Logic reads a modifier one of two ways:
        // - by its value (ActorState.Value): Offset + PerStack × stacks, more being
        //   better, as S:387's sweep found of every row. A stack never lowers it —
        //   every PerStack is clamped 0 up — so that read is a non-decrease under
        //   any tuning that loads, Charges' 1 to 2 included, and guards nothing: it
        //   is checked below as the premise, not as the guard;
        // - by its presence: a Stacks call naming the modifier, a rule its first
        //   stack switches on. That is where the shape lives — "a cap where there
        //   was none" — and no comparison of the table's numbers can see it. Every
        //   presence read in Logic's IL is found and pinned, and each is measured by
        //   the reader that makes it, at none and at one: today
        //   TurnSystem.AttacksPerTurn's of Charges alone, no limit at none and 2
        //   throws at one.
        // A first stack that tightens a read is the negative shape, and a modifier
        // whose first stack does must be forged-only, so no weapon is grafted it. A
        // new presence read fails the pin by name until its reader is measured here.
        Assert.Equal([("TurnSystem.AttacksPerTurn", Charges)], PresenceReads());
        var readers = new Dictionary<ModifierType, Func<PartyMemberState, int?>>
        {
            [Charges] = holder => new TurnSystem(new int[20, 20], [holder], [], () => 10).AttacksPerTurn(holder),   // null: no limit
        };
        Assert.Equal(PresenceReads().Select(read => read.Modifier).Distinct().Order(), readers.Keys.Order());

        // What t's first stack, acquired on a fresh weapon of id, does to the read
        // that asks for t by name: a limit where there was none, or a lower one, is
        // a decrease.
        string? Decrease(string id, ModifierType t)
        {
            if (!readers.TryGetValue(t, out var read))
                return null;   // read by its value alone
            var with = Fresh(id);
            with.Acquire(t, 1);
            int? none = read(TestPools.Holding("A", Fresh(id)));
            int? one = read(TestPools.Holding("A", with));
            return one is int limit && (none is not int free || limit < free)
                ? $"{t} grafted onto {with.Name}: {(none is int was ? was.ToString() : "no limit")} -> {limit}"
                : null;
        }

        // Every modifier the enchanter can graft onto any weapon of the catalogue.
        var rules = GameContent.Current.Modifiers;
        var grafted = new HashSet<ModifierType>();
        foreach (var def in GameContent.Current.Weapons.All)
            foreach (var t in Enchanter.GraftCandidates(Fresh(def.Id)))
            {
                grafted.Add(t);
                Assert.Null(Decrease(def.Id, t));
                Assert.True(rules.Resolve(t, 1) >= rules.Resolve(t, 0), $"{t}'s value falls with its first stack");   // the premise: PerStack ≥ 0
            }
        Assert.DoesNotContain(Charges, grafted);
        Assert.Equal(Enum.GetValues<ModifierType>().Except([Charges, .. ModifierRules.NeverStacked]).Order(), grafted.Order());   // every other one somewhere

        // The guard fires on the shape, and on nothing else: let out of forgedOnly,
        // Charges is offered to every weapon forged without it, and the measure
        // finds it a decrease on each — a bow held to 2 throws where only its
        // movement bound it — and no other graft one. Each it finds is in the
        // shipped forgedOnly.
        var forgedOnly = rules.ForgedOnly;
        using (TestContent.UsePremise(tuning: TestShop.Ruled, restricted: ShippedContent.Restricted with { ForgedOnly = [] }))
        {
            var offered = GameContent.Current.Weapons.All
                .SelectMany(def => Enchanter.GraftCandidates(Fresh(def.Id)).Select(t => (def.Id, Modifier: t)))
                .ToArray();
            var decreases = offered.Where(graft => Decrease(graft.Id, graft.Modifier) is not null).ToArray();
            Assert.Equal(offered.Where(graft => graft.Modifier == Charges), decreases);
            Assert.Equal(GameContent.Current.Weapons.All.Count(def => def.Forged.GetValueOrDefault(Charges) == 0), decreases.Length);
            Assert.Equal("Charges grafted onto Hunting Bow: no limit -> 2", Decrease("hunting_bow", Charges));
            Assert.All(decreases, decrease => Assert.Contains(decrease.Modifier, forgedOnly));
        }
    }

    /// <summary>
    /// Every presence read in <c>Logic/</c> — a call to a <c>Stacks(ModifierType)</c>,
    /// an actor's, a weapon's or a spread's, on a modifier the code names, the
    /// constant loaded just before the call — as the method that makes it and the
    /// modifier it asks for. A Stacks call on a modifier held in a variable asks the
    /// same of every modifier (the relations, the caps) and is not one.
    /// </summary>
    private static (string Reader, ModifierType Modifier)[] PresenceReads()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        string logic = typeof(Floor).Namespace!;
        var reads = new List<(string, ModifierType)>();
        foreach (var type in Types(typeof(Floor).Assembly).Where(t => t.Namespace is { } ns && (ns == logic || ns.StartsWith(logic + ".", StringComparison.Ordinal))))
            foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            {
                if (Body(method) is not { } il)
                    continue;
                int? loaded = null;
                foreach (var (op, token, at) in InstructionsAt(il))
                {
                    if (loaded is int value && IsCall(op) && Resolve(method, token) is { Name: "Stacks" } target
                        && target.GetParameters() is [{ ParameterType: var parameter }] && parameter == typeof(ModifierType))
                        reads.Add(($"{Root(method.DeclaringType)!.Name}.{method.Name}", (ModifierType)value));
                    loaded = Loaded(op, il, at);
                }
            }
        return reads.Distinct().Order().ToArray();
    }

    /// <summary>The int <paramref name="op"/> loads as a constant, its operand at <paramref name="at"/> in <paramref name="il"/>, or null when it loads none.</summary>
    private static int? Loaded(OpCode op, byte[] il, int at)
    {
        if (op == OpCodes.Ldc_I4_S)
            return (sbyte)il[at];
        if (op == OpCodes.Ldc_I4)
            return BitConverter.ToInt32(il, at);
        if (op == OpCodes.Ldc_I4_M1)
            return -1;
        int shortForm = Array.IndexOf(ShortConstants, op);
        return shortForm >= 0 ? shortForm : null;
    }

    /// <summary><c>ldc.i4.0</c> to <c>ldc.i4.8</c>, each at the index of the int it loads.</summary>
    private static readonly OpCode[] ShortConstants =
        [OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8];

    [Fact]
    public void TheRollIsTheRunsAndTheDepositsOwn_AReloadNeverRerolls()
    {
        // R1 (S:1643–1652): a bad roll is never reloaded away. The roll's stream is
        // keyed by the finishing run's map seed and the deposit's ordinal, both
        // saved: the same two give the same roll, and a campaign saved as the run
        // began, loaded and played to the same run's end, comes back with the same
        // report and the same weapon. The roll is made in one place, as a service
        // finishes, off the one stream.
        using var landing = TestContent.UsePremise(tuning: Landing(25));
        var one = Fresh("tower_guard");
        var two = Fresh("tower_guard");
        Assert.Equal(Enchanter.Improve(one, Service.Refine, 30, GoldenSeed, 7), Enchanter.Improve(two, Service.Refine, 30, GoldenSeed, 7));
        Assert.Equal(one.Modifiers, two.Modifiers);
        Assert.Equal(Fingerprint(Enchanter.ImproveStream(GoldenSeed, 7)), Fingerprint(Enchanter.ImproveStream(GoldenSeed, 7)));
        Assert.NotEqual(Fingerprint(Enchanter.ImproveStream(GoldenSeed, 7)), Fingerprint(Enchanter.ImproveStream(GoldenSeed, 8)));
        Assert.NotEqual(Fingerprint(Enchanter.ImproveStream(GoldenSeed, 7)), Fingerprint(Enchanter.ImproveStream(GoldenSeed + 1, 7)));

        var campaign = TestShop.Opened();
        var guard = TestShop.Worn("tower_guard", wear: 30);
        Enchanter.Deposit(campaign, TestShop.Give(campaign, "C", 2, guard), Service.Refine);
        var visit = campaign.Enter("long_gallery", CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        string text = SaveCodec.Write(SaveGame.Capture(campaign));
        var loaded = SaveGame.Restore(SaveCodec.Read(text), GameContent.Current).Campaign;
        loaded.Visit!.OpenTurns(loaded.Party, CampaignRuns.Ten);

        RunResult Retreat(CampaignState played)
        {
            CampaignRuns.Down(played.Visit!, played.Party);
            CampaignRuns.Down(played.Visit!, played.Party);
            var result = CampaignRuns.Exit(played);
            Assert.True(result.ServiceTick);
            return result;
        }
        var played = Retreat(campaign);
        var replayed = Retreat(loaded);
        var report = Assert.Single(played.Services!);
        Assert.True(report.Finished);
        Assert.NotNull(report.Improved);
        Assert.Equal(new ImproveRoll(Enchanter.ImproveStream(CampaignRuns.FiveDeep, 0).NextInt(0, 2999), 3000, 6000), report.Roll);
        Assert.Equal<ServiceReport>(played.Services!, replayed.Services!);
        Assert.Equal(guard.Modifiers, loaded.Stable[^1].Modifiers);
        Assert.Same(guard, campaign.Stable[^1]);

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Same(MethodBase x, MethodBase y) => x.Module == y.Module && x.MetadataToken == y.MetadataToken;
        string[] CallersOf(string name) => Calls(typeof(Enchanter).Assembly)
            .Where(call => Same(call.Target, typeof(Enchanter).GetMethod(name, Declared)!))
            .Select(call => $"{Root(call.From.DeclaringType)!.Name}.{call.From.Name}")
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Enchanter.Tick"], CallersOf(nameof(Enchanter.Improve)));
        Assert.Equal(["Enchanter.Improve"], CallersOf(nameof(Enchanter.ImproveStream)));
    }

    [Fact]
    public void TheRollIsKeyedByTheOrdinal_NeverThePlaceInTheShop()
    {
        // §1d: the roll's second key is the deposit's ordinal — the campaign's count
        // of deposits as it went in, never reused — not where the deposit stands in
        // the shop as the run ends. Every other pinned roll comes off a deposit whose
        // ordinal is its place. TestShop's shop with deposit 0 taken back: the
        // staff's Vampiric (ordinal 1) and the Tower Guard's refinement (ordinal 2)
        // now stand first and second, and each rolls on its own ordinal's stream —
        // the draws 1073 and 2815 TheReportCarriesTheDrawForTheLog pins — and lands
        // on what that stream draws next. A tick keyed by the place would draw 232,
        // ordinal 0's.
        using var landing = TestContent.UsePremise(tuning: Landing(25));
        var campaign = TestShop.Shop();
        Enchanter.TakeBack(campaign, campaign.AtTheEnchanter[0]);
        Assert.Equal([1, 2], campaign.AtTheEnchanter.Select(deposit => deposit.Ordinal));
        Assert.Equal(232, Enchanter.ImproveStream(CampaignRuns.FiveDeep, 0).NextInt(0, 2999));

        var back = TestShop.Counted(campaign).Services!;
        Assert.Equal(["staff_of_renewal", "tower_guard"], back.Select(report => report.WeaponId));
        Assert.All(back, report => Assert.True(report.Finished));
        Assert.Equal(new ImproveRoll(1073, 3000, 3000), back[0].Roll);   // enchanting: 100 in 100
        Assert.Equal(new ImproveRoll(2815, 3000, 6000), back[1].Roll);   // refining 30 cashed: twice that
        Assert.Equal(new Improvement(ImprovementKind.Deepen, Resonant), back[0].Improved);   // a staff's one deepen
        Assert.Equal(new Improvement(ImprovementKind.Deepen, Block), back[1].Improved);
    }

    [Fact]
    public void WhatALandedRollLandsOnIsPinned()
    {
        // §1d's draws, always in this order: whether it lands, then which outcome,
        // then which modifier — a deepen walking its candidates in ModifierType
        // order, each as wide as its weight, a graft indexing its list alike. The
        // ratio tests would pass a reorder of the second and third draws, a walk
        // from the other end or another index, and each moves what a seed and an
        // ordinal land on — a mid-run save resumed on a later build included — so
        // the outcomes are pinned: a Tower Guard (Block x2, the signature, weighing
        // 3; Push x1) refined on a full bar of 30 at odds that always land, a
        // quarter grafting, over the shipped seed's first twelve ordinals. The
        // shop's, through the tick, are TheRollIsKeyedByTheOrdinal_NeverThePlaceInTheShop's.
        using var landing = TestContent.UsePremise(tuning: Landing(25));
        Improvement Deepen(ModifierType t) => new(ImprovementKind.Deepen, t);
        Improvement Graft(ModifierType t) => new(ImprovementKind.Graft, t);
        Improvement[] expected =
        [
            Graft(Softening), Graft(Light), Deepen(Block), Deepen(Block), Deepen(Block), Deepen(Push),
            Deepen(Block), Deepen(Push), Graft(Brace), Deepen(Block), Deepen(Block), Deepen(Block),
        ];
        var landed = Enumerable.Range(0, expected.Length)
            .Select(ordinal => Enchanter.Improve(Fresh("tower_guard"), Service.Refine, 30, GoldenSeed, ordinal).Improved)
            .ToArray();
        Assert.Equal(expected, landed);
    }

    [Fact]
    public void TheStreamIsItsOwn()
    {
        // New randomness gets "its own stream derived from the map seed … never a
        // continuation" (ROADMAP): SplitMix32(mapSeed ^ ServiceSalt ^ ordinal ×
        // ServiceMix), a stream per deposit, on two constants of its own — written
        // out here as literals, so this pins the formula rather than restating the
        // code — each odd and 32-bit, and neither a value any other salt or mix in
        // Logic holds, found by reflection, the private ones included.
        Assert.Equal((0xCC9E2D51L, 0x1B873593L), (Enchanter.ServiceSalt, Enchanter.ServiceMix));
        foreach (long seed in new[] { GoldenSeed, 0L, uint.MaxValue })
            foreach (int ordinal in new[] { 0, 1, 2, 4095, 4096 })
                Assert.Equal(Fingerprint(new Mulberry32(FarmLadder.SplitMix32(seed ^ 0xCC9E2D51 ^ (ordinal * 0x1B873593L)))),
                    Fingerprint(Enchanter.ImproveStream(seed, ordinal)));

        var constants = Types(typeof(Floor).Assembly)
            .Where(t => t.Namespace == typeof(Floor).Namespace)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(f => f.IsLiteral && (f.Name.EndsWith("Salt", StringComparison.Ordinal) || f.Name.EndsWith("Mix", StringComparison.Ordinal)))
            .Select(f => (Name: $"{f.DeclaringType!.Name}.{f.Name}", Value: Convert.ToInt64(f.GetRawConstantValue())))
            .ToArray();
        Assert.Contains(constants, c => c.Name == $"{nameof(Enchanter)}.{nameof(Enchanter.ServiceSalt)}");   // the scan finds what it guards
        foreach (long constant in new[] { Enchanter.ServiceSalt, Enchanter.ServiceMix })
        {
            Assert.InRange(constant, 1L, uint.MaxValue);
            Assert.Equal(1, constant & 1);
            Assert.Single(constants, c => c.Value == constant);
        }

        // No deposit a campaign can plausibly make rolls on a stream anything else
        // rolls on, under the shipped BossFloorMax: ordinals 0 to 4096 coincide with
        // no drop, revival or boss-drop stream of any floor a boss can sit on, over
        // the domain those families are verified on (drops to spawn 128, revivals
        // to spawn and cycle 64), nor with the visit's dice, draws 0 to 4096. Every
        // family is SplitMix32 over the map seed XORed with a term of its own, so
        // the check holds whatever the map seed; the roll is keyed by the run's
        // seed, as the dice are, the rest by each floor's.
        using var shipped = TestContent.Use();
        int floors = GameContent.Current.Tuning.BossFloorMax;
        Assert.Equal(10, floors);
        foreach (long seed in new[] { GoldenSeed, 0L })
        {
            var finalized = new Dictionary<ulong, string>();
            void Finalized(Mulberry32 stream, string name)
            {
                ulong print = Fingerprint(stream);
                if (!finalized.TryAdd(print, name))
                    Assert.Fail($"{name} is the same stream as {finalized[print]}");
            }
            for (int n = 0; n < floors; n++)
            {
                long floorSeed = DungeonState.FloorSeed(seed, n);
                for (int spawn = 0; spawn <= 128; spawn++)
                    Finalized(LootTable.StreamFor(floorSeed, spawn), $"drop: floor {n}, spawn {spawn}");
                for (int spawn = 0; spawn <= 64; spawn++)
                    for (int cycle = 0; cycle <= 64; cycle++)
                        Finalized(FarmLadder.ReviveStream(floorSeed, spawn, cycle), $"revival: floor {n}, spawn {spawn}, cycle {cycle}");
                Finalized(BossLoot.StreamFor(floorSeed), $"boss drop: floor {n}");
            }
            for (int draw = 0; draw <= 4096; draw++)
                Finalized(DungeonState.DiceStream(seed, draw), $"dice: draw {draw}");
            for (int ordinal = 0; ordinal <= 4096; ordinal++)
                Finalized(Enchanter.ImproveStream(seed, ordinal), $"improvement: deposit {ordinal}");
            Assert.Equal(floors * (129 + 65 * 65 + 1) + 2 * 4097, finalized.Count);
        }
    }

    [Fact]
    public void TheReportCarriesTheDrawForTheLog()
    {
        // Logic never logs: a finished service's report carries its roll as it was
        // drawn — the first draw off its stream, out of 100 × StartingPool, against
        // the odds' numerator — for the run's end's log line, which shows the roll;
        // its keys, the map seed and the ordinal, are named in the log where each
        // was made (the run's entry; the deposit's line, deposit #n, Phase 6a.5) and
        // held by a save made during the run, a load of which replays it. The save
        // keeps what the roll did, never the roll: a report a load brings back has
        // none, and the document names no roll. TestShop's shop, worked home on a
        // run entered on FiveDeep: B's staff enchanted Vampiric (deposit 1, 35
        // cashed) and C's Tower Guard refined (deposit 2, 30 cashed).
        var campaign = TestShop.Shop();
        var back = TestShop.Counted(campaign);
        var (enchant, refine) = (back.Services![1], back.Services[2]);
        Assert.True(enchant.Finished && refine.Finished);
        Assert.Equal(new ImproveRoll(Enchanter.ImproveStream(CampaignRuns.FiveDeep, 1).NextInt(0, 2999), 3000, 90), enchant.Roll);
        Assert.Equal(new ImproveRoll(Enchanter.ImproveStream(CampaignRuns.FiveDeep, 2).NextInt(0, 2999), 3000, 180), refine.Roll);
        Assert.Equal((1073L, 2815L), (enchant.Roll!.Draw, refine.Roll!.Draw));   // the draws the log's own test pins
        Assert.Null(back.Services[0].Roll);   // stalled: no roll

        static JsonArray Reports(string text) => JsonNode.Parse(text)!["campaign"]!["lastResult"]!["services"]!.AsArray();
        string saved = SaveCodec.Write(SaveGame.Capture(campaign));
        Assert.All(Reports(saved), row => Assert.False(row!.AsObject().ContainsKey("roll")));
        var loaded = SaveGame.Restore(SaveCodec.Read(saved), GameContent.Current).Campaign;
        Assert.Equal(back.Services.Select(report => report with { Roll = null }), loaded.LastResult!.Services!);

        // A roll that landed: what it did is saved by name, and comes back.
        using var landing = TestContent.UsePremise(tuning: Landing(25));
        var improving = TestShop.Shop();
        var improved = TestShop.Counted(improving);
        Assert.All(improved.Services!.Where(report => report.Finished), report => Assert.NotNull(report.Improved));
        string text = SaveCodec.Write(SaveGame.Capture(improving));
        var rows = Reports(text);
        for (int i = 0; i < improved.Services!.Count; i++)
        {
            var did = improved.Services[i].Improved;
            Assert.Equal((did?.Kind.ToString(), did?.Modifier.ToString()),
                (rows[i]!["improved"]?.GetValue<string>(), rows[i]!["modifier"]?.GetValue<string>()));
        }
        var again = SaveGame.Restore(SaveCodec.Read(text), GameContent.Current).Campaign;
        Assert.Equal(improved.Services.Select(report => report with { Roll = null }), again.LastResult!.Services!);
        Assert.Equal(text, SaveCodec.Write(SaveGame.Capture(again)));
    }
}
