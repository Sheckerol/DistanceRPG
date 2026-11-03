using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The one place a test builds a party member, and the one place a fixture's
/// pools are filled.
/// <para>
/// A member's maxima are earned now (§2.1): both pools open at
/// <see cref="Tuning.StartingPool"/> — 30 — and rise a point at a time. Every
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
/// files that assert only enemy numbers. A member left at 30/30 changes what a
/// scenario does without failing any assertion that names a pool, so
/// <c>ProgressionStateTests.EveryFixtureMemberIsGrown</c> reads the test
/// assembly's IL and refuses a <see cref="PartyMemberState"/> built anywhere but
/// here. A member who genuinely needs the real starting pool asks for
/// <see cref="Fresh"/> and says so -- and because that is the one door back to
/// 30/30, the same guard enumerates <see cref="Fresh"/>'s callers as well and
/// allows only the progression tests, which are what it is for.
/// </para>
/// </summary>
public static class TestPools
{
    /// <summary>The HP a fixture member carries: the flat pool every scenario in this suite was written against.</summary>
    public const int FixtureHp = 100;

    /// <summary>The mana a fixture member carries, for the same reason. An enemy's pool is <see cref="GameConstants.MaxMana"/> and is not this number by anything but coincidence.</summary>
    public const int FixtureMana = 100;

    /// <summary>
    /// A fixture member: no innate nature unless <paramref name="stats"/> names
    /// one, both pools grown to the suite's numbers, nothing equipped. The caller
    /// fills the inventory and whatever else its scenario needs.
    /// <para>
    /// A spread is worth asking for only where the scenario is about the divisor
    /// — the enchantment ladder's INT, say — and it changes no maximum here: a
    /// member is grown to <see cref="FixtureHp"/>/<see cref="FixtureMana"/> either
    /// way, a wizard simply reaching them for less XP.
    /// </para>
    /// </summary>
    public static PartyMemberState Char(string id, int colorIndex = 0, float x = 0f, float y = 0f, InnateStats? stats = null)
        => Fresh(id, stats ?? InnateStats.None, colorIndex, x, y).Grown();

    /// <summary>
    /// A fixture member with <paramref name="weapon"/> already in hand, grown
    /// until its <em>spendable</em> pool is <see cref="FixtureMana"/>: what
    /// every per-file <c>Char(id, r, c, weaponId)</c> helper builds.
    /// <para>
    /// <strong>Equipped first, then grown, and grown against
    /// <see cref="ActorState.UsableMaxMana"/>.</strong> An enchantment reserves
    /// max mana while it is equipped (§3.3), so a member handed the suite's 100
    /// and then a Nameless Knife would have 100 earned and 0 spendable, and
    /// every shipped trigger number in the file holding it would have to be
    /// re-derived against a pool the scenario was never about. Growing past the
    /// locks keeps <see cref="FixtureMana"/> meaning what it has always meant:
    /// the mana this fixture can actually spend.
    /// </para>
    /// <para>
    /// The extra points are credited exactly as <see cref="Grown"/> credits the
    /// first hundred — one threshold at a time, through
    /// <see cref="PartyMemberState.Credit"/> — so the member stays in a state
    /// play could reach. The loop reads the spendable pool afresh each turn
    /// because it is not monotonic in the earned one: the point that finally
    /// covers a sleeping entry's lock wakes it and takes the whole lock at once.
    /// </para>
    /// </summary>
    public static PartyMemberState Holding(string id, Weapon? weapon, int colorIndex = 0, float x = 0f, float y = 0f, InnateStats? stats = null)
    {
        var member = Fresh(id, stats ?? InnateStats.None, colorIndex, x, y);
        member.Inventory[0] = weapon;
        member.Grown();

        while (member.UsableMaxMana < FixtureMana)
            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));
        member.Mana = member.UsableMaxMana;

        return member;
    }

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

        // A gained mana point arrives empty on purpose -- crediting on a spend
        // must not refund the cast -- so the pool a fixture spawns holding is
        // filled here, to the spendable ceiling rather than the earned one
        // (section 3.3: nobody ever holds mana the equipped locks reserved).
        // HP needs no line: its points arrive filled.
        member.Mana = member.UsableMaxMana;

        return member;
    }

    private static void RequireReachable(int wanted, int start, string name)
    {
        if (wanted < start)
            throw new ArgumentOutOfRangeException(name, wanted, $"A pool is only ever credited: it opens at {start} and never falls to {wanted}.");
    }
}
