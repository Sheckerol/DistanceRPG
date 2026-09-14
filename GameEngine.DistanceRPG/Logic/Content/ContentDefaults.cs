using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled fallback for every content file (§5.4): the instances the
/// game runs on when no file overrides them, and what Phase 5's loader falls
/// back to. Ships tuning and the relations; the catalogues follow.
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

    /// <summary>
    /// The enchantment ids the default relations name. Until the enchantment
    /// catalogue exists this is the four elemental innates the opposed-type
    /// groups refer to; the catalogue replaces it as the source of known ids.
    /// </summary>
    public static readonly IReadOnlySet<string> EnchantmentIds =
        new HashSet<string>(StringComparer.Ordinal) { "flaming", "cold", "shocking", "acidic" };
}
