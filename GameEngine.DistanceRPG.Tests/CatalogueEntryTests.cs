using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The rest of §3.3's starting set as behaviour: Arcane's untyped damage,
/// Shattering's deeper riders, the shielding entry the settled notes ask for,
/// and Echoing, declared with its behaviour open. Beside them the rule that
/// makes an element worth rolling onto a martial weapon at all — the swing
/// types itself at (0,5), ahead of the chart — which is an enchantment rule and
/// so lives with the enchantments rather than in the swing path.
/// <para>
/// Nothing here swaps content: every entry it exercises is in the compiled
/// catalogue, and the tiers it needs are instances rather than rows.
/// </para>
/// </summary>
public class CatalogueEntryTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    /// <summary>An ad-hoc dagger-class weapon with a forged spread and catalogue entries at the given tiers, all of them forged.</summary>
    private static Weapon Carrying(string name, (ModifierType Type, int Stacks)[] forged, params (string Id, int Tier)[] entries)
        => Built(name, WeaponClass.Dagger, range: 40, forged, entries);

    /// <summary>The same with a reach: a shot that carries on needs a second body still inside the weapon's range.</summary>
    private static Weapon Reaching(string name, int range, params (string Id, int Tier)[] entries)
        => Built(name, WeaponClass.Ranged, range, [], entries);

    private static Weapon Built(string name, WeaponClass cls, int range, (ModifierType Type, int Stacks)[] forged, (string Id, int Tier)[] entries)
    {
        var catalogue = GameContent.Current.Enchantments;
        var spread = new Dictionary<ModifierType, int>();
        foreach (var (type, stacks) in forged)
            spread[type] = spread.GetValueOrDefault(type) + stacks;
        var def = new WeaponDef(
            Id: "test_" + name.ToLowerInvariant().Replace(' ', '_'), Name: name, Class: cls, Role: null,
            Range: range, Damage: 15, Cost: 30, ManaCost: 0,
            Forged: spread, Enchantments: entries.Select(e => new EnchantmentRef(e.Id, e.Tier)).ToList(),
            Shape: null, Unique: false, DerivedFrom: null);
        return new Weapon(def, entries.Select(e => new Enchantment(catalogue[e.Id], e.Tier)).ToList());
    }

    /// <summary>A fixture member holding <paramref name="weapon"/>, grown until its spendable pool is the suite's.</summary>
    private static PartyMemberState Holding(string id, int r, int c, Weapon weapon)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, weapon, x: x, y: y);
    }

    /// <summary>A dummy with no Block (fists) unless told otherwise, and enough HP that nothing here kills it.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, DamageType? attunement = null)
    {
        var (x, y) = At(r, c);
        return new EnemyState
        {
            X = x, Y = y, Hp = 500, Attunement = attunement,
            Weapon = weapon ?? TestWeapons.Make("Fists", 40, 1, 0),
        };
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

    /// <summary>Record every DamageTaken payload as the chain settled it, after every compiled handler.</summary>
    private static List<DamagePayload> HitProbe(TurnSystem turns)
    {
        var hits = new List<DamagePayload>();
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            hits.Add(p);
            return p;
        });
        return hits;
    }

    // ── Arcane ───────────────────────────────────────────────────────────────

    [Fact]
    public void Arcane_AddsUntypedDamageBesideTheWeaponsShare()
    {
        // "Bonus damage -- the wizard-DPS core", for a lock of 30 and a trigger
        // of 8: its potency's levels, which is Tuning.ArcanePotency through the
        // one LevelsFor call every kind uses, so the tier multiplies it. Four at
        // tier 1, twelve at tier 3. It lands in the enchantment share beside the
        // weapon's and never in it, like everything else at step 4.
        var grid = new int[20, 20];
        var knife = Carrying("Arcane Knife", [], ("arcane", 1));
        var a = Holding("A", 5, 5, knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);

        Assert.Equal(4, GameContent.Current.Tuning.ArcanePotency);
        Assert.True(turns.TryAttack(a, dummy));
        Assert.Equal(4, hits[^1].EnchantmentShare);
        Assert.Equal(hits[^1].WeaponShare + 4, hits[^1].Dealt);
        Assert.Equal(((ActorState)a, 8, 8), Assert.Single(spent));

        // Tier three, three times as much, for the same flat trigger: the lock
        // (90) is what the depth costs, and the trigger is what firing costs.
        var deep = Carrying("Deep Arcane", [], ("arcane", 3));
        var b = Holding("B", 5, 5, deep);
        var far = Enemy(5, 6);
        var second = new TurnSystem(grid, new[] { b }, new[] { far }, () => 10);
        var deepHits = HitProbe(second);
        Assert.Equal(90, deep.Enchantments[0].EffectiveLock);
        Assert.True(second.TryAttack(b, far));
        Assert.Equal(12, deepHits[^1].EnchantmentShare);
    }

    [Fact]
    public void Arcane_IsUntyped_SoTheChartNeitherResistsNorAmplifiesIt()
    {
        // "Raw magical damage, no element, and therefore nothing the type chart
        // can resist or amplify. Arcane never gets halved and never gets the
        // x1.5." That is what makes it the safe damage entry and the four
        // elements the situational ones, so it is asserted against every
        // attunement there is rather than against one.
        var grid = new int[20, 20];
        foreach (var attunement in Enum.GetValues<DamageType>())
        {
            var knife = Carrying("Arcane Knife", [], ("arcane", 1));
            var a = Holding("A", 5, 5, knife);
            var dummy = Enemy(5, 6, attunement: attunement == DamageType.None ? null : attunement);
            var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);
            var hits = HitProbe(turns);

            Assert.True(turns.TryAttack(a, dummy));
            Assert.Equal(4, hits[^1].EnchantmentShare);
            Assert.Equal(DamageType.None, hits[^1].Type);
        }
    }

    [Fact]
    public void Arcane_IsTheRenamedEntry()
    {
        // "Arcane was Arcane Edge." An edge is a thing a blade has, and the
        // enchantment goes on wands; the id is the short name and the long one
        // exists nowhere, so no content can name it by mistake.
        var catalogue = GameContent.Current.Enchantments;
        Assert.True(catalogue.TryGet("arcane", out var arcane));
        Assert.Equal("Arcane", arcane.Name);
        Assert.Equal(EffectKind.BonusDamage, arcane.Effect);
        Assert.False(catalogue.TryGet("arcane_edge", out _));
        Assert.DoesNotContain(catalogue.All, e => e.Id.Contains("edge", StringComparison.Ordinal));
        Assert.DoesNotContain(catalogue.All, e => e.Name.Contains("Edge", StringComparison.Ordinal));
    }

    // ── Shattering ───────────────────────────────────────────────────────────

    [Fact]
    public void Shattering_LandsTheRidersOneLevelDeeper()
    {
        // "Crit riders (§1.6) land one level deeper", one level per tier: a
        // Disarming Kris's CritWeaken lands two levels with it and one without.
        // It fires at step 4 of the hit rather than on Crit, because the riders
        // settle at step 7 of that same chain and a Crit handler would arrive
        // after they had landed.
        var grid = new int[20, 20];
        var kris = Carrying("Shattering Kris", [(CritWeaken, 1)], ("shattering", 1));
        var a = Holding("A", 5, 5, kris);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 20);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);
        var dealt = new List<DamagePayload>();
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 9), "probe",
            (p, _, _) => { dealt.Add(p); return p; });

        Assert.True(turns.TryAttack(a, dummy));
        Assert.True(hits[^1].IsCrit);
        Assert.Equal(1, hits[^1].RiderDepth);
        Assert.Equal(2, dummy.StatusLevel(StatusEffectType.Weakened));
        Assert.Equal(((ActorState)a, 10, 10), Assert.Single(spent));

        // And the depth is consumed where it was settled: (7,0) of this chain
        // landed it, so it is cleared with the hit's other writes on the way to
        // DamageDealt and an entry firing there cannot read the same crit's
        // depth a second time.
        Assert.Equal(0, Assert.Single(dealt).RiderDepth);

        // The same crit without it: one level, the stack's own.
        var plain = Carrying("Plain Kris", [(CritWeaken, 1)]);
        var b = Holding("B", 5, 5, plain);
        var bare = Enemy(5, 6);
        var second = new TurnSystem(grid, new[] { b }, new[] { bare }, () => 20);
        Assert.True(second.TryAttack(b, bare));
        Assert.Equal(1, bare.StatusLevel(StatusEffectType.Weakened));

        // And a swing that does not crit lands no rider to deepen, so the entry
        // does not fire and pays nothing.
        var c = Holding("C", 5, 5, Carrying("Shattering Kris", [(CritWeaken, 1)], ("shattering", 1)));
        var calm = Enemy(5, 6);
        var third = new TurnSystem(grid, new[] { c }, new[] { calm }, () => 10);
        var thirdHits = HitProbe(third);
        var thirdSpent = ManaProbe(third);
        Assert.True(third.TryAttack(c, calm));
        Assert.Equal(0, thirdHits[^1].RiderDepth);
        Assert.Equal(0, calm.StatusLevel(StatusEffectType.Weakened));
        Assert.Empty(thirdSpent);
    }

    [Fact]
    public void Shattering_GrantsNoModifierStack_ItsDepthIsAnEffectItApplies()
    {
        // "A handful of enchantments happen to grant a modifier stack as their
        // effect (Weightless, Shattering), but that is an effect they apply, not
        // what they are." So the depth rides the payload and nothing touches the
        // weapon's spread: after the crit the wielder carries exactly the one
        // CritWeaken stack it was forged with, and no CritSunder appears from
        // nowhere -- a rider the attacker does not carry is still not landed.
        var grid = new int[20, 20];
        var kris = Carrying("Shattering Kris", [(CritWeaken, 1)], ("shattering", 2));
        var a = Holding("A", 5, 5, kris);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 20);

        Assert.True(turns.TryAttack(a, dummy));

        Assert.Equal(3, dummy.StatusLevel(StatusEffectType.Weakened));           // one stack, two tiers deeper
        Assert.Equal(0, dummy.StatusLevel(StatusEffectType.Sundered));           // no CritSunder to deepen
        Assert.Equal(1, kris.Modifiers.Stacks(CritWeaken));
        Assert.Equal(1, a.Value(CritWeaken));
        Assert.Equal(kris.Forged.Entries.Count(), kris.Modifiers.Entries.Count());
    }

    // ── The shielding entry ──────────────────────────────────────────────────

    [Fact]
    public void Aegis_AbsorbsEnchantmentDamageOnly()
    {
        // The settled answer to "enchantment damage is slightly better into
        // armour": a counter rather than a nerf -- "a catalogue entry whose
        // EffectPerLevel absorbs a point of enchantment damage, sitting beside
        // Block as the second armour for the second damage source". It takes one
        // point per level off the enchantment share, leaves the weapon's share
        // alone, re-fixes what was dealt, and pays out of the defender's own pool.
        var grid = new int[20, 20];
        var knife = Carrying("Arcane Knife", [], ("arcane", 1));
        var a = Holding("A", 5, 5, knife);
        var shielded = Enemy(5, 6, Carrying("Shielded", [], ("aegis", 1)));
        var turns = new TurnSystem(grid, new[] { a }, new[] { shielded }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);
        int poolBefore = shielded.Mana;

        Assert.True(turns.TryAttack(a, shielded));
        var hit = hits[^1];
        Assert.Equal(3, hit.EnchantmentShare);                      // four, one absorbed
        Assert.Equal(3, hit.ForgedShare);                           // and the forged share falls with it
        Assert.Equal(0, hit.Absorbed);                              // Block took nothing: the weapon's share is untouched
        Assert.Equal(hit.WeaponShare + 3, hit.Dealt);
        Assert.Equal(2, hit.DefenderManaToSpend);
        Assert.Equal(poolBefore - 2, shielded.Mana);
        Assert.Contains(spent, s => ReferenceEquals(s.Self, shielded) && s.Wanted == 2 && s.Spent == 2);

        // Weapon XP is credited the weapon's damage plus the forged share, so
        // absorbing without reducing the forged share would let the attacker
        // level on damage the shield swallowed -- and a wand, whose forged
        // element is its whole damage, would level in full against a target that
        // took none of it. One point of the credit is gone with the point absorbed.
        var b = Holding("B", 5, 5, Carrying("Arcane Knife", [], ("arcane", 1)));
        var bare = Enemy(5, 6);
        var second = new TurnSystem(grid, new[] { b }, new[] { bare }, () => 10);
        Assert.True(second.TryAttack(b, bare));
        Assert.Equal(b.WeaponXp[WeaponClass.Dagger] - 1, a.WeaponXp[WeaponClass.Dagger]);
    }

    [Fact]
    public void Aegis_TakesItsTierInPoints_AndSleepsWithItsLock()
    {
        // A point per level, so a tier-two entry takes two -- and a tier-six one
        // locks 120 of a dummy's flat 100, which it cannot pay, so it is dormant
        // and absorbs nothing. A compiled defender handler obeys dormancy exactly
        // as the loops do: these are the entries with the largest locks, and a
        // sleeping shield that still shielded would break the rule where it
        // matters most.
        var grid = new int[20, 20];
        var a = Holding("A", 5, 5, Carrying("Arcane Knife", [], ("arcane", 1)));
        var deep = Enemy(5, 6, Carrying("Shielded", [], ("aegis", 2)));
        var turns = new TurnSystem(grid, new[] { a }, new[] { deep }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryAttack(a, deep));
        Assert.Equal(2, hits[^1].EnchantmentShare);
        Assert.Equal(2, hits[^1].ForgedShare);

        var b = Holding("B", 5, 5, Carrying("Arcane Knife", [], ("arcane", 1)));
        var asleep = Enemy(5, 6, Carrying("Shielded", [], ("aegis", 6)));
        var second = new TurnSystem(grid, new[] { b }, new[] { asleep }, () => 10);
        var sleepy = HitProbe(second);
        var spent = ManaProbe(second);

        Assert.Equal(120, asleep.EquippedWeapon!.Enchantments[0].EffectiveLock);
        Assert.True(asleep.IsDormant(0));
        Assert.True(second.TryAttack(b, asleep));
        Assert.Equal(4, sleepy[^1].EnchantmentShare);
        Assert.Equal(0, sleepy[^1].DefenderManaToSpend);
        Assert.DoesNotContain(spent, s => ReferenceEquals(s.Self, asleep));
    }

    // ── An element on a martial weapon ───────────────────────────────────────

    [Fact]
    public void AnElementOnASwingTypesTheHitAndPaysForItself()
    {
        // §3.3 prices every element at a lock of 20 and a trigger of 5, "Fires on
        // Hit" -- so a rare drop putting Flaming on a dagger has to do something,
        // and what an element does is type the hit. The typing is the whole of
        // it: the four elements carry Potency 0, so nothing is added beside the
        // weapon's share; what moves the number is the chart reading the type off
        // the weapon's own base.
        var grid = new int[20, 20];
        var knife = Carrying("Flaming Knife", [], ("flaming", 1));
        var a = Holding("A", 5, 5, knife);
        var opposed = Enemy(5, 6, attunement: DamageType.Cold);
        var turns = new TurnSystem(grid, new[] { a }, new[] { opposed }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryAttack(a, opposed));
        Assert.Equal(DamageType.Flaming, hits[^1].Type);
        Assert.Equal(0, hits[^1].EnchantmentShare);
        Assert.Equal(((ActorState)a, 5, 5), Assert.Single(spent));
        Assert.Equal(5, knife.Enchantments[0].Xp);                  // the payment is the entry's own experience

        // The same swing into a target the chart does not care about, to say what
        // the typing was worth: x1.5 of the weapon's own base, truncated.
        var plainKnife = Carrying("Flaming Knife", [], ("flaming", 1));
        var b = Holding("B", 5, 5, plainKnife);
        var unattuned = Enemy(5, 6);
        var second = new TurnSystem(grid, new[] { b }, new[] { unattuned }, () => 10);
        var flat = HitProbe(second);
        Assert.True(second.TryAttack(b, unattuned));
        Assert.Equal(flat[^1].WeaponShare * CombatRules.OpposedDamagePercent / 100, hits[^1].WeaponShare);

        // A wielder who cannot pay the trigger swings untyped and is charged
        // nothing: the ordinary non-event, and the chart never sees a type.
        var c = Holding("C", 5, 5, Carrying("Flaming Knife", [], ("flaming", 1)));
        var poor = Enemy(5, 6, attunement: DamageType.Cold);
        var third = new TurnSystem(grid, new[] { c }, new[] { poor }, () => 10);
        var broke = HitProbe(third);
        var nothing = ManaProbe(third);
        c.Mana = 0;
        Assert.True(third.TryAttack(c, poor));
        Assert.Equal(DamageType.None, broke[^1].Type);
        Assert.Equal(flat[^1].WeaponShare, broke[^1].WeaponShare);
        Assert.Empty(nothing);
    }

    [Fact]
    public void AWandsElementIsStillTheCastsToPay_AndIsPaidOnce()
    {
        // The step stands aside for a caster: a wand's element types its cast,
        // once per cast and not once per body the shape catches, and the hits it
        // fans out to carry that type in. Two doors onto one payment would be a
        // double charge, which is why the step asks the weapon's kind as well as
        // whether a cast fired.
        var grid = new int[20, 20];
        var wand = TestWeapons.Get("wand_of_the_blast", DamageType.Flaming);
        var a = Holding("A", 5, 5, wand);
        var first = Enemy(5, 6, attunement: DamageType.Cold);
        var second = Enemy(5, 7, attunement: DamageType.Cold);
        var turns = new TurnSystem(grid, new[] { a }, new[] { first, second }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryCastArea(a, (first.X, first.Y)));

        Assert.Equal(2, hits.Count);                                // both bodies the blast caught
        Assert.All(hits, h => Assert.Equal(DamageType.Flaming, h.Type));
        Assert.All(hits, h => Assert.Equal(0, h.ManaToSpend));      // the hits pay nothing: the cast paid
        var record = Assert.Single(spent);
        int trigger = wand.Innate!.ResolvedTriggerCost(wand, 1);
        Assert.Equal(wand.ResolvedManaCost + trigger, record.Wanted);   // one cast, one trigger, however many bodies
    }

    [Fact]
    public void ACleavePaysForEveryBodyItTypes()
    {
        // §3.3 prices an element per hit, and a cleave is one swing landing
        // several times: each body it fans out to is its own hit, so each one
        // pays the trigger again. The "once per shape, not once per body" rule
        // belongs to the cast, which is one action aimed at a point.
        var grid = new int[20, 20];
        var cleaver = Carrying("Flaming Cleaver", [(Cleave, 1)], ("flaming", 1));
        var a = Holding("A", 5, 5, cleaver);
        var aimed = Enemy(5, 6, attunement: DamageType.Cold);
        var caught = Enemy(5, 7, attunement: DamageType.Cold);
        var turns = new TurnSystem(grid, new[] { a }, new[] { aimed, caught }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryAttack(a, aimed));

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(DamageType.Flaming, h.Type));
        Assert.All(spent, s => Assert.Same(a, s.Self));
        Assert.Equal(new[] { (5, 5), (5, 5) }, spent.Select(s => (s.Wanted, s.Spent)).ToArray());
        Assert.Equal(10, cleaver.Enchantments[0].Xp);               // two hits, two payments, both the entry's

        // And a wielder with one trigger left types the body it aimed at and
        // swings plain through the rest: the ordinary running-out, per body.
        var poorCleaver = Carrying("Flaming Cleaver", [(Cleave, 1)], ("flaming", 1));
        var b = Holding("B", 5, 5, poorCleaver);
        var first = Enemy(5, 6, attunement: DamageType.Cold);
        var second = Enemy(5, 7, attunement: DamageType.Cold);
        var poor = new TurnSystem(grid, new[] { b }, new[] { first, second }, () => 10);
        var poorHits = HitProbe(poor);
        var once = ManaProbe(poor);
        b.Mana = 5;

        Assert.True(poor.TryAttack(b, first));
        Assert.Equal(new[] { DamageType.Flaming, DamageType.None }, poorHits.Select(h => h.Type).ToArray());
        Assert.Equal(((ActorState)b, 5, 5), Assert.Single(once));
        Assert.Equal(0, b.Mana);
    }

    [Fact]
    public void APierceKeepsTheTypeDownTheLine_ForOnePayment()
    {
        // The other multi-body shape, and the other answer: a shot carried on is
        // one hit continuing rather than a second swing. The DamageDealt applier
        // seeds the continuation with the type the first hit settled, so the step
        // finds it already typed and stands aside, and the whole line is typed
        // for one payment of the element's trigger.
        var grid = new int[20, 20];
        var bow = Reaching("Flaming Bow", range: 320, ("flaming", 1), ("piercing", 1));
        var a = Holding("A", 5, 2, bow);
        var first = Enemy(5, 6, attunement: DamageType.Cold);
        var second = Enemy(5, 10, attunement: DamageType.Cold);
        var turns = new TurnSystem(grid, new[] { a }, new[] { first, second }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryAttack(a, first));

        Assert.Equal(2, hits.Count);
        Assert.Equal(new[] { false, true }, hits.Select(h => h.FromPierce).ToArray());
        Assert.All(hits, h => Assert.Equal(DamageType.Flaming, h.Type));

        // The element's 5 on the hit, then the shot's own 6 to carry on, and
        // nothing more: the second body is typed for free.
        Assert.All(spent, s => Assert.Same(a, s.Self));
        Assert.Equal(new[] { (5, 5), (6, 6) }, spent.Select(s => (s.Wanted, s.Spent)).ToArray());
        Assert.Equal(5, bow.Enchantments[0].Xp);
    }

    [Fact]
    public void TheMartialElementStepRunsBeforeTheChart()
    {
        // The whole point of the step number. The type a hit carries is settled
        // before the chain begins and read by the chart at (3,1), which (3,9)
        // then closes the weapon's share over; an element firing at step 4 -- the
        // slot every other enchantment rule sits in -- would be typing a hit the
        // chart had already resolved. The same handler, moved to (3,5), proves it:
        // the payload still says Flaming and the number never moves.
        var knife = Carrying("Flaming Knife", [], ("flaming", 1));
        var a = TestPools.Holding("A", knife);
        var defender = Enemy(5, 6, attunement: DamageType.Cold);

        int Share(HandlerPriority? at)
        {
            var table = new EventTable();
            CombatBehaviours.Register(table);
            if (at is { } priority)
                table.On<DamagePayload>(GameEvent.DamageTaken, priority, "MartialElement", EnchantmentBehaviours.MartialElement);
            var settled = table.Raise(GameEvent.DamageTaken, DamagePayload.Initial(knife, roll: 10, distanceUnits: 32), a, defender);
            Assert.Equal(at == null ? DamageType.None : DamageType.Flaming, settled.Type);
            return settled.WeaponShare;
        }

        int untyped = Share(null);
        int early = Share(EnchantmentBehaviours.MartialElementPriority);
        int late = Share(new HandlerPriority(3, 5));

        Assert.Equal(untyped * CombatRules.OpposedDamagePercent / 100, early);
        Assert.Equal(untyped, late);
    }

    // ── Attachment order, and the one pair it cannot be shown with ────────────

    [Fact]
    public void AttachmentOrderDecidesWhatFiresWhenThePoolIsShort()
    {
        // "A weapon's mana runs out somewhere down the list, so you are really
        // choosing what fires at full strength when you are poor." Two entries
        // that fire on the same event, a pool that covers one of them, and the
        // two orders: the leader fires whole and the trailer scales to nothing,
        // which is the non-event that passes the remainder down the list.
        var grid = new int[20, 20];
        var leadsWithDamage = Carrying("Arcane First", [(CritWeaken, 1)], ("arcane", 1), ("shattering", 2));
        var a = Holding("A", 5, 5, leadsWithDamage);
        var first = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { first }, () => 20);
        var hits = HitProbe(turns);
        a.Mana = 10;

        Assert.True(turns.TryAttack(a, first));
        Assert.Equal(4, hits[^1].EnchantmentShare);                 // Arcane paid its eight
        Assert.Equal(0, hits[^1].RiderDepth);                       // two left of a trigger of ten: a non-event
        Assert.Equal(1, first.StatusLevel(StatusEffectType.Weakened));

        var leadsWithDepth = Carrying("Shattering First", [(CritWeaken, 1)], ("shattering", 2), ("arcane", 1));
        var b = Holding("B", 5, 5, leadsWithDepth);
        var second = Enemy(5, 6);
        var reversed = new TurnSystem(grid, new[] { b }, new[] { second }, () => 20);
        var reversedHits = HitProbe(reversed);
        b.Mana = 10;

        Assert.True(reversed.TryAttack(b, second));
        Assert.Equal(2, reversedHits[^1].RiderDepth);               // Shattering paid its ten
        Assert.Equal(0, reversedHits[^1].EnchantmentShare);         // and left Arcane nothing
        Assert.Equal(3, second.StatusLevel(StatusEffectType.Weakened));
    }

    [Fact]
    public void SerratedAndArcaneReadTheSameEitherWay_BecauseTheyFireOnDifferentEvents()
    {
        // §3.3's worked example is "a dagger that leads with Serrated and trails
        // with Arcane bleeds reliably and adds damage when it can afford to;
        // reverse them and it is a damage weapon that sometimes bleeds." That one
        // pair is the case attachment order cannot decide: Arcane fires at step 4
        // of DamageTaken and Serrated on DamageDealt, a later event on the same
        // swing, so Arcane is served first in both orders and the pool Serrated
        // sees is what Arcane left of it either way. The choice the passage
        // describes is real -- the test above is it -- but it is a choice among
        // entries firing on the same event, which is where the loop reads
        // attachment order at all.
        var grid = new int[20, 20];

        (int Share, int Bleed) Swing(int mana, params (string Id, int Tier)[] entries)
        {
            var knife = Carrying("Order " + mana + entries[0].Id, [], entries);
            var member = Holding("A", 5, 5, knife);
            var dummy = Enemy(5, 6);
            var turns = new TurnSystem(grid, new[] { member }, new[] { dummy }, () => 10);
            var hits = HitProbe(turns);
            member.Mana = mana;
            Assert.True(turns.TryAttack(member, dummy));
            return (hits[^1].EnchantmentShare, dummy.StatusLevel(StatusEffectType.Bleeding));
        }

        // Eight: Arcane takes all of it in both orders, and nothing bleeds.
        Assert.Equal((4, 0), Swing(8, ("serrated", 1), ("arcane", 1)));
        Assert.Equal((4, 0), Swing(8, ("arcane", 1), ("serrated", 1)));

        // Ten: Arcane takes eight in both orders, and the wound is what the two
        // it left buys -- the same wound, from the same remainder.
        Assert.Equal(Swing(10, ("serrated", 1), ("arcane", 1)), Swing(10, ("arcane", 1), ("serrated", 1)));
        Assert.Equal((4, 3), Swing(10, ("serrated", 1), ("arcane", 1)));
    }

    // ── Echoing, and the starting set as a table ─────────────────────────────

    [Fact]
    public void Echoing_IsDeclaredAndInert()
    {
        // Lock 20, trigger 15, "fires on Attack: the weapon's class feature
        // triggers once more" -- and that is the whole of what the docs say. The
        // phrase has no meaning for six of the eight classes, and AttackDeclared,
        // the event it names, is raised nowhere: both are recorded in
        // open-questions.md rather than invented here. So the entry exists,
        // validates, attaches and reserves its lock, and fires on nothing.
        var catalogue = GameContent.Current.Enchantments;
        Assert.True(catalogue.TryGet("echoing", out var echoing));
        Assert.Equal((20, 15, EffectKind.Echoing), (echoing.Lock, echoing.Trigger, echoing.Effect));
        Assert.False(echoing.Unique);
        foreach (var evt in Enum.GetValues<GameEvent>())
            Assert.False(EnchantmentBehaviours.FiresOn(evt, EffectKind.Echoing), $"{evt} runs it");

        var grid = new int[20, 20];
        var knife = Carrying("Echoing Knife", [], ("echoing", 1));
        var a = Holding("A", 5, 5, knife);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 20);
        var spent = ManaProbe(turns);

        Assert.Equal(20, a.PaidLocks);                              // it reserves like any other entry
        Assert.False(a.IsDormant(0));
        Assert.Empty(turns.Events.HandlersFor(GameEvent.AttackDeclared));   // nothing is registered on it, and nothing raises it
        Assert.True(turns.TryAttack(a, dummy));
        Assert.Empty(spent);
        Assert.Equal(0, knife.Enchantments[0].Xp);
    }

    [Fact]
    public void EveryConditionTheStartingSetNamesResolvesToAnEvent()
    {
        // "What fires it — on hit, on crit, on kill, on being hit, on cast": the
        // condition vocabulary, and the dispatch point per condition §3.5 asks
        // the turn system for. Each one is an event the table already dispatches
        // rather than a branch anywhere, and the unified attack resolver is the
        // single call site hit, crit and kill all pass through. The starting
        // set's own "Fires on" column reads off the tables the same way.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        foreach (var evt in new[] { GameEvent.DamageTaken, GameEvent.DamageDealt, GameEvent.Killed, GameEvent.Cast })
            Assert.NotEmpty(table.HandlersFor(evt));

        Assert.True(EnchantmentBehaviours.FiresOn(GameEvent.DamageTaken, EffectKind.BonusDamage));      // Arcane: on hit
        Assert.True(EnchantmentBehaviours.FiresOn(GameEvent.DamageTaken, EffectKind.Shattering));       // Shattering: on a crit, read off the hit it rides
        Assert.True(EnchantmentBehaviours.FiresOn(GameEvent.DamageTaken, EffectKind.ElementalDamage));  // the four elements: on hit
        Assert.True(EnchantmentBehaviours.FiresOn(GameEvent.Cast, EffectKind.ApplyStatus));             // the four staff effects: a cast is a hit
        Assert.True(EnchantmentBehaviours.FiresOn(GameEvent.DamageDealt, EffectKind.Vampiric));         // Vampiric: on damage dealt

        // The crit is the one condition with no chain of its own: the event is
        // raised, but the entry that answers it reads IsCrit off the hit, because
        // the riders it deepens settle at step 7 of that chain and a Crit handler
        // arrives after they have landed. "On being hit" is the defender's side
        // of the same hit, which is where the shielding entry is compiled. And
        // the one condition with no event at all is Attack, which nothing raises
        // and which Echoing waits on.
        Assert.Empty(table.HandlersFor(GameEvent.Crit));
        Assert.Contains(table.HandlersFor(GameEvent.DamageTaken), h => h.ToString() == "DamageTaken (5,1) Aegis");
        Assert.Empty(table.HandlersFor(GameEvent.AttackDeclared));
    }

    [Theory]
    // The §3.3 starting-set table, verbatim: lock, then the flat trigger, or
    // null for a status applier, whose price is the DoT ladder over the levels it
    // lands. The table prints 5 for those four, but Phase 1 ruled -- and the
    // content validator enforces -- that an entry quotes a flat trigger or
    // applies a status and never both, because a flat cost decays into free as
    // the levels rise. They still cost, which is the line the table is making.
    [InlineData("arcane", 30, 8)]
    [InlineData("vampiric", 20, 2)]
    [InlineData("echoing", 20, 15)]
    [InlineData("shattering", 25, 10)]
    [InlineData("flaming", 20, 5)]
    [InlineData("shocking", 20, 5)]
    [InlineData("acidic", 20, 5)]
    [InlineData("cold", 20, 5)]
    [InlineData("regeneration", 15, null)]
    [InlineData("ward", 20, null)]
    [InlineData("poison", 20, null)]
    [InlineData("mire", 25, null)]
    public void EveryStartingSetEntryExistsWithItsDocLockAndTrigger(string id, int @lock, int? trigger)
    {
        var catalogue = GameContent.Current.Enchantments;
        Assert.True(catalogue.TryGet(id, out var def), $"the catalogue carries no '{id}'");
        Assert.Equal(@lock, def.Lock);
        Assert.Equal(trigger, def.Trigger);
        Assert.False(def.Unique);                                   // the starting set is what a campaign can meet
        Assert.Contains(id, LootTable.DropPool);

        // And none of them fires free, whichever way it is priced (settled: "no
        // zero-cost triggers, ever").
        var entry = new Enchantment(def, Tier: 1);
        var bare = TestWeapons.Make("Bare", 40, 15, 30);
        Assert.True(entry.TriggerCostFor(entry.LevelsFor(Math.Max(1, def.Potency))) > 0);
        Assert.True(entry.ResolvedTriggerCost(bare, 1) > 0);
    }
}
