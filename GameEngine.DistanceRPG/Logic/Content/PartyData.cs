namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One member of the §5.9 starting party (<see cref="StartingParty"/>): who a
/// starting party member is and what they open the game holding. Reference data,
/// in code by the user's ruling of 2026-09-25, which reverses §5.9's "a starting
/// loadout is content, not a constant": which member gets which permutation is
/// still "a first guess" the doc expects to move after playing (§2.1), and moving
/// it is an edit to <see cref="StartingParty"/> and a rebuild.
/// </summary>
/// <param name="Id">The stable string id a save refers to the member by.</param>
/// <param name="Name">The display name.</param>
/// <param name="Stats">The innate spread (§2.1): a permutation of 1–4, checked at load.</param>
/// <param name="StartingWeaponId">A weapon id, equipped in slot 0. Must resolve in the catalogue.</param>
/// <param name="BagWeaponIds">
/// The remaining slots in order — the fourth field beyond §5.9's three, because
/// the party has always started with a staff in the bag and D carries a second,
/// debuff one (Mire, per settled.md, not Blight). Bounded by the inventory at load.
/// </param>
public sealed record PartyMemberDef(
    string Id, string Name, InnateStats Stats, string StartingWeaponId, IReadOnlyList<string> BagWeaponIds);

/// <summary>The §5.9 starting roster, in its order, which is the order members are spawned in.</summary>
public sealed record PartyData(IReadOnlyList<PartyMemberDef> Members);
