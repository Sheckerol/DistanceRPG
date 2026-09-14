namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Every trigger the design names, and nothing else (§1.7). Modifiers,
/// enchantments and statuses declare which of these they handle, and one
/// dispatcher (<see cref="EventTable"/>) runs them in declared order. A
/// mechanic that needs an event not listed here is the design saying it is a
/// new <em>kind</em> of thing — a signal worth keeping, so there is
/// deliberately no catch-all member.
/// </summary>
public enum GameEvent
{
    AttackDeclared,
    DamageComputed,
    DamageDealt,
    DamageTaken,
    Crit,
    Killed,
    HealingReceived,
    HealingAboveFull,
    ManaSpent,
    MovementSpent,
    TurnStart,
    TurnEnd,
    RoundEnd,
    ThreatZoneEntered,
    Cast,
}
