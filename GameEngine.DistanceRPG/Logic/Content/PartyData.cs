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
/// The remaining slots in order — the fourth field beyond §5.9's three: a second
/// weapon apiece, each member's own since the user's swap of 2026-11-19 (it was a
/// staff in every bag). Bounded by the inventory at load.
/// </param>
/// <param name="WandElement">
/// The element the wands this member starts with carry (the user's swap of
/// 2026-11-19 put the first wand in a starting bag): a wand's element is rolled by
/// the drop that makes it (§3.1), and a starting weapon is no drop, so the roster
/// names it. Null for a member who starts with no wand that takes one; named
/// exactly when one does, checked at load
/// (<see cref="ContentValidator.RulePartyWandElement"/>).
/// </param>
public sealed record PartyMemberDef(
    string Id, string Name, InnateStats Stats, string StartingWeaponId, IReadOnlyList<string> BagWeaponIds, DamageType? WandElement = null);

/// <summary>The §5.9 starting roster, in its order, which is the order members are spawned in.</summary>
public sealed record PartyData(IReadOnlyList<PartyMemberDef> Members);
