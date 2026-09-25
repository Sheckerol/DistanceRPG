namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A save's fields asked of each other (the user's ruling of 2026-09-25, R3(b):
/// "since we are storing in raw json we should probably not trust the fields").
/// <see cref="SaveMapper"/> reads every field on its own first — each id the
/// content's, each count from its floor, and up to its ceiling where a larger one
/// would wrap the arithmetic that reads it — and only then hands the
/// document here, so every refusal of a single field keeps its own path. What is
/// left is a save each of whose fields could be true while together they could
/// not: a dungeon retired that was never cleared, a last run's summary that
/// disagrees with the campaign it ended, a visit into a dungeon the hub could not
/// have offered, a boss standing that the visit has down. No play writes one, so
/// the save is refused whole as one that cannot be read — set aside, never
/// deleted, the backup tried — and never restored into a state the rules below
/// assume cannot happen.
/// <para>
/// Pure over the document: no content and no tuning is read, so no tuning in
/// range and no content that loads refuses a save play wrote. Each check holds
/// by construction in play — the walk behind each is on the check — and on both
/// saves the pre-rework code wrote. The checks run in the order below, and each
/// is a <see cref="SaveFormatException"/> naming the field and, in its first
/// clause, what contradicts it — the notice of a save set aside shows that clause
/// (<c>CampaignBoot.WhyUnreadable</c>), so every problem is in the HUD's font, an
/// id named bare: <c>campaign.retired[0]: tutorial is retired and was never
/// cleared</c>.
/// </para>
/// <para>
/// <strong>What is not refused, and why</strong> (settled.md, R3(b)): a lock at or
/// below the run counter — a lock that has lifted stays in the campaign
/// (<see cref="CampaignState.LockedInOrder"/>) and the early offer writes the
/// counter itself, so that is play's everyday state; a lock on a retired dungeon,
/// which play writes when <c>dungeons.json</c> moves <c>retiresOnClear</c> onto a
/// dungeon already locked; hit points or mana over a maximum the load derives
/// from today's tuning and content; a soul's tier, which its pin plays at 1; and
/// the counts no clamp or rule gives an honest ceiling short of the largest whole
/// number.
/// </para>
/// </summary>
internal static class SaveConsistency
{
    private const string At = "campaign";

    /// <summary>
    /// Refuse <paramref name="campaign"/> if two of its fields contradict each
    /// other — a document <see cref="SaveMapper.Restore"/> has already read field
    /// by field, so every list is there, every id resolves and is listed once, and
    /// every name is its enum's.
    /// </summary>
    /// <exception cref="SaveFormatException">The first contradiction, in the order the checks are listed, and where.</exception>
    internal static void Check(CampaignDto campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var cleared = new HashSet<string>(campaign.Cleared, StringComparer.Ordinal);
        var retired = new HashSet<string>(campaign.Retired, StringComparer.Ordinal);
        var locks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in campaign.Locks)
            locks[entry.DungeonId] = entry.LockedUntilRun;

        // 1. A dungeon retires on its clear, which records it cleared first.
        for (int i = 0; i < campaign.Retired.Count; i++)
            if (!cleared.Contains(campaign.Retired[i]))
                throw new SaveFormatException($"{At}.retired[{i}]",
                    $"{campaign.Retired[i]} is retired and was never cleared: a dungeon retires on its clear, which records it cleared");

        // 2. Only a clear writes a lock; the early offer rewrites one a clear wrote.
        for (int i = 0; i < campaign.Locks.Count; i++)
            if (!cleared.Contains(campaign.Locks[i].DungeonId))
                throw new SaveFormatException($"{At}.locks[{i}].dungeonId",
                    $"{campaign.Locks[i].DungeonId} is locked and was never cleared: only a clear writes a lock");

        // (3, a lock on a retired dungeon, is withdrawn: play writes it — see the class.)

        if (campaign.LastResult is { } last)
            LastRun(campaign, last, cleared, retired, locks);
        else
            NoRunEnded(campaign);

