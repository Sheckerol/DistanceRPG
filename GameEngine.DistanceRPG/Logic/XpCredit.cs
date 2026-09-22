namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The three pools a party member earns (§2.2), each fed by doing the thing it
/// measures. A fourth, a weapon's wear capacity (§6.1), belongs to the item
/// rather than to the member and is not one of these.
/// </summary>
public enum XpPool
{
    /// <summary>Proficiency with a weapon class: fed by the weapon's own mitigated damage, or by a staff's status levels applied.</summary>
    Weapon,

    /// <summary>Max HP: fed by HP actually restored — "constitution grows by getting hurt and then healed".</summary>
    Health,

    /// <summary>Max mana: fed by mana actually spent, by a cast or by an enchantment trigger.</summary>
    Mana,
}

/// <summary>
/// One raw XP credit: which pool it feeds, the weapon class when the pool is
/// <see cref="XpPool.Weapon"/>, and the payload's own integer.
/// <para>
/// <strong>The amount is raw</strong> (§2.3): nothing multiplies a gain by a
/// stat anywhere in the tree, because the governing stat divides the
/// <em>threshold</em> instead. Two members handed the same credit bank the same
/// XP and differ only in how often that XP crosses a bar.
/// </para>
/// <para>
/// A value type with no behaviour: the arithmetic is
/// <see cref="PartyMemberState.Credit"/>'s and the decision of what figure to
/// credit is the applier's, so this is only the sentence the two exchange.
/// </para>
/// </summary>
/// <param name="Pool">The pool credited.</param>
/// <param name="Class">The class levelled, for <see cref="XpPool.Weapon"/>; null for the two pools that are the member's own.</param>
/// <param name="Amount">The raw figure: damage dealt, levels applied, HP restored or mana spent. Never scaled, never negative.</param>
public readonly record struct XpCredit(XpPool Pool, WeaponClass? Class, int Amount);
