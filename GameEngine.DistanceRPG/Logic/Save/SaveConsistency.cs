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
/// from today's tuning and content, and a weapon's wear over the capacity today's
/// tuning replays for it (held instead to a ceiling no tuning in range can pass,
/// <see cref="SaveMapper"/>); a soul's tier, which its pin plays at 1; and
/// the counts no clamp or rule gives an honest ceiling, which
/// <see cref="SaveMapper"/> holds only to the sanity bound past which the next
/// step of play would wrap (<see cref="ContentBounds.MostCount"/>). And of the
/// enchanter's queue (Phase 6a.3a): an Enchant's deposit below today's price or
/// above today's capacity, both today's tuning; a queued enchantment today's
/// <c>restricted.json</c> would refuse or lists among the never-rolled — the
/// deposit was legal when it was made, and the file beside the game is
/// JSON-master data an edit may move since; which dungeon is retired when
/// something is at the enchanter — check 30 asks that one is, never that it is
/// the one today's <c>dungeons.json</c> retires on its clear, which an edit may
/// move; and a run's end's report against the deposits still in the shop, which
/// a report outlives.
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

        Shop(campaign);
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
    /// 15–20. The visit in progress, against the campaign that entered it and
    /// against itself: entered as <see cref="CampaignState.Enter"/> enters, played
    /// as a visit plays — XP only gained, the boss down for good once it falls, a
    /// drop handed over only then, and no weapon worn that no member wielded.
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

        // 20. No enemy's weapon wears (an enemy's wear feed does nothing), and a
        // drop is made when it is rolled: neither has banked or cashed any wear
        // (Phase 6a.1). A weapon the party put down, and a spoil, are the party's,
        // and carry their wear.
        for (int f = 0; f < visit.Floors.Count; f++)
        {
            var rows = visit.Floors[f].Enemies;
            for (int k = 0; k < rows.Count; k++)
            {
                string where = $"{at}.floors[{f}].enemies[{k}]";
                Unworn(rows[k].Weapon, $"{where}.weapon", "no enemy weapon is ever one the party wields, so it never wears");
                if (rows[k].GroundItem is { } lying)
                    Unworn(lying.Weapon, $"{where}.groundItem.weapon", "a drop is made when it is rolled, and nothing has wielded it since");
            }
        }
    }

    /// <summary>
    /// 21–25, 27–30. The enchanter (Phase 6a.3a): the shop, as the hub's deposits
    /// fill it and every run's end works it, and what the last run's end said of
    /// it. A deposit takes the next ordinal and cashes the whole pool, which a
    /// take-back gives back; its runs are the count plus one; the tick that works
    /// its last run brings it back; an Enchant buys an enchantment the campaign has
    /// met, which the weapon has room for; a run that counts works the newest, and
    /// one that did not works nothing; and nothing is deposited before the
    /// tutorial is beaten.
    /// </summary>
    private static void Shop(CampaignDto campaign)
    {
        const string at = $"{At}.enchanter";
        var queue = campaign.Enchanter ?? [];
        var seen = new HashSet<string>(campaign.Seen, StringComparer.Ordinal);

        // 21. A deposit takes the next ordinal, and the count moves past it: the shop
        // is in deposit order, each ordinal its own, each below the count.
        for (int i = 0; i < queue.Count; i++)
        {
            int ordinal = queue[i].Ordinal;
            if (i > 0 && ordinal <= queue[i - 1].Ordinal)
                throw new SaveFormatException($"{at}[{i}].ordinal",
                    $"{ordinal} follows {queue[i - 1].Ordinal}: the shop is in deposit order, and each deposit takes the next ordinal, never one reused");
            if (ordinal >= campaign.DepositsMade)
                throw new SaveFormatException($"{at}[{i}].ordinal",
                    $"{ordinal} is not below the deposits made, {campaign.DepositsMade}: a deposit takes the next ordinal, and the count moves past it");
        }

        // 22. A deposit cashes the whole pool, and nothing wears a weapon in the shop.
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].Weapon.Wear != 0)
                throw new SaveFormatException($"{at}[{i}].weapon.wear",
                    $"{queue[i].Weapon.Wear} worn in the shop: the deposit cashed it all, and nothing wears a weapon there");

        // 23. The tick that works a service's last run brings its weapon back.
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].RunsWorked >= queue[i].Runs)
                throw new SaveFormatException($"{at}[{i}].runsWorked",
                    $"{queue[i].RunsWorked} of {queue[i].Runs} runs worked in the shop: the tick that works the last run brings the weapon back");

        // 24. A service takes the count plus one runs, fixed as it went in, and the
        // count cannot move while the weapon is in the shop.
        for (int i = 0; i < queue.Count; i++)
        {
            int count = queue[i].Weapon.Enchantments.Count;
            if (queue[i].Runs != count + 1)
                throw new SaveFormatException($"{at}[{i}].runs",
                    $"{queue[i].Runs} for a weapon holding {count} enchantments: a service takes the count plus one runs, and the count does not move in the shop");
        }

        // 25. An Enchant buys an enchantment the campaign has met; a refinement names none.
        for (int i = 0; i < queue.Count; i++)
        {
            var deposit = queue[i];
            bool enchant = deposit.Service == nameof(Service.Enchant);
            if (enchant && deposit.Enchantment is null)
                throw new SaveFormatException($"{at}[{i}].enchantment", "missing on an Enchant: a deposit to enchant names the enchantment it buys");
            if (enchant && !seen.Contains(deposit.Enchantment!))
                throw new SaveFormatException($"{at}[{i}].enchantment",
                    $"{deposit.Enchantment} is not among the seen: the enchanter offers only what the campaign has met");
            if (!enchant && deposit.Enchantment is not null)
                throw new SaveFormatException($"{at}[{i}].enchantment", $"{deposit.Enchantment} named on a {deposit.Service}: a refinement attaches nothing");
        }

        // (26, a transfer's source, arrives with the transfer.)

        // 27. What the last run's end said of the shop: a report for each deposit in
        // it as the run ended, in deposit order, each saying what its deposit bought
        // as the deposit does (25) — so no more reports than deposits were ever made
        // — and, on a service it brought back, what the improvement roll did to the
        // weapon, if anything (Phase 6a.4). A run that counted worked the newest few
        // a run on — the newest always, the enchanter working on one at least — and
        // left the older ones stalled, short of their last run; a run that did not
        // count worked nothing. Nothing ties a report to a deposit still in the shop:
        // a report outlives the entry it speaks of.
        if (campaign.LastResult is { Services: { } reports } last)
        {
            const string where = $"{At}.lastResult.services";
            int worked = 0;
            for (int i = 0; i < reports.Count; i++)
            {
                var report = reports[i];
                bool enchant = report.Service == nameof(Service.Enchant);
                if (enchant && report.Enchantment is null)
                    throw new SaveFormatException($"{where}[{i}].enchantment", "missing on an Enchant: a report says what its deposit bought, and a deposit to enchant names its enchantment");
                if (enchant && !seen.Contains(report.Enchantment!))
                    throw new SaveFormatException($"{where}[{i}].enchantment",
                        $"{report.Enchantment} is not among the seen: its deposit bought what the campaign had met, and the campaign forgets nothing it has met");
                if (!enchant && report.Enchantment is not null)
                    throw new SaveFormatException($"{where}[{i}].enchantment", $"{report.Enchantment} named on a {report.Service}: a refinement attaches nothing");

                // What the improvement roll did (Phase 6a.4): an outcome and the
                // modifier it gave a stack of, together, and only as the service
                // came back — the roll is made as its last run is worked.
                if ((report.Improved is null) != (report.Modifier is null))
                    throw new SaveFormatException($"{where}[{i}].{(report.Improved is null ? "improved" : "modifier")}",
                        $"missing beside {(report.Improved is null ? $"the modifier {report.Modifier}" : $"the outcome {report.Improved}")}: an improvement is an outcome and the modifier it gave a stack of, together");
                if (report.Improved is not null && (report.Stalled || report.RunsWorked != report.Runs))
                    throw new SaveFormatException($"{where}[{i}].improved",
                        $"{report.Improved} on a service {(report.Stalled ? "stalled" : $"at {report.RunsWorked} of {report.Runs} runs")}: the improvement roll is made as a service comes back, its last run worked");

                if (report.Stalled)
                {
                    if (report.RunsWorked >= report.Runs)
                        throw new SaveFormatException($"{where}[{i}].runsWorked",
                            $"{report.RunsWorked} of {report.Runs} runs on a stalled report: a stalled deposit is still in the shop, short of its last run");
                    if (last.ServiceTick && i == reports.Count - 1)
                        throw new SaveFormatException($"{where}[{i}].stalled",
                            "true on the newest deposit, on a run that counted: a run that counts works the newest, and one at least");
                    if (last.ServiceTick && i > 0 && !reports[i - 1].Stalled)
                        throw new SaveFormatException($"{where}[{i}].stalled",
                            "true after a worked report, on a run that counted: a run that counts works the newest, and the reports are in deposit order");
                    continue;
                }
                if (!last.ServiceTick)
                    throw new SaveFormatException($"{where}[{i}].stalled",
                        "false on a run that did not count: a run that did not count works nothing, and reports every deposit stalled");
                if (report.RunsWorked < 1)
                    throw new SaveFormatException($"{where}[{i}].runsWorked",
                        $"{report.RunsWorked} of {report.Runs} runs on a worked report: the run worked it a run at least");
                worked++;
            }
            if (worked > ContentBounds.MostWorked)
                throw new SaveFormatException(where,
                    $"{worked} worked, more than the {ContentBounds.MostWorked} the enchanter works on at most (EnchanterWorksOn at its most)");
            if (reports.Count > campaign.DepositsMade)
                throw new SaveFormatException(where,
                    $"{reports.Count} reports and {campaign.DepositsMade} deposits made: a report speaks of a deposit, and each deposit is its own");
        }

        // 28. The deposit cashed the wear it holds: the weapon has cashed that much at
        // least. And a take-back puts it back — in the pool, and off what was cashed
        // (Weapon.RefundWear) — so the weapon it gives back must load: its wear within
        // the ceiling every weapon row is held to (SaveMapper), under what it had
        // cashed before the deposit. Play writes both, the weapon having gone in as
        // one that loaded — short of a cash saturated at the count's ceiling, a
        // billion hits in, which no play reaches.
        for (int i = 0; i < queue.Count; i++)
        {
            var deposit = queue[i];
            if (deposit.WearAtDeposit > deposit.Weapon.WearCashed)
                throw new SaveFormatException($"{at}[{i}].wearAtDeposit",
                    $"{deposit.WearAtDeposit} is above what the weapon has ever cashed, {deposit.Weapon.WearCashed}: the deposit cashed it");
            int before = deposit.Weapon.WearCashed - deposit.WearAtDeposit;
            long banked = ContentBounds.Bar + (long)ContentBounds.MostCapacityPerBar * before;
            if (deposit.WearAtDeposit > banked)
                throw new SaveFormatException($"{at}[{i}].wearAtDeposit",
                    $"{deposit.WearAtDeposit} is above {banked}, the most a weapon that had cashed {before} banks: the deposit cashed what the weapon banked, and a take-back puts it back");
        }

        // 29. What an Enchant attaches as it finishes must load: the entry is the
        // weapon's next, so the weapon has room for one more.
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].Service == nameof(Service.Enchant) && queue[i].Weapon.Enchantments.Count >= ContentBounds.MostEnchantments)
                throw new SaveFormatException($"{at}[{i}].weapon.enchantments",
                    $"{queue[i].Weapon.Enchantments.Count} entries on a deposit to enchant: the one it attaches would be more than the {ContentBounds.MostEnchantments} a weapon can hold");

        // 30. The enchanter opens when the tutorial is beaten (§6.3), which retires
        // it, and retired is never undone: a shop holding anything, a run's end
        // reporting on one, or a deposit ever made needs a dungeon retired. Asked of
        // the retired list alone, never of which dungeon today's dungeons.json
        // retires on its clear — a copy beside the game may move that flag onto
        // another (as check 3's withdrawal found), and a save play wrote still loads.
        if (campaign.Retired.Count == 0)
        {
            const string why = "the enchanter opens once the tutorial is beaten, which retires it, and takes nothing before";
            if (queue.Count > 0)
                throw new SaveFormatException(at, $"{queue.Count} in the shop and no dungeon is retired: {why}");
            if (campaign.LastResult?.Services is { Count: > 0 } told)
                throw new SaveFormatException($"{At}.lastResult.services", $"{told.Count} reported at the enchanter and no dungeon is retired: {why}");
            if (campaign.DepositsMade > 0)
                throw new SaveFormatException($"{At}.depositsMade", $"{campaign.DepositsMade} made and no dungeon is retired: {why}");
        }
    }

    /// <summary>20's one question: a weapon no member has wielded has banked no wear and cashed none.</summary>
    private static void Unworn(WeaponDto weapon, string at, string why)
    {
        if (weapon.Wear != 0)
            throw new SaveFormatException($"{at}.wear", $"{weapon.Wear} worn: {why}");
        if (weapon.WearCashed != 0)
            throw new SaveFormatException($"{at}.wearCashed", $"{weapon.WearCashed} cashed: {why}");
    }

    /// <summary>16's one question: what a pool held at the door is no more than what it holds now.</summary>
    private static void NoMoreThanNow(int atDoor, int now, string member, string where)
    {
        if (atDoor > now)
            throw new SaveFormatException(where,
                $"{atDoor} is more than member {member} holds now, {now}: a visit only adds XP, and a wipe claws it back only as the run ends");
    }
}