        // 14. A member is coloured by its place in the roster when the campaign began.
        var colours = new Dictionary<int, string>();
        for (int i = 0; i < campaign.Party.Count; i++)
        {
            var member = campaign.Party[i];
            if (!colours.TryAdd(member.ColorIndex, member.Id))
                throw new SaveFormatException($"{At}.party[{i}].colorIndex",
                    $"{member.ColorIndex} is already the colour of member {colours[member.ColorIndex]}: a member is coloured by its place in the roster, one colour each");
        }

        if (campaign.Visit is { } visit)
            Visit(campaign, visit, retired, locks);
    }

    /// <summary>4. No run has ended: only a run's end moves the counter or clears a dungeon, and it always writes the summary.</summary>
    private static void NoRunEnded(CampaignDto campaign)
    {
        if (campaign.RunCounter != 0)
            throw new SaveFormatException($"{At}.runCounter",
                $"the counter stands at {campaign.RunCounter} and no run has ended: only the end of a run moves the counter, and it writes the last result");
        if (campaign.Cleared.Count > 0)
            throw new SaveFormatException($"{At}.cleared[0]",
                $"{campaign.Cleared[0]} is cleared and no run has ended: only the end of a run clears a dungeon, and it writes the last result");
    }

    /// <summary>
    /// 5–13. The last run's summary, against itself and against the campaign it
    /// ended: <see cref="CampaignState.EndRun"/> writes both at once, and nothing
    /// but the early offer moves either before the next run's end.
    /// </summary>
    private static void LastRun(CampaignDto campaign, RunResultDto last, HashSet<string> cleared, HashSet<string> retired, Dictionary<string, int> locks)
    {
        const string at = $"{At}.lastResult";
        bool wiped = last.Outcome == nameof(RunOutcome.Death);

        // 5. The outcome: a wipe is a death; walking out, a boss kill exactly when the boss is down.
        if (last.Outcome == nameof(RunOutcome.BossKill) && !last.BossDefeated)
            throw new SaveFormatException($"{at}.outcome",
                "BossKill with the boss standing: a run is a boss kill only when the party walks out with the boss down");
        if (last.Outcome == nameof(RunOutcome.Retreat) && last.BossDefeated)
            throw new SaveFormatException($"{at}.outcome",
                "Retreat with the boss down: a party that walks out with the boss down has made a boss kill");

        // 6. The tick: a run counts for its boss, or for the mercy of a failure deep enough — never both.
        if (last.ServiceTick != (last.BossDefeated || last.MercyTick))
            throw new SaveFormatException($"{at}.serviceTick", last.ServiceTick
                ? "counted with the boss standing and no mercy earned: a run counts for its boss or for its mercy, and for nothing else"
                : last.BossDefeated
                    ? "not counted with the boss down: a run that downs its boss always counts"
                    : "not counted with a mercy tick: the mercy tick is a count");
        if (last.MercyTick && last.BossDefeated)
            throw new SaveFormatException($"{at}.mercyTick",
                "a mercy tick with the boss down: the mercy is for a run that failed deep, never for a win");

        // 7. A clear locks its dungeon or retires it, never both; a failure does neither; a lock is the counter plus the runs a floor.
        bool locked = last.LockedUntilRun is not null;
        if (last.BossDefeated && !locked && !last.Retired)
            throw new SaveFormatException($"{at}.lockedUntilRun",
                "no lock and not retired, with the boss down: a clear locks its dungeon, or retires it");
        if (last.BossDefeated && locked && last.Retired)
            throw new SaveFormatException($"{at}.retired",
                "retired and locked both: a clear retires a dungeon in place of locking it");
        if (!last.BossDefeated && last.Retired)
            throw new SaveFormatException($"{at}.retired",
                "retired with the boss standing: only a clear retires a dungeon");
        if (!last.BossDefeated && locked)
            throw new SaveFormatException($"{at}.lockedUntilRun",
                $"{last.LockedUntilRun} with the boss standing: only a clear locks a dungeon");
        if (last.LockedUntilRun is { } written && written < last.RunCounter)
            throw new SaveFormatException($"{at}.lockedUntilRun",
                $"{written} is below the run counter, {last.RunCounter}: a clear locks its dungeon from the counter on, never before it");

        // 8. The boss floor is told only on reaching it, and it is the bottom: nothing lies below it.
        if (last.BossFloorRevealed is { } revealed && revealed != last.DeepestFloor)
            throw new SaveFormatException($"{at}.bossFloorRevealed",
                $"{revealed} is not the deepest floor the party reached, {last.DeepestFloor}: the boss floor is told only on reaching it, and nothing lies below it");
        if (last.BossFloorRevealed is null && last.BossDefeated)
            throw new SaveFormatException($"{at}.bossFloorRevealed",
                "not revealed with the boss down: the boss is fought on its own floor, so a party that downed it reached it");
        if (last.DeepestFloor > ContentBounds.DeepestBossFloor)
            throw new SaveFormatException($"{at}.deepestFloor",
                $"{last.DeepestFloor} is above {ContentBounds.DeepestBossFloor}: no dungeon goes deeper than the deepest floor a boss waits on (BossFloorMax at its most)");

        // 9. Walking out keeps the spoils in the bags; a wipe loses them and claws XP back.
        if (!wiped && last.SpoilsLost.Count > 0)
            throw new SaveFormatException($"{at}.spoilsLost",
                $"{last.SpoilsLost.Count} lost on a run that did not wipe: only a wipe takes spoils out of the bags");
        if (!wiped && last.Clawbacks.Count > 0)
            throw new SaveFormatException($"{at}.clawbacks",
                $"{last.Clawbacks.Count} clawed back on a run that did not wipe: only a wipe claws XP back");
        if (wiped && last.SpoilsKept.Count > 0)
            throw new SaveFormatException($"{at}.spoilsKept",
                $"{last.SpoilsKept.Count} kept on a run that wiped: a wipe loses every spoil still in a bag");

        // 10. A claw-back takes back, once a pool.
        var pools = new HashSet<(string Member, string Pool)>();
        for (int i = 0; i < last.Clawbacks.Count; i++)
        {
            var clawback = last.Clawbacks[i];
            if (clawback.After > clawback.Before)
                throw new SaveFormatException($"{at}.clawbacks[{i}].after",
                    $"{clawback.After} is above what the pool held, {clawback.Before}: a claw-back takes XP back, and never hands it out");
            if (!pools.Add((clawback.MemberId, clawback.Pool)))
                throw new SaveFormatException($"{at}.clawbacks[{i}]",
                    $"the {clawback.Pool} pool of {clawback.MemberId} is clawed back twice: a wipe claws each pool back once");
        }

        // 11. The counter moves only at a run's end, which writes it to both.
        if (last.RunCounter != campaign.RunCounter)
            throw new SaveFormatException($"{at}.runCounter",
                $"{last.RunCounter} where the campaign has counted {campaign.RunCounter}: the counter moves only at the end of a run, which writes it here too");

        // 12. A clear writes one lock to both. Only the early offer moves it before
        // the next run's end — to the counter, as it enters the dungeon — and a run
        // that locks nothing writes none here while an earlier lock stays in the
        // campaign, so a lock left unset is never compared.
        if (last.LockedUntilRun is { } lockWritten)
        {
            bool held = locks.TryGetValue(last.DungeonId, out int campaignLock);
            bool earlyOffer = held && campaignLock == campaign.RunCounter
                && string.Equals(campaign.Visit?.DungeonId, last.DungeonId, StringComparison.Ordinal);
            if (!held)
                throw new SaveFormatException($"{at}.lockedUntilRun",
                    $"{lockWritten} and the campaign holds no lock on {last.DungeonId}: a clear writes one lock to both");
            if (campaignLock != lockWritten && !earlyOffer)
                throw new SaveFormatException($"{at}.lockedUntilRun",
                    $"{lockWritten} where the campaign locks {last.DungeonId} until run {campaignLock}: a clear writes one lock to both, and only the early offer moves it after");
        }

        // 13. A clear records its dungeon cleared, and a retiring clear retired.
        if (last.BossDefeated && !cleared.Contains(last.DungeonId))
            throw new SaveFormatException($"{at}.dungeonId",
                $"{last.DungeonId} is not cleared and its last run downed the boss: a clear records its dungeon cleared");
        if (last.Retired && !retired.Contains(last.DungeonId))
            throw new SaveFormatException($"{at}.dungeonId",
                $"{last.DungeonId} is not retired and its last run retired it: a retiring clear records it retired");
    }

    /// <summary>
    /// 15–19. The visit in progress, against the campaign that entered it and
    /// against itself: entered as <see cref="CampaignState.Enter"/> enters, played
    /// as a visit plays — XP only gained, the boss down for good once it falls, a
    /// drop handed over only then.
    /// </summary>
    private static void Visit(CampaignDto campaign, VisitDto visit, HashSet<string> retired, Dictionary<string, int> locks)
    {
        const string at = $"{At}.visit";

        // 15. Entering refuses a retired dungeon, and a locked one bar the early offer, which lifts its lock to the counter.
        if (retired.Contains(visit.DungeonId))
            throw new SaveFormatException($"{at}.dungeonId",
                $"{visit.DungeonId} is retired: a retired dungeon is gone for good, and no run enters it");
        if (locks.TryGetValue(visit.DungeonId, out int until) && until > campaign.RunCounter)
            throw new SaveFormatException($"{at}.dungeonId",
                $"{visit.DungeonId} is locked until run {until} and the counter stands at {campaign.RunCounter}: a locked dungeon is entered only as the early offer, which lifts its lock to the counter");

        // 16. A visit only adds XP; the wipe claws back only as the run ends, with the
        // visit gone. The walk has matched the snapshot to the party in roster order.
        var door = visit.EntrySnapshot.Members;
        for (int i = 0; i < door.Count && i < campaign.Party.Count; i++)
        {
            var member = campaign.Party[i];
            string where = $"{at}.entrySnapshot.members[{i}]";
            foreach (var (name, xp) in door[i].WeaponXp)
                NoMoreThanNow(xp, member.WeaponXp.TryGetValue(name, out int now) ? now : 0, member.Id, $"{where}.weaponXp.{name}");
            NoMoreThanNow(door[i].HpXp, member.HpXp, member.Id, $"{where}.hpXp");
            NoMoreThanNow(door[i].ManaXp, member.ManaXp, member.Id, $"{where}.manaXp");
        }

        // 17. The promise is a stack a floor past the offset, and the offset is never negative.
        if (visit.PromisedStacks > visit.BossFloor)
            throw new SaveFormatException($"{at}.promisedStacks",
                $"{visit.PromisedStacks} is above the boss floor, {visit.BossFloor}: a boss drop is promised a stack a floor at most");

        // 18. The boss's death is what records it down, and a boss never rises: when
        // the bottom floor is saved, its boss row stands exactly while the visit says so.
        int bottom = visit.BossFloor - 1;
        if (bottom < visit.Floors.Count)
        {
            var rows = visit.Floors[bottom].Enemies;
            for (int k = 0; k < rows.Count; k++)
                if (rows[k].Boss && rows[k].Body.Alive == visit.BossDefeated)
                    throw new SaveFormatException($"{at}.floors[{bottom}].enemies[{k}].body.alive", visit.BossDefeated
                        ? "the boss stands and the visit has it down: its death is what records it down, and a boss never rises"
                        : "the boss has fallen and the visit has it standing: its fall records it down at once");
        }

        // 19. A drop lies only on a corpse once the boss is down, and nothing rises after.
        for (int f = 0; f < visit.Floors.Count; f++)
        {
            var rows = visit.Floors[f].Enemies;
            for (int k = 0; k < rows.Count; k++)
            {
                if (!rows[k].DropTaken)
                    continue;
                string where = $"{at}.floors[{f}].enemies[{k}].dropTaken";
                if (!visit.BossDefeated)
                    throw new SaveFormatException(where, "taken with the boss standing: nothing is handed over while the dungeon resurrects");
                if (rows[k].Body.Alive)
                    throw new SaveFormatException(where, "taken from a standing row: a drop lies only on a corpse, and nothing rises once the boss is down");
            }
        }
    }

    /// <summary>16's one question: what a pool held at the door is no more than what it holds now.</summary>
    private static void NoMoreThanNow(int atDoor, int now, string member, string where)
    {
        if (atDoor > now)
            throw new SaveFormatException(where,
                $"{atDoor} is more than member {member} holds now, {now}: a visit only adds XP, and a wipe claws it back only as the run ends");
    }
}
