namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.5 <c>restricted.json</c> shape: what may coexist and what may be
/// rolled, for modifiers and enchantments alike, keyed by stable string id.
/// Modifier ids are the <see cref="ModifierType"/> names; enchantment ids are the
/// catalogue's. Phase 1 ships a compiled instance (<see cref="ContentDefaults.Restricted"/>);
/// Phase 5 adds only deserialisation.
/// </summary>
/// <param name="Excludes">
/// Exclusion groups: every member excludes every other. Expanded to directed
/// pairs by <see cref="ModifierRules.Symmetric"/> at load, so a group is one
/// line and cannot be half-declared.
/// </param>
/// <param name="Requires">Dependencies, one way: id to the ids it cannot exist without.</param>
/// <param name="Kind">
/// Weapon-type restrictions: id to <see cref="MeleeKind"/>, <see cref="RangedKind"/>
/// or <see cref="CasterKind"/>. Absent means any kind.
/// </param>
/// <param name="ForgedOnly">Deepenable if already present, never added from zero (a safety rule).</param>
/// <param name="NeverRolled">Never granted by any roll at all — the unique enchantments (an economy rule). Read by later phases' rolls.</param>
public sealed record RestrictedData(
    IReadOnlyList<string[]> Excludes,
    IReadOnlyDictionary<string, string[]> Requires,
    IReadOnlyDictionary<string, string> Kind,
    string[] ForgedOnly,
    string[] NeverRolled)
{
    public const string MeleeKind = "melee";
    public const string RangedKind = "ranged";
    public const string CasterKind = "caster";

    private static readonly IReadOnlyDictionary<string, WeaponKind> Kinds =
        new Dictionary<string, WeaponKind>(StringComparer.Ordinal)
        {
            [MeleeKind] = WeaponKind.Melee,
            [RangedKind] = WeaponKind.Ranged,
            [CasterKind] = WeaponKind.Caster,
        };

    /// <summary>Resolve a <see cref="Kind"/> value to a <see cref="WeaponKind"/>; false for anything else.</summary>
    public static bool TryParseKind(string kind, out WeaponKind parsed) => Kinds.TryGetValue(kind, out parsed);
}
