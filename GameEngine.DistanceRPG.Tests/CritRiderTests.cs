using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.6: a crit leaves a mark, a successful block leaves one on the attacker,
/// and the two riders sit at their own pipeline steps — Weakened subtracts at
/// step 2, Sundered adds at step 3 — on both sides of the fight.
/// </summary>
public class CritRiderTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Member(Weapon? weapon, string id = "A")
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0 };
        c.Inventory[0] = weapon;
        return c;
    }

    /// <summary>The settled payload through a fresh compiled chain with no applier: the numbers alone, nothing written.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits: 0), attacker, defender);
    }

    /// <summary>
    /// A member and an enemy 50 units apart on an open grid under a turn
    /// system, so the applier writes; the enemy's HP is raised so nothing
    /// here kills it.
    /// </summary>
    private static (TurnSystem turns, PartyMemberState a, EnemyState enemy) Duel(Weapon attackerWeapon, string enemyWeaponId, Func<int> roll)
    {
        var grid = new int[20, 20];
        var a = Member(attackerWeapon);
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Weapon = TestWeapons.Get(enemyWeaponId), Hp = 1000 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, roll);
        return (turns, a, enemy);
    }

    [Fact]
    public void Weakened_SubtractsFloorOne_BeforeSundered()
    {
        // Base 5, attacker Weakened 10, defender Sundered 3: the floor is Weakened's
        // own, taken at step 2 before Sundered adds at step 3 — 4, not max(1, 5 - 10 + 3) = 1.
        var attacker = Member(TestWeapons.Make("Club", 40, 5, 30));
        attacker.ApplyStatus(Weakened, null, 10);
        var defender = Member(null, "B");
        defender.ApplyStatus(Sundered, null, 3);

        var settled = Settle(attacker, defender, roll: 10);
        Assert.Equal(4, settled.WeaponShare);
        Assert.Equal(4, settled.Dealt);
        Assert.Equal(4, settled.Taken);

        // However deep it goes, the weapon's share still lands for 1: nothing is reduced to harmlessness.
        attacker.ApplyStatus(Weakened, null, 90);
        Assert.Equal(100, attacker.StatusLevel(Weakened));
        defender.StatusEffects.Clear();
        var floored = Settle(attacker, defender, roll: 10);
        Assert.Equal(1, floored.WeaponShare);
        Assert.True(floored.Dealt >= 1);

        // The subtraction comes off step 1's figure, so off the halved value of a natural 1 too.
        attacker.StatusEffects.Clear();
        attacker.ApplyStatus(Weakened, null, 1);
        var weak = Settle(attacker, defender, roll: 1);
        Assert.Equal(RollOutcome.Weak, weak.Outcome);
        Assert.Equal(1, weak.WeaponShare);   // 5 halves to 2, then one less
    }

    [Fact]
    public void Sundered_AddsAfterMultiplier()
    {
        // Crit x2 (the multiplier's offset alone), base 6, Sundered 3: (6 x 2) + 3 = 15, not (6 + 3) x 2 = 18.
        var attacker = Member(TestWeapons.Make("Mace", 40, 6, 30));
        var defender = Member(null, "B");
        defender.ApplyStatus(Sundered, null, 3);

        var crit = Settle(attacker, defender, roll: 20);
        Assert.True(crit.IsCrit);
        Assert.Equal(15, crit.WeaponShare);
        Assert.Equal(15, crit.Dealt);

        // Weakened 2 on the same crit and no Sundered: 12 - 2 = 10.
        defender.StatusEffects.Clear();
        attacker.ApplyStatus(Weakened, null, 2);
        Assert.Equal(10, Settle(attacker, defender, roll: 20).Dealt);

        // Both at once: (12 - 2) + 3 = 13 — Weakened first, then Sundered, on the weapon's share alone.
        defender.ApplyStatus(Sundered, null, 3);
        var both = Settle(attacker, defender, roll: 20);
        Assert.Equal(13, both.WeaponShare);
        Assert.Equal(0, both.EnchantmentShare);
        Assert.Equal(13, both.Dealt);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    public void Crit_AppliesRiderStacks_EvenWhenWardSwallowsAll(int stacks)
    {
        // A kris with CritWeaken xN lands N Weakened levels on a crit, and a stiletto with
        // CritSunder xN lands N Sundered: no stack is inert at any depth. The defender's
        // Ward swallows the whole hit and the riders land anyway — and they change nothing
        // about the hit that applied them.
        var kris = TestWeapons.Make("Kris", 40, 15, 30, (CritWeaken, stacks));
        var (turns, a, enemy) = Duel(kris, "arming_sword", () => 20);
        enemy.ApplyStatus(Ward, null, 500);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(stacks, enemy.StatusLevel(Weakened));
        Assert.Equal(0, enemy.StatusLevel(Sundered));
        Assert.Equal(1000, enemy.Hp);   // the Ward took all of it

        var res = hit!.Value;
        Assert.Equal(30, res.Dealt);    // 15 x2, Block skipped: the riders did not touch the applying hit
        Assert.Equal(0, res.Taken);
        Assert.Equal(30, res.WardSpent);
        Assert.Equal(500 - 30, enemy.StatusLevel(Ward));
        var rider = Assert.Single(res.Riders);
        Assert.Equal((Weakened, stacks), (rider.Type, rider.Levels));

        var stiletto = TestWeapons.Make("Stiletto", 40, 15, 30, (CritSunder, stacks));
        var (turns2, b, enemy2) = Duel(stiletto, "arming_sword", () => 20);
        Assert.True(turns2.TryAttack(b, enemy2));
        Assert.Equal(stacks, enemy2.StatusLevel(Sundered));
        Assert.Equal(0, enemy2.StatusLevel(Weakened));

        // Not on a normal hit: riders are step 7, and step 7 is a crit's.
        var (turns3, c, enemy3) = Duel(TestWeapons.Make("Kris", 40, 15, 30, (CritWeaken, stacks)), "arming_sword", () => 10);
        Assert.True(turns3.TryAttack(c, enemy3));
        Assert.Empty(enemy3.StatusEffects);
    }

    [Fact]
    public void DisarmingKris_And_WeakspotStiletto_LandTheirRiders_OnNineteen()
    {
        var (turns, a, enemy) = Duel(TestWeapons.Get("disarming_kris"), "arming_sword", () => 19);
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(1, enemy.StatusLevel(Weakened));
        Assert.Equal(1000 - 45, enemy.Hp);   // 15 x3 on a 19, Block skipped

        var (turns2, b, enemy2) = Duel(TestWeapons.Get("weakspot_stiletto"), "arming_sword", () => 19);
        Assert.True(turns2.TryAttack(b, enemy2));
        Assert.Equal(1, enemy2.StatusLevel(Sundered));
        Assert.Equal(0, enemy2.StatusLevel(Weakened));
    }

    [Fact]
    public void BlockWeaken_AppliesToAttackerOnSuccessfulBlock_NotOnCrit()
    {
        // The Warden's Shield: Block x1 and BlockWeaken x1. A blocked dagger swing leaves
        // Weakened on the attacker; a crit is never blocked, so it leaves nothing.
        var (turns, a, enemy) = Duel(TestWeapons.Get("weakspot_stiletto"), "wardens_shield", () => 10);
        AttackResolution? hit = null;
        turns.EnemyHit += (_, r) => hit = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(3, hit!.Value.Blocked);
        Assert.Equal(1, a.StatusLevel(Weakened));
        Assert.Equal(1000 - 12, enemy.Hp);

        // The Weakened attacker's next swing is a point lighter — 15 - 1 = 14, Block 3 -> 11 — and the second block stacks another level.
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(1000 - 12 - 11, enemy.Hp);
        Assert.Equal(2, a.StatusLevel(Weakened));

        var (crit, b, enemy2) = Duel(TestWeapons.Get("weakspot_stiletto"), "wardens_shield", () => 19);
        AttackResolution? critHit = null;
        crit.EnemyHit += (_, r) => critHit = r;
        Assert.True(crit.TryAttack(b, enemy2));
        Assert.Equal(RollOutcome.Crit, critHit!.Value.Roll.Outcome);
        Assert.Equal(0, critHit.Value.Blocked);
        Assert.Equal(0, b.StatusLevel(Weakened));

        // A block that absorbed nothing — the weapon's share was already 1 — is not a successful block either.
        var attacker = Member(TestWeapons.Make("Pin", 10, 1, 0));
        var shield = Member(TestWeapons.Get("wardens_shield"), "B");
        var settled = Settle(attacker, shield, roll: 10);
        Assert.Equal(0, settled.Absorbed);
        Assert.False(settled.Blocked);
        Assert.Empty(settled.ApplyToAttacker);
    }

    [Fact]
    public void EnemyCrit_LandsRidersOnParty_AndBypassesPartyBlock()
    {
        // A kris dummy crits on 19: the shield-bearer's Block is skipped, the rider lands
        // on the party member, and — a bypassed block never happened — the member's
        // BlockWeaken does not fire back. Same handlers, other side.
        var grid = new int[20, 20];
        var member = Member(TestWeapons.Get("wardens_shield"));
        member.X = 5 * Tile;
        member.Y = 5 * Tile;
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Weapon = TestWeapons.Get("disarming_kris") };
        var turns = new TurnSystem(grid, new[] { member }, new[] { enemy }, () => 19);
        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        int distance = CombatRules.SurfaceDistanceUnits(enemy, member);

        var crit = CombatRules.Resolve(turns.Events, enemy, member, enemy.Weapon, distance, () => 19);
        Assert.Equal(RollOutcome.Crit, crit.Roll.Outcome);
        Assert.Equal(0, crit.Blocked);
        Assert.Equal(45, crit.Taken);
        Assert.Equal(GameConstants.PlayerHp - 45, member.Hp);
        Assert.Equal(1, member.StatusLevel(Weakened));
        Assert.Equal(0, enemy.StatusLevel(Weakened));
        Assert.Equal(1, hits);

        // The same swing on a 10 is blocked for 3, and the shield's BlockWeaken lands on the dummy.
        var normal = CombatRules.Resolve(turns.Events, enemy, member, enemy.Weapon, distance, () => 10);
        Assert.Equal(3, normal.Blocked);
        Assert.Equal(12, normal.Taken);
        Assert.Equal(1, enemy.StatusLevel(Weakened));
        Assert.Equal(2, hits);
    }
}
