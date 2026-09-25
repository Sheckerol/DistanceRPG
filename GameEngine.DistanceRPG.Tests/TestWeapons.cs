using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Weapons for tests: catalogue instances by stable id, and ad-hoc weapons by
/// statline and forged spread for the cases the catalogue does not carry.
/// </summary>
public static class TestWeapons
{
    /// <summary>A fresh instance of the catalogue weapon <paramref name="id"/>; a wand needs its element.</summary>
    public static Weapon Get(string id, DamageType? element = null)
        => GameContent.Current.Weapons.Instantiate(id, element);

    /// <summary>An ad-hoc dagger-class weapon with a forged spread and no enchantments. Validation is the catalogue's, not this.</summary>
    public static Weapon Make(string name, int range, int damage, int cost, params (ModifierType Type, int Stacks)[] forged)
        => Make(name, range, damage, cost, manaCost: 0, WeaponClass.Dagger, forged);

    public static Weapon Make(string name, int range, int damage, int cost, int manaCost, WeaponClass cls,
        params (ModifierType Type, int Stacks)[] forged)
    {
        var spread = new Dictionary<ModifierType, int>();
        foreach (var (type, stacks) in forged)
            spread[type] = spread.GetValueOrDefault(type) + stacks;
        var def = new WeaponDef(
            Id: "test_" + name.ToLowerInvariant().Replace(' ', '_'), Name: name, Class: cls, Role: null,
            Range: range, Damage: damage, Cost: cost, ManaCost: manaCost,
            Forged: spread, Enchantments: [], Shape: null, Unique: false, DerivedFrom: null);
        return new Weapon(def, []);
    }

    /// <summary>
    /// An ad-hoc dagger-class weapon with no forged spread and the catalogue
    /// enchantments <paramref name="enchantmentIds"/> attached in that order at
    /// tier 1, for what reads a weapon's list — the loop's printed chain. The
    /// catalogue's validation (a caster's innate, a wand's element) is not this.
    /// </summary>
    public static Weapon Enchanted(string name, int range, int damage, int cost, params string[] enchantmentIds)
    {
        var catalogue = GameContent.Current.Enchantments;
        var def = new WeaponDef(
            Id: "test_" + name.ToLowerInvariant().Replace(' ', '_'), Name: name, Class: WeaponClass.Dagger, Role: null,
            Range: range, Damage: damage, Cost: cost, ManaCost: 0,
            Forged: new Dictionary<ModifierType, int>(), Enchantments: enchantmentIds.Select(id => new EnchantmentRef(id)).ToList(),
            Shape: null, Unique: false, DerivedFrom: null);
        return new Weapon(def, enchantmentIds.Select(id => new Enchantment(catalogue[id], Tier: 1)).ToList());
    }
}

/// <summary>
/// Swap <see cref="GameContent.Current"/> for one test, restored on dispose.
/// The current content is process-wide and xUnit runs test classes in
/// parallel, so a class that calls <see cref="Use"/> or <see cref="UsePremise"/>
/// must sit in the <see cref="Collection"/> collection, which runs with nothing
/// else. Content built from parts without being made current comes from
/// <see cref="Load"/>.
/// </summary>
public static class TestContent
{
    public const string Collection = "GameContent";

    /// <summary>Load content with the given parts (the shipped content's for the rest) and make it current until disposed.</summary>
    public static IDisposable Use(RestrictedData? restricted = null, WeaponsData? weapons = null,
        EnchantmentsData? enchantments = null, Tuning? tuning = null, PartyData? party = null, DungeonsData? dungeons = null)
    {
        var previous = GameContent.Current;
        GameContent.Use(Load(tuning, restricted, enchantments, weapons, party, dungeons));
        return new Restore(previous);
    }

    /// <summary>
    /// Make the shipped content under the premise current until disposed: the
    /// numbers the suite's worked examples were written against
    /// (<see cref="Premise"/>) — what a test stands on without testing — so the
    /// master copy may move them without failing it. With nothing given it is the
    /// premise's content as it is, loaded once; a test that needs one more thing
    /// edits a part from the premise's own — the tuning, the weapons, the dungeons —
    /// or gives the relations, the enchantment catalogue or the party whole, as
    /// <see cref="Use"/> takes them. A class that calls it sits in <see cref="Collection"/>.
    /// </summary>
    public static IDisposable UsePremise(Func<Tuning, Tuning>? tuning = null, Func<WeaponsData, WeaponsData>? weapons = null,
        Func<DungeonsData, DungeonsData>? dungeons = null, RestrictedData? restricted = null, EnchantmentsData? enchantments = null,
        PartyData? party = null)
    {
        var previous = GameContent.Current;
        GameContent.Use(tuning is null && weapons is null && dungeons is null && restricted is null && enchantments is null && party is null
            ? Premise.Content
            : Load(tuning is null ? Premise.Tuning : tuning(Premise.Tuning), restricted, enchantments,
                weapons is null ? Premise.Weapons : weapons(Premise.Weapons), party,
                dungeons is null ? Premise.Dungeons : dungeons(Premise.Dungeons)));
        return new Restore(previous);
    }

    /// <summary>
    /// Load content with the given parts — the shipped content's
    /// (<see cref="ShippedContent"/>) for every part left null — validated as
    /// the game's load validates it, and never made current: the suite's one
    /// door to content built from parts. The parameters run in the order of
    /// <c>GameContent.Load</c>'s records overload, so a call reads as it did there.
    /// </summary>
    /// <exception cref="ContentException">The parts are not well formed together; the entry, the rule and the file whose stage broke are named.</exception>
    public static GameContent Load(Tuning? tuning = null, RestrictedData? restricted = null, EnchantmentsData? enchantments = null,
        WeaponsData? weapons = null, PartyData? party = null, DungeonsData? dungeons = null)
        => GameContent.Load(
            tuning ?? ShippedContent.Tuning,
            restricted ?? ShippedContent.Restricted,
            enchantments ?? ShippedContent.Enchantments,
            weapons ?? ShippedContent.Weapons,
            party ?? ShippedContent.Party,
            dungeons ?? ShippedContent.Dungeons);

    private sealed class Restore(GameContent previous) : IDisposable
    {
        public void Dispose() => GameContent.Use(previous);
    }
}

/// <summary>The collection classes using <see cref="TestContent.Use"/> join: it never runs beside another class.</summary>
[CollectionDefinition(TestContent.Collection, DisableParallelization = true)]
public sealed class GameContentCollection
{
}
