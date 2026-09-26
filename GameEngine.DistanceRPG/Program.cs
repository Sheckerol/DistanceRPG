using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the command line asked for, as <see cref="Program.Parse"/> read it: the
/// folder <c>--write-data</c> names, or null, and whether <c>--new-campaign</c>
/// was given.
/// </summary>
/// <param name="WriteDataDirectory">The folder the data files are written to, instead of playing; null when none was named.</param>
/// <param name="NewCampaign">Start fresh at launch, in the lowest empty slot — else slot 1, what it holds set aside.</param>
internal sealed record LaunchOptions(string? WriteDataDirectory, bool NewCampaign);

internal class Program
{
    /// <summary>
    /// The command that writes the data files instead of playing:
    /// <c>DistanceRPG --write-data &lt;folder&gt;</c> writes the four copies the
    /// game carries built in (<see cref="ShippedData.Texts"/>: <c>tuning.json</c>,
    /// <c>restricted.json</c>, <c>weapons.json</c> and <c>dungeons.json</c>), byte
    /// for byte, into the folder and exits, no window opened. Run on the folder
    /// beside the executable it puts back every copy there as the build shipped
    /// it — the way back from an edited copy, which a rebuild keeps when it is
    /// newer than its master. It refuses the source <c>Data</c> folder, which
    /// holds the master copies themselves: writing the last build's copies there
    /// would put them over edits not yet built. (The enchantment catalogue and the
    /// starting party are code, and have no file to write.)
    /// </summary>
    internal const string WriteDataOption = "--write-data";

    /// <summary>
    /// <c>DistanceRPG --new-campaign</c> starts a fresh campaign in the slot the
    /// title's NEW CAMPAIGN list would start on — the lowest empty slot within
    /// <c>tuning.json</c>'s <c>SaveSlots</c>, else slot 1, whose backup and save it
    /// sets aside as <c>campaign.abandoned-N.json</c>, never deleted — saved there
    /// at once (1f), and saving there from then on, straight to the screen between
    /// runs past the title: developer tooling, beside the title's NEW CAMPAIGN,
    /// where the player chooses the slot and is asked before one in use is set
    /// aside. Neither file is left to come back as the new campaign's own.
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
                if (!WriteData(directory, line => Log.Info(line)))
                {
                    Log.Error($"{WriteDataOption} wrote nothing: {Path.GetFullPath(directory)} is the source Data folder, which holds the master copies - "
                        + "edit them there and rebuild, or name another folder (the one beside the executable, to reset its copies)");
                    return 1;
                }
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
    /// The four built-in copies, written into <paramref name="directory"/> (made
    /// absolute against the working folder, and created if need be) byte for
    /// byte, each path reported as it is written — or nothing at all, and false,
    /// when the folder is the source <c>Data</c> folder
    /// (<see cref="IsSourceDataFolder"/>). A copy the build does not carry is
    /// reported, not written. The command's whole body, so a test runs it into a
    /// folder of its own.
    /// </summary>
    internal static bool WriteData(string directory, Action<string>? report = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string full = Path.GetFullPath(directory);
        if (IsSourceDataFolder(full))
        {
            report?.Invoke($"[Data] {full} is the source Data folder: it holds the master copies, and the built-in copies are not written over them");
            return false;
        }

        var texts = ShippedData.Texts;
        foreach (var (name, why) in texts.Unreadable ?? new Dictionary<string, string>())
            report?.Invoke($"[Data] {name} is not written: {why}");
        DataFiles.Write(full, texts, report);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="directory"/> is the source <c>Data</c> folder: a
    /// folder named <c>Data</c> whose parent holds <c>GameEngine.DistanceRPG.csproj</c>.
    /// Its files are the master copies — the ones the game is built with — so the
    /// command's old use, regenerating them there, would now put the last build's
    /// copies over edits not yet built.
    /// </summary>
    internal static bool IsSourceDataFolder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var folder = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));
        return string.Equals(folder.Name, "Data", StringComparison.OrdinalIgnoreCase)
            && folder.Parent is { } parent
            && File.Exists(Path.Combine(parent.FullName, "GameEngine.DistanceRPG.csproj"));
    }
}
