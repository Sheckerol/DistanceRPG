using System.Collections;
using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// What the campaign has met (§3.3): the enchantment ids a weapon has brought
/// into the party's hands, which is what §6.4's enchanter will offer from and
/// what §5.1 saves. Small, and the one collection that only ever grows.
/// </summary>
public class CampaignStateTests
{
    /// <summary>A weapon carrying exactly the named entries, for the cases the catalogue has no single item for.</summary>
    private static Weapon Carrying(params string[] enchantmentIds)
        => TestWeapons.Enchanted("Met", range: 40, damage: 10, cost: 30, enchantmentIds);

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
    public void SeenKeepsUniqueSouls_AndTheOfferFilterIsNeverRolled()
    {
        // The Nameless Knife's three souls, two of which no roll and no service
        // may ever grant. They still go in: the set is what the campaign has MET,
        // and whether an entry may then be copied is a different question with an
        // answer already in the content (§5.5's neverRolled). Filtering here would
        // make the set answer a question §6.4 has not asked yet.
        var campaign = new CampaignState();
        campaign.See(TestWeapons.Get("flensing_knife_unique"));
        Assert.Equal(new[] { "overheal", "serrated", "vampiric" }, campaign.SeenInOrder);

        var neverRolled = GameContent.Current.Enchantments.NeverRolled;
        Assert.Contains("serrated", neverRolled);
        Assert.Contains("overheal", neverRolled);
        Assert.DoesNotContain("vampiric", neverRolled);   // the Efficiency dagger carries it at tier 3, so it is an ordinary entry

        // What an offer could draw from is therefore the met set less that filter
        // - one line, over a view whose order is fixed.
        Assert.Equal(new[] { "vampiric" }, campaign.SeenInOrder.Where(id => !neverRolled.Contains(id)));
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

        // And the type hands out no other view to iterate by accident: no public
        // set, no public list, nothing but the sorted one.
        var iterable = typeof(CampaignState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(CampaignState.SeenInOrder))
            .Where(p => typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string))
            .Select(p => p.Name)
            .Concat(typeof(CampaignState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name));
        Assert.Empty(iterable);
    }
}
