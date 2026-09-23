using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §3.1/§3.3's mana lock as a budget: what an equipped entry reserves, what the
/// remainder of the pool is, and what happens to the entries the pool could not
/// cover. Equipping never rejects — "a weapon you cannot afford is not a weapon
/// you cannot use" — so the tail of the list sleeps instead, which is the same
/// non-event as a trigger the wielder cannot pay for, and it wakes on its own
/// the moment the pool grows to cover it.
/// <para>
/// The fixtures here deliberately do <em>not</em> come through
/// <see cref="TestPools.Holding"/>: that grows a member past its locks so the
/// rest of the suite's trigger arithmetic stands, and the pool being too small
/// is the whole subject of this file.
/// </para>
/// </summary>
public class EnchantmentLockTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    /// <summary>
    /// A fixture member at the suite's earned pool with <paramref name="weapon"/>
    /// in hand and its pool clamped to what is actually spendable — the same
    /// clamp <see cref="TurnSystem.NotifyWeaponChanged"/> applies on a swap, done
    /// by hand because these fixtures put a weapon straight into the slot.
    /// </summary>
    private static PartyMemberState Wielding(string id, Weapon weapon, int r = 5, int c = 5)
    {
        var (x, y) = At(r, c);
        var member = TestPools.Char(id, x: x, y: y);
        member.Inventory[0] = weapon;
        member.Mana = member.UsableMaxMana;
        return member;
    }

    /// <summary>A dummy on a tile's centre with fists unless told otherwise, and enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? TestWeapons.Make("Fists", 40, 1, 0), Hp = hp };
    }

    /// <summary>An ad-hoc dagger carrying catalogue entries at the given tiers, in that attachment order.</summary>
    private static Weapon Souled(string name, params (string Id, int Tier)[] entries)
    {
        var catalogue = GameContent.Current.Enchantments;
        return Carrying(name, entries.Select(e => new Enchantment(catalogue[e.Id], e.Tier)).ToArray());
    }

    /// <summary>The same, from entry instances the caller holds on to: what the shared-instance assertions need.</summary>
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

    /// <summary>
    /// The §3.3 Arcane row, stood up as a def because the entry itself is a later
    /// sub-step's: lock 30, trigger 8, <see cref="Tuning.ArcanePotency"/> at
    /// <c>ApplyPercent</c> 100. Its kind is the closest shipped shape that fires
    /// on a landed hit and pays a flat trigger, so the budget can be watched
    /// running out.
    /// </summary>
    private static EnchantmentDef Arcane => new(
        Id: "arcane", Name: "Arcane", Effect: EffectKind.Vampiric, Targets: TargetSide.Enemy,
        Lock: 30, Trigger: 8,
        Potency: GameContent.Current.Tuning.ArcanePotency, ApplyPercent: 100,
        Applies: null, DamageType: null, Unique: false);

    /// <summary>Record every ManaSpent the table settles: who paid, what was wanted and paid, and through what.</summary>
    private static List<(ActorState Self, int Wanted, int Spent, string Source)> ManaProbe(TurnSystem turns)
    {
        var spent = new List<(ActorState, int, int, string)>();
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            spent.Add((s, p.Wanted, p.Spent, p.Source));
            return p;
        });
        return spent;
    }

    private static int Distance(ActorState a, ActorState b) => CombatRules.SurfaceDistanceUnits(a, b);

    [Fact]
    public void AnEquippedEntryReservesItsLock()
    {
        // "Max mana reserved while equipped; returned in full on unequip." The
        // reservation is the entry's EffectiveLock -- Lock x Tier -- so depth is
        // what costs, and the pool every spend reads is the remainder.
        var member = TestPools.Char("A");
        Assert.Equal((100, 0, 100), (member.MaxMana, member.PaidLocks, member.UsableMaxMana));

        var leech = Souled("Deep Leech", ("vampiric", 3));
        Assert.Equal(60, leech.TotalLock);                       // 20 x 3: the item's own figure, wielder or none
        Assert.Equal(20, Souled("Leech", ("vampiric", 1)).TotalLock);

        member.Inventory[0] = leech;
        Assert.Equal((100, 60, 40), (member.MaxMana, member.PaidLocks, member.UsableMaxMana));
        Assert.False(member.IsDormant(0));

        // Unequipping returns the ceiling in full and refunds no points: the lock
        // was reserved out of the pool, never taken from it.
        member.Inventory[0] = null;
        Assert.Equal((100, 0, 100), (member.MaxMana, member.PaidLocks, member.UsableMaxMana));

        // Through the swap the turn system knows about, the pool itself is
        // clamped: a wielder taking up a locked weapon cannot go on holding mana
        // the new reservation has taken. Putting it down is not a refill.
        var (x, y) = At(5, 5);
        var swapper = TestPools.Char("B", x: x, y: y);
        swapper.Inventory[0] = TestWeapons.Get("weakspot_stiletto");
        swapper.Inventory[1] = leech;
        var turns = new TurnSystem(new int[20, 20], new[] { swapper }, Array.Empty<EnemyState>(), () => 10);
        Assert.Equal(100, swapper.Mana);

        Assert.True(turns.TrySwap(swapper, 1));
        Assert.Equal((60, 40, 40), (swapper.PaidLocks, swapper.UsableMaxMana, swapper.Mana));

        Assert.True(turns.TrySwap(swapper, 1));
        Assert.Equal((0, 100, 40), (swapper.PaidLocks, swapper.UsableMaxMana, swapper.Mana));
    }

    [Fact]
    public void EquippingNeverRejects()
    {
        // "A staff dropping with a 30-lock enchantment would otherwise be
        // unequippable by a fighter with a small pool. Instead the lock goes
        // unpaid and the enchantment sits dormant." Nothing throws, nothing is
        // refused, and nothing is half-paid: a lock is whole or it is asleep.
        var member = TestPools.Char("A");
        var warstaff = Souled("Warstaff", ("mire", 5));           // 25 x 5 = 125, against a pool of 100
        member.Inventory[0] = warstaff;

        Assert.Same(warstaff, member.EquippedWeapon);
        Assert.True(member.IsDormant(0));
        Assert.Equal((0, 100), (member.PaidLocks, member.UsableMaxMana));
        Assert.Equal(125, warstaff.TotalLock);                    // what it would reserve; what it does reserve is 0

        // The doc's own example number: the 30 of the Sturdy/Arcane row. Alone it
        // fits a hundred-point pool, so what makes it dormant is what is left of
        // the pool when it is reached -- 25 here, behind 75 of Mire.
        Assert.Equal(30, GameContent.Current.Enchantments["sturdy"].Lock);
        var heavy = Wielding("B", Souled("Heavy Bulwark", ("mire", 3), ("sturdy", 1)));
        Assert.Equal((75, 25), (heavy.PaidLocks, heavy.UsableMaxMana));
        Assert.Equal((false, true), (heavy.IsDormant(0), heavy.IsDormant(1)));
    }

    [Fact]
    public void ADormantEntryDoesNotFire_AndPaysNothing()
    {
        // "The same non-event as a trigger you cannot afford": the entry is
        // skipped, nothing happens and nothing is charged -- while the pool still
        // has mana in it, which is what separates dormancy from being broke.
        var grid = new int[20, 20];
        var weapon = Souled("Warded Leech", ("mire", 3), ("vampiric", 2));   // 75 paid, 40 more than the 25 left
        var asleep = Wielding("A", weapon);
        asleep.Hp = 50;
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { asleep }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);

        Assert.True(asleep.IsDormant(1));
        Assert.True(turns.TryAttack(asleep, dummy));
        Assert.Equal((50, 25), (asleep.Hp, asleep.Mana));   // no drink, and the 25 it had is untouched
        Assert.Empty(spent);

        // The identical weapon on a pool that covers both entries: the same entry
        // fires, so what was missing was the lock and nothing else.
        var awake = TestPools.Holding("B", Souled("Warded Leech", ("mire", 3), ("vampiric", 2)), x: asleep.X, y: asleep.Y);
        awake.Hp = 50;
        var other = Enemy(5, 6);
        var paid = new TurnSystem(grid, new[] { awake }, new[] { other }, () => 10);
        var paidSpends = ManaProbe(paid);

        Assert.False(awake.IsDormant(1));
        Assert.True(paid.TryAttack(awake, other));
        Assert.Equal((52, TestPools.FixtureMana - 2), (awake.Hp, awake.Mana));   // 1 per tier, for the flat 2
        Assert.Equal(((ActorState)awake, 2, 2, "test_warded_leech"), Assert.Single(paidSpends));
    }

    [Fact]
    public void ADormantSturdyDoesNotSpare()
    {
        // The two souls a defender carries are compiled handlers outside every
        // loop, and they obey dormancy through the index DefenderSoul now hands
        // back. These are exactly the entries with the largest locks in the game
        // -- Sturdy 30, Immovable 25 -- so leaving them ungated would break the
        // rule where it matters most: a dormant Bulwark would still refuse death.
        var grid = new int[20, 20];
        var maul = TestWeapons.Make("Maul", 40, 30, 30);

        // The ballast ahead of each soul is itself unaffordable, so nothing is
        // paid at all and the wielder faces the blow with its whole pool in hand:
        // what kills it is the soul being asleep and not the soul being poor.
        var doomed = Wielding("A", Souled("Heavy Bulwark", ("mire", 5), ("sturdy", 1)));
        doomed.Hp = 10;
        var killer = Enemy(5, 6, maul);
        var turns = new TurnSystem(grid, new[] { doomed }, new[] { killer }, () => 10);
        var spent = ManaProbe(turns);

        Assert.Equal((0, 100, 100), (doomed.PaidLocks, doomed.UsableMaxMana, doomed.Mana));
        Assert.True(doomed.IsDormant(1));
        CombatRules.Resolve(turns.Events, killer, doomed, killer.Weapon, Distance(killer, doomed), () => 10);
        Assert.Equal((0, false, 100), (doomed.Hp, doomed.Alive, doomed.Mana));   // it died holding twice Sturdy's trigger
        Assert.Empty(spent);

        // The shipped Bulwark, whose one entry the pool does cover, is the control.
        var spared = Wielding("B", TestWeapons.Get("the_bulwark"));
        spared.Hp = 10;
        var swinger = Enemy(5, 6, maul);
        var awake = new TurnSystem(grid, new[] { spared }, new[] { swinger }, () => 10);
        Assert.Equal((30, 70), (spared.PaidLocks, spared.UsableMaxMana));

        Assert.False(spared.IsDormant(0));
        CombatRules.Resolve(awake.Events, swinger, spared, swinger.Weapon, Distance(swinger, spared), () => 10);
        Assert.Equal((1, true, 70 - 40), (spared.Hp, spared.Alive, spared.Mana));   // 30 into Block 9, spared for its 40

        // Immovable is the same shape at (8,3): asleep, the line is shoved.
        var pushed = Wielding("C", Souled("Heavy Wall", ("mire", 5), ("immovable", 1)));
        var shover = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        var shove = new TurnSystem(grid, new[] { pushed }, new[] { shover }, () => 10);

        Assert.True(pushed.IsDormant(1));
        CombatRules.Resolve(shove.Events, shover, pushed, shover.Weapon, Distance(shover, pushed), () => 10);
        Assert.Equal((At(5, 4), 100), ((pushed.X, pushed.Y), pushed.Mana));

        // And awake, off the shipped Wall, it holds for its ten as it always did.
        var held = Wielding("D", TestWeapons.Get("hoplites_wall"));
        var pusher = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        var line = new TurnSystem(grid, new[] { held }, new[] { pusher }, () => 10);

        Assert.False(held.IsDormant(0));
        CombatRules.Resolve(line.Events, pusher, held, pusher.Weapon, Distance(pusher, held), () => 10);
        Assert.Equal((At(5, 5), 75 - 10), ((held.X, held.Y), held.Mana));
    }

    [Fact]
    public void LocksArePaidInAttachmentOrder_AndTheRemainderSleeps()
    {
        // Locks are paid strictly in attachment order, and the remainder after
        // the first unaffordable entry sleeps even where a later one would fit.
        // That is the opposite of partial firing, which does pass the remainder
        // down the list, and the two differ on purpose: a lock is a reservation
        // the whole list competes for once, a trigger a purchase each entry
        // makes in turn.
        var weapon = Souled("Layered Knife", ("vampiric", 3), ("mire", 3), ("regeneration", 1));   // 60, 75, 15
        var member = Wielding("A", weapon);

        Assert.Equal((150, 60, 40), (weapon.TotalLock, member.PaidLocks, member.UsableMaxMana));
        Assert.Equal((false, true, true), (member.IsDormant(0), member.IsDormant(1), member.IsDormant(2)));

        // The 15 of the last entry would have fitted in the 40 the first left; it
        // sleeps because the 75 ahead of it did not.
        Assert.True(weapon.Enchantments[2].EffectiveLock < member.UsableMaxMana);

        // Reversing the order reverses who sleeps, which is what makes attachment
        // order gameplay rather than bookkeeping.
        var reordered = Wielding("B", Souled("Relayered Knife", ("regeneration", 1), ("vampiric", 3), ("mire", 3)));
        Assert.Equal((75, 25), (reordered.PaidLocks, reordered.UsableMaxMana));
        Assert.Equal((false, false, true), (reordered.IsDormant(0), reordered.IsDormant(1), reordered.IsDormant(2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => member.IsDormant(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestPools.Char("C").IsDormant(0));
    }

    [Fact]
    public void ItWakesWhenThePoolGrows()
    {
        // "The enchantment wakes up if your pool ever grows to cover it." Nothing
        // subscribes to anything: dormancy is recomputed on read, so the point
        // that finally covers the lock is the whole of the event.
        var grid = new int[20, 20];
        var member = Wielding("A", Souled("Warded Leech", ("mire", 3), ("vampiric", 2)));
        member.Hp = 50;
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { member }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);

        Assert.True(member.IsDormant(1));
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((50, 0), (member.Hp, spent.Count));

        // Max mana is grown by spending mana, one threshold at a time, and at 115
        // the second entry's 40 finally fits behind the first's 75.
        while (member.IsDormant(1))
            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));
        Assert.Equal((115, 115, 0), (member.MaxMana, member.PaidLocks, member.UsableMaxMana));

        // Nothing was written to the entry to wake it: the record is the same one.
        Assert.Equal(2, member.EquippedWeapon!.Enchantments[1].Tier);
        Assert.Equal(0, member.EquippedWeapon.Enchantments[1].Xp);

        // Awake, it fires -- once there is anything to fire with.
        member.Mana = 2;
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((52, 0), (member.Hp, member.Mana));
        Assert.Equal(((ActorState)member, 2, 2, "test_warded_leech"), Assert.Single(spent));
    }

    [Fact]
    public void DormancyIsDerived_NeverStored()
    {
        // "Never stored on the enchantment, which is shared and immutable." The
        // type carries no dormancy of any name, and the one instance two weapons
        // share reads asleep in one wielder's hands and awake in the other's.
        var members = typeof(Enchantment)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name)
            .Where(n => n.Contains("Dormant", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Asleep", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Awake", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Paid", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal([], members);

        var shared = new Enchantment(GameContent.Current.Enchantments["vampiric"], Tier: 2);
        var ballast = new Enchantment(GameContent.Current.Enchantments["mire"], Tier: 3);

        var small = Wielding("A", Carrying("Small Hand", ballast, shared));
        var large = TestPools.Holding("B", Carrying("Large Hand", ballast, shared));

        Assert.Same(shared, small.EquippedWeapon!.Enchantments[1]);
        Assert.Same(shared, large.EquippedWeapon!.Enchantments[1]);
        Assert.Equal((true, false), (small.IsDormant(1), large.IsDormant(1)));

        // And it is recomputed, not cached: the same actor's answer changes when
        // its pool does, with no write anywhere.
        small.Credit(new XpCredit(XpPool.Mana, null, 100_000));
        Assert.False(small.IsDormant(1));
        Assert.Same(shared, small.EquippedWeapon.Enchantments[1]);
    }

    [Fact]
    public void LockingCompetesWithFiring()
    {
        // "Lock your whole pool and you have nothing left to trigger with." The
        // doc's own worked case: a tier-6 Arcane on a 200-pool wizard has 20 mana
        // left to fire with, which is two more triggers and then nothing -- the
        // enchantment has very nearly eaten the character that grew it.
        var grid = new int[20, 20];
        var (x, y) = At(5, 5);
        var wizard = TestPools.Char("A", x: x, y: y);
        wizard.Inventory[0] = Carrying("Arcane Knife", new Enchantment(Arcane, Tier: 6));
        wizard.Grown(maxMana: 200);
        wizard.Hp = 1;                                   // room for every drink to land whole

        Assert.Equal((200, 180, 20, 20), (wizard.MaxMana, wizard.PaidLocks, wizard.UsableMaxMana, wizard.Mana));
        Assert.False(wizard.IsDormant(0));

        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { wizard }, new[] { dummy }, () => 10);
        var spent = ManaProbe(turns);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);

        Assert.True(turns.TryAttack(wizard, dummy));     // 8 of the 20
        Assert.True(turns.TryAttack(wizard, dummy));     // 8 of the 12
        Assert.Equal(new[] { 8, 8 }, spent.Select(s => s.Spent));
        Assert.Equal(new[] { 24, 24 }, healed);          // potency 4 at tier 6
        Assert.Equal(4, wizard.Mana);

        // The scraps buy a fraction of a fire, and then the budget is the cap.
        Assert.True(turns.TryAttack(wizard, dummy));
        Assert.Equal(new[] { 8, 8, 4 }, spent.Select(s => s.Spent));
        Assert.Equal(new[] { 24, 24, 12 }, healed);
        Assert.Equal(0, wizard.Mana);

        Assert.True(turns.TryAttack(wizard, dummy));
        Assert.Equal(3, spent.Count);
        Assert.Equal(3, healed.Count);

        // No cap is needed anywhere in this system because the budget is the cap:
        // nothing clamps the tier, the lock or the pool.
        Assert.Equal(180, wizard.EquippedWeapon!.TotalLock);
        Assert.Equal(200, wizard.MaxMana);
    }

    [Fact]
    public void RegenNeverRefillsPastTheUsablePool()
    {
        // Mana comes back only from movement left unspent, and it comes back into
        // the spendable pool: a wielder never banks its way into mana the locks
        // have already reserved.
        var member = Wielding("A", Souled("Deep Leech", ("vampiric", 3)));
        Assert.Equal((100, 60, 40), (member.MaxMana, member.PaidLocks, member.UsableMaxMana));

        member.Mana = 0;
        Assert.Equal(16, member.RegenManaFromUnusedMovement(GameConstants.MaxDistance));
        Assert.Equal(16, member.Mana);

        Assert.Equal(24, member.RegenManaFromUnusedMovement(10_000f));
        Assert.Equal(40, member.Mana);                    // the usable ceiling, not the earned 100
        Assert.Equal(0, member.RegenManaFromUnusedMovement(10_000f));
        Assert.Equal((40, 100), (member.Mana, member.MaxMana));

        // Unequipped, the same member banks the whole of what it earned.
        member.Inventory[0] = null;
        Assert.Equal(60, member.RegenManaFromUnusedMovement(10_000f));
        Assert.Equal(100, member.Mana);
    }

    [Theory]
    [MemberData(nameof(UniqueWeaponIds))]
    public void TheSumOfPaidLocksNeverExceedsMaxMana(string uniqueId)
    {
        // "The sum of equipped locks may not exceed max mana" -- enforced by
        // dormancy rather than by refusing to equip, so it has to hold at every
        // pool size on the way up and not only at the end. The uniques are where
        // it bites: the Nameless Knife's three entries want 105 of a pool of 100.
        var weapon = TestWeapons.Get(uniqueId);
        var member = Wielding("A", weapon);

        for (int guard = 0; member.UsableMaxMana < TestPools.FixtureMana; guard++)
        {
            Assert.True(guard < 500, $"{uniqueId} never covered its locks.");
            Assert.True(member.PaidLocks <= member.MaxMana,
                $"{uniqueId} reserved {member.PaidLocks} of a pool of {member.MaxMana}.");
            Assert.Equal(member.MaxMana - member.PaidLocks, member.UsableMaxMana);

            // What is paid is exactly the awake prefix's locks, and nothing behind
            // the first sleeper is counted.
            int awake = 0;
            while (awake < weapon.Enchantments.Count && !member.IsDormant(awake)) awake++;
            Assert.Equal(weapon.Enchantments.Take(awake).Sum(e => e.EffectiveLock), member.PaidLocks);
            Assert.All(Enumerable.Range(awake, weapon.Enchantments.Count - awake), i => Assert.True(member.IsDormant(i)));

            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));
        }

        // Once the pool covers them all, every entry is awake and the reservation
        // is the item's own total.
        Assert.Equal(weapon.TotalLock, member.PaidLocks);
        Assert.All(Enumerable.Range(0, weapon.Enchantments.Count), i => Assert.False(member.IsDormant(i)));
    }

    public static TheoryData<string> UniqueWeaponIds()
    {
        var data = new TheoryData<string>();
        foreach (var def in GameContent.Current.Weapons.Uniques)
            data.Add(def.Id);
        return data;
    }
}
