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
/// parallel, so a class that calls <see cref="Use"/> must sit in the
/// <see cref="Collection"/> collection, which runs with nothing else. Content
/// built from parts without being made current comes from <see cref="Load"/>.
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
