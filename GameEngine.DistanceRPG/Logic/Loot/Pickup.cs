namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Taking a weapon off the floor (§3.5): which slot it lands in, or why it does
/// not, and everything the taking means — the bag, the corpse's retired offer,
/// and what the campaign has now met.
/// <para>
/// <strong>It lives here rather than in the scene because every word of it is
/// decidable</strong> — three slots, first empty one wins, a full bag refuses —
/// and a rule that can be decided without a window should be testable without
/// one. <c>DungeonScene.PickUp</c> keeps what is genuinely the scene's: the
/// marker it removes, the reach readout it refreshes, and the cue it prints.
/// </para>
/// <para>
/// The three slots are Phase 3's: §4.4 replaces them with 24 party-wide ones, so
/// the refusal below reads as a carry limit rather than a fault, and the count it
/// names is <see cref="PartyMemberState.InventorySlots"/> rather than a literal.
/// </para>
/// Engine-free and deterministic, like everything else under <c>Logic/</c>.
/// </summary>
public static class Pickup
{
    /// <summary>What <see cref="FirstEmptySlot"/> and <see cref="Take"/> answer when every slot is full: a carry limit, not a fault.</summary>
    public const int BagFull = -1;

    /// <summary>
    /// The slot a picked-up weapon lands in — the first empty one, so a member
    /// with an empty hand equips what they take and a member who is carrying
    /// stows it — or <see cref="BagFull"/> when there is none.
    /// </summary>
    public static int FirstEmptySlot(IReadOnlyList<Weapon?> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        for (int slot = 0; slot < inventory.Count; slot++)
            if (inventory[slot] is null)
                return slot;
        return BagFull;
    }

    /// <summary>
    /// Move <paramref name="drop"/> off <paramref name="corpse"/> and into
    /// <paramref name="member"/>'s bag, and answer with the slot it landed in or
    /// <see cref="BagFull"/>.
    /// <para>
    /// The three writes are one act and happen together: the weapon is in the
    /// bag, <see cref="EnemyState.DropTaken"/> retires the offer so the floor
    /// stops re-deriving it (<see cref="TurnSystem.GroundItemOf"/>), and the
    /// campaign has met every entry the weapon carries (§3.3: "added to whenever
    /// a weapon carrying one enters your inventory" — the pickup <em>is</em> that
    /// trigger). A refusal performs none of them: a bag that cannot take the
    /// weapon has not met it and has not taken it off the floor.
    /// </para>
    /// </summary>
    public static int Take(CampaignState campaign, EnemyState corpse, Drop drop, PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(corpse);
        ArgumentNullException.ThrowIfNull(drop);
        ArgumentNullException.ThrowIfNull(member);

        int slot = FirstEmptySlot(member.Inventory);
        if (slot == BagFull) return BagFull;

        member.Inventory[slot] = drop.Weapon;
        corpse.DropTaken = true;
        campaign.See(drop.Weapon);
        return slot;
    }
}
