namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// How a run's end arrives (§4.4): the party walked out by the first floor's
/// way out, or every member fell. The two are the only doors — there is no
/// shortcut exit (§4.3) — and <see cref="CampaignState.EndRun"/> reads which one
/// with whether the boss is down to name the <see cref="RunOutcome"/>.
/// </summary>
public enum RunEnding
{
    /// <summary>The party left by the first floor's up-stairs, gathered, in its own phase.</summary>
    Exited,

    /// <summary>Every member fell (<see cref="TurnSystem.GameOver"/>, raised on to the campaign as <see cref="DungeonState.Wiped"/>).</summary>
    Wiped,
}

/// <summary>
/// The three ways a run ends (§4.4, "three outcomes, not two"), in the doc's
/// table's order. Only one of them should feel like nothing happened, and
/// retreat and death share the mercy tick without sharing an XP outcome.
/// </summary>
public enum RunOutcome
{
    /// <summary>Out alive with the boss dead: the loot collected on the way out is kept, the XP is kept, and the run ticks once as a success.</summary>
    BossKill,

    /// <summary>Out alive with the boss standing: nothing dropped to keep, the XP kept, and a mercy tick only for having got three floors down.</summary>
    Retreat,

    /// <summary>The party wiped: the loot is lost, a share of the run's XP gain is clawed back, and the run ticks as its depth and the boss earned.</summary>
    Death,
}

/// <summary>
/// One member's XP pools as they stood when the party stepped into the dungeon
/// (§4.4): weapon proficiency in every class, HP growth and mana growth — the
/// three pools that grow in play. Innate stats never move (§2.1), so there is
/// nothing of them here to roll back.
/// </summary>
/// <param name="MemberId">The member the pools are, by the roster's stable id.</param>
/// <param name="WeaponXp">Cumulative weapon XP per class, one entry for each <see cref="WeaponClass"/>, in enum order.</param>
/// <param name="HpXp">Cumulative HP XP (<see cref="PartyMemberState.HpXp"/>).</param>
/// <param name="ManaXp">Cumulative mana XP (<see cref="PartyMemberState.ManaXp"/>).</param>
public sealed record MemberXp(string MemberId, IReadOnlyList<int> WeaponXp, int HpXp, int ManaXp);

/// <summary>
/// The party's XP at the moment it stepped into the dungeon (§4.4:
/// "Party XP pools ... are captured the moment the party steps into the
/// dungeon"), held by the visit (<see cref="DungeonState.EntrySnapshot"/>) —
/// the object §5.1's Run group is — and discarded with it. A wipe rolls each
/// pool back by <see cref="Tuning.DeathXpLossPercent"/> of its gain since this
/// (<see cref="ClawBack"/>); any other ending leaves it untouched and it goes
/// with the visit.
/// <para>
/// A copy of plain integers, never a reference to a live pool — which is what
/// §2.2's pools being cumulative numbers bought — so nothing a member does in
/// the run can move it.
/// </para>
/// </summary>
/// <param name="Members">Each member's pools, in roster order.</param>
public sealed record XpSnapshot(IReadOnlyList<MemberXp> Members)
{
    private static readonly WeaponClass[] Classes = Enum.GetValues<WeaponClass>();

    /// <summary>The name <see cref="XpClawback.Pool"/> gives the HP pool; a weapon class's pool is named by its class.</summary>
    public const string HpPool = "HP";

    /// <summary>The name <see cref="XpClawback.Pool"/> gives the mana pool.</summary>
    public const string ManaPool = "Mana";

    /// <summary>Each member of <paramref name="party"/>'s three pools as they stand now: every class's weapon XP, HP XP, mana XP.</summary>
    public static XpSnapshot Of(IReadOnlyList<PartyMemberState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new XpSnapshot(party
            .Select(member => new MemberXp(member.Id, Array.AsReadOnly(Classes.Select(cls => member.WeaponXp[cls]).ToArray()),
                member.HpXp, member.ManaXp))
            .ToArray());
    }

