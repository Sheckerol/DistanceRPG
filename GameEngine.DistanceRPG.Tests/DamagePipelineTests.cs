using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

public class DamagePipelineTests
{
    private const float Tile = GameConstants.Tile;
    private static Weapon Dagger => GameConstants.Weapons[0];   // dmg 15, CritRange 4 -> CritWindow x4
    private static Weapon Sword => GameConstants.Weapons[1];    // dmg 10, Block 3 -> Block x1
    private static Weapon Spear => GameConstants.Weapons[2];    // dmg 7, Brace 1 -> Brace x1

    private static PartyMemberState Member(Weapon? weapon, string id = "A")
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0 };
        c.Inventory[0] = weapon;
        return c;
    }

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
        var attacker = Member(new Weapon("Maul", 40, 18, 30, []));
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

        Assert.Equal(GameConstants.PlayerHp, bulwark.Hp);   // resolving writes nothing
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
        Assert.Equal(14, CombatRules.CritThreshold(attacker));   // 20 - (4 + 2)
        Assert.Equal(RollOutcome.Crit, Resolve(attacker, golem, 14).Roll.Outcome);
        Assert.Equal(16, CombatRules.CritThreshold(Dagger));     // the weapon alone knows nothing of it
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
    public void Legacy_DaggerStillCritsOn16_SwordStillBlocks3()
    {
        // The parity bridge from the shipped abilities, deleted with them.
        Assert.Equal(4, Dagger.Modifiers.Stacks(CritWindow));
        Assert.Equal(16, CombatRules.CritThreshold(Dagger));
        Assert.Equal(1, Sword.Modifiers.Stacks(Block));
        Assert.Equal(3, Sword.Modifiers.Value(Block));
        Assert.Equal(1, Spear.Modifiers.Stacks(Brace));
        Assert.Equal(ModifierSet.Empty, GameConstants.Weapons[GameConstants.StaffWeaponIdx].Modifiers);

        var attacker = Member(Dagger);
        var defender = new EnemyState();   // sword

        var crit = Resolve(attacker, defender, 16);
        Assert.Equal(RollOutcome.Crit, crit.Roll.Outcome);
        Assert.Equal(30, crit.Roll.Damage);   // x2: CritMultiplier's offset alone
        Assert.Equal(30, crit.Dealt);
        Assert.Equal(0, crit.Blocked);

        var normal = Resolve(attacker, defender, 15);
        Assert.Equal(RollOutcome.Normal, normal.Roll.Outcome);
        Assert.Equal(15, normal.Roll.Damage);
        Assert.Equal(3, normal.Blocked);
        Assert.Equal(12, normal.Taken);
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
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 16);   // the dagger crits on 16: 30, Block skipped

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
        Assert.Equal(30, hit.Dealt);
        Assert.Equal(30, hit.Damage);
        Assert.Equal(0, hit.Blocked);
        Assert.Equal(0, enemy.Hp);
        Assert.False(enemy.Alive);
        Assert.Equal(1, defeated);
        Assert.Equal(new[] { "killed 30/30 alive=False", "dealt 30/30 hp=0", "crit 16" }, log);

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
}
