using System.Globalization;

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
/// it: §5.1's Run group and every floor the party has stood on. A quit that
/// finds none is settled to the next one first (<see cref="SettleToSafePoint"/>),
/// one fixed logic step at a time (<see cref="LogicStepSeconds"/>).
/// </para>
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// The one step the logic clock takes, in seconds: every settle
    /// (<see cref="SettleToSafePoint"/>) steps it, and live play is to step the
    /// same one — from Phase 5.8 the scene's fixed clock runs whole steps of it,
    /// a remainder carried and never stepped — so the state a settle saves is the
    /// state the player would have met, at any frame rate, and a fixed sequence of
    /// turns is the same world however its frames fell. At
    /// <see cref="GameConstants.EnemySpeed"/> an enemy moves 1.25 logic units a
    /// step, so the scene's tile-crossing sighting samples every tile a walk
    /// crosses; at 60 Hz two steps a frame move an enemy as smoothly as the
    /// frame's own step does, with no interpolation.
    /// </summary>
    public const float LogicStepSeconds = 1f / 120f;

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
    /// Step <paramref name="campaign"/> to its next safe point (1f), for a quit
    /// that finds none: the enemies' turn in flight — its banner, their walks,
    /// their beats — is played out, one <see cref="LogicStepSeconds"/> step at a
    /// time, to the next player turn, and only then is anything written. A
    /// campaign already at a safe point takes no step. A settle in which the
    /// enemies wipe the party ends the run in <c>Logic/</c> as play would (the
    /// visit's <see cref="DungeonState.Wiped"/> →
    /// <see cref="CampaignState.EndRun"/>), which leaves a campaign between runs:
    /// safe, the run's result recorded, and nothing of the visit to save.
    /// <para>
    /// <strong>The step is the caller's.</strong> The game's (Phase 5.8) is the
    /// scene's own — one <see cref="TurnSystem.Update"/> of
    /// <see cref="LogicStepSeconds"/> plus the enemy-phase sighting the scene
    /// feeds back (<see cref="TurnSystem.NotifyEnemyVisible"/> on a tile
    /// crossing, which decides whose zones may react) — so the settle takes
    /// exactly the steps play would have taken, on the visit's own die, and saves
    /// the turn the player would have met. Handed a step, the settle calls it and
    /// never <see cref="TurnSystem.Update"/> itself; handed none, it steps
    /// <see cref="TurnSystem.Update"/> alone, as a <c>Logic/</c> test steps the
    /// live side it compares with.
    /// </para>
    /// <para>
    /// <paramref name="maxSteps"/> bounds a bug, not a game: the default is
    /// nearly half an hour of game time, and no enemy phase comes near it. And a
    /// system that no step can bring to its player turn is refused at once
    /// rather than stepped to the bound: a raise in flight, which only the code
    /// inside it can finish — a settle never steps a system from inside its own
    /// cascade — and a game already over whose run nothing has ended.
    /// </para>
    /// </summary>
    /// <param name="campaign">The campaign to settle.</param>
    /// <param name="step">One fixed logic step, as the game takes it; null steps the open system's <see cref="TurnSystem.Update"/> by <see cref="LogicStepSeconds"/>.</param>
    /// <param name="maxSteps">The most steps taken before the settle gives up.</param>
    /// <returns>
    /// True once the campaign is at a safe point (<see cref="IsSafePoint"/>) — at
    /// once when it already was; false when it is not, and the steps ran out or
    /// no step can bring it to one: then nothing should be saved over the last
    /// safe point's save.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSteps"/> is negative.</exception>
    public static bool SettleToSafePoint(CampaignState campaign, Action? step = null, int maxSteps = 200_000)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSteps);

        for (int taken = 0; !IsSafePoint(campaign); taken++)
        {
            var turns = campaign.Visit!.Turns!;   // off a safe point there is a visit, and a system open on it
            if (taken == maxSteps || turns.Events.Running || turns.Phase == TurnPhase.GameOver)
                return false;
            if (step is null)
                turns.Update(LogicStepSeconds);
            else
                step();
        }
        return true;
    }

    /// <summary>
    /// The campaign as a document, stamped with <see cref="SaveCodec.CurrentVersion"/>
    /// and the hash of the tuning it is being played under — the current
    /// content's, on the current version's keys, which the document says
    /// (<see cref="SaveFile.TuningHashVersion"/>) — and never the tuning's values
    /// (§5.3): between runs, or with the visit in progress at a safe point.
    /// </summary>
    /// <param name="campaign">The campaign, at a safe point (<see cref="IsSafePoint"/>).</param>
    /// <param name="view">What the scene is showing, carried for it; null for nothing to say.</param>
    /// <param name="savedAt">
    /// When it is being saved, written as the document's last member
    /// (<see cref="SaveFile.SavedAt"/>, <see cref="TimeText"/>); null records no time.
    /// Handed in by the edge, whose clock it is: <c>Logic/</c> never reads one, so
    /// a capture is the same text for the same campaign and the same time.
    /// </param>
    /// <exception cref="InvalidOperationException">The campaign is not at a safe point: the enemies are moving, their turn's banner is up, or a raise is running.</exception>
    public static SaveFile Capture(CampaignState campaign, ViewDto? view = null, DateTimeOffset? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (!IsSafePoint(campaign))
            throw new InvalidOperationException(
                $"A visit to '{campaign.Visit!.Def.Id}' is not at a safe point ({campaign.Visit.Turns!.Phase}{(campaign.Visit.Turns.Events.Running ? ", a raise running" : "")}): a campaign is saved between runs, or at a player turn with nothing in flight.");

        return new SaveFile(SaveCodec.CurrentVersion, TuningJson.Hash(GameContent.Current.Tuning, SaveCodec.CurrentVersion), SaveCodec.CurrentVersion,
            SaveMapper.Capture(campaign), view, savedAt is { } at ? TimeText(at) : null);
    }

    /// <summary>
    /// A save's time as the document writes it (<see cref="SaveFile.SavedAt"/>):
    /// the UTC instant, round-trip (<c>"O"</c>), ending <c>Z</c> —
    /// <c>2026-09-25T14:32:00.0000000Z</c>. A <c>+00:00</c> offset would be written
    /// <c>+00:00</c> by the save's encoder; the <c>Z</c> reads the same and
    /// stays readable in the file.
    /// </summary>
    internal static string TimeText(DateTimeOffset at) => at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// When <paramref name="file"/> was saved, or null when it records no time —
    /// as every save written before the slots does. The member is raw text a hand
    /// can edit, and never trusted (the user's ruling of 2026-09-25 on raw JSON), so
    /// one that is no round-trip time is refused, naming it: <c>savedAt: yesterday
    /// is no time</c>, a problem whose first clause a notice draws, so it quotes
    /// nothing.
    /// </summary>
    /// <exception cref="SaveFormatException">The time is there and is no round-trip time.</exception>
    internal static DateTimeOffset? SavedAtOf(SaveFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.SavedAt is not { } text)
            return null;
        if (DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            return at;
        throw new SaveFormatException(SaveCodec.SavedAtMember, $"{(string.IsNullOrWhiteSpace(text) ? "a blank" : text.Trim())} is no time");
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
    /// tuning warns and loads (<see cref="LoadedCampaign.TuningChanged"/>) — asked
    /// on the keys of the version the save's hash is of
    /// (<see cref="SaveFile.TuningHashVersion"/>, PHASE 6a DECISION 9), so a key
    /// that began to play since is no number the campaign was played under; an id
    /// the content no longer has refuses the save, naming where, and so do a time
    /// that is no time (<see cref="SavedAtOf"/>) and a hash of a version the
    /// document is older than, before anything is built.
    /// </summary>
    /// <param name="file">A document as <see cref="SaveCodec.Read"/> returns it, at <see cref="SaveCodec.CurrentVersion"/>.</param>
    /// <param name="content">
    /// The content to restore under, which must be <see cref="GameContent.Current"/>:
    /// a member and a weapon are built under the current content — a weapon's
    /// costs resolve against its modifier table — so a restore under any other
    /// would hand back rows of one content carrying another's numbers.
    /// </param>
    /// <exception cref="SaveFormatException">The document names something the content does not have, holds a count out of range or a time that is no time, contradicts itself, or is at another version; and where.</exception>
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
        if (file.TuningHashVersion < 1 || file.TuningHashVersion > file.Version)
            throw new SaveFormatException(SaveCodec.TuningHashVersionMember,
                $"{file.TuningHashVersion} is no version of this document: a tuning hash is of the keys of version 1 to {file.Version}, never of a key set newer than its document");
        SavedAtOf(file);   // a time that is no time refuses the save before a floor is built

        var campaign = SaveMapper.Restore(file.Campaign, content);
        bool changed = !string.Equals(file.TuningHash, TuningJson.Hash(content.Tuning, file.TuningHashVersion), StringComparison.Ordinal);
        return new LoadedCampaign(campaign, changed, file.View);
    }
}
