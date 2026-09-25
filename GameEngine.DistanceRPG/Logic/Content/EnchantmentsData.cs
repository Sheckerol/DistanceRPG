namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.7 enchantment catalogue: its rows, in order, each a row of the game's
/// catalogue. The game's one catalogue is code — reference data, by the user's
/// ruling of 2026-09-25 — and is <see cref="EnchantmentDefinitions.Data"/>,
/// handed to the load as data and checked there like any content; a test hands
/// the load another to simulate a code edit.
/// </summary>
public sealed record EnchantmentsData(IReadOnlyList<EnchantmentDef> Enchantments);
