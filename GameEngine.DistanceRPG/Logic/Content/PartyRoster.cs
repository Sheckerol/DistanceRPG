namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded starting roster (§5.9), keyed by stable string id and kept in
/// file order — the same shape as <see cref="WeaponCatalogue"/>, because a
/// roster is content by the same argument. File order is spawn order, so a
/// party is built by walking <see cref="All"/>; nothing reads a member by
/// position in an array of constants any more.
/// </summary>
public sealed class PartyRoster
{
    private readonly Dictionary<string, PartyMemberDef> _byId = new(StringComparer.Ordinal);

    /// <summary>Build over validated data; by then the ids are unique, every spread is a permutation and every weapon id resolves.</summary>
    public PartyRoster(PartyData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        All = data.Members.ToArray();
        foreach (var member in All)
            if (!_byId.TryAdd(member.Id, member))
                throw new ContentException(member.Id, ContentValidator.RuleDuplicateId);
    }

    /// <summary>Every member, in file order.</summary>
    public IReadOnlyList<PartyMemberDef> All { get; }

    public PartyMemberDef this[string id]
        => TryGet(id, out var member) ? member : throw new KeyNotFoundException($"No party member '{id}' in the roster.");

    public bool TryGet(string id, out PartyMemberDef member) => _byId.TryGetValue(id, out member!);
}
