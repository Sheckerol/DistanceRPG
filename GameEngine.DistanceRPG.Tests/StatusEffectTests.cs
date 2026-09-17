using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

public class StatusEffectTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The Staff of Renewal's statline: Resonant x1 resolves its 15 mana to 13.</summary>
    private static Weapon Staff => TestWeapons.Get("staff_of_renewal");

    private static PartyMemberState Char(string id, float x, float y, string weaponId = "weakspot_stiletto")
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = TestWeapons.Get(weaponId);
        return c;
    }

    private static (TurnSystem turns, PartyMemberState a, PartyMemberState b) Scene(float bOffsetX = 30f)
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, "staff_of_renewal");
        var b = Char("B", 5 * Tile + bOffsetX, 5 * Tile);
        var enemy = new EnemyState { X = 18 * Tile, Y = 18 * Tile };
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        return (turns, a, b);
    }

    /// <summary>The same scene with the enemy handed back: far away and never seen, so it acts on nobody but still sees every boundary event.</summary>
    private static (TurnSystem turns, PartyMemberState a, PartyMemberState b, EnemyState enemy) SceneWithEnemy()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, "staff_of_renewal");
        var b = Char("B", 5 * Tile + 30f, 5 * Tile);
        var enemy = new EnemyState { X = 18 * Tile, Y = 18 * Tile };
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        return (turns, a, b, enemy);
    }

    /// <summary>One full round: the party's turn ends, the enemy phase runs, the round closes and the next player turn starts.</summary>
    private static void Round(TurnSystem turns)
    {
        turns.EndTurn();
        for (float t = 0f; t < 3f; t += 1f / 30f)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    // ── Casting (unchanged in meaning) ───────────────────────────────────────

    [Fact]
    public void Cast_AppliesRegen_AndSpendsMovementAndMana()
    {
        var (turns, a, b) = Scene();

        StatusEffect? buffed = null;
        turns.CharacterBuffed += (_, e) => buffed = e;

        Assert.True(turns.CanCast(a, b));
        Assert.True(turns.TryCast(a, b));

        Assert.Equal(1, b.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxDistance - Staff.Cost, a.DistLeft);
        Assert.Equal(GameConstants.MaxMana - Staff.ResolvedManaCost, a.Mana);   // 13, Resonant x1; the innate's trigger arrives with the cast economy
        Assert.NotNull(buffed);
        Assert.Equal(1, buffed!.Levels);
    }

    [Fact]
    public void Cast_StacksLevelOnRepeat()
    {
        var (turns, a, b) = Scene();

        Assert.True(turns.TryCast(a, b));
        Assert.True(turns.TryCast(a, b));
        Assert.True(turns.TryCast(a, b));

        Assert.Equal(3, b.StatusLevel(Regeneration));
        Assert.Equal(GameConstants.MaxMana - 3 * Staff.ResolvedManaCost, a.Mana);
    }

    [Fact]
    public void Cast_CanTargetSelf()
    {
        var (turns, a, _) = Scene();
        Assert.True(turns.CanCast(a, a));
        Assert.True(turns.TryCast(a, a));
        Assert.Equal(1, a.StatusLevel(Regeneration));
    }

    [Fact]
    public void Cast_BlockedWithoutMana()
    {
        var (turns, a, b) = Scene();
        a.Mana = Staff.ResolvedManaCost - 1;
        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.TryCast(a, b));
        Assert.Equal(0, b.StatusLevel(Regeneration));
    }

    [Fact]
    public void Cast_BlockedOutOfRange()
    {
        // 200px apart, surface-to-surface well beyond the staff's 100 range.
        var (turns, a, b) = Scene(bOffsetX: 200f);
        Assert.False(turns.CanCast(a, b));
    }

    [Fact]
    public void Staff_CannotAttackEnemies()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, "staff_of_renewal");
        var enemy = new EnemyState { X = 5 * Tile + 30f, Y = 5 * Tile }; // well within staff range
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.False(turns.CanAttack(a, enemy));
        Assert.False(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.DummyHp, enemy.Hp);       // untouched
        Assert.Equal(GameConstants.MaxDistance, a.DistLeft);  // no movement spent
    }

    [Fact]
    public void RegularWeapon_CannotCast()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, "weakspot_stiletto"); // a dagger
        var b = Char("B", 5 * Tile + 30f, 5 * Tile);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { new EnemyState { X = 18 * Tile, Y = 18 * Tile } }, () => 10);

        Assert.False(turns.CanCast(a, b));
        Assert.False(turns.TryCast(a, b));
        Assert.Equal(0, b.StatusLevel(Regeneration));
    }

    // ── Regeneration: unchanged by the table ─────────────────────────────────

    [Fact]
    public void EndTurn_RegenHealsByLevelThenDecays()
    {
        var (turns, _, b) = Scene();
        b.Hp = 50;
        b.ApplyStatus(Regeneration, null, 3);

        var healed = new List<int>();
        turns.CharacterHealed += (c, amount) => { if (c == b) healed.Add(amount); };

        turns.EndTurn();

        Assert.Equal(53, b.Hp);
        Assert.Equal(2, b.StatusLevel(Regeneration));
        Assert.Equal(new[] { 3 }, healed);
    }

    [Fact]
    public void EndTurn_RegenIsRemovedAtExpiry()
    {
        var (turns, _, b) = Scene();
        b.Hp = 50;
        b.ApplyStatus(Regeneration, null, 1);

        turns.EndTurn();

        Assert.Equal(51, b.Hp);
        Assert.Empty(b.StatusEffects);
    }

    [Fact]
    public void EndTurn_RegenNeverOverheals()
    {
        var (turns, _, b) = Scene();
        b.Hp = b.MaxHp - 2;
        b.ApplyStatus(Regeneration, null, 5);

        var healed = new List<int>();
        turns.CharacterHealed += (c, amount) => { if (c == b) healed.Add(amount); };

        turns.EndTurn();

        Assert.Equal(b.MaxHp, b.Hp);
        Assert.Equal(new[] { 2 }, healed);      // capped at missing HP
        Assert.Equal(4, b.StatusLevel(Regeneration)); // still decays
    }

    [Fact]
    public void EndTurn_DeadMemberShedsEffects()
    {
        var (turns, _, b) = Scene();
        b.Hp = 0;
        b.Alive = false;
        b.ApplyStatus(Regeneration, null, 3);

        turns.EndTurn();

        Assert.Empty(b.StatusEffects);
        Assert.Equal(0, b.Hp); // regen can't resurrect
    }

    [Fact]
    public void EndTurn_FullyIdleTurnRegensOneTenthOfMana()
    {
        var (turns, a, _) = Scene();
        a.Mana = 0;
        // a.DistLeft is a full budget (nothing moved) → full regen slice.
        turns.EndTurn();
        Assert.Equal(GameConstants.MaxMana / GameConstants.ManaRegenTurns, a.Mana);
    }

    [Fact]
    public void EndTurn_ManaRegenScalesWithUnusedMovement()
    {
        var (turns, a, _) = Scene();
        a.Mana = 0;
        a.DistLeft = GameConstants.MaxDistance / 2f; // half the budget left unspent
        turns.EndTurn();
        Assert.Equal(GameConstants.MaxMana / GameConstants.ManaRegenTurns / 2, a.Mana);
    }

    [Fact]
    public void Regeneration_OverflowRaisesHealingAboveFull()
    {
        // The tick is a HealingReceived whose applier heals what fits and queues the
        // surplus as HealingAboveFull — the event Overheal will bank from.
        var (turns, _, b) = Scene();
        b.Hp = b.MaxHp - 2;
        b.ApplyStatus(Regeneration, null, 5);

        var received = new List<(ActorState Self, ActorState Other, HealPayload Heal)>();
        var overflowed = new List<(ActorState Self, HealPayload Heal)>();
        turns.Events.On<HealPayload>(GameEvent.HealingReceived, new HandlerPriority(9, 9), "probe",
            (p, s, o) => { received.Add((s, o, p)); return p; });
        turns.Events.On<HealPayload>(GameEvent.HealingAboveFull, new HandlerPriority(9, 9), "probe",
            (p, s, _) => { overflowed.Add((s, p)); return p; });

        turns.EndTurn();

        var (self, other, heal) = Assert.Single(received);
        Assert.Same(b, self);
        Assert.Same(b, other);   // a tick's source is the actor itself
        Assert.Equal((5, 2, 3), (heal.Amount, heal.Applied, heal.Overflow));
        Assert.Equal(nameof(Regeneration), heal.Source);

        var (above, surplus) = Assert.Single(overflowed);
        Assert.Same(b, above);
        Assert.Equal(3, surplus.Overflow);
        Assert.Equal(b.MaxHp, b.Hp);
        Assert.Equal(0, b.StatusLevel(Ward));   // nothing banks the surplus without a pool: discarded on purpose
    }

    // ── The table and the representation ─────────────────────────────────────

    [Fact]
    public void Table_HasARowAndATunedEffectForEveryStatus()
    {
        var tuning = GameContent.Current.Tuning;
        foreach (var type in Enum.GetValues<StatusEffectType>())
        {
            var rule = StatusRules.Of(type);
            Assert.Equal(type, rule.Type);
            Assert.True(tuning.EffectPerLevel.ContainsKey(type), $"{type} has no EffectPerLevel row");
            Assert.Equal(tuning.EffectPerLevel[type], StatusRules.EffectPerLevel(type));
        }
        Assert.Equal(10, StatusRules.Table.Count);

        // The doc's three columns: 1 damage a level for the DoTs, 1 HP for regen, 1 absorbed for Ward, 10% of movement for Mire.
        Assert.Equal(1, StatusRules.EffectPerLevel(Poison));
        Assert.Equal(1, StatusRules.EffectPerLevel(Bleeding));
        Assert.Equal(1, StatusRules.EffectPerLevel(Searing));
        Assert.Equal(1, StatusRules.EffectPerLevel(Regeneration));
        Assert.Equal(1, StatusRules.EffectPerLevel(Ward));
        Assert.Equal(10, StatusRules.EffectPerLevel(Mire));
        Assert.Equal(1, StatusRules.EffectPerLevel(Sundered));
        Assert.Equal(1, StatusRules.EffectPerLevel(Weakened));

        Assert.Equal((StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement), Row(Regeneration));
        Assert.Equal((StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement), Row(Poison));
        Assert.Equal((StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement), Row(Bleeding));
        Assert.Equal((StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement), Row(Searing));
        Assert.Equal((StatusTrigger.TakingDamage, OnTrigger.SpendMany), Row(Ward));
        Assert.Equal((StatusTrigger.ReceivingHealing, OnTrigger.ConvertAndReset), Row(OverhealPool));
        Assert.Equal((StatusTrigger.RoundEnd, OnTrigger.Reset), Row(Softened));
        Assert.Equal(OnTrigger.None, StatusRules.Of(Mire).OnTrigger);
        Assert.Equal(OnTrigger.None, StatusRules.Of(Sundered).OnTrigger);
        Assert.Equal(OnTrigger.None, StatusRules.Of(Weakened).OnTrigger);

        // Decay is not a column: the turn-end tickers take their loss as the tick, everything else at the round's end.
        foreach (var type in Enum.GetValues<StatusEffectType>())
            Assert.Equal(StatusRules.Of(type).Trigger != StatusTrigger.TurnEnd, StatusRules.DecaysAtRoundEnd(type));

        static (StatusTrigger, OnTrigger) Row(StatusEffectType type) => (StatusRules.Of(type).Trigger, StatusRules.Of(type).OnTrigger);
    }

    [Fact]
    public void ApplyStatus_AccumulatesOnReapplication_AndDropsAtZero()
    {
        var actor = new EnemyState();
        var first = actor.ApplyStatus(Sundered, null, 4);
        Assert.Equal(new StatusEffect(Sundered, null, 4), first);
        var again = actor.ApplyStatus(Sundered, null, 3);
        Assert.Equal(7, again.Levels);                          // adds levels, refreshes nothing
        Assert.Single(actor.StatusEffects);

        Assert.Null(actor.AdjustStatus(Sundered, null, -7));    // removed at zero, not kept empty
        Assert.Empty(actor.StatusEffects);
        Assert.Null(actor.AdjustStatus(Sundered, null, -1));    // and nothing to remove is nothing
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.ApplyStatus(Sundered, null, 0));
    }

    [Fact]
    public void Reapplication_AddsLevels()
    {
        // A CritSunder x4 knife that crits three times has applied twelve levels — and the
        // target then takes +12 from everyone: a plain dagger swing from B lands 15 + 12 into Block 3.
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.Inventory[0] = TestWeapons.Make("Sunder Knife", 40, 15, 30, (CritSunder, 4));
        var b = Char("B", 5 * Tile, 7 * Tile, "flensing_knife");
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 6 * Tile, Hp = 1000 };
        var rolls = new Queue<int>([20, 20, 20, 10]);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => rolls.Dequeue());

        for (int i = 0; i < 3; i++)
            Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(12, enemy.StatusLevel(Sundered));

        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;
        Assert.True(turns.TryAttack(b, enemy));
        Assert.Equal(RollOutcome.Normal, hit!.Value.Roll.Outcome);
        Assert.Equal(15 + 12 - 3, hit.Value.Taken);
    }

    [Fact]
    public void Searing_KeysOnElement()
    {
        // Flaming 3 and Cold 2 are two entries of one status, ticking side by side.
        var (turns, _, b) = Scene();
        b.ApplyStatus(Searing, DamageType.Flaming, 3);
        b.ApplyStatus(Searing, DamageType.Cold, 2);

        Assert.Equal(2, b.StatusEffects.Count(e => e.Type == Searing));
        Assert.Equal(3, b.StatusLevel(Searing, DamageType.Flaming));
        Assert.Equal(2, b.StatusLevel(Searing, DamageType.Cold));
        Assert.Equal(5, b.StatusLevel(Searing));
        Assert.Equal(0, b.StatusLevel(Searing, DamageType.Shocking));

        b.ApplyStatus(Searing, DamageType.Flaming, 1);   // re-lighting the same element adds to its entry
        Assert.Equal(4, b.StatusLevel(Searing, DamageType.Flaming));

        var ticks = new List<StatusTick>();
        turns.ActorStatusTicked += (actor, tick) => { if (actor == b) ticks.Add(tick); };
        turns.EndTurn();
        Assert.Equal(GameConstants.PlayerHp - 4 - 2, b.Hp);
        Assert.Equal(3, b.StatusLevel(Searing, DamageType.Flaming));
        Assert.Equal(1, b.StatusLevel(Searing, DamageType.Cold));
        Assert.Equal(new[] { (DamageType.Flaming, 4), (DamageType.Cold, 2) }, ticks.Select(t => (t.Element!.Value, t.Damage)));

        // The element is the key, not decoration: a burn needs one and nothing else takes one.
        Assert.Throws<ArgumentException>(() => b.ApplyStatus(Searing, null, 1));
        Assert.Throws<ArgumentException>(() => b.ApplyStatus(Poison, DamageType.Flaming, 1));
    }

    // ── Ticks and decay ──────────────────────────────────────────────────────

    [Fact]
    public void DoT_TicksLevelThenDecrements_FiveFourThreeTwoOne()
    {
        // Poison 5 on an enemy: five damage at its first turn's end, then 4, 3, 2, 1 —
        // fifteen in all — and the status is gone after the fifth. The tick is the decay.
        var (turns, _, _, enemy) = SceneWithEnemy();
        enemy.ApplyStatus(Poison, null, 5);
        var ticks = new List<int>();
        turns.ActorStatusTicked += (actor, tick) => { if (actor == enemy) ticks.Add(tick.Damage); };

        var hpAfter = new List<int>();
        var levelAfter = new List<int>();
        for (int round = 0; round < 5; round++)
        {
            Round(turns);
            hpAfter.Add(enemy.Hp);
            levelAfter.Add(enemy.StatusLevel(Poison));
        }

        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, ticks);
        Assert.Equal(new[] { 45, 41, 38, 36, 35 }, hpAfter);
        Assert.Equal(new[] { 4, 3, 2, 1, 0 }, levelAfter);
        Assert.Empty(enemy.StatusEffects);
        Assert.Equal(GameConstants.DummyHp - 15, enemy.Hp);

        Round(turns);   // nothing left to tick
        Assert.Equal(GameConstants.DummyHp - 15, enemy.Hp);
    }

    [Fact]
    public void DoTTick_CanKill_QueuesKilled_AndDefeatsTheEnemy()
    {
        var (turns, _, _, enemy) = SceneWithEnemy();
        enemy.Hp = 3;
        enemy.ApplyStatus(Poison, null, 5);
        enemy.ApplyStatus(Ward, null, 2);   // and the corpse sheds it all
        KillPayload? killed = null;
        turns.Events.On<KillPayload>(GameEvent.Killed, new HandlerPriority(9, 9), "probe",
            (p, s, o) => { killed = p; Assert.Same(enemy, s); Assert.Same(enemy, o); return p; });
        int defeated = 0;
        turns.EnemyDefeated += _ => defeated++;

        Round(turns);

        Assert.False(enemy.Alive);
        Assert.Equal(0, enemy.Hp);
        Assert.Equal(1, defeated);
        Assert.Equal(0, enemy.DefeatedAtTurn);   // it fell under the turn that was ending
        Assert.NotNull(killed);
        Assert.Null(killed!.Weapon);              // a tick has no weapon and no back-reference to what applied it
        Assert.Equal(5, killed.Dealt);
        Assert.Empty(enemy.StatusEffects);
    }

    [Fact]
    public void PoisonTick_CanWipeTheParty_AndTheTurnStandsDown()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.Hp = 2;
        a.ApplyStatus(Poison, null, 5);
        var turns = new TurnSystem(grid, new[] { a }, new[] { new EnemyState { X = 18 * Tile, Y = 18 * Tile } }, () => 10);
        bool died = false, over = false;
        turns.CharacterDied += _ => died = true;
        turns.GameOver += () => over = true;

        turns.EndTurn();

        Assert.False(a.Alive);
        Assert.True(died);
        Assert.True(over);
        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.Empty(a.StatusEffects);
    }

    [Fact]
    public void RoundEnd_DecaysNonTickStatuses_ByOne()
    {
        // Sundered 3, Ward 4 and Weakened 1 on a member: one level each at the round's end, and
        // Weakened is gone at zero. Poison 2 ticks at the turn's end instead and is not swept again.
        var (turns, _, b) = Scene();
        b.ApplyStatus(Sundered, null, 3);
        b.ApplyStatus(Ward, null, 4);
        b.ApplyStatus(Weakened, null, 1);
        b.ApplyStatus(Poison, null, 2);

        Round(turns);

        Assert.Equal(2, b.StatusLevel(Sundered));
        Assert.Equal(3, b.StatusLevel(Ward));
        Assert.Equal(0, b.StatusLevel(Weakened));
        Assert.DoesNotContain(b.StatusEffects, e => e.Type == Weakened);
        Assert.Equal(1, b.StatusLevel(Poison));   // 2 ticked, minus one; the sweep left it alone
        Assert.Equal(GameConstants.PlayerHp - 2, b.Hp);

        // The enemy side decays on the same round.
        var (turns2, _, _, enemy) = SceneWithEnemy();
        enemy.ApplyStatus(Sundered, null, 1);
        enemy.ApplyStatus(Mire, null, 3);
        Round(turns2);
        Assert.Equal(0, enemy.StatusLevel(Sundered));
        Assert.Equal(2, enemy.StatusLevel(Mire));
    }

    [Fact]
    public void Softened_ResetsAtRoundEnd()
    {
        // Block stripped for a round is stripped for exactly one: whatever its level, gone at the round's end, not one lighter.
        var (turns, _, b, enemy) = SceneWithEnemy();
        b.ApplyStatus(Softened, null, 3);
        enemy.ApplyStatus(Softened, null, 5);

        turns.EndTurn();
        Assert.Equal(3, b.StatusLevel(Softened));   // a turn's end is not the round's

        for (float t = 0f; t < 3f; t += 1f / 30f)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(0, b.StatusLevel(Softened));
        Assert.Equal(0, enemy.StatusLevel(Softened));
        Assert.Empty(b.StatusEffects);
    }

    [Fact]
    public void DeadActor_ShedsEverything_AtTheRoundsEndToo()
    {
        // A member cut down during the enemy phase carries nothing into the next round.
        var (turns, _, b) = Scene();
        b.ApplyStatus(Sundered, null, 3);
        b.ApplyStatus(Regeneration, null, 2);
        turns.EndTurn();                       // b's turn ends alive: regen ticks (2, then level 1), Sundered waits for the round
        Assert.Equal(1, b.StatusLevel(Regeneration));
        b.Hp = 0;
        b.Alive = false;                       // falls before the round closes
        for (float t = 0f; t < 3f; t += 1f / 30f)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Empty(b.StatusEffects);
        Assert.Equal(0, b.Hp);
    }

    [Fact]
    public void StatusChains_ArePrintable_InTheirDeclaredOrder()
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        Assert.Equal(
            new[] { "TurnEnd (0,0) Shed", "TurnEnd (1,0) DoTTick", "TurnEnd (1,1) RegenTick" },
            table.HandlersFor(GameEvent.TurnEnd).Select(h => h.ToString()));
        Assert.Equal(
            new[] { "RoundEnd (0,0) Shed", "RoundEnd (1,0) SoftenedReset", "RoundEnd (9,0) Decay" },
            table.HandlersFor(GameEvent.RoundEnd).Select(h => h.ToString()));
        Assert.Equal(
            new[] { "HealingReceived (1,0) CapToMissingHp" },
            table.HandlersFor(GameEvent.HealingReceived).Select(h => h.ToString()));
        Assert.Equal(
            new[] { "HealingAboveFull (1,0) OverhealPool" },
            table.HandlersFor(GameEvent.HealingAboveFull).Select(h => h.ToString()));

        // The tick handlers settle ticks on the payload and write nothing: the actor is untouched until the applier.
        var poisoned = new EnemyState();
        poisoned.ApplyStatus(Poison, null, 3);
        poisoned.ApplyStatus(Regeneration, null, 2);
        var settled = table.Raise(GameEvent.TurnEnd, new TurnPayload(0, Side.Enemy), poisoned, poisoned);   // no applier on this table
        Assert.Equal(
            new[] { new StatusTick(Poison, null, 3, 0, -1), new StatusTick(Regeneration, null, 0, 2, -1) },
            settled.Ticks);
        Assert.Equal(GameConstants.DummyHp, poisoned.Hp);
        Assert.Equal(3, poisoned.StatusLevel(Poison));
    }

    // ── Mire ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Mire_CutsBudgetTenPercentPerLevel_ParalysesAtTen()
    {
        // Level 5 halves the budget, level 10 is all of it: paralysis is the same counter crossing ten.
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.ApplyStatus(Mire, null, 5);
        a.StartTurn();
        Assert.Equal(80f, a.EffectiveMax);
        Assert.Equal(80f, a.DistLeft);

        a.ApplyStatus(Mire, null, 5);   // 10
        a.StartTurn();
        Assert.Equal(0f, a.EffectiveMax);

        a.ApplyStatus(Mire, null, 3);   // 13: past ten is still nothing, never negative
        a.StartTurn();
        Assert.Equal(0f, a.EffectiveMax);

        // Level 12 through the rounds: decay releases it — immobile for two rounds, then a
        // 90% cut on the third (16 of 160) as one level comes off each round. The cut is taken
        // off the whole budget, bank included: the fourth round is (160 + 8 banked) at 20%.
        var (turns, _, b) = Scene();
        b.ApplyStatus(Mire, null, 12);
        var budgets = new List<float>();
        for (int round = 0; round < 4; round++)
        {
            Round(turns);
            budgets.Add(b.EffectiveMax);
        }
        Assert.Equal(new[] { 0f, 0f, 16f }, budgets.Take(3));
        Assert.Equal((160f + 8f) * 0.2f, budgets[3], 3);
        Assert.Equal(8, b.StatusLevel(Mire));
    }

    [Fact]
    public void Mire_CutsTheEnemyBudgetTheSameWay()
    {
        // A seen dummy 23 tiles out walks its 100 toward the character; mired at 5 it walks 50, at 10 not at all.
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile + 16, 5 * Tile + 16);
        var enemy = new EnemyState { X = 25 * Tile + 16, Y = 5 * Tile + 16 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        enemy.ApplyStatus(Mire, null, 5);

        float startX = enemy.X;
        turns.NotifyEnemyVisible(enemy, true);
        Round(turns);
        Assert.Equal(startX - GameConstants.EnemyMove / 2f, enemy.X, 1);
        Assert.Equal(4, enemy.StatusLevel(Mire));   // and it decayed with the round

        enemy.ApplyStatus(Mire, null, 6);   // 10: paralysed
        float parkedX = enemy.X;
        turns.NotifyEnemyVisible(enemy, true);
        Round(turns);
        Assert.Equal(parkedX, enemy.X);
        Assert.Equal(9, enemy.StatusLevel(Mire));
    }
}
