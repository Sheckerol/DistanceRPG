namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.7 <c>enchantments.json</c> shape: the catalogue's rows in file order.
/// Phase 1 ships a compiled instance (<see cref="ContentDefaults.Enchantments"/>);
/// Phase 5 adds only deserialisation.
/// </summary>
public sealed record EnchantmentsData(IReadOnlyList<EnchantmentDef> Enchantments);
