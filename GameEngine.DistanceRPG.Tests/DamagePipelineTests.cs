using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

public class DamagePipelineTests
{
    private const float Tile = GameConstants.Tile;

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>
    /// A member and an enemy 50 units apart on an open grid under a turn
    /// system, so the applier writes; the enemy's HP is raised so nothing
    /// here kills it by accident.
    /// </summary>
    private static (TurnSystem turns, PartyMemberState a, EnemyState enemy) Duel(Weapon attackerWeapon, Weapon enemyWeapon, Func<int> roll)
    {
        var grid = new int[20, 20];
        var a = Member(attackerWeapon);
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Weapon = enemyWeapon, Hp = 200 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, roll);
        return (turns, a, enemy);
    }

    private static Weapon Club => TestWeapons.Make("Club", 40, 10, 30);
    private static Weapon Fists => TestWeapons.Make("Fists", 40, 1, 0);   // an enemy weapon with no Block

    // Catalogue instances shared across this class, read-only: nothing here Acquires on them.
    private static readonly Weapon Dagger = TestWeapons.Get("weakspot_stiletto");   // dmg 15, CritWindow x1 (19+), CritMultiplier x1 (x3)
    private static readonly Weapon Spear = TestWeapons.Get("skirmishers_pike");     // dmg 7, Brace x1

    private static PartyMemberState Member(Weapon? weapon, string id = "A")
        => TestPools.Holding(id, weapon);

    /// <summary>The HUD-facing resolution through the compiled chain, writing nothing.</summary>
    private static AttackResolution Resolve(ActorState attacker, ActorState defender, int roll)
        => CombatRules.ResolveAttack(attacker, defender, attacker.EquippedWeapon!, distanceUnits: 0, () => roll);

    /// <summary>The settled payload itself, through a fresh compiled chain with no applier.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll, int distanceUnits = 0)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits), attacker, defender);
    }

    [Fact]
    public void Crit_SkipsBlockEntirely_BeforeMinOneClamp()
    {
        var attacker = Member(TestWeapons.Make("Maul", 40, 18, 30));
        var bulwark = Member(null, "B");
        bulwark.Innate = ModifierSet.Of((Block, 8));   // 24 absorbed: the Bulwark's ceiling
        Assert.Equal(24, bulwark.Value(Block));

        var blocked = Settle(attacker, bulwark, roll: 10);
        Assert.Equal(17, blocked.Absorbed);
        Assert.Equal(1, blocked.Dealt);
        Assert.Equal(1, blocked.Taken);
        Assert.True(blocked.Blocked);

        var crit = Settle(attacker, bulwark, roll: 20);
        Assert.True(crit.IsCrit);
        Assert.Equal(0, crit.Absorbed);   // skipped, not reduced: never absorbed down to 1 and then let through
        Assert.Equal(36, crit.Dealt);
        Assert.Equal(36, crit.Taken);
        Assert.False(crit.Blocked);       // a bypassed block never happened, which Riposte and BlockWeaken read

        Assert.Equal(TestPools.FixtureHp, bulwark.Hp);   // resolving writes nothing
    }

    [Fact]
    public void Innate_AddsToWeapon()
    {
        var attacker = Member(Dagger);
        var golem = new EnemyState { Innate = ModifierSet.Of((Block, 1)) };   // sword x1 plus innate x1
        Assert.Equal(2, golem.Stacks(Block));
        Assert.Equal(6, golem.Value(Block));                     // resolved together: 3 per stack, one offset
        Assert.Equal(1, golem.Weapon.Modifiers.Stacks(Block));   // the weapon itself is untouched: innate never raises a weapon's cap

        var res = Resolve(attacker, golem, 10);
        Assert.Equal(6, res.Blocked);
        Assert.Equal(9, res.Dealt);
        Assert.Equal(9, res.Taken);

        // Innate CritWindow widens the wielder's window the same way.
        attacker.Innate = ModifierSet.Of((CritWindow, 2));
        Assert.Equal(17, CombatRules.CritThreshold(attacker));   // 20 - (1 + 2)
        Assert.Equal(RollOutcome.Crit, Resolve(attacker, golem, 17).Roll.Outcome);
        Assert.Equal(19, CombatRules.CritThreshold(Dagger));     // the weapon alone knows nothing of it
    }

    [Fact]
    public void Dealt_IsNotClampedToRemainingHp()
    {
        var grid = new int[20, 20];
        var a = Member(Dagger);
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Hp = 5 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        AttackResolution? res = null;
        turns.EnemyHit += (_, r) => res = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.NotNull(res);
        Assert.Equal(12, res.Value.Dealt);    // 15 into Block 3: the whole blow is recorded, not the 5 HP it found
        Assert.Equal(12, res.Value.Taken);
        Assert.Equal(12, res.Value.Damage);
        Assert.Equal(0, enemy.Hp);            // HP is what gets clamped
        Assert.False(enemy.Alive);
    }

    [Fact]
    public void Block_NeverSpillsIntoEnchantmentShare()
    {
        // Weapon share 5, enchantment share 4, defender Block x3 (9), no crit:
        // Block takes the weapon share to 1, and the 5 Block left over is not
        // spent on the enchantment share.
        var attacker = Member(Dagger);
        var defender = Member(null, "B");
        defender.Innate = ModifierSet.Of((Block, 3));
        var before = DamagePayload.Initial(Dagger, roll: 10, distanceUnits: 0)
            with { Amount = 5, WeaponShare = 5, EnchantmentShare = 4 };

        var after = CombatBehaviours.Step5_Block(before, attacker, defender);
        Assert.Equal(4, after.Absorbed);
        Assert.Equal(5, after.Dealt);   // 1 of the weapon's, all 4 of the enchantment's
        Assert.True(after.Blocked);
        Assert.Equal(5, after.WeaponShare);   // the shares themselves are never rewritten
        Assert.Equal(4, after.EnchantmentShare);

        var crit = CombatBehaviours.Step5_Block(before with { IsCrit = true }, attacker, defender);
        Assert.Equal(0, crit.Absorbed);
        Assert.Equal(9, crit.Dealt);
        Assert.False(crit.Blocked);
    }

    [Fact]
    public void Payload_KeepsSharesAndOutputsApart_AndSettlesTheSameEveryTime()
    {
        var attacker = Member(Dagger);
        var defender = new EnemyState();

        var settled = Settle(attacker, defender, roll: 10, distanceUnits: 22);
        Assert.Equal(15, settled.Amount);
        Assert.Equal(15, settled.WeaponShare);
        Assert.Equal(0, settled.EnchantmentShare);
        Assert.Equal(3, settled.Absorbed);
        Assert.Equal(12, settled.Dealt);     // fixed at step 5
        Assert.Equal(0, settled.WardSpent);
        Assert.Equal(12, settled.Taken);     // fixed at step 6
        Assert.True(settled.Blocked);
        Assert.False(settled.IsCrit);
        Assert.Equal(RollOutcome.Normal, settled.Outcome);
        Assert.Equal(10, settled.Roll);
        Assert.Equal(DamageType.None, settled.Type);
        Assert.Equal(22, settled.DistanceUnits);
        Assert.Same(Dagger, settled.Weapon);
        Assert.Empty(settled.ApplyToDefender);
        Assert.Empty(settled.ApplyToAttacker);
        Assert.Null(settled.Displace);
        Assert.Equal(0, settled.ManaToSpend);

        var again = Settle(attacker, defender, roll: 10, distanceUnits: 22);
        Assert.Equal(
            (settled.Dealt, settled.Taken, settled.Absorbed, settled.Blocked),
            (again.Dealt, again.Taken, again.Absorbed, again.Blocked));

        // A natural 1 halves first and the rest of the chain still runs: 7 -> 3 into Block 3 -> 1.
        var weak = Settle(Member(Spear), defender, roll: 1);
        Assert.Equal(RollOutcome.Weak, weak.Outcome);
        Assert.Equal(3, weak.WeaponShare);
        Assert.Equal(2, weak.Absorbed);
        Assert.Equal(1, weak.Taken);
    }

    [Fact]
    public void Applier_WritesHpOnce_RaisesTheTypedHit_AndQueuesKilledDamageDealtCrit()
    {
        var grid = new int[20, 20];
        var a = Member(Dagger);
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Hp = 20 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 19);   // the dagger crits on 19: 15 x3 = 45, Block skipped

        var log = new List<string>();
        turns.Events.On<KillPayload>(GameEvent.Killed, new HandlerPriority(9, 0), "probe",
            (p, s, o) => { log.Add($"killed {p.Dealt}/{p.Taken} alive={o.Alive}"); return p; });
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 0), "probe",
            (p, s, o) => { log.Add($"dealt {p.Dealt}/{p.Taken} hp={o.Hp}"); return p; });
        turns.Events.On<CritPayload>(GameEvent.Crit, new HandlerPriority(9, 0), "probe",
            (p, s, o) => { log.Add($"crit {p.Roll}"); return p; });

        var hits = new List<AttackResolution>();
        turns.EnemyHit += (_, r) => hits.Add(r);
        int defeated = 0;
        turns.EnemyDefeated += _ => defeated++;

        Assert.True(turns.TryAttack(a, enemy));

        var hit = Assert.Single(hits);
        Assert.Equal(45, hit.Dealt);
        Assert.Equal(45, hit.Damage);
        Assert.Equal(0, hit.Blocked);
        Assert.Equal(0, enemy.Hp);
        Assert.False(enemy.Alive);
        Assert.Equal(1, defeated);
        Assert.Equal(new[] { "killed 45/45 alive=False", "dealt 45/45 hp=0", "crit 19" }, log);

        // A survivable, blocked hit queues DamageDealt alone.
        log.Clear();
        var b = Member(Dagger, "B");
        b.X = 5 * Tile;
        b.Y = 7 * Tile;
        var tough = new EnemyState { X = 5 * Tile + 50f, Y = 7 * Tile };
        var calm = new TurnSystem(grid, new[] { b }, new[] { tough }, () => 10);
        calm.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 0), "probe",
            (p, _, _) => { log.Add($"dealt {p.Dealt}"); return p; });
        calm.Events.On<KillPayload>(GameEvent.Killed, new HandlerPriority(9, 0), "probe",
            (p, _, _) => { log.Add("killed"); return p; });
        calm.Events.On<CritPayload>(GameEvent.Crit, new HandlerPriority(9, 0), "probe",
            (p, _, _) => { log.Add("crit"); return p; });

        Assert.True(calm.TryAttack(b, tough));
        Assert.Equal(new[] { "dealt 12" }, log);
        Assert.Equal(GameConstants.DummyHp - 12, tough.Hp);
        Assert.True(tough.Alive);
    }

    [Fact]
    public void SurfaceDistance_RoundsUpIntoThePayload()
    {
        var a = Member(Dagger);
        a.X = 0f;
        a.Y = 0f;
        var e = new EnemyState { X = 100f, Y = 0f };   // centres 100 apart, radii 14 + 14
        Assert.Equal(72f, CombatRules.SurfaceDistance(a, e), 3);
        Assert.Equal(72, CombatRules.SurfaceDistanceUnits(a, e));

        e.X = 100.5f;
        Assert.Equal(73, CombatRules.SurfaceDistanceUnits(a, e));   // a fraction past counts as the next unit

        e.X = 10f;   // overlapping circles: never negative
        Assert.Equal(0, CombatRules.SurfaceDistanceUnits(a, e));
    }

    [Fact]
    public void Ward_TakesWhatWasDealt_AfterBlock_OnCritsToo_DownToZero()
    {
        // 10 into Ward 4, no Block, no crit: Dealt 10, Taken 6, the Ward spent to nothing.
        var (turns, a, enemy) = Duel(Club, Fists, () => 10);
        enemy.ApplyStatus(Ward, null, 4);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;
        Assert.True(turns.TryAttack(a, enemy));
        var res = hit!.Value;
        Assert.Equal((10, 6, 4, 0), (res.Dealt, res.Taken, res.WardSpent, res.Blocked));
        Assert.Equal(200 - 6, enemy.Hp);
        Assert.Equal(0, enemy.StatusLevel(Ward));

        // 10 non-crit into Block x1 (3) and Ward 4: Block first, off the weapon's share, then Ward off what was dealt — Dealt 7, Taken 3.
        var (turns2, b, blocker) = Duel(Club, TestWeapons.Get("arming_sword"), () => 10);
        blocker.ApplyStatus(Ward, null, 4);
        AttackResolution? hit2 = null;
        turns2.EnemyHit += (_, r) => hit2 = r;
        Assert.True(turns2.TryAttack(b, blocker));
        var res2 = hit2!.Value;
        Assert.Equal((7, 3, 4, 3), (res2.Dealt, res2.Taken, res2.WardSpent, res2.Blocked));
        Assert.Equal(200 - 3, blocker.Hp);
        Assert.Equal(0, blocker.StatusLevel(Ward));

        // A crit of 20 into Block x8 (24) and Ward 5: Block skipped, Ward not — it is temporary health, not armour. Dealt 20, Taken 15.
        var (turns3, c, bulwark) = Duel(Club, Fists, () => 20);
        bulwark.Innate = ModifierSet.Of((Block, 8));
        bulwark.ApplyStatus(Ward, null, 5);
        AttackResolution? hit3 = null;
        turns3.EnemyHit += (_, r) => hit3 = r;
        Assert.True(turns3.TryAttack(c, bulwark));
        var res3 = hit3!.Value;
        Assert.Equal(RollOutcome.Crit, res3.Roll.Outcome);
        Assert.Equal((20, 15, 5, 0), (res3.Dealt, res3.Taken, res3.WardSpent, res3.Blocked));
        Assert.Equal(200 - 15, bulwark.Hp);
        Assert.Equal(0, bulwark.StatusLevel(Ward));
    }

    [Fact]
    public void DeeplyWardedHit_CreditsDealt_LetsOneThrough_AndDoesNotKill()
    {
        // Ward lets one through exactly as Block does — the pipeline's one floor, applied
        // once at the end — so a pool deep enough to swallow the hit still costs a point of
        // HP. The whole hit was dealt all the same, which is what weapon XP, Serrated and
        // the clean-kill test read, and 1 of 10 is not a death at 5 HP.
        var (turns, a, enemy) = Duel(Club, Fists, () => 10);
        enemy.Hp = 5;
        enemy.ApplyStatus(Ward, null, 20);
        var log = new List<string>();
        turns.Events.On<DamagePayload>(GameEvent.DamageDealt, new HandlerPriority(9, 9), "probe",
            (p, _, _) => { log.Add($"dealt {p.Dealt} taken {p.Taken}"); return p; });
        turns.Events.On<KillPayload>(GameEvent.Killed, new HandlerPriority(9, 9), "probe",
            (p, _, _) => { log.Add("killed"); return p; });
        int defeated = 0;
        turns.EnemyDefeated += _ => defeated++;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(new[] { "dealt 10 taken 1" }, log);
        Assert.Equal(4, enemy.Hp);
        Assert.True(enemy.Alive);
        Assert.Equal(0, defeated);
        Assert.Equal(20 - 9, enemy.StatusLevel(Ward));   // nine spent, not ten: the floor never reaches the pool
    }

    [Fact]
    public void TwentyIntoTwentyWard_LeavesOneThroughAndOneLevel()
    {
        // §3.3's own worked example, in its own numbers: "a 20-damage hit into 20 Ward
        // leaves 1 damage through and 1 level remaining — 19 absorbed, 19 spent". The
        // floor is why a party that stacks Ward is beatable at all: a big pool buys a
        // long fight, never an unlosable one.
        var (turns, a, enemy) = Duel(TestWeapons.Make("Maul", 40, 20, 30), Fists, () => 10);
        enemy.ApplyStatus(Ward, null, 20);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;

        Assert.True(turns.TryAttack(a, enemy));
        var res = hit!.Value;
        Assert.Equal((20, 19, 1), (res.Dealt, res.WardSpent, res.Taken));
        Assert.Equal(200 - 1, enemy.Hp);
        Assert.Equal(1, enemy.StatusLevel(Ward));

        // And that last level is one level, not a pool: the next hit spends it and the
        // floor takes the rest of what it could not stop.
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal((20, 1, 19), (hit!.Value.Dealt, hit!.Value.WardSpent, hit!.Value.Taken));
        Assert.Equal(0, enemy.StatusLevel(Ward));
    }

    [Fact]
    public void NaturalOne_HalvesThenRunsRemainingSteps()
    {
        // Spear 7 on a natural 1: halved to 3 (step 1); the attacker's Weakened 1 -> 2 (step 2);
        // the defender's Sundered 2 -> 4 (step 3); Block x1 absorbs 3 -> Dealt 1 (step 5);
        // Ward swallows nothing of a hit already down to the floor -> Taken 1, and the level
        // stands (step 6); not a crit, so the CritSunder stacks do not land (step 7).
        var attacker = Member(TestWeapons.Make("Pike", 128, 7, 55, (CritSunder, 2)));
        attacker.ApplyStatus(Weakened, null, 1);
        var defender = new EnemyState();   // the arming sword: Block x1
        defender.ApplyStatus(Sundered, null, 2);
        defender.ApplyStatus(Ward, null, 1);

        var weak = Settle(attacker, defender, roll: 1);
        Assert.Equal(RollOutcome.Weak, weak.Outcome);
        Assert.Equal(4, weak.WeaponShare);
        Assert.Equal(3, weak.Absorbed);
        Assert.Equal(1, weak.Dealt);
        Assert.Equal(0, weak.WardSpent);
        Assert.Equal(1, weak.Taken);
        Assert.True(weak.Blocked);
        Assert.Empty(weak.ApplyToDefender);
    }

    [Fact]
    public void Overheal_ConvertsSurplusToWardAtFiveToOne_AndResets()
    {
        // A member one HP short, Regeneration 3, and twelve surplus points banked in the
        // hidden pool: the tick heals 1 and overflows 2; the overflow's HealingAboveFull
        // converts the pool — 12 / 5 = 2 Ward, the 2 left over lost — and the pool is gone.
        var grid = new int[20, 20];
        var a = Member(Dagger);
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        a.Hp = a.MaxHp - 1;
        a.ApplyStatus(Regeneration, null, 3);
        a.ApplyStatus(OverhealPool, null, 12);
        var healed = new List<int>();
        turns.CharacterHealed += (_, amount) => healed.Add(amount);

        turns.EndTurn();

        Assert.Equal(a.MaxHp, a.Hp);
        Assert.Equal(new[] { 1 }, healed);
        Assert.Equal(2, a.StatusLevel(Ward));
        Assert.Equal(0, a.StatusLevel(OverhealPool));
        Assert.Equal(2, a.StatusLevel(Regeneration));

        // Under five points converts to nothing and still resets: lossy, not banked across heals.
        var b = Member(Dagger, "B");
        b.X = 5 * Tile;
        b.Y = 7 * Tile;
        var turns2 = new TurnSystem(grid, new[] { b }, new[] { new EnemyState { X = 15 * Tile, Y = 15 * Tile } }, () => 10);
        b.ApplyStatus(Regeneration, null, 1);   // at full HP: all of it overflows
        b.ApplyStatus(OverhealPool, null, 4);
        turns2.EndTurn();
        Assert.Equal(0, b.StatusLevel(Ward));
        Assert.Equal(0, b.StatusLevel(OverhealPool));

        // With no surplus to trigger it, the pool decays like everything else: one level a round.
        var c = Member(Dagger, "C");
        c.X = 5 * Tile;
        c.Y = 9 * Tile;
        var turns3 = new TurnSystem(grid, new[] { c }, new[] { new EnemyState { X = 15 * Tile, Y = 15 * Tile } }, () => 10);
        c.ApplyStatus(OverhealPool, null, 3);
        turns3.EndTurn();
        Advance(turns3, 3f);
        Assert.Equal(TurnPhase.Player, turns3.Phase);
        Assert.Equal(2, c.StatusLevel(OverhealPool));
        Assert.Equal(0, c.StatusLevel(Ward));
    }
}
