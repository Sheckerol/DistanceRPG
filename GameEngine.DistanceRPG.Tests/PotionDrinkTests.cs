using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;
using static GameEngine.DistanceRPG.Tests.TestPotions;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Drinking a potion (Phase 6b.2; §3.4): a member drinks from its own bag for
/// <see cref="TurnSystem.PotionUseCost"/> movement, in combat and out (PHASE 6b
/// DECISION 2), paid whole or refused, and the potion restores a share of a pool,
/// rounded down (PHASE 6b DECISION 3) — health through the heal applier, which
/// credits HP XP with exactly what it restored and hands the overshoot to whatever
/// banks it, mana through the mana applier's refund, which teaches nothing. No
/// quality is refused, and no share wraps the appliers' arithmetic.
/// <para>
/// The numbers are this suite's own input — a share of 20 a unit for both pools
/// and a drink at 40, §3.4's figures — stated over the numbers the worked examples
/// were written against (<see cref="Premise"/>), so a retune of the shipped file
/// fails none of it and no master number is pinned here (ShippedDataTests' R5).
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public sealed class PotionDrinkTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The share a unit of quality restores, health and mana alike: this suite's input.</summary>
    private const int Share = 20;

    /// <summary>What a drink costs: this suite's input.</summary>
    private const int Cost = 40;

    private static Tuning Drinks(Tuning t) => t with { HealthPotionPercent = Share, ManaPotionPercent = Share, PotionUseCost = Cost };

    private readonly IDisposable _premise = TestContent.UsePremise(tuning: Drinks);

    public void Dispose() => _premise.Dispose();

    /// <summary>A fixture member holding <paramref name="weaponId"/>, standing on a tile's centre, both pools grown to 100 spendable.</summary>
    private static PartyMemberState Member(string id = "A", string weaponId = "arming_sword", int c = 5)
        => TestPools.Holding(id, TestWeapons.Get(weaponId), x: c * Tile + Tile / 2f, y: 5 * Tile + Tile / 2f);

    /// <summary>A turn system for <paramref name="party"/> with one dummy far off, and a die that counts what it draws.</summary>
    private static (TurnSystem Turns, EnemyState Enemy, Func<int> Rolls) Turns(params PartyMemberState[] party)
    {
        int rolls = 0;
        var enemy = new EnemyState { X = 25 * Tile + Tile / 2f, Y = 15 * Tile + Tile / 2f };
        var turns = new TurnSystem(new int[20, 30], party, [enemy], () => { rolls++; return 10; });
        return (turns, enemy, () => rolls);
    }

    /// <summary>Every MovementSpent raised: its source and what it settled at.</summary>
    private static List<(string Source, int Spent)> MovementProbe(TurnSystem turns)
    {
        var spent = new List<(string, int)>();
        turns.Events.On<MovementPayload>(GameEvent.MovementSpent, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            spent.Add((p.Source, p.Spent));
            return p;
        });
        return spent;
    }

    [Fact]
    public void ADrinkCostsPotionUseCost_InCombatAndOut()
    {
        // §3.4: "on the same principle as swapping" — and a drink costs its 40 out
        // of combat as in it (PHASE 6b DECISION 2), where a swap is free until a
        // live enemy has been seen. Raised through MovementSpent for every drink,
        // as a swap is: the one record of every cost.
        var a = Member();
        a.Inventory[1] = Health(1);
        a.Inventory[2] = Mana(1);
        var (turns, enemy, _) = Turns(a);
        var spent = MovementProbe(turns);

        Assert.Equal((0, Cost), (turns.SwapCost, turns.PotionUseCost));
        var drink = turns.TryDrink(a, 1);
        Assert.Equal(Cost, drink!.Cost);
        Assert.Equal(Premise.Budget - Cost, a.DistLeft);

        turns.NotifyEnemyVisible(enemy, true);
        Assert.Equal((Premise.Tuning.WeaponSwapCost, Cost), (turns.SwapCost, turns.PotionUseCost));
        Assert.NotNull(turns.TryDrink(a, 2));
        Assert.Equal(Premise.Budget - 2 * Cost, a.DistLeft);
        Assert.Equal([("potion", Cost), ("potion", Cost)], spent);
        Assert.Empty(a.Potions);

        // At a cost of 0 a drink is free, and still raised: it settles at zero, as
        // a free swap does, and the applier writes nothing.
        using (TestContent.UsePremise(tuning: t => Drinks(t) with { PotionUseCost = 0 }))
        {
            var b = Member("B");
            b.Inventory[3] = Health(2);
            var (free, _, _) = Turns(b);
            var freeSpent = MovementProbe(free);
            Assert.Equal(0, free.PotionUseCost);
            Assert.Equal(0, free.TryDrink(b, 3)!.Cost);
            Assert.Equal(Premise.Budget, b.DistLeft);
            Assert.Equal([("potion", 0)], freeSpent);
        }
    }

    [Fact]
    public void AHealthPotionHealsAShareOfMaxHp_RoundedDown()
    {
        // §3.4: "a share of max HP", per unit of quality (§6.6), rounded down with
        // no minimum (PHASE 6b DECISION 3): at 20, quality 1 on 30 is 6, and on 33
        // it is 6 too (6.6); quality 3 on 33 is 19 (19.8); quality 5 is all 33, the
        // ceiling §6.6 names; quality 7 on 30 is 42, a whole pool and 12 over.
        Assert.Equal(6, Alchemist.Share(PotionType.Health, 1, 30));
        Assert.Equal(6, Alchemist.Share(PotionType.Health, 1, 33));
        Assert.Equal(19, Alchemist.Share(PotionType.Health, 3, 33));
        Assert.Equal(33, Alchemist.Share(PotionType.Health, 5, 33));
        Assert.Equal(42, Alchemist.Share(PotionType.Health, 7, 30));
        Assert.Equal(0, Alchemist.Share(PotionType.Health, 1, 4));   // 0.8: no minimum
        Assert.Equal(5, Alchemist.QualityForAll(PotionType.Health));
        Assert.Equal(5, Alchemist.QualityForAll(PotionType.Mana));

        // A potion is a share of the drinker's maximum as it stands when drunk, so it
        // rides the ladder: the same potion offers a grown member more.
        var a = Member();
        Assert.Equal(TestPools.FixtureHp, a.MaxHp);
        Assert.Equal(40, Alchemist.Share(Health(2), a));
        a.Grown(maxHp: 150);
        Assert.Equal(60, Alchemist.Share(Health(2), a));

        // Drunk at 90 of 150, quality 2 restores its 60: HP 150, the share offered and
        // the HP gained both 60.
        a.Hp = 90;
        a.Inventory[4] = Health(2);
        var (turns, _, _) = Turns(a);
        var drink = turns.TryDrink(a, 4);
        Assert.Equal(new PotionDrink(Health(2), 4, 60, 60, Cost), drink);
        Assert.Equal(150, a.Hp);
        Assert.Null(a.Inventory[4]);

        // At 30 a unit the same potion offers 90 (a share of 30 is its own number).
        using (TestContent.UsePremise(tuning: t => Drinks(t) with { HealthPotionPercent = 30 }))
            Assert.Equal((9, 4), (Alchemist.Share(PotionType.Health, 1, 33), Alchemist.QualityForAll(PotionType.Health)));
    }

    [Fact]
    public void ItHealsThroughTheHealApplier_CreditingHpXpForWhatItRestored()
    {
        // "The same call TickStatusEffects uses" (§3.5): a HealingReceived from the
        // member to itself, which caps at missing HP, restores it, tells the healed
        // feed, and credits HP XP with exactly what was applied (§2.2) — a potion is
        // a legitimate source of constitution, on the terms of getting hurt first.
        var a = Member();
        a.Hp = 75;
        a.Inventory[1] = Health(1);
        var (turns, _, _) = Turns(a);
        var heals = new List<(string Source, int Amount, int Applied, int Overflow)>();
        turns.Events.On<HealPayload>(GameEvent.HealingReceived, new HandlerPriority(9, 9), "probe", (p, s, o) =>
        {
            Assert.Same(a, s);
            Assert.Same(a, o);
            heals.Add((p.Source, p.Amount, p.Applied, p.Overflow));
            return p;
        });
        var healed = new List<int>();
        turns.CharacterHealed += (member, amount) =>
        {
            Assert.Same(a, member);
            healed.Add(amount);
        };
        var drunk = new List<(PartyMemberState, PotionDrink)>();
        turns.PotionDrunk += (member, drink) => drunk.Add((member, drink));
        int hpXp = a.HpXp, manaXp = a.ManaXp;

        var drink = turns.TryDrink(a, 1)!;

        Assert.Equal([(TurnSystem.HealthPotionSource, 20, 20, 0)], heals);
        Assert.Equal([20], healed);
        Assert.Equal(95, a.Hp);
        Assert.Equal((hpXp + 20, manaXp), (a.HpXp, a.ManaXp));
        Assert.Equal(new PotionDrink(Health(1), 1, 20, 20, Cost), drink);
        Assert.Equal([(a, drink)], drunk);
    }

    [Fact]
    public void AFullHpDrinkRestoresNothing_LearnsNothing_AndIsAllowed()
    {
        // §3.4: "a character at full health who drinks restores nothing and therefore
        // learns nothing — the potion is simply wasted". Allowed — no rule says when
        // a potion may be drunk — and paid for, and gone.
        var a = Member();
        a.Inventory[2] = Health(3);
        var (turns, _, _) = Turns(a);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);
        int hpXp = a.HpXp;

        Assert.True(turns.CanDrink(a, 2));
        var drink = turns.TryDrink(a, 2)!;

        Assert.Equal((60, 0), (drink.Share, drink.Restored));
        Assert.Equal((TestPools.FixtureHp, hpXp), (a.Hp, a.HpXp));
        Assert.Empty(healed);
        Assert.Empty(a.Potions);
        Assert.Equal(Premise.Budget - Cost, a.DistLeft);

        // A mana potion at a full pool is the same: nothing taken, and no record raised.
        a.Inventory[3] = Mana(1);
        var records = new List<ManaPayload>();
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, _, _) => { records.Add(p); return p; });
        Assert.Equal(0, turns.TryDrink(a, 3)!.Restored);
        Assert.Empty(records);
        Assert.Equal(TestPools.FixtureMana, a.Mana);
    }

    [Fact]
    public void TheOvershootIsLost_UnlessOverhealBanksItAsWard()
    {
        // §6.6: a big potion wastes its overshoot — "except to Overheal". It falls
        // out of two rules meeting, with no code of its own: what lands past full is
        // HealingAboveFull's, where an Overheal carrier banks it into the hidden pool
        // at its percentage, paying its trigger, and the pool folds it into Ward at
        // OverhealPerWard to one. Overheal teaches no HP (§3.4): the XP is what the
        // heal restored, never the overshoot.
        var plain = Member("A", "arming_sword", c: 3);
        var knife = TestWeapons.Get("flensing_knife_unique");
        var carrier = TestPools.Holding("B", knife, x: 7 * Tile + Tile / 2f, y: 5 * Tile + Tile / 2f);
        var overheal = knife.Enchantments.Single(entry => entry.Def.Effect == EffectKind.Overheal);
        foreach (var member in new[] { plain, carrier })
        {
            member.Hp = 90;
            member.Inventory[1] = Health(1);   // 20 of 100: 10 restored, 10 over
        }
        var (turns, _, _) = Turns(plain, carrier);
        var above = new List<(ActorState Self, int Overflow)>();
        turns.Events.On<HealPayload>(GameEvent.HealingAboveFull, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            above.Add((s, p.Overflow));
            return p;
        });
        int plainXp = plain.HpXp, carrierXp = carrier.HpXp, carrierMana = carrier.Mana;

        var lost = turns.TryDrink(plain, 1)!;
        var banked = turns.TryDrink(carrier, 1)!;

        Assert.Equal([((ActorState)plain, 10), (carrier, 10)], above);
        Assert.Equal((20, 10), (lost.Share, lost.Restored));
        Assert.Equal((20, 10), (banked.Share, banked.Restored));
        Assert.Equal((TestPools.FixtureHp, 0, 0, plainXp + 10), (plain.Hp, plain.StatusLevel(Ward), plain.StatusLevel(OverhealPool), plain.HpXp));

        // The carrier's ten points over grant ten levels (Overheal's percentage is
        // 100), which fold into two Ward at five to one; the trigger is paid.
        Assert.Equal(10, overheal.LevelsFor(10));
        int ward = 10 * Premise.Tuning.EffectPerLevel[OverhealPool] / Premise.Tuning.OverhealPerWard;
        Assert.Equal(2, ward);
        Assert.Equal((TestPools.FixtureHp, ward, 0, carrierXp + 10), (carrier.Hp, carrier.StatusLevel(Ward), carrier.StatusLevel(OverhealPool), carrier.HpXp));
        Assert.Equal(carrierMana - overheal.ResolvedTriggerCost(knife, 10), carrier.Mana);

        // At full, the whole share is overshoot, and Overheal banks all of it.
        carrier.Inventory[2] = Health(1);
        turns.TryDrink(carrier, 2);
        Assert.Equal((TestPools.FixtureHp, ward + 20 / Premise.Tuning.OverhealPerWard, carrierXp + 10), (carrier.Hp, carrier.StatusLevel(Ward), carrier.HpXp));
    }

    [Fact]
    public void AManaPotionRestoresAShareOfMaxMana_CappedAtTheSpendablePool_AndTeachesNothing()
    {
        // §3.4: "a share of max mana" — the pool the member has earned, MaxMana,
        // whatever its locks reserve (PHASE 6b DECISION 3) — and what it restores is
        // capped at the spendable pool, as Siphon's refund is: its route, a ManaSpent
        // record that spends nothing and only hands back. Mana XP is mana spent
        // (§2.2), so "the reload, skipped" teaches nothing.
        var a = Member("A", "staff_of_renewal");
        Assert.True(a.MaxMana > a.UsableMaxMana, "the staff's innate reserves part of the pool");
        a.Mana = 50;
        a.Inventory[1] = Mana(1);
        a.Inventory[2] = Mana(5);
        var (turns, _, _) = Turns(a);
        var records = new List<(ActorState Self, ManaPayload Record)>();
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, s, _) => { records.Add((s, p)); return p; });
        int hpXp = a.HpXp, manaXp = a.ManaXp;

        int one = a.MaxMana * Share / 100;
        var small = turns.TryDrink(a, 1)!;
        Assert.Equal((one, one), (small.Share, small.Restored));
        Assert.Equal(50 + one, a.Mana);

        // Quality 5 offers the whole earned pool, and the pool takes what is spendable.
        int room = a.UsableMaxMana - a.Mana;
        var big = turns.TryDrink(a, 2)!;
        Assert.Equal((a.MaxMana, room), (big.Share, big.Restored));
        Assert.Equal(a.UsableMaxMana, a.Mana);

        Assert.Equal(
            [((ActorState)a, new ManaPayload(0, 0, TurnSystem.ManaPotionSource, Restored: one)), (a, new ManaPayload(0, 0, TurnSystem.ManaPotionSource, Restored: room))],
            records);
        Assert.Equal((hpXp, manaXp), (a.HpXp, a.ManaXp));
    }

    [Fact]
    public void SeveralDrinksInATurn_WhileTheMovementLasts()
    {
        // §3.4: "No cooldown, no per-fight limit, no rule about when it may be
        // used": a budget of 160 buys four drinks at 40, and the fifth is refused
        // — the movement is gone, the potion kept.
        var a = Member();
        a.Hp = 1;
        for (int slot = 1; slot < a.Inventory.Length; slot++)
            a.Inventory[slot] = Health(1);
        var (turns, _, _) = Turns(a);
        int drunk = 0;
        while (turns.TryDrink(a, drunk + 1) is not null)
            drunk++;

        Assert.Equal((int)Premise.Budget / Cost, drunk);
        Assert.Equal(4, drunk);
        Assert.Equal(1 + 4 * 20, a.Hp);
        Assert.Equal(0f, a.DistLeft);
        Assert.Equal([(5, Health(1))], a.Potions.ToArray());
    }

    [Fact]
    public void NoDrinkForTheFallen_OutOfThePartysPhase_OrShortOfMovement()
    {
        // A drink is the living member's own, in the party's phase — a potion never
        // revives — from a potion in its own bag, with the whole cost to spend: at
        // 39 of a 40 cost it is refused whole, never part-paid (as a swap short of
        // its cost is). Each refusal spends nothing and keeps the potion.
        var a = Member("A", c: 3);
        var b = Member("B", c: 7);
        a.Inventory[1] = Health(2);
        a.Inventory[2] = TestWeapons.Get("hatchet");
        b.Inventory[1] = Health(2);
        var (turns, _, _) = Turns(a, b);
        var spent = MovementProbe(turns);

        // No potion there: the hand, an empty slot, a weapon's, past the bag.
        foreach (int slot in new[] { 0, 2, 3, a.Inventory.Length, -1 })
        {
            Assert.False(turns.CanDrink(a, slot), $"slot {slot}");
            Assert.Null(turns.TryDrink(a, slot));
        }

        // Short by one: refused whole.
        a.DistLeft = Cost - 1;
        Assert.False(turns.CanDrink(a, 1));
        Assert.Null(turns.TryDrink(a, 1));
        Assert.Equal((Cost - 1f, Health(2)), (a.DistLeft, a.PotionIn(1)));
        a.DistLeft = Cost;
        Assert.True(turns.CanDrink(a, 1));   // exactly the cost is enough

        // The fallen drink nothing.
        b.Hp = 0;
        b.Alive = false;
        Assert.False(turns.CanDrink(b, 1));
        Assert.Null(turns.TryDrink(b, 1));
        Assert.Equal((0, Health(2)), (b.Hp, b.PotionIn(1)));

        // Out of the party's phase, nobody drinks.
        turns.EndTurn();
        Assert.NotEqual(TurnPhase.Player, turns.Phase);
        Assert.False(turns.CanDrink(a, 1));
        Assert.Null(turns.TryDrink(a, 1));
        Assert.Equal(Health(2), a.PotionIn(1));
        Assert.Empty(spent);
    }

    [Fact]
    public void NoQualityIsRefused_AndTheShareNeverWraps()
    {
        // §6.6: no quality cap — "Overheal can use it" — so a share can be any size,
        // and is worked out wide: it saturates at the largest whole number and never
        // wraps, at any percent the file allows.
        Assert.Equal(int.MaxValue, Alchemist.ShareAt(100, int.MaxValue, int.MaxValue));
        Assert.Equal(int.MaxValue, Alchemist.ShareAt(1, ContentBounds.MostCount, int.MaxValue));
        Assert.Equal(int.MaxValue / 100, Alchemist.ShareAt(1, 1, int.MaxValue));
        Assert.Equal(ContentBounds.MostCount, Alchemist.ShareAt(100, ContentBounds.MostCount, 1));
        Assert.Equal(int.MaxValue, Alchemist.Share(PotionType.Health, ContentBounds.MostCount, 30));

        // The bound a drink hands the heal applier past full keeps the arithmetic
        // the overshoot meets whole: Overheal's levels at ApplyPercent's ceiling are
        // MostLevels at most; its trigger, scaled to them (PartialFire: the levels
        // times what it pays, a flat trigger at most), stays inside an int; and so
        // does the hidden pool's fold, the levels banked and granted times
        // EffectPerLevel at its ceiling.
        int applyCeiling = ((TableKey)TuningSchema.Find(nameof(Tuning.ApplyPercent))!).Max;
        int effectCeiling = ((TableKey)TuningSchema.Find(nameof(Tuning.EffectPerLevel))!).Max;
        Assert.True(applyCeiling * ContentBounds.MostOverflow / 100 <= ContentBounds.MostLevels);
        var knife = TestWeapons.Get("flensing_knife_unique");
        var trigger = knife.Enchantments.Single(entry => entry.Def.Effect == EffectKind.Overheal);
        Assert.True((long)ContentBounds.MostLevels * trigger.ResolvedTriggerCost(knife, ContentBounds.MostLevels) <= int.MaxValue);
        Assert.True(2L * ContentBounds.MostLevels * effectCeiling <= int.MaxValue);
        Assert.InRange(ContentBounds.MostOverflow, 100_000, 110_000);   // about 107,000 points: no play nears it

        // And a drink at the ceiling: a share of 100 a unit, quality at the sanity
        // bound, and members whose pools sit at the save's ceiling — XP at it, less a
        // pool's worth so what the drink credits stays inside it — at 1 HP and no
        // mana. A drinks with Overheal in hand, B without. Every pool is left in
        // range, and the campaign saves and restores whole. The potions are laid in
        // the bags mid-run, in place: no run's yield gives the sanity bound, so none
        // goes through TestPotions, and each is drunk before the save is written.
        using var ceiling = TestContent.UsePremise(tuning: t => Drinks(t) with { HealthPotionPercent = 100, ManaPotionPercent = 100 });
        var campaign = CampaignState.Start(GameContent.Current);
        var (a, b) = (campaign.Party[0], campaign.Party[1]);
        var carried = TestWeapons.Get("flensing_knife_unique");
        var overheal = carried.Enchantments.Single(entry => entry.Def.Effect == EffectKind.Overheal);
        a.Inventory[0] = carried;
        campaign.See(carried);
        Assert.DoesNotContain(b.EquippedWeapon!.Enchantments, entry => entry.Def.Effect == EffectKind.Overheal);
        foreach (var member in new[] { a, b })
        {
            member.HpXp = ContentBounds.MostCount;
            member.HpXp = ContentBounds.MostCount - member.MaxHp;
            member.ManaXp = ContentBounds.MostCount - 1000;   // the triggers Overheal pays are credited as spent
        }
        var visit = campaign.Enter(campaign.DefaultNext(), CampaignRuns.FiveDeep);
        var turns = visit.OpenTurns(campaign.Party, CampaignRuns.Ten);

        foreach (var member in new[] { a, b })
        {
            Assert.True(member.MaxHp > 10_000, $"{member.Id}: {member.MaxHp} is no pool at the ceiling");
            int health = Array.IndexOf(member.Inventory, null, 1);
            member.Inventory[health] = Health(ContentBounds.MostCount);
            int mana = Array.IndexOf(member.Inventory, null, health + 1);
            member.Inventory[mana] = Mana(ContentBounds.MostCount);
            member.Hp = 1;
            member.Mana = 0;

            var refill = turns.TryDrink(member, mana)!;
            Assert.Equal((int.MaxValue, member.UsableMaxMana), (refill.Share, refill.Restored));
            Assert.Equal(member.UsableMaxMana, member.Mana);

            var heal = turns.TryDrink(member, health)!;
            Assert.Equal(int.MaxValue, heal.Share);
            Assert.Equal(member.MaxHp, member.Hp);
            Assert.Equal(member.MaxHp - 1, heal.Restored);
            Assert.InRange(member.Mana, 0, member.UsableMaxMana);
            Assert.InRange(member.HpXp, 0, ContentBounds.MostCount);
            Assert.InRange(member.ManaXp, 0, ContentBounds.MostCount);
            Assert.InRange(member.StatusLevel(Ward), 0, ContentBounds.MostLevels);
            Assert.InRange(member.StatusLevel(OverhealPool), 0, ContentBounds.MostLevels);
        }

        // A banks the overshoot the drink handed it, MostOverflow, as Ward: its
        // levels at Overheal's percentage, folded at OverhealPerWard to one. B's is lost.
        long levels = overheal.LevelsFor((int)ContentBounds.MostOverflow);
        Assert.Equal(levels * GameContent.Current.Tuning.EffectPerLevel[OverhealPool] / GameContent.Current.Tuning.OverhealPerWard, a.StatusLevel(Ward));
        Assert.True(a.StatusLevel(Ward) > 0);
        Assert.Equal((0, 0), (b.StatusLevel(Ward), b.StatusLevel(OverhealPool)));

        string text = SaveCodec.Write(SaveGame.Capture(campaign));
        var restored = SaveGame.Restore(SaveCodec.Read(text), GameContent.Current).Campaign;
        StateAssert.Equivalent(campaign.Party, restored.Party, "party");
        restored.Visit!.OpenTurns(restored.Party);   // opened as the game's resume opens it
        Assert.Equal(text, SaveCodec.Write(SaveGame.Capture(restored)));
    }

    [Fact]
    public void QualityForAllHealsCompletelyFromOne_AtEveryPercent()
    {
        // §6.6's ceiling, "a potion that heals you completely from 1 HP", is
        // ceil(100 / percent) units: the least quality whose share is the whole pool,
        // at every percent the file allows and every maximum — rounding down never
        // weakens it, since percent × quality is then 100 or more.
        int[] maxima = [1, 2, 3, 7, 30, 33, 99, 100, 101, 997, 46_341, int.MaxValue / 100];
        for (int percent = 1; percent <= 100; percent++)
        {
            int quality = Alchemist.QualityForAllAt(percent);
            Assert.True(percent * quality >= 100, $"{percent}");
            Assert.True(quality == 1 || percent * (quality - 1) < 100, $"{percent}: {quality} is not the least");
            foreach (int max in maxima)
            {
                Assert.True(Alchemist.ShareAt(percent, quality, max) >= max, $"{percent} percent, quality {quality}, max {max}");
                if (quality > 1 && max >= 1)
                    Assert.True(Alchemist.ShareAt(percent, quality - 1, max) < max, $"{percent} percent, quality {quality - 1}, max {max}");
            }
        }

        // Read through the tuning, and drunk: at 20, 33 and 100 a unit, a member at 1
        // HP drinks the quality for all and is whole.
        foreach (int percent in new[] { 20, 33, 100 })
        {
            using var _ = TestContent.UsePremise(tuning: t => Drinks(t) with { HealthPotionPercent = percent });
            int quality = Alchemist.QualityForAll(PotionType.Health);
            Assert.Equal(Alchemist.QualityForAllAt(percent), quality);
            var a = Member();
            a.Hp = 1;
            a.Inventory[1] = Health(quality);
            var (turns, _, _) = Turns(a);
            Assert.Equal(a.MaxHp - 1, turns.TryDrink(a, 1)!.Restored);
            Assert.Equal(a.MaxHp, a.Hp);
        }
    }

    [Fact]
    public void ADrinkDrawsNoDie()
    {
        // Phase 6b draws no randomness: a drink is a cost and a share, and the
        // visit's dice stream — what a resume replays from — is where it was.
        var campaign = CampaignState.Start(GameContent.Current);
        Carrying(campaign, "A", (2, Health(2)), (3, Mana(1)));
        var visit = campaign.Enter(campaign.DefaultNext(), CampaignRuns.FiveDeep);
        var turns = visit.OpenTurns(campaign.Party);   // the visit's own die
        var a = campaign.Party[0];
        a.Hp -= 10;
        a.Mana -= 5;
        int rolled = visit.DiceRolled;

        Assert.Equal(10, turns.TryDrink(a, 2)!.Restored);   // 12 of 30 offered, 10 missing
        Assert.Equal(5, turns.TryDrink(a, 3)!.Restored);    // 6 of 30 offered, 5 missing
        Assert.Equal(rolled, visit.DiceRolled);

        // And a turn system's own die is never asked.
        var b = Member("B");
        b.Hp = 50;
        b.Inventory[1] = Health(1);
        var (own, _, rolls) = Turns(b);
        Assert.NotNull(own.TryDrink(b, 1));
        Assert.Equal(0, rolls());
    }
}
