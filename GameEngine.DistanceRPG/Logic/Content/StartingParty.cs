namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The four who start a campaign (§2.1, §5.9): reference data, kept in code by
/// the user's ruling of 2026-09-25 — the roster and its loadouts, which reverses
/// §5.9's "decided in this file, not hardcoded". A change here is a rebuild, and
/// it plays from the next campaign started: a saved campaign keeps the party it
/// started with, each member's spread and bag held in the save.
/// <para>
/// Still checked every time the game starts, last, against the weapons it names
/// (<see cref="ContentValidator.ValidateParty"/>): four members, each spread a
/// permutation of 1–4, every weapon there and within the fitted carry, a caster
/// in hand. The one failure a data file can cause is a <c>weapons.json</c> that
/// renames a weapon only the roster names — A's <c>weakspot_stiletto</c>, C's
/// <c>great_axe</c> — so a failure here is stamped with that file, naming the
/// member, the rule and the weapon.
/// </para>
/// <para>
/// The §2.1 table's spreads and starting weapons, with the slot-1 staff the
/// party has always carried and D's second, debuff staff in the bag. Every
/// spread is a permutation of 1–4, so no member is better endowed than another;
/// the pairs mirror each other, which is what makes each stat somebody's 4
/// exactly once and leaves D the party's only CON 1 — the caster as the single
/// thing that must not be reached. The axe goes to C, the only STR 4, and D
/// gives up the second spear for a staff, because a party with no caster never
/// discovers half the game.
/// </para>
/// Expect these to move after playing: the permutation rule is the part worth
/// keeping, and which member gets which permutation is a first guess.
/// </summary>
public static class StartingParty
{
    /// <summary>The starting roster, in the order its members stand, spawn and are numbered (1–4).</summary>
    public static PartyData Data { get; } = new(
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
}
