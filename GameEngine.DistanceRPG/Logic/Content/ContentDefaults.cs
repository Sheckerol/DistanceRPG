using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.VariantRole;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled fallback for every data file (§5.4): the instances the game
/// runs on when no file overrides them, and what Phase 5's loader falls back
/// to. Tuning, the relations, the thirty-two weapons and the eleven uniques,
/// and the five dungeons — one line per entry, so the validator is the
/// reviewer. The enchantment catalogue and the starting party are not here and
/// have no file: they are reference data in code by the user's ruling of
/// 2026-09-25 (<see cref="EnchantmentDefinitions"/>, <see cref="StartingParty"/>).
/// </summary>
public static class ContentDefaults
{
    /// <summary>The §5.3 scalars at their compiled values.</summary>
    public static readonly Tuning Tuning = new();

    /// <summary>
    /// The nine unique souls (§1.5, §3.3): the eight the §3.3 table keeps out
    /// of the catalogue and the wand unique's lingering element — the rows
    /// <see cref="EnchantmentDefinitions"/> marks unique. Never rolled by a
    /// drop, never copied by the enchanter; each exists only on the uniques
    /// that carry it. Vampiric is not among them: the Efficiency dagger carries
    /// it at tier 3, and a unique is pinned at tier 1.
    /// </summary>
    private static readonly string[] UniqueSoulIds =
        ["siphon", "weightless", "sturdy", "momentum", "overheal", "serrated", "immovable", "piercing", "burning"];

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
            ["burning"] = ["flaming"],                 // a lingering element needs its element on the same weapon, ahead of it: Searing is nothing without Flaming to say how many levels
        },
        Kind: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(Brace)] = RestrictedData.MeleeKind,         // a threat zone is a weapon's physical reach
            [nameof(Opportunist)] = RestrictedData.MeleeKind,
            [nameof(Overwatch)] = RestrictedData.RangedKind,    // holding a shot is what a nocked arrow does
            // Resonant is deliberately absent: it discounts enchantment triggers too.
        },
        ForgedOnly: [nameof(Charges)],   // a cap, not a bonus: granted from zero it would make a bow worse
        NeverRolled: UniqueSoulIds);     // the unique enchantments: cross-checked with the entries' flag on load

    // ---- Weapons (§5.6): eight classes, four variants each, and the uniques ----
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

    /// <summary>The thirty-two, in class order.</summary>
    private static readonly WeaponDef[] Variants =
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
    ];

    /// <summary>
    /// The unique table (§1.5): each a variant with one modifier it already
    /// carries raised to x3 — or, for a Light-forged one, the signature at x2
    /// paid for in souls — and a soul no other weapon can have; a caster
    /// unique raises nothing and puts the whole artifact in its enchantments.
    /// Everything else is the variant's, copied from it, so the statline can
    /// never drift. The three the docs leave unnamed carry provisional names.
    /// </summary>
    private static readonly WeaponDef[] Uniques =
    [
        Unique("the_bulwark", "The Bulwark", "tower_guard", Block, 3, Ref("sturdy")),                               // absorbs 9, shoves what it stops, and refuses to let you die
        Unique("feathered_death", "Feathered Death", "bandolier", Charges, 3, Ref("weightless")),                    // four throws that barely cost anything to make
        Unique("shieldbreaker", "Shieldbreaker", "reaver", Splitting, 3, Ref("momentum")),                          // ignores 9 Block across the swing
        Unique("widowmaker", "Widowmaker", "assassins_fang", CritWindow, 3, Ref("siphon")),                         // finds the gap on 17+, and every kill funds the enchantments doing it
        Unique("hoplites_wall", "Hoplite's Wall", "phalanx_spear", Brace, 3, Ref("immovable")),                     // three retaliations at full reach, from a line that cannot be moved
        Unique("stormcrow", "Stormcrow", "longbow", Longshot, 3, Ref("piercing")),                                  // +3 a tile, and the shot does not stop at the first body
        Unique("flensing_knife_unique", "Nameless Knife", "flensing_knife", CritWindow, 2,                          // stabs itself a shield: three souls in a line — the Light shape, x2 buying three
            Ref("serrated"), Ref("vampiric", tier: 3), Ref("overheal")),
        CasterUnique("rotwood", "Rotwood", "staff_of_blight", Ref("poison", tier: 3)),                              // a rot that starts where an ordinary staff's ends
        CasterUnique("the_long_candle", "The Long Candle", "wand_of_the_beam", Ref("shocking"), Ref("flaming")),    // a beam of plasma: two elements, each answering the chart
        CasterUnique("wand_of_the_nova_unique", "Nameless Nova", "wand_of_the_nova", Ref("flaming"), Ref("burning")),   // a circle that keeps burning after it lands
        CasterUnique("staff_of_renewal_unique", "Nameless Renewal", "staff_of_renewal", Ref("regeneration"), Ref("overheal")),   // healing that stops being wasted on the healthy
    ];

    /// <summary>The §5.6 catalogue as <c>weapons.json</c> would declare it: the thirty-two in class order, then the uniques.</summary>
    public static readonly WeaponsData Weapons = new([.. Variants, .. Uniques]);

    private const int StaffRange = 100;
    private const int StaffCost = 40;
    private const int WandRange = 160;
    private const int WandDamage = 8;
    private const int WandCost = 45;
    private const int WandManaCost = 20;

    // ---- Dungeons (§5.8): the tutorial, and four provisional dungeons ----
    // Each theme is a class signature with a unique to hunt: the Purity variant
    // of the theme's own class, grafted by the boss, is that class's unique by
    // arithmetic (§4.3). Only the tutorial's golem is the docs' — 80 HP, a
    // movement budget of 40, Block x3 innate (S:302). Everything else here —
    // the four dungeons, their names, their bosses' names and statlines — is
    // PROVISIONAL: picked so the hub has somewhere to go, and expected to move
    // after play. settled.md's first playable hub has six; the two missing need
    // content that does not exist yet (a Great Axe unique for Cleave, or a
    // repeated theme), and Phase 6b completes the roster. No dungeon here is
    // attuned: attunement is its own field, and none is set yet.

    /// <summary>
    /// The §5.8 dungeons as <c>dungeons.json</c> would declare them, in the
    /// order the hub lists them. A floor range left null reads the tuning's
    /// 5–10; the tutorial pins both ends to 2 and is the one dungeon that
    /// retires on its clear. What each can drop is computed from its theme
    /// (<see cref="DungeonCatalogue.ThemeClasses"/>) and written nowhere.
    /// </summary>
    public static readonly DungeonsData Dungeons = new(
    [
        //       id               name                  theme        boss floor   boss
        Dungeon("tutorial", "The Tutorial", Block, bossFloor: 2,
            Boss("stone_golem", "Stone Golem", maxHp: 80, movementBudget: 40, (Block, 3)),   // the docs' golem: 80 HP, slow, Block x3 whatever it holds
            retiresOnClear: true),
        Dungeon("spear_hall", "The Spear Hall", Brace, bossFloor: null,
            Boss("spear_warden", "Spear Warden", maxHp: 200, movementBudget: 70, (Brace, 3))),                 // provisional: drops daggers, swords and spears; hunts Hoplite's Wall
        Dungeon("long_gallery", "The Long Gallery", Longshot, bossFloor: null,
            Boss("gallery_marksman", "Gallery Marksman", maxHp: 200, movementBudget: 70, (Longshot, 3))),      // provisional: drops all eight; hunts Stormcrow, a bow no enemy wields
        Dungeon("knife_warrens", "The Knife Warrens", CritWindow, bossFloor: null,
            Boss("knife_mother", "Knife Mother", maxHp: 200, movementBudget: 70, (CritWindow, 3))),            // provisional: drops all eight; hunts Widowmaker
        Dungeon("quiver_pits", "The Quiver Pits", Charges, bossFloor: null,
            Boss("quiver_keeper", "Quiver Keeper", maxHp: 200, movementBudget: 70, (Charges, 3))),             // provisional: Charges is forged-only, so it drops throwing weapons alone; hunts Feathered Death
    ]);

    /// <summary>A dungeon row, unattuned: <paramref name="bossFloor"/> pins both ends of the boss's floor range, or null leaves both to the tuning.</summary>
    private static DungeonDef Dungeon(string id, string name, ModifierType theme, int? bossFloor, BossDef boss, bool retiresOnClear = false)
        => new(id, name, theme, Attunement: null, BossFloorMin: bossFloor, BossFloorMax: bossFloor, boss, retiresOnClear);

    /// <summary>A boss's statline and the modifiers it carries on itself rather than on its weapon.</summary>
    private static BossDef Boss(string id, string name, int maxHp, int movementBudget, params (ModifierType Type, int Stacks)[] innate)
        => new(id, name, maxHp, movementBudget, Spread(innate));

    /// <summary>
    /// The rows of the data files the docs did not fix — the ids the comments
    /// above already mark provisional, first guesses expected to move after
    /// play: the three uniques the docs leave unnamed, and the four dungeons
    /// beside the tutorial with their bosses, names and statlines alike. The
    /// generator writes <c>/* provisional */</c> after each one's id in its file
    /// (<see cref="JsonText.IdNote"/>), so a designer tuning the copy beside the
    /// game can tell a guessed row from one the docs fixed — the way
    /// <c>tuning.json</c>'s <c>Provisional:</c> descriptions mark a number. The
    /// enchantment catalogue's own guesses — the shielding entry's id
    /// (<c>aegis</c>) and Burning's lock — are code, and their comments in
    /// <see cref="EnchantmentDefinitions"/> say so.
    /// </summary>
    internal static readonly IReadOnlySet<string> ProvisionalIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "flensing_knife_unique", "wand_of_the_nova_unique", "staff_of_renewal_unique",   // weapons.json: the Nameless three
        "spear_hall", "spear_warden", "long_gallery", "gallery_marksman",        // dungeons.json: the four and their bosses
        "knife_warrens", "knife_mother", "quiver_pits", "quiver_keeper",
    };

    private static WeaponDef MartialDef(string id, string name, WeaponClass cls, VariantRole role, int range, int damage, int cost,
        (ModifierType, int)[] baseline, params (ModifierType, int)[] adds)
        => new(id, name, cls, role, range, damage, cost, ManaCost: 0, Spread(baseline, adds), Enchantments: [], Shape: null, Unique: false, DerivedFrom: null);

    private static WeaponDef StaffDef(string id, string name, int manaCost, string innate)
        => new(id, name, WeaponClass.Staff, Role: null, StaffRange, Damage: 0, StaffCost, manaCost, Spread(CasterBaseline),
            [new EnchantmentRef(innate)], Shape: null, Unique: false, DerivedFrom: null);

    private static WeaponDef WandDef(string id, string name, AreaShape shape)
        => new(id, name, WeaponClass.Wand, Role: null, WandRange, WandDamage, WandCost, WandManaCost, Spread(CasterBaseline),
            Enchantments: [], shape, Unique: false, DerivedFrom: null);

    /// <summary>A martial unique: its variant with <paramref name="raised"/> taken to <paramref name="to"/> and nothing else changed, carrying <paramref name="souls"/>.</summary>
    private static WeaponDef Unique(string id, string name, string derivedFrom, ModifierType raised, int to, params EnchantmentRef[] souls)
    {
        var variant = VariantById(derivedFrom);
        var spread = new Dictionary<ModifierType, int>(variant.Forged) { [raised] = to };
        return variant with { Id = id, Name = name, Role = null, Forged = spread, Enchantments = souls, Unique = true, DerivedFrom = derivedFrom };
    }

    /// <summary>A caster unique: its variant, spread and statline untouched, with the enchantment list that is the whole artifact.</summary>
    private static WeaponDef CasterUnique(string id, string name, string derivedFrom, params EnchantmentRef[] enchantments)
        => VariantById(derivedFrom) with { Id = id, Name = name, Enchantments = enchantments, Unique = true, DerivedFrom = derivedFrom };

    private static WeaponDef VariantById(string id) => Variants.Single(v => v.Id == id);

    private static EnchantmentRef Ref(string id, int tier = 1) => new(id, tier);

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
