namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Taking a weapon off the floor (§3.5): which slot it lands in, or why it does
/// not, and everything the taking means — the bag, the corpse's retired offer,
/// what the campaign has now met, and the run's spoils (§4.4). A weapon lies on
/// the floor in one of two ways, and each has its own taking: a corpse's drop
/// (<see cref="Take"/>), or a weapon the party put down itself
/// (<see cref="TakeLoose"/>, off <see cref="Floor.LooseItems"/>). Both land in
/// the first empty slot, and only the first can make a spoil.
/// <para>
/// <strong>It lives here rather than in the scene because every word of it is
/// decidable</strong> — the member's slots, first empty one wins, a full bag
/// refuses — and a rule that can be decided without a window should be testable
/// without one. <c>DungeonScene.PickUp</c> keeps what is genuinely the scene's:
/// the marker it removes, the reach readout it refreshes, and the cue it prints.
/// </para>
/// <para>
/// The slots are §4.4's: 24 party-wide, six a member
/// (<see cref="PartyMemberState.InventorySlots"/>), where Phase 3 had three.
/// Loot lands in the same slots the weapons are carried in — there is no haul
/// bag — so the refusal below reads as a carry limit rather than a fault.
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
    /// The writes are one act and happen together: the weapon is in the bag,
    /// <see cref="EnemyState.DropTaken"/> retires the offer so the floor stops
    /// re-deriving it (<see cref="TurnSystem.GroundItemOf"/>), the campaign has
    /// met every entry the weapon carries (§3.3: "added to whenever a weapon
    /// carrying one enters your inventory" — the pickup <em>is</em> that
    /// trigger), and, handed the visit, the weapon is one of the run's spoils
    /// (<see cref="DungeonState.Spoils"/>, §4.4): what a way out carries home and a
    /// wipe forfeits. A refusal performs none of them: a bag that cannot take the
    /// weapon has not met it and has not taken it off the floor.
    /// </para>
    /// </summary>
    /// <param name="visit">
    /// The visit in progress — <paramref name="campaign"/>'s <see cref="CampaignState.Visit"/>
    /// — whose spoils the weapon joins. Whoever plays a floor passes it; omitted,
    /// the pickup counts toward no run's end.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="visit"/> is given and is not the campaign's visit in progress.</exception>
    public static int Take(CampaignState campaign, EnemyState corpse, Drop drop, PartyMemberState member, DungeonState? visit = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(corpse);
        ArgumentNullException.ThrowIfNull(drop);
        ArgumentNullException.ThrowIfNull(member);
        if (visit is not null && !ReferenceEquals(visit, campaign.Visit))
            throw new ArgumentException("A spoil is the visit in progress's: the visit handed in is not the campaign's.", nameof(visit));

        int slot = FirstEmptySlot(member.Inventory);
        if (slot == BagFull) return BagFull;

        member.Inventory[slot] = drop.Weapon;
        corpse.DropTaken = true;
        campaign.See(drop.Weapon);
        visit?.RecordSpoil(drop.Weapon);
        return slot;
    }

    /// <summary>
    /// Take <paramref name="item"/>, a weapon the party put down
    /// (<see cref="Floor.Leave"/>), back off <paramref name="floor"/> into
    /// <paramref name="member"/>'s bag, and answer with the slot it landed in or
    /// <see cref="BagFull"/>. It lands in the first empty slot, like any pickup, so
    /// whoever takes it need not be whoever left it, and the slot need not be the
    /// one it left.
    /// <para>
    /// The writes are one act and happen together: the weapon is in the bag, it is
    /// off the floor's <see cref="Floor.LooseItems"/>, and the campaign has met
    /// every entry it carries (§3.3 — it is entering an inventory again, though a
    /// weapon the party already held has nothing new to meet). A refusal performs
    /// none of them: the item stays where it lies.
    /// </para>
    /// <para>
    /// <strong>Taking a weapon back never makes it a spoil</strong>
    /// (<see cref="DungeonState.Spoils"/>, §4.4). The spoils are what came off a
    /// corpse this visit, recorded when <see cref="Take"/> put each into a bag.
    /// So a spoil put down and taken back is still the same spoil: kept on the way
    /// out and lost to a wipe. And a weapon the party carried in stays gear however
    /// often it is put down and taken back: a wipe never takes it.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="item"/> is not lying on <paramref name="floor"/>: only what is there can be taken.</exception>
    public static int TakeLoose(CampaignState campaign, Floor floor, LooseItem item, PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(member);
        if (!floor.LooseItems.Contains(item))
            throw new ArgumentException($"{item.Weapon.Name} at {item.Tile} is not lying on floor {floor.Index + 1}: only what is there can be taken.", nameof(item));

        int slot = FirstEmptySlot(member.Inventory);
        if (slot == BagFull) return BagFull;

        member.Inventory[slot] = item.Weapon;
        floor.LooseItems.Remove(item);
        campaign.See(item.Weapon);
        return slot;
    }
}
