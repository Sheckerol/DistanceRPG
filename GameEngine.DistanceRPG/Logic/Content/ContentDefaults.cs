using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.VariantRole;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled fallback for every content file (§5.4): the instances the
/// game runs on when no file overrides them, and what Phase 5's loader falls
/// back to. Tuning, the relations, the enchantment catalogue's innates and the
/// thirty-two weapons — one line per entry, so the validator is the reviewer.
/// </summary>
public static class ContentDefaults
{
    /// <summary>The §5.3 scalars at their compiled values.</summary>
    public static readonly Tuning Tuning = new();

    /// <summary>The §1.1 relations as <c>restricted.json</c> would declare them.</summary>
    public static readonly RestrictedData Restricted = new(
        Excludes:
        [
            [nameof(Brace), nameof(Opportunist), nameof(Overwatch)],   // threat zone: one weapon, one zone it watches
            [nameof(Push), nameof(Drag), nameof(Rout)],                // displacement: one direction per weapon
            [nameof(Riposte), nameof(BlockWeaken)],                    // block response: one block, one payoff
            [nameof(CritWeaken), nameof(CritSunder)],                  // crit rider: one crit, one rider
            [nameof(Light), nameof(Resonant)],                         // currency: one discount per weapon
            ["flaming", "cold"],                                       // opposed damage types (enchantment ids)
            ["shocking", "acidic"],
        ],
        Requires: new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [nameof(Riposte)] = [nameof(Block)],       // nothing to counter off
            [nameof(BlockWeaken)] = [nameof(Block)],   // nothing to succeed at
            [nameof(Rout)] = [nameof(Cleave)],         // without a cleave it is just a worse Push
        },
        Kind: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(Brace)] = RestrictedData.MeleeKind,         // a threat zone is a weapon's physical reach
            [nameof(Opportunist)] = RestrictedData.MeleeKind,
            [nameof(Overwatch)] = RestrictedData.RangedKind,    // holding a shot is what a nocked arrow does
            // Resonant is deliberately absent: it discounts enchantment triggers too.
        },
        ForgedOnly: [nameof(Charges)],   // a cap, not a bonus: granted from zero it would make a bow worse
        NeverRolled: []);                // the unique enchantments, once they exist

    // ---- Enchantments (§5.7): the eight innates ----
    // Lock per the §3.3 catalogue. A status applier quotes no flat trigger: its
    // price is the DoT ladder over the levels it applies. Potency is the levels
    // one cast grants at ApplyPercent 100 — provisional, re-priced in Phase 3.
    // An element's ApplyPercent is the burn it feeds; the tuning table overrides it.

    /// <summary>The §5.7 catalogue as <c>enchantments.json</c> would declare it.</summary>
    public static readonly EnchantmentsData Enchantments = new(
    [
        Innate("regeneration", "Regeneration", TargetSide.Ally, @lock: 15, potency: 1, StatusEffectType.Regeneration),
        Innate("ward", "Ward", TargetSide.Ally, @lock: 20, potency: 5, StatusEffectType.Ward),
        Innate("poison", "Poison", TargetSide.Enemy, @lock: 20, potency: 3, StatusEffectType.Poison),
        Innate("mire", "Mire", TargetSide.Enemy, @lock: 25, potency: 2, StatusEffectType.Mire),
        Element("flaming", "Flaming", DamageType.Flaming, applyPercent: 20),
        Element("cold", "Cold", DamageType.Cold, applyPercent: 15),
        Element("shocking", "Shocking", DamageType.Shocking, applyPercent: 25),
        Element("acidic", "Acidic", DamageType.Acidic, applyPercent: 15),
    ]);

    /// <summary>The enchantment ids the default relations may name: the catalogue's.</summary>
    public static readonly IReadOnlySet<string> EnchantmentIds =
        new HashSet<string>(Enchantments.Enchantments.Select(e => e.Id), StringComparer.Ordinal);

    // ---- Weapons (§5.6): eight classes, four variants each ----
    // Statlines from §1.2/§1.3; a variant is its class baseline plus one added
    // modifier type. Cost columns are base costs: Light resolves an Efficiency
    // weapon down by 10% at instantiation, never here.

    private static readonly (ModifierType, int)[] DaggerBaseline = [(CritWindow, 1), (CritMultiplier, 1)];
    private static readonly (ModifierType, int)[] SwordBaseline = [(Block, 1), (Push, 1)];
    private static readonly (ModifierType, int)[] SpearBaseline = [(Brace, 1), (Longshot, 1)];
    private static readonly (ModifierType, int)[] AxeBaseline = [(Cleave, 1), (Opportunist, 1)];
    private static readonly (ModifierType, int)[] RangedBaseline = [(Longshot, 1), (CritWindow, 1)];
    private static readonly (ModifierType, int)[] ThrowingBaseline = [(Charges, 1), (CritMultiplier, 1)];
    private static readonly (ModifierType, int)[] CasterBaseline = [(Resonant, 1)];

    /// <summary>The §5.6 catalogue as <c>weapons.json</c> would declare it: the thirty-two, in class order.</summary>
    public static readonly WeaponsData Weapons = new(
    [
        // Dagger (DEX): 19-20 to crit, x3 when it lands.
        MartialDef("flensing_knife", "Flensing Knife", Dagger, Efficiency, 40, 15, 30, DaggerBaseline, (Light, 1)),
        MartialDef("assassins_fang", "Assassin's Fang", Dagger, Purity, 40, 15, 30, DaggerBaseline, (CritWindow, 1)),
        MartialDef("disarming_kris", "Disarming Kris", Dagger, Control, 40, 15, 30, DaggerBaseline, (CritWeaken, 1)),
        MartialDef("weakspot_stiletto", "Weakspot Stiletto", Dagger, Support, 40, 15, 30, DaggerBaseline, (CritSunder, 1)),
        // Sword & Shield (max(STR, DEX)): a shield bash — you stop them, then you move them.
        MartialDef("arming_sword", "Arming Sword", Sword, Efficiency, 80, 10, 50, SwordBaseline, (Light, 1)),
        MartialDef("tower_guard", "Tower Guard", Sword, Purity, 80, 10, 50, SwordBaseline, (Block, 1)),
        MartialDef("riposte_blade", "Riposte Blade", Sword, Control, 80, 10, 50, SwordBaseline, (Riposte, 1)),
        MartialDef("wardens_shield", "Warden's Shield", Sword, Support, 80, 10, 50, SwordBaseline, (BlockWeaken, 1)),
        // Spear (STR): reach 128, exactly four tiles, so Longshot's tile count is a rule.
        MartialDef("skirmishers_pike", "Skirmisher's Pike", Spear, Efficiency, 128, 7, 55, SpearBaseline, (Light, 1)),
        MartialDef("phalanx_spear", "Phalanx Spear", Spear, Purity, 128, 7, 55, SpearBaseline, (Brace, 1)),
        MartialDef("halberd", "Halberd", Spear, Control, 128, 7, 55, SpearBaseline, (Push, 1)),
        MartialDef("pinning_lance", "Pinning Lance", Spear, Support, 128, 7, 55, SpearBaseline, (Pin, 1)),
        // Axe (STR): nobody walks away from an axe. The Reaver forges Splitting x2.
        MartialDef("hatchet", "Hatchet", Axe, Efficiency, 60, 18, 60, AxeBaseline, (Light, 1)),
        MartialDef("great_axe", "Great Axe", Axe, Purity, 60, 18, 60, AxeBaseline, (Cleave, 1)),
        MartialDef("reaver", "Reaver", Axe, Control, 60, 18, 60, AxeBaseline, (Splitting, 2)),
        MartialDef("routing_axe", "Routing Axe", Axe, Support, 60, 18, 60, AxeBaseline, (Rout, 1)),
        // Ranged (DEX): damage 5 is meant to look low; the range curve does the work.
        MartialDef("hunting_bow", "Hunting Bow", Ranged, Efficiency, 320, 5, 30, RangedBaseline, (Light, 1)),
        MartialDef("longbow", "Longbow", Ranged, Purity, 320, 5, 30, RangedBaseline, (Longshot, 1)),
        MartialDef("pinning_bow", "Pinning Bow", Ranged, Control, 320, 5, 30, RangedBaseline, (Pin, 1)),
        MartialDef("crossbow", "Crossbow", Ranged, Support, 320, 5, 30, RangedBaseline, (Overwatch, 1)),
        // Throwing (STR): 15 a throw is honest only because Charges caps it.
        MartialDef("darts", "Darts", Throwing, Efficiency, 190, 9, 15, ThrowingBaseline, (Light, 1)),
        MartialDef("bandolier", "Bandolier", Throwing, Purity, 190, 9, 15, ThrowingBaseline, (Charges, 1)),
        MartialDef("harpoon", "Harpoon", Throwing, Control, 190, 9, 15, ThrowingBaseline, (Drag, 1)),
        MartialDef("softening_javelins", "Softening Javelins", Throwing, Support, 190, 9, 15, ThrowingBaseline, (Softening, 1)),
        // Staff (INT): Resonant x1 plus the effect it casts, fixed by variant. Range 100, cost 40; mana per variant.
        StaffDef("staff_of_renewal", "Staff of Renewal", manaCost: 15, "regeneration"),
        StaffDef("staff_of_warding", "Staff of Warding", manaCost: 20, "ward"),
        StaffDef("staff_of_blight", "Staff of Blight", manaCost: 20, "poison"),
        StaffDef("staff_of_mire", "Staff of Mire", manaCost: 25, "mire"),
        // Wand (INT): Resonant x1 plus an element supplied at instantiation; four shapes in logic units.
        WandDef("wand_of_the_blast", "Wand of the Blast", AreaShape.Blast(radius: 48, targetReach: 160)),
        WandDef("wand_of_the_cone", "Wand of the Cone", AreaShape.Cone(angleDegrees: 90, length: 128)),
        WandDef("wand_of_the_beam", "Wand of the Beam", AreaShape.Beam(width: 32, length: 224)),
        WandDef("wand_of_the_nova", "Wand of the Nova", AreaShape.Nova(radius: 96)),
    ]);

    private const int StaffRange = 100;
    private const int StaffCost = 40;
    private const int WandRange = 160;
    private const int WandDamage = 8;
    private const int WandCost = 45;
    private const int WandManaCost = 20;

    private static EnchantmentDef Innate(string id, string name, TargetSide targets, int @lock, int potency, StatusEffectType applies)
        => new(id, name, EffectKind.ApplyStatus, targets, @lock, Trigger: null, potency, ApplyPercent: 100, applies, DamageType: null, Unique: false);

    private static EnchantmentDef Element(string id, string name, DamageType type, int applyPercent)
        => new(id, name, EffectKind.ElementalDamage, TargetSide.Enemy, Lock: 20, Trigger: 5, Potency: 0, applyPercent, Applies: null, type, Unique: false);

    private static WeaponDef MartialDef(string id, string name, WeaponClass cls, VariantRole role, int range, int damage, int cost,
        (ModifierType, int)[] baseline, params (ModifierType, int)[] adds)
        => new(id, name, cls, role, range, damage, cost, ManaCost: 0, Spread(baseline, adds), Enchantments: [], Shape: null, Unique: false, DerivedFrom: null);

    private static WeaponDef StaffDef(string id, string name, int manaCost, string innate)
        => new(id, name, WeaponClass.Staff, Role: null, StaffRange, Damage: 0, StaffCost, manaCost, Spread(CasterBaseline),
            [new EnchantmentRef(innate)], Shape: null, Unique: false, DerivedFrom: null);

    private static WeaponDef WandDef(string id, string name, AreaShape shape)
        => new(id, name, WeaponClass.Wand, Role: null, WandRange, WandDamage, WandCost, WandManaCost, Spread(CasterBaseline),
            Enchantments: [], shape, Unique: false, DerivedFrom: null);

    /// <summary>A forged spread from its parts; a type named twice sums — the same modifier applied again.</summary>
    private static IReadOnlyDictionary<ModifierType, int> Spread(params (ModifierType Type, int Stacks)[][] parts)
    {
        var spread = new Dictionary<ModifierType, int>();
        foreach (var part in parts)
            foreach (var (type, stacks) in part)
                spread[type] = spread.GetValueOrDefault(type) + stacks;
        return spread;
    }
}
