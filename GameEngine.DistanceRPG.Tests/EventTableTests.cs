using System.Collections.Immutable;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class EventTableTests
{
    private const float Tile = GameConstants.Tile;

    // The only index-bearing line in this class: sub-step 3 swaps it for TestWeapons.Get("weakspot_stiletto").
    private static Weapon Dagger => GameConstants.Weapons[0];   // dmg 15, CritRange 4 -> CritWindow x4

    /// <summary>A payload that records which handlers touched it, in order.</summary>
    private sealed record Trace(ImmutableList<string> Steps)
    {
        public static readonly Trace Empty = new(ImmutableList<string>.Empty);
        public Trace Then(string step) => this with { Steps = Steps.Add(step) };
        public string Log => string.Join(",", Steps);
    }

    private static PartyMemberState Member(string id = "A")
        => new() { Id = id, ColorIndex = 0 };

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void Handlers_RunInPriorityOrder_RegardlessOfRegistrationOrder()
    {
        var table = new EventTable();
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(5, 0), "five", (p, _, _) => p.Then("5"));
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(1, 0), "one", (p, _, _) => p.Then("1"));
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(3, 0), "three", (p, _, _) => p.Then("3"));
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(3, 1), "three-one", (p, _, _) => p.Then("3.1"));
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(2, 9), "two-nine", (p, _, _) => p.Then("2.9"));

        var settled = table.Raise(GameEvent.DamageTaken, Trace.Empty, Member(), new EnemyState());
        Assert.Equal(new[] { "1", "2.9", "3", "3.1", "5" }, settled.Steps);

        // The same chain settles the same way every time: order is declared, never incidental.
        Assert.Equal(settled.Steps, table.Raise(GameEvent.DamageTaken, Trace.Empty, Member(), new EnemyState()).Steps);
        Assert.Equal(new[] { "(1,0)", "(2,9)", "(3,0)", "(3,1)", "(5,0)" },
            table.HandlersFor(GameEvent.DamageTaken).Select(h => h.Priority.ToString()));
    }

    [Fact]
    public void DuplicatePriority_Throws()
    {
        var table = new EventTable();
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(5, 0), "Block", (p, _, _) => p);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(5, 0), "Other", (p, _, _) => p));
        Assert.Contains("Block", ex.Message);
        Assert.Contains("(5,0)", ex.Message);

        // Order is per event: the same priority elsewhere is no clash.
        table.On<Trace>(GameEvent.DamageDealt, new HandlerPriority(5, 0), "Other", (p, _, _) => p);
        Assert.Single(table.HandlersFor(GameEvent.DamageTaken));
    }

    [Fact]
    public void Raise_InsideHandler_Throws_EnqueueIsDrained()
    {
        var self = Member();
        var other = new EnemyState();

        // A handler that raises recurses, and recursion is refused.
        var recursive = new EventTable();
        recursive.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(1, 0), "recurse",
            (p, s, o) => recursive.Raise(GameEvent.Crit, p, s, o));
        var ex = Assert.Throws<InvalidOperationException>(() => recursive.Raise(GameEvent.DamageTaken, Trace.Empty, self, other));
        Assert.StartsWith("Enqueue, don't Raise", ex.Message);

        // An applier that raises is refused the same way.
        var recursiveApplier = new EventTable();
        recursiveApplier.Applies<Trace>(GameEvent.DamageTaken, (p, s, o, t) => t.Raise(GameEvent.Crit, p, s, o));
        Assert.Throws<InvalidOperationException>(() => recursiveApplier.Raise(GameEvent.DamageTaken, Trace.Empty, self, other));

        // Queued follow-ups run after the outer applier, FIFO, each with its own chain and applier.
        var log = new List<string>();
        var table = new EventTable();
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(1, 0), "hit", (p, s, o) =>
        {
            table.Enqueue(GameEvent.DamageDealt, p.Then("dealt"), s, o);
            return p.Then("hit");
        });
        table.Applies<Trace>(GameEvent.DamageTaken, (p, s, o, t) =>
        {
            log.Add("apply:" + p.Log);
            t.Enqueue(GameEvent.Crit, p.Then("crit"), s, o);
        });
        table.On<Trace>(GameEvent.DamageDealt, new HandlerPriority(0, 0), "dealt", (p, _, _) =>
        {
            log.Add("dealt:" + p.Log);
            return p;
        });
        table.Applies<Trace>(GameEvent.Crit, (p, _, _, _) => log.Add("crit:" + p.Log));

        var settled = table.Raise(GameEvent.DamageTaken, Trace.Empty, self, other);
        Assert.Equal("hit", settled.Log);
        Assert.Equal(new[] { "apply:hit", "dealt:dealt", "crit:hit,crit" }, log);
    }

    [Fact]
    public void Cascade_DeeperThanMaxDepth_Throws()
    {
        Assert.Equal(64, EventTable.MaxCascadeDepth);

        var table = new EventTable();
        int applied = 0;
        table.Applies<Trace>(GameEvent.RoundEnd, (p, s, o, t) =>
        {
            applied++;
            t.Enqueue(GameEvent.RoundEnd, p, s, o);   // a self-perpetuating cascade
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            table.Raise(GameEvent.RoundEnd, Trace.Empty, Member(), new EnemyState()));
        Assert.Contains("64", ex.Message);
        Assert.Equal(EventTable.MaxCascadeDepth + 1, applied);   // generations 0..64 ran; queueing the 65th tripped the guard

        // A failed cascade leaves nothing behind: the table is clean for the next raise.
        var probe = new List<string>();
        table.Applies<Trace>(GameEvent.Killed, (p, _, _, _) => probe.Add("killed"));
        table.Raise(GameEvent.Killed, Trace.Empty, Member(), new EnemyState());
        Assert.Equal(new[] { "killed" }, probe);
        Assert.Equal(EventTable.MaxCascadeDepth + 1, applied);
    }

    [Fact]
    public void Applier_RunsExactlyOnce_WithSettledPayload()
    {
        var table = new EventTable();
        var applied = new List<Trace>();
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(1, 0), "one", (p, _, _) => p.Then("1"));
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(5, 0), "five", (p, _, _) => p.Then("5"));
        table.Applies<Trace>(GameEvent.DamageTaken, (p, _, _, _) => applied.Add(p));

        var settled = table.Raise(GameEvent.DamageTaken, Trace.Empty, Member(), new EnemyState());

        var once = Assert.Single(applied);
        Assert.Same(settled, once);
        Assert.Equal("1,5", settled.Log);

        // One world-write per event: a second applier is refused.
        Assert.Throws<InvalidOperationException>(() =>
            table.Applies<Trace>(GameEvent.DamageTaken, (_, _, _, _) => { }));
    }

    [Fact]
    public void Enqueue_OutsideARaise_DispatchesNow()
    {
        var table = new EventTable();
        var log = new List<string>();
        table.On<Trace>(GameEvent.Killed, new HandlerPriority(0, 0), "note", (p, _, _) => p.Then("noted"));
        table.Applies<Trace>(GameEvent.Killed, (p, _, _, _) => log.Add(p.Log));

        table.Enqueue(GameEvent.Killed, Trace.Empty, Member(), new EnemyState());

        Assert.Equal(new[] { "noted" }, log);
    }

    [Fact]
    public void EachEvent_CarriesOnePayloadRecord()
    {
        var table = new EventTable();
        table.On<Trace>(GameEvent.Killed, new HandlerPriority(0, 0), "trace", (p, _, _) => p);

        Assert.Throws<InvalidOperationException>(() =>
            table.On<KillPayload>(GameEvent.Killed, new HandlerPriority(1, 0), "kill", (p, _, _) => p));
        Assert.Throws<InvalidOperationException>(() =>
            table.Raise(GameEvent.Killed, new KillPayload(Dagger, 1, 1), Member(), new EnemyState()));
    }

    [Fact]
    public void HandlersFor_IsSortedAndPrintable()
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);

        var chain = table.HandlersFor(GameEvent.DamageTaken);
        Assert.Equal(
            new[]
            {
                "DamageTaken (1,0) RollToBase",
                "DamageTaken (3,9) FixWeaponShare",
                "DamageTaken (5,0) Block",
                "DamageTaken (6,9) FixTaken",
            },
            chain.Select(h => h.ToString()));
        Assert.Equal(chain.OrderBy(h => h.Priority), chain);
        Assert.Empty(table.HandlersFor(GameEvent.Killed));

        // With an actor: the same list until weapons carry enchantments to expand.
        Assert.Equal(chain, table.HandlersFor(GameEvent.DamageTaken, Member()));
    }

    [Fact]
    public void RegisterAll_HasNoDuplicatePriorities_AndEveryChainIsStrictlyOrdered()
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);   // On throws on a shared priority, so registering at all is the assertion

        foreach (var evt in Enum.GetValues<GameEvent>())
        {
            var chain = table.HandlersFor(evt);
            for (int i = 1; i < chain.Count; i++)
                Assert.True(chain[i - 1].Priority < chain[i].Priority,
                    $"{evt}: {chain[i - 1]} is not strictly before {chain[i]}");
        }
    }

    [Fact]
    public void Handlers_DoNotMutateActors()
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);

        var attacker = Member();
        attacker.Inventory[0] = Dagger;
        attacker.X = 10f;
        attacker.Y = 20f;
        attacker.Mana = 40;
        attacker.ApplyStatusEffect(StatusEffectType.Regeneration, 2);
        var defender = new EnemyState { X = 50f, Y = 20f, Hp = 30, Innate = ModifierSet.Of((ModifierType.Block, 1)) };
        defender.ApplyStatusEffect(StatusEffectType.Regeneration, 1);

        var payload = DamagePayload.Initial(Dagger, roll: 20, distanceUnits: 22);
        var chain = table.Chain<DamagePayload>(GameEvent.DamageTaken);
        Assert.Equal(4, chain.Count);
        foreach (var (info, handler) in chain)
        {
            string before = Snapshot(attacker) + " | " + Snapshot(defender);
            var next = handler(payload, attacker, defender);
            Assert.NotNull(next);
            string after = Snapshot(attacker) + " | " + Snapshot(defender);
            Assert.True(before == after, $"{info} wrote to an actor: {before} became {after}");
            payload = next;
        }

        // The chain did all its work on the payload alone.
        Assert.True(payload.IsCrit);
        Assert.Equal(30, payload.Taken);
        Assert.Equal(30, defender.Hp);
    }

    [Fact]
    public void TurnSystem_RaisesTurnBoundaryEvents_PerActor()
    {
        var grid = new int[20, 20];
        var a = Member();
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        a.Inventory[0] = Dagger;
        var enemy = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile };   // adjacent and seen: it acts
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        var log = new List<string>();
        string Who(ActorState actor) => actor == a ? "A" : "E";
        turns.Events.On<TurnPayload>(GameEvent.TurnEnd, new HandlerPriority(9, 0), "probe", (p, s, o) =>
        {
            Assert.Same(s, o);
            log.Add($"TurnEnd {Who(s)} {p.Side} t{p.TurnCount}");
            return p;
        });
        turns.Events.On<TurnPayload>(GameEvent.RoundEnd, new HandlerPriority(9, 0), "probe", (p, s, _) =>
        {
            log.Add($"RoundEnd {Who(s)} t{p.TurnCount}");
            return p;
        });
        turns.Events.On<TurnPayload>(GameEvent.TurnStart, new HandlerPriority(9, 0), "probe", (p, s, _) =>
        {
            log.Add($"TurnStart {Who(s)} {p.Side} t{p.TurnCount}");
            return p;
        });

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 6f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(new[]
        {
            "TurnEnd A Party t0",
            "TurnStart E Enemy t0",
            "TurnEnd E Enemy t0",
            "RoundEnd A t0",
            "RoundEnd E t0",
            "TurnStart A Party t1",
        }, log);
    }

    [Fact]
    public void TurnSystem_RaisesTurnBoundaryEvents_ForTheWholeRoster_AliveOrNot()
    {
        // Two members, one fallen; three enemies: one that acts, one passive
        // (never seen, nobody in reach — it skips its action) and one dead. The
        // boundary events do not care: every actor on the roster sees
        // TurnStart, TurnEnd and RoundEnd once, in that order, so no handler
        // ever gets a TurnEnd or RoundEnd without the TurnStart before it.
        var grid = new int[20, 20];
        var a = Member();
        a.X = 5 * Tile;
        a.Y = 5 * Tile;
        a.Inventory[0] = Dagger;
        var fallen = Member("B");
        fallen.X = 5 * Tile;
        fallen.Y = 7 * Tile;
        fallen.Hp = 0;
        fallen.Alive = false;
        var acting = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile };   // adjacent and seen: it acts
        var passive = new EnemyState { X = 15 * Tile, Y = 15 * Tile };      // unseen since spawn, nobody in reach
        var dead = new EnemyState { X = 15 * Tile, Y = 5 * Tile, Hp = 0, Alive = false };
        var turns = new TurnSystem(grid, new[] { a, fallen }, new[] { acting, passive, dead }, () => 10);

        var names = new Dictionary<ActorState, string> { [a] = "A", [fallen] = "B", [acting] = "E1", [passive] = "E2", [dead] = "E3" };
        var log = new List<string>();
        foreach (var evt in new[] { GameEvent.TurnStart, GameEvent.TurnEnd, GameEvent.RoundEnd })
            turns.Events.On<TurnPayload>(evt, new HandlerPriority(9, 0), "probe", (p, s, o) =>
            {
                Assert.Same(s, o);
                log.Add($"{evt} {names[s]}");
                return p;
            });

        turns.NotifyEnemyVisible(acting, true);
        turns.EndTurn();
        Advance(turns, 6f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(new[]
        {
            "TurnEnd A", "TurnEnd B",
            "TurnStart E1", "TurnStart E2", "TurnStart E3",
            "TurnEnd E1", "TurnEnd E2", "TurnEnd E3",
            "RoundEnd A", "RoundEnd B", "RoundEnd E1", "RoundEnd E2", "RoundEnd E3",
            "TurnStart A", "TurnStart B",
        }, log);

        // The events say the phase opened for them, not that they acted: the
        // passive one stayed put and the dead stayed dead.
        Assert.Equal((15 * Tile, 15 * Tile), (passive.X, passive.Y));
        Assert.False(dead.Alive);
        Assert.False(fallen.Alive);
        Assert.Equal(GameConstants.PlayerHp - 30, a.Hp);   // the acting enemy's three sword beats still landed on A
    }

    private static string Snapshot(ActorState a)
        => $"{a.Hp}/{a.Mana}/{a.X}/{a.Y}/{a.Alive}/"
           + string.Join(",", a.StatusEffects.Select(e => $"{e.Type}:{e.Level}"));
}
