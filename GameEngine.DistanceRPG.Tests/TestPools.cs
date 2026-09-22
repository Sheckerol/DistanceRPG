using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The one place a test builds a party member, and the one place a fixture's
/// pools are filled.
/// <para>
/// A member's maxima are earned now (§2.1): both pools open at
/// <see cref="Tuning.StartingPool"/> — 25 — and rise a point at a time. Every
/// scenario in this suite was written against the flat 100/100 a member used to
/// carry, and a scenario is about the thing it names (a block, a cleave, a
/// reaction), not about the size of the pool it runs in; so a fixture is
/// <see cref="Grown"/> back to <see cref="FixtureHp"/>/<see cref="FixtureMana"/>
/// and every shipped number stands unchanged.
/// </para>
/// <para>
/// <strong>Grown by crediting, never by assignment.</strong> The pool is its raw
/// XP and nothing else, so a fixture handed a maximum directly would sit in a
/// state no play could reach. <see cref="Grown"/> credits one threshold at a
/// time through the same <see cref="PartyMemberState.Credit"/> the appliers use,
/// which leaves the member's XP, points and maxima consistent with each other.
/// </para>
/// <para>
/// <strong>Every file's fixture comes through here</strong> — including the
/// files that assert only enemy numbers. A member left at 25/25 changes what a
/// scenario does without failing any assertion that names a pool, so
/// <c>ProgressionStateTests.EveryFixtureMemberIsGrown</c> reads the test
/// assembly's IL and refuses a <see cref="PartyMemberState"/> built anywhere but
/// here. A member who genuinely needs the real starting pool asks for
/// <see cref="Fresh"/> and says so.
/// </para>
/// </summary>
public static class TestPools
{
    /// <summary>The HP a fixture member carries: the flat pool every scenario in this suite was written against.</summary>
    public const int FixtureHp = 100;

    /// <summary>The mana a fixture member carries, for the same reason. An enemy's pool is <see cref="GameConstants.MaxMana"/> and is not this number by anything but coincidence.</summary>
    public const int FixtureMana = 100;

    /// <summary>
    /// A fixture member: no innate nature, both pools grown to the suite's
    /// numbers, nothing equipped. The caller fills the inventory and whatever
    /// else its scenario needs.
    /// </summary>
    public static PartyMemberState Char(string id, int colorIndex = 0, float x = 0f, float y = 0f)
        => Fresh(id, InnateStats.None, colorIndex, x, y).Grown();

    /// <summary>
    /// A member as the game makes one: a spread and nothing earned, so both
    /// pools read <see cref="Tuning.StartingPool"/>. For tests that are about
    /// progression itself; a combat fixture wants <see cref="Char"/>.
    /// </summary>
    public static PartyMemberState Fresh(string id, InnateStats stats, int colorIndex = 0, float x = 0f, float y = 0f)
        => new() { Id = id, ColorIndex = colorIndex, Stats = stats, X = x, Y = y };

    /// <summary>
    /// Credit <paramref name="member"/> until its maxima are
    /// <paramref name="maxHp"/> and <paramref name="maxMana"/>, one threshold at
    /// a time. At <see cref="InnateStats.None"/> a point costs the whole bar, so
    /// a grown fixture's next point costs 100 of whatever feeds it and no
    /// scenario in this suite moves a maximum mid-run.
    /// </summary>
    /// <returns>The same member, so a helper can build and grow in one expression.</returns>
    public static PartyMemberState Grown(this PartyMemberState member, int maxHp = FixtureHp, int maxMana = FixtureMana)
    {
        ArgumentNullException.ThrowIfNull(member);
        RequireReachable(maxHp, member.MaxHp, nameof(maxHp));
        RequireReachable(maxMana, member.MaxMana, nameof(maxMana));

        // Exactly one threshold buys exactly one point, so each loop lands on its
        // target rather than stepping over it.
        while (member.MaxHp < maxHp)
            member.Credit(new XpCredit(XpPool.Health, null, member.HealthPool.XpToNext));
        while (member.MaxMana < maxMana)
            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));

        return member;
    }

    private static void RequireReachable(int wanted, int start, string name)
    {
        if (wanted < start)
            throw new ArgumentOutOfRangeException(name, wanted, $"A pool is only ever credited: it opens at {start} and never falls to {wanted}.");
    }
}
