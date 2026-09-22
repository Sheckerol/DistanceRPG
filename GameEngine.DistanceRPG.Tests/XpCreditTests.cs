using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.DamageType;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §2.2's four credit sites, as the appliers run them: a blow teaches the
/// weapon class it was struck with, a cast teaches the staff the levels it
/// landed, healing teaches constitution and a spend teaches the mana pool. Each
/// is credited by the one applier that writes the thing the pool measures, from
/// the payload the chain handed back — "damage is a value that returns" — and
/// through the feed the roster fixed for the earner, so an enemy's credit is
/// nothing at all and no applier ever asks an actor its kind.
/// <para>
/// Nothing here reads a level: the effects of proficiency are the next step, so
/// every number in this file is the XP itself.
/// </para>
/// In the content collection because the wand tests price an element to make an
/// enchantment's share visible at all.
/// </summary>
[Collection(TestContent.Collection)]
public class XpCreditTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, Weapon weapon)
    {
        var (x, y) = At(r, c);
        var ch = TestPools.Char(id, x: x, y: y);
        ch.Inventory[0] = weapon;
        return ch;
    }

    private static PartyMemberState Char(string id, int r, int c, string weaponId, DamageType? element = null)
        => Char(id, r, c, TestWeapons.Get(weaponId, element));

    /// <summary>A dummy on a tile's centre with no Block (fists) unless told otherwise, and enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? Fists, Hp = hp };
    }

    private static Weapon Fists => TestWeapons.Make("Fists", 40, 1, 0);

    /// <summary>The enemy's default sword: Block x1, three off the weapon's share of every swing.</summary>
    private static Weapon Sword => TestWeapons.Get("arming_sword");

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>A caster, an ally a tile away and an enemy two tiles away, all inside a staff's 100.</summary>
    private static (TurnSystem Turns, PartyMemberState Caster, PartyMemberState Ally, EnemyState Enemy) CastScene(string staffId, Func<int>? roll = null)
    {
        var grid = new int[20, 20];
        var caster = Char("A", 5, 5, staffId);
        var ally = Char("B", 5, 6, "weakspot_stiletto");
        var enemy = Enemy(5, 7, Sword);
        var turns = new TurnSystem(grid, new[] { caster, ally }, new[] { enemy }, roll ?? (() => 10));
        return (turns, caster, ally, enemy);
    }

    /// <summary>
    /// A member's two pool totals as a scene starts. A grown fixture opens
    /// holding the HP and mana XP that bought its 100/100 pools — TestPools.Grown
    /// earns them the way play would rather than assigning a maximum — so what a
    /// credit site added is always the difference and never the number itself.
    /// Weapon XP is untouched by growing a pool, so a ladder is read as it stands.
    /// </summary>
    private readonly record struct Opening(int HpXp, int ManaXp)
    {
        public static Opening Of(PartyMemberState member) => new(member.HpXp, member.ManaXp);

        public int HpGained(PartyMemberState member) => member.HpXp - HpXp;

        public int ManaGained(PartyMemberState member) => member.ManaXp - ManaXp;
    }

    /// <summary>Every credit that bought a member something, in order: who earned it, what for, and how many points or levels it came to.</summary>
    private static List<(PartyMemberState Member, XpCredit Credit, int Gained)> CreditProbe(TurnSystem turns)
    {
        var credited = new List<(PartyMemberState, XpCredit, int)>();
        turns.XpCredited += (member, credit, gained) => credited.Add((member, credit, gained));
        return credited;
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

    /// <summary>
    /// Content in which <paramref name="ids"/> are elements worth three levels a
    /// hit: Phase 1 prices an element's own share at nothing (potency 0), so
    /// without this an enchantment share is always zero and there is nothing to
    /// attribute. The tuning table's percentage is the one the catalogue reads.
    /// </summary>
    private static IDisposable PricedElements(params string[] ids)
        => TestContent.Use(
            enchantments: new EnchantmentsData(ContentDefaults.Enchantments.Enchantments
                .Select(e => ids.Contains(e.Id) ? e with { Potency = 3 } : e).ToList()),
            tuning: ContentDefaults.Tuning with { ApplyPercent = ids.ToDictionary(id => id, _ => 100, StringComparer.Ordinal) });

    // ── Weapon XP: the weapon's own mitigated damage ─────────────────────────

    [Fact]
    public void WeaponXp_IsThePostBlockWeaponShare()
    {
        // You learn from the damage you actually did (§2.2): a 15-damage dagger
        // into Block 3 teaches the 12 that got through, and it teaches the class
        // rather than the item.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var blocker = Enemy(5, 6, Sword);
        var turns = new TurnSystem(grid, new[] { a }, new[] { blocker }, () => 10);
        var credited = CreditProbe(turns);
        var opening = Opening.Of(a);

        Assert.True(turns.TryAttack(a, blocker));

        Assert.Equal(12, a.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(12, a.WeaponXp.Total);              // one class: the only one it swung
        Assert.Equal((0, 0), (opening.HpGained(a), opening.ManaGained(a)));   // a swing feeds no pool but the weapon's
        Assert.Equal(200 - 12, blocker.Hp);
        Assert.Empty(credited);                          // 12 against a 100-XP first step buys no level yet

        // Armour slows you down: the same swing into a bulwark teaches the one
        // point Block's minimum let through.
        var b = Char("B", 7, 5, "weakspot_stiletto");
        var bulwark = Enemy(7, 6);
        bulwark.Innate = ModifierSet.Of((Block, 8));
        var heavy = new TurnSystem(grid, new[] { b }, new[] { bulwark }, () => 10);

        Assert.True(heavy.TryAttack(b, bulwark));
        Assert.Equal(1, b.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(200 - 1, bulwark.Hp);
    }

    [Fact]
    public void Crit_CreditsTheMultipliedUnblockedHit()
    {
        // A crit fast-forwards proficiency twice over: it multiplies the damage
        // and skips Block entirely, so the whole 45 is credited where the same
        // swing on a 10 credits 9. Crit builds level fastest.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");   // 19+ to crit, x3 when it lands
        var blocker = Enemy(5, 6, Sword);
        blocker.Innate = ModifierSet.Of((Block, 1));    // sword x1 plus innate x1: 6 off anything but a crit
        var turns = new TurnSystem(grid, new[] { a }, new[] { blocker }, () => 20);

        Assert.True(turns.TryAttack(a, blocker));
        Assert.Equal(45, a.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(200 - 45, blocker.Hp);

        var b = Char("B", 7, 5, "weakspot_stiletto");
        var same = Enemy(7, 6, Sword);
        same.Innate = ModifierSet.Of((Block, 1));
        var plain = new TurnSystem(grid, new[] { b }, new[] { same }, () => 10);

        Assert.True(plain.TryAttack(b, same));
        Assert.Equal(9, b.WeaponXp[WeaponClass.Dagger]);
    }

    [Fact]
    public void Ward_DoesNotReduceTheCredit()
    {
        // Ward is temporary health, not mitigation: what it swallows was still
        // dealt. The credit sits before absorption by construction — it is the
        // weapon's share less Block, and Ward comes after both.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var warded = Enemy(5, 6, Sword);
        warded.ApplyStatus(Ward, null, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { warded }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryAttack(a, warded));

        var hit = Assert.Single(hits).Hit;
        Assert.Equal((12, 6, 6), (hit.Dealt, hit.WardSpent, hit.Taken));
        Assert.Equal(12, a.WeaponXp[WeaponClass.Dagger]);   // the credit follows Dealt, not Taken
        Assert.Equal(200 - 6, warded.Hp);
        Assert.Equal(0, warded.StatusLevel(Ward));
    }

    [Fact]
    public void Xp_IsCreditedFromTheSettledPayload_NotTheRolledOne()
    {
        // The attacker cannot credit XP until the defender's handlers have run
        // and handed the number back. A step of the defender's at (5,5) that
        // absorbs four more moves the credit with it: damage is a value that
        // returns, not one that is written.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var blocker = Enemy(5, 6, Sword);
        var turns = new TurnSystem(grid, new[] { a }, new[] { blocker }, () => 10);
        turns.Events.On<DamagePayload>(GameEvent.DamageTaken, new HandlerPriority(5, 5), "probe",
            (p, _, _) => p with { Absorbed = p.Absorbed + 4, Dealt = p.Dealt - 4 });

        Assert.True(turns.TryAttack(a, blocker));

        Assert.Equal(8, a.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(200 - 8, blocker.Hp);

        // The identical swing with nothing after Block credits the 12 the chain
        // settled without it.
        var b = Char("B", 7, 5, "weakspot_stiletto");
        var untouched = Enemy(7, 6, Sword);
        var plain = new TurnSystem(grid, new[] { b }, new[] { untouched }, () => 10);
        Assert.True(plain.TryAttack(b, untouched));
        Assert.Equal(12, b.WeaponXp[WeaponClass.Dagger]);
    }

    // ── Weapon XP: forged counts, acquired does not ──────────────────────────

    [Fact]
    public void ForgedEnchantmentCounts_AcquiredDoesNot()
    {
        // The distinction is not weapon-versus-magic but what the weapon is
        // versus what was attached to it later (§3.1). A wand's element is
        // forged — a wand's damage IS its element, and without the rule a wand
        // would have no XP source at all — so its share is credited; the same
        // element grafted on afterwards is not.
        using var priced = PricedElements("flaming");
        var grid = new int[20, 20];

        var dropped = TestWeapons.Get("wand_of_the_nova", Flaming);
        Assert.Equal(1, dropped.ForgedEnchantmentCount);
        Assert.True(dropped.IsForged(0));

        var a = Char("A", 5, 5, dropped);
        var caught = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { caught }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        var hit = Assert.Single(hits).Hit;
        Assert.Equal((8, 3, 3, 11), (hit.WeaponShare, hit.EnchantmentShare, hit.ForgedShare, hit.Dealt));
        Assert.Equal(11, a.WeaponXp[WeaponClass.Wand]);
        Assert.Equal(200 - 11, caught.Hp);

        // The same element bolted onto the same wand past its def's list: the
        // same blow, the same 11 on the target, and only the wand's own 8 learnt.
        var grafted = new Weapon(GameContent.Current.Weapons["wand_of_the_nova"],
            [new Enchantment(GameContent.Current.Enchantments["flaming"], 1)]);
        Assert.Equal(0, grafted.ForgedEnchantmentCount);
        Assert.False(grafted.IsForged(0));

        var b = Char("B", 7, 5, grafted);
        var also = Enemy(7, 6);
        var bolted = new TurnSystem(grid, new[] { b }, new[] { also }, () => 10);
        var boltedHits = HitProbe(bolted);

        Assert.True(bolted.TryCastArea(b, (b.X, b.Y)));

        var boltedHit = Assert.Single(boltedHits).Hit;
        Assert.Equal((8, 3, 0, 11), (boltedHit.WeaponShare, boltedHit.EnchantmentShare, boltedHit.ForgedShare, boltedHit.Dealt));
        Assert.Equal(8, b.WeaponXp[WeaponClass.Wand]);
        Assert.Equal(200 - 11, also.Hp);   // the target felt no difference at all
    }

    [Fact]
    public void ForgedShare_IsAttributedEntryByEntry_NotOffTheLoopsTotal()
    {
        // Two elements on one wand — the Long Candle carries two, so content
        // allows it; a Nova here, for a circle rather than a beam — the second
        // grafted on. Each entry's own contribution is attributed as it fires,
        // so the forged one's 3 is credited and the acquired one's 3 is not.
        // Taken off the loop's total instead, the whole 6 would ride in on the
        // forged entry's back.
        using var priced = PricedElements("shocking", "flaming");
        var grid = new int[20, 20];
        var catalogue = GameContent.Current.Enchantments;
        var twinned = new Weapon(GameContent.Current.Weapons["wand_of_the_nova"],
            [new Enchantment(catalogue["shocking"], 1), new Enchantment(catalogue["flaming"], 1)],
            forgedCount: 1);
        Assert.Equal((1, true, false), (twinned.ForgedEnchantmentCount, twinned.IsForged(0), twinned.IsForged(1)));

        var a = Char("A", 5, 5, twinned);
        var caught = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { caught }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        var hit = Assert.Single(hits).Hit;
        Assert.Equal(Shocking, hit.Type);   // the first element to fire types the cast
        Assert.Equal((8, 6, 3, 14), (hit.WeaponShare, hit.EnchantmentShare, hit.ForgedShare, hit.Dealt));
        Assert.Equal(11, a.WeaponXp[WeaponClass.Wand]);   // 8 + 3, never 8 + 6
    }

    [Fact]
    public void BothNumbersAreInOnePayload()
    {
        // Two questions, two numbers, both already in the payload: the clean-kill
        // test (§3.2) asks what the blow put out and reads Dealt, which counts
        // everything; proficiency asks what the craft was worth and reads the
        // weapon's share with its forged entries. On a wand carrying a grafted
        // element they differ, and neither is derived from the other.
        using var priced = PricedElements("flaming");
        var grid = new int[20, 20];
        var grafted = new Weapon(GameContent.Current.Weapons["wand_of_the_nova"],
            [new Enchantment(GameContent.Current.Enchantments["flaming"], 1)]);
        var a = Char("A", 5, 5, grafted);
        var caught = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { caught }, () => 10);
        var hits = HitProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        var hit = Assert.Single(hits).Hit;
        Assert.Equal(11, hit.Dealt);                              // output: everything the blow put out
        Assert.Equal(8, hit.WeaponDealt + hit.ForgedShare);       // craft: what the weapon itself did
        Assert.Equal(8, a.WeaponXp[WeaponClass.Wand]);
        Assert.Equal(hit.WeaponShare + hit.EnchantmentShare - hit.Absorbed, hit.Dealt);
    }

    [Fact]
    public void WandXp_IsTheSumAcrossTheShape()
    {
        // A wand's proficiency is fed by damage across every target in the shape,
        // which rewards good placement: three bodies in one Nova is three times
        // the credit of one, each hit crediting its own post-mitigation share.
        // The cast itself lands no status, so it credits nothing: a wand's XP is
        // its hits.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova", Flaming);
        var caught = new[] { Enemy(5, 6), Enemy(5, 4), Enemy(4, 5) };
        var turns = new TurnSystem(grid, new[] { a }, caught, () => 10);
        var hits = HitProbe(turns);
        var opening = Opening.Of(a);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));

        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal((8, 0, 8), (h.Hit.WeaponShare, h.Hit.EnchantmentShare, h.Hit.Dealt)));
        Assert.Equal(24, a.WeaponXp[WeaponClass.Wand]);
        Assert.Equal(22, opening.ManaGained(a));     // the cast's 18 and the element's one trigger of 4
        Assert.Equal(24, a.WeaponXp.Total);          // and nothing landed on any other class

        // One body, one hit, one third of it: the sum is the sum of the hits.
        var b = Char("B", 9, 5, "wand_of_the_nova", Flaming);
        var lone = new TurnSystem(grid, new[] { b }, new[] { Enemy(9, 6) }, () => 10);
        Assert.True(lone.TryCastArea(b, (b.X, b.Y)));
        Assert.Equal(8, b.WeaponXp[WeaponClass.Wand]);
    }

    // ── Weapon XP: a staff's levels applied ──────────────────────────────────

    [Fact]
    public void StaffXp_IsTheLevelsItApplied()
    {
        // A staff deals no damage, so its ladder is fed by status levels applied
        // — the staff's own output stated in the only units it has. One level of
        // Regeneration is one point.
        var (turns, a, b, _) = CastScene("staff_of_renewal");

        Assert.True(turns.TryCast(a, b));
        Assert.Equal(1, a.WeaponXp[WeaponClass.Staff]);
        Assert.Equal(1, b.StatusLevel(Regeneration));
        Assert.Equal(0, b.WeaponXp.Total);   // the target learns nothing: the cast is the caster's craft

        // A crit doubles the levels, so it doubles the credit — the same rule a
        // crit follows on a swing, in the caster's own units.
        var (crit, c, d, _) = CastScene("staff_of_renewal", () => 20);
        Assert.True(crit.TryCast(c, d));
        Assert.Equal(2, d.StatusLevel(Regeneration));
        Assert.Equal(2, c.WeaponXp[WeaponClass.Staff]);

        // And a debuff staff levels the same ladder off what it strips: Mire's
        // two levels are two, whichever staff applied them.
        var (mired, e, _, enemy) = CastScene("staff_of_mire");
        Assert.True(mired.TryCast(e, enemy));
        Assert.Equal(2, enemy.StatusLevel(Mire));
        Assert.Equal(2, e.WeaponXp[WeaponClass.Staff]);
    }

    // ── Health XP: what was actually restored ────────────────────────────────

    [Fact]
    public void HpXp_IsWhatWasRestored_OverhealCreditsNothing()
    {
        // Constitution grows by getting hurt and then healed: the credit is the
        // healing that landed, already capped at what was missing, so a party
        // that never takes a scratch never gains HP.
        var grid = new int[20, 20];
        var full = Char("A", 5, 5, "weakspot_stiletto");
        var turns = new TurnSystem(grid, new[] { full }, new[] { Enemy(15, 15) }, () => 10);
        var opening = Opening.Of(full);
        full.ApplyStatus(Regeneration, null, 4);

        turns.EndTurn();

        Assert.Equal(0, opening.HpGained(full));
        Assert.Equal(full.MaxHp, full.Hp);

        // Two points missing and a four-level tick: two restored, two credited,
        // and the two that had nowhere to go teach nothing.
        var hurt = Char("B", 5, 5, "weakspot_stiletto");
        var second = new TurnSystem(grid, new[] { hurt }, new[] { Enemy(15, 15) }, () => 10);
        var hurtOpening = Opening.Of(hurt);
        hurt.Hp = hurt.MaxHp - 2;
        hurt.ApplyStatus(Regeneration, null, 4);

        second.EndTurn();

        Assert.Equal(2, hurtOpening.HpGained(hurt));
        Assert.Equal(hurt.MaxHp, hurt.Hp);
    }

    [Fact]
    public void HpXp_ComesFromEverySourceThroughOneApplier()
    {
        // Every heal in the game routes through the one HealingReceived applier,
        // so "HP actually restored to you" is true by construction and a new
        // source — a potion, later — needs no credit of its own.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, TestWeapons.Enchanted("Leech", 40, 15, 30, "vampiric"));
        a.Hp = a.MaxHp - 5;
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var opening = Opening.Of(a);

        Assert.True(turns.TryAttack(a, enemy));
        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(2, opening.HpGained(a));  // a soul's drink is a heal like any other
        Assert.Equal(a.MaxHp - 3, a.Hp);
        Assert.Equal(30, a.WeaponXp[WeaponClass.Dagger]);   // and the swings fed the knife apart from it
        Assert.Equal(4, opening.ManaGained(a));             // and the two triggers that paid for the drinks fed the pool that pays them

        a.ApplyStatus(Regeneration, null, 2);
        turns.EndTurn();

        Assert.Equal(4, opening.HpGained(a));  // and so is a regen tick: one applier, one credit
        Assert.Equal(a.MaxHp - 1, a.Hp);
    }

    // ── Mana XP: what the pool actually paid ─────────────────────────────────

    [Fact]
    public void ManaXp_IsTheManaActuallyDeducted()
    {
        // Every cast and every trigger arrives at the one ManaSpent record, and
        // the credit is what the pool paid for it: Renewal's 13 plus the innate's
        // trigger of 1.
        var (turns, a, b, _) = CastScene("staff_of_renewal");
        var opening = Opening.Of(a);

        Assert.True(turns.TryCast(a, b));

        Assert.Equal(14, opening.ManaGained(a));
        Assert.Equal(TestPools.FixtureMana - 14, a.Mana);

        // A fumble doubles the cast's mana; a pool that cannot pay it is emptied
        // rather than overdrawn, and the credit is the 13 the pool actually paid,
        // not the 26 the spend wanted.
        var (fumbled, c, d, _) = CastScene("staff_of_renewal", () => 1);
        var fumbledOpening = Opening.Of(c);
        c.Mana = 13;

        Assert.True(fumbled.TryCast(c, d));

        Assert.Equal(13, fumbledOpening.ManaGained(c));
        Assert.Equal(0, c.Mana);
        Assert.Empty(d.StatusEffects);                     // the trigger had nothing left: the cast landed nothing
        Assert.Equal(0, c.WeaponXp[WeaponClass.Staff]);    // and so taught the staff nothing either
    }

    [Fact]
    public void ARefundDoesNotUncredit()
    {
        // Siphon's kill pays 5 and hands back 15 in one record. The credit is the
        // spend: mana restored is not mana unspent, and the pool that pays for
        // the enchantments is fed by running them.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "widowmaker");
        a.Mana = 50;
        var enemy = Enemy(5, 6, hp: 5);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var opening = Opening.Of(a);

        Assert.True(turns.TryAttack(a, enemy));

        Assert.False(enemy.Alive);
        Assert.Equal(5, opening.ManaGained(a));
        Assert.Equal(60, a.Mana);                          // net +10, and the credit is the 5, not the net
        Assert.Equal(15, a.WeaponXp[WeaponClass.Dagger]);  // the blow taught 15 though the body only held 5
    }

    // ── The feed, not a type test ────────────────────────────────────────────

    [Fact]
    public void EnemiesHaveNoPools()
    {
        // An enemy has no pools at all — not empty ones — and the appliers say so
        // by handing the credit to the feed the roster fixed, which for an enemy
        // does nothing. So an enemy hitting, healing and casting credits nothing
        // and throws nothing, and the member it hits learns nothing from being hit.
        var grid = new int[20, 20];
        grid[5, 4] = 1;   // a wall behind A: the sword's Push has nowhere to shove it, so every beat lands in reach
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var swordsman = Enemy(5, 6, Sword, hp: 50);
        var turns = new TurnSystem(grid, new[] { a }, new[] { swordsman }, () => 10);
        var credited = CreditProbe(turns);
        var opening = Opening.Of(a);
        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(swordsman, true);

        turns.EndTurn();
        Advance(turns, 6f);

        Assert.Equal(3, hits);
        Assert.Equal(TestPools.FixtureHp - 30, a.Hp);
        Assert.Equal((0, 0, 0), (a.WeaponXp.Total, opening.HpGained(a), opening.ManaGained(a)));
        Assert.Empty(credited);

        // An enemy healer casts, spends mana and heals an ally through the same
        // three appliers: three credits that go nowhere, and no throw.
        var far = Char("B", 25, 25, "weakspot_stiletto");   // out of everyone's reach: the healer's turn is the test
        var healer = Enemy(5, 5, TestWeapons.Get("staff_of_renewal"));
        var wounded = Enemy(5, 7, Sword, hp: 10);
        var enemyTurn = new TurnSystem(new int[30, 30], new[] { far }, new[] { healer, wounded }, () => 10);
        var alsoCredited = CreditProbe(enemyTurn);
        var farOpening = Opening.Of(far);

        enemyTurn.EndTurn();
        Advance(enemyTurn, 15f);

        Assert.True(wounded.Hp > 10, "the healer should have mended its ally");
        Assert.True(healer.Mana < healer.MaxMana, "and paid for it out of its own pool");
        Assert.Empty(alsoCredited);
        Assert.Equal((0, 0, 0), (far.WeaponXp.Total, farOpening.HpGained(far), farOpening.ManaGained(far)));
    }

    // ── The typed event ──────────────────────────────────────────────────────

    [Fact]
    public void XpCredited_TellsPresentationWhenAPointLands()
    {
        // The HUD reads levels off state like everything else; what it cannot see
        // in state is the moment one lands, so the crossing is a typed event —
        // the credit that did it and what it bought — and no new GameEvent.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var blocker = Enemy(5, 6, Sword);
        var turns = new TurnSystem(grid, new[] { a }, new[] { blocker }, () => 10);
        var credited = CreditProbe(turns);
        a.WeaponXp[WeaponClass.Dagger] = 99;   // one point short of the first step: 100 / 1

        Assert.True(turns.TryAttack(a, blocker));

        var (member, credit, gained) = Assert.Single(credited);
        Assert.Same(a, member);
        Assert.Equal(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 12), credit);
        Assert.Equal(1, gained);
        Assert.Equal(2, a.WeaponLevel(a.EquippedWeapon!));

        // The next swing crosses nothing — level 2 costs 200 — and says nothing.
        credited.Clear();
        Assert.True(turns.TryAttack(a, blocker));
        Assert.Empty(credited);
        Assert.Equal(2, a.WeaponLevel(a.EquippedWeapon!));

        // A pool announces itself the same way: one XP short of the bar, the heal
        // that crosses it reports the point it bought.
        var b = Char("B", 7, 5, "weakspot_stiletto");
        var second = new TurnSystem(grid, new[] { b }, new[] { Enemy(15, 15) }, () => 10);
        var pooled = CreditProbe(second);
        b.HpXp += b.HealthPool.XpToNext - b.HealthPool.XpIntoNext - 1;
        int bar = b.MaxHp;
        b.Hp = b.MaxHp - 1;
        b.ApplyStatus(Regeneration, null, 1);

        second.EndTurn();

        var (healed, hpCredit, points) = Assert.Single(pooled);
        Assert.Same(b, healed);
        Assert.Equal(new XpCredit(XpPool.Health, null, 1), hpCredit);
        Assert.Equal(1, points);
        Assert.Equal(bar + 1, b.MaxHp);
    }
}
