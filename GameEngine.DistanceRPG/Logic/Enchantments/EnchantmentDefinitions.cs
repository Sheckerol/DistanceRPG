namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The enchantment catalogue and its unique souls (§5.7, §3.3): reference data,
/// kept in code by the user's ruling of 2026-09-25 ("behaviour, modifiers, and
/// enchantments living in the code due to them being reference data"). An entry
/// is what a behaviour is addressed by: its <see cref="EffectKind"/> is the code
/// it fires, and its stable string id is what the relations
/// (<c>restricted.json</c>), the weapons (<c>weapons.json</c>), the drop pool and
/// a save name it by. A change here is a rebuild, never a data edit.
/// <para>
/// Still checked every time the game starts, as the file it replaced was: on its
/// own first — ids unique, a lock of at least one, no trigger of zero, a trigger
/// or a status and never both, the drop pool resolving
/// (<see cref="ContentValidator.ValidateEnchantments"/>) — and then against the
/// relations, whose <c>neverRolled</c> must name its entries alone and whose
/// <c>excludes</c> must oppose its four elements in two pairs. Those two are
/// <c>restricted.json</c>'s to fix, so a failure there is stamped with that file;
/// a row broken here fails the suite (<c>CodeCatalogueTests</c>) before a
/// playtester can meet it.
/// </para>
/// <para>
/// The twenty-two rows run in the order the catalogue has always had, which
/// <see cref="EnchantmentCatalogue.All"/> and
/// <see cref="EnchantmentCatalogue.Rollable"/> keep; no draw indexes it — a drop
/// indexes the code-fixed <see cref="LootTable.DropPool"/>, so appending a row
/// re-rolls nobody.
/// </para>
/// </summary>
public static class EnchantmentDefinitions
{
    /// <summary>
    /// A number this catalogue does not state, because <c>tuning.json</c> owns it:
    /// the <c>ApplyPercent</c> of the four elements and Serrated, whose dials are
    /// <c>tuning.json</c>'s <c>ApplyPercent</c> table, and Arcane's potency, whose
    /// dial is its <c>ArcanePotency</c>. A percentage and a potency no row can mean
    /// (§5.3: a number a playtest turns lives in <c>tuning.json</c> alone, so it
    /// has no second home here). The catalogue lays the dial over it as it is
    /// built (<see cref="EnchantmentCatalogue"/>), and the load refuses a row left
    /// to a dial the tuning does not name
    /// (<see cref="ContentValidator.RuleTuningOwnsTheNumber"/>).
    /// </summary>
    public const int SetByTuning = -1;

    // ---- Enchantments (§5.7): the eight innates, Vampiric, and the nine souls ----
    // Lock per the §3.3 catalogue. A status applier quotes no flat trigger: its
    // price is the DoT ladder over the levels it applies. Potency is the levels
    // one cast grants at ApplyPercent 100 — provisional, re-priced in Phase 3.
    // An element's ApplyPercent is the burn it feeds, and tuning.json's alone:
    // the rows below leave it to the ApplyPercent table (SetByTuning).
    // A soul is a rule or a magnitude. A rule quotes a flat trigger: the ones
    // that fire once — a kill refunded, a life spared, a shove refused, a shot
    // carried on — fire whole or not at all, while Overheal's grant scales to
    // what it could pay, as Vampiric's drink does. A magnitude takes a
    // percentage of its source and is priced on the ladder over what it lands.

    /// <summary>The §5.7 catalogue: the eight innates, Vampiric, the rest of §3.3's starting set, and the nine unique souls, in the catalogue's order.</summary>
    public static EnchantmentsData Data { get; } = new(
    [
        Innate("regeneration", "Regeneration", TargetSide.Ally, @lock: 15, potency: 1, StatusEffectType.Regeneration),
        Innate("ward", "Ward", TargetSide.Ally, @lock: 20, potency: 5, StatusEffectType.Ward),
        Innate("poison", "Poison", TargetSide.Enemy, @lock: 20, potency: 3, StatusEffectType.Poison),
        Innate("mire", "Mire", TargetSide.Enemy, @lock: 25, potency: 2, StatusEffectType.Mire),
        Element("flaming", "Flaming", DamageType.Flaming, applyPercent: SetByTuning),     // tuning.json ApplyPercent.flaming
        Element("cold", "Cold", DamageType.Cold, applyPercent: SetByTuning),              // tuning.json ApplyPercent.cold
        Element("shocking", "Shocking", DamageType.Shocking, applyPercent: SetByTuning),  // tuning.json ApplyPercent.shocking
        Element("acidic", "Acidic", DamageType.Acidic, applyPercent: SetByTuning),        // tuning.json ApplyPercent.acidic
        // The catalogue entry the Efficiency dagger arrives with at tier 3: fires on damage dealt, heals a flat 1 per tier per instance, 2 a trigger.
        Catalogue("vampiric", "Vampiric", EffectKind.Vampiric, TargetSide.Ally, @lock: 20, trigger: 2, potency: 1),
        // The rest of the §3.3 starting set. Lock and trigger are the table's; the
        // potency column it has no row for is read back out of the same section:
        // Arcane's is tuning.json's ArcanePotency (provisional, and laid over this
        // row by the catalogue — keyed by this id, so a playtest turns it in
        // tuning.json without a rebuild and a second bonus-damage entry keeps its
        // own), and Shattering's 1 is "crit riders land one level deeper" at tier 1.
        Catalogue("arcane", "Arcane", EffectKind.BonusDamage, TargetSide.Enemy, @lock: 30, trigger: 8, potency: SetByTuning),   // tuning.json ArcanePotency
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
        Magnitude("serrated", "Serrated", EffectKind.Serrated, TargetSide.Enemy, @lock: 20, applyPercent: SetByTuning, StatusEffectType.Bleeding, damageType: null),   // Bleeding at BleedPercent of the weapon's share: tuning.json ApplyPercent.serrated
        Rule("immovable", "Immovable", EffectKind.Immovable, TargetSide.Ally, @lock: 25, trigger: 10, potency: 0),   // a shove on the wielder is negated entirely
        Rule("piercing", "Piercing", EffectKind.Piercing, TargetSide.Enemy, @lock: 20, trigger: 6, potency: 0),     // the shot continues to the next body in line, re-rolled
        Magnitude("burning", "Burning", EffectKind.LingeringElement, TargetSide.Enemy, @lock: 20, applyPercent: 100, StatusEffectType.Searing, DamageType.Flaming),   // Flaming lingers as Searing/Flaming at the element's percentage of its damage, paid once per cast; lock 20 provisional — the §3.3 unique table does not price Burning, Phase 3 does
    ]);

    /// <summary>A staff's innate: the status it casts, landed on whoever the staff targets, priced on the ladder over the levels it applies.</summary>
    private static EnchantmentDef Innate(string id, string name, TargetSide targets, int @lock, int potency, StatusEffectType applies)
        => new(id, name, EffectKind.ApplyStatus, targets, @lock, Trigger: null, potency, ApplyPercent: 100, applies, DamageType: null, Unique: false);

    /// <summary>A wand's innate: the element its hit is typed with, feeding the burn at <paramref name="applyPercent"/> — the tuning's, for every element the game ships.</summary>
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
}
