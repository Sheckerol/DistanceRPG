using System.Reflection;
using System.Reflection.Emit;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Progression as a party member carries it (§2.1, §2.2): an innate spread, the
/// three raw XP numbers, and the maxima computed from them. Nothing here credits
/// XP through the turn system — the appliers come next — so every claim is about
/// the state and its arithmetic: the pools open level whatever the spread, they
/// rise a point per bar with the stat dividing it, and the three integers are
/// the whole progression.
/// <para>
/// In the content collection because one test swaps <see cref="Tuning"/> to show
/// the starting bar is a key and not a literal.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class ProgressionStateTests
{
    /// <summary>The roster's spread for <paramref name="id"/>: the doc's table, read where the game reads it.</summary>
    private static InnateStats Spread(string id) => GameContent.Current.Party[id].Stats;

    private static int StartingPool => GameContent.Current.Tuning.StartingPool;

    private static int Credit(PartyMemberState member, XpPool pool, int amount) => member.Credit(new XpCredit(pool, null, amount));

    private static int Credit(PartyMemberState member, WeaponClass cls, int amount) => member.Credit(new XpCredit(XpPool.Weapon, cls, amount));

    [Fact]
    public void BothPoolsStartAtTheStartingPool_WhateverTheSpread()
    {
        // "Nobody starts with more of anything": A is DEX 4 / CON 2 and D is
        // INT 4 / CON 1, and they open the game with the same 30 HP and the same
        // 30 mana. The stat is a rate, so it shows up in what a point costs and
        // never in what a pool holds.
        var a = TestPools.Fresh("A", Spread("A"));
        var d = TestPools.Fresh("D", Spread("D"));

        Assert.Equal(30, StartingPool);
        Assert.NotEqual(a.Stats, d.Stats);
        Assert.Equal((StartingPool, StartingPool), (a.MaxHp, a.MaxMana));
        Assert.Equal((StartingPool, StartingPool), (d.MaxHp, d.MaxMana));

        // And both spawn full: an unwritten pool reads as the maximum.
        Assert.Equal((StartingPool, StartingPool), (a.Hp, a.Mana));
        Assert.Equal((StartingPool, StartingPool), (d.Hp, d.Mana));

        // What differs is the price of the next point: 30 / 2 against 30 / 1 for
        // health, 30 / 3 against 30 / 4 for mana.
        Assert.Equal((15, 10), (a.HealthPool.XpToNext, a.ManaPool.XpToNext));
        Assert.Equal((30, 7), (d.HealthPool.XpToNext, d.ManaPool.XpToNext));
    }

    [Fact]
    public void StartingPool_IsTheTuningKey_NotALiteral()
    {
        // The 30 is one content key, read by both pools on every read, so a
        // tuning swap moves a member's maxima with no member rebuilt and nothing
        // invalidated.
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { StartingPool = 40 });

        var member = TestPools.Fresh("A", Spread("A"));
        Assert.Equal((40, 40), (member.MaxHp, member.MaxMana));
        Assert.Equal(20, member.HealthPool.XpToNext);   // 40 / CON 2
    }

    [Fact]
    public void MaxHp_RisesOnePointPerBar_ConDividingIt()
    {
        // CON 4 buys a point for a quarter of the bar; CON 1 pays the whole of
        // it. Neither gains anything for the XP below the bar, because the credit
        // is raw and only the threshold knows the stat.
        var fighter = TestPools.Fresh("B", Spread("B"));   // CON 4
        Assert.Equal(0, Credit(fighter, XpPool.Health, 6));
        Assert.Equal((StartingPool, 6), (fighter.MaxHp, fighter.HealthPool.XpIntoNext));
        Assert.Equal(1, Credit(fighter, XpPool.Health, 1));
        Assert.Equal((StartingPool + 1, 7), (fighter.MaxHp, fighter.HpXp));

        var wizard = TestPools.Fresh("D", Spread("D"));    // CON 1
        Assert.Equal(0, Credit(wizard, XpPool.Health, 29));
        Assert.Equal(StartingPool, wizard.MaxHp);
        Assert.Equal(1, Credit(wizard, XpPool.Health, 1));
        Assert.Equal((StartingPool + 1, 30), (wizard.MaxHp, wizard.HpXp));
    }

    [Fact]
    public void MaxMana_RisesOnePointPerBar_IntDividingIt()
    {
        // The same arithmetic against INT: D's mana grows four times as fast as
        // C's, and C is the one holding an axe.
        var caster = TestPools.Fresh("D", Spread("D"));    // INT 4
        Assert.Equal(1, Credit(caster, XpPool.Mana, 7));
        Assert.Equal(StartingPool + 1, caster.MaxMana);

        var axe = TestPools.Fresh("C", Spread("C"));       // INT 1
        Assert.Equal(0, Credit(axe, XpPool.Mana, 29));
        Assert.Equal(1, Credit(axe, XpPool.Mana, 1));
        Assert.Equal(StartingPool + 1, axe.MaxMana);
        Assert.Equal(StartingPool, axe.MaxHp);             // and nothing else moved
    }

    [Fact]
    public void AFighterAndAWizardOpenTheSame_AndDivergeFourfold()
    {
        // Fed identical healing, B (CON 4) banks four points where D (CON 1)
        // banks one -- 7 + 7 + 8 + 8 against a single 30. The XP is the same
        // number in both books: nothing multiplies a gain by a stat.
        var fighter = TestPools.Fresh("B", Spread("B"));
        var wizard = TestPools.Fresh("D", Spread("D"));
        Assert.Equal(4, Credit(fighter, XpPool.Health, 30));
        Assert.Equal(1, Credit(wizard, XpPool.Health, 30));

        Assert.Equal(fighter.HpXp, wizard.HpXp);
        Assert.Equal((StartingPool + 4, StartingPool + 1), (fighter.MaxHp, wizard.MaxHp));

        // The mirror, on a ladder rather than a pool, and against the member the
        // claim actually names: D will never be the better knife-fighter however
        // much damage their knife does. Fed the same 150 dagger XP as A (DEX 4),
        // that same D is a level behind -- A has paid 25 + 50 + 75 exactly and
        // wields at 4, D has paid 33 + 66 and is still on 3 with 51 carried --
        // and both books hold the same number, because the stat is in the
        // threshold and never in the credit.
        var thief = TestPools.Fresh("A", Spread("A"));
        Credit(thief, WeaponClass.Dagger, 150);
        Credit(wizard, WeaponClass.Dagger, 150);

        Assert.Equal(thief.WeaponXp[WeaponClass.Dagger], wizard.WeaponXp[WeaponClass.Dagger]);
        var dagger = TestWeapons.Get("weakspot_stiletto");
        Assert.Equal(4, thief.WeaponLevel(dagger));    // 150 paid, nothing carried into a 100 bar
        Assert.Equal(3, wizard.WeaponLevel(dagger));   // 99 paid, 51 carried: half a bar short of A
    }

    [Fact]
    public void Pools_AreTheWholeState()
    {
        // The seam Phase 4's death rollback and Phase 5's save need: three plain
        // integers are the entire progression, so writing them back restores
        // every derived number. A member handed the raw XP reads exactly like the
        // member who earned it.
        var stats = Spread("A");
        var dagger = TestWeapons.Get("weakspot_stiletto");

        var earned = TestPools.Fresh("A", stats);
        Credit(earned, XpPool.Health, 40);
        Credit(earned, XpPool.Mana, 30);
        Credit(earned, WeaponClass.Dagger, 150);

        var restored = TestPools.Fresh("A", stats);
        restored.HpXp = earned.HpXp;
        restored.ManaXp = earned.ManaXp;
        restored.WeaponXp[WeaponClass.Dagger] = earned.WeaponXp[WeaponClass.Dagger];

        Assert.Equal((earned.MaxHp, earned.MaxMana), (restored.MaxHp, restored.MaxMana));
        Assert.Equal(earned.HealthPool, restored.HealthPool);
        Assert.Equal(earned.ManaPool, restored.ManaPool);
        Assert.Equal(earned.WeaponLevel(dagger), restored.WeaponLevel(dagger));
        Assert.Equal(earned.Hp, restored.Hp);            // both full: one filled as it earned, one never written
        Assert.Equal(earned.Stats, restored.Stats);
    }

    [Fact]
    public void WeaponXp_IsPerClassPerCharacter_AndSurvivesASwap()
    {
        // A proficiency is the character's and the class's: swapping to a staff
        // and back leaves the dagger ladder exactly where it was, any dagger
        // wields at that level, and the member next to them has learned nothing.
        var a = TestPools.Fresh("A", Spread("A"));
        var stiletto = TestWeapons.Get("weakspot_stiletto");
        var knife = TestWeapons.Get("flensing_knife");
        var staff = TestWeapons.Get("staff_of_renewal");

        a.Inventory[0] = stiletto;
        Credit(a, WeaponClass.Dagger, 60);
        a.Inventory[0] = staff;
        Credit(a, WeaponClass.Staff, 10);
        a.Inventory[0] = stiletto;

        Assert.Equal(60, a.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(10, a.WeaponXp[WeaponClass.Staff]);
        Assert.Equal(2, a.WeaponLevel(stiletto));       // DEX 4: the first step cost 25
        Assert.Equal(2, a.WeaponLevel(knife));          // a different dagger, the same proficiency
        Assert.Equal(Progression.StartingLevel, a.WeaponLevel(TestWeapons.Get("great_axe")));
        Assert.Equal(new[] { (WeaponClass.Dagger, 60), (WeaponClass.Staff, 10) }, a.WeaponXp.Entries.ToArray());
        Assert.Equal(70, a.WeaponXp.Total);

        var twin = TestPools.Fresh("A2", Spread("A"));
        Assert.Equal(0, twin.WeaponXp[WeaponClass.Dagger]);
        Assert.Equal(Progression.StartingLevel, twin.WeaponLevel(stiletto));
    }

    [Fact]
    public void Stats_AreFixedAtCreation()
    {
        // Three claims, because "never rise" has to hold against the compiler as
        // well as against the code: the property is init-only, so no assignment
        // past construction compiles; nothing in the game assembly writes it
        // outside a method that is building a member; and no amount of credit
        // moves a spread.
        var setter = typeof(PartyMemberState).GetProperty(nameof(PartyMemberState.Stats))!.SetMethod!;
        Assert.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());

        var late = Calls(typeof(PartyMemberState).Assembly)
            .Where(c => c.Target is MethodInfo { Name: "set_Stats" } m && m.DeclaringType == typeof(PartyMemberState))
            .Where(c => !BuildsAMember(c.From))
            .Select(Describe)
            .Distinct()
            .ToArray();
        Assert.Equal([], late);

        // The scanner is not reading an empty assembly: the test fixtures do set
        // a spread, at construction, and are found.
        Assert.Contains(
            Calls(typeof(TestPools).Assembly),
            c => c.Target is MethodInfo { Name: "set_Stats" } m && m.DeclaringType == typeof(PartyMemberState));

        var member = TestPools.Fresh("B", Spread("B"));
        Credit(member, XpPool.Health, 400);
        Credit(member, XpPool.Mana, 400);
        Credit(member, WeaponClass.Sword, 400);
        Assert.Equal(Spread("B"), member.Stats);
    }

    [Fact]
    public void Hp_ReadsFullUntilWritten()
    {
        // HP nobody has written reads as the maximum, so a member who has earned
        // points spawns at the new full rather than at a number copied when they
        // were made. A member who has been hurt keeps its number, and a raw XP
        // write -- a rollback, a load -- moves the ceiling without healing anyone.
        var fresh = TestPools.Fresh("B", Spread("B"));
        Assert.Equal(fresh.MaxHp, fresh.Hp);

        Credit(fresh, XpPool.Health, 30);                 // CON 4: four points
        Assert.Equal((StartingPool + 4, StartingPool + 4), (fresh.MaxHp, fresh.Hp));

        var wounded = TestPools.Fresh("B", Spread("B"));
        wounded.Hp = 10;
        wounded.HpXp = 30;                                 // written raw, not credited
        Assert.Equal((StartingPool + 4, 10), (wounded.MaxHp, wounded.Hp));
    }

    [Fact]
    public void AGainedPointFillsHpButNotMana()
    {
        // A split, and a deliberate one. Constitution grows by getting hurt and
        // then healed, so an HP point that arrived empty would leave the member
        // one short of the ceiling until some later heal: the point arrives
        // filled. Mana is earned by spending, so filling a gained point would
        // hand part of the cast back; the ceiling rises and the pool stays where
        // the spend left it, to be refilled by unspent movement like any other.
        var hurt = TestPools.Fresh("B", Spread("B"));      // CON 4
        hurt.Hp = hurt.MaxHp - 10;
        Assert.Equal(1, Credit(hurt, XpPool.Health, 7));
        Assert.Equal((StartingPool + 1, StartingPool - 10 + 1), (hurt.MaxHp, hurt.Hp));

        var caster = TestPools.Fresh("D", Spread("D"));    // INT 4
        caster.Mana = caster.MaxMana - 13;                 // what a Renewal cast leaves
        Assert.Equal(2, Credit(caster, XpPool.Mana, 14));
        Assert.Equal(StartingPool + 2, caster.MaxMana);
        Assert.Equal(StartingPool - 13, caster.Mana);      // untouched: casting never pays for itself

        // And a pool nobody has written is no exception, though it is the one
        // that would break the rule for free: it reads as its own ceiling, so a
        // ceiling that rose unpinned would hand over a filled point.
        var unspent = TestPools.Fresh("D", Spread("D"));   // INT 4, mana never written
        Assert.Equal(2, Credit(unspent, XpPool.Mana, 14));
        Assert.Equal(StartingPool + 2, unspent.MaxMana);
        Assert.Equal(StartingPool, unspent.Mana);          // what the old bar held, not what the new one holds
    }

    [Fact]
    public void EveryFixtureMemberIsGrown()
    {
        // The sweep's guard. A member left at the real starting pool inside a
        // combat fixture changes what the scenario does without failing anything
        // that names a pool -- an enemy-HP assertion still passes while the run
        // that produced it is a different run. So there is exactly one place a
        // test builds a member, and this reads the test assembly's IL to say so.
        var offenders = Calls(typeof(TestPools).Assembly)
            .Where(c => c.Op == OpCodes.Newobj && c.Target.DeclaringType == typeof(PartyMemberState))
            .Where(c => Root(c.From.DeclaringType) != typeof(TestPools))
            .Select(Describe)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([], offenders);

        // The factory has a second door, and it opens on exactly the state this
        // guard exists to forbid: TestPools.Fresh hands back a member at the real
        // 25/25, which is what a progression test wants and what a combat fixture
        // must never hold. A file calling Fresh instead of Char satisfies the
        // assertion above while dropping its members to a quarter of the pool
        // every scenario was written against, so name those callers too.
        static bool Allowed(Type? root) => root == typeof(TestPools) || root == typeof(ProgressionStateTests);

        var freshCallers = Calls(typeof(TestPools).Assembly)
            .Where(c => c.Target is MethodInfo { Name: nameof(TestPools.Fresh) } m && m.DeclaringType == typeof(TestPools))
            .Where(c => !Allowed(Root(c.From.DeclaringType)))
            .Select(Describe)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([], freshCallers);

        // PartyMemberState.From is the third door, and it opens on the same
        // state: the game's own construction hands back a member at the starting
        // pool. It is the scene's to call, and a progression test's; a combat
        // fixture reaching for it would drop its members to a quarter of the pool
        // as silently as building one by hand would.
        var fromCallers = Calls(typeof(TestPools).Assembly)
            .Where(c => c.Target is MethodInfo { Name: nameof(PartyMemberState.From) } m && m.DeclaringType == typeof(PartyMemberState))
            .Where(c => !Allowed(Root(c.From.DeclaringType)))
            .Select(Describe)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([], fromCallers);

        // And the scanner does find each of the calls that are allowed, so no
        // assertion above can pass by finding nothing at all.
        Assert.Contains(
            Calls(typeof(TestPools).Assembly),
            c => c.Op == OpCodes.Newobj && c.Target.DeclaringType == typeof(PartyMemberState));
        Assert.Contains(
            Calls(typeof(TestPools).Assembly),
            c => c.Target is MethodInfo { Name: nameof(TestPools.Fresh) } m && m.DeclaringType == typeof(TestPools)
                && Root(c.From.DeclaringType) == typeof(ProgressionStateTests));
        Assert.Contains(
            Calls(typeof(TestPools).Assembly),
            c => c.Target is MethodInfo { Name: nameof(PartyMemberState.From) } m && m.DeclaringType == typeof(PartyMemberState)
                && Root(c.From.DeclaringType) == typeof(ProgressionStateTests));
    }

    [Fact]
    public void RosterSize_MatchesThePresentationsCapacity()
    {
        // SpawnParty walks the spawn tiles it asked for, one per party colour, so
        // a roster longer than that capacity would spawn a prefix and drop
        // whoever came last without a word. The loader refuses a roster that is
        // not PartySize; this is the other end of the same rule.
        Assert.Equal(GameConstants.PartySize, DungeonScene.PartyColorCount);
        Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
    }

    [Fact]
    public void From_BuildsAMemberFromItsRosterEntry()
    {
        // The scene's construction, moved into Logic so it can be asserted. Every
        // field of a spawned member comes off the roster row: the id a save will
        // name them by, the spread, the weapon in hand and the bag behind it, in
        // file order. The scene supplies only where they stand.
        var roster = GameContent.Current.Party.All;
        var party = roster.Select((def, i) => PartyMemberState.From(def, i, i * 32f, 64f)).ToArray();

        Assert.Equal(new[] { "A", "B", "C", "D" }, party.Select(m => m.Id));
        Assert.Equal(new[] { 0, 1, 2, 3 }, party.Select(m => m.ColorIndex));
        Assert.Equal(roster.Select(d => d.Stats), party.Select(m => m.Stats));
        Assert.Equal(new[] { "weakspot_stiletto", "tower_guard", "great_axe", "staff_of_renewal" },
            party.Select(m => m.EquippedWeapon!.Id));
        Assert.Equal(new[] { "staff_of_renewal", "staff_of_renewal", "staff_of_renewal", "staff_of_mire" },
            party.Select(m => m.Inventory[1]!.Id));
        Assert.All(party, m => Assert.Null(m.Inventory[2]));
        Assert.Equal((0f, 64f), (party[0].X, party[0].Y));
        Assert.Equal((96f, 64f), (party[3].X, party[3].Y));

        // Two members never share a weapon instance, or one member's wear and
        // grafts would be another's.
        Assert.Equal(4, party.Select(m => m.Inventory[1]!).Distinct().Count());

        // And the point of the whole exercise: the spread reaches the member, so
        // the §2.1 table divides a real threshold. A spawned member used to carry
        // InnateStats.None -- 1/1/1/1, not even a permutation -- and every pool
        // and every ladder divided by 1, so the four grew at identical rates and
        // the table might as well not have existed.
        var (c, d) = (party[2], party[3]);
        Assert.Equal(new InnateStats(4, 2, 3, 1), c.Stats);
        Assert.Equal(new InnateStats(2, 3, 1, 4), d.Stats);
        Assert.All(party, m => Assert.True(m.Stats.IsPermutation, $"{m.Id}: {m.Stats}"));

        // D is always the better caster, and no amount of swinging a staff makes
        // C one: the fourfold threshold difference is on the spawned members.
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Staff, d.Stats));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Staff, c.Stats));

        // The mirror, and the class table resolving to something other than 1
        // for once: C's axe against D's, and B's sword taking the better of
        // STR 3 and DEX 1 rather than either in particular.
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Axe, c.Stats));
        Assert.Equal(2, Progression.GoverningStat(WeaponClass.Axe, d.Stats));
        Assert.Equal(3, Progression.GoverningStat(WeaponClass.Sword, party[1].Stats));

        // They open the game identical and diverge only through growth: the same
        // 25 mana, the same 25 HP, and then the same credit buying D four points
        // where it buys C one.
        Assert.Equal((StartingPool, StartingPool), (c.MaxMana, d.MaxMana));
        Assert.Equal((StartingPool, StartingPool), (c.MaxHp, d.MaxHp));
        Assert.Equal(1, Credit(c, XpPool.Mana, StartingPool));
        Assert.Equal(4, Credit(d, XpPool.Mana, StartingPool));

        // The staff ladder's first rung costs D 25 and C 100 -- the fourfold
        // difference, read off members the scene built rather than off the data.
        // The same 25 buys D the rung and leaves C exactly where it was.
        Assert.Equal(25, d.Proficiency(WeaponClass.Staff).XpToNext);
        Assert.Equal(100, c.Proficiency(WeaponClass.Staff).XpToNext);
        Assert.Equal(0, Credit(c, WeaponClass.Staff, 25));
        Assert.Equal(1, Credit(d, WeaponClass.Staff, 25));
        Assert.Equal(Progression.StartingLevel, c.WeaponLevel(c.Inventory[1]!));
        Assert.Equal(Progression.StartingLevel + 1, d.WeaponLevel(d.EquippedWeapon!));
    }

    [Fact]
    public void SpearDummiesStillBraceAgainstTheParty()
    {
        // §2.1's tutorial lesson, re-asserted over the party the roster actually
        // builds: nobody carries a spear any more, so Brace is taught from the
        // receiving end -- walking into a spear dummy's reach still costs a free
        // poke, through the same one move path Phase 1 shipped.
        var catalogue = GameContent.Current.Weapons;
        Assert.DoesNotContain(
            WeaponClass.Spear,
            GameContent.Current.Party.All
                .SelectMany(m => m.BagWeaponIds.Prepend(m.StartingWeaponId))
                .Select(id => catalogue[id].Class));

        const float tile = GameConstants.Tile;
        var grid = new int[20, 30];
        var a = PartyMemberState.From(GameContent.Current.Party["A"], 0, 5 * tile + 16f, 5 * tile + 16f);
        var enemy = new EnemyState { X = a.X + 200f, Y = a.Y, Weapon = TestWeapons.Get("skirmishers_pike") };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        int braces = 0;
        turns.EnemyBraceTriggered += _ => braces++;
        turns.NotifyEnemyVisible(enemy, true);

        // Outside the spear's reach (162 surface > 128): stepping around is safe.
        a.X += 10f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(0, braces);
        Assert.Equal(StartingPool, a.Hp);

        // Into reach: the free poke, 7 plus Longshot x1's +1 at the fourth tile,
        // and A's dagger blocks nothing. A roster member opens at the starting
        // pool, so this is what the lesson costs a fresh party.
        a.X = enemy.X - 150f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(1, braces);
        Assert.Equal(StartingPool - 8, a.Hp);
    }

    [Fact]
    public void From_RejectsARosterEntryThatNamesAMissingWeapon()
    {
        // From trusts its def, and the loader is what makes that safe: a typo in
        // party.json is a ContentException naming the member and the rule, raised
        // before anything spawns -- never a KeyNotFoundException halfway through
        // building the party, with two members on the floor and two not.
        var typo = new PartyData(ContentDefaults.Party.Members
            .Select(m => m.Id == "C" ? m with { StartingWeaponId = "great_axe_of_typos" } : m)
            .ToList());

        var refused = Assert.Throws<ContentException>(() => TestContent.Use(party: typo));
        Assert.Equal("C", refused.EntryId);
        Assert.Equal(ContentValidator.RulePartyWeaponExists, refused.Rule);

        // The refusal left the running roster alone, so the four members that do
        // spawn are still the four the validator passed.
        Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
        Assert.All(GameContent.Current.Party.All, def => PartyMemberState.From(def, 0, 0f, 0f));

        // And the other end of the same rule: handed a def the loader never saw,
        // From does throw, which is why the loader has to be the one to look.
        Assert.Throws<KeyNotFoundException>(
            () => PartyMemberState.From(typo.Members[2], 2, 0f, 0f));
    }

    // ---- reading IL: the two guards above are claims about code, not about state ----

    /// <summary>The outermost type a method belongs to, so a lambda's closure or an iterator's state machine is charged to the class that wrote it.</summary>
    private static Type? Root(Type? type)
    {
        while (type?.DeclaringType is { } outer)
            type = outer;
        return type;
    }

    private static string Describe((MethodBase From, MethodBase Target, OpCode Op) call)
        => $"{call.From.DeclaringType?.FullName}.{call.From.Name}";

    /// <summary>True when <paramref name="method"/> constructs a member, so a write to an init-only property there is part of building one.</summary>
    private static bool BuildsAMember(MethodBase method)
        => Body(method) is { } il && Instructions(il).Any(i => i.Op == OpCodes.Newobj
            && Resolve(method, i.Token)?.DeclaringType == typeof(PartyMemberState));

    /// <summary>Every call, callvirt and newobj in <paramref name="assembly"/>, with the method it sits in.</summary>
    private static IEnumerable<(MethodBase From, MethodBase Target, OpCode Op)> Calls(Assembly assembly)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var type in Types(assembly))
        {
            var methods = type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags));
            foreach (var method in methods)
            {
                if (Body(method) is not { } il) continue;
                foreach (var (op, token) in Instructions(il))
                {
                    if (op != OpCodes.Newobj && op != OpCodes.Call && op != OpCodes.Callvirt) continue;
                    if (Resolve(method, token) is { } target)
                        yield return (method, target, op);
                }
            }
        }
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t is not null)!;
        }
    }

    private static byte[]? Body(MethodBase method)
    {
        try
        {
            return method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            return null;   // no managed body to read: an extern, a runtime intrinsic
        }
    }

    private static MethodBase? Resolve(MethodBase from, int token)
    {
        try
        {
            var typeArgs = from.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
            var methodArgs = from is MethodInfo { IsGenericMethodDefinition: true } m ? m.GetGenericArguments() : null;
            return from.Module.ResolveMethod(token, typeArgs, methodArgs);
        }
        catch (Exception)
        {
            return null;   // a token that is not a method this assembly can name
        }
    }

    /// <summary>
    /// Walk a method body one instruction at a time. A byte-wise search for the
    /// opcode would match the same byte inside an operand, so every instruction's
    /// operand is measured and skipped.
    /// </summary>
    private static IEnumerable<(OpCode Op, int Token)> Instructions(byte[] il)
    {
        int i = 0;
        while (i < il.Length)
        {
            short code = il[i++];
            if (code == 0xFE && i < il.Length)
                code = (short)((0xFE << 8) | il[i++]);
            if (!OpCodeTable.TryGetValue(code, out var op))
                yield break;   // not something this walker knows: stop rather than guess a length

            int operand = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, i)),
                _ => 4,
            };
            if (i + operand > il.Length) yield break;

            int token = op.OperandType == OperandType.InlineMethod ? BitConverter.ToInt32(il, i) : 0;
            yield return (op, token);
            i += operand;
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);
}
