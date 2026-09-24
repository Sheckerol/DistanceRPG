using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.4's carry, and the one way to make room in it: a member puts a bag weapon
/// down where it stands (<see cref="Floor.Leave"/>), and anyone takes it back
/// (<see cref="Pickup.TakeLoose"/>). It stays on that floor for the rest of the
/// visit and goes with the dungeon. This is what makes a full bag's ascent
/// targeted rather than first-come: the weak pickup goes down beside the better
/// one it makes room for.
/// <para>
/// These run on real floors of the shipped seed's tutorial, through the campaign
/// that holds the visit. Every fact a test leans on is pinned before it is used.
/// </para>
/// </summary>
public class LooseItemTests
{
    private const float Tile = GameConstants.Tile;

    /// <summary>The shipped seed: the tutorial is two floors deep on it.</summary>
    private const long GoldenSeed = 2762136374;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    /// <summary>A roll that neither crits nor fumbles.</summary>
    private static int Ten() => 10;

    /// <summary>Four fixture members in roster order, each holding a Weakspot Stiletto, their bags empty.</summary>
    private static PartyMemberState[] Party()
        => Ids.Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i)).ToArray();

    /// <summary>Enter the shipped seed's tutorial with <paramref name="party"/> and open its first floor.</summary>
    private static (CampaignState Campaign, DungeonState Visit) Enter(PartyMemberState[] party)
    {
        var campaign = new CampaignState(party);
        var visit = campaign.Enter("tutorial", GoldenSeed);
        Assert.Equal(2, visit.BossFloor);
        visit.OpenTurns(party, Ten);
        return (campaign, visit);
    }

    /// <summary>Stand the party round <paramref name="stair"/> as a party that walked there would: around living enemies, never on them.</summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) stair)
    {
        var floor = visit.Current;
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, stair.R, stair.C, party.Count, taken);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = Displacer.CentreOf(tiles[i].R, tiles[i].C);
    }

    /// <summary>Gather at the down-stairs, take them, and open the floor below.</summary>
    private static void Down(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        visit.OpenTurns(party, Ten);
    }

    /// <summary>Gather at the up-stairs, take them, and open the floor above.</summary>
    private static void Up(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.UpStairs);
        visit.Ascend(party);
        visit.OpenTurns(party, Ten);
    }

    /// <summary>Every weapon the party carries, hand and bag, as one list.</summary>
    private static Weapon[] Carried(IEnumerable<PartyMemberState> party)
        => party.SelectMany(m => m.Inventory).OfType<Weapon>().ToArray();

    [Fact]
    public void AWeaponLeftHereStaysOnThisFloorForTheVisit()
    {
        // A member puts a bag weapon down where it stands: out of the bag, onto
        // the floor at the member's tile. It is the very weapon, and nobody
        // carries it now. It costs nothing: it is not a swap, and the hand is
        // untouched. It stays on that floor, and only there, however the party
        // comes and goes: down to the bottom and back up, it is where it was put.
        var party = Party();
        var tower = TestWeapons.Get("tower_guard");
        var bow = TestWeapons.Get("longbow");
        party[1].Inventory[2] = tower;   // B's bag: a Tower Guard in its second slot
        party[2].Inventory[1] = bow;     // C's: a Longbow in its first
        var (_, visit) = Enter(party);
        var first = visit.Current;
        var b = party[1];
        var hand = b.Inventory[0];
        float budget = b.DistLeft;
        var tile = Displacer.TileOf(b);
        Assert.Equal(0, first.Map.Grid[tile.R, tile.C]);   // B stands on a floor tile, round the arrival
        Assert.Empty(first.LooseItems);

        var left = first.Leave(b, 2);

        Assert.Same(tower, left.Weapon);
        Assert.Equal(tile, left.Tile);
        Assert.Equal(new[] { left }, first.LooseItems);
        Assert.Null(b.Inventory[2]);
        Assert.DoesNotContain(tower, Carried(party));
        Assert.Same(hand, b.Inventory[0]);   // the hand is untouched
        Assert.Equal(budget, b.DistLeft);    // and it cost nothing

        // Down to the bottom floor, which holds nothing of the first's; C puts
        // its longbow down there.
        Down(visit, party);
        var bottom = visit.Current;
        Assert.NotSame(first, bottom);
        Assert.Empty(bottom.LooseItems);
        var below = bottom.Leave(party[2], 1);
        Assert.Equal(Displacer.TileOf(party[2]), below.Tile);

        // Back up: the first floor holds its Tower Guard where B put it, and the
        // bottom floor its longbow. Each floor keeps its own, and nobody carries either.
        Up(visit, party);
        Assert.Same(first, visit.Current);
        var still = Assert.Single(visit.Current.LooseItems);
        Assert.Same(tower, still.Weapon);
        Assert.Equal(tile, still.Tile);
        Assert.Same(bow, Assert.Single(bottom.LooseItems).Weapon);
        Assert.DoesNotContain(tower, Carried(party));
        Assert.DoesNotContain(bow, Carried(party));

        // And for the visit, not the turn: the turns pass and it lies there still.
        var turns = visit.Turns!;
        Gather(visit, party, visit.Current.UpStairs);
        for (int turn = 0; turn < 3; turn++)
        {
            turns.EndTurn();
            for (float t = 0f; t < 10f; t += 1f / 30f)
                turns.Update(1f / 30f);
            Assert.Equal(TurnPhase.Player, turns.Phase);
        }
        Assert.Equal(new[] { still }, visit.Current.LooseItems);
    }

    [Fact]
    public void PickingItBackUpFillsTheFirstEmptySlot()
    {
        // Taking a loose item back is a pickup like any other. It lands in the
        // first empty slot of whoever takes it, whether or not that member left
        // it, and whatever slot it came from. It comes off the floor, and the
        // campaign has met what it carries. A full bag refuses and takes nothing:
        // the item stays where it lies, and nothing is met.
        var party = Party();
        var (a, b, c) = (party[0], party[1], party[2]);
        string[] bag = ["staff_of_renewal", "tower_guard", "hatchet", "longbow", "bandolier"];
        Assert.Equal(a.Inventory.Length - 1, bag.Length);
        for (int slot = 1; slot < a.Inventory.Length; slot++)
        {
            a.Inventory[slot] = TestWeapons.Get(bag[slot - 1]);
            b.Inventory[slot] = TestWeapons.Get("arming_sword");   // B's bag is full
        }
        var (campaign, visit) = Enter(party);
        var floor = visit.Current;
        Assert.False(campaign.HasSeen("regeneration"));   // a campaign built round its party meets nothing on its own

        // A puts two down: the staff out of slot 1, the longbow out of slot 4.
        var staff = floor.Leave(a, 1);
        var bow = floor.Leave(a, 4);
        Assert.Equal(new[] { staff, bow }, floor.LooseItems);
        Assert.Null(a.Inventory[1]);
        Assert.Null(a.Inventory[4]);
        Assert.Contains(staff.Weapon.Enchantments, e => e.Id == "regeneration");

        // B's bag is full: it refuses, and takes nothing. The staff stays on the
        // floor, B's bag is as it was, and nothing is met.
        var full = b.Inventory.ToArray();
        Assert.Equal(Pickup.BagFull, Pickup.TakeLoose(campaign, floor, staff, b));
        Assert.Equal(full, b.Inventory);
        Assert.Equal(new[] { staff, bow }, floor.LooseItems);
        Assert.False(campaign.HasSeen("regeneration"));

        // A takes the longbow back into slot 1, the first empty one, not slot 4 where it came from.
        Assert.Equal(1, Pickup.TakeLoose(campaign, floor, bow, a));
        Assert.Same(bow.Weapon, a.Inventory[1]);
        Assert.Null(a.Inventory[4]);
        Assert.Equal(new[] { staff }, floor.LooseItems);

        // C, who never held the staff, takes it: into C's first empty slot, and
        // the campaign has met what it carries.
        Assert.Equal(1, Pickup.TakeLoose(campaign, floor, staff, c));
        Assert.Same(staff.Weapon, c.Inventory[1]);
        Assert.Empty(floor.LooseItems);
        Assert.True(campaign.HasSeen("regeneration"));

        // Taken is taken: it is no longer there to take again, and a refusal moves nothing.
        var before = party.Select(m => m.Inventory.ToArray()).ToArray();
        Assert.Throws<ArgumentException>(() => Pickup.TakeLoose(campaign, floor, staff, a));
        Assert.Equal(before, party.Select(m => m.Inventory.ToArray()));
        Assert.Empty(floor.LooseItems);
    }

    [Fact]
    public void SlotZeroCannotBeLeft_NorAnEmptySlot()
    {
        // Only a bag weapon is put down, and only one that is there. Slot 0 is
        // the weapon in hand, and nobody empties their own hand this way. An empty
        // slot has nothing in it to put down, and past the bag there is no slot at
        // all. A weapon is put down where its carrier stands, so a member who does
        // not stand on a floor tile of this floor puts nothing on it. Every
        // refusal moves nothing.
        var party = Party();
        var a = party[0];
        var hatchet = TestWeapons.Get("hatchet");
        a.Inventory[3] = hatchet;
        var (_, visit) = Enter(party);
        var floor = visit.Current;
        var before = a.Inventory.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => floor.Leave(a, 0));                    // the hand
        Assert.Throws<ArgumentException>(() => floor.Leave(a, 2));                              // an empty slot
        Assert.Throws<ArgumentOutOfRangeException>(() => floor.Leave(a, a.Inventory.Length));   // past the bag
        Assert.Throws<ArgumentOutOfRangeException>(() => floor.Leave(a, -1));
        Assert.Equal(before, a.Inventory);
        Assert.Empty(floor.LooseItems);

        // Stood in a wall, off the floor's walkable tiles, A puts nothing down.
        var standing = (a.X, a.Y);
        Assert.Equal(1, floor.Map.Grid[0, 0]);
        (a.X, a.Y) = Displacer.CentreOf(0, 0);
        Assert.Throws<ArgumentException>(() => floor.Leave(a, 3));
        Assert.Equal(before, a.Inventory);
        Assert.Empty(floor.LooseItems);

        // Back where it stood, the hatchet goes down like any bag weapon.
        (a.X, a.Y) = standing;
        Assert.Same(hatchet, floor.Leave(a, 3).Weapon);
        Assert.Null(a.Inventory[3]);
    }

    [Fact]
    public void ItIsGoneWhenTheDungeonIs()
    {
        // §4.1: leaving the dungeon discards all of it, and a weapon put down in it
        // is part of the dungeon now. The visit goes, and the item goes with it: it
        // is in no bag and in neither of the run's lists — the party's own gear
        // included, which is lost once it is left behind. The same seed entered
        // again is a fresh dungeon whose floors hold nothing.
        var party = Party();
        var bow = TestWeapons.Get("longbow");
        party[3].Inventory[1] = bow;   // D's own longbow, carried in
        var (campaign, visit) = Enter(party);
        var left = visit.Current.Leave(party[3], 1);
        Assert.Same(bow, left.Weapon);

        Gather(visit, party, visit.Current.UpStairs);
        var result = campaign.EndRun(RunEnding.Exited);
        Assert.Equal(RunOutcome.Retreat, result.Outcome);
        Assert.Null(campaign.Visit);
        Assert.DoesNotContain(bow, result.SpoilsKept);
        Assert.DoesNotContain(bow, result.SpoilsLost);
        Assert.DoesNotContain(bow, Carried(party));
        Assert.Null(party[3].Inventory[1]);

        var again = campaign.Enter("tutorial", GoldenSeed);
        Assert.Empty(again.Floors.Intersect(visit.Floors));
        Assert.All(again.Floors, floor => Assert.Empty(floor.LooseItems));
        Assert.DoesNotContain(bow, Carried(party));   // nothing brings it back
    }
}