    /// <summary>
    /// A wipe's clawback (§4.4): every pool of every member loses
    /// <paramref name="lossPercent"/> of what it gained since this snapshot —
    /// <c>gain × lossPercent / 100</c>, truncated, so it never takes more than
    /// the share, and never anything a pool held before the run. At 100 the run's
    /// progress is gone; at 0 nothing is taken. Weapon XP per class, then HP,
    /// then mana, member by member in roster order.
    /// </summary>
    /// <param name="party">The members, each matched to its snapshot by id; a member the snapshot does not hold is left alone.</param>
    /// <param name="lossPercent">The share of each gain taken back, 0 to 100 (<see cref="Tuning.DeathXpLossPercent"/>).</param>
    /// <returns>Every pool that lost anything, in the order above: what it held, and what it holds now.</returns>
    internal IReadOnlyList<XpClawback> ClawBack(IReadOnlyList<PartyMemberState> party, int lossPercent)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentOutOfRangeException.ThrowIfNegative(lossPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lossPercent, 100);

        var clawbacks = new List<XpClawback>();
        foreach (var member in party)
        {
            var entry = Members.FirstOrDefault(m => m.MemberId == member.Id);
            if (entry is null) continue;

            for (int i = 0; i < Classes.Length; i++)
            {
                var cls = Classes[i];
                int now = member.WeaponXp[cls];
                int after = Claw(now, entry.WeaponXp[i], lossPercent);
                if (after == now) continue;
                member.WeaponXp[cls] = after;
                clawbacks.Add(new XpClawback(member.Id, cls.ToString(), now, after));
            }

            int hp = member.HpXp, hpAfter = Claw(hp, entry.HpXp, lossPercent);
            if (hpAfter != hp)
            {
                member.HpXp = hpAfter;
                clawbacks.Add(new XpClawback(member.Id, HpPool, hp, hpAfter));
            }

            int mana = member.ManaXp, manaAfter = Claw(mana, entry.ManaXp, lossPercent);
            if (manaAfter != mana)
            {
                member.ManaXp = manaAfter;
                clawbacks.Add(new XpClawback(member.Id, ManaPool, mana, manaAfter));
            }
        }
        return clawbacks;
    }

    /// <summary>What a pool holding <paramref name="now"/> keeps: the share of its gain since <paramref name="then"/> is taken, truncated. A pool that has not grown loses nothing.</summary>
    private static int Claw(int now, int then, int lossPercent)
    {
        int gain = Math.Max(0, now - then);
        return now - gain * lossPercent / 100;
    }
}

/// <summary>What a wipe took back from one pool (§4.4): the member, the pool, what it held and what it holds now.</summary>
/// <param name="MemberId">The member, by roster id.</param>
/// <param name="Pool">
/// The pool: a weapon class by its <see cref="WeaponClass"/> name (<c>Dagger</c>),
/// or <see cref="XpSnapshot.HpPool"/> or <see cref="XpSnapshot.ManaPool"/>.
/// </param>
/// <param name="Before">The pool's cumulative XP at the wipe.</param>
/// <param name="After">What it holds after the clawback: never below what it held at entry.</param>
public sealed record XpClawback(string MemberId, string Pool, int Before, int After);

