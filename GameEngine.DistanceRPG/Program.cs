using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the command line asked for, as <see cref="Program.Parse"/> read it: the
/// folder <c>--write-data</c> names, or null, and whether <c>--new-campaign</c>
/// was given.
/// </summary>
/// <param name="WriteDataDirectory">The folder the data files are written to, instead of playing; null when none was named.</param>
/// <param name="NewCampaign">Set the campaign aside and start fresh at launch.</param>
internal sealed record LaunchOptions(string? WriteDataDirectory, bool NewCampaign);

internal class Program
{
    /// <summary>
    /// The command that writes the data files instead of playing:
    /// <c>DistanceRPG --write-data &lt;folder&gt;</c> writes the six files the
    /// generator makes of the compiled content (<see cref="ContentFiles.WriteDefaults"/>:
    /// <c>tuning.json</c>, <c>restricted.json</c>, <c>enchantments.json</c>,
    /// <c>weapons.json</c>, <c>dungeons.json</c> and <c>party.json</c>) into the
    /// folder and exits, no window opened. Run on <c>GameEngine.DistanceRPG/Data</c>
    /// it regenerates the shipped files the tests pin; run on the folder beside the
    /// executable it resets every edited copy to the built-in content.
    /// </summary>
    internal const string WriteDataOption = "--write-data";

    /// <summary>
    /// <c>DistanceRPG --new-campaign</c> sets the campaign's backup and its save
    /// aside as <c>campaign.abandoned-N.json</c> — never deleted — and starts a
    /// fresh campaign, saved at once (1f): the way out of a campaign a playtester
    /// wants gone without finishing its run, beside NEW CAMPAIGN on the screen
    /// between runs. Neither file is left to come back as the new campaign's own.
    /// </summary>
    internal const string NewCampaignOption = "--new-campaign";

    private static int Main(string[] args)
    {
        // Install diagnostics first so any startup failure ends up in a crash dump
        // rather than disappearing into the void.
        Log.Initialize();
        CrashHandler.Install();
        Log.Info($"DistanceRPG starting (args: {string.Join(" ", args)})");

        try
        {
            var warnings = new List<string>();
            var options = Parse(args, warnings);
            foreach (string warning in warnings)
                Log.Warn($"[Launch] {warning}");

            if (Array.IndexOf(args, WriteDataOption) >= 0)
            {
                if (options.WriteDataDirectory is not { } directory)
                {
                    Log.Error($"{WriteDataOption} names no folder: DistanceRPG {WriteDataOption} <folder>");
                    return 1;
                }
                if (options.NewCampaign)
                    Log.Warn($"[Launch] {NewCampaignOption} does nothing beside {WriteDataOption}, which writes the data files and exits without playing");
                WriteData(directory, line => Log.Info(line));
                return 0;
            }

            using var game = new DistanceRpgGame(options);
            game.Run();
            return 0;
        }
        finally
        {
            Log.Info("DistanceRPG shutting down");
            Log.Shutdown();
        }
    }

    /// <summary>
    /// The command line, read: the folder <see cref="WriteDataOption"/> names
    /// (<see cref="WriteDataDirectory"/>) and whether <see cref="NewCampaignOption"/>
    /// is there. Pure: nothing is read but the arguments. An argument the game does
    /// not know is ignored, and so is a <see cref="WriteDataOption"/> that names no
    /// folder, each with a warning in <paramref name="warnings"/> for the log.
    /// </summary>
    internal static LaunchOptions Parse(IReadOnlyList<string> args, ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? writeData = WriteDataDirectory(args);
        bool newCampaign = false;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg == WriteDataOption)
            {
                if (writeData is not null && i + 1 < args.Count && args[i + 1] == writeData)
                    i++;   // the folder it names
                else
                    warnings?.Add($"{WriteDataOption} names no folder: DistanceRPG {WriteDataOption} <folder>");
            }
            else if (arg == NewCampaignOption)
            {
                newCampaign = true;
            }
            else
            {
                warnings?.Add($"'{arg}' is no argument the game knows, and is ignored: it knows {WriteDataOption} <folder> and {NewCampaignOption}");
            }
        }
        return new LaunchOptions(writeData, newCampaign);
    }

    /// <summary>The folder <see cref="WriteDataOption"/> names — the argument after it — or null when it names none.</summary>
    internal static string? WriteDataDirectory(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int i = 0; i < args.Count - 1; i++)
            if (args[i] == WriteDataOption && !string.IsNullOrWhiteSpace(args[i + 1]) && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return args[i + 1];
        return null;
    }

    /// <summary>
    /// The generator's six files, written into <paramref name="directory"/> (made
    /// absolute against the working folder, and created if need be), each path
    /// reported as it is written. The command's whole body, so a test runs it into
    /// a folder of its own.
    /// </summary>
    internal static void WriteData(string directory, Action<string>? report = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DataFiles.Write(Path.GetFullPath(directory), ContentFiles.WriteDefaults(), report);
    }
}
