using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestPotions;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A bag slot can hold a potion (Phase 6b.1; §3.4's "third item category", and
/// §3.5: <c>Inventory</c> "needs to hold something other than a <c>Weapon</c>"). A
/// potion is a value — a type and a quality of one unit at least — and lies in a
/// member's bag, never the hand (PHASE 6b DECISION 1). It fills its slot: a pickup
/// finds no room there, a swap never takes it up, an empty hand passes it over for
/// the first weapon, and every rule that keeps a member armed counts the weapons,
/// never the slots (PHASE 6a DECISION 7). In a dungeon it is poured out, never left
/// on the floor, through the one door a potion leaves a bag by. On the numbers the
/// worked examples were written against (<see cref="Premise"/>), with the game's
/// own party wherever a campaign is played.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class PotionSlotTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static WeaponPlace Slot(string memberId, int slot) => new(memberId, slot);

    private static WeaponPlace StableEnd(CampaignState campaign) => WeaponPlace.InTheStable(campaign.Stable.Count);

    [Fact]
    public void APotionIsAValue_OfAQualityOfOneAtLeast()
    {
        // §3.5: "(PotionType Type, int Quality) and nothing else". Two of the same
        // type and quality are the same potion: nothing finds one by reference, a
        // slot says which is meant. And there are two, which is the whole list.
        Assert.Equal(Health(2), Health(2));
        Assert.Equal(Health(2).GetHashCode(), Health(2).GetHashCode());
        Assert.NotEqual(Health(2), Mana(2));
        Assert.NotEqual(Health(2), Health(3));
        Assert.Equal([PotionType.Health, PotionType.Mana], Enum.GetValues<PotionType>());

        // One unit makes an ordinary potion, and a quality is a count of units:
        // never below 1, and with no ceiling — past a whole pool more quality is
        // overshoot, never refused (§6.6). A type that is neither is no potion.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Potion(PotionType.Health, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Potion(PotionType.Mana, -3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Potion((PotionType)2, 1));
        Assert.Equal(int.MaxValue, Mana(int.MaxValue).Quality);

        // It is a slot's item, as a weapon is, and named as the screen names it.
        IItem item = Health(2);
        Assert.Equal("HEALTH POTION Q2", item.ShownName);
        Assert.Equal("MANA POTION Q1", Mana(1).ShownName);
        Assert.IsAssignableFrom<IItem>(TestWeapons.Get("hatchet"));
    }

    [Fact]
    public void ABagSlotHoldsAPotion_TheHandNever()
    {
        // A potion lies in the bag, slots 1 and up: the hand keeps a weapon. The
        // readers say which a slot holds, and a member's weapons are counted apart
        // from its potions.
        var member = TestPools.Holding("A", TestWeapons.Get("arming_sword"));
        member.Inventory[1] = TestWeapons.Get("hatchet");
        member.Inventory[2] = Health(2);
        member.Inventory[4] = Mana(1);
        Assert.Equal(("hatchet", (Potion?)null), (member.WeaponIn(1)!.Id, member.PotionIn(1)));
        Assert.Equal(((Weapon?)null, Health(2)), (member.WeaponIn(2), member.PotionIn(2)));
        Assert.Equal(2, member.WeaponsCarried);
        Assert.Equal([(2, Health(2)), (4, Mana(1))], member.Potions.ToArray());

        // The one door a potion leaves by refuses the hand, a slot the member does
        // not carry, a weapon's slot and an empty one, and moves nothing doing it.
        Assert.Throws<ArgumentOutOfRangeException>(() => member.TakePotion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => member.TakePotion(member.Inventory.Length));
        Assert.Throws<ArgumentException>(() => member.TakePotion(1));
        Assert.Throws<ArgumentException>(() => member.TakePotion(3));
        Assert.Equal([(2, Health(2)), (4, Mana(1))], member.Potions.ToArray());

        // An empty hand is never armed with a potion: a bag of potions alone leaves
        // it empty, and a weapon behind them is the one taken up.
        var bare = TestPools.Char("B");
        bare.Inventory[1] = Mana(1);
        bare.Inventory[2] = Health(3);
        Assert.Null(bare.ArmFromBag());
        Assert.Null(bare.Inventory[0]);
        var axe = TestWeapons.Get("great_axe");
        bare.Inventory[3] = axe;
        Assert.Same(axe, bare.ArmFromBag());
        Assert.Same(axe, bare.EquippedWeapon);
        Assert.Equal([(1, Mana(1)), (2, Health(3))], bare.Potions.ToArray());

        // And a save that lays a potion in the hand is refused there, by its path.
        var campaign = CampaignState.Start(GameContent.Current);
        Carrying(campaign, "A", (2, Health(2)));
        var tree = JsonNode.Parse(SaveCodec.Write(SaveGame.Capture(campaign)))!.AsObject();
        tree["campaign"]!["party"]![0]!["potions"]![0]!["slot"] = 0;
        var refused = Assert.Throws<SaveFormatException>(() => SaveGame.Restore(SaveCodec.Read(tree.ToJsonString()), GameContent.Current));
        Assert.Equal(("campaign.party[0].potions[0].slot", "0 is the hand: the hand keeps a weapon, and a bag runs from slot 1"), (refused.Where, refused.Problem));
    }

    [Fact]
    public void APotionsSlotIsFullToAPickup()
    {
        // §3.4: a potion occupies a slot. A pickup lands in the first empty one,
        // and a potion's is not empty.
        var hatchet = TestWeapons.Get("hatchet");
        Assert.Equal(2, Pickup.FirstEmptySlot([hatchet, Health(1), null, Mana(1)]));
        Assert.Equal(Pickup.BagFull, Pickup.FirstEmptySlot([hatchet, Health(1), Mana(1)]));

        // A bag full of weapons and potions takes nothing off a corpse: the drop
        // stays where it lies, and the potions where they are.
        var member = TestPools.Holding("B", TestWeapons.Get("tower_guard"));
        for (int slot = 1; slot <= 3; slot++)
            member.Inventory[slot] = TestWeapons.Get("staff_of_renewal");
        member.Inventory[4] = Health(2);
        member.Inventory[5] = Mana(1);
        var campaign = new CampaignState();
        var corpse = new EnemyState();
        var drop = new Drop(TestWeapons.Get("arming_sword"), DefeatCount: 3, WasUniqueRoll: false);
        Assert.Equal(Pickup.BagFull, Pickup.Take(campaign, corpse, drop, member));
        Assert.False(corpse.DropTaken);
        Assert.Equal([(4, Health(2)), (5, Mana(1))], member.Potions.ToArray());

        // Poured out, its slot is room again, and the drop lands there.
        Assert.Equal(Health(2), member.TakePotion(4));
        Assert.Equal(4, Pickup.Take(campaign, corpse, drop, member));
        Assert.Same(drop.Weapon, member.Inventory[4]);
        Assert.True(corpse.DropTaken);
    }

    [Fact]
    public void AnEmptyHandTakesTheFirstWeapon_NeverAPotion()
    {
        // The rest before a dungeon arms an empty hand from the bag (§4.1): the
        // first weapon, passed over any potion ahead of it, which stays where it is.
        var member = TestPools.Char("C");
        member.Inventory[1] = Health(3);
        member.Inventory[2] = Mana(2);
        var axe = TestWeapons.Get("great_axe");
        member.Inventory[4] = axe;
        member.Rest();
        Assert.Same(axe, member.EquippedWeapon);
        Assert.Null(member.Inventory[4]);
        Assert.Equal([(1, Health(3)), (2, Mana(2))], member.Potions.ToArray());

        // Carrying potions and no weapon, a member walks in unarmed, its potions in the bag.
        var none = TestPools.Char("D");
        none.Inventory[1] = Health(1);
        none.Rest();
        Assert.Null(none.Inventory[0]);
        Assert.Equal(Health(1), none.PotionIn(1));

        // And a move between runs that empties a hand arms it the same way
        // (PHASE 6a DECISION 7): A's staff behind a potion is the one taken up.
        var campaign = CampaignState.Start(GameContent.Current);
        var a = campaign.Party[0];
        var staff = a.WeaponIn(1)!;
        campaign.Move(Slot("A", 1), Slot("A", 3));
        Carrying(campaign, "A", (1, Health(2)));
        var stiletto = a.EquippedWeapon!;
        var moved = campaign.Move(Slot("A", 0), StableEnd(campaign));
        Assert.Equal((stiletto, (Weapon?)null, staff), (moved.Moved, moved.Swapped, moved.Armed));
        Assert.Same(staff, a.EquippedWeapon);
        Assert.Equal([(1, Health(2))], a.Potions.ToArray());
    }

    [Fact]
    public void APotionIsNeverSwappedIn()
    {
        // A swap equips a weapon, and the hand keeps one: a potion's slot is never
        // swapped in, in combat or out, and nothing is spent on the refusal.
        var a = TestPools.Holding("A", TestWeapons.Get("longbow"), x: 5 * Tile + 16, y: 5 * Tile + 16);
        a.Inventory[1] = Health(2);
        var enemy = new EnemyState { X = a.X + 60f, Y = a.Y };
        var turns = new TurnSystem(new int[20, 30], new[] { a }, new[] { enemy }, () => 10);
        var spent = new List<string>();
        turns.Events.On<MovementPayload>(GameEvent.MovementSpent, new HandlerPriority(9, 9), "probe", (p, _, _) =>
        {
            spent.Add(p.Source);
            return p;
        });

        foreach (bool combat in new[] { false, true })
        {
            if (combat)
                turns.NotifyEnemyVisible(enemy, true);
            Assert.False(turns.CanSwap(a, 1), combat ? "in combat" : "out of combat");
            Assert.False(turns.TrySwap(a, 1));
            Assert.Equal("longbow", a.EquippedWeapon!.Id);
            Assert.Equal(Health(2), a.PotionIn(1));
            Assert.Equal(Premise.Budget, a.DistLeft);
        }
        Assert.Empty(spent);

        // A weapon in the slot beside it still swaps in, at the swap's price.
        a.Inventory[2] = TestWeapons.Get("weakspot_stiletto");
        Assert.True(turns.TrySwap(a, 2));
        Assert.Equal(("weakspot_stiletto", "longbow"), (a.EquippedWeapon!.Id, a.WeaponIn(2)!.Id));
        Assert.Equal(["swap"], spent);
    }

    [Fact]
    public void APotionIsPouredOut_NeverLeftOnTheFloor()
    {
        // In a dungeon, X then a potion's number pours it out (PHASE 6b DECISION 1):
        // a floor holds weapons alone, so putting a potion down is refused and
        // nothing moves; the potion leaves the bag through its one door instead,
        // gone, and nothing lies on the floor.
        var campaign = CampaignState.Start(GameContent.Current);
        var a = Carrying(campaign, "A", (2, Health(2)));
        var visit = campaign.Enter(campaign.DefaultNext(), CampaignRuns.FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var floor = visit.Current;

        var refused = Assert.Throws<ArgumentException>(() => floor.Leave(a, 2));
        Assert.Contains("poured out, never left", refused.Message, StringComparison.Ordinal);
        Assert.Empty(floor.LooseItems);
        Assert.Equal(Health(2), a.PotionIn(2));

        Assert.Equal(Health(2), a.TakePotion(2));
        Assert.Null(a.Inventory[2]);
        Assert.Empty(a.Potions);
        Assert.Empty(floor.LooseItems);

        // A weapon beside it is still put down as one: the floor keeps it.
        var staff = a.WeaponIn(1)!;
        Assert.Same(staff, floor.Leave(a, 1).Weapon);
        Assert.Same(staff, Assert.Single(floor.LooseItems).Weapon);
    }

    [Fact]
    public void EveryWeaponCountCountsWeapons()
    {
        // Each member keeps a weapon (PHASE 6a DECISION 7), and every rule that
        // keeps one counts the weapons, never the slots: a member carrying one
        // weapon and four potions carries one weapon. A's staff goes to the stable,
        // and A carries the stiletto alone, with a bag of potions.
        var campaign = TestShop.Opened();
        campaign.Move(Slot("A", 1), StableEnd(campaign));
        var a = Carrying(campaign, "A", (1, Health(1)), (2, Health(2)), (3, Mana(1)), (4, Mana(2)));
        var stiletto = a.EquippedWeapon!;
        Assert.Equal(1, a.WeaponsCarried);

        // Put away, it would leave A with none: the hub asks first, and the door refuses.
        Assert.Same(a, campaign.WouldDisarm(Slot("A", 0), StableEnd(campaign)));
        Assert.Same(a, campaign.WouldDisarm(Slot("A", 0), Slot("C", 3)));
        Assert.Throws<InvalidOperationException>(() => campaign.Move(Slot("A", 0), StableEnd(campaign)));
        Assert.Same(stiletto, a.EquippedWeapon);

        // Deposited, the enchanter refuses it at its gate, and the shop's door does too.
        Assert.Equal(Enchanter.WouldCarryNone("A"), Enchanter.Refusal(campaign, stiletto, Service.Refine));
        Assert.Throws<InvalidOperationException>(() => campaign.Enqueue(Slot("A", 0), new Deposit(campaign.DepositsMade, stiletto, Service.Refine, 0, 2)));
        Assert.Empty(campaign.AtTheEnchanter);
        Assert.Same(stiletto, a.EquippedWeapon);

        // A transfer onto it would take it into the shop with its source: refused too.
        var bStaff = campaign.Party[1].WeaponIn(1)!;
        Assert.NotEmpty(bStaff.Enchantments);
        Assert.Equal(Enchanter.WouldCarryNone("A"), Enchanter.TransferRefusal(campaign, stiletto, bStaff, 0));

        // And a weapon in a hand put down in its own bag, behind nothing but potions,
        // would come straight back: the emptied hand takes the first weapon, passing
        // the potions over. Onto a potion's slot, or behind a weapon, it would not.
        Assert.True(campaign.ComesStraightBack(Slot("A", 0), Slot("A", 5)));
        Assert.False(campaign.ComesStraightBack(Slot("A", 0), Slot("A", 1)));   // a potion lies there, and nothing ahead of it: no move puts a weapon on it
        var b = Carrying(campaign, "B", (2, Mana(3)));
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 3)));   // B's staff, in slot 1, is taken up instead
        Assert.False(campaign.ComesStraightBack(Slot("B", 0), Slot("B", 2)));   // B's staff is ahead of it too, and a potion lies there
        Assert.Equal(Mana(3), b.PotionIn(2));
    }

    [Fact]
    public void APotionLeavesABagThroughOneDoor()
    {
        // Read off the IL: a potion leaves a bag by one door (PartyMemberState.TakePotion),
        // and its one caller is the pour-out in a dungeon — X then the potion's number.
        // Phase 6b's later steps add theirs here, each by name: the drink, the run's
        // end's spoilage, the pour-back at the alchemist.
        Assert.Equal(["DungeonScene.LeaveSlot"], CallersOf(typeof(PartyMemberState), nameof(PartyMemberState.TakePotion)));
    }
}
