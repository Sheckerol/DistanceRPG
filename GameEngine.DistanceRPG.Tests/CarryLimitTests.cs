using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// 4.4's carry: six slots a member, twenty-four party-wide, replacing the three
/// that were a testing remnant. Slot 0 is still the weapon in hand, every bag
/// slot swaps in, and loot lands in the same slots the weapons are carried in
/// -- there is no haul bag, so every drop carried out of the dungeon is a
/// weapon that could have been swung. The carry is a tuning key: these stand on
/// the 24 they were written against (<see cref="Premise"/>), made current for each
/// test, and two of them swap it, so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class CarryLimitTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>A fixture member at the centre of tile (5, 5), holding <paramref name="weaponId"/>.</summary>
    private static PartyMemberState Holding(string id, string weaponId)
        => TestPools.Holding(id, TestWeapons.Get(weaponId), x: 5 * Tile + 16, y: 5 * Tile + 16);

    /// <summary>A turn system for <paramref name="member"/> alone on an open floor, its one dummy far off and unseen: out of combat, so a swap is free.</summary>
    private static TurnSystem Quiet(PartyMemberState member)
        => new(new int[20, 30], new[] { member }, new[] { new EnemyState { X = 25 * Tile, Y = 15 * Tile } }, () => 10);

    [Fact]
    public void AMemberCarriesSix_ThePartyTwentyFour()
    {
        // PHASE4-116/-140: six a member, twenty-four for the party, and the one
        // number the tuning holds is the party's.
        Assert.Equal(24, GameContent.Current.Tuning.PartyCarrySlots);
        Assert.Equal(6, PartyMemberState.InventorySlots);

        var party = new[] { "A", "B", "C", "D" }.Select(id => TestPools.Char(id)).ToArray();
        Assert.Equal(GameConstants.PartySize, party.Length);
        Assert.All(party, member => Assert.Equal(6, member.Inventory.Length));
        Assert.Equal(GameContent.Current.Tuning.PartyCarrySlots, party.Sum(member => member.Inventory.Length));

        // The count is content: a member is built with the carry current when it
        // is made, and keeps it.
        PartyMemberState deep;
        using (TestContent.UsePremise(tuning: t => t with { PartyCarrySlots = 32 }))
        {
            Assert.Equal(8, PartyMemberState.InventorySlots);
            deep = TestPools.Char("A");
            Assert.Equal(8, deep.Inventory.Length);
        }
        Assert.Equal(8, deep.Inventory.Length);
        Assert.Equal(6, TestPools.Char("A").Inventory.Length);

        // The roster is measured against the same six: the starting weapon and a
        // bag of five fit, and one more does not.
        var five = new PartyData(ShippedContent.Party.Members.Select((m, i) => i == 3
            ? m with { BagWeaponIds = ["staff_of_mire", "staff_of_blight", "staff_of_warding", "staff_of_renewal", "arming_sword"] }
            : m).ToList());
        using (TestContent.UsePremise(party: five))
            Assert.Equal(5, GameContent.Current.Party["D"].BagWeaponIds.Count);

        var six = new PartyData(ShippedContent.Party.Members.Select((m, i) => i == 3
            ? m with { BagWeaponIds = [.. five.Members[3].BagWeaponIds, "hatchet"] }
            : m).ToList());
        var refused = Assert.Throws<ContentException>(() => TestContent.UsePremise(party: six));
        Assert.Equal(("D", ContentValidator.RulePartyBagFits), (refused.EntryId, refused.Rule));
    }

    [Fact]
    public void SlotZeroIsStillTheWeaponInHand()
    {
        // PHASE4-142: the bag grew; the hand did not move.
        var member = Holding("A", "arming_sword");
        Assert.Same(member.Inventory[0], member.EquippedWeapon);
        Assert.Equal("arming_sword", member.EquippedWeapon!.Id);

        // Whatever comes out of the last slot goes into slot 0 and is what the member swings.
        member.Inventory[5] = TestWeapons.Get("longbow");
        var turns = Quiet(member);
        Assert.True(turns.TrySwap(member, 5));
        Assert.Same(member.Inventory[0], member.EquippedWeapon);
        Assert.Equal("longbow", member.EquippedWeapon!.Id);
        Assert.Equal("arming_sword", member.Inventory[5]!.Id);

        // Slot 0 is the hand itself, never a slot to swap in from.
        Assert.False(turns.CanSwap(member, 0));
    }

    [Fact]
    public void EveryBagSlotSwapsIn()
    {
        // PHASE4-143: the swap generalises to every slot a member carries, 1 through 5.
        var member = Holding("B", "arming_sword");
        string[] bag = ["weakspot_stiletto", "phalanx_spear", "great_axe", "longbow", "bandolier"];
        Assert.Equal(member.Inventory.Length - 1, bag.Length);
        for (int slot = 1; slot < member.Inventory.Length; slot++)
            member.Inventory[slot] = TestWeapons.Get(bag[slot - 1]);

        var turns = Quiet(member);
        for (int slot = 1; slot < member.Inventory.Length; slot++)
        {
            var wanted = member.Inventory[slot];
            var held = member.EquippedWeapon;
            Assert.True(turns.CanSwap(member, slot), $"slot {slot}");
            Assert.True(turns.TrySwap(member, slot), $"slot {slot}");
            Assert.Same(wanted, member.EquippedWeapon);   // the bag slot's weapon is in hand
            Assert.Same(held, member.Inventory[slot]);   // and what was in hand took its place
        }

        // Out of combat every one of them was free; past the last slot there is nothing to swap.
        Assert.Equal(Premise.Budget, member.DistLeft);
        Assert.False(turns.CanSwap(member, member.Inventory.Length));
    }

    [Fact]
    public void LootAndWeaponsShareTheSlots()
    {
        // PHASE4-117: a pickup lands in the first empty slot of the same six the
        // weapons are carried in -- here slot 5, behind the weapon in hand and four
        // carried ones.
        var member = Holding("B", "tower_guard");
        for (int slot = 1; slot <= 4; slot++)
            member.Inventory[slot] = TestWeapons.Get("staff_of_renewal");

        var campaign = new CampaignState();
        var corpse = new EnemyState();
        var drop = new Drop(TestWeapons.Get("arming_sword"), DefeatCount: 3, WasUniqueRoll: false);
        Assert.Equal(5, Pickup.Take(campaign, corpse, drop, member));
        Assert.Same(drop.Weapon, member.Inventory[5]);
        Assert.True(corpse.DropTaken);

        // That was the last slot: the next drop has nowhere to go, and stays where it lies.
        var next = new EnemyState();
        Assert.Equal(Pickup.BagFull, Pickup.Take(campaign, next, new Drop(TestWeapons.Get("hatchet"), DefeatCount: 0, WasUniqueRoll: false), member));
        Assert.False(next.DropTaken);

        // And there is no other container: the only things on a member that hold a
        // weapon are the six slots and the hand, which is slot 0 of them.
        var holders = typeof(PartyMemberState)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => typeof(Weapon).IsAssignableFrom(p.PropertyType)
                || typeof(IEnumerable<Weapon?>).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { nameof(PartyMemberState.EquippedWeapon), nameof(PartyMemberState.Inventory) }, holders);
        Assert.Same(member.Inventory[0], member.EquippedWeapon);
    }

    [Fact]
    public void ACarryThatDoesNotDivideAmongTheMembersIsRefused()
    {
        // Twenty-five slots among four members leaves one nobody carries, and a
        // carry under four leaves somebody without a hand.
        foreach (int carry in new[] { 25, 26, 27, 3, 0, -4 })
        {
            var ex = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(ShippedContent.Tuning with { PartyCarrySlots = carry }));
            Assert.Equal((nameof(Tuning.PartyCarrySlots), ContentValidator.RuleCarryFillsTheParty), (ex.EntryId, ex.Rule));
        }

        // Refused at load, before anything is built on it, leaving the running content alone.
        var before = GameContent.Current;
        var loaded = Assert.Throws<ContentException>(() => TestContent.Use(tuning: ShippedContent.Tuning with { PartyCarrySlots = 30 }));
        Assert.Equal(ContentValidator.RuleCarryFillsTheParty, loaded.Rule);
        Assert.Same(before, GameContent.Current);

        // Every multiple of the party from a hand each to nine slots each divides evenly and loads.
        foreach (int carry in new[] { 4, 24, 28, 36 })
            ContentValidator.ValidateTuning(ShippedContent.Tuning with { PartyCarrySlots = carry });
    }

    [Fact]
    public void ACarryDeeperThanTheNumberKeysIsRefused()
    {
        // With the bag open a slot is equipped by the number key printed beside it
        // -- the hand is 1, the bag runs from 2 -- and the number row stops at 9
        // (PHASE4-143). Forty slots would give each member ten, and a pickup would
        // land in a tenth slot no key can swap into the hand, so the loader refuses
        // any carry deeper than nine a member: 36 party-wide loads, 40 does not.
        Assert.Equal(9, GameConstants.MaxInventorySlots);
        int deepest = GameConstants.PartySize * GameConstants.MaxInventorySlots;
        Assert.Equal(36, deepest);
        ContentValidator.ValidateTuning(ShippedContent.Tuning with { PartyCarrySlots = deepest });
        foreach (int carry in new[] { deepest + GameConstants.PartySize, 400 })
        {
            var ex = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(ShippedContent.Tuning with { PartyCarrySlots = carry }));
            Assert.Equal((nameof(Tuning.PartyCarrySlots), ContentValidator.RuleEverySlotHasAKey), (ex.EntryId, ex.Rule));
        }

        // Refused at load, before a member is built on it, leaving the running content alone.
        var before = GameContent.Current;
        var loaded = Assert.Throws<ContentException>(() => TestContent.Use(tuning: ShippedContent.Tuning with { PartyCarrySlots = deepest + GameConstants.PartySize }));
        Assert.Equal(ContentValidator.RuleEverySlotHasAKey, loaded.Rule);
        Assert.Same(before, GameContent.Current);

        // The other end of the same rule: the scene binds a number key for every
        // slot the loader admits. At the deepest carry a member holds nine, the
        // bag reads 2-9, and each of its eight slots has its key.
        Assert.InRange(GameConstants.MaxInventorySlots, 1, DungeonScene.HighestSlotKey);
        using (TestContent.Use(tuning: ShippedContent.Tuning with { PartyCarrySlots = deepest }))
        {
            var member = TestPools.Char("A");
            Assert.Equal(GameConstants.MaxInventorySlots, member.Inventory.Length);
            Assert.Equal("PRESS 2-9 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(member.Inventory.Length));
        }
    }
}
