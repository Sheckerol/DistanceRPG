using System.Collections.Immutable;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.DamageType;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.5/§3.3's souls as behaviour: each is an entry in the per-kind table of
/// the event it fires on — or, for the two the defender carries, a compiled
/// step of its own — pays its trigger through the one record, and settles
/// everything on the payload for the applier to write once. In the content
/// collection because tests here swap the current content.
/// </summary>
[Collection(TestContent.Collection)]
public class SoulBehaviourTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, TestWeapons.Get(weaponId), x: x, y: y);
    }

    private static PartyMemberState Holding(string id, int r, int c, Weapon weapon)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, weapon, x: x, y: y);
    }

    /// <summary>A dummy on a tile's centre with no Block (fists) unless told otherwise, and enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? Fists, Hp = hp };
    }

    private static Weapon Fists => TestWeapons.Make("Fists", 40, 1, 0);

    /// <summary>An ad-hoc dagger-class weapon with no forged spread carrying catalogue entries at the given tiers, for souls off their uniques.</summary>
    private static Weapon Souled(string name, int range, int damage, int cost, params (string Id, int Tier)[] enchantments)
    {
        var catalogue = GameContent.Current.Enchantments;
        var def = new WeaponDef(
            Id: "test_" + name.ToLowerInvariant().Replace(' ', '_'), Name: name, Class: WeaponClass.Dagger, Role: null,
            Range: range, Damage: damage, Cost: cost, ManaCost: 0,
            Forged: new Dictionary<ModifierType, int>(), Enchantments: enchantments.Select(e => new EnchantmentRef(e.Id, e.Tier)).ToList(),
            Shape: null, Unique: false, DerivedFrom: null);
        return new Weapon(def, enchantments.Select(e => new Enchantment(catalogue[e.Id], e.Tier)).ToList());
    }

    /// <summary>Record every ManaSpent the table settles: who paid, what was wanted, paid and restored, and through what.</summary>
    private static List<(ActorState Self, int Wanted, int Spent, int Restored, string Source)> ManaProbe(TurnSystem turns)
    {
        var spent = new List<(ActorState, int, int, int, string)>();
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            spent.Add((s, p.Wanted, p.Spent, p.Restored, p.Source));
            return p;
        });
        return spent;
    }

    /// <summary>Record every DamageTaken payload as the chain settled it, after every compiled handler.</summary>
    private static List<(ActorState Target, DamagePayload Hit)> HitProbe(TurnSystem turns)
    {
        var hits = new List<(ActorState, DamagePayload)>();
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(9, 9), "probe", (p, _, o) =>
        {
            hits.Add((o, p));
            return p;
        });
        return hits;
    }

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    private static int Distance(ActorState a, ActorState b) => CombatRules.SurfaceDistanceUnits(a, b);

    /// <summary>A fresh table carrying every compiled behaviour and nothing a test probes with: what a chain prints as.</summary>
    private static EventTable Compiled()
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table;
    }

    // ── Immovable ────────────────────────────────────────────────────────────

    [Fact]
    public void Immovable_NegatesPushOnTheWielder_PaysTrigger()
    {
        // A sword dummy's Push x1 on the Hoplite's Wall's wielder, in the wielder's opponent's phase (a raw
        // resolve's default): the shove is negated entirely — the hit lands, the line does not move — for
        // 10 out of the wielder's own pool, as one record. Unable to pay, the wielder is shoved like anyone
        // else and pays nothing. In its own phase it is the one moving, and a shove lands as on anyone.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "hoplites_wall");
        var sword = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        var turns = new TurnSystem(grid, new[] { a }, new[] { sword }, () => 10);
        var spent = ManaProbe(turns);
        var hits = HitProbe(turns);
        Assert.Equal(new HandlerPriority(8, 3), EnchantmentBehaviours.ImmovablePriority);

        CombatRules.Resolve(turns.Events, sword, a, sword.Weapon, Distance(sword, a), () => 10);

        Assert.Equal(At(5, 5), (a.X, a.Y));
        Assert.Equal(TestPools.FixtureHp - 10, a.Hp);   // no shield on a spear: the sword's 10 in full
        Assert.Equal(TestPools.FixtureMana - 10, a.Mana);
        Assert.Equal(((ActorState)a, 10, 10, 0, "hoplites_wall"), Assert.Single(spent));
        var (_, hit) = Assert.Single(hits);
        Assert.Null(hit.Displace);
        Assert.Equal(10, hit.DefenderManaToSpend);

        a.Mana = 9;
        CombatRules.Resolve(turns.Events, sword, a, sword.Weapon, Distance(sword, a), () => 10);
        Assert.Equal(At(5, 4), (a.X, a.Y));
        Assert.Equal(9, a.Mana);
        Assert.Single(spent);

        // A blow that kills moves nobody: there is no shove to refuse, and the corpse pays nothing.
        a.Mana = TestPools.FixtureMana;
        a.Hp = 10;
        CombatRules.Resolve(turns.Events, sword, a, sword.Weapon, Distance(sword, a), () => 10);
        Assert.Equal((false, TestPools.FixtureMana, 0), (a.Alive, a.Mana, hits[^1].Hit.DefenderManaToSpend));
        Assert.Single(spent);

        // Same soul, other side: a dummy holding the Wall refuses the Tower Guard's shove and pays from its own pool.
        var b = Char("B", 5, 5, "tower_guard");
        var wall = Enemy(5, 6, TestWeapons.Get("hoplites_wall"));
        var enemies = new TurnSystem(grid, new[] { b }, new[] { wall }, () => 10);
        Assert.True(enemies.TryAttack(b, wall));
        Assert.Equal(At(5, 6), (wall.X, wall.Y));
        Assert.Equal(wall.UsableMaxMana - 10, wall.Mana);   // the Wall's Immovable reserves 25 of the dummy's flat 100
        Assert.Equal(200 - 10, wall.Hp);

        // Through a real enemy phase: the dummy's three swings each shove at the line, and the line holds
        // three times, 10 a time.
        var d = Char("D", 5, 5, "hoplites_wall");
        var dummy = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        var charged = new TurnSystem(grid, new[] { d }, new[] { dummy }, () => 10);
        var chargedHits = HitProbe(charged);
        charged.EndTurn();
        Advance(charged, 4f);
        Assert.Equal(TurnPhase.Player, charged.Phase);
        Assert.Equal(3, chargedHits.Count);
        Assert.All(chargedHits, h => Assert.Equal((false, null, 10), (h.Hit.OnDefendersTurn, h.Hit.Displace, h.Hit.DefenderManaToSpend)));
        Assert.Equal((At(5, 5), TestPools.FixtureHp - 30, TestPools.FixtureMana - 30), ((d.X, d.Y), d.Hp, d.Mana));

        // In the wielder's own phase: the Riposte Blade answers the Wall's blocked thrust with a counter
        // that shoves it a tile, and nothing is paid — the turn system marks the counter as landing on the
        // defender's own turn, and the thrust as landing in the blade's opponent's phase.
        var c = Char("C", 5, 5, "hoplites_wall");
        var blade = Enemy(5, 6, TestWeapons.Get("riposte_blade"));
        var own = new TurnSystem(grid, new[] { c }, new[] { blade }, () => 10);
        var ownHits = HitProbe(own);
        var ownSpent = ManaProbe(own);
        Assert.True(own.TryAttack(c, blade));
        Assert.Equal(2, ownHits.Count);
        Assert.Equal(((ActorState)blade, false, true), (ownHits[0].Target, ownHits[0].Hit.OnDefendersTurn, ownHits[0].Hit.Blocked));
        Assert.Equal(((ActorState)c, true, 1), (ownHits[1].Target, ownHits[1].Hit.OnDefendersTurn, ownHits[1].Hit.Displace!.Tiles));
        Assert.Equal((At(5, 4), TestPools.FixtureHp - 10, TestPools.FixtureMana), ((c.X, c.Y), c.Hp, c.Mana));
        Assert.Empty(ownSpent);
    }

    // ── Piercing ─────────────────────────────────────────────────────────────

    [Fact]
    public void Piercing_ContinuesToNextTargetInLine_Rerolled()
    {
        // The Stormcrow's shot does not stop at the first body: for 6 it continues to the next body on the
        // line beyond it — within a tile of the shot's ray, within range and sight — as a fresh hit on a
        // fresh roll, at that body's own distance. A body a row over is not in line; one past the bow's
        // reach is not reached; and the body the shot was carried to does not carry it on again. A shot
        // with nobody on its line has nowhere to go, and pays nothing.
        var grid = new int[20, 20];
        var a = Char("A", 5, 2, "stormcrow");
        var first = Enemy(5, 6);       // four tiles: surface 100, the fourth tile pays Longshot's 3
        var second = Enemy(5, 10);     // eight tiles: surface 228, five paying tiles
        var beside = Enemy(8, 6);      // three rows off the line
        var beyond = Enemy(5, 14);     // twelve tiles: surface 356, past the bow's 320
        var rolls = new Queue<int>([10, 19]);
        var turns = new TurnSystem(grid, new[] { a }, new[] { first, second, beside, beyond }, () => rolls.Dequeue());
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);
        var carried = new List<(ActorState Target, bool Pierces)>();   // Piercing settles on DamageDealt, after the hit
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 9), "probe", (p, _, o) =>
        {
            carried.Add((o, p.Pierces));
            return p;
        });

        Assert.True(turns.TryAttack(a, first));

        Assert.Equal(2, hits.Count);
        Assert.Same(first, hits[0].Target);
        Assert.Equal((10, false, 8, false), (hits[0].Hit.Roll, hits[0].Hit.FromPierce, hits[0].Hit.Dealt, hits[0].Hit.IsCrit));
        Assert.Same(second, hits[1].Target);
        Assert.Equal((19, true, 40, true), (hits[1].Hit.Roll, hits[1].Hit.FromPierce, hits[1].Hit.Dealt, hits[1].Hit.IsCrit));   // (5 + 5 x 3) x2 on the crit
        Assert.Equal(new[] { ((ActorState)first, true), ((ActorState)second, false) }, carried);   // carried on once, and no further
        Assert.Equal(200 - 8, first.Hp);
        Assert.Equal(200 - 40, second.Hp);
        Assert.Equal(200, beside.Hp);
        Assert.Equal(200, beyond.Hp);
        Assert.Equal(((ActorState)a, 6, 6, 0, "stormcrow"), Assert.Single(spent));
        Assert.Equal(TestPools.FixtureMana - 6, a.Mana);
        Assert.Equal(1, turns.AttacksThisTurn(a));   // the continuation is the same shot

        // Unable to pay, the shot stops at the first body and nothing is spent.
        var b = Char("B", 5, 2, "stormcrow");
        b.Mana = 5;
        var e1 = Enemy(5, 6);
        var e2 = Enemy(5, 10);
        var dry = new TurnSystem(grid, new[] { b }, new[] { e1, e2 }, () => 10);
        var dryHits = HitProbe(dry);
        Assert.True(dry.TryAttack(b, e1));
        Assert.Single(dryHits);
        Assert.Equal((5, 200), (b.Mana, e2.Hp));

        // With nobody on the line — a lone target, the only other bodies a row over or past the bow's reach —
        // the shot has nowhere to go: the line read at impact names nobody, Piercing does not fire, and
        // nothing is paid, however full the pool.
        var c = Char("C", 5, 2, "stormcrow");
        var lone = Enemy(5, 6);
        var offLine = Enemy(8, 6);
        var farAway = Enemy(5, 14);
        var clear = new TurnSystem(grid, new[] { c }, new[] { lone, offLine, farAway }, () => 10);
        var clearHits = HitProbe(clear);
        var clearSpent = ManaProbe(clear);
        var clearDealt = new List<DamagePayload>();
        clear.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            clearDealt.Add(p);
            return p;
        });
        Assert.True(clear.TryAttack(c, lone));
        Assert.Single(clearHits);
        var dealt = Assert.Single(clearDealt);
        Assert.Null(dealt.NextInLine);
        Assert.Equal((false, 0), (dealt.Pierces, dealt.ManaToSpend));
        Assert.Empty(clearSpent);
        Assert.Equal((TestPools.FixtureMana, 200 - 8, 200, 200), (c.Mana, lone.Hp, offLine.Hp, farAway.Hp));

        // The handler itself: a payload whose line names nobody, or a body already dead, settles nothing;
        // one naming a living body settles the continuation for its 6.
        var table = Compiled();
        var shot = DamagePayload.Initial(c.EquippedWeapon!, 10, 100) with { WeaponShare = 8, Dealt = 8, Taken = 8 };
        var corpse = Enemy(5, 10);
        corpse.Alive = false;
        Assert.Equal((false, 0), Settled(shot));
        Assert.Equal((false, 0), Settled(shot with { NextInLine = corpse }));
        Assert.Equal((true, 6), Settled(shot with { NextInLine = Enemy(5, 10) }));

        (bool Pierces, int ManaToSpend) Settled(DamagePayload payload)
        {
            var settled = table.Raise(GameEvent.DamageDealt, payload, c, lone);
            return (settled.Pierces, settled.ManaToSpend);
        }
    }

    // ── Vampiric, Serrated, Bleeding ─────────────────────────────────────────

    [Fact]
    public void Vampiric_HealsOnePerTier_PerInstance_Costs2()
    {
        // Vampiric fires on damage dealt and heals a flat 1 per tier — every instance, whatever it dealt,
        // Ward-swallowed or not — for 2 a trigger; a tier-3 drink is 3 HP. Short of the 2 it fires at the
        // fraction it can pay; dry, it is a non-event.
        var grid = new int[20, 20];
        var a = Holding("A", 5, 5, TestWeapons.Enchanted("Leech", 40, 15, 30, "vampiric"));
        a.Hp = 50;
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var spent = ManaProbe(turns);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(52, a.Hp);
        Assert.Equal(new[] { 1, 1 }, healed);
        Assert.Equal(TestPools.FixtureMana - 4, a.Mana);
        Assert.Equal(2, spent.Count);
        Assert.All(spent, s => Assert.Equal(((ActorState)a, 2, 2, 0, "test_leech"), s));
        Assert.Equal(200 - 30, enemy.Hp);

        var b = Holding("B", 5, 5, Souled("Deep Leech", 40, 15, 30, ("vampiric", 3)));
        b.Hp = 50;
        var warded = Enemy(5, 6);
        warded.ApplyStatus(Ward, null, 50);   // the hit is dealt and all but the floor swallowed: still an instance
        var deep = new TurnSystem(grid, new[] { b }, new[] { warded }, () => 10);
        Assert.True(deep.TryAttack(b, warded));
        Assert.Equal((53, TestPools.FixtureMana - 2, 199, 36), (b.Hp, b.Mana, warded.Hp, warded.StatusLevel(Ward)));

        b.Mana = 1;
        Assert.True(deep.TryAttack(b, warded));
        Assert.Equal((54, 0), (b.Hp, b.Mana));   // 3 x 1 / 2 = 1 HP for the 1 it had
        Assert.True(deep.TryAttack(b, warded));
        Assert.Equal((54, 0), (b.Hp, b.Mana));   // nothing left: a non-event
    }

    [Fact]
    public void Serrated_AppliesBleedingFromWeaponShare_NotEnchantmentShare()
    {
        // Serrated leaves Bleeding at BleedPercent (20%) of the WEAPON's share of the hit, never the total:
        // a hit whose weapon share is 10 beside an enchantment share of 10 bleeds 2, not 4, and its trigger
        // rides the same ladder — 3 / 3 = 1 for two levels. Through the turn system the Nameless Knife's
        // 15 leaves 3 levels for 2, and the wound ticks at the victim's turn end.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var serrated = Holding("S", 5, 5, Souled("Saw", 40, 15, 30, ("serrated", 1)));
        var victim = Enemy(5, 6);
        var dealt = DamagePayload.Initial(serrated.EquippedWeapon!, 10, 32) with { WeaponShare = 10, EnchantmentShare = 10, Dealt = 20, Taken = 20 };
        var settled = table.Raise(GameEvent.DamageDealt, dealt, serrated, victim);
        Assert.Equal(new StatusApplication(Bleeding, null, 2), Assert.Single(settled.ApplyToDefender));
        Assert.Equal(1, settled.ManaToSpend);
        Assert.Empty(victim.StatusEffects);   // settled, not written

        var grid = new int[20, 20];
        var a = Holding("A", 5, 5, Souled("Saw", 40, 15, 30, ("serrated", 1)));
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var spent = ManaProbe(turns);
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(3, enemy.StatusLevel(Bleeding));
        Assert.Equal(((ActorState)a, 2, 2, 0, "test_saw"), Assert.Single(spent));
        Assert.Equal(200 - 15, enemy.Hp);

        (enemy.X, enemy.Y) = At(15, 15);   // sits the enemy phase out far from everyone and unseen
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(200 - 15 - 3, enemy.Hp);
        Assert.Equal(2, enemy.StatusLevel(Bleeding));

        // It reads Dealt — after Block, before Ward — and of that the blade's part: 15 into Block 3 is 12
        // of the weapon's, two levels for one, however much of it Ward then swallowed (11 of the 12:
        // the floor never lets it take the last point).
        var warded = table.Raise(GameEvent.DamageDealt,
            DamagePayload.Initial(serrated.EquippedWeapon!, 10, 32) with { WeaponShare = 15, Absorbed = 3, Dealt = 12, WardSpent = 11, Taken = 1 },
            serrated, victim);
        Assert.Equal((12, new StatusApplication(Bleeding, null, 2), 1), (warded.WeaponDealt, Assert.Single(warded.ApplyToDefender), warded.ManaToSpend));
        var b = Holding("B", 5, 5, Souled("Saw", 40, 15, 30, ("serrated", 1)));
        var shield = Enemy(5, 6, TestWeapons.Get("arming_sword"));   // Block x1: 3 absorbed
        var blocked = new TurnSystem(grid, new[] { b }, new[] { shield }, () => 10);
        var blockedSpent = ManaProbe(blocked);
        Assert.True(blocked.TryAttack(b, shield));
        Assert.Equal((200 - 12, 2), (shield.Hp, shield.StatusLevel(Bleeding)));
        Assert.Equal(((ActorState)b, 1, 1, 0, "test_saw"), Assert.Single(blockedSpent));

        // A hit that kills leaves nobody to bleed, and the wound is neither settled nor paid for.
        var c = Holding("C", 5, 5, Souled("Saw", 40, 15, 30, ("serrated", 1)));
        var frail = Enemy(5, 6, hp: 10);
        var lethal = new TurnSystem(grid, new[] { c }, new[] { frail }, () => 10);
        var lethalSpent = ManaProbe(lethal);
        Assert.True(lethal.TryAttack(c, frail));
        Assert.False(frail.Alive);
        Assert.Empty(lethalSpent);
        Assert.Equal(TestPools.FixtureMana, c.Mana);
    }

    [Fact]
    public void Bleeding_DoesNotTriggerVampiric()
    {
        // The knife's swing drinks; the wound it left does not. A Bleeding tick is damage the status deals,
        // on the enemy's turn, and no instance of the wielder's.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "flensing_knife_unique");
        a.Hp = 50;
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(53, a.Hp);                       // the swing's drink: tier 3
        Assert.Equal(3, enemy.StatusLevel(Bleeding));
        Assert.Equal(200 - 15, enemy.Hp);

        (enemy.X, enemy.Y) = At(15, 15);
        turns.NotifyActorMoved(enemy, MoveKind.Forced);
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(200 - 15 - 3, enemy.Hp);         // the wound ticked
        Assert.Equal(53, a.Hp);                       // and nobody drank from it
        Assert.Equal(new[] { 3 }, healed);
    }

    // ── Overheal ─────────────────────────────────────────────────────────────

    [Fact]
    public void Overheal_RenewalTickOnFullWielder_BecomesWard_AllyNotYet()
    {
        // The Nameless Renewal's Overheal: a Regeneration tick landing on its wielder at full HP is banked
        // — the whole surplus into the hidden pool, for 8 less Resonant's tenth: 7 — and folded into Ward at
        // five to one in the same chain. The soul answers the surplus of whoever holds it. The Renewal home's
        // ally half — a tick on a topped-up ally shielding that ally — is deferred to Phase 3 by ruling: a
        // tick carries no reference to whoever cast the Regeneration it came from (no status remembers its
        // applier), so an ally holding a plain staff overflows the same five and keeps nothing. Pinned here
        // so the deferral is visible, not silent.
        var grid = new int[20, 20];
        var healer = Char("H", 5, 5, "staff_of_renewal_unique");
        var ally = Char("A", 5, 6, "staff_of_renewal");
        healer.ApplyStatus(Regeneration, null, 5);   // as five Renewal casts leave it, at full HP
        ally.ApplyStatus(Regeneration, null, 5);
        var turns = new TurnSystem(grid, new[] { healer, ally }, new[] { Enemy(15, 15) }, () => 10);
        var spent = ManaProbe(turns);
        var above = new List<(ActorState Self, int Overflow, int ManaToSpend)>();
        turns.Events.On<HealPayload>(GameEvent.HealingAboveFull, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            above.Add((s, p.Overflow, p.ManaToSpend));
            return p;
        });

        turns.EndTurn();

        Assert.Equal((TestPools.FixtureHp, 1, 0, 4), (healer.Hp, healer.StatusLevel(Ward), healer.StatusLevel(OverhealPool), healer.StatusLevel(Regeneration)));
        Assert.Equal(TestPools.FixtureMana - 7, healer.Mana);
        Assert.Equal(((ActorState)healer, 7, 7, 0, "staff_of_renewal_unique"), Assert.Single(spent));
        Assert.Equal((TestPools.FixtureHp, 0, 0, 4), (ally.Hp, ally.StatusLevel(Ward), ally.StatusLevel(OverhealPool), ally.StatusLevel(Regeneration)));
        Assert.Equal(TestPools.FixtureMana, ally.Mana);
        Assert.Equal(new[] { ((ActorState)healer, 5, 7), ((ActorState)ally, 5, 0) }, above);

        // The chain, printed for the healer: Overheal at attachment index 1 ahead of the pool's fold at (1,0).
        Assert.Equal(new[] { "HealingAboveFull (0,0) regeneration@0", "HealingAboveFull (0,1) overheal@1", "HealingAboveFull (1,0) OverhealPool" },
            Compiled().HandlersFor(GameEvent.HealingAboveFull, healer).Select(h => h.ToString()));
    }

    // ── Burning ──────────────────────────────────────────────────────────────

    [Fact]
    public void Burning_LingersOnlyWithFlamingPresent_OncePerCast()
    {
        // The Nameless Nova's Flaming lingers as Searing/Flaming on every body the circle catches — Flaming's
        // 20% of the wand's 8, one level each — and the burn is paid for once per cast, on its ladder over
        // those levels (1), beside the element's 4 and the cast's 18: three bodies, one payment. Without the
        // element paid the burn is quiet and free; without the element present it is refused content.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova_unique");
        var ring = new[] { Enemy(5, 6), Enemy(5, 4), Enemy(4, 5) };
        var turns = new TurnSystem(grid, new[] { a }, ring, () => 10);
        var spent = ManaProbe(turns);
        var hits = HitProbe(turns);
        var casts = new List<CastPayload>();
        turns.Events.On<CastPayload>(GameEvent.Cast, new HandlerPriority(9, 9), "probe", (p, _, _) => { casts.Add(p); return p; });

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        var cast = Assert.Single(casts);
        Assert.Equal((Flaming, 5), (cast.Type, cast.ManaToSpend));
        Assert.Equal(new[] { "flaming", "burning" }, cast.Fired);
        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal((Flaming, 8, new StatusApplication(Searing, Flaming, 1)), (h.Hit.Type, h.Hit.Dealt, Assert.Single(h.Hit.ApplyToDefender))));
        Assert.All(hits, h => Assert.Equal(new[] { "flaming", "burning" }, h.Hit.CastFired));
        Assert.All(ring, e => Assert.Equal((200 - 8, 1), (e.Hp, e.StatusLevel(Searing, Flaming))));
        Assert.Equal(((ActorState)a, 18 + 5, 18 + 5, 0, "wand_of_the_nova_unique"), Assert.Single(spent));
        Assert.Equal(TestPools.FixtureMana - 23, a.Mana);

        // With mana for the cast alone the element is a non-event, so the burn has nothing to linger.
        var b = Char("B", 5, 5, "wand_of_the_nova_unique");
        b.Mana = 18;
        var lone = Enemy(5, 6);
        var dry = new TurnSystem(grid, new[] { b }, new[] { lone }, () => 10);
        var dryCasts = new List<CastPayload>();
        dry.Events.On<CastPayload>(GameEvent.Cast, new HandlerPriority(9, 9), "probe", (p, _, _) => { dryCasts.Add(p); return p; });
        Assert.True(dry.TryCastArea(b, (b.X, b.Y)));
        Assert.Equal((None, 0), (Assert.Single(dryCasts).Type, dryCasts[0].ManaToSpend));
        Assert.True(dryCasts[0].Fired.IsDefaultOrEmpty);
        Assert.Equal((200 - 8, 0, 0), (lone.Hp, lone.StatusLevel(Searing), b.Mana));

        // A crit doubles the wand's figure and the burn with it: 16 at 20% is 3 levels, paid once at the ladder's 2 less 10%.
        var c = Char("C", 5, 5, "wand_of_the_nova_unique");
        var seared = Enemy(5, 6);
        var crit = new TurnSystem(grid, new[] { c }, new[] { seared }, () => 20);
        Assert.True(crit.TryCastArea(c, (c.X, c.Y)));
        Assert.Equal((200 - 16, 3), (seared.Hp, seared.StatusLevel(Searing, Flaming)));
        Assert.Equal(TestPools.FixtureMana - 9 - 4 - 1, c.Mana);

        // The burn is a status like any other: it ticks at the victim's turn end, the element as its key.
        (seared.X, seared.Y) = At(15, 15);
        crit.NotifyActorMoved(seared, MoveKind.Forced);
        crit.EndTurn();
        Advance(crit, 3f);
        Assert.Equal((200 - 16 - 3, 2), (seared.Hp, seared.StatusLevel(Searing, Flaming)));

        // Without Flaming on the weapon, content refuses the burn: a lingering element needs its element.
        var ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments,
            new WeaponsData(ContentDefaults.Weapons.Weapons.Select(w => w.Id == "wand_of_the_nova_unique" ? w with { Enchantments = [new EnchantmentRef("burning")] } : w).ToList())));
        Assert.Equal(("wand_of_the_nova_unique", ContentValidator.RuleEnchantmentDependency), (ex.EntryId, ex.Rule));

        // And transfer the Flaming off, as a later transfer (section 6.5) will leave a weapon built past the
        // validator, and the burn goes quiet: the cast lands untyped, nothing lingers, nothing is paid for it.
        var novaDef = GameContent.Current.Weapons["wand_of_the_nova_unique"];
        var stripped = new Weapon(novaDef with { Enchantments = [new EnchantmentRef("burning")] },
            [new Enchantment(GameContent.Current.Enchantments["burning"], 1)]);
        var e = Holding("E", 5, 5, stripped);
        var quiet = Enemy(5, 6);
        var transferred = new TurnSystem(grid, new[] { e }, new[] { quiet }, () => 10);
        Assert.True(transferred.TryCastArea(e, (e.X, e.Y)));
        Assert.Equal((200 - 8, 0, TestPools.FixtureMana - 18), (quiet.Hp, quiet.StatusLevel(Searing), e.Mana));
    }

    [Fact]
    public void LingeringElement_IsTheElementsLadder_AndEveryRowIsData()
    {
        // How deep a burn goes is the element's ladder, not the soul's (section 1.5): levels are the element's
        // ApplyPercent of the element damage, and nothing else. At elementDamage 30, 10% is 3 levels (6 over 3
        // turns), 25% is 7 (28 over 7), 50% is 15 (120 over 15, outlasting the fight); at 10%, a hit for 100
        // stacks 10 levels and one for 1000 stacks 100. The burn's price rides the same ladder, L(L+1)/2 over 3.
        var flaming = GameContent.Current.Enchantments["flaming"];
        var burning = new Enchantment(GameContent.Current.Enchantments["burning"], 1);
        foreach (var (percent, levels, total) in new[] { (10, 3, 6), (25, 7, 28), (50, 15, 120) })
        {
            Assert.Equal(levels, new Enchantment(flaming with { ApplyPercent = percent }, 1).LevelsFor(30));
            Assert.Equal(total, levels * (levels + 1) / 2);
            Assert.Equal(Math.Max(1, total / 3), burning.TriggerCostFor(levels));
        }
        var ten = new Enchantment(flaming with { ApplyPercent = 10 }, 1);
        Assert.Equal((10, 100), (ten.LevelsFor(100), ten.LevelsFor(1000)));

        // The doc's own case (section 1.5): a tier-6 Flaming on a wand dealing 30 applies 3 levels at 10% — 6
        // damage over 3 turns, and a six-target Nova totals 36. The burn reads the element damage alone: the
        // tier is already in the 30, never a second factor on the levels, which is what a catalogue entry's own
        // application would make of it (18).
        var sixth = new Enchantment(flaming with { ApplyPercent = 10 }, 6);
        Assert.Equal((3, 18), (sixth.LevelsOffSource(30), sixth.LevelsFor(30)));

        // Level the Flaming and the Searing it leaves goes deeper with it — through the element damage the tier
        // builds, never by multiplying the levels. Phase 1 prices an element's own share at nothing (its potency
        // is 0; the tier's contribution and INT scaling are section 3.3's), so a tier-2 Flaming still leaves 1
        // level off the wand's 8, as tier 1 does. Tier is earned by casting (section 3.3), so a tier-2 Flaming
        // is the weapon as play leaves it rather than as it drops: built here directly, past the content
        // validator, which holds an arriving unique to one shape.
        var grid = new int[20, 20];
        var nova = GameContent.Current.Weapons["wand_of_the_nova_unique"];
        var levelled = new Weapon(nova with { Enchantments = [new EnchantmentRef("flaming", 2), new EnchantmentRef("burning")] },
            [new Enchantment(flaming, 2), burning]);
        {
            var a = Holding("A", 5, 5, levelled);
            var target = Enemy(5, 6);
            var turns = new TurnSystem(grid, new[] { a }, new[] { target }, () => 10);
            Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
            Assert.Equal((200 - 8, 1), (target.Hp, target.StatusLevel(Searing, Flaming)));
        }

        // Once the share is priced — a test Flaming whose 20% of potency 25 adds 5 a tier beside the wand's 8 —
        // levelling it deepens the burn through that damage: 13 at tier 1 is 2 levels, 18 at tier 2 is 3,
        // where a tier factor on the levels would have made tier 2 seven.
        var priced = flaming with { Potency = 25 };
        foreach (var (tier, dealt, levels) in new[] { (1, 13, 2), (2, 18, 3) })
        {
            var weapon = new Weapon(nova with { Enchantments = [new EnchantmentRef("flaming", tier), new EnchantmentRef("burning")] },
                [new Enchantment(priced, tier), burning]);
            var a = Holding("A", 5, 5, weapon);
            var target = Enemy(5, 6);
            var turns = new TurnSystem(grid, new[] { a }, new[] { target }, () => 10);
            Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
            Assert.Equal((200 - dealt, levels), (target.Hp, target.StatusLevel(Searing, Flaming)));
        }

        // The lingering table's other rows are data, not code: a Shocking Nova unique leaves Mire, the staff's
        // own effect delivered over an area, on every body its circle catches, at Shocking's 25% of the 8: two
        // levels each, and the burn's price paid once for the cast beside the element's.
        var shock = new EnchantmentDef("test_static", "Static", EffectKind.LingeringElement, TargetSide.Enemy, Lock: 20, Trigger: null,
            Potency: 0, ApplyPercent: 100, Applies: Mire, DamageType: Shocking, Unique: true);
        var requires = new Dictionary<string, string[]>(ContentDefaults.Restricted.Requires, StringComparer.Ordinal) { ["test_static"] = ["shocking"] };
        var stormNova = ContentDefaults.Weapons.Weapons.Single(w => w.Id == "wand_of_the_nova_unique") with
        {
            Id = "test_storm_nova", Name = "Storm Nova", Enchantments = [new EnchantmentRef("shocking"), new EnchantmentRef("test_static")],
        };
        using (TestContent.Use(
            restricted: ContentDefaults.Restricted with { NeverRolled = [.. ContentDefaults.Restricted.NeverRolled, "test_static"], Requires = requires },
            enchantments: new EnchantmentsData([.. ContentDefaults.Enchantments.Enchantments, shock]),
            weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons, stormNova])))
        {
            var a = Char("A", 5, 5, "test_storm_nova");
            var ring = new[] { Enemy(5, 6), Enemy(5, 4), Enemy(4, 5) };
            var turns = new TurnSystem(grid, new[] { a }, ring, () => 10);
            var spent = ManaProbe(turns);
            Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
            Assert.All(ring, r => Assert.Equal((200 - 8, 2), (r.Hp, r.StatusLevel(Mire))));
            Assert.Equal(((ActorState)a, 18 + 4 + 1, 18 + 4 + 1, 0, "test_storm_nova"), Assert.Single(spent));
        }
    }

    // ── Siphon and Sturdy ────────────────────────────────────────────────────

    [Fact]
    public void Siphon_RestoresFifteenForFive_OnKill()
    {
        // The Widowmaker's kill pays 5 and restores 15 — one record, spent then restored, never past the pool:
        // net +10, and net nothing at a full pool. A tick death names no weapon and refunds nobody.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "widowmaker");
        a.Mana = 50;
        var enemy = Enemy(5, 6, hp: 5);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var spent = ManaProbe(turns);
        int defeated = 0;
        turns.EnemyDefeated += _ => defeated++;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.False(enemy.Alive);
        Assert.Equal(1, defeated);
        Assert.Equal(60, a.Mana);
        Assert.Equal(((ActorState)a, 5, 5, 15, "widowmaker"), Assert.Single(spent));

        var b = Char("B", 5, 5, "widowmaker");
        var full = Enemy(5, 6, hp: 5);
        var capped = new TurnSystem(grid, new[] { b }, new[] { full }, () => 10);
        Assert.True(capped.TryAttack(b, full));
        Assert.Equal(TestPools.FixtureMana, b.Mana);

        // Unable to pay the 5, it fires nothing and restores nothing.
        var c = Char("C", 5, 5, "widowmaker");
        c.Mana = 4;
        var cheap = Enemy(5, 6, hp: 5);
        var dry = new TurnSystem(grid, new[] { c }, new[] { cheap }, () => 10);
        Assert.True(dry.TryAttack(c, cheap));
        Assert.Equal(4, c.Mana);

        // The handler on a tick death: self and other are the corpse, the weapon is null, and nothing is settled.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var corpse = Enemy(5, 6, TestWeapons.Get("widowmaker"));
        corpse.Alive = false;
        var settled = table.Raise(GameEvent.Killed, new KillPayload(Weapon: null, 5, 5), corpse, corpse);
        Assert.Equal((0, 0), (settled.ManaToSpend, settled.ManaRestored));
    }

    [Fact]
    public void Sturdy_SurvivesAtOneHp_Pays40()
    {
        // The Bulwark refuses to let you die: a lethal blow leaves its wielder at 1 HP for 40 out of the
        // wielder's own pool — what was dealt stands, what reached HP is what one point could bear — and
        // again at 1 HP, taking nothing; unable to pay, the wielder dies as anyone would.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "the_bulwark");
        a.Hp = 10;
        var maul = Enemy(5, 6, TestWeapons.Make("Maul", 40, 30, 30));
        var turns = new TurnSystem(grid, new[] { a }, new[] { maul }, () => 10);
        var spent = ManaProbe(turns);
        var hits = new List<AttackResolution>();
        turns.CharacterHit += (_, r) => hits.Add(r);
        int died = 0;
        turns.CharacterDied += _ => died++;
        Assert.Equal(new HandlerPriority(6, 1), EnchantmentBehaviours.SturdyPriority);

        CombatRules.Resolve(turns.Events, maul, a, maul.Weapon, Distance(maul, a), () => 10);
        Assert.Equal((21, 9, 9), (hits[^1].Dealt, hits[^1].Taken, hits[^1].Blocked));   // 30 into Block 9 is 21 dealt; 9 reach HP, 12 are spared
        Assert.Equal((1, true, TestPools.FixtureMana - 40), (a.Hp, a.Alive, a.Mana));
        Assert.Equal(((ActorState)a, 40, 40, 0, "the_bulwark"), Assert.Single(spent));

        CombatRules.Resolve(turns.Events, maul, a, maul.Weapon, Distance(maul, a), () => 10);
        Assert.Equal((21, 0), (hits[^1].Dealt, hits[^1].Taken));
        Assert.Equal((1, true, TestPools.FixtureMana - 80), (a.Hp, a.Alive, a.Mana));

        CombatRules.Resolve(turns.Events, maul, a, maul.Weapon, Distance(maul, a), () => 10);   // 20 left: short of the 40
        Assert.Equal(21, hits[^1].Taken);
        Assert.Equal((0, false, TestPools.FixtureMana - 80), (a.Hp, a.Alive, a.Mana));
        Assert.Equal(2, spent.Count);
        Assert.Equal(0, died);   // a raw resolve holds the death for the turn system's next outermost raise
        turns.EndTurn();         // the wipe: the boundary raise runs the held death's consequences
        Assert.Equal((1, TurnPhase.GameOver), (died, turns.Phase));

        // A blow that is not lethal fires nothing: the shield takes it as any shield would.
        var b = Char("B", 5, 5, "the_bulwark");
        var sword = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        var calm = new TurnSystem(grid, new[] { b }, new[] { sword }, () => 10);
        CombatRules.Resolve(calm.Events, sword, b, sword.Weapon, Distance(sword, b), () => 10);
        Assert.Equal((TestPools.FixtureHp - 1, TestPools.FixtureMana), (b.Hp, b.Mana));   // 10 into Block 9
    }

    // ── The placeholders ─────────────────────────────────────────────────────

    [Fact]
    public void Weightless_And_Momentum_AreInertPlaceholders()
    {
        // Feathered Death's Weightless and Shieldbreaker's Momentum are declared souls in the tables — printed
        // in their chains at their attachment index — with no behaviour yet: a throw costs what it costs, a
        // kill refunds nothing, and neither pays a trigger, since nothing fired.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "feathered_death");
        var b = Char("B", 5, 7, "shieldbreaker");
        var target = Enemy(5, 6, hp: 5);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { target }, () => 10);
        var spent = ManaProbe(turns);
        Assert.Equal(new[] { "DamageDealt (0,0) weightless@0" }, turns.Events.HandlersFor(GameEvent.DamageDealt, a).Select(h => h.ToString()));
        Assert.Equal(new[] { "Killed (0,0) momentum@0" }, turns.Events.HandlersFor(GameEvent.Killed, b).Select(h => h.ToString()));

        Assert.True(turns.TryAttack(a, target));
        Assert.Equal(GameConstants.MaxDistance - 15, a.DistLeft);
        Assert.Equal(TestPools.FixtureMana, a.Mana);
        Assert.False(target.Alive);

        var victim = Enemy(5, 6, hp: 5);
        var axe = new TurnSystem(grid, new[] { b }, new[] { victim }, () => 10);
        Assert.True(axe.TryAttack(b, victim));
        Assert.False(victim.Alive);
        Assert.Equal(GameConstants.MaxDistance - 60, b.DistLeft);
        Assert.Equal(TestPools.FixtureMana, b.Mana);
        Assert.Empty(spent);
    }

    [Fact]
    public void Echoing_IsTheThirdPlaceholder_AndHoldsNoTableRowAtAll()
    {
        // §3.3's Echoing is declared with no behaviour for the same reason as those two — "the weapon's class
        // feature triggers once more" has no meaning for six of the eight classes — with one difference worth
        // reading off the tables. Weightless and Momentum hold real rows, inert ones, so FiresOn answers true
        // for both; Echoing holds no row on any event, and the event it names (AttackDeclared) has no chain
        // because nothing raises it. Both halves are in open-questions.md rather than guessed at here.
        //
        // It still prints in the loops' chains like any attached entry: the chain lists what the loop will
        // walk for this wielder, which is attachment order, and whether a walked entry does anything is the
        // table's answer and not the chain's.
        var grid = new int[20, 20];
        var a = Holding("A", 5, 5, Souled("Echoing Knife", 40, 15, 30, ("echoing", 1)));
        var target = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { target }, () => 10);
        var spent = ManaProbe(turns);

        foreach (var evt in Enum.GetValues<GameEvent>())
            Assert.False(EnchantmentBehaviours.FiresOn(evt, EffectKind.Echoing), $"{evt} runs it");
        Assert.Equal(new[] { "DamageDealt (0,0) echoing@0" }, turns.Events.HandlersFor(GameEvent.DamageDealt, a).Select(h => h.ToString()));
        Assert.Empty(turns.Events.HandlersFor(GameEvent.AttackDeclared));

        Assert.True(turns.TryAttack(a, target));
        Assert.Equal(TestPools.FixtureMana, a.Mana);
        Assert.Empty(spent);
    }

    // ── Handlers never write ─────────────────────────────────────────────────

    [Fact]
    public void SoulHandlers_SettleOnThePayload_AndWriteNothing()
    {
        // Rule 4 over the chains the souls fill: every handler on DamageDealt, Killed and HealingAboveFull —
        // the loop with its entries, the pool's fold — hands back a payload with its effect settled on it and
        // leaves both actors exactly as it found them; the appliers write, once. The defender's two souls
        // at (6,1) and (8,3) are DamageTaken handlers, which EventTableTests walks the same way.
        var table = Compiled();
        var knife = Char("A", 5, 5, "flensing_knife_unique");
        knife.ApplyStatus(OverhealPool, null, 4);
        knife.Mana = 60;
        var reaper = Enemy(5, 6, TestWeapons.Get("widowmaker"));
        reaper.ApplyStatus(Bleeding, null, 2);
        reaper.Mana = 40;

        var dealt = Walk(table.Chain<DamagePayload>(GameEvent.DamageDealt),
            DamagePayload.Initial(knife.EquippedWeapon!, 10, 4) with { WeaponShare = 15, Dealt = 15, Taken = 15 }, knife, reaper);
        Assert.Equal((new StatusApplication(Bleeding, null, 3), 3, 4), (Assert.Single(dealt.ApplyToDefender), dealt.HealToAttacker, dealt.ManaToSpend));

        var killed = Walk(table.Chain<KillPayload>(GameEvent.Killed), new KillPayload(reaper.Weapon, 15, 15), reaper, knife);
        Assert.Equal((5, 15), (killed.ManaToSpend, killed.ManaRestored));

        var above = Walk(table.Chain<HealPayload>(GameEvent.HealingAboveFull), new HealPayload(3, 0, 3, "vampiric"), knife, knife);
        Assert.Equal(new[] { new StatusTick(OverhealPool, null, 0, 0, -4), new StatusTick(Ward, null, 0, 0, 1) }, above.Ticks);   // 4 banked + 3 granted: one Ward
        Assert.Equal(8, above.ManaToSpend);

        static T Walk<T>(IReadOnlyList<(HandlerInfo Info, Handler<T> Handler)> chain, T payload, ActorState self, ActorState other)
        {
            Assert.NotEmpty(chain);
            foreach (var (info, handler) in chain)
            {
                string before = Snapshot(self) + " | " + Snapshot(other);
                payload = handler(payload, self, other);
                Assert.NotNull(payload);
                Assert.True(before == Snapshot(self) + " | " + Snapshot(other), $"{info} wrote to an actor");
            }
            return payload;
        }

        static string Snapshot(ActorState a)
            => $"{a.Hp}/{a.Mana}/{a.X},{a.Y}/{a.Alive}/{string.Join(",", a.StatusEffects.Select(e => $"{e.Type}:{e.Element}:{e.Levels}"))}";
    }

    // ── The cascade and the Long Candle ──────────────────────────────────────

    [Fact]
    public void EfficiencyDagger_VampiricThenOverheal_IsOneQueuedCascade()
    {
        // The legitimate cascade the queue exists for: the knife's hit lands, DamageDealt drains — Serrated
        // wounds, Vampiric drinks — the drink is a queued HealingReceived on a full wielder, its overflow a
        // queued HealingAboveFull, where Overheal grants the pool and the pool folds into Ward, and the
        // triggers are paid as they settle. Nothing recurses; every step is a generation of the one drain.
        // A drink is three points and a Ward costs five, so the first swing banks its three and the second
        // makes six: one Ward, and the remainder lost — the pool carries what one heal cannot convert.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "flensing_knife_unique");
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var log = new List<string>();
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            log.Add($"dealt {p.Dealt} heal {p.HealToAttacker} bleed {p.ApplyToDefender.Single().Levels} mana {p.ManaToSpend}");
            return p;
        });
        turns.Events.On<ManaPayload>(GameEvent.ManaSpent, new HandlerPriority(9, 9), "probe", (p, _, _) => { log.Add($"spent {p.Spent}"); return p; });
        turns.Events.On<HealPayload>(GameEvent.HealingReceived, new HandlerPriority(9, 9), "probe", (p, _, _) => { log.Add($"heal {p.Amount}/{p.Applied}/{p.Overflow}"); return p; });
        turns.Events.On<HealPayload>(GameEvent.HealingAboveFull, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            log.Add($"above {p.Overflow} ticks {string.Join(",", p.Ticks.Select(t => $"{t.Type}{t.LevelsDelta:+0;-0}"))} mana {p.ManaToSpend}");
            return p;
        });

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(new[]
        {
            "dealt 15 heal 3 bleed 3 mana 4",
            "spent 4",
            "heal 3/0/3",
            "above 3 ticks OverhealPool+3 mana 8",
            "spent 8",
        }, log);
        Assert.Equal((TestPools.FixtureHp, 0, 3), (a.Hp, a.StatusLevel(Ward), a.StatusLevel(OverhealPool)));
        Assert.Equal(TestPools.FixtureMana - 4 - 8, a.Mana);
        Assert.Equal((200 - 15, 3), (enemy.Hp, enemy.StatusLevel(Bleeding)));

        log.Clear();
        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(new[]
        {
            "dealt 15 heal 3 bleed 3 mana 4",
            "spent 4",
            "heal 3/0/3",
            "above 3 ticks OverhealPool-3,Ward+1 mana 8",
            "spent 8",
        }, log);
        Assert.Equal((TestPools.FixtureHp, 1, 0), (a.Hp, a.StatusLevel(Ward), a.StatusLevel(OverhealPool)));
        Assert.Equal(TestPools.FixtureMana - 2 * (4 + 8), a.Mana);
        Assert.Equal((200 - 30, 6), (enemy.Hp, enemy.StatusLevel(Bleeding)));

        // Printed for the wielder, DamageDealt runs its three souls in attachment order.
        Assert.Equal(new[] { "DamageDealt (0,0) serrated@0", "DamageDealt (0,1) vampiric@1", "DamageDealt (0,2) overheal@2" },
            Compiled().HandlersFor(GameEvent.DamageDealt, a).Select(h => h.ToString()));
    }

    [Fact]
    public void LongCandle_EachElementResolvesAgainstAttunementSeparately()
    {
        // The Long Candle answers two attunements: Shocking, first in order, types the cast and the weapon's
        // share; each element's own share resolves against the target's attunement by its own type at step
        // 4 — halved in a Flaming dungeon on the one element while the other still lands in full — and each
        // pays its trigger once at the cast. Priced here so the shares are visible: 4 each at 100%.
        using var _ = TestContent.Use(
            enchantments: new EnchantmentsData(ContentDefaults.Enchantments.Enchantments
                .Select(e => e.Id is "shocking" or "flaming" ? e with { Potency = 4 } : e).ToList()),
            tuning: ContentDefaults.Tuning with { ApplyPercent = new Dictionary<string, int>(StringComparer.Ordinal) { ["shocking"] = 100, ["flaming"] = 100 } });

        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "the_long_candle");
        var enemy = Enemy(5, 7);
        enemy.Attunement = Flaming;
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var hits = HitProbe(turns);
        var spent = ManaProbe(turns);
        var casts = new List<CastPayload>();
        turns.Events.On<CastPayload>(GameEvent.Cast, new HandlerPriority(9, 9), "probe", (p, _, _) => { casts.Add(p); return p; });

        Assert.True(turns.TryCastArea(a, (enemy.X, enemy.Y)));

        var cast = Assert.Single(casts);
        Assert.Equal((Shocking, 8), (cast.Type, cast.ManaToSpend));   // 4 for each element
        Assert.Equal(new[] { "shocking", "flaming" }, cast.Fired);
        var (_, hit) = Assert.Single(hits);
        Assert.Equal((Shocking, 8, 6, 14), (hit.Type, hit.WeaponShare, hit.EnchantmentShare, hit.Dealt));   // 8 + 4 (Shocking, unrelated) + 2 (Flaming, halved)
        Assert.Equal(200 - 14, enemy.Hp);
        Assert.Equal(((ActorState)a, 18 + 8, 18 + 8, 0, "the_long_candle"), Assert.Single(spent));

        // Through the chain alone against the other attunements: opposed to Flaming, the Flaming share is x1.5;
        // attuned to Shocking, the weapon's share and the Shocking share are halved while Flaming lands whole.
        static DamagePayload Settle(ActorState attacker, ActorState defender)
        {
            var table = new EventTable();
            Behaviours.RegisterAll(table);
            return table.Raise(GameEvent.DamageTaken,
                DamagePayload.Initial(attacker.EquippedWeapon!, 10, 32, Shocking, castFired: ImmutableArray.Create("shocking", "flaming")), attacker, defender);
        }
        var cold = Enemy(5, 7);
        cold.Attunement = Cold;
        Assert.Equal((8, 10, 18), (Settle(a, cold).WeaponShare, Settle(a, cold).EnchantmentShare, Settle(a, cold).Dealt));   // 8 + 4 + 6
        var shocked = Enemy(5, 7);
        shocked.Attunement = Shocking;
        Assert.Equal((4, 6, 10), (Settle(a, shocked).WeaponShare, Settle(a, shocked).EnchantmentShare, Settle(a, shocked).Dealt));   // 4 + 2 + 4
        var plain = Enemy(5, 7);
        Assert.Equal((8, 8, 16), (Settle(a, plain).WeaponShare, Settle(a, plain).EnchantmentShare, Settle(a, plain).Dealt));
    }
}
