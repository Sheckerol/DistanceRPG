using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.DamageType;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.4's wands: an area cast is one Cast that pays the cast's mana and the
/// element's trigger once, and one typed hit per actor the shape caught, each
/// through the full pipeline — the attunement chart at (3,1) before Block,
/// friendly fire off behind a constant, one roll shared by every target. In
/// the content collection because tests here swap the current content.
/// </summary>
[Collection(TestContent.Collection)]
public class WandTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId, DamageType? element = null)
    {
        var (x, y) = At(r, c);
        var ch = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        ch.Inventory[0] = TestWeapons.Get(weaponId, element);
        return ch;
    }

    /// <summary>A dummy on a tile's centre with no Block (fists) unless told otherwise, and enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? Fists, Hp = hp };
    }

    private static Weapon Fists => TestWeapons.Make("Fists", 40, 1, 0);

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

    /// <summary>Record every Cast payload as the chain settled it.</summary>
    private static List<CastPayload> CastProbe(TurnSystem turns)
    {
        var casts = new List<CastPayload>();
        turns.Events.On<CastPayload>(GameEvent.Cast, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            casts.Add(p);
            return p;
        });
        return casts;
    }

    /// <summary>Record every ManaSpent the table settles.</summary>
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

    /// <summary>The settled payload of a typed hit through a fresh compiled chain with no applier: the numbers alone, nothing written.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll, DamageType type)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits: 32, type), attacker, defender);
    }

    // ── The element is the hit's type ────────────────────────────────────────

    [Fact]
    public void Element_IsTheHitType()
    {
        // A Flaming Nova: one Cast typed Flaming by the element for one payment of its trigger (5, less
        // 10% at Resonant x1: 4) beside the cast's 18 (20 at x1), 45 movement, and one hit per body
        // caught, typed Flaming, through the pipeline: 8 into no Block.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var wand = a.EquippedWeapon!;
        var enemy = Enemy(5, 7);   // 64 off: inside the 96
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var hits = HitProbe(turns);
        var casts = CastProbe(turns);
        var spent = ManaProbe(turns);
        var resolutions = new List<AttackResolution>();
        turns.EnemyHit += (_, r) => resolutions.Add(r);
        Assert.Equal((160, 8, 45, 20, 18), (wand.Range, wand.Damage, wand.Cost, wand.ManaCost, wand.ResolvedManaCost));
        Assert.Equal(4, wand.Innate!.ResolvedTriggerCost(wand, 1));

        Assert.True(turns.CanCastArea(a, (a.X, a.Y)));
        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        var (target, hit) = Assert.Single(hits);
        Assert.Same(enemy, target);
        Assert.Equal(Flaming, hit.Type);
        Assert.Same(wand, hit.Weapon);
        Assert.Equal((8, 8, 8, 0, false), (hit.WeaponShare, hit.Dealt, hit.Taken, hit.EnchantmentShare, hit.OnAlly));
        Assert.Equal(200 - 8, enemy.Hp);
        Assert.Equal(8, Assert.Single(resolutions).Dealt);
        Assert.Equal(GameConstants.MaxDistance - 45, a.DistLeft);
        Assert.Equal(GameConstants.MaxMana - 18 - 4, a.Mana);
        Assert.Equal(((ActorState)a, 22, 22, "wand_of_the_nova"), Assert.Single(spent));
        Assert.Equal(0, turns.AttacksThisTurn(a));   // a cast is no chosen attack

        // The cast's own record: typed by the element, the trigger in ManaToSpend, the caster standing
        // in as its own target, and the element's levels nothing (its potency is Phase 3's to price).
        var cast = Assert.Single(casts);
        Assert.Equal((10, false, false, 0, 18, 4, Flaming), (cast.Roll, cast.IsCrit, cast.IsFumble, cast.Levels, cast.ManaCost, cast.ManaToSpend, cast.Type));
        Assert.Same(a, cast.Target);
        Assert.True(cast.ApplyToTarget.IsDefaultOrEmpty);

        // The four elements each type the hit with themselves; a martial swing stays untyped.
        foreach (var element in new[] { Cold, Shocking, Acidic })
        {
            var b = Char("B", 5, 5, "wand_of_the_blast", element);
            Assert.Equal(element, Settle(b, Enemy(5, 6), 10, element).Type);
        }
        var swing = Char("C", 5, 5, "weakspot_stiletto");
        Assert.Equal(None, Settle(swing, Enemy(5, 6), 10, None).Type);
    }

    [Fact]
    public void Staff_CastStaysUntyped_AndAWandRefusesTheStaffPath()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "staff_of_renewal");
        var b = Char("B", 5, 6, "weakspot_stiletto");
        var w = Char("W", 7, 5, "wand_of_the_nova", Flaming);
        var enemy = Enemy(5, 7);
        var turns = new TurnSystem(grid, new[] { a, b, w }, new[] { enemy }, () => 10);
        var casts = CastProbe(turns);

        Assert.True(turns.TryCast(a, b));
        Assert.Equal(None, Assert.Single(casts).Type);

        // A wand is an area caster: the single-target cast refuses it, the area cast takes it, and a staff has no shape to aim.
        Assert.False(turns.CanCast(w, enemy));
        Assert.False(turns.TryCast(w, enemy));
        Assert.False(turns.CanCastArea(a, (enemy.X, enemy.Y)));
        Assert.Empty(turns.AreaTargets(a, (enemy.X, enemy.Y)));
        Assert.True(turns.CanCastArea(w, (w.X, w.Y)));
        Assert.Equal(new ActorState[] { enemy }, turns.AreaTargets(w, (w.X, w.Y)));

        // A wand cannot strike either.
        Assert.False(turns.CanAttack(w, enemy));
    }

    [Fact]
    public void AreaCast_RefusedOffTurn_ForTheDead_OutOfMovementOrMana_AndWithNobodyCaught()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var enemy = Enemy(5, 7);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var aim = (a.X, a.Y);

        a.DistLeft = 44f;
        Assert.False(turns.CanCastArea(a, aim));   // 45 to cast
        a.DistLeft = 45f;
        Assert.True(turns.CanCastArea(a, aim));
        a.Mana = 17;
        Assert.False(turns.CanCastArea(a, aim));   // 18 for the cast; the trigger is no gate
        a.Mana = 18;
        Assert.True(turns.CanCastArea(a, aim));

        // Nobody in the shape: a cast is a hit, and there is nothing to hit.
        (enemy.X, enemy.Y) = At(15, 15);
        Assert.False(turns.CanCastArea(a, aim));
        Assert.False(turns.TryCastArea(a, aim));
        Assert.Equal(45f, a.DistLeft);
        Assert.Equal(18, a.Mana);
        (enemy.X, enemy.Y) = At(5, 7);

        enemy.Alive = false;
        Assert.False(turns.CanCastArea(a, aim));
        enemy.Alive = true;
        a.Alive = false;
        Assert.False(turns.CanCastArea(a, aim));
        a.Alive = true;

        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.False(turns.CanCastArea(a, aim));
        Assert.False(turns.TryCastArea(a, aim));
        Assert.Equal(200, enemy.Hp);
    }

    // ── The type chart ───────────────────────────────────────────────────────

    [Fact]
    public void SameAttunement_Halves()
    {
        // A Flaming hit into a Flaming-attuned target is halved: 8 lands for 4. Halved rather than
        // nullified — nothing in this game has a zero — so a single point still lands for one.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var enemy = Enemy(5, 7);
        enemy.Attunement = Flaming;
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
        var (_, hit) = Assert.Single(hits);
        Assert.Equal((4, 4, 4), (hit.WeaponShare, hit.Dealt, hit.Taken));
        Assert.Equal(200 - 4, enemy.Hp);

        Assert.Equal(4, CombatBehaviours.AgainstAttunement(8, Flaming, Flaming));
        Assert.Equal(1, CombatBehaviours.AgainstAttunement(1, Cold, Cold));
        Assert.Equal(1, CombatBehaviours.AgainstAttunement(3, Acidic, Acidic));
        Assert.Equal(2, CombatRules.ResistedDamageDivisor);

        // On both sides of the chart the attunement is the target's: a Cold caster into a Cold dungeon is halved too.
        var b = Char("B", 5, 5, "wand_of_the_nova", Cold);
        var frost = Enemy(5, 7);
        frost.Attunement = Cold;
        Assert.Equal(4, Settle(b, frost, 10, Cold).Dealt);
    }

    [Fact]
    public void Opposed_x1_5()
    {
        // Into the opposed type the hit is x1.5, truncated: Flaming into Cold and Cold into Flaming, Shocking into
        // Acidic and Acidic into Shocking; 8 lands for 12, 9 for 13. The pairs are read off the relations
        // file's groups, the same groups that keep Push and Drag apart, never a matrix in code.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var enemy = Enemy(5, 7);
        enemy.Attunement = Cold;
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
        Assert.Equal(200 - 12, enemy.Hp);

        foreach (var (type, opposed) in new[] { (Flaming, Cold), (Cold, Flaming), (Shocking, Acidic), (Acidic, Shocking) })
        {
            Assert.Equal(opposed, GameContent.Current.Enchantments.OpposedTo(type));
            Assert.True(GameContent.Current.Enchantments.Opposes(type, opposed));
            Assert.Equal(12, CombatBehaviours.AgainstAttunement(8, type, opposed));
            Assert.Equal(13, CombatBehaviours.AgainstAttunement(9, type, opposed));
            var caster = Char("B", 5, 5, "wand_of_the_blast", type);
            var target = Enemy(5, 6);
            target.Attunement = opposed;
            Assert.Equal(12, Settle(caster, target, 10, type).Dealt);
        }
        Assert.Null(GameContent.Current.Enchantments.OpposedTo(None));
        Assert.False(GameContent.Current.Enchantments.Opposes(None, Flaming));
        Assert.False(GameContent.Current.Enchantments.Opposes(Flaming, Shocking));
        Assert.Equal(150, CombatRules.OpposedDamagePercent);
    }

    [Fact]
    public void Unattuned_Unchanged()
    {
        // An unattuned target, an unrelated type, and an untyped martial swing into an attuned target: all unchanged.
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var plain = Enemy(5, 7);
        Assert.Null(plain.Attunement);
        Assert.Equal(8, Settle(a, plain, 10, Flaming).Dealt);

        var unrelated = Enemy(5, 7);
        unrelated.Attunement = Shocking;
        Assert.Equal(8, Settle(a, unrelated, 10, Flaming).Dealt);
        Assert.Equal(8, CombatBehaviours.AgainstAttunement(8, Flaming, Shocking));
        Assert.Equal(8, CombatBehaviours.AgainstAttunement(8, Flaming, Acidic));
        Assert.Equal(8, CombatBehaviours.AgainstAttunement(8, Flaming, null));
        Assert.Equal(8, CombatBehaviours.AgainstAttunement(8, None, Flaming));

        var swing = Char("B", 5, 5, "weakspot_stiletto");
        var attuned = Enemy(5, 6, TestWeapons.Get("arming_sword"));
        attuned.Attunement = Flaming;
        var hit = Settle(swing, attuned, 10, None);
        Assert.Equal((15, 12), (hit.WeaponShare, hit.Dealt));   // 15 into Block 3, the chart never touched
    }

    [Fact]
    public void TypeMultiplier_BeforeBlock_ResistedCritLandsInFull()
    {
        // The chart runs at (3,1), building the weapon's own damage before Block absorbs at step 5:
        // a resisted 8 into a Flaming-attuned Block x1 is halved to 4 and then blocked down to the
        // minimum 1 (after Block it would have been (8 - 3) / 2 = 2); an opposed 12 into the same
        // Block lands 9. A resisted crit skips Block entirely and lands its halved damage in full.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var order = table.HandlersFor(GameEvent.DamageTaken).Select(h => h.ToString()).ToList();
        Assert.Equal(order.IndexOf("DamageTaken (3,0) Sundered") + 1, order.IndexOf("DamageTaken (3,1) TypeChart"));
        Assert.True(order.IndexOf("DamageTaken (3,1) TypeChart") < order.IndexOf("DamageTaken (3,9) FixWeaponShare"));
        Assert.True(order.IndexOf("DamageTaken (3,9) FixWeaponShare") < order.IndexOf("DamageTaken (5,0) Block"));

        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var shield = Enemy(5, 7, TestWeapons.Get("arming_sword"));   // Block x1: 3
        shield.Attunement = Flaming;
        var resisted = Settle(a, shield, 10, Flaming);
        Assert.Equal((4, 3, 1, true), (resisted.WeaponShare, resisted.Absorbed, resisted.Dealt, resisted.Blocked));

        shield.Attunement = Cold;
        var opposed = Settle(a, shield, 10, Flaming);
        Assert.Equal((12, 3, 9), (opposed.WeaponShare, opposed.Absorbed, opposed.Dealt));

        shield.Attunement = Flaming;
        var crit = Settle(a, shield, 20, Flaming);
        Assert.True(crit.IsCrit);
        Assert.Equal((8, 0, 8, 8, false), (crit.WeaponShare, crit.Absorbed, crit.Dealt, crit.Taken, crit.Blocked));   // 16 halved, Block skipped

        // Through the turn system, the same: a resisted crit into a Tower Guard dummy (Block 6) lands 8.
        var grid = new int[20, 20];
        var b = Char("B", 5, 5, "wand_of_the_nova", Flaming);
        var guard = Enemy(5, 7, TestWeapons.Get("tower_guard"));
        guard.Attunement = Flaming;
        var turns = new TurnSystem(grid, new[] { b }, new[] { guard }, () => 20);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;
        Assert.True(turns.TryCastArea(b, (b.X, b.Y)));
        Assert.Equal((RollOutcome.Crit, 8, 0), (hit!.Value.Roll.Outcome, hit.Value.Dealt, hit.Value.Blocked));
        Assert.Equal(200 - 8, guard.Hp);

        // The chart multiplies what Sundered added: the same slot, (3,0) then (3,1). Weakened before both.
        var sundered = Enemy(5, 7);
        sundered.Attunement = Flaming;
        sundered.ApplyStatus(Sundered, null, 2);
        Assert.Equal(5, Settle(a, sundered, 10, Flaming).Dealt);   // (8 + 2) / 2
        var weak = Char("C", 5, 5, "wand_of_the_nova", Flaming);
        weak.ApplyStatus(Weakened, null, 2);
        Assert.Equal(4, Settle(weak, sundered, 10, Flaming).Dealt);   // (8 - 2 + 2) / 2
    }

    // ── Friendly fire ────────────────────────────────────────────────────────

    [Fact]
    public void FriendlyFire_OffByDefault_HalfWhenOn()
    {
        // Off by default: the shape catches the far side alone, and an ally standing in it is untouched.
        Assert.False(GameContent.Current.Tuning.FriendlyFireEnabled);
        Assert.Equal(50, GameContent.Current.Tuning.FriendlyFireAllyPercent);
        var grid = new int[20, 20];
        {
            var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
            var b = Char("B", 5, 4, "weakspot_stiletto");   // 32 off: inside the 96
            var enemy = Enemy(5, 7);
            var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
            Assert.Equal(new ActorState[] { enemy }, turns.AreaTargets(a, (a.X, a.Y)));
            int characterHits = 0;
            turns.CharacterHit += (_, _) => characterHits++;
            Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
            Assert.Equal(GameConstants.PlayerHp, b.Hp);
            Assert.Equal(0, characterHits);
            Assert.Equal(200 - 8, enemy.Hp);
        }

        // On: allies inside the shape are hit — the caster never — at half the base, marked on the
        // payload and priced at (1,2); the enemy takes the full 8, and the trigger is still paid once.
        using (TestContent.Use(tuning: ContentDefaults.Tuning with { FriendlyFireEnabled = true }))
        {
            Assert.True(GameContent.Current.Tuning.FriendlyFireEnabled);
            var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
            var b = Char("B", 5, 4, "weakspot_stiletto");
            var c = Char("C", 15, 15, "weakspot_stiletto");   // well outside
            var enemy = Enemy(5, 7);
            var turns = new TurnSystem(grid, new[] { a, b, c }, new[] { enemy }, () => 10);
            Assert.Equal(new ActorState[] { b, enemy }, turns.AreaTargets(a, (a.X, a.Y)));
            var hits = HitProbe(turns);
            var friendly = new List<AttackResolution>();
            turns.CharacterHit += (_, r) => friendly.Add(r);
            var spent = ManaProbe(turns);

            Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

            Assert.Equal(GameConstants.PlayerHp - 4, b.Hp);
            Assert.Equal(GameConstants.PlayerHp, c.Hp);
            Assert.Equal(GameConstants.PlayerHp, a.Hp);
            Assert.Equal(200 - 8, enemy.Hp);
            Assert.Equal(4, Assert.Single(friendly).Dealt);
            Assert.Equal(2, hits.Count);
            var allyHit = hits.Single(h => h.Target == b).Hit;
            var enemyHit = hits.Single(h => h.Target == enemy).Hit;
            Assert.True(allyHit.OnAlly);
            Assert.False(enemyHit.OnAlly);
            Assert.Equal((Flaming, 4, 4), (allyHit.Type, allyHit.WeaponShare, allyHit.Dealt));
            Assert.Equal((Flaming, 8, 8), (enemyHit.Type, enemyHit.WeaponShare, enemyHit.Dealt));
            Assert.Equal(((ActorState)a, 22, 22, "wand_of_the_nova"), Assert.Single(spent));
        }
        Assert.False(GameContent.Current.Tuning.FriendlyFireEnabled);

        // The handler itself: half the base, never below one; a hit on the far side passes through untouched.
        var wand = TestWeapons.Get("wand_of_the_nova", Flaming);
        var ally = DamagePayload.Initial(wand, 10, 32, Flaming, onAlly: true) with { Amount = 8, WeaponShare = 8 };
        var self = Char("S", 5, 5, "wand_of_the_nova", Flaming);
        var other = Char("O", 5, 6, "weakspot_stiletto");
        Assert.Equal(4, CombatBehaviours.FriendlyFire(ally, self, other).Amount);
        Assert.Equal(1, CombatBehaviours.FriendlyFire(ally with { Amount = 1, WeaponShare = 1 }, self, other).Amount);
        var foe = ally with { OnAlly = false };
        Assert.Same(foe, CombatBehaviours.FriendlyFire(foe, self, other));
        Assert.Equal(new HandlerPriority(1, 2), CombatBehaviours.FriendlyFirePriority);
    }

    // ── One payment per cast ─────────────────────────────────────────────────

    [Fact]
    public void Nova_SixTargets_PaysTriggerOnce_EachTargetFullLevels()
    {
        // Six bodies in a Nova: one Cast, one trigger (4) beside the cast's 18 — the same 22 a one-target
        // Blast pays — and six typed hits each landing the full 8, not a share of it. Area damage is
        // strictly cheaper per point than single-target: the shape's reward, not a tax.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var ring = new[] { Enemy(5, 6), Enemy(5, 4), Enemy(4, 5), Enemy(6, 5), Enemy(4, 6), Enemy(6, 4) };
        var turns = new TurnSystem(grid, new[] { a }, ring, () => 10);
        var hits = HitProbe(turns);
        var casts = CastProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        Assert.Equal(6, hits.Count);
        Assert.All(hits, h => Assert.Equal((Flaming, 8, 8, 0), (h.Hit.Type, h.Hit.Dealt, h.Hit.Taken, h.Hit.EnchantmentShare)));
        Assert.All(ring, e => Assert.Equal(200 - 8, e.Hp));
        Assert.Equal(ring.OrderBy(e => (e.X - a.X) * (e.X - a.X) + (e.Y - a.Y) * (e.Y - a.Y)).Select(e => (ActorState)e), hits.Select(h => h.Target));
        Assert.Single(casts);
        Assert.Equal(4, casts[0].ManaToSpend);
        Assert.Equal(((ActorState)a, 22, 22, "wand_of_the_nova"), Assert.Single(spent));
        Assert.Equal(GameConstants.MaxMana - 22, a.Mana);
        Assert.Equal(GameConstants.MaxDistance - 45, a.DistLeft);

        // A one-target Blast pays exactly the same.
        var b = Char("B", 5, 5, "wand_of_the_blast", Flaming);
        var lone = Enemy(5, 8);
        var single = new TurnSystem(grid, new[] { b }, new[] { lone }, () => 10);
        var spentOnce = ManaProbe(single);
        Assert.True(single.TryCastArea(b, (lone.X, lone.Y)));
        Assert.Equal(200 - 8, lone.Hp);
        Assert.Equal(((ActorState)b, 22, 22, "wand_of_the_blast"), Assert.Single(spentOnce));
        Assert.Equal(a.Mana, b.Mana);

        // The trigger is no gate, and unpaid it is a non-event: with mana for the cast alone the hits
        // land untyped — the chart never touches them — and only the cast's 18 is spent.
        var c = Char("C", 5, 5, "wand_of_the_nova", Flaming);
        c.Mana = 20;
        var frost = Enemy(5, 7);
        frost.Attunement = Cold;
        var dry = new TurnSystem(grid, new[] { c }, new[] { frost }, () => 10);
        var dryHits = HitProbe(dry);
        var dryCasts = CastProbe(dry);
        Assert.True(dry.TryCastArea(c, (c.X, c.Y)));
        Assert.Equal((None, 0), (Assert.Single(dryCasts).Type, dryCasts[0].ManaToSpend));
        Assert.Equal((None, 8), (Assert.Single(dryHits).Hit.Type, dryHits[0].Hit.Dealt));   // opposed would have been 12
        Assert.Equal(2, c.Mana);

        // The element's own contribution at step 4 is what the entry adds beside the weapon's share — priced
        // at nothing in Phase 1 (its potency is 0), and whatever it is priced at, each target takes the
        // whole of it while the cast pays the trigger once: re-priced to three levels at 100%, six
        // bodies take 8 + 3 each for the one 4.
        using var _ = TestContent.Use(
            enchantments: new EnchantmentsData(ContentDefaults.Enchantments.Enchantments
                .Select(e => e.Id == "flaming" ? e with { Potency = 3 } : e).ToList()),
            tuning: ContentDefaults.Tuning with { ApplyPercent = new Dictionary<string, int>(StringComparer.Ordinal) { ["flaming"] = 100 } });
        Assert.Equal(3, TestWeapons.Get("wand_of_the_nova", Flaming).Innate!.LevelsFor(3));
        var d = Char("D", 5, 5, "wand_of_the_nova", Flaming);
        var crowd = new[] { Enemy(5, 6), Enemy(5, 4), Enemy(4, 5), Enemy(6, 5), Enemy(4, 6), Enemy(6, 4) };
        var priced = new TurnSystem(grid, new[] { d }, crowd, () => 10);
        var pricedHits = HitProbe(priced);
        var pricedCasts = CastProbe(priced);
        var pricedSpent = ManaProbe(priced);
        Assert.True(priced.TryCastArea(d, (d.X, d.Y)));
        Assert.Equal(6, pricedHits.Count);
        Assert.All(pricedHits, h => Assert.Equal((8, 3, 11), (h.Hit.WeaponShare, h.Hit.EnchantmentShare, h.Hit.Dealt)));
        Assert.All(crowd, e => Assert.Equal(200 - 11, e.Hp));
        Assert.Equal(((ActorState)d, 22, 22, "wand_of_the_nova"), Assert.Single(pricedSpent));
        Assert.Equal(3, Assert.Single(pricedCasts).Levels);
    }

    // ── Opposed types cannot share a weapon ──────────────────────────────────

    [Fact]
    public void OpposedElements_CannotShareAWeapon_ViaRestricted()
    {
        // Flaming and Cold exclude each other exactly as Push and Drag do — the same relations file, the
        // same groups — so content listing both on one weapon is refused, naming the entry and the rule:
        // a unique wand fixing both, or a fighter's dagger carrying both (the counter is transferable).
        var excludes = ContentDefaults.Restricted.Excludes;
        Assert.Contains(excludes, g => g.SequenceEqual(new[] { "flaming", "cold" }));
        Assert.Contains(excludes, g => g.SequenceEqual(new[] { "shocking", "acidic" }));
        Assert.Contains(excludes, g => g.SequenceEqual(new[] { nameof(Push), nameof(Drag), nameof(Rout) }));

        var ex = LoadWith(UniqueWand("test_frostfire", "flaming", "cold"));
        Assert.Equal(("test_frostfire", ContentValidator.RuleEnchantmentsExcluded), (ex.EntryId, ex.Rule));
        Assert.Contains("'flaming' and 'cold'", ex.Message);
        ex = LoadWith(UniqueWand("test_galvanic", "acidic", "shocking"));
        Assert.Equal(("test_galvanic", ContentValidator.RuleEnchantmentsExcluded), (ex.EntryId, ex.Rule));
        ex = LoadWith(Dagger("test_frostfire_knife", "cold", "flaming"));
        Assert.Equal(("test_frostfire_knife", ContentValidator.RuleEnchantmentsExcluded), (ex.EntryId, ex.Rule));

        // A wand fixed to one element cannot be instantiated as another, opposed or not.
        using (TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons, UniqueWand("test_ember", "flaming")])))
        {
            Assert.Equal(Flaming, TestWeapons.Get("test_ember").Innate!.Def.DamageType);
            Assert.Equal(Flaming, TestWeapons.Get("test_ember", Flaming).Innate!.Def.DamageType);
            Assert.Throws<ArgumentException>(() => TestWeapons.Get("test_ember", Cold));
            Assert.Throws<ArgumentException>(() => TestWeapons.Get("test_ember", Shocking));
        }
        Assert.Equal(32, GameContent.Current.Weapons.All.Count);   // the defaults are untouched by any of it
    }

    [Fact]
    public void FlamingPlusShocking_Legal()
    {
        // Non-opposing types stack freely: a Flaming-and-Shocking wand is legal content, and so is a
        // Cold-and-Acidic dagger; both instantiate with the elements in attachment order.
        using var _ = TestContent.Use(weapons: new WeaponsData(
            [.. ContentDefaults.Weapons.Weapons, UniqueWand("test_candle", "flaming", "shocking"), Dagger("test_rime_knife", "cold", "acidic")]));

        var candle = TestWeapons.Get("test_candle");
        Assert.Equal(new[] { "flaming", "shocking" }, candle.Enchantments.Select(e => e.Id));
        Assert.Equal(Flaming, candle.Innate!.Def.DamageType);
        var knife = TestWeapons.Get("test_rime_knife");
        Assert.Equal(new[] { "cold", "acidic" }, knife.Enchantments.Select(e => e.Id));
        Assert.Null(knife.Innate);
        Assert.Equal(34, GameContent.Current.Weapons.All.Count);
    }

    // ── Casts crit too ───────────────────────────────────────────────────────

    [Fact]
    public void CastCrit_HalvesMana_DoublesLevels_OneRollPerCast()
    {
        // One natural roll per cast, shared by every target: a 20 crits the cast (mana 18 halved to 9,
        // the element's levels doubled — from nothing to nothing, its potency being Phase 3's) and
        // every hit alike (8 x2 = 16 each, Block skipped on all three); the trigger is the same 4.
        var grid = new int[20, 20];
        int rolls = 0;
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var guards = new[] { Enemy(5, 6, TestWeapons.Get("arming_sword")), Enemy(5, 4, TestWeapons.Get("arming_sword")), Enemy(4, 5, TestWeapons.Get("tower_guard")) };
        var turns = new TurnSystem(grid, new[] { a }, guards, () => { rolls++; return 20; });
        var hits = HitProbe(turns);
        var casts = CastProbe(turns);
        var spent = ManaProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        Assert.Equal(1, rolls);
        var cast = Assert.Single(casts);
        Assert.Equal((20, true, false, 0, 9, 4, Flaming), (cast.Roll, cast.IsCrit, cast.IsFumble, cast.Levels, cast.ManaCost, cast.ManaToSpend, cast.Type));
        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal((20, true, RollOutcome.Crit, 16, 0, 16), (h.Hit.Roll, h.Hit.IsCrit, h.Hit.Outcome, h.Hit.WeaponShare, h.Hit.Absorbed, h.Hit.Taken)));
        Assert.All(guards, g => Assert.Equal(200 - 16, g.Hp));
        Assert.Equal(((ActorState)a, 13, 13, "wand_of_the_nova"), Assert.Single(spent));
        Assert.Equal(GameConstants.MaxMana - 9 - 4, a.Mana);

        // A fumble doubles the cast's mana (36) and halves every hit (4 each): the same one roll.
        int fumbles = 0;
        var b = Char("B", 5, 5, "wand_of_the_nova", Flaming);
        var dummies = new[] { Enemy(5, 6), Enemy(5, 4) };
        var fumbled = new TurnSystem(grid, new[] { b }, dummies, () => { fumbles++; return 1; });
        var fumbleHits = HitProbe(fumbled);
        var fumbleCasts = CastProbe(fumbled);
        Assert.True(fumbled.TryCastArea(b, (b.X, b.Y)));
        Assert.Equal(1, fumbles);
        Assert.Equal((1, false, true, 36, 4), (fumbleCasts[0].Roll, fumbleCasts[0].IsCrit, fumbleCasts[0].IsFumble, fumbleCasts[0].ManaCost, fumbleCasts[0].ManaToSpend));
        Assert.All(fumbleHits, h => Assert.Equal((RollOutcome.Weak, 4), (h.Hit.Outcome, h.Hit.Dealt)));
        Assert.All(dummies, d => Assert.Equal(200 - 4, d.Hp));
        Assert.Equal(GameConstants.MaxMana - 36 - 4, b.Mana);

        // The window is the caster's: an innate CritWindow x1 makes 19 a crit for the cast and its hits alike; without it 19 is plain.
        var c = Char("C", 5, 5, "wand_of_the_nova", Flaming);
        c.Innate = ModifierSet.Of((CritWindow, 1));
        var target = Enemy(5, 6);
        var wide = new TurnSystem(grid, new[] { c }, new[] { target }, () => 19);
        var wideCasts = CastProbe(wide);
        Assert.True(wide.TryCastArea(c, (c.X, c.Y)));
        Assert.True(wideCasts[0].IsCrit);
        Assert.Equal(200 - 16, target.Hp);
        var d = Char("D", 5, 5, "wand_of_the_nova", Flaming);
        var plain = Enemy(5, 6);
        var narrow = new TurnSystem(grid, new[] { d }, new[] { plain }, () => 19);
        Assert.True(narrow.TryCastArea(d, (d.X, d.Y)));
        Assert.Equal(200 - 8, plain.Hp);
    }

    // ── The table ────────────────────────────────────────────────────────────

    [Fact]
    public void CastChain_ExpandsTheElement_AndTheHitChainRunsItAtStepFour()
    {
        // Printed for a wand's wielder, the Cast chain is the element at attachment index 0, and the
        // DamageTaken chain lists it at (4,0) between the closed weapon share and Block: the element is
        // an entry in both per-kind tables, never an edit to the loop.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_beam", Shocking);
        var turns = new TurnSystem(grid, new[] { a }, new[] { Enemy(5, 7) }, () => 10);
        Assert.Equal(new[] { "Cast (0,0) shocking@0" }, turns.Events.HandlersFor(GameEvent.Cast, a).Select(h => h.ToString()));
        var hitChain = turns.Events.HandlersFor(GameEvent.DamageTaken, a).Select(h => h.ToString()).ToList();
        int at = hitChain.IndexOf("DamageTaken (4,0) shocking@0");
        Assert.True(at > 0);
        Assert.Equal("DamageTaken (3,9) FixWeaponShare", hitChain[at - 1]);
        Assert.Equal("DamageTaken (5,0) Block", hitChain[at + 1]);

        // Rule 4 on the wand's cast: the chain settles the type and the payment on the payload; not a
        // point of mana is written until the applier.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var wand = a.EquippedWeapon!;
        var payload = new CastPayload(wand, a, Roll: 10, IsCrit: false, IsFumble: false, Levels: 0, ManaCost: 18);
        foreach (var (info, handler) in table.Chain<CastPayload>(GameEvent.Cast))
        {
            int before = a.Mana;
            payload = handler(payload, a, a);
            Assert.True(before == a.Mana, $"{info} wrote to the caster");
        }
        Assert.Equal((Shocking, 4), (payload.Type, payload.ManaToSpend));
        Assert.True(payload.ApplyToTarget.IsDefaultOrEmpty);
        Assert.Equal(GameConstants.MaxMana, a.Mana);

        // With nothing left after the cast the element is a non-event: untyped, unpaid.
        a.Mana = 18;
        var starved = table.Raise(GameEvent.Cast, payload with { Type = None, ManaToSpend = 0 }, a, a);
        Assert.Equal((None, 0), (starved.Type, starved.ManaToSpend));
        a.Mana = 21;   // 3 left of the 4 wanted: a type is whole or nothing
        var short3 = table.Raise(GameEvent.Cast, payload with { Type = None, ManaToSpend = 0 }, a, a);
        Assert.Equal((None, 0), (short3.Type, short3.ManaToSpend));
        a.Mana = 22;
        var exact = table.Raise(GameEvent.Cast, payload with { Type = None, ManaToSpend = 0 }, a, a);
        Assert.Equal((Shocking, 4), (exact.Type, exact.ManaToSpend));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WeaponDef UniqueWand(string id, params string[] enchantmentIds)
        => new(id, id, WeaponClass.Wand, Role: null, Range: 160, Damage: 8, Cost: 45, ManaCost: 20,
            Forged: new Dictionary<ModifierType, int> { [Resonant] = 1 },
            Enchantments: enchantmentIds.Select(e => new EnchantmentRef(e)).ToList(),
            Shape: AreaShape.Nova(96), Unique: true, DerivedFrom: "wand_of_the_nova");

    private static WeaponDef Dagger(string id, params string[] enchantmentIds)
        => new(id, id, WeaponClass.Dagger, Role: null, Range: 40, Damage: 15, Cost: 30, ManaCost: 0,
            Forged: new Dictionary<ModifierType, int> { [CritWindow] = 1, [CritMultiplier] = 1 },
            Enchantments: enchantmentIds.Select(e => new EnchantmentRef(e)).ToList(),
            Shape: null, Unique: true, DerivedFrom: "weakspot_stiletto");

    private static ContentException LoadWith(WeaponDef extra)
        => Assert.Throws<ContentException>(() => GameContent.Load(
            ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments,
            new WeaponsData([.. ContentDefaults.Weapons.Weapons, extra])));
}
