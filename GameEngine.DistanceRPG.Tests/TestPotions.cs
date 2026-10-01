using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Potions in a bag the way play leaves them (Phase 6b): the one door every potion
/// fixture of the suite goes through, so a rule that ties a carried potion to the
/// rest of the campaign is taught here once and never hunted for through the suite.
/// A potion is carried only once a dungeon besides the tutorial has been cleared —
/// the alchemist arrives with that clear (§6.6), and the save refuses a potion
/// before it (<c>SaveConsistency</c>, check 31) — so a campaign with no such clear
/// is first given one, as play gives it: a whole run to the bottom of a dungeon
/// that does not retire on its clear and out again (<see cref="CampaignRuns.Clear"/>),
/// on the premise's numbers its depth stands on.
/// </summary>
internal static class TestPotions
{
    /// <summary>
    /// <paramref name="campaign"/>, between its runs, with <paramref name="potions"/>
    /// laid in <paramref name="memberId"/>'s bag: each in a bag slot, 1 and up, where
    /// nothing lies — a slot holds one thing, and the hand keeps a weapon. A campaign
    /// that has cleared nothing beyond the tutorial clears a dungeon first.
    /// </summary>
    /// <returns>The member carrying them.</returns>
    public static PartyMemberState Carrying(CampaignState campaign, string memberId, params (int Slot, Potion Potion)[] potions)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (!campaign.ClearedInOrder.Except(campaign.RetiredInOrder, StringComparer.Ordinal).Any())
        {
            Assert.True(campaign.Visit is null, "a potion is mixed between runs: give the campaign its potions before the visit");
            using (TestContent.UsePremise())
            {
                var dungeon = GameContent.Current.Dungeons.All.First(def => !def.RetiresOnClear && campaign.IsSelectable(def));
                CampaignRuns.Clear(campaign, dungeon.Id, CampaignRuns.FiveDeep);
            }
        }

        var member = campaign.Party.Single(m => m.Id == memberId);
        foreach (var (slot, potion) in potions)
        {
            Assert.InRange(slot, 1, member.Inventory.Length - 1);   // the hand keeps a weapon
            Assert.Null(member.Inventory[slot]);   // a slot holds one thing
            member.Inventory[slot] = potion;
        }
        return member;
    }

    /// <summary>A health potion of <paramref name="quality"/>.</summary>
    public static Potion Health(int quality) => new(PotionType.Health, quality);

    /// <summary>A mana potion of <paramref name="quality"/>.</summary>
    public static Potion Mana(int quality) => new(PotionType.Mana, quality);
}
