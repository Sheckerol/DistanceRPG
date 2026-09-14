namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The one dispatcher every behaviour hangs off (§1.7). A behaviour registers
/// a handler on an event at a declared <see cref="HandlerPriority"/>;
/// <see cref="Raise{TPayload}"/> runs the event's handlers in that order, each
/// transforming the payload and returning it, hands the settled payload to
/// the event's single applier — the one place the world is written — and
/// then drains whatever the chain queued. Three rules keep it honest, and
/// each is enforced rather than documented: order is data (the per-event
/// chain is sorted explicitly, an equal priority is a startup error, and
/// dispatch never iterates a Dictionary); handlers never write (every payload
/// is an immutable record and a handler only returns); and follow-ups queue
/// rather than recurse (a nested raise throws, the cascade depth is bounded,
/// and an overrun is a bug rather than something clamped).
/// </summary>
public sealed class EventTable
{
    /// <summary>
    /// Generations of queued follow-ups one raise may drain before the cascade
    /// is declared a bug. A guard, not a gameplay dial: the one legitimate
    /// chain in the design (a shove into a threat zone, a brace, another shove)
    /// is bounded by the per-turn brace budget, far below this.
    /// </summary>
    public const int MaxCascadeDepth = 64;

    private static readonly int EventCount = Enum.GetValues<GameEvent>().Length;

    private sealed class Entry
    {
        public required HandlerPriority Priority { get; init; }
        public required int Ordinal { get; init; }
        public required string Name { get; init; }
        public required Delegate Handler { get; init; }
        public required Func<object, ActorState, ActorState, object> Invoke { get; init; }
    }

    private sealed record Pending(GameEvent Event, object Payload, ActorState Self, ActorState Other, int Generation);

    // Keyed by event ordinal. Each chain is a list re-sorted on every
    // registration, so dispatch walks a sorted list and never a hash order.
    private readonly List<Entry>?[] _chains = new List<Entry>?[EventCount];
    private readonly Action<object, ActorState, ActorState, EventTable>?[] _appliers =
        new Action<object, ActorState, ActorState, EventTable>?[EventCount];
    private readonly Type?[] _payloadTypes = new Type?[EventCount];
    private readonly Queue<Pending> _queue = new();
    private int _nextOrdinal;
    private bool _running;
    private int _generation;

    /// <summary>
    /// Register a behaviour on <paramref name="evt"/> at <paramref name="priority"/>.
    /// The chain is re-sorted by (priority, registration order) on every call;
    /// a priority already taken on that event throws, because undefined order
    /// among same-event handlers is exactly what this table exists to prevent.
    /// </summary>
    public void On<TPayload>(GameEvent evt, HandlerPriority priority, string name, Handler<TPayload> handler)
        where TPayload : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        FixPayloadType(evt, typeof(TPayload));

        var chain = _chains[Index(evt)] ??= new List<Entry>();
        var taken = chain.FirstOrDefault(e => e.Priority == priority);
        if (taken != null)
            throw new InvalidOperationException(
                $"{evt} already runs '{taken.Name}' at {priority}; '{name}' cannot share it. Order among same-event handlers is declared, never undefined.");

