using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.VariantRole;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled fallback for every content file (§5.4): the instances the
/// game runs on when no file overrides them, and what Phase 5's loader falls
/// back to. Tuning, the relations, the enchantment catalogue's innates and
/// souls, the thirty-two weapons and the eleven uniques — one line per entry,
/// so the validator is the reviewer.
/// </summary>
public static class ContentDefaults
{
    /// <summary>The §5.3 scalars at their compiled values.</summary>
    public static readonly Tuning Tuning = new();

    /// <summary>
    /// The nine unique souls (§1.5, §3.3): the eight the §3.3 table keeps out
    /// of the catalogue and the wand unique's lingering element. Never rolled
    /// by a drop, never copied by the enchanter; each exists only on the
    /// uniques that carry it. Vampiric is not among them: the Efficiency
    /// dagger carries it at tier 3, and a unique is pinned at tier 1.
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

    // ---- Enchantments (§5.7): the eight innates, Vampiric, and the nine souls ----
    // Lock per the §3.3 catalogue. A status applier quotes no flat trigger: its
    // price is the DoT ladder over the levels it applies. Potency is the levels
    // one cast grants at ApplyPercent 100 — provisional, re-priced in Phase 3.
    // An element's ApplyPercent is the burn it feeds; the tuning table overrides it.
    // A soul is a rule or a magnitude. A rule quotes a flat trigger: the ones
    // that fire once — a kill refunded, a life spared, a shove refused, a shot
    // carried on — fire whole or not at all, while Overheal's grant scales to
    // what it could pay, as Vampiric's drink does. A magnitude takes a
    // percentage of its source and is priced on the ladder over what it lands.

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
        // The catalogue entry the Efficiency dagger arrives with at tier 3: fires on damage dealt, heals a flat 1 per tier per instance, 2 a trigger.
        Catalogue("vampiric", "Vampiric", EffectKind.Vampiric, TargetSide.Ally, @lock: 20, trigger: 2, potency: 1),
        // The rest of the §3.3 starting set. Lock and trigger are the table's; the
        // potency column it has no row for is read back out of the same section:
        // Arcane's is Tuning.ArcanePotency (provisional, and laid over this row by
        // the catalogue — keyed by this id, so a playtest turns it in tuning.json
        // without a content edit and a second bonus-damage entry keeps its own),
        // and Shattering's 1 is "crit riders land one level deeper" at tier 1.
        Catalogue("arcane", "Arcane", EffectKind.BonusDamage, TargetSide.Enemy, @lock: 30, trigger: 8, potency: 4),
        Catalogue("shattering", "Shattering", EffectKind.Shattering, TargetSide.Enemy, @lock: 25, trigger: 10, potency: 1),
        // The shielding entry settled.md asks for and never names: it absorbs one
        // point of enchantment damage per level, which is the EffectPerLevel row it
        // is described by, read as a potency. The id is provisional — the docs name
        // none, and content needs something to refer to it by. Lock and trigger are
        // Vampiric's mirrored: the same one-point-per-tier magnitude on every hit,
        // from the other side of it; the §3.3 table prices neither.
        Catalogue("aegis", "Aegis", EffectKind.Shielding, TargetSide.Ally, @lock: 20, trigger: 2, potency: 1),
        // Declared and inert, the Weightless/Momentum shape: "the weapon's class
        // feature triggers once more" has no definition for six of the eight
        // classes, and AttackDeclared, the event it fires on, is raised nowhere.
        // Both go to docs/roadmap/open-questions.md rather than being invented here.
        Catalogue("echoing", "Echoing", EffectKind.Echoing, TargetSide.Ally, @lock: 20, trigger: 15, potency: 0),
        // The souls, in the §3.3 table's order; each bends a rule the rest of the game is built on.
        Rule("siphon", "Siphon", EffectKind.Siphon, TargetSide.Ally, @lock: 20, trigger: 5, potency: 15),           // a kill restores 15: net +10
        Rule("weightless", "Weightless", EffectKind.Weightless, TargetSide.Ally, @lock: 25, trigger: 5, potency: 0),   // attacks cost less movement — declared, inert until the amount is fixed
        Rule("sturdy", "Sturdy", EffectKind.Sturdy, TargetSide.Ally, @lock: 30, trigger: 40, potency: 1),           // survive lethal damage at 1 HP
        Rule("momentum", "Momentum", EffectKind.Momentum, TargetSide.Ally, @lock: 30, trigger: 10, potency: 0),     // a kill refunds part of the swing — declared, inert until the fraction is fixed
        Rule("overheal", "Overheal", EffectKind.Overheal, TargetSide.Ally, @lock: 25, trigger: 8, potency: 0),      // healing above full banks into Ward at OverhealPerWard to 1
        Magnitude("serrated", "Serrated", EffectKind.Serrated, TargetSide.Enemy, @lock: 20, applyPercent: 20, StatusEffectType.Bleeding, damageType: null),   // Bleeding at BleedPercent of the weapon's share; the tuning table overrides the percentage
        Rule("immovable", "Immovable", EffectKind.Immovable, TargetSide.Ally, @lock: 25, trigger: 10, potency: 0),   // a shove on the wielder is negated entirely
        Rule("piercing", "Piercing", EffectKind.Piercing, TargetSide.Enemy, @lock: 20, trigger: 6, potency: 0),     // the shot continues to the next body in line, re-rolled
        Magnitude("burning", "Burning", EffectKind.LingeringElement, TargetSide.Enemy, @lock: 20, applyPercent: 100, StatusEffectType.Searing, DamageType.Flaming),   // Flaming lingers as Searing/Flaming at the element's percentage of its damage, paid once per cast; lock 20 provisional — the §3.3 unique table does not price Burning, Phase 3 does
    ]);

    /// <summary>The enchantment ids the default relations may name: the catalogue's.</summary>
    public static readonly IReadOnlySet<string> EnchantmentIds =
        new HashSet<string>(Enchantments.Enchantments.Select(e => e.Id), StringComparer.Ordinal);

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

    // ---- The starting roster (§5.9): the §2.1 table, verbatim ----

    /// <summary>
    /// The four starting members as <c>party.json</c> would declare them: the
    /// §2.1 table's spreads and starting weapons, with the slot-1 staff the
    /// party has always carried and D's second, debuff staff in the bag.
    /// <para>
    /// Every spread is a permutation of 1–4, so no member is better endowed than
    /// another; the pairs mirror each other, which is what makes each stat
    /// somebody's 4 exactly once and leaves D the party's only CON 1 — the
    /// caster as the single thing that must not be reached. The axe goes to C,
    /// the only STR 4, and D gives up the second spear for a staff, because a
    /// party with no caster never discovers half the game.
    /// </para>
    /// Expect these to move after playing: the permutation rule is the part
    /// worth keeping, and which member gets which permutation is a first guess.
    /// </summary>
    public static readonly PartyData Party = new(
    [
        //             id   name       STR DEX CON INT   equipped                bag
        Member("A", "Dagger", 1, 4, 2, 3, "weakspot_stiletto", "staff_of_renewal"),
        Member("B", "Shield", 3, 1, 4, 2, "tower_guard", "staff_of_renewal"),
        Member("C", "Axe", 4, 2, 3, 1, "great_axe", "staff_of_renewal"),
        Member("D", "Caster", 2, 3, 1, 4, "staff_of_renewal", "staff_of_mire"),
    ]);

    private static PartyMemberDef Member(string id, string name, int str, int dex, int con, int @int,
        string startingWeaponId, params string[] bagWeaponIds)
        => new(id, name, new InnateStats(str, dex, con, @int), startingWeaponId, bagWeaponIds);

    private static EnchantmentDef Innate(string id, string name, TargetSide targets, int @lock, int potency, StatusEffectType applies)
        => new(id, name, EffectKind.ApplyStatus, targets, @lock, Trigger: null, potency, ApplyPercent: 100, applies, DamageType: null, Unique: false);

    private static EnchantmentDef Element(string id, string name, DamageType type, int applyPercent)
        => new(id, name, EffectKind.ElementalDamage, TargetSide.Enemy, Lock: 20, Trigger: 5, Potency: 0, applyPercent, Applies: null, type, Unique: false);

    /// <summary>A catalogue entry with a flat trigger: what a drop may roll.</summary>
    private static EnchantmentDef Catalogue(string id, string name, EffectKind kind, TargetSide targets, int @lock, int trigger, int potency)
        => new(id, name, kind, targets, @lock, trigger, potency, ApplyPercent: 100, Applies: null, DamageType: null, Unique: false);

    /// <summary>A unique soul that is a rule: a flat trigger and no ladder — paid whole or not at all by the souls that fire once, scaled to what it could pay by one that grants levels (Overheal).</summary>
    private static EnchantmentDef Rule(string id, string name, EffectKind kind, TargetSide targets, int @lock, int trigger, int potency)
        => new(id, name, kind, targets, @lock, trigger, potency, ApplyPercent: 100, Applies: null, DamageType: null, Unique: true);

    /// <summary>A unique soul that is a magnitude: a percentage of its source, the status it leaves, and no flat trigger — its price rides the ladder over what it lands.</summary>
    private static EnchantmentDef Magnitude(string id, string name, EffectKind kind, TargetSide targets, int @lock, int applyPercent, StatusEffectType applies, DamageType? damageType)
        => new(id, name, kind, targets, @lock, Trigger: null, Potency: 0, applyPercent, applies, damageType, Unique: true);

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
