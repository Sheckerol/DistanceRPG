using System.Collections.Immutable;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Section 3.3's "tier is earned by use": the mana an enchantment spends is that
/// enchantment's experience, credited where the mana moves, against the entry
/// that moved it and at the wielder's INT. The payload carried a total before
/// this sub-step; what is new is that the total is attributed -- one
/// <see cref="EnchantmentPayment"/> per entry as it fires -- and that the five
/// appliers that write a mana record credit the entries that paid for it.
/// <para>
/// One number and two ladders: max mana is credited the spend, and the entries
/// are credited their shares of the same spend, so nothing is counted twice and
/// nothing is counted at two different figures.
/// </para>
/// </summary>
public class EnchantmentXpTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    /// <summary>
    /// The section 3.3 Arcane row stood up as a def, as the ladder and lock
    /// files already do: lock 30, trigger 8, <see cref="Tuning.ArcanePotency"/>
    /// at <c>ApplyPercent</c> 100. The entry itself is a later sub-step's; its
    /// kind is the closest shipped shape that fires on a landed hit and pays a
    /// flat trigger, which is what lets the budget be watched running out.
    /// </summary>
    private static EnchantmentDef Arcane => new(
        Id: "arcane", Name: "Arcane", Effect: EffectKind.Vampiric, Targets: TargetSide.Enemy,
        Lock: 30, Trigger: 8,
        Potency: GameContent.Current.Tuning.ArcanePotency, ApplyPercent: 100,
        Applies: null, DamageType: null, Unique: false);

    /// <summary>A dummy with enough HP that nothing here kills it by accident, unless the scenario is a kill.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 100_000)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? TestWeapons.Make("Fists", 40, 1, 0), Hp = hp };
    }

    /// <summary>An ad-hoc dagger carrying the given entry instances in that attachment order.</summary>
    private static Weapon Carrying(string name, params Enchantment[] entries)
    {
        var def = new WeaponDef(
            Id: "test_" + name.ToLowerInvariant().Replace(' ', '_'), Name: name, Class: WeaponClass.Dagger, Role: null,
            Range: 40, Damage: 15, Cost: 30, ManaCost: 0,
            Forged: new Dictionary<ModifierType, int>(),
            Enchantments: entries.Select(e => new EnchantmentRef(e.Id, e.Tier)).ToList(),
            Shape: null, Unique: false, DerivedFrom: null);
        return new Weapon(def, entries);
    }

    /// <summary>The same, from catalogue ids at the given tiers.</summary>
    private static Weapon Souled(string name, params (string Id, int Tier)[] entries)
    {
        var catalogue = GameContent.Current.Enchantments;
        return Carrying(name, entries.Select(e => new Enchantment(catalogue[e.Id], e.Tier)).ToArray());
    }

    /// <summary>A fixture member holding <paramref name="weapon"/>, grown until its spendable pool is the suite's.</summary>
    private static PartyMemberState Wielding(string id, Weapon weapon, int r = 5, int c = 5, InnateStats? stats = null)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, weapon, x: x, y: y, stats: stats);
    }

    /// <summary>Record every ManaSpent the table settles: who paid, what was wanted and paid.</summary>
    private static List<(ActorState Self, int Wanted, int Spent)> ManaProbe(TurnSystem turns)
    {
        var spent = new List<(ActorState, int, int)>();
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            spent.Add((s, p.Wanted, p.Spent));
            return p;
        });
        return spent;
    }

    private static (int Tier, int Xp) Entry(Weapon weapon, int index) => (weapon.Enchantments[index].Tier, weapon.Enchantments[index].Xp);

    private static int[] Xps(Weapon weapon) => weapon.Enchantments.Select(e => e.Xp).ToArray();

    [Fact]
    public void AnEntryIsCreditedTheManaItActuallyPaid()
    {
        // "Mana is an enchantment's experience. Every trigger it pays for feeds
        // its own pool." Pays for -- not asks for. An entry that ran out
        // mid-list fires at the fraction it could pay and is charged for exactly
        // that, so the credit is the payment and never the price.
        var grid = new int[20, 20];
        var knife = Carrying("Arcane Knife", new Enchantment(Arcane, Tier: 1));
        var member = Wielding("A", knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { member }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);

        member.Hp = 1;                                   // room for the drink to land whole
        Assert.Equal(8, knife.Enchantments[0].ResolvedTriggerCost(knife, knife.Enchantments[0].LevelsFor(Arcane.Potency)));
        Assert.Equal((1, 0), Entry(knife, 0));

        // Whole: the pool covers the eight it wants, and eight is what it banks.
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((1, 8), Entry(knife, 0));
        Assert.Equal(new[] { 4 }, healed);               // potency 4 at tier 1

        // Partial: five left of the trigger's eight buys five eighths of the
        // effect for five mana, and five is the credit. The bar it is climbing
        // is thirty, so nothing about the tier hides the difference.
        member.Mana = 5;
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((1, 13), Entry(knife, 0));
        Assert.Equal(new[] { 4, 2 }, healed);            // 4 x 5 / 8
        Assert.Equal(30, knife.Enchantments[0].XpToNextTier(InnateStats.Low));
        Assert.Equal(new[] { 8, 5 }, spent.Select(s => s.Spent).ToArray());

        // Nothing at all: a pool that cannot buy a scrap of the effect is the
        // non-event the section 3.1 rule already names, and a non-event teaches
        // nothing.
        member.Mana = 1;
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((1, 13), Entry(knife, 0));
        Assert.Equal(new[] { 4, 2 }, healed);
        Assert.Equal(2, spent.Count);
    }

    [Fact]
    public void TheSameSpendFeedsBothLadders()
    {
        // "The same manaSpent credit that already grows max mana, counted a
        // second time against the thing that spent it. One number, two ladders,
        // no second definition." A staff's cast is the clearest case: one record
        // of fourteen -- thirteen of cast and one of trigger -- and the entry is
        // credited the one it paid while the pool is credited all fourteen.
        var grid = new int[20, 20];
        var a = Wielding("A", TestWeapons.Get("staff_of_renewal"));
        var (bx, by) = At(5, 6);
        var b = TestPools.Char("B", x: bx, y: by);
        var turns = new TurnSystem(grid, new[] { a, b }, Array.Empty<EnemyState>(), () => 10);
        var spent = ManaProbe(turns);

        var staff = a.EquippedWeapon!;
        int manaXp = a.ManaXp;
        Assert.Equal((1, 0), Entry(staff, 0));

        Assert.True(turns.TryCast(a, b));

        Assert.Equal(((ActorState)a, 14, 14), Assert.Single(spent));
        Assert.Equal(14, a.ManaXp - manaXp);              // the whole record grows the pool
        Assert.Equal((1, 1), Entry(staff, 0));            // the trigger's one point grows the entry

        // And a second cast banks a second point against a bar of fifteen -- the
        // entry's own lock at its own tier, which is the only cost a tier has.
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(28, a.ManaXp - manaXp);
        Assert.Equal((1, 2), Entry(staff, 0));
        Assert.Equal(15, staff.Enchantments[0].XpToNextTier(InnateStats.Low));
    }

    [Fact]
    public void AnOverdrawnSpendCreditsBothLaddersTheSameNumber()
    {
        // ManaPayload's own doc comment says enchantment XP is counted on Spent,
        // the mana actually paid, and a record never overdraws the pool. So when
        // a record comes up short of what it wanted, the per-entry credits are
        // scaled by Spent / Wanted: the two ladders count one number, and the
        // shipped sentence stays true.
        var grid = new int[20, 20];

        // Whole first, which is every shipped case: the record is the entries'
        // payments and nothing else, so each is credited exactly what it paid
        // and the credits sum to Spent. The scaling is invisible here, and that
        // is the point of stating it.
        var knife = TestWeapons.Get("flensing_knife_unique");
        var a = Wielding("A", knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);
        int manaXp = a.ManaXp;

        Assert.True(turns.TryAttack(a, dummy));

        // Two records: the hit's souls (serrated 2, vampiric 2) and the surplus
        // healing's Overheal (8). Each is covered in full, so each entry's
        // credit is its payment, and the two ladders read the same twelve.
        Assert.Equal(new[] { (4, 4), (8, 8) }, spent.Select(s => (s.Wanted, s.Spent)).ToArray());
        Assert.Equal(new[] { 2, 2, 8 }, Xps(knife));
        Assert.Equal(12, a.ManaXp - manaXp);
        Assert.Equal(12, Xps(knife).Sum());

        // Short second. No shipped path settles a record with payments in it
        // that the pool cannot cover -- every loop subtracts what is already
        // claimed before an entry fires, a cast's own mana included -- so the
        // claim the pool falls short of is made here by a handler after the
        // loop, which is exactly the shape a cast's mana has: part of the record
        // that belongs to no entry.
        var second = TestWeapons.Get("flensing_knife_unique");
        var b = Wielding("B", second);
        var other = Enemy(5, 6);
        var shortfall = new TurnSystem(grid, new[] { b }, new[] { other }, () => 10);
        var shortSpends = ManaProbe(shortfall);
        shortfall.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 8), "claim",
            (p, _, _) => p with { ManaToSpend = p.ManaToSpend + 4 });
        b.Mana = 4;
        int bManaXp = b.ManaXp;

        Assert.True(shortfall.TryAttack(b, other));

        // Wanted eight, paid four: each entry is credited half of what it paid,
        // the remainder of the halving going to the last entry that paid, and
        // the part of the record that was nobody's keeps its own half. Nothing
        // is credited mana the pool never paid.
        Assert.Equal((8, 4), Assert.Single(shortSpends.Select(s => (s.Wanted, s.Spent))));
        Assert.Equal(new[] { 1, 1, 0 }, Xps(second));
        Assert.Equal(4, b.ManaXp - bManaXp);
        Assert.Equal(0, b.Mana);
        Assert.Equal(4 * 4 / 8, Xps(second).Sum());
    }

    [Fact]
    public void CarryingItIsNotEnough()
    {
        // "An unfired enchantment has nothing driven through it, so there is
        // nothing to deepen." Two members, the same leech in each hand; one
        // swings and one walks.
        var grid = new int[20, 20];
        var swinging = Souled("Leech", ("vampiric", 1));
        var carried = Souled("Leech", ("vampiric", 1));
        var a = Wielding("A", swinging, 5, 5);
        var b = Wielding("B", carried, 7, 5);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { dummy }, () => 10);

        Assert.True(turns.TryAttack(a, dummy));
        (b.X, b.Y) = At(7, 6);                            // it walked; nothing was driven through the circle

        Assert.Equal((1, 2), Entry(swinging, 0));
        Assert.Equal((1, 0), Entry(carried, 0));

        // Carrying it through a whole round changes nothing either: the credit
        // rides the payment, so there is no path to a tier that does not go
        // through firing.
        turns.EndTurn();
        for (int step = 0; step < 3000 && turns.Phase != TurnPhase.Player; step++)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal((1, 0), Entry(carried, 0));
    }

    [Theory]
    [MemberData(nameof(ClimbingEntries))]
    public void EveryCatalogueEntryCanClimb(string id)
    {
        // "Every catalogue enchantment can level, because every trigger costs
        // something. There is no enchantment in the game that fires for free, so
        // there is none that cannot climb." The theory is over the entries that
        // fire through one of the five loops and are not unique -- a unique is
        // pinned at tier 1 by design (below), and an entry outside the loops has
        // no payment to ride.
        var grid = new int[20, 20];
        var def = GameContent.Current.Enchantments[id];
        var weapon = Carrying("Proving " + id, new Enchantment(def, Tier: 1));
        var member = Wielding("A", weapon);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { member }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);
        member.Hp = 1;                                    // a drink has somewhere to land

        for (int fired = 0; fired < 200 && weapon.Enchantments[0].Tier == 1; fired++)
        {
            member.Mana = member.UsableMaxMana;
            member.DistLeft = GameConstants.MaxDistance;
            Fire(turns, member, dummy, def, weapon);
        }

        Assert.True(spent.Count > 0, $"'{id}' fired without paying anything.");
        Assert.All(spent, s => Assert.True(s.Spent > 0, $"'{id}' fired free."));
        Assert.True(weapon.Enchantments[0].Tier > 1, $"'{id}' never left tier 1.");
        Assert.Equal(spent.Sum(s => s.Spent), TotalCredited(weapon.Enchantments[0], InnateStats.Low));
    }

    /// <summary>
    /// Fire <paramref name="def"/>'s entry once, through the event its kind has
    /// a behaviour on. A cast-firing kind is raised as a cast at no cast mana of
    /// its own, so the record is the trigger alone; everything else rides a
    /// swing. Nothing here decides what an entry costs -- the loop and the
    /// behaviour do, exactly as in play.
    /// </summary>
    private static void Fire(TurnSystem turns, PartyMemberState member, EnemyState dummy, EnchantmentDef def, Weapon weapon)
    {
        if (EnchantmentBehaviours.FiresOn(GameEvent.Cast, def.Effect))
        {
            var entry = weapon.Enchantments[0];
            ActorState target = def.Targets == TargetSide.Ally ? member : dummy;
            turns.Events.Raise(GameEvent.Cast,
                new CastPayload(weapon, target, Roll: 10, IsCrit: false, IsFumble: false,
                    Levels: entry.LevelsFor(def.Potency), ManaCost: 0), member, target);
            return;
        }
        Assert.True(turns.TryAttack(member, dummy));
    }

    /// <summary>
    /// The mana an entry has been credited in total: the bars it has crossed
    /// plus the remainder into the tier it is on. Replayed off the entry rather
    /// than counted at the call sites, so the theory compares what was spent
    /// against what the ladder actually banked.
    /// </summary>
    private static int TotalCredited(Enchantment entry, int stat)
    {
        int total = entry.Xp;
        var walk = new Enchantment(entry.Def, Tier: 1);
        while (walk.Tier < entry.Tier)
        {
            total += Math.Max(1, walk.XpToNextTier(stat));
            walk = walk with { Tier = walk.Tier + 1 };
        }
        return total;
    }

    /// <summary>
    /// The catalogue entries a wielder can climb: not unique (a unique is pinned
    /// at tier 1 forever, and the pin is the design rather than a gap), and with
    /// a behaviour in the table of one of the five events a loop runs on --
    /// which is what attributes a payment, and therefore what a credit rides.
    /// An entry outside the loops (a compiled defender handler) and an entry
    /// declared with no behaviour are both excluded by that filter rather than
    /// by a list anyone has to maintain.
    /// </summary>
    public static TheoryData<string> ClimbingEntries()
    {
        var loops = new[] { GameEvent.Cast, GameEvent.DamageTaken, GameEvent.DamageDealt, GameEvent.Killed, GameEvent.HealingAboveFull };
        var data = new TheoryData<string>();
        foreach (var def in GameContent.Current.Enchantments.All)
            if (!def.Unique && loops.Any(evt => EnchantmentBehaviours.FiresOn(evt, def.Effect)))
                data.Add(def.Id);
        return data;
    }

    [Fact]
    public void AUniqueAccruesAndStaysAtTierOne()
    {
        // "They still accrue mana spent, since every trigger still costs; it
        // simply buys nothing." Not a dead stack: the same spend still grows the
        // wielder's max mana, which is the other ladder the number feeds.
        var grid = new int[20, 20];
        var knife = TestWeapons.Get("flensing_knife_unique");
        var a = Wielding("A", knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);
        int manaXp = a.ManaXp;

        Assert.Equal(new[] { true, false, true }, knife.Enchantments.Select(e => e.Unique).ToArray());

        for (int swing = 0; swing < 4; swing++)
        {
            a.DistLeft = GameConstants.MaxDistance;
            Assert.True(turns.TryAttack(a, dummy));
        }

        // Serrated and Overheal bank four swings' worth and stay where the flag
        // pins them; Vampiric, a catalogue entry, banks the same way and would
        // climb given enough of it.
        Assert.Equal(new[] { 8, 8, 32 }, Xps(knife));
        Assert.Equal(new[] { Enchantment.UniqueTier, 3, Enchantment.UniqueTier }, knife.Enchantments.Select(e => e.Tier).ToArray());
        Assert.Equal(48, a.ManaXp - manaXp);              // and every point of it still bought pool

        // The lock is the tier's, so a pinned tier is a pinned reservation too:
        // a unique never grows more expensive to carry by being used.
        Assert.Equal(new[] { 20, 60, 25 }, knife.Enchantments.Select(e => e.EffectiveLock).ToArray());
    }

    [Fact]
    public void IntScalesPotencyThroughTheLadder()
    {
        // Section 3.3 says enchantment potency scales with INT; section 2.1 says
        // a stat divides a threshold and never multiplies a value. Both hold at
        // once because INT divides the tier's bar: the same spend buys a wizard
        // more depth, and depth is what lifts potency. No rate multiplier exists
        // anywhere -- the two wielders are credited the identical mana.
        var grid = new int[20, 20];
        var wizardKnife = Souled("Leech", ("vampiric", 1));
        var fighterKnife = Souled("Leech", ("vampiric", 1));
        var wizard = Wielding("A", wizardKnife, 5, 5, new InnateStats(STR: 1, DEX: 2, CON: 3, INT: 4));
        var fighter = Wielding("B", fighterKnife, 7, 5, new InnateStats(STR: 4, DEX: 3, CON: 2, INT: 1));
        var wizardDummy = Enemy(5, 6);
        var fighterDummy = Enemy(7, 6);
        var turns = new TurnSystem(grid, new[] { wizard, fighter }, new[] { wizardDummy, fighterDummy }, () => 10);
        var spent = ManaProbe(turns);
        wizard.Hp = 1;
        fighter.Hp = 1;

        for (int swing = 0; swing < 10; swing++)
        {
            wizard.DistLeft = GameConstants.MaxDistance;
            fighter.DistLeft = GameConstants.MaxDistance;
            Assert.True(turns.TryAttack(wizard, wizardDummy));
            Assert.True(turns.TryAttack(fighter, fighterDummy));
        }

        // Twenty mana each, to the point.
        Assert.Equal(20, spent.Where(s => s.Self == wizard).Sum(s => s.Spent));
        Assert.Equal(20, spent.Where(s => s.Self == fighter).Sum(s => s.Spent));

        // The wizard's bars were 5, 10 and 15; the fighter's first was 20.
        Assert.Equal((3, 5), Entry(wizardKnife, 0));
        Assert.Equal((2, 0), Entry(fighterKnife, 0));

        // And so the same entry, fired the same number of times, is a deeper
        // circle in one hand than the other -- which is the whole of "potency
        // scales with INT", with the scaling living in the ladder.
        var deep = wizardKnife.Enchantments[0];
        var shallow = fighterKnife.Enchantments[0];
        Assert.Equal((3, 60), (deep.LevelsFor(deep.Def.Potency), deep.EffectiveLock));
        Assert.Equal((2, 40), (shallow.LevelsFor(shallow.Def.Potency), shallow.EffectiveLock));
    }

    [Fact]
    public void ThePaymentIsAttributedToTheEntryThatMadeIt()
    {
        // The credit is "against the thing that spent it", which needs the index
        // and not a total: two entries on one weapon, only one of which fires on
        // a swing, and only that one's pool moves. The index is a position in
        // the wielder's current weapon -- the one the loop walked -- and nothing
        // can swap between the chain and the applier, because the applier runs
        // inside the raise.
        var grid = new int[20, 20];
        var knife = Souled("Tainted Leech", ("poison", 1), ("vampiric", 1));
        var member = Wielding("A", knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { member }, new[] { dummy }, () => 10);
        var payments = new List<EnchantmentPayment[]>();
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 8), "probe", (p, _, _) =>
        {
            payments.Add(p.Payments.IsDefault ? Array.Empty<EnchantmentPayment>() : p.Payments.ToArray());
            return p;
        });
        member.Hp = 1;

        Assert.False(member.IsDormant(0));
        Assert.False(member.IsDormant(1));
        Assert.True(turns.TryAttack(member, dummy));

        // Poison casts and never rides a swing, so the swing's one payment names
        // index 1 and the credit lands there.
        Assert.Equal(new[] { new EnchantmentPayment(1, 2) }, Assert.Single(payments));
        Assert.Equal(new[] { 0, 2 }, Xps(knife));

        // The hit itself carried no payment at all: no shipped kind pays at step
        // 4, so the attribution the hit's applier reads is empty and the credit
        // it makes is none. Cleared on the way to DamageDealt, so the entries
        // that fire there are not charged with the hit's list a second time.
        var onHit = new List<int>();
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 8), "hit",
            (p, _, _) => { onHit.Add(p.Payments.IsDefaultOrEmpty ? 0 : p.Payments.Length); return p; });
        member.DistLeft = GameConstants.MaxDistance;
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal(new[] { 0 }, onHit.ToArray());
        Assert.Equal(new[] { 0, 4 }, Xps(knife));
    }

    [Fact]
    public void CreditsRideEveryEventTheLoopRunsOn()
    {
        // A credit is not a rule of the cast path or of the hit path: it is what
        // every applier that writes a mana record does with the attribution its
        // loop settled. All five, one at a time.
        var grid = new int[20, 20];

        // Cast: a staff's innate, paid out of what the cast left.
        var staff = TestWeapons.Get("staff_of_renewal");
        var caster = Wielding("A", staff, 5, 5);
        var (bx, by) = At(5, 6);
        var ally = TestPools.Char("B", x: bx, y: by);
        var casting = new TurnSystem(grid, new[] { caster, ally }, Array.Empty<EnemyState>(), () => 10);
        Assert.True(casting.TryCast(caster, ally));
        Assert.Equal((1, 1), Entry(staff, 0));

        // DamageTaken, step 4: no shipped kind pays here -- an element's trigger
        // is its cast's -- so the event is driven with a hit that already
        // carries a payment, which is what the Arcane entry of the next
        // sub-step will settle. The entry on the weapon casts and so fires on
        // nothing a swing raises, leaving the injected payment the only credit.
        var quiet = Souled("Quiet Knife", ("regeneration", 1));
        var struck = Wielding("C", quiet, 5, 5);
        var target = Enemy(5, 6);
        var hitting = new TurnSystem(grid, new[] { struck }, new[] { target }, () => 10);
        int mana = struck.Mana;
        hitting.Events.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(quiet, roll: 10, CombatRules.SurfaceDistanceUnits(struck, target))
                with { ManaToSpend = 3, Payments = ImmutableArray.Create(new EnchantmentPayment(0, 3)) },
            struck, target);
        Assert.Equal((1, 3), Entry(quiet, 0));
        Assert.Equal(mana - 3, struck.Mana);

        // DamageDealt: what rides a landed hit.
        var leech = Souled("Leech", ("vampiric", 1));
        var drinker = Wielding("D", leech, 5, 5);
        var bled = Enemy(5, 6);
        var drinking = new TurnSystem(grid, new[] { drinker }, new[] { bled }, () => 10);
        drinker.Hp = 1;
        Assert.True(drinking.TryAttack(drinker, bled));
        Assert.Equal((1, 2), Entry(leech, 0));

        // Killed: Siphon's refund costs five before it hands fifteen back, and
        // the five is its experience like any other.
        var reaper = Souled("Reaper", ("siphon", 1));
        var killer = Wielding("E", reaper, 5, 5);
        var doomed = Enemy(5, 6, hp: 1);
        var killing = new TurnSystem(grid, new[] { killer }, new[] { doomed }, () => 10);
        Assert.True(killing.TryAttack(killer, doomed));
        Assert.False(doomed.Alive);
        Assert.Equal((Enchantment.UniqueTier, 5), Entry(reaper, 0));

        // HealingAboveFull: Overheal banks the surplus for eight, and the eight
        // is credited against the healed actor's own weapon -- the payload names
        // no weapon, only a source string.
        var knife = TestWeapons.Get("flensing_knife_unique");
        var full = Wielding("F", knife, 5, 5);
        var struckBy = Enemy(5, 6);
        var banking = new TurnSystem(grid, new[] { full }, new[] { struckBy }, () => 10);
        Assert.Equal(full.MaxHp, full.Hp);
        Assert.True(banking.TryAttack(full, struckBy));
        Assert.Equal((Enchantment.UniqueTier, 8), Entry(knife, 2));
    }
}
