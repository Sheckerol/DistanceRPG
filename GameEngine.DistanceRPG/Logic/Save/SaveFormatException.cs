namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A save that cannot be restored, and where: malformed text, a version newer
/// than this build reads, a member missing, or a name or an id the loaded
/// content does not have — <c>campaign.party[1].inventory[2].id: 'blade_x' is no
/// weapon in the content</c>. A save never limps: it restores whole or is
/// refused whole, and the refusal names the path in the document that broke
/// it, so a playtester can open the file and find the line.
/// </summary>
public sealed class SaveFormatException : Exception
{
    /// <param name="where">The path in the document: <c>campaign.party[1].inventory[2].id</c>, <c>version</c>, or a line for text that is not JSON.</param>
    /// <param name="problem">What is wrong there, as a sentence.</param>
    /// <param name="inner">The failure underneath, when there is one.</param>
    public SaveFormatException(string where, string problem, Exception? inner = null)
        : base($"{where}: {problem}", inner)
    {
        ArgumentNullException.ThrowIfNull(where);
        ArgumentNullException.ThrowIfNull(problem);
        Where = where;
        Problem = problem;
    }

    /// <summary>The path in the document that broke the restore: <c>campaign.seen[3]</c>.</summary>
    public string Where { get; }

    /// <summary>What is wrong there.</summary>
    public string Problem { get; }
}