        chain.Add(new Entry
        {
            Priority = priority,
            Ordinal = _nextOrdinal++,
            Name = name,
            Handler = handler,
            Invoke = (payload, self, other) =>
                handler((TPayload)payload, self, other)
                ?? throw new InvalidOperationException($"{evt} handler '{name}' returned no payload."),
        });
        chain.Sort(static (a, b) =>
        {
            int byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.Ordinal.CompareTo(b.Ordinal);
        });
    }

    /// <summary>
    /// The one world-write for <paramref name="evt"/>: called once per raise
    /// with the settled payload, after every handler has returned. It may
    /// <see cref="Enqueue{TPayload}"/> follow-ups; it never raises. A second
    /// applier for the same event throws.
    /// </summary>
    public void Applies<TPayload>(GameEvent evt, Action<TPayload, ActorState, ActorState, EventTable> applier)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(applier);
        FixPayloadType(evt, typeof(TPayload));

        int i = Index(evt);
        if (_appliers[i] != null)
            throw new InvalidOperationException($"{evt} already has an applier; the settled result is applied once, by one place.");
        _appliers[i] = (payload, self, other, table) => applier((TPayload)payload, self, other, table);
    }

    /// <summary>
    /// The top-level entry: run the sorted chain, apply the settled payload,
    /// drain the queue (each queued event getting its own chain and applier,
    /// in FIFO order), and return the settled payload of <paramref name="evt"/>
    /// itself. Calling this from inside a handler or applier throws — a
    /// follow-up is queued with <see cref="Enqueue{TPayload}"/>, never raised.
    /// </summary>
    public TPayload Raise<TPayload>(GameEvent evt, TPayload payload, ActorState self, ActorState other)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(other);
        if (_running)
            throw new InvalidOperationException(
                $"Enqueue, don't Raise: {evt} was raised from inside a running chain. Follow-ups queue and the outermost Raise drains them.");
        FixPayloadType(evt, typeof(TPayload));

        _running = true;
        try
        {
            var settled = (TPayload)Dispatch(evt, payload, self, other, generation: 0);
            Drain();
            return settled;
        }
        finally
        {
            Reset();
        }
    }

    /// <summary>
    /// Queue a follow-up. From inside a running chain it is drained by the
    /// outermost raise, one generation deeper than the event that queued it;
    /// a generation past <see cref="MaxCascadeDepth"/> throws rather than
    /// being clamped. With nothing running it is the only event in the queue
    /// and is dispatched now.
    /// </summary>
    public void Enqueue<TPayload>(GameEvent evt, TPayload payload, ActorState self, ActorState other)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(other);
        FixPayloadType(evt, typeof(TPayload));

        if (_running)
        {
            int generation = _generation + 1;
            if (generation > MaxCascadeDepth)
                throw new InvalidOperationException(
                    $"Event cascade deeper than {MaxCascadeDepth} generations ({evt} queued at generation {generation}): a runaway chain is a bug, not something to clamp.");
            _queue.Enqueue(new Pending(evt, payload, self, other, generation));
            return;
        }

        _running = true;
        try
        {
            _queue.Enqueue(new Pending(evt, payload, self, other, Generation: 0));
            Drain();
        }
        finally
        {
            Reset();
        }
    }

    /// <summary>
    /// The chain for <paramref name="evt"/> as a sorted list you can print.
    /// <paramref name="self"/> is where that actor's enchantment entries will
    /// be expanded (step 4, index = attachment order) once weapons carry
    /// enchantments; until then the list is the compiled handlers alone.
    /// </summary>
    public IReadOnlyList<HandlerInfo> HandlersFor(GameEvent evt, ActorState? self = null)
    {
        var chain = _chains[Index(evt)];
        if (chain == null)
            return Array.Empty<HandlerInfo>();
        return chain.Select(e => new HandlerInfo(evt, e.Priority, e.Name)).ToArray();
    }

    /// <summary>The typed chain, in dispatch order, for tests that wrap each handler.</summary>
    internal IReadOnlyList<(HandlerInfo Info, Handler<TPayload> Handler)> Chain<TPayload>(GameEvent evt)
        where TPayload : class
    {
        var chain = _chains[Index(evt)];
        if (chain == null)
            return Array.Empty<(HandlerInfo, Handler<TPayload>)>();
        FixPayloadType(evt, typeof(TPayload));
        return chain.Select(e => (new HandlerInfo(evt, e.Priority, e.Name), (Handler<TPayload>)e.Handler)).ToArray();
    }

    private object Dispatch(GameEvent evt, object payload, ActorState self, ActorState other, int generation)
    {
        _generation = generation;
        int i = Index(evt);
        var chain = _chains[i];
        if (chain != null)
            foreach (var entry in chain)
                payload = entry.Invoke(payload, self, other);
        _appliers[i]?.Invoke(payload, self, other, this);
        return payload;
    }

    private void Drain()
    {
        while (_queue.TryDequeue(out var item))
            Dispatch(item.Event, item.Payload, item.Self, item.Other, item.Generation);
    }

    private void Reset()
    {
        _running = false;
        _generation = 0;
        _queue.Clear();   // only non-empty after a throw mid-drain: a broken cascade never leaks into the next raise
    }

    /// <summary>Each event has one payload record: the first registration or raise fixes it, and a different type afterwards is a programming error.</summary>
    private void FixPayloadType(GameEvent evt, Type payloadType)
    {
        int i = Index(evt);
        var fixedType = _payloadTypes[i];
        if (fixedType == null)
        {
            _payloadTypes[i] = payloadType;
            return;
        }
        if (fixedType != payloadType)
            throw new InvalidOperationException($"{evt} carries {fixedType.Name}, not {payloadType.Name}: each event has one payload record.");
    }

    private static int Index(GameEvent evt)
    {
        int i = (int)evt;
        if ((uint)i >= (uint)EventCount)
            throw new ArgumentOutOfRangeException(nameof(evt), evt, "Not a GameEvent.");
        return i;
    }
}
