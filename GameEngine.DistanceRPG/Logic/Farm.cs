namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The farm (§6.6): where potions come from. Every dungeon besides the tutorial
/// beaten once is a worker (<see cref="Clears"/>), and every run that counts
/// makes a batch of <see cref="Tuning.FarmYieldPerClear"/> units a worker, in the
/// same event as the enchanter's tick (<see cref="CampaignState.EndRun"/>, PHASE 6b
/// DECISION 4). Engine-free and stateless: it asks the campaign's own records —
/// the cleared and retired sets, the last run's result and the bags — and keeps
/// none of its own.
/// <para>
/// <strong>The batch is a flow, not a stock</strong> (§6.6), and this is the
/// spoilage rule's one home (§6.7: "the batch must never reach
/// <c>CampaignState</c>"). The run's end saves what the farm yielded as a fact of
/// the run (<see cref="RunResult.FarmYield"/>); the batch is that yield less every
/// potion's quality the bags hold, worked out (<see cref="Batch"/>), never a field
/// (PHASE 6b DECISION 6). So nothing is ever added to it — each run's end replaces
/// the last result, and its yield with it — and a quit and a reload never refill
/// it. What is left unmixed spoils with no code to spoil it: the batch reads 0 the
/// moment a visit is held, and the next run's end brings a fresh one. A potion is
/// the one thing that outlives a mix, and every potion carried spoils as the run
/// ends (<see cref="Spoil"/>), so nothing of a batch reaches a second descent.
/// </para>
/// <para>
/// <strong>The alchemist arrives with the first real clear</strong> (§6.6, "the
/// tutorial does not count"): derived from the clear set, never saved
/// (<see cref="AlchemistHasArrived"/>), as a weapon's capacity is replayed rather
/// than stored (PHASE 6a DECISION 2). A new campaign, and every campaign before its
/// first clear of a dungeon besides the tutorial, has no batch and no potions.
/// </para>
/// </summary>
public static class Farm
{
    /// <summary>
    /// The farm's workers: every dungeon cleared at least once that is not retired
    /// — <c>|cleared \ retired|</c>. The tutorial retires on its clear, so it never
    /// counts, and a repeat clear finds its dungeon in the set already. Asked of the
    /// campaign's own records, never of which dungeon today's <c>dungeons.json</c>
    /// retires on its clear (check 30's precedent): a copy beside the game may move
    /// that flag, and the farm a campaign built stays the farm it built.
    /// </summary>
    public static int Clears(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        return campaign.ClearedInOrder.Count(id => !campaign.IsRetired(id));
    }

    /// <summary>
    /// Whether the alchemist has arrived (§6.6): with the first clear of a dungeon
    /// besides the tutorial — the farm's first worker. Derived, never saved. So he
    /// is there while the farm has a worker: a <c>dungeons.json</c> that moves
    /// <c>retiresOnClear</c> onto the farm's one worker retires it on its re-clear,
    /// and he goes with it until the next first clear (PHASE 6b DECISION 11).
    /// </summary>
    public static bool AlchemistHasArrived(CampaignState campaign) => Clears(campaign) >= 1;

    /// <summary>
    /// The units a run's end yields (§6.6): <see cref="Tuning.FarmYieldPerClear"/>
    /// a worker on a run that counted — a boss kill, or §6.3's mercy, the
    /// enchanter's tick (PHASE 6b DECISION 4) — and nothing on one that did not.
    /// Worked out wide and held at <see cref="ContentBounds.MostCount"/>, the count a
    /// save reads it back to.
    /// </summary>
    /// <param name="clears">The farm's workers as the run ends (<see cref="Clears"/>), the run's own first clear among them (PHASE 6b DECISION 5).</param>
    /// <param name="counted">Whether the run counted (<see cref="RunResult.ServiceTick"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="clears"/> is negative.</exception>
    public static int Yield(int clears, bool counted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clears);
        if (!counted)
            return 0;
        long yield = (long)GameContent.Current.Tuning.FarmYieldPerClear * clears;
        return (int)Math.Min(yield, ContentBounds.MostCount);
    }

    /// <summary>
    /// The batch the alchemist holds now (§6.6): between runs, what the last run's
    /// end yielded less every potion's quality the bags hold — a quality of
    /// <c>q</c> is <c>q</c> units mixed — and never below 0. During a visit, 0:
    /// what was left unmixed spoiled on the descent. Worked out, never a field
    /// (PHASE 6b DECISION 6), so a mix writes only a potion into a bag and a
    /// pour-back hands its units back exactly.
    /// </summary>
    public static int Batch(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.Visit is not null || campaign.LastResult is not { } last)
            return 0;
        long left = last.FarmYield - Mixed(campaign);
        return left > 0 ? (int)left : 0;
    }

    /// <summary>The units every potion in every bag was mixed from: the sum of their qualities, in roster then slot order, summed wide.</summary>
    public static long Mixed(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        long units = 0;
        foreach (var member in campaign.Party)
            foreach (var (_, potion) in member.Potions)
                units += potion.Quality;
        return units;
    }

    /// <summary>
    /// Every potion carried spoils (§6.6: "a mixed potion keeps exactly as long as
    /// the trip it was mixed for"): each is taken out of its bag through the one
    /// door a potion leaves by (<see cref="PartyMemberState.TakePotion"/>), in
    /// roster then slot order, and handed back for the run's result. Weapons are
    /// never touched. <strong>Its one caller is the run's end</strong>
    /// (<see cref="CampaignState.EndRun"/>, pinned by IL), on every ending, a wipe's
    /// included: a potion is a consumable, lost by its lifetime, never gear a wipe
    /// forfeits.
    /// </summary>
    /// <returns>The potions spoiled, empty when none was carried.</returns>
    internal static IReadOnlyList<Potion> Spoil(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var spoiled = new List<Potion>();
        foreach (var member in campaign.Party)
            foreach (int slot in member.Potions.Select(carried => carried.Slot).ToArray())
                spoiled.Add(member.TakePotion(slot));
        return spoiled;
    }
}
