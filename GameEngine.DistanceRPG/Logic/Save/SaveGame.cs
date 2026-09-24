namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A campaign a save brought back: the campaign itself, whether the tuning it
/// was played under is not the one loaded now, and what the scene was showing.
/// </summary>
/// <param name="Campaign">The campaign, whole.</param>
/// <param name="TuningChanged">
/// The save's tuning hash is not the loaded tuning's (§5.3: "loading under
/// different numbers warns rather than refuses"): the campaign plays on under
/// the new numbers, and whoever loaded it says so. The next save records the
/// new hash, so one change warns once.
/// </param>
/// <param name="View">The document's view, handed through to the scene as it was saved; <c>Logic/</c> reads none of it.</param>
public sealed record LoadedCampaign(CampaignState Campaign, bool TuningChanged, ViewDto? View);

/// <summary>
/// The campaign to and from its save document (§5.1, §5.2): the one entry the
/// game saves and loads through, with the text on either side of it
/// <see cref="SaveCodec"/>'s and the file the edge's. A capture is plain values
/// — ids for content, names for enums — and a restore resolves them against the
/// content loaded now, through the internal doors the gameplay types keep for
/// it, so none of them carries a serialisation attribute or a public setter for
/// a load's sake (§5.2).
/// <para>
/// A campaign is saved <strong>between runs</strong> — the party, what it
/// carries and has met, the run counter, the locks, the cleared and retired
/// dungeons and the last run's summary, which is already a whole save (§6.7: "a
/// save with no dungeon in progress is now a valid state") — and <strong>at any
/// safe point of a visit</strong> (<see cref="IsSafePoint"/>), the visit inside
/// it: §5.1's Run group and every floor the party has stood on.
/// </para>
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// Whether <paramref name="campaign"/> can be saved as it stands (1f): between
    /// runs; in a visit with no floor's system open — right after the entry, or
    /// between the stairs and the arrival; or with the floor's system in the
    /// player phase and no raise running. Never while the enemies take their turn,
    /// its banner included, and never inside a raise: a save holds none of the
    /// enemy phase's working state, and a raise's cascade is half-settled.
    /// </summary>
    public static bool IsSafePoint(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.Visit is not { } visit || visit.Turns is not { } turns)
            return true;
        return turns.Phase == TurnPhase.Player && !turns.Events.Running;
    }

    /// <summary>
    /// The campaign as a document, stamped with <see cref="SaveCodec.CurrentVersion"/>
    /// and the hash of the tuning it is being played under — the current
    /// content's — and never the tuning's values (§5.3): between runs, or with
    /// the visit in progress at a safe point.
    /// </summary>
    /// <param name="campaign">The campaign, at a safe point (<see cref="IsSafePoint"/>).</param>
    /// <param name="view">What the scene is showing, carried for it; null for nothing to say.</param>
    /// <exception cref="InvalidOperationException">The campaign is not at a safe point: the enemies are moving, their turn's banner is up, or a raise is running.</exception>
    public static SaveFile Capture(CampaignState campaign, ViewDto? view = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (!IsSafePoint(campaign))
            throw new InvalidOperationException(
                $"A visit to '{campaign.Visit!.Def.Id}' is not at a safe point ({campaign.Visit.Turns!.Phase}{(campaign.Visit.Turns.Events.Running ? ", a raise running" : "")}): a campaign is saved between runs, or at a player turn with nothing in flight.");

        return new SaveFile(SaveCodec.CurrentVersion, TuningJson.Hash(GameContent.Current.Tuning), SaveMapper.Capture(campaign), view);
    }

    /// <summary>
    /// The campaign <paramref name="file"/> describes, whole, under
    /// <paramref name="content"/>: every weapon, enchantment, dungeon and status it
    /// names resolved by id or by name, every member rebuilt with the bag it was
    /// built with, the visit in progress — if there was one — with every floor it
    /// reached stood back on its regenerated terrain and the rest regenerated
    /// from the seed, and nothing re-derived that the save holds. No floor's
    /// system is open: whoever plays the visit opens it
    /// (<see cref="DungeonState.OpenTurns"/>), as an arrival does. A different
    /// tuning warns and loads (<see cref="LoadedCampaign.TuningChanged"/>); an id
    /// the content no longer has refuses the save, naming where.
    /// </summary>
    /// <param name="file">A document as <see cref="SaveCodec.Read"/> returns it, at <see cref="SaveCodec.CurrentVersion"/>.</param>
    /// <param name="content">
    /// The content to restore under, which must be <see cref="GameContent.Current"/>:
    /// a member and a weapon are built under the current content — a weapon's
    /// costs resolve against its modifier table — so a restore under any other
    /// would hand back rows of one content carrying another's numbers.
    /// </param>
    /// <exception cref="SaveFormatException">The document names something the content does not have, holds a count out of range, or is at another version; and where.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    public static LoadedCampaign Restore(SaveFile file, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);
        if (!ReferenceEquals(content, GameContent.Current))
            throw new ArgumentException(
                "A save is restored under the current content: its members and weapons are built under GameContent.Current, so the content resolving its ids must be the current one too.",
                nameof(content));
        if (file.Version != SaveCodec.CurrentVersion)
            throw new SaveFormatException(SaveCodec.VersionMember,
                $"{file.Version}: a restore reads version {SaveCodec.CurrentVersion}, which SaveCodec.Read lifts an older document to");

        var campaign = SaveMapper.Restore(file.Campaign, content);
        bool changed = !string.Equals(file.TuningHash, TuningJson.Hash(content.Tuning), StringComparison.Ordinal);
        return new LoadedCampaign(campaign, changed, file.View);
    }
}