/// <summary>
/// How a run ended, and everything it carried out to the campaign (§4.4, §4.5,
/// §6.3), what the enchanter did on its end among it (§6.2): what
/// <see cref="CampaignState.EndRun"/> returns and
/// <see cref="CampaignState.RunEnded"/> raises, and what the run's summary
/// reads. Nothing in it is dungeon state — the visit is gone by the time
/// anyone reads it — except the one number §4.3 lets out once it is known,
/// the depth, when the party reached it.
/// </summary>
/// <param name="DungeonId">The dungeon the run was in.</param>
/// <param name="Outcome">Which of the three outcomes it was.</param>
/// <param name="BossDefeated">Whether the boss fell — on a death too: killing it and dying on the way out is a death that still counts the clear.</param>
/// <param name="DeepestFloor">The deepest floor the party stood on, a 1-based floor number.</param>
/// <param name="BossFloorRevealed">The boss floor — the dungeon's depth — only if the party reached it (§4.3: learned by reaching it, never told); null otherwise.</param>
/// <param name="ServiceTick">Whether the run counted (§6.3): a boss kill's success tick, or a failure's mercy tick for having got <see cref="Tuning.MercyFloor"/> floors down. Entering counts nothing.</param>
/// <param name="MercyTick">Whether the tick was the mercy one: a failure deep enough to count, never a win.</param>
/// <param name="RunCounter">The campaign's run counter after this run's tick.</param>
/// <param name="LockedUntilRun">The lock the clear wrote (<c>RunCounter + LockRuns</c>), or null: nothing was cleared, or the dungeon retired instead.</param>
/// <param name="Retired">Whether the clear retired the dungeon for good (the tutorial, §4.3).</param>
/// <param name="SpoilsKept">This run's pickups carried home: the ones still in a bag on the way out.</param>
/// <param name="SpoilsLost">This run's pickups a wipe took out of the bags.</param>
/// <param name="Clawbacks">What a wipe took back from each pool; empty on any other ending.</param>
/// <param name="Services">
/// What the run's end did at the enchanter (§6.2, §6.3; Phase 6a.3a): a report for
/// every deposit that was in the shop as the run ended, in deposit order — each
/// worked a run on, the ones that finished among them, or left stalled — or null
/// when the shop was empty, which is what a save writes and reads back. Appended,
/// with its default, so a result built without it reads as one with an empty shop.
/// </param>
public sealed record RunResult(string DungeonId, RunOutcome Outcome, bool BossDefeated, int DeepestFloor, int? BossFloorRevealed,
    bool ServiceTick, bool MercyTick, int RunCounter, int? LockedUntilRun, bool Retired,
    IReadOnlyList<Weapon> SpoilsKept, IReadOnlyList<Weapon> SpoilsLost, IReadOnlyList<XpClawback> Clawbacks,
    IReadOnlyList<ServiceReport>? Services = null);

/// <summary>
/// What one run's end did with one deposit at the enchanter (§6.2, §6.3; Phase
/// 6a.3a): the weapon by its catalogue id, the service, how many of its runs are
/// worked and how many it takes, and the enchantment an Enchant attaches. A run
/// that counted <em>worked</em> the newest few a run on
/// (<see cref="Tuning.EnchanterWorksOn"/>) — <c>1 ≤ RunsWorked ≤ Runs</c>, the
/// service finished when they meet, its weapon back in the stable — and left the
/// rest <see cref="Stalled"/>; a run that did not count worked nothing, and
/// reports every entry stalled with its runs as they were, a fresh deposit's at 0.
/// So a stalled report has <c>0 ≤ RunsWorked ≤ Runs − 1</c>. The summary reads
/// these, after a load too, since the save keeps them with the result. Nothing
/// ties a report to a deposit in the shop: it outlives the entry it speaks of.
/// </summary>
/// <param name="WeaponId">The weapon serviced, by its catalogue id: what the summary names it by.</param>
/// <param name="Service">What the deposit bought.</param>
/// <param name="RunsWorked">The runs of its service worked by the end of this run.</param>
/// <param name="Runs">The runs its service takes, fixed at deposit.</param>
/// <param name="Stalled">Whether this run left it unworked: an older deposit behind the newest, or any deposit on a run that did not count.</param>
/// <param name="EnchantmentId">The enchantment an Enchant attaches (on finishing, attached), or null for a refinement.</param>
public sealed record ServiceReport(string WeaponId, Service Service, int RunsWorked, int Runs, bool Stalled = false, string? EnchantmentId = null)
{
    /// <summary>Whether this run finished the service: worked, its last run done, its weapon back in the stable.</summary>
    public bool Finished => !Stalled && RunsWorked == Runs;
}
