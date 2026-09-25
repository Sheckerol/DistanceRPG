using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.2's round-trip test (PHASE5-030): "save, reload, and assert that a fixed
/// sequence of turns produces an identical world state". A campaign saved
/// mid-turn and the campaign its save brings back each play the same script — by
/// stable ids, on the same dice — and stay one world: the same document after
/// every turn, the same hits with the same numbers, the same reactions. On a
/// test's injected die and on the visit's own (<see cref="DungeonState.RollD20"/>,
/// USER DECISION 1: seeded and saved, so a reload is never a reroll), through a
/// farm's revivals and a threat zone crossed after the reload; and a quit that
/// finds the enemies moving settles them, one fixed logic step at a time
/// (<see cref="SaveGame.LogicStepSeconds"/>), to the next player turn before
/// anything is written (<see cref="SaveGame.SettleToSafePoint"/>).
/// <para>
/// Played by <see cref="VisitScript"/> on the game's own campaign, as S6's round
/// trips are, every seed's depth pinned before a test leans on it. In the content
/// collection: the farm runs under a tuning of its own.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class ReloadDeterminismTests
{
    /// <summary>A seed the Spear Hall rolls five floors on (pinned where a test leans on it).</summary>
    private const long FiveDeep = 4;

    /// <summary>The shipped seed: the tutorial is two floors deep on it.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>How many turns the fixed sequence plays after the save.</summary>
    private const int TurnsPlayed = 4;

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    // ── Dice ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A d20 sequence that visits every face — crits for every crit window,
    /// fumbles, and everything between — so a script played on it takes every
    /// branch a roll can send a hit down, the same on both sides of a reload.
    /// </summary>
    private static readonly int[] Faces = [10, 17, 3, 20, 1, 12, 8, 19, 5, 14, 11, 2, 16, 7, 18, 4, 13, 9, 15, 6];

    /// <summary><see cref="Faces"/> from position <paramref name="start"/> on, round and round: one side's copy of the sequence.</summary>
    private static Func<int> Sequence(int start = 0)
    {
        int next = start;
        return () => Faces[next++ % Faces.Length];
    }

    /// <summary>
    /// The die a test hands a floor before it knows what that floor will roll:
    /// tens (<see cref="VisitScript.Ten"/>) until it is told otherwise — the
    /// flagship is pinned on tens — and every roll it hands out, kept.
    /// </summary>
    private sealed class Dice(Func<int>? source = null)
    {
        private Func<int> _source = source ?? VisitScript.Ten;

        /// <summary>Every roll handed out, in order.</summary>
        public List<int> Rolls { get; } = new();

        /// <summary>Roll <paramref name="source"/> from now on.</summary>
        public void Use(Func<int> source) => _source = source;

        public int Roll()
        {
            int roll = _source();
            Rolls.Add(roll);
            return roll;
        }
    }

    /// <summary>What the own die of a visit seeded <paramref name="mapSeed"/> rolls at draws <paramref name="from"/> onward, <paramref name="count"/> of them: its stream's draws, read without rolling it.</summary>
    private static int[] Draws(long mapSeed, int from, int count)
        => Enumerable.Range(from, count).Select(draw => DungeonState.DiceStream(mapSeed, draw).NextInt(1, 20)).ToArray();

    // ── What a floor's system says, by stable id ─────────────────────────────

    /// <summary>
    /// Everything <paramref name="turns"/> tells whoever watches it, written by
    /// stable ids — a member by roster id, a row by floor and spawn index — so two
    /// sides' records compare as text: every hit with its roll and its numbers,
    /// every reaction, shove, tick, status, heal, death, rising and drop, and each
    /// new turn.
    /// </summary>
    private static List<string> Record(TurnSystem turns, DungeonState visit)
    {
        var log = new List<string>();
        string Who(ActorState actor) => actor switch
        {
            PartyMemberState member => member.Id,
            EnemyState row => $"{visit.FloorIndex}/{row.SpawnIndex}",
            _ => throw new Xunit.Sdk.XunitException("An actor that is neither a member nor a row."),
        };
        static string Numbers(AttackResolution hit)
            => $"roll {hit.Roll.Roll} {hit.Roll.Outcome} base {hit.Roll.Damage} dealt {hit.Dealt} taken {hit.Taken} blocked {hit.Blocked} ward {hit.WardSpent}"
                + $" riders [{string.Join(", ", hit.Riders.Select(rider => $"{rider.Type}{rider.Element} {rider.Levels}"))}]";

        turns.PlayerTurnStarted += () => log.Add($"turn {turns.TurnCount}");
        turns.CharacterHit += (member, hit) => log.Add($"hit {Who(member)}: {Numbers(hit)}");
        turns.EnemyHit += (row, hit) => log.Add($"hit {Who(row)}: {Numbers(hit)}");
        turns.BraceTriggered += member => log.Add($"brace {Who(member)}");
        turns.EnemyBraceTriggered += row => log.Add($"brace {Who(row)}");
        turns.OpportunistTriggered += actor => log.Add($"opportunist {Who(actor)}");
        turns.OverwatchTriggered += actor => log.Add($"overwatch {Who(actor)}");
        turns.RiposteTriggered += actor => log.Add($"riposte {Who(actor)}");
        turns.ActorDisplaced += (actor, tiles) => log.Add($"shoved {Who(actor)} {tiles}");
        turns.ActorStatusTicked += (actor, tick) => log.Add($"tick {Who(actor)}: {tick.Type} {tick.Damage}");
        turns.ActorStatusApplied += (target, effect, source) => log.Add($"status {Who(target)} {effect.Type} {effect.Levels} from {Who(source)}");
        turns.CharacterHealed += (member, amount) => log.Add($"healed {Who(member)} {amount}");
        turns.EnemyHealed += (row, amount) => log.Add($"healed {Who(row)} {amount}");
        turns.CharacterDied += member => log.Add($"died {Who(member)}");
        turns.EnemyDefeated += row => log.Add($"defeated {Who(row)}");
        turns.EnemyResurrected += row => log.Add($"risen {Who(row)}");
        turns.EnemyDropped += (row, drop) => log.Add($"dropped {Who(row)} {drop.Weapon.Id}");
        return log;
    }

    /// <summary>The reactions among a record: what a threat zone or a block answered.</summary>
    private static string[] Reactions(IEnumerable<string> log)
        => log.Where(line => line.StartsWith("brace ", StringComparison.Ordinal) || line.StartsWith("opportunist ", StringComparison.Ordinal)
            || line.StartsWith("overwatch ", StringComparison.Ordinal) || line.StartsWith("riposte ", StringComparison.Ordinal)).ToArray();

    /// <summary>The natural roll of every hit among a record, in order.</summary>
    private static int[] HitRolls(IEnumerable<string> log)
        => log.Where(line => line.StartsWith("hit ", StringComparison.Ordinal))
            .Select(line => int.Parse(line.Split(" roll ", 2)[1].Split(' ', 2)[0]))
            .ToArray();

    // ── Moving on a floor, as the script's geometry needs it ─────────────────

    /// <summary>Whether a tile is floor with no living actor on it, member or row.</summary>
    private static bool Free(VisitScript side, (int R, int C) tile)
    {
        var grid = side.Floor.Map.Grid;
        if (tile.R < 0 || tile.R >= grid.GetLength(0) || tile.C < 0 || tile.C >= grid.GetLength(1) || grid[tile.R, tile.C] != 0)
            return false;
        return !side.Party.Concat<ActorState>(side.Floor.Enemies).Any(actor => actor.Alive && Displacer.TileOf(actor) == tile);
    }

    /// <summary>Whether <paramref name="reactor"/>'s weapon would reach <paramref name="mover"/> standing on <paramref name="tile"/>: its range, surface to surface, and its sight — the edge its zone answers at.</summary>
    private static bool Reaches(EnemyState reactor, ActorState mover, (int R, int C) tile, int[,] grid)
    {
        var (x, y) = Displacer.CentreOf(tile.R, tile.C);
        return CombatRules.InAttackRange(reactor.X, reactor.Y, reactor.Radius, x, y, mover.Radius, reactor.Weapon)
            && LineOfSight.HasLineOfSight(grid, reactor.X, reactor.Y, x, y);
    }

    /// <summary>Whether <paramref name="attacker"/>'s weapon would reach <paramref name="target"/> from <paramref name="tile"/>: the same test, the attacker moved.</summary>
    private static bool Hits(ActorState attacker, ActorState target, (int R, int C) tile, int[,] grid)
    {
        var (x, y) = Displacer.CentreOf(tile.R, tile.C);
        return CombatRules.InAttackRange(x, y, attacker.Radius, target.X, target.Y, target.Radius, attacker.EquippedWeapon!)
            && LineOfSight.HasLineOfSight(grid, x, y, target.X, target.Y);
    }

    /// <summary>The free tile nearest <paramref name="from"/> — ties by row, then column — that <paramref name="accept"/> takes, searched out to eight tiles; (-1, -1) for none.</summary>
    private static (int R, int C) Nearest(VisitScript side, (int R, int C) from, Func<(int R, int C), bool> accept)
    {
        var tiles = new List<(int R, int C)>();
        for (int dr = -8; dr <= 8; dr++)
            for (int dc = -8; dc <= 8; dc++)
                tiles.Add((from.R + dr, from.C + dc));
        return tiles
            .Where(tile => tile != from && Free(side, tile) && accept(tile))
            .OrderBy(tile => (tile.R - from.R) * (tile.R - from.R) + (tile.C - from.C) * (tile.C - from.C))
            .ThenBy(tile => tile.R).ThenBy(tile => tile.C)
            .DefaultIfEmpty((-1, -1))
            .First();
    }

    // ── The fixed sequence ───────────────────────────────────────────────────

    /// <summary>
    /// One turn of the fixed sequence in the flagship's halberd room, the same
    /// moves on whichever side plays it: the room looked over; A, B and C, in
    /// roster order, each going for the halberd while it stands — from where it
    /// stands if its weapon reaches, else from the nearest free tile it does — and
    /// swinging once; D casting its staff on the first of the party it can; and
    /// the turn ended, the enemies' run to the next player phase on the logic
    /// clock's own step (<see cref="SaveGame.LogicStepSeconds"/>).
    /// </summary>
    private static void PlayHalberdTurn(VisitScript side)
    {
        var turns = side.Turns;
        var grid = side.Floor.Map.Grid;
        var halberd = side.Row(3, 0);
        side.Look();
        foreach (var id in new[] { "A", "B", "C" })
        {
            var member = side.Member(id);
            if (!member.Alive || !halberd.Alive)
                continue;
            if (!EnemyAi.CanHit(member, halberd, member.EquippedWeapon!, grid))
            {
                var from = Nearest(side, Displacer.TileOf(member), tile => Hits(member, halberd, tile, grid));
                if (from != (-1, -1))
                    side.Walk(id, from);
            }
            if (member.Alive && halberd.Alive && turns.CanAttack(member, halberd))
                side.Swing(id, halberd);
        }

        var d = side.Member("D");
        if (side.Party.FirstOrDefault(member => turns.CanCast(d, member)) is { } patient)
            Assert.True(turns.TryCast(d, patient));

        side.Look();
        side.EndTurn(SaveGame.LogicStepSeconds);
    }

    [Fact]
    public void AFixedSequenceOfTurnsAfterAReloadIsTheSameWorld()
    {
        // PHASE5-030, on S6's flagship: the Spear Hall on seed 4, saved on floor 4
        // at turn 10, mid-turn — B inside the seen halberd's reach, C just outside
        // it. The live campaign and the one its save brings back play the same
        // turns on the same dice, and stay one world.
        var liveDie = new Dice();                       // tens: everything the flagship pins is pinned on them
        var live = VisitSaveTests.Flagship(liveDie.Roll);
        string text = Save(live.Campaign);
        Assert.All(liveDie.Rolls, roll => Assert.Equal(10, roll));
        int beforeTheSave = liveDie.Rolls.Count;

        // From the save on, each side rolls its own copy of one varied sequence.
        liveDie.Use(Sequence());
        var backDie = new Dice(Sequence());
        var back = new VisitScript(Load(text).Campaign, backDie.Roll);
        back.Resume();
        Assert.Equal(text, Save(back.Campaign));
        VisitScript[] sides = [live, back];

        // Where B steps and C walks in, read off the live floor once and handed to
        // both. B steps to a tile still inside the halberd's reach, and inside or
        // outside every other row's exactly as before: it crosses no edge. C
        // crosses into the halberd's reach from just outside it.
        var halberd = live.Row(0, "halberd");
        var grid = live.Floor.Map.Grid;
        var (b, c) = (live.Member("B"), live.Member("C"));
        Assert.True(EnemyAi.CanHit(halberd, b, halberd.Weapon, grid));
        Assert.False(EnemyAi.CanHit(halberd, c, halberd.Weapon, grid));
        var others = live.Floor.Enemies.Where(row => row.Alive && !ReferenceEquals(row, halberd)).ToArray();
        var bTile = Displacer.TileOf(b);
        var bStep = Nearest(live, bTile, tile => Reaches(halberd, b, tile, grid)
            && others.All(row => Reaches(row, b, tile, grid) == Reaches(row, b, bTile, grid)));
        var cIn = Nearest(live, Displacer.TileOf(c), tile => Reaches(halberd, c, tile, grid));
        Assert.NotEqual((-1, -1), bStep);
        Assert.NotEqual((-1, -1), cIn);

        var liveLog = Record(live.Turns, live.Visit);
        var backLog = Record(back.Turns, back.Visit);

        // B's standing pair fires on neither side: B was inside the halberd's reach
        // before the save, the reload re-read its zone from where everyone stands,
        // and a step inside it is no entry. C's walk in fires the brace on both.
        foreach (var side in sides)
            side.Walk("B", bStep);
        Assert.Empty(liveLog);
        Assert.Equal(liveLog, backLog);
        foreach (var side in sides)
            side.Walk("C", cIn);
        Assert.Equal(["brace 3/0"], Reactions(liveLog));
        Assert.Equal(liveLog, backLog);
        Assert.Equal(Save(live.Campaign), Save(back.Campaign));

        // Then the turns: the same document after every one, and the same hits,
        // reactions, shoves, ticks, deaths and drops, number for number and roll
        // for roll.
        for (int turn = 0; turn < TurnsPlayed; turn++)
        {
            foreach (var side in sides)
                PlayHalberdTurn(side);
            Assert.Equal(liveLog, backLog);
            Assert.Equal(Save(live.Campaign), Save(back.Campaign));
        }
        Assert.Equal(liveDie.Rolls.Skip(beforeTheSave), backDie.Rolls);
        Assert.Equal((10 + TurnsPlayed, 10 + TurnsPlayed), (live.Visit.Turn, back.Visit.Turn));

        // And the sequence did what it says, so the sameness means something: the
        // party's blows landed on the halberd, the enemies' on the party, and both
        // sides rolled every die of it.
        Assert.Contains(liveLog, line => line.StartsWith("hit 3/0: ", StringComparison.Ordinal));
        Assert.Contains(liveLog, line => line.StartsWith("hit B: ", StringComparison.Ordinal) || line.StartsWith("hit C: ", StringComparison.Ordinal));
        Assert.True(backDie.Rolls.Count > 10, $"{backDie.Rolls.Count} rolls");
    }

    // ── The farm across a reload ─────────────────────────────────────────────

    /// <summary>Back to the arrival room and turns ended — on the logic clock's step — until <paramref name="row"/> is up again: three at most.</summary>
    private static void UntilItRises(VisitScript side, EnemyState row)
    {
        side.Home();
        for (int turn = 0; turn < 3 && !row.Alive; turn++)
            side.EndTurn(SaveGame.LogicStepSeconds);
        Assert.True(row.Alive, $"spawn {row.SpawnIndex} never rose");
    }

    /// <summary>
    /// The first of the party still standing walks up to <paramref name="row"/>
    /// and puts it down (<see cref="VisitScript.Kill"/>): a farm is waited out in
    /// the arrival room, in reach of a thrower, and whoever it has not felled
    /// does the killing.
    /// </summary>
    private static void KillWithTheFirstStanding(VisitScript side, EnemyState row)
    {
        var killer = side.Party.First(member => member.Alive && member.EquippedWeapon is { IsCaster: false });
        side.Kill(killer.Id, row);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFarmGoesOnTheSameAfterAReload(bool visitsOwnDie)
    {
        // The boss alive, and every revival a turn after its kill: a flensing-knife
        // dummy on the tutorial's first floor killed, up again with its first cycle
        // rolled, and killed again — a cycle pending — then saved mid-turn. The live
        // campaign and its reload each wait for it to rise, kill it a third time and
        // let it rise again: the per-cycle revival stream, the revival turn
        // arithmetic across the save, and the counters that key them, which S6's
        // round trips compare only field for field. On an injected die, and on the
        // visit's own.
        using var _ = TestContent.Use(tuning: ShippedContent.Tuning with { ResurrectTurnsBase = 2, ResurrectTurnsFloor = 1 });
        Assert.Equal((1, 1), (FarmLadder.ResurrectTurns(1), FarmLadder.ResurrectTurns(2)));

        var liveDie = visitsOwnDie ? null : new Dice(Sequence());
        var live = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed, liveDie is null ? null : liveDie.Roll, visitsOwnDie);
        Assert.Equal((2, false), (live.Visit.BossFloor, live.Visit.BossDefeated));
        var knife = live.Row(20, "flensing_knife");

        KillWithTheFirstStanding(live, knife);
        UntilItRises(live, knife);
        Assert.Equal((1, 1), (knife.DefeatCount, knife.RevivalsRolled));
        Assert.True(knife.RevivalDamage + knife.RevivalMaxHp > 0);
        KillWithTheFirstStanding(live, knife);
        Assert.Equal((2, 1, 1), (knife.DefeatCount, knife.RevivalsRolled, knife.DefeatedAtTurn));
        string text = Save(live.Campaign);

        var back = visitsOwnDie
            ? new VisitScript(Load(text).Campaign, visitsOwnDie: true)
            : new VisitScript(Load(text).Campaign, new Dice(Sequence(liveDie!.Rolls.Count)).Roll);
        back.Resume();
        VisitScript[] sides = [live, back];
        var rows = new[] { knife, back.Row(0, 20) };
        Assert.Equal(visitsOwnDie ? live.Visit.DiceRolled : 0, back.Visit.DiceRolled);
        Assert.True(visitsOwnDie == (live.Visit.DiceRolled > 0));

        // The pending cycle is rolled after the reload on both sides, at the turn
        // the saved defeat turn and count make it due; the next one likewise.
        for (int i = 0; i < sides.Length; i++)
        {
            UntilItRises(sides[i], rows[i]);
            Assert.Equal((2, 2, 2), (sides[i].Visit.Turn, rows[i].DefeatCount, rows[i].RevivalsRolled));
            KillWithTheFirstStanding(sides[i], rows[i]);
            UntilItRises(sides[i], rows[i]);
            Assert.Equal((3, 3, 3), (sides[i].Visit.Turn, rows[i].DefeatCount, rows[i].RevivalsRolled));
        }
        Assert.Equal((knife.RevivalsRolled, knife.RevivalDamage, knife.RevivalMaxHp, knife.MaxHp),
            (rows[1].RevivalsRolled, rows[1].RevivalDamage, rows[1].RevivalMaxHp, rows[1].MaxHp));
        Assert.Equal((live.Visit.Turn, live.Visit.DiceRolled), (back.Visit.Turn, back.Visit.DiceRolled));
        Assert.Equal(Save(live.Campaign), Save(back.Campaign));
    }

    // ── The visit's own die ──────────────────────────────────────────────────

    [Fact]
    public void TheVisitsDieRollsOnAcrossAReload()
    {
        // USER DECISION 1, provisional (seeded and saved): the flagship as saved —
        // its tens were injected, so the visit's own die has rolled nothing yet —
        // opened with OpenTurns(party) and no die at all, as the game is to open
        // every floor: every roll is the visit's own, the turn's first its
        // stream's first draw, and the count goes into the document.
        string flagship = Save(VisitSaveTests.Flagship().Campaign);
        Assert.Equal(0, SaveCodec.Read(flagship).Campaign.Visit!.DiceRolled);
        var live = new VisitScript(Load(flagship).Campaign, visitsOwnDie: true);
        live.Resume();
        Assert.Equal(0, live.Visit.DiceRolled);

        var opening = Record(live.Turns, live.Visit);
        PlayHalberdTurn(live);
        int rolled = live.Visit.DiceRolled;
        Assert.True(rolled > 0);
        Assert.Equal(Draws(FiveDeep, 0, 1)[0], HitRolls(opening)[0]);   // A's walk up and swing roll first, before anything else rolls
        string text = Save(live.Campaign);
        Assert.Equal(rolled, SaveCodec.Read(text).Campaign.Visit!.DiceRolled);

        // Reloaded, it rolls on from exactly there: the next roll on either side is
        // the stream's draw at the saved count — the roll play without the reload
        // would have made next, never a reroll — and then the same rolls, the same
        // hits and the same document after every turn.
        var back = new VisitScript(Load(text).Campaign, visitsOwnDie: true);
        back.Resume();
        Assert.Equal(rolled, back.Visit.DiceRolled);
        int next = Draws(FiveDeep, rolled, 1)[0];
        Assert.Equal((next, next), (live.Visit.RollD20(), back.Visit.RollD20()));

        var liveLog = Record(live.Turns, live.Visit);
        var backLog = Record(back.Turns, back.Visit);
        for (int turn = 1; turn < TurnsPlayed; turn++)
        {
            PlayHalberdTurn(live);
            PlayHalberdTurn(back);
            Assert.Equal(liveLog, backLog);
            Assert.Equal(live.Visit.DiceRolled, back.Visit.DiceRolled);
            Assert.Equal(Save(live.Campaign), Save(back.Campaign));
        }
        Assert.True(back.Visit.DiceRolled > rolled + 1);
        Assert.NotEmpty(HitRolls(backLog));
    }

    [Fact]
    public void OneSeedOneDie_AFairD20()
    {
        // The die is the visit's: two visits on one seed roll one sequence — the
        // n-th roll the visit stream's n-th draw — and another seed another.
        var first = NewCampaign().Enter("spear_hall", FiveDeep);
        var second = NewCampaign().Enter("spear_hall", FiveDeep);
        var other = NewCampaign().Enter("spear_hall", GoldenSeed);
        static int[] Roll(DungeonState visit, int count) => Enumerable.Range(0, count).Select(_ => visit.RollD20()).ToArray();
        var rolls = Roll(first, 200);
        Assert.Equal(rolls, Roll(second, 200));
        Assert.Equal(Draws(FiveDeep, 0, 200), rolls);
        Assert.Equal((200, 200, 0), (first.DiceRolled, second.DiceRolled, other.DiceRolled));
        Assert.NotEqual(rolls, Roll(other, 200));

        // A floor opened with no die rolls it: A's one swing at the javelins — no
        // zone armed, nothing seen, so nothing else rolls — is the stream's first
        // draw, and the die has rolled once.
        var campaign = NewCampaign();
        var visit = campaign.Enter("spear_hall", FiveDeep);
        var turns = visit.OpenTurns(campaign.Party);
        var script = new VisitScript(campaign, visitsOwnDie: true);
        var javelins = script.Row(11, "softening_javelins");
        var log = Record(turns, visit);
        script.Walk("A", script.Beside(javelins));
        script.Swing("A", javelins);
        Assert.Equal(Draws(FiveDeep, 0, 1), HitRolls(log));
        Assert.Equal(1, visit.DiceRolled);

        // Fair: over twenty thousand rolls on each of three seeds, every face comes
        // up within a percentage point of a twentieth of the time, and no other
        // number ever does.
        foreach (long seed in new[] { FiveDeep, GoldenSeed, 0L })
        {
            var die = NewCampaign().Enter("spear_hall", seed);
            var counts = new Dictionary<int, int>();
            const int Rolls = 20_000;
            for (int i = 0; i < Rolls; i++)
            {
                int roll = die.RollD20();
                counts[roll] = counts.GetValueOrDefault(roll) + 1;
            }
            Assert.Equal(Enumerable.Range(1, 20), counts.Keys.Order());
            foreach (var (face, count) in counts)
                Assert.True(Math.Abs(count / (double)Rolls - 1 / 20.0) <= 0.01, $"seed {seed}, face {face}: {count} of {Rolls}");
        }
    }

    [Fact]
    public void ADocumentFromBeforeTheDieIsADieNotYetRolled_ACountNoPlayReachesIsRefused()
    {
        // diceRolled is appended to the visit — last in its object, with its
        // default — so a document written before Phase 5.7 reads as a die that has
        // rolled nothing, and a negative count is refused, naming where.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep, visitsOwnDie: true);
        script.Kill("A", script.Row(11, "softening_javelins"));
        int rolled = script.Visit.DiceRolled;
        Assert.True(rolled > 0);

        var tree = JsonNode.Parse(Save(script.Campaign))!.AsObject();
        var visit = tree["campaign"]!["visit"]!.AsObject();
        Assert.Equal(("diceRolled", rolled), (visit.Last().Key, (int)visit["diceRolled"]!));
        Assert.Equal(rolled, Load(tree.ToJsonString()).Campaign.Visit!.DiceRolled);

        visit.Remove("diceRolled");
        Assert.Equal(0, Load(tree.ToJsonString()).Campaign.Visit!.DiceRolled);

        visit["diceRolled"] = -1;
        var refused = Assert.Throws<SaveFormatException>(() => Load(tree.ToJsonString()));
        Assert.Equal("campaign.visit.diceRolled", refused.Where);
    }

    // ── The settle ───────────────────────────────────────────────────────────

    /// <summary>
    /// A campaign at the point a quit arrives: the Spear Hall's first floor on its
    /// own die, A in the lance's room and out of its reach, the room looked over,
    /// and the turn ended — then the logic clock stepped until the lance is on its
    /// way to A. Every call builds the same campaign to the same point. Shared with
    /// <c>CampaignBootTests</c>, whose quit settles it through the save's own door.
    /// </summary>
    internal static VisitScript MidWalk()
    {
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep, visitsOwnDie: true);
        var lance = script.Row(10, "pinning_lance");
        script.Walk("A", (8, 40));
        Assert.False(EnemyAi.CanHit(lance, script.Member("A"), lance.Weapon, script.Floor.Map.Grid));
        script.Look();

        var turns = script.Turns;
        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(script.Campaign));   // the banner is up: no save point
        for (int step = 0; step < 10_000 && turns.Phase != TurnPhase.EnemyMoving; step++)
            turns.Update(SaveGame.LogicStepSeconds);
        Assert.Equal(TurnPhase.EnemyMoving, turns.Phase);
        for (int step = 0; step < 12; step++)   // a few steps into the walk: the quit arrives mid-stride
            turns.Update(SaveGame.LogicStepSeconds);
        Assert.Equal(TurnPhase.EnemyMoving, turns.Phase);
        return script;
    }

    [Fact]
    public void AQuitMidEnemyPhaseSettlesToTheNextPlayerTurn()
    {
        // 1f: a quit that finds the enemies moving settles them to the next player
        // turn before anything is written — and what it writes is exactly what live
        // play reaches, stepping the same fixed step. Every campaign here is built
        // to the same point mid-walk, so each can be taken on a different way.
        Assert.Equal(1f / 120f, SaveGame.LogicStepSeconds);
        var live = MidWalk();
        var campaign = live.Campaign;
        Assert.False(SaveGame.IsSafePoint(campaign));
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(campaign));

        // Live play, stepped at LogicStepSeconds to the next player turn: the
        // lance's walk ends in blows on A, so dice are rolled on the way.
        var played = MidWalk();
        var log = Record(played.Turns, played.Visit);
        int stepsToPlay = 0;
        while (played.Turns.Phase != TurnPhase.Player && stepsToPlay < 200_000)
        {
            played.Turns.Update(SaveGame.LogicStepSeconds);
            stepsToPlay++;
        }
        Assert.Equal(TurnPhase.Player, played.Turns.Phase);
        Assert.Contains(log, line => line.StartsWith("hit A: ", StringComparison.Ordinal));
        string reached = Save(played.Campaign);

        // The settle on its own step reaches the same turn — the same blows, the
        // same document — in exactly those steps: bounded one short, it has not.
        var settledLog = Record(live.Turns, live.Visit);
        Assert.True(SaveGame.SettleToSafePoint(campaign, maxSteps: stepsToPlay));
        Assert.Equal(TurnPhase.Player, live.Turns.Phase);
        Assert.Equal(log, settledLog);
        Assert.Equal(reached, Save(campaign));
        var cut = MidWalk();
        Assert.False(SaveGame.SettleToSafePoint(cut.Campaign, maxSteps: stepsToPlay - 1));
        Assert.False(SaveGame.IsSafePoint(cut.Campaign));

        // Handed a step, the settle takes exactly that many of it, and never steps
        // the system itself: the game settles with the scene's own step.
        var stepped = MidWalk();
        int calls = 0;
        Assert.True(SaveGame.SettleToSafePoint(stepped.Campaign, () =>
        {
            calls++;
            stepped.Turns.Update(SaveGame.LogicStepSeconds);
        }));
        Assert.Equal(stepsToPlay, calls);
        Assert.Equal(reached, Save(stepped.Campaign));

        // A step that moves nothing: the settle calls it, and nothing else, up to
        // the bound — the lance where it was, the phase what it was — and says it
        // never settled.
        var stuck = MidWalk();
        var stuckLance = stuck.Row(0, 10);
        var at = (stuckLance.X, stuckLance.Y);
        int idle = 0;
        Assert.False(SaveGame.SettleToSafePoint(stuck.Campaign, () => idle++, maxSteps: 240));
        Assert.Equal(240, idle);
        Assert.Equal((TurnPhase.EnemyMoving, at), (stuck.Turns.Phase, (stuckLance.X, stuckLance.Y)));
        Assert.False(SaveGame.IsSafePoint(stuck.Campaign));

        // At a safe point already, a settle takes no step at all.
        Assert.True(SaveGame.SettleToSafePoint(campaign, () => throw new Xunit.Sdk.XunitException("a safe point needs no step")));
        Assert.True(SaveGame.SettleToSafePoint(NewCampaign(), () => throw new Xunit.Sdk.XunitException("a campaign between runs needs no step")));
    }

    [Fact]
    public void ASettleThatWipesThePartyLeavesACampaignBetweenRuns()
    {
        // A settle plays the enemies' turn out as play would, wipe and all: A stands
        // alone at 1 HP beside the Spear Hall's lance, the rest poisoned past saving
        // as the turn ends. The lance's blow ends the run in Logic — the visit's
        // Wiped, the campaign's EndRun — so the settle leaves a campaign between
        // runs: no visit, the run's result recorded, and safe to save.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep, visitsOwnDie: true);
        var campaign = script.Campaign;
        var lance = script.Row(10, "pinning_lance");
        var results = new List<RunResult>();
        campaign.RunEnded += results.Add;

        script.Walk("A", script.Beside(lance));   // unseen, so its zone is not armed: no brace on the way in
        script.Member("A").Hp = 1;
        foreach (var id in new[] { "B", "C", "D" })
        {
            var member = script.Member(id);
            member.Hp = 1;
            member.ApplyStatus(StatusEffectType.Poison, null, 5);
        }
        script.Turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, script.Turns.Phase);
        Assert.Equal([true, false, false, false], script.Party.Select(member => member.Alive));
        Assert.False(SaveGame.IsSafePoint(campaign));
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(campaign));
        Assert.Empty(results);

        Assert.True(SaveGame.SettleToSafePoint(campaign));
        Assert.Null(campaign.Visit);
        var result = Assert.Single(results);
        Assert.Same(result, campaign.LastResult);
        Assert.Equal(("spear_hall", RunOutcome.Death), (result.DungeonId, result.Outcome));
        Assert.All(campaign.Party, member => Assert.False(member.Alive));

        Assert.True(SaveGame.IsSafePoint(campaign));
        string text = Save(campaign);
        Assert.Null(SaveCodec.Read(text).Campaign.Visit);
        var back = Load(text).Campaign;
        Assert.Null(back.Visit);
        Assert.Equal(RunOutcome.Death, back.LastResult!.Outcome);
    }
}
