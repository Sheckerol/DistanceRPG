namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Every compiled behaviour, registered in one fixed sequence, so a table
/// built anywhere — the turn system's or a test's — carries the same chains.
/// Adding a behaviour is one handler and one priority in the group it belongs
/// to; nothing here or elsewhere is edited for it.
/// </summary>
public static class Behaviours
{
    public static void RegisterAll(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        CombatBehaviours.Register(table);
        StatusBehaviours.Register(table);
        DisplacementBehaviours.Register(table);
        ReactionBehaviours.Register(table);
        EnchantmentBehaviours.Register(table);
    }
}
