using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Potions in a bag the way play leaves them (Phase 6b): the one door every potion
/// fixture of the suite goes through, so a rule that ties a carried potion to the
/// rest of the campaign is taught here once and never hunted for through the suite.
/// A potion is mixed between runs, so one is laid only while no visit is held; it
/// is carried only once a dungeon besides the tutorial has been cleared — the
/// alchemist arrives with that clear (§6.6), and the save refuses a potion before
/// it (<c>SaveConsistency</c>, check 31); and it is mixed from what the last run's
/// end yielded, so the potions every bag holds add up to no more than that yield
/// (check 32, Phase 6b.3).
/// <para>
/// <strong>So a fixture is given the run that makes its potions possible</strong>,
/// as play gives it. When the last run's end already yielded enough for every
/// potion the bags hold once these are laid, nothing is played. When it did not,
/// the potions the bags hold are lifted out, one run that counts is played — the
/// first clear of a dungeon besides the tutorial when the campaign has none, a
/// whole run to the bottom of a dungeon that does not retire on its clear and out
/// again (<see cref="CampaignRuns.Clear"/>), after the tutorial's own clear while it
/// stands, since the hub lists the tutorial alone until it is beaten; and otherwise
/// a deep retreat, a mercy (<see cref="CampaignRuns.DeepRetreat"/>) — and everything
/// is laid back. So a second call on one campaign never spoils what the first laid:
/// the run that covers it never sees a potion.
/// </para>
/// <para>
/// <strong>The run is played on the premise's content</strong>
/// (<see cref="Premise"/>), with a <see cref="Tuning.FarmYieldPerClear"/> stated
/// here that makes its yield cover the lot, whatever content the caller has in
/// force. So a fixture under dungeons of its own — a <c>dungeons.json</c> beside the
/// game that moves <c>retiresOnClear</c>, say — lays its potions before it enters
/// them, as play mixes them before that file is put beside the game, or after a run
/// that yielded enough; one that needs a run under them is refused here, by name.
/// </para>
/// <para>
/// <strong>What a fixture keeps.</strong> It asserts no potion outlives a run's end
/// — every potion carried spoils as a run ends, a wipe's included, so a potion
/// wanted after a run is laid after it — and it keeps to qualities a run's yield
/// could give, a few units, never the sanity bound: a yield of a thousand a clear at
/// most (check 33). A run arms every empty hand from its bag
/// (<see cref="PartyMemberState.Rest"/>), so a fixture that needs a hand left empty
/// lays its potions after a run that counted and yields enough, where this helper
/// plays nothing.
/// </para>
/// </summary>
internal static class TestPotions
{
    /// <summary>
    /// <paramref name="campaign"/>, between its runs, with <paramref name="potions"/>
    /// laid in <paramref name="memberId"/>'s bag: each in a bag slot, 1 and up, where
    /// nothing lies — a slot holds one thing, and the hand keeps a weapon — after the
    /// run that yields enough for them, when the last one did not.
    /// </summary>
    /// <returns>The member carrying them.</returns>
    public static PartyMemberState Carrying(CampaignState campaign, string memberId, params (int Slot, Potion Potion)[] potions)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        Assert.True(campaign.Visit is null, "a potion is mixed between runs: give the campaign its potions before the visit");

        long total = Farm.Mixed(campaign) + potions.Sum(laid => (long)laid.Potion.Quality);
        if (!Farm.AlchemistHasArrived(campaign) || (campaign.LastResult?.FarmYield ?? 0) < total)
            YieldEnough(campaign, total);

        var member = campaign.Party.Single(m => m.Id == memberId);
        foreach (var (slot, potion) in potions)
        {
            Assert.InRange(slot, 1, member.Inventory.Length - 1);   // the hand keeps a weapon
            Assert.Null(member.Inventory[slot]);   // a slot holds one thing
            member.Inventory[slot] = potion;
        }
        return member;
    }

    /// <summary>
    /// Play the one run that brings the alchemist when the campaign has no real clear
    /// — the tutorial's clear first, while it stands — or a mercy otherwise, at a
    /// yield a clear that covers <paramref name="total"/> units, on the premise's
    /// content — the bags' potions lifted out first, so it spoils none, and laid back.
    /// </summary>
    private static void YieldEnough(CampaignState campaign, long total)
    {
        Assert.True(GameContent.Current.Dungeons.All.Select(def => (def.Id, def.RetiresOnClear))
                .SequenceEqual(Premise.Dungeons.Dungeons.Select(def => (def.Id, def.RetiresOnClear))),
            "the run that yields for a potion is played on the premise's dungeons: lay a fixture's potions before it enters dungeons of its own, as play mixes them before such a dungeons.json is put beside the game");

        var held = campaign.Party.SelectMany(member => member.Potions.Select(carried => (Member: member, carried.Slot))).ToArray()
            .Select(at => (at.Member, at.Slot, Potion: at.Member.TakePotion(at.Slot))).ToArray();

        bool arrives = !Farm.AlchemistHasArrived(campaign);
        int clears = Farm.Clears(campaign) + (arrives ? 1 : 0);
        long perClear = (total + clears - 1) / clears;
        Assert.True(perClear <= ContentBounds.MostYieldPerClear,
            $"{total} units over {clears} clears is more than any tuning yields a clear: a potion fixture keeps to qualities a run could give");
        using (TestContent.UsePremise(tuning: t => t with { FarmYieldPerClear = (int)perClear }))
        {
            var tutorial = GameContent.Current.Dungeons.Tutorial;
            if (!campaign.IsRetired(tutorial.Id))
                CampaignRuns.Clear(campaign, tutorial.Id, CampaignRuns.GoldenSeed);   // the hub lists the tutorial alone until it is beaten
            var dungeon = GameContent.Current.Dungeons.All.First(def => !def.RetiresOnClear && campaign.IsSelectable(def));
            var ran = arrives ? CampaignRuns.Clear(campaign, dungeon.Id, CampaignRuns.FiveDeep) : CampaignRuns.DeepRetreat(campaign, dungeon.Id);
            Assert.True(ran.FarmYield >= total, $"the run yielded {ran.FarmYield} for {total} units");
            Assert.Null(ran.Spoiled);   // nothing was carried through it
        }

        foreach (var (member, slot, potion) in held)
        {
            Assert.Null(member.Inventory[slot]);
            member.Inventory[slot] = potion;
        }
    }

    /// <summary>A health potion of <paramref name="quality"/>.</summary>
    public static Potion Health(int quality) => new(PotionType.Health, quality);

    /// <summary>A mana potion of <paramref name="quality"/>.</summary>
    public static Potion Mana(int quality) => new(PotionType.Mana, quality);
}
