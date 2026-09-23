using System.Collections.Immutable;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class EventTableTests
{
    private const float Tile = GameConstants.Tile;

    // A catalogue instance shared across this class, read-only: nothing here Acquires on it.
    private static readonly Weapon Dagger = TestWeapons.Get("weakspot_stiletto");   // dmg 15, CritWindow x1 (19+), CritMultiplier x1 (x3)

    /// <summary>A payload that records which handlers touched it, in order.</summary>
    private sealed record Trace(ImmutableList<string> Steps)
    {
        public static readonly Trace Empty = new(ImmutableList<string>.Empty);
        public Trace Then(string step) => this with { Steps = Steps.Add(step) };
        public string Log => string.Join(",", Steps);
    }

    private static PartyMemberState Member(string id = "A")
        => TestPools.Char(id);

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
    public void ExpandingHandler_OwnsItsStep()
    {
        // The enchantment loop prints its entries at (its step, attachment index), so a compiled handler
        // beside it on the step would print an index that is not its place in the chain: refused, in
        // either order of registration.
        static IEnumerable<string> Two(ActorState _) => new[] { "a", "b" };
        var table = new EventTable();
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(4, 0), "Loop", (p, _, _) => p, expand: Two);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(4, 1), "Beside", (p, _, _) => p));
        Assert.Contains("Loop", ex.Message);
        Assert.Contains("Beside", ex.Message);

        var reversed = new EventTable();
        reversed.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(4, 1), "Beside", (p, _, _) => p);
        Assert.Throws<InvalidOperationException>(() =>
            reversed.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(4, 0), "Loop", (p, _, _) => p, expand: Two));

        // The steps either side are anyone's, and the loop's rows print at its step, indexed by entry.
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(3, 9), "Before", (p, _, _) => p);
        table.On<Trace>(GameEvent.DamageTaken, new HandlerPriority(5, 0), "After", (p, _, _) => p);
        Assert.Equal(new[] { "DamageTaken (3,9) Before", "DamageTaken (4,0) a@0", "DamageTaken (4,1) b@1", "DamageTaken (5,0) After" },
            table.HandlersFor(GameEvent.DamageTaken, Member()).Select(h => h.ToString()));
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
                "DamageTaken (0,5) MartialElement",
                "DamageTaken (1,0) RollToBase",
                "DamageTaken (1,1) Longshot",
                "DamageTaken (1,2) FriendlyFire",
                "DamageTaken (2,0) Weakened",
                "DamageTaken (3,0) Sundered",
                "DamageTaken (3,1) TypeChart",
                "DamageTaken (3,9) FixWeaponShare",
                "DamageTaken (4,0) Enchantments",
                "DamageTaken (5,0) Block",
                "DamageTaken (5,1) Aegis",
                "DamageTaken (6,0) Ward",
                "DamageTaken (6,1) Sturdy",
                "DamageTaken (6,9) FixTaken",
                "DamageTaken (7,0) CritRiders",
                "DamageTaken (7,1) BlockWeaken",
                "DamageTaken (7,2) Pin",
                "DamageTaken (7,3) Softening",
                "DamageTaken (8,0) Push",
                "DamageTaken (8,1) Drag",
                "DamageTaken (8,2) Rout",
                "DamageTaken (8,3) Immovable",
            },
            chain.Select(h => h.ToString()));
        Assert.Equal(chain.OrderBy(h => h.Priority), chain);
        Assert.Equal(new[] { "Killed (0,0) Enchantments" }, table.HandlersFor(GameEvent.Killed).Select(h => h.ToString()));   // the loop, for Siphon

        // With an actor the loop prints as what it will run for them: nothing attached (or no weapon)
        // leaves the list as it is; a weapon carrying entries lists them at step 4, one row per entry at
        // its attachment index, so the index-is-priority contract reads off the chain.
        Assert.Equal(chain, table.HandlersFor(GameEvent.DamageTaken, Member()));
        var enchanted = Member();
        enchanted.Inventory[0] = TestWeapons.Enchanted("Charged Knife", 32, 10, 30, "shocking", "flaming");
        var rows = table.HandlersFor(GameEvent.DamageTaken, enchanted).Select(h => h.ToString()).ToList();
        Assert.Equal(chain.Take(8).Select(h => h.ToString()), rows.Take(8));
        Assert.Equal(new[] { "DamageTaken (4,0) shocking@0", "DamageTaken (4,1) flaming@1" }, rows.Skip(8).Take(2));
        Assert.Equal(chain.Skip(9).Select(h => h.ToString()), rows.Skip(10));
    }

    [Fact]
    public void TheDamageTakenChainPrintsMartialElementAtZeroFive_AndAegisAtFiveOne()
    {
        // Section 3.3's last two compiled steps are two listed rows, and where
        // they sit is the whole of what they are: an element on a martial weapon
        // has to type the swing before the chart reads the type at (3,1), and the
        // shield has to absorb after Block has fixed what was dealt at (5,0).
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        var order = table.HandlersFor(GameEvent.DamageTaken).Select(h => h.ToString()).ToList();

        Assert.Equal(new HandlerPriority(0, 5), EnchantmentBehaviours.MartialElementPriority);
        Assert.Equal(new HandlerPriority(5, 1), EnchantmentBehaviours.AegisPriority);
        Assert.True(order.IndexOf("DamageTaken (0,5) MartialElement") < order.IndexOf("DamageTaken (3,1) TypeChart"));
        Assert.True(order.IndexOf("DamageTaken (5,0) Block") < order.IndexOf("DamageTaken (5,1) Aegis"));
        Assert.True(order.IndexOf("DamageTaken (5,1) Aegis") < order.IndexOf("DamageTaken (6,0) Ward"));

        // Neither could have been a row on step 4: that step belongs to the
        // enchantment loop, which prints its entries at (4, attachment index),
        // and On refuses a compiled handler beside it.
        var ex = Assert.Throws<InvalidOperationException>(() => table.On<DamagePayload>(
            GameEvent.DamageTaken, new HandlerPriority(4, 1), "AegisTooEarly", EnchantmentBehaviours.Aegis));
        Assert.Contains("step 4 belongs to 'Enchantments'", ex.Message);
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
        attacker.ApplyStatus(StatusEffectType.Regeneration, null, 2);
        var defender = new EnemyState { X = 50f, Y = 20f, Hp = 30, Innate = ModifierSet.Of((ModifierType.Block, 1)) };
        defender.ApplyStatus(StatusEffectType.Regeneration, null, 1);

        var payload = DamagePayload.Initial(Dagger, roll: 20, distanceUnits: 22);
        var chain = table.Chain<DamagePayload>(GameEvent.DamageTaken);
        Assert.Equal(22, chain.Count);
        foreach (var (info, handler) in chain)
        {
            string before = Snapshot(attacker) + " | " + Snapshot(defender);
            var next = handler(payload, attacker, defender);
            Assert.NotNull(next);
            string after = Snapshot(attacker) + " | " + Snapshot(defender);
            Assert.True(before == after, $"{info} wrote to an actor: {before} became {after}");
            payload = next;
        }

        // The chain did all its work on the payload alone: a natural 20 on the stiletto is 15 x3, Block skipped.
        Assert.True(payload.IsCrit);
        Assert.Equal(45, payload.Taken);
        Assert.Equal(30, defender.Hp);
    }

    [Fact]
    public void StatusHandlers_DoNotMutateActors()
    {
        // The same walk with every status handler given something to read — the attacker
        // Weakened, the defender Sundered and Warded, the stiletto's CritSunder riding the
        // crit — settles it all on the payload: Weakened 1 off, Sundered 1 on, Block skipped,
        // Ward 3 swallowed, the rider appended. Not a level and not a hit point is written.
        var table = new EventTable();
        Behaviours.RegisterAll(table);

        var attacker = Member();
        attacker.Inventory[0] = Dagger;
        attacker.ApplyStatus(StatusEffectType.Weakened, null, 1);
        var defender = new EnemyState { Hp = 30, Innate = ModifierSet.Of((ModifierType.Block, 1)) };
        defender.ApplyStatus(StatusEffectType.Sundered, null, 1);
        defender.ApplyStatus(StatusEffectType.Ward, null, 3);

        var payload = DamagePayload.Initial(Dagger, roll: 20, distanceUnits: 22);
        foreach (var (info, handler) in table.Chain<DamagePayload>(GameEvent.DamageTaken))
        {
            string before = Snapshot(attacker) + " | " + Snapshot(defender);
            payload = handler(payload, attacker, defender);
            string after = Snapshot(attacker) + " | " + Snapshot(defender);
            Assert.True(before == after, $"{info} wrote to an actor: {before} became {after}");
        }

        Assert.True(payload.IsCrit);
        Assert.Equal(45, payload.Dealt);
        Assert.Equal(3, payload.WardSpent);
        Assert.Equal(42, payload.Taken);
        Assert.Equal(new StatusApplication(StatusEffectType.Sundered, null, 1), Assert.Single(payload.ApplyToDefender));
        Assert.Equal(30, defender.Hp);
        Assert.Equal(3, defender.StatusLevel(StatusEffectType.Ward));
        Assert.Equal(1, defender.StatusLevel(StatusEffectType.Sundered));
        Assert.Equal(1, attacker.StatusLevel(StatusEffectType.Weakened));
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
        // Probes sit at (9,9), after every compiled handler on these events (the status decay is (9,0)).
        turns.Events.On<TurnPayload>(GameEvent.TurnEnd, new HandlerPriority(9, 9), "probe", (p, s, o) =>
        {
            Assert.Same(s, o);
            log.Add($"TurnEnd {Who(s)} {p.Side} t{p.TurnCount}");
            return p;
        });
        turns.Events.On<TurnPayload>(GameEvent.RoundEnd, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            log.Add($"RoundEnd {Who(s)} t{p.TurnCount}");
            return p;
        });
        turns.Events.On<TurnPayload>(GameEvent.TurnStart, new HandlerPriority(9, 9), "probe", (p, s, _) =>
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
        grid[5, 4] = 1;   // a wall directly behind A: the sword's Push x1 has nowhere to shove it, so all three beats land
        var a = Member();
        a.X = 5 * Tile + 16;   // on the tile's centre, so the wall does not touch the sight line
        a.Y = 5 * Tile + 16;
        a.Inventory[0] = Dagger;
        var fallen = Member("B");
        fallen.X = 5 * Tile;
        fallen.Y = 7 * Tile;
        fallen.Hp = 0;
        fallen.Alive = false;
        var acting = new EnemyState { X = 5 * Tile + 16 + 60f, Y = 5 * Tile + 16 };   // adjacent and seen: it acts
        var passive = new EnemyState { X = 15 * Tile, Y = 15 * Tile };      // unseen since spawn, nobody in reach
        var dead = new EnemyState { X = 15 * Tile, Y = 5 * Tile, Hp = 0, Alive = false };
        var turns = new TurnSystem(grid, new[] { a, fallen }, new[] { acting, passive, dead }, () => 10);

        var names = new Dictionary<ActorState, string> { [a] = "A", [fallen] = "B", [acting] = "E1", [passive] = "E2", [dead] = "E3" };
        var log = new List<string>();
        foreach (var evt in new[] { GameEvent.TurnStart, GameEvent.TurnEnd, GameEvent.RoundEnd })
            turns.Events.On<TurnPayload>(evt, new HandlerPriority(9, 9), "probe", (p, s, o) =>
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
        Assert.Equal(TestPools.FixtureHp - 30, a.Hp);   // the acting enemy's three sword beats still landed on A
    }

    private static string Snapshot(ActorState a)
        => $"{a.Hp}/{a.Mana}/{a.X}/{a.Y}/{a.Alive}/"
           + string.Join(",", a.StatusEffects.Select(e => $"{e.Type}:{e.Element}:{e.Levels}"));
}
