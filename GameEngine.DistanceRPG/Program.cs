using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

internal class Program
{
    /// <summary>
    /// The command that writes the data files instead of playing:
    /// <c>DistanceRPG --write-data &lt;folder&gt;</c> writes what the generator makes of
    /// the compiled content (<see cref="ContentFiles.WriteDefaults"/>) into the
    /// folder and exits, no window opened. Run on <c>GameEngine.DistanceRPG/Data</c>
    /// it regenerates the shipped files the tests pin; run on the folder beside the
    /// executable it resets an edited copy to the built-in numbers.
    /// </summary>
    internal const string WriteDataOption = "--write-data";

    private static int Main(string[] args)
    {
        // Install diagnostics first so any startup failure ends up in a crash dump
        // rather than disappearing into the void.
        Log.Initialize();
        CrashHandler.Install();
        Log.Info($"DistanceRPG starting (args: {string.Join(" ", args)})");

        try
        {
            if (Array.IndexOf(args, WriteDataOption) >= 0)
            {
                if (WriteDataDirectory(args) is not { } directory)
                {
                    Log.Error($"{WriteDataOption} names no folder: DistanceRPG {WriteDataOption} <folder>");
                    return 1;
                }
                WriteData(directory, line => Log.Info(line));
                return 0;
            }

            using var game = new DistanceRpgGame();
            game.Run();
            return 0;
        }
        finally
        {
            Log.Info("DistanceRPG shutting down");
            Log.Shutdown();
        }
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
    /// The generator's files, written into <paramref name="directory"/> (made
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
