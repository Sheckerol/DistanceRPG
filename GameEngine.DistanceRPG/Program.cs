using GameEngine.Core.Diagnostics;

namespace GameEngine.DistanceRPG;

internal class Program
{
    private static void Main(string[] args)
    {
        // Install diagnostics first so any startup failure ends up in a crash dump
        // rather than disappearing into the void.
        Log.Initialize();
        CrashHandler.Install();
        Log.Info($"DistanceRPG starting (args: {string.Join(" ", args)})");

        try
        {
            using var game = new DistanceRpgGame();
            game.Run();
        }
        finally
        {
            Log.Info("DistanceRPG shutting down");
            Log.Shutdown();
        }
    }
}
