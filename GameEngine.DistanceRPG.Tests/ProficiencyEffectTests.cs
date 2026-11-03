using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.DamageType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// What proficiency pays once it is earned (§2.2): <c>+floor(L / 2)</c> damage
/// on the weapon's own base and <c>-1</c> movement per three levels off the
/// weapon's own resolved cost, floored so a weapon never becomes free.
/// <para>
/// Both effects enter at one place each, and both places are shared. The damage
/// bonus enters at step 1 — before the crit multiplier, because it is the
/// weapon's own damage rather than a rider on it — through
/// <see cref="CombatRules.BaseDamage"/>, which the roll, Longshot's re-take and
/// a wand's cast all call: a bonus added at only one of the three would be
/// dropped by every Longshot weapon past the free tiles and would desync a
/// levelled wand's burn from its hit. The movement discount enters at the use
/// site through <see cref="ActorState.MovementCost"/>, after Light, so the gate
/// and the charge read one number.
/// </para>
/// In the content collection: the wand case prices an element's burn up far
/// enough that what the cast paid for and what the hit landed can be told apart.
/// </summary>
[Collection(TestContent.Collection)]
public class ProficiencyEffectTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The centre of tile (row, column), in logic units.</summary>
    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Member(string weaponId, string id = "A")
        => TestPools.Holding(id, TestWeapons.Get(weaponId));

    /// <summary>A member with nothing in hand and no Block: a body for a swing to land on.</summary>
    private static PartyMemberState Bare(string id = "B") => TestPools.Char(id);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, TestWeapons.Get(weaponId), x: x, y: y);
    }

    /// <summary>A dummy on a tile's centre with fists — no Block, so a hit's own number is what lands — and enough HP that nothing here kills it.</summary>
    private static EnemyState Enemy(int r, int c, Weapon? weapon = null, int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = weapon ?? TestWeapons.Make("Fists", 40, 1, 0), Hp = hp };
    }

    /// <summary>The settled payload through a fresh compiled chain with no applier: the numbers alone, nothing written.</summary>
    private static DamagePayload Settle(ActorState attacker, ActorState defender, int roll, int distanceUnits)
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table.Raise(GameEvent.DamageTaken,
            DamagePayload.Initial(attacker.EquippedWeapon!, roll, distanceUnits), attacker, defender);
    }

    private static List<CastPayload> CastProbe(TurnSystem turns)
    {
        var casts = new List<CastPayload>();
        turns.Events.On<CastPayload>(GameEvent.Cast, new HandlerPriority(9, 9), "probe", (p, _, _) => { casts.Add(p); return p; });
        return casts;
    }

    /// <summary>
    /// The cumulative XP a wielder needs to stand at <paramref name="level"/>:
    /// the per-step costs summed, each floored by its own integer division —
    /// the replay is the definition, not a closed form. A fixture carries
    /// <see cref="InnateStats.None"/>, so every class divides by 1.
    /// </summary>
    private static int XpForLevel(int level)
    {
        int total = 0;
        for (int l = Progression.StartingLevel; l < level; l++)
            total += Progression.XpToNext(GameContent.Current.Tuning.WeaponXpPerLevel * l, InnateStats.Low);
        return total;
    }

    /// <summary>Stand <paramref name="member"/> at <paramref name="level"/> in <paramref name="cls"/>, by the raw XP that buys it and nothing else.</summary>
    private static PartyMemberState Levelled(PartyMemberState member, WeaponClass cls, int level)
    {
        member.WeaponXp[cls] = XpForLevel(level);
        Assert.Equal(level, member.Proficiency(cls).Level);
        return member;
    }

    // ── The damage bonus ─────────────────────────────────────────────────────

    [Fact]
    public void DamageBonus_IsOnePerTwoLevels_AndACritMultipliesIt()
    {
        // A dagger the wielder has mastered hits harder, and a crit multiplies
        // what it hits for: the bonus is the weapon's own damage, so it enters
        // at step 1 with the rest of it rather than flat at the end the way a
        // temporary debuff does.
        var a = Member("weakspot_stiletto");   // 15, crit x3 in a 19+ window
        var target = Bare();

        Assert.Equal(15, Settle(a, target, 10, 0).WeaponShare);          // level 1: floor(1 / 2) is nothing
        Assert.Equal(16, Settle(Levelled(a, WeaponClass.Dagger, 2), target, 10, 0).WeaponShare);
        Assert.Equal(16, Settle(Levelled(a, WeaponClass.Dagger, 3), target, 10, 0).WeaponShare);
        Assert.Equal(17, Settle(Levelled(a, WeaponClass.Dagger, 4), target, 10, 0).WeaponShare);
        Assert.Equal(51, Settle(a, target, 20, 0).WeaponShare);          // (15 + 2) x 3, not 15 x 3 + 2

        // A natural 1 halves the resolved number, bonus included: 17 / 2.
        Assert.Equal(8, Settle(a, target, 1, 0).WeaponShare);

        // The ladder is the class's: an axe career does nothing for the knife in their hand.
        a.WeaponXp[WeaponClass.Axe] = XpForLevel(9);
        Assert.Equal(17, Settle(a, target, 10, 0).WeaponShare);
    }

    [Fact]
    public void ALongshotWeaponKeepsTheBonusThroughTheRetake()
    {
        // Longshot re-takes step 1 over a base the distance has priced and
        // overwrites what step 1 settled, so a bonus that lived only in step 1
        // would vanish from every spear, bow, throw and brace past the free
        // tiles — and only there, which is what makes it worth pinning.
        var spear = Member("skirmishers_pike");   // 7, Longshot x1: the fourth tile pays
        var target = Bare();
        Levelled(spear, WeaponClass.Spear, 4);    // +2

        Assert.Equal(9, Settle(spear, target, 10, 0).WeaponShare);      // inside the free tiles: 7 + 2, no re-take
        Assert.Equal(9, Settle(spear, target, 10, 96).WeaponShare);
        Assert.Equal(10, Settle(spear, target, 10, 97).WeaponShare);    // the re-take: 7 + 2 + 1
        Assert.Equal(10, Settle(spear, target, 10, 128).WeaponShare);

        // A bow, whose every shot is past them: 5 + 2 + (10 - 3) x 2.
        var archer = Member("longbow", "C");
        Levelled(archer, WeaponClass.Ranged, 4);
        Assert.Equal(21, Settle(archer, target, 10, 320).WeaponShare);
        Assert.Equal(7, Settle(archer, target, 10, 0).WeaponShare);     // 5 + 2 in melee, where Longshot pays nothing
    }

    [Fact]
    public void ALevelledWandsBurnMovesWithTheBonus()
    {
        // The cast sizes the burn its element will leave before any target is
        // known, by re-deriving the wand's figure under the cast's roll. The
        // hits size it off the share they actually landed. Both must read the
        // same base or the cast pays for one burn and the shape lands another —
        // from wand level 2 on, where the bonus first bites.
        using var priced = TestContent.Use(tuning: ContentDefaults.Tuning with
        {
            ApplyPercent = new Dictionary<string, int>(StringComparer.Ordinal) { ["flaming"] = 100 },
        });

        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "wand_of_the_nova_unique");   // 8, Flaming + Burning
        var fresh = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { fresh }, () => 10);
        var casts = CastProbe(turns);

        Assert.True(turns.TryCastArea(a, (a.X, a.Y)));
        Assert.Equal((200 - 8, 8), (fresh.Hp, fresh.StatusLevel(Searing, Flaming)));
        int atLevelOne = Assert.Single(casts).ManaToSpend;

        // The same wand at level 4: two more damage, two more levels of burn,
        // and the cast paid its ladder over the levels that actually landed.
        var b = Char("B", 5, 5, "wand_of_the_nova_unique");
        Levelled(b, WeaponClass.Wand, 4);
        var burnt = Enemy(5, 6);
        var levelled = new TurnSystem(grid, new[] { b }, new[] { burnt }, () => 10);
        var levelledCasts = CastProbe(levelled);

        Assert.True(levelled.TryCastArea(b, (b.X, b.Y)));
        Assert.Equal((200 - 10, 10), (burnt.Hp, burnt.StatusLevel(Searing, Flaming)));
        Assert.Equal(atLevelOne + 6, Assert.Single(levelledCasts).ManaToSpend);   // the 10-level ladder, not the 8-level one
    }

    [Fact]
    public void TheBonusIsPartOfTheWeaponsShare_SoItFeedsItsOwnLadder()
    {
        // Weapon XP is the weapon's own mitigated damage, and the bonus is part
        // of that damage: proficiency feeds the very ladder that granted it.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        Levelled(a, WeaponClass.Dagger, 2);
        int before = a.WeaponXp[WeaponClass.Dagger];
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);

        Assert.True(turns.TryAttack(a, dummy));

        Assert.Equal(200 - 16, dummy.Hp);                             // 15 + 1, nothing between it and HP
        Assert.Equal(before + 16, a.WeaponXp[WeaponClass.Dagger]);    // credited what it dealt, bonus included
    }

    [Fact]
    public void ALevelCrossedMidSwingPaysTheLaterBodies()
    {
        // Each body credits as it is struck and each hit reads the level afresh
        // at step 1, so a fan that crosses a ladder step mid-swing pays the new
        // level to the bodies it has not reached yet. The axe below stands one
        // body short of level 2, so the one it catches takes the +1 the one it
        // aimed at bought. Deterministic — target order is the reach's, and no
        // roll is re-taken — and pinned here so that a fixture's single extra
        // point of damage reads as a level-up and not an arithmetic slip.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "great_axe");                    // 18, Cleave x2
        a.WeaponXp[WeaponClass.Axe] = XpForLevel(2) - 18;        // one body short of the bar
        var aimed = Enemy(5, 6);
        var fanned = Enemy(6, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { aimed, fanned }, () => 10);
        Assert.Equal(1, a.WeaponLevel(a.EquippedWeapon!));

        Assert.True(turns.TryAttack(a, aimed));

        Assert.Equal(200 - 18, aimed.Hp);                        // swung at level 1
        Assert.Equal(200 - 19, fanned.Hp);                       // the aimed body's 18 bought level 2 mid-swing
        Assert.Equal(2, a.WeaponLevel(a.EquippedWeapon!));
    }

    // ── The movement discount ────────────────────────────────────────────────

    [Fact]
    public void MovementDiscount_IsOnePerThreeLevels_AfterLight_NeverBelowOne()
    {
        // The weapon's number is the weapon's — Light already in it — and the
        // discount is the wielder's, taken off it at the use site. Floored at 1,
        // because nothing in this game is free.
        var a = Member("flensing_knife");
        var knife = a.EquippedWeapon!;
        Assert.Equal(27, knife.ResolvedCost);        // 30 less Light x1
        Assert.Equal(27, a.MovementCost(knife));     // level 1: floor(1 / 3) is nothing

        Assert.Equal(27, Levelled(a, WeaponClass.Dagger, 2).MovementCost(knife));
        Assert.Equal(26, Levelled(a, WeaponClass.Dagger, 3).MovementCost(knife));
        Assert.Equal(24, Levelled(a, WeaponClass.Dagger, 9).MovementCost(knife));

        // A weapon that costs a single unit never becomes free, whatever the career.
        var feather = TestWeapons.Make("Featherblade", 40, 5, 1);
        a.Inventory[1] = feather;
        Assert.Equal(1, feather.ResolvedCost);
        Assert.Equal(1, a.MovementCost(feather));
        Levelled(a, WeaponClass.Dagger, 99);
        Assert.Equal(1, a.MovementCost(feather));
        Assert.Equal(1, a.MovementCost(knife));      // 27 less 33, floored

        // The item's own cost is untouched by any of it: the statline reads the
        // same in anyone's hands.
        Assert.Equal(27, knife.ResolvedCost);
    }

    [Fact]
    public void AnAttackAtExactlyTheDiscountedBudgetLands()
    {
        // The gate and the charge read one number. A member holding exactly the
        // discounted cost swings and is emptied by it; one unit short is refused.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "flensing_knife");
        Levelled(a, WeaponClass.Dagger, 9);
        var dummy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { dummy }, () => 10);

        int cost = a.MovementCost(a.EquippedWeapon!);
        Assert.Equal(24, cost);

        a.DistLeft = cost - 1;
        Assert.False(turns.CanAttack(a, dummy));
        Assert.False(turns.TryAttack(a, dummy));

        a.DistLeft = cost;
        Assert.True(turns.CanAttack(a, dummy));
        Assert.True(turns.TryAttack(a, dummy));
        Assert.Equal(0f, a.DistLeft);
    }

    // ── The baseline both effects rest on ────────────────────────────────────

    [Fact]
    public void LevelOneChangesNothing()
    {
        // Levels start at 1 and both effects floor to nothing there, which is
        // what lets every scenario written before §2.2 stand unchanged.
        var a = Member("weakspot_stiletto");
        var weapon = a.EquippedWeapon!;
        var target = Bare();

        Assert.Equal(0, a.WeaponXp.Total);
        Assert.Equal(Progression.StartingLevel, a.WeaponLevel(weapon));
        Assert.Equal(weapon.Damage, Settle(a, target, 10, 0).WeaponShare);
        Assert.Equal(weapon.ResolvedCost, a.MovementCost(weapon));
    }

    [Fact]
    public void AnEnemyWieldsAtTheBaseline()
    {
        // An enemy carries no pools, so it wields everything at the level a
        // class nobody has trained is wielded at — asked of the actor, never
        // decided by asking what kind of actor it is. The two scaled enemy cost
        // sites multiply exactly this number, so they do not move either.
        var enemy = new EnemyState { Weapon = TestWeapons.Get("great_axe") };
        foreach (string id in new[] { "great_axe", "flensing_knife", "staff_of_renewal", "longbow" })
        {
            var weapon = TestWeapons.Get(id);
            Assert.Equal(Progression.StartingLevel, enemy.WeaponLevel(weapon));
            Assert.Equal(weapon.ResolvedCost, enemy.MovementCost(weapon));
        }

        // And it swings for the weapon's own damage, whatever anyone else has
        // earned with one.
        var target = Bare();
        var axe = enemy.Weapon;
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        Assert.Equal(axe.Damage, table.Raise(GameEvent.DamageTaken, DamagePayload.Initial(axe, 10, 0), enemy, target).WeaponShare);
    }
}
