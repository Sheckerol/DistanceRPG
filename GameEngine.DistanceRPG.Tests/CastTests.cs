using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.3's staves and the cast economy: a cast is a hit that resolves through
/// the table, rolls its d20, pays the resolved cast mana plus the innate's
/// resolved trigger out of what the cast left, lands on the side its innate is
/// for, and both sides pay and regain mana by the one divisor. In the content
/// collection because one test swaps the current content.
/// </summary>
[Collection(TestContent.Collection)]
public class CastTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, string weaponId = "weakspot_stiletto")
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = TestWeapons.Get(weaponId);
        return c;
    }

    private static EnemyState Enemy(float x, float y, string weaponId = "arming_sword")
        => new() { X = x, Y = y, Weapon = TestWeapons.Get(weaponId) };

    /// <summary>A caster holding <paramref name="staffId"/>, an ally 30 to its right and an enemy 60 to its right (all inside a staff's 100), rolling <paramref name="roll"/> (10 unless given).</summary>
    private static (TurnSystem turns, PartyMemberState a, PartyMemberState b, EnemyState enemy) Scene(string staffId, Func<int>? roll = null)
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, staffId);
        var b = Char("B", a.X + 30f, a.Y);
        var enemy = Enemy(a.X + 60f, a.Y);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, roll ?? (() => 10));
        return (turns, a, b, enemy);
    }

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>Record every ManaSpent the table settles, after every compiled handler.</summary>
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

    /// <summary>What one cast of <paramref name="staff"/> takes from the pool for <paramref name="levels"/> of its innate: the resolved cast mana plus the innate's resolved trigger.</summary>
    private static int CastMana(Weapon staff, int levels)
        => staff.ResolvedManaCost + staff.Innate!.ResolvedTriggerCost(staff, levels);

    private static string Snapshot(ActorState a)
        => $"{a.Hp}/{a.Mana}/{a.X}/{a.Y}/{a.Alive}/"
           + string.Join(",", a.StatusEffects.Select(e => $"{e.Type}:{e.Element}:{e.Levels}"));

    // ── The four staves ──────────────────────────────────────────────────────

    [Fact]
    public void Renewal_AppliesOneLevel_AtCost40Movement_13Plus1Mana()
    {
        // Parity statline: today's shipped Staff, Range 100 / Cost 40 / Mana 15, Regeneration on an ally.
        // Resonant x1 resolves the 15 to 13, and the innate's trigger (the DoT ladder over the one level it
        // applies, floored at 1, discounted by the same 10%) is 1: fourteen from the pool, in one record.
        var (turns, a, b, _) = Scene("staff_of_renewal");
        var staff = a.EquippedWeapon!;
        var regeneration = staff.Innate!;
        Assert.Equal((100, 40, 15, 13), (staff.Range, staff.Cost, staff.ManaCost, staff.ResolvedManaCost));
        Assert.Equal(1, regeneration.ResolvedTriggerCost(staff, regeneration.LevelsFor(regeneration.Def.Potency)));
        var spent = ManaProbe(turns);
        var casts = CastProbe(turns);
        var applied = new List<(ActorState Target, StatusEffect Effect, ActorState Source)>();
        turns.ActorStatusApplied += (t, e, s) => applied.Add((t, e, s));
        StatusEffect? buffed = null;
        turns.CharacterBuffed += (_, e) => buffed = e;

        Assert.True(turns.CanCast(a, b));
        Assert.True(turns.TryCast(a, b));

        Assert.Equal(1, b.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxDistance - 40, a.DistLeft);
        Assert.Equal(GameConstants.MaxMana - 13 - 1, a.Mana);
        Assert.Equal(((ActorState)a, 14, 14, "staff_of_renewal"), Assert.Single(spent));
        Assert.Equal(new StatusEffect(Regeneration, null, 1), buffed);
        Assert.Equal(((ActorState)b, new StatusEffect(Regeneration, null, 1), (ActorState)a), Assert.Single(applied));
        Assert.Equal(0, turns.AttacksThisTurn(a));   // a cast is no chosen attack: Charges never counts it

        // The settled payload: the roll, no crit, the innate's one level, the cast's 13, the trigger's 1, one application.
        var cast = Assert.Single(casts);
        Assert.Equal((10, false, false, 1, 13, 1), (cast.Roll, cast.IsCrit, cast.IsFumble, cast.Levels, cast.ManaCost, cast.ManaToSpend));
        Assert.Same(staff, cast.Weapon);
        Assert.Same(b, cast.Target);
        Assert.Equal(new StatusApplication(Regeneration, null, 1), Assert.Single(cast.ApplyToTarget));
    }

    [Fact]
    public void Blight_TargetsEnemyOnly()
    {
        // The Staff of Blight's Poison lands on an enemy (the debuff staves need TryCast to take one) and
        // never on an ally or the caster: 18 (20 at Resonant x1) plus a 1 trigger for three levels.
        var (turns, a, b, enemy) = Scene("staff_of_blight");
        var spent = ManaProbe(turns);
        var buffed = new List<(EnemyState Enemy, StatusEffect Effect)>();
        turns.EnemyBuffed += (e, eff) => buffed.Add((e, eff));

        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.CanCast(a, a));
        Assert.False(turns.TryCast(a, b));
        Assert.True(turns.CanCast(a, enemy));
        Assert.True(turns.TryCast(a, enemy));

        Assert.Equal(3, enemy.StatusLevel(Poison));
        Assert.Empty(b.StatusEffects);
        Assert.Equal(19, CastMana(a.EquippedWeapon!, 3));
        Assert.Equal(GameConstants.MaxMana - 18 - 1, a.Mana);
        Assert.Equal(((ActorState)a, 19, 19, "staff_of_blight"), Assert.Single(spent));
        Assert.Equal((enemy, new StatusEffect(Poison, null, 3)), Assert.Single(buffed));

        // The cast is the hit the status rode in on: the enemy's own turn end ticks it, 3 and then a level off.
        turns.EndTurn();
        Advance(turns, 6f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(GameConstants.DummyHp - 3, enemy.Hp);
        Assert.Equal(2, enemy.StatusLevel(Poison));
    }

    [Fact]
    public void Mire_TargetsEnemy()
    {
        // The signature debuff: two Mire levels tax the enemy's next budget by 20%, for 22 (25 at x1) plus a 1 trigger.
        var (turns, a, b, enemy) = Scene("staff_of_mire");
        Assert.False(turns.CanCast(a, b));
        Assert.True(turns.TryCast(a, enemy));
        Assert.Equal(2, enemy.StatusLevel(Mire));
        Assert.Equal(GameConstants.MaxMana - 22 - 1, a.Mana);
        Assert.Equal(80f, StatusBehaviours.MiredBudget(enemy, GameConstants.EnemyMove));
    }

    [Fact]
    public void Renewal_RefusesEnemy()
    {
        var (turns, a, _, enemy) = Scene("staff_of_renewal");
        Assert.False(turns.CanCast(a, enemy));
        Assert.False(turns.TryCast(a, enemy));
        Assert.Empty(enemy.StatusEffects);
        Assert.Equal(GameConstants.MaxMana, a.Mana);
        Assert.Equal(GameConstants.MaxDistance, a.DistLeft);
    }

    [Fact]
    public void Warding_AppliesWard()
    {
        // Five Ward levels on an ally, self included, and never on an enemy: 18 (20 at x1) plus a 4 trigger (15 / 3 = 5, less 10%).
        var (turns, a, b, enemy) = Scene("staff_of_warding");
        Assert.False(turns.CanCast(a, enemy));
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(5, b.StatusLevel(Ward));
        Assert.Equal(GameConstants.MaxMana - 18 - 4, a.Mana);
        Assert.True(turns.TryCast(a, a));
        Assert.Equal(5, a.StatusLevel(Ward));
        Assert.Equal(GameConstants.MaxMana - 2 * 22, a.Mana);
    }

    [Fact]
    public void Cast_RefusedOffTurn_ForTheDead_OutOfMovement_AndOffTheRoster()
    {
        var (turns, a, b, _) = Scene("staff_of_renewal");
        a.DistLeft = 39f;
        Assert.False(turns.CanCast(a, b));   // 40 to cast
        a.DistLeft = 40f;
        Assert.True(turns.CanCast(a, b));
        b.Alive = false;
        Assert.False(turns.CanCast(a, b));
        b.Alive = true;
        Assert.False(turns.CanCast(a, new PartyMemberState { Id = "X", ColorIndex = 0, X = a.X, Y = a.Y }));   // not on the roster
        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.TryCast(a, b));
        Assert.Empty(b.StatusEffects);
    }

    // ── Casts crit too ───────────────────────────────────────────────────────

    [Fact]
    public void Crit_DoublesLevels_HalvesMana()
    {
        // A natural 20 (staves roll d20 like attacks, in the caster's own window) doubles the effect
        // level and halves the cast's mana, floored: a critical Renewal stacks twice the regeneration
        // for 6 (13 / 2) plus the trigger for two levels (1); a critical Staff of Mire strips twice the
        // movement, four levels for 11 (22 / 2) plus 2 (10 / 3 = 3, less 10%).
        var (turns, a, b, _) = Scene("staff_of_renewal", () => 20);
        var casts = CastProbe(turns);
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(2, b.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxMana - 6 - 1, a.Mana);
        var cast = Assert.Single(casts);
        Assert.Equal((20, true, false, 2, 6, 1), (cast.Roll, cast.IsCrit, cast.IsFumble, cast.Levels, cast.ManaCost, cast.ManaToSpend));

        var (turns2, a2, _, enemy) = Scene("staff_of_mire", () => 20);
        Assert.True(turns2.TryCast(a2, enemy));
        Assert.Equal(4, enemy.StatusLevel(Mire));
        Assert.Equal(60f, StatusBehaviours.MiredBudget(enemy, GameConstants.EnemyMove));
        Assert.Equal(GameConstants.MaxMana - 11 - 2, a2.Mana);

        // The window is the caster's: an innate CritWindow x1 makes 19 a crit too; without it 19 is a plain cast.
        var (turns3, a3, b3, _) = Scene("staff_of_renewal", () => 19);
        a3.Innate = ModifierSet.Of((CritWindow, 1));
        Assert.True(turns3.TryCast(a3, b3));
        Assert.Equal(2, b3.StatusLevel(Regeneration));
        var (turns4, a4, b4, _) = Scene("staff_of_renewal", () => 19);
        Assert.True(turns4.TryCast(a4, b4));
        Assert.Equal(1, b4.StatusLevel(Regeneration));

        // The roll's consequences as a pure function of its inputs, like the swing's.
        Assert.Equal(new CastRoll(20, true, false, 10, 9), CombatRules.RollToCast(20, 20, 5, 18));
        Assert.Equal(new CastRoll(1, false, true, 5, 36), CombatRules.RollToCast(1, 20, 5, 18));
        Assert.Equal(new CastRoll(10, false, false, 5, 18), CombatRules.RollToCast(10, 20, 5, 18));
    }

    [Fact]
    public void Fumble_DoublesMana_KeepsLevels()
    {
        // A natural 1 doubles the cast's mana and leaves the effect: Renewal at 26 (13 x 2) plus the 1 trigger, one level.
        var (turns, a, b, _) = Scene("staff_of_renewal", () => 1);
        var casts = CastProbe(turns);
        var spent = ManaProbe(turns);
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(1, b.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxMana - 26 - 1, a.Mana);
        var cast = Assert.Single(casts);
        Assert.Equal((1, false, true, 1, 26, 1), (cast.Roll, cast.IsCrit, cast.IsFumble, cast.Levels, cast.ManaCost, cast.ManaToSpend));
        Assert.Equal(((ActorState)a, 27, 27, "staff_of_renewal"), Assert.Single(spent));

        // The gate is the resolved cost and the roll comes after it: a fumble at exactly 13 in the pool
        // wants 26, empties the pool rather than overdrawing it, and leaves the trigger nothing (a
        // non-event, so the cast lands nothing). The record says what was wanted and what was paid.
        var (turns2, a2, b2, _) = Scene("staff_of_renewal", () => 1);
        var spent2 = ManaProbe(turns2);
        a2.Mana = 13;
        Assert.True(turns2.CanCast(a2, b2));
        Assert.True(turns2.TryCast(a2, b2));
        Assert.Equal(0, a2.Mana);
        Assert.Empty(b2.StatusEffects);
        Assert.Equal(((ActorState)a2, 26, 13, "staff_of_renewal"), Assert.Single(spent2));
    }

    // ── Resonant and the trigger economy ─────────────────────────────────────

    [Fact]
    public void Resonant_DiscountsCastAndTrigger()
    {
        // Efficiency, not magnitude: -10% a stack off every point of mana the weapon spends, cast and
        // trigger alike, truncated. A Staff of Mire at Resonant x5 costs 12 instead of 25, at its x6
        // ceiling 10; and x6 is the ceiling, forge-limited to x1, so five acquired stacks is all there is.
        var mire = TestWeapons.Get("staff_of_mire");
        Assert.Equal((1, 22), (mire.Stacks(Resonant), mire.ResolvedManaCost));
        mire.Acquire(Resonant, 4);
        Assert.Equal((5, 12), (mire.Stacks(Resonant), mire.ResolvedManaCost));
        mire.Acquire(Resonant, 1);
        Assert.Equal((6, 10), (mire.Stacks(Resonant), mire.ResolvedManaCost));
        mire.Acquire(Resonant, 5);
        Assert.Equal((6, 10), (mire.Stacks(Resonant), mire.ResolvedManaCost));

        // The trigger takes the same discount: Ward's 5 (15 / 3) is 4 at x1 and 2 at x5 and x6, beside a
        // base-20 staff's cast of 18, 10 and 8. Same Ward, cheaper.
        var (turns, a, b, _) = Scene("staff_of_warding");
        var staff = a.EquippedWeapon!;
        var ward = staff.Innate!;
        Assert.Equal((18, 4), (staff.ResolvedManaCost, ward.ResolvedTriggerCost(staff, 5)));
        staff.Acquire(Resonant, 4);
        Assert.Equal((10, 2), (staff.ResolvedManaCost, ward.ResolvedTriggerCost(staff, 5)));
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(5, b.StatusLevel(Ward));
        Assert.Equal(GameConstants.MaxMana - 12, a.Mana);
        staff.Acquire(Resonant, 1);
        Assert.Equal((8, 2), (staff.ResolvedManaCost, ward.ResolvedTriggerCost(staff, 5)));
        Assert.True(turns.TryCast(a, b));
        Assert.Equal(10, b.StatusLevel(Ward));
        Assert.Equal(GameConstants.MaxMana - 12 - 10, a.Mana);

        // Same Mire, cheaper: the discounted cast lands the full two levels for 12 plus the 1 trigger.
        var (turns2, a2, _, enemy) = Scene("staff_of_mire");
        a2.EquippedWeapon!.Acquire(Resonant, 4);
        Assert.True(turns2.TryCast(a2, enemy));
        Assert.Equal(2, enemy.StatusLevel(Mire));
        Assert.Equal(GameConstants.MaxMana - 12 - 1, a2.Mana);
    }

    [Fact]
    public void TriggerPartialFire_ScalesEffect_ZeroIsNonEvent()
    {
        // The trigger is never a gate: the cast's own mana is, and the innate then fires at what is
        // left over what it wanted. Warding wants 4 for five levels after its 18: with 20 in the pool
        // 2 are left, so it fires at 2/4 (two levels) and pays exactly the 2; with 19, one level for
        // 1; with 18 nothing is left, and an effect that scales to nothing is a non-event: it does not
        // fire and pays nothing, while the cast still spent its 18.
        foreach (var (pool, levels, paid) in new[] { (20, 2, 2), (19, 1, 1), (18, 0, 0) })
        {
            var (turns, a, b, _) = Scene("staff_of_warding");
            var spent = ManaProbe(turns);
            a.Mana = pool;
            Assert.True(turns.CanCast(a, b));
            Assert.True(turns.TryCast(a, b));
            Assert.Equal(levels, b.StatusLevel(Ward));
            Assert.Equal(0, a.Mana);
            Assert.Equal(((ActorState)a, 18 + paid, 18 + paid, "staff_of_warding"), Assert.Single(spent));
        }

        // A crit's doubled levels price the trigger up the ladder (55 / 3 = 18, less 10% is 16) while
        // halving the cast to 9: with 18 in the pool 9 are left, so ten levels fire at 9/16, five, for the 9.
        var (turns2, a2, b2, _) = Scene("staff_of_warding", () => 20);
        a2.Mana = 18;
        Assert.True(turns2.TryCast(a2, b2));
        Assert.Equal(5, b2.StatusLevel(Ward));
        Assert.Equal(0, a2.Mana);

        // The payment itself, the pure function every kind on every event shares (the levels to apply
        // and the mana to pay): full when affordable, the fraction when not, (0, 0) when the fraction
        // rounds to nothing or nothing is left, paying nothing either way.
        var staff = TestWeapons.Get("staff_of_warding");
        var ward = staff.Innate!;
        Assert.Equal((5, 4), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: 100));
        Assert.Equal((5, 4), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: 4));
        Assert.Equal((3, 3), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: 3));
        Assert.Equal((1, 1), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: 1));
        Assert.Equal((0, 0), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: 0));
        Assert.Equal((0, 0), EnchantmentBehaviours.PartialFire(ward, staff, 10, manaLeft: 1));   // 10 x 1 / 16 = 0
        Assert.Equal((0, 0), EnchantmentBehaviours.PartialFire(ward, staff, 5, manaLeft: -3));
    }

    // ── The enemy side ───────────────────────────────────────────────────────

    /// <summary>A debuff caster with two party members in its reach, the nearer at 60 and the other at 90, nobody able to hit back.</summary>
    private static (TurnSystem turns, EnemyState caster, PartyMemberState near, PartyMemberState far) DebuffScene(string staffId)
    {
        var grid = new int[30, 30];
        var caster = Enemy(5 * Tile + 16, 5 * Tile + 16, staffId);
        var near = Char("A", caster.X + 60f, caster.Y);
        var far = Char("B", caster.X, caster.Y + 90f);
        var turns = new TurnSystem(grid, new[] { near, far }, new[] { caster }, () => 10);
        return (turns, caster, near, far);
    }

    [Fact]
    public void EnemyDebuffStaff_CastsOnNearestPartyMember()
    {
        // A Staff of Blight in enemy hands is an attacker whose swing is a cast: it closes like one and,
        // in reach, casts on the nearest party member (four beats of its 100 budget at 25 a cast: twelve
        // Poison levels on the nearer member and none on the other) paying 19 a cast from its own pool.
        var (turns, caster, near, far) = DebuffScene("staff_of_blight");
        Assert.True(caster.Weapon.IsCaster);
        Assert.False(caster.IsSupportCaster);
        var applied = new List<(ActorState Target, StatusEffect Effect, ActorState Source)>();
        turns.ActorStatusApplied += (t, e, s) => applied.Add((t, e, s));
        var buffed = new List<PartyMemberState>();
        turns.CharacterBuffed += (c, _) => buffed.Add(c);

        turns.NotifyEnemyVisible(caster, true);
        turns.EndTurn();
        Advance(turns, 8f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(12, near.StatusLevel(Poison));
        Assert.Empty(far.StatusEffects);
        Assert.Equal(GameConstants.PlayerHp, near.Hp);   // it ticks at the member's own turn end
        Assert.Equal(GameConstants.MaxMana - 4 * 19, caster.Mana);
        Assert.Equal(new[] { 3, 6, 9, 12 }, applied.Select(x => x.Effect.Levels));
        Assert.All(applied, x => { Assert.Same(near, x.Target); Assert.Same(caster, x.Source); });
        Assert.Equal(new[] { near, near, near, near }, buffed);

        // The member's own turn end takes the twelve and a level off.
        turns.EndTurn();
        Assert.Equal(GameConstants.PlayerHp - 12, near.Hp);
        Assert.Equal(11, near.StatusLevel(Poison));
    }

    [Fact]
    public void EnemyHealer_StillMendsWounded()
    {
        // The healer keeps its meaning: a support staff mends the most-wounded ally in reach, one cast a
        // beat, now paying 14 a cast (13 plus the 1 trigger) from its own pool; the four levels tick at
        // the ally's turn end, 4 HP back and one level off.
        var grid = new int[30, 30];
        var healer = Enemy(5 * Tile + 16, 5 * Tile + 16, "staff_of_renewal");
        var ally = Enemy(healer.X + 60f, healer.Y);
        ally.Hp = 10;
        var party = Char("A", 25 * Tile, 25 * Tile);
        var turns = new TurnSystem(grid, new[] { party }, new[] { healer, ally }, () => 10);
        Assert.True(healer.IsSupportCaster);
        var buffed = new List<(EnemyState Enemy, int Levels)>();
        turns.EnemyBuffed += (e, eff) => buffed.Add((e, eff.Levels));

        turns.EndTurn();
        Advance(turns, 8f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(new[] { (ally, 1), (ally, 2), (ally, 3), (ally, 4) }, buffed);
        Assert.Equal(14, ally.Hp);
        Assert.Equal(3, ally.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxMana - 4 * 14, healer.Mana);   // its budget is spent: nothing left to bank
    }

    [Fact]
    public void EnemyCasterPaysMana_AndRegens()
    {
        // Both sides pay, and both regain by the one divisor. A debuff caster with 40 mana casts twice
        // (19 each) and stops short of a third at 2, then banks the 50 budget it left for 5; an idle
        // caster far from everyone banks its whole 100 for 10, a mired one its cut 50 for 5.
        var grid = new int[30, 30];
        var caster = Enemy(5 * Tile + 16, 5 * Tile + 16, "staff_of_blight");
        caster.Mana = 40;
        var near = Char("A", caster.X + 60f, caster.Y);
        var idle = Enemy(25 * Tile + 16, 25 * Tile + 16, "staff_of_renewal");
        idle.Mana = 0;
        var mired = Enemy(25 * Tile + 16, 20 * Tile + 16);
        mired.Mana = 0;
        mired.ApplyStatus(Mire, null, 5);
        var turns = new TurnSystem(grid, new[] { near }, new[] { caster, idle, mired }, () => 10);

        turns.NotifyEnemyVisible(caster, true);
        turns.EndTurn();
        Advance(turns, 8f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(6, near.StatusLevel(Poison));
        Assert.Equal(40 - 2 * 19 + 5, caster.Mana);
        Assert.Equal(10, idle.Mana);
        Assert.Equal(5, mired.Mana);
        Assert.Equal(4, mired.StatusLevel(Mire));   // the round's decay came after the bank
    }

    // ── The table ────────────────────────────────────────────────────────────

    [Fact]
    public void CastChain_IsPrintable_AndExpandsTheInnate()
    {
        // The Cast chain is the enchantment loop at (0,0); printed for a caster it reads as the attached
        // entries at their attachment index (the index-is-priority contract), and for an actor with
        // nothing attached, or no weapon, it stays the loop.
        var (turns, a, b, _) = Scene("staff_of_renewal");
        Assert.Equal(new[] { "Cast (0,0) Enchantments" }, turns.Events.HandlersFor(GameEvent.Cast).Select(h => h.ToString()));
        Assert.Equal(new[] { "Cast (0,0) regeneration@0" }, turns.Events.HandlersFor(GameEvent.Cast, a).Select(h => h.ToString()));
        Assert.Equal(new[] { "Cast (0,0) Enchantments" }, turns.Events.HandlersFor(GameEvent.Cast, b).Select(h => h.ToString()));
        Assert.Equal(new[] { "Cast (0,0) Enchantments" },
            turns.Events.HandlersFor(GameEvent.Cast, new PartyMemberState { Id = "X", ColorIndex = 0 }).Select(h => h.ToString()));
        Assert.Equal("Enchantments", EnchantmentBehaviours.LoopName);
    }

    [Fact]
    public void CastHandlers_DoNotMutateActors_ThePayloadCarriesTheStatusAndThePayment()
    {
        // Rule 4: the chain settles the application and the payment on the payload; not a level and
        // not a point of mana is written until the applier.
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var caster = Char("A", 0, 0, "staff_of_warding");
        caster.Mana = 20;
        var ally = Char("B", 30, 0);
        var staff = caster.EquippedWeapon!;
        var payload = new CastPayload(staff, ally, Roll: 10, IsCrit: false, IsFumble: false, Levels: 5, ManaCost: 18);
        foreach (var (info, handler) in table.Chain<CastPayload>(GameEvent.Cast))
        {
            string before = Snapshot(caster) + " | " + Snapshot(ally);
            payload = handler(payload, caster, ally);
            string after = Snapshot(caster) + " | " + Snapshot(ally);
            Assert.True(before == after, $"{info} wrote to an actor: {before} became {after}");
        }
        Assert.Equal(new StatusApplication(Ward, null, 2), Assert.Single(payload.ApplyToTarget));   // five at 2/4 of what it wanted
        Assert.Equal(2, payload.ManaToSpend);
        Assert.Equal(20, caster.Mana);
        Assert.Empty(ally.StatusEffects);

        // Raised on a table with no applier, the same: settled, and nothing written.
        var settled = table.Raise(GameEvent.Cast, payload with { ApplyToTarget = default, ManaToSpend = 0 }, caster, ally);
        Assert.Equal(2, settled.ManaToSpend);
        Assert.Equal(20, caster.Mana);
        Assert.Empty(ally.StatusEffects);
    }

    [Fact]
    public void Cast_LandsAnElementKeyedStatus_WithTheInnatesElement()
    {
        // The application carries the innate's element when the status is keyed on one (the status
        // row's flag, not a switch on the type), so a staff whose innate applies Searing/Flaming lands
        // Searing/Flaming. A test entry stands in until Burning exists.
        var searing = new EnchantmentDef("searing_flame", "Searing Flame", EffectKind.ApplyStatus, TargetSide.Enemy,
            Lock: 20, Trigger: null, Potency: 3, ApplyPercent: 100, Applies: Searing, DamageType: DamageType.Flaming, Unique: false);
        var embers = new WeaponDef("staff_of_embers", "Staff of Embers", WeaponClass.Staff, Role: null,
            Range: 100, Damage: 0, Cost: 40, ManaCost: 20,
            Forged: new Dictionary<ModifierType, int> { [Resonant] = 1 },
            Enchantments: [new EnchantmentRef("searing_flame")], Shape: null, Unique: false, DerivedFrom: null);
        using var _ = TestContent.Use(
            enchantments: new EnchantmentsData([.. ContentDefaults.Enchantments.Enchantments, searing]),
            weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons, embers]));

        var (turns, a, b, enemy) = Scene("staff_of_embers");
        var casts = CastProbe(turns);
        Assert.False(turns.CanCast(a, b));
        Assert.True(turns.TryCast(a, enemy));

        Assert.Equal(new StatusEffect(Searing, DamageType.Flaming, 3), Assert.Single(enemy.StatusEffects));
        Assert.Equal(3, enemy.StatusLevel(Searing, DamageType.Flaming));
        Assert.Equal(new StatusApplication(Searing, DamageType.Flaming, 3), Assert.Single(Assert.Single(casts).ApplyToTarget));
        Assert.Equal(GameConstants.MaxMana - 18 - 1, a.Mana);   // 6 / 3 = 2 for three levels, less 10%
    }
}
