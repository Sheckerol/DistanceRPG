using System.Collections;
using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// What the campaign has met (§3.3): the enchantment ids a weapon has brought
/// into the party's hands, which is what §6.2's enchanter offers from
/// (<see cref="Enchanter.Offers"/>) and what §5.1 saves. Small, and the one collection that only ever grows.
/// </summary>
public class CampaignStateTests
{
    /// <summary>A weapon carrying exactly the named entries, for the cases the catalogue has no single item for.</summary>
    private static Weapon Carrying(params string[] enchantmentIds)
        => TestWeapons.Enchanted("Met", range: 40, damage: 10, cost: 30, enchantmentIds);

    /// <summary>Stand the party round <paramref name="tile"/> — around living enemies, never on them — as a party that walked there would.</summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) tile)
    {
        var floor = visit.Current;
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, tile.R, tile.C, party.Count, taken);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = Displacer.CentreOf(tiles[i].R, tiles[i].C);
    }

    /// <summary>
    /// A clear of <paramref name="dungeonId"/>: in, down every floor, the boss's
    /// death recorded on the bottom one as its fall records it
    /// (<see cref="DungeonState.RecordBossDefeated"/>), back up, and out by the
    /// first floor's way out.
    /// </summary>
    private static void Clear(CampaignState campaign, string dungeonId)
    {
        var party = campaign.Party;
        var visit = campaign.Enter(dungeonId, 4);   // five floors deep wherever the range is the tuning's
        visit.OpenTurns(party, () => 10);
        while (visit.Current.DownStairs is { } down)
        {
            Gather(visit, party, down);
            visit.Descend(party);
            visit.OpenTurns(party, () => 10);
        }
        visit.RecordBossDefeated();
        while (visit.FloorIndex > 0)
        {
            Gather(visit, party, visit.Current.UpStairs);
            visit.Ascend(party);
            visit.OpenTurns(party, () => 10);
        }
        Gather(visit, party, visit.Current.UpStairs);
        Assert.Equal(RunOutcome.BossKill, campaign.EndRun(RunEnding.Exited).Outcome);
    }

    [Fact]
    public void SeeAddsEveryEnchantmentAWeaponCarries_AndOnlyGrows()
    {
        var campaign = new CampaignState();
        Assert.Empty(campaign.SeenInOrder);
        Assert.False(campaign.HasSeen("poison"));

        // A staff's innate, a wand's element: the entry the weapon arrived with
        // is met the moment the weapon is.
        campaign.See(TestWeapons.Get("staff_of_blight"));
        Assert.Equal(new[] { "poison" }, campaign.SeenInOrder);
        Assert.True(campaign.HasSeen("poison"));
        Assert.False(campaign.HasSeen("ward"));

        campaign.See(TestWeapons.Get("wand_of_the_nova", DamageType.Cold));
        campaign.See(TestWeapons.Get("tower_guard"));   // a martial weapon carrying none: nothing to meet, and no fault in that
        Assert.Equal(new[] { "cold", "poison" }, campaign.SeenInOrder);

        // Every entry a weapon carries, not just its first.
        campaign.See(Carrying("ward", "vampiric"));
        Assert.Equal(new[] { "cold", "poison", "vampiric", "ward" }, campaign.SeenInOrder);

        // And it only grows: meeting the same entry again is not an event, and
        // nothing that has been met is ever forgotten.
        campaign.See(TestWeapons.Get("staff_of_blight"));
        campaign.See(TestWeapons.Get("tower_guard"));
        Assert.Equal(new[] { "cold", "poison", "vampiric", "ward" }, campaign.SeenInOrder);
    }

    [Fact]
    public void TheStartingLoadoutIsMetToo_NotOnlyWhatCameOffTheFloor()
    {
        // "Whenever a weapon carrying one enters your inventory" is a rule about
        // the bag, not about the floor. A party does not find its first weapons:
        // it is handed them off the roster. Meeting only what was picked up would
        // leave a party that starts with a Renewal staff facing an enchanter
        // (6.4) that will not offer regeneration - an entry it has carried since
        // turn 0.
        //
        // The bags below are the roster's own loadout, filled the way
        // PartyMemberState.From fills one - the starting weapon in slot 0 and the
        // bag behind it. From itself is the scene's to call and a progression
        // test's (ProgressionStateTests.EveryFixtureMemberIsGrown enforces that),
        // so this mirrors it rather than calling it.
        var campaign = new CampaignState();
        var roster = GameContent.Current.Party.All;
        foreach (var def in roster)
        {
            var carrier = TestPools.Holding(def.Id, TestWeapons.Get(def.StartingWeaponId));
            for (int i = 0; i < def.BagWeaponIds.Count; i++)
                carrier.Inventory[i + 1] = TestWeapons.Get(def.BagWeaponIds[i]);
            campaign.See(carrier);
        }

        var carried = roster
            .SelectMany(def => def.BagWeaponIds.Prepend(def.StartingWeaponId))
            .SelectMany(id => TestWeapons.Get(id).Enchantments.Select(e => e.Id))
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal);
        Assert.Equal(carried, campaign.SeenInOrder);
        Assert.True(campaign.HasSeen("regeneration"), "the Renewal staff every member carries was never met");

        // The bag is read whole - the starting weapon and everything behind it -
        // and an empty slot is not an entry with no id.
        var member = TestPools.Char("E");
        var seen = new CampaignState();
        seen.See(member);
        Assert.Empty(seen.SeenInOrder);

        member.Inventory[2] = TestWeapons.Get("staff_of_mire");
        seen.See(member);
        Assert.Equal(new[] { "mire" }, seen.SeenInOrder);
    }

    [Fact]
    public void SeenKeepsUniqueSouls_AndTheOfferDropsThemAndTheInert()
    {
        // The Nameless Knife's three souls, two of which no roll and no service
        // may ever grant. They still go in: the set is what the campaign has MET,
        // and whether an entry may then be copied is a different question with an
        // answer already in the content (§5.5's neverRolled). Filtering here would
        // make the set answer the enchanter's question for it. So does an entry
        // that does nothing yet: Echoing, met, is met.
        var campaign = new CampaignState();
        campaign.See(TestWeapons.Get("flensing_knife_unique"));
        campaign.See(Carrying("echoing"));
        Assert.Equal(new[] { "echoing", "overheal", "serrated", "vampiric" }, campaign.SeenInOrder);

        var neverRolled = GameContent.Current.Enchantments.NeverRolled;
        Assert.Contains("serrated", neverRolled);
        Assert.Contains("overheal", neverRolled);
        Assert.DoesNotContain("vampiric", neverRolled);   // the Efficiency dagger carries it at tier 3, so it is an ordinary entry
        Assert.DoesNotContain("echoing", neverRolled);    // no soul: an entry declared inert (LootTable.IsInert)

        // The enchanter asks it (Phase 6a.3b): its offer is the met set, in the
        // view's fixed order, less the souls and the kinds that do nothing yet.
        Assert.Equal(new[] { "vampiric" }, Enchanter.Offers(campaign, TestWeapons.Get("tower_guard")).Select(offer => offer.Id));
    }

    [Fact]
    public void SeenInOrderIsOrdinalSorted_AndIsTheOnlyThingIteratedOrSaved()
    {
        // .NET randomises string hashing per process, so a HashSet's enumeration
        // order is not stable between two launches of the game even for identical
        // contents. An offer draw over it would then be irreproducible from the
        // same seed and the same save, and §5.1's serialisation would churn for
        // nothing. So the sorted view is the only thing anyone iterates - which
        // the membership assertions above would never have caught.
        string[] ids = ["ward", "poison", "flaming", "acidic", "vampiric", "regeneration", "mire", "cold", "shocking"];

        var forwards = new CampaignState();
        foreach (string id in ids)
            forwards.See(Carrying(id));

        var backwards = new CampaignState();
        foreach (string id in ids.Reverse())
            backwards.See(Carrying(id));

        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), forwards.SeenInOrder);
        Assert.Equal(forwards.SeenInOrder, backwards.SeenInOrder);

        // The campaign grows the views §5.1 saves (Phase 4: the party, the
        // permanent half), and each is an ordered list whose order is a fact of
        // the data. The party is roster order: the order it was handed, read the
        // same way every time, and for a new campaign the roster file's order.
        var a = TestPools.Char("A");
        var b = TestPools.Char("B");
        var handed = new CampaignState([b, a]);
        Assert.Equal(new[] { b, a }, handed.Party);
        Assert.Equal(handed.Party, handed.Party);

        var roster = GameContent.Current.Party.All.Select(def => def.Id).ToArray();
        Assert.Equal(roster, CampaignState.Start(GameContent.Current).Party.Select(m => m.Id));
        Assert.Equal(roster, CampaignState.Start(GameContent.Current).Party.Select(m => m.Id));

        // And the run's-end records (Phase 4: the locks, the cleared set, the
        // retired set), each an ordinal-sorted view read the same way every time
        // (LocksAndRetirementsAreOrdinalOrdered fills them).
        var fresh = new CampaignState();
        Assert.Equal(fresh.LockedInOrder, fresh.LockedInOrder);
        Assert.Equal(fresh.ClearedInOrder, fresh.ClearedInOrder);
        Assert.Equal(fresh.RetiredInOrder, fresh.RetiredInOrder);

        // And the type hands out no other view to iterate by accident: no public
        // set, no hash-ordered list, nothing but the named ordered views — the
        // stable among them, a list in the order its weapons were put there (Phase
        // 6a.2), and the enchanter's queue, in deposit order (Phase 6a.3a).
        string[] orderedViews =
        [
            nameof(CampaignState.SeenInOrder), nameof(CampaignState.Party),
            nameof(CampaignState.LockedInOrder), nameof(CampaignState.ClearedInOrder), nameof(CampaignState.RetiredInOrder),
            nameof(CampaignState.Stable), nameof(CampaignState.AtTheEnchanter),
        ];
        var iterable = typeof(CampaignState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !orderedViews.Contains(p.Name))
            .Where(p => typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string))
            .Select(p => p.Name)
            .Concat(typeof(CampaignState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name));
        Assert.Empty(iterable);
    }

    [Fact]
    public void LocksAndRetirementsAreOrdinalOrdered()
    {
        // The run's-end records §5.1 will save — the lock each clear wrote, the
        // dungeons cleared at least once, the dungeons retired for good — are kept
        // for lookups and handed out sorted by id with the ordinal comparer,
        // whatever order the runs came in: two campaigns clearing the same
        // dungeons in opposite orders read identically, and neither reads a hash
        // set's order. The tutorial retires and is never a lock.
        string[] hub = ["spear_hall", "long_gallery", "knife_warrens", "quiver_pits"];
        PartyMemberState[] Party() => new[] { "A", "B", "C", "D" }.Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i)).ToArray();

        var forwards = new CampaignState(Party());
        Clear(forwards, "tutorial");
        foreach (string id in hub)
            Clear(forwards, id);

        var backwards = new CampaignState(Party());
        Clear(backwards, "tutorial");
        foreach (string id in hub.Reverse())
            Clear(backwards, id);

        string[] sorted = ["knife_warrens", "long_gallery", "quiver_pits", "spear_hall"];
        Assert.Equal(sorted, hub.Order(StringComparer.Ordinal));
        Assert.Equal(sorted, forwards.LockedInOrder.Select(l => l.DungeonId));
        Assert.Equal(sorted, backwards.LockedInOrder.Select(l => l.DungeonId));
        Assert.Equal(forwards.LockedInOrder, forwards.LockedInOrder);
        foreach (var (id, until) in forwards.LockedInOrder)
            Assert.Equal(forwards.LockedUntilRun(id), until);   // the view and the lookup are one record

        Assert.Equal(sorted.Append("tutorial"), forwards.ClearedInOrder);
        Assert.Equal(forwards.ClearedInOrder, backwards.ClearedInOrder);
        Assert.Equal(new[] { "tutorial" }, forwards.RetiredInOrder);
        Assert.Equal(forwards.RetiredInOrder, backwards.RetiredInOrder);
        Assert.DoesNotContain("tutorial", forwards.LockedInOrder.Select(l => l.DungeonId));

        // The same five clears, the same counter; only which dungeon waits longest differs.
        Assert.Equal((5, 5), (forwards.RunCounter, backwards.RunCounter));
        Assert.NotEqual(forwards.LockedInOrder, backwards.LockedInOrder);
    }
}
