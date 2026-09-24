namespace GameEngine.DistanceRPG.Logic;

// The save document (§5.1, §5.2): a DTO layer of plain values — ids for
// content, names for enums, numbers for the rest — which is what lets the
// gameplay types stay free of serialisation attributes (§5.2: "or Logic/ stops
// being clean"). Every attribute sits on SaveJsonContext; SaveMapper walks the
// live state into these records and back through the types' internal restore
// doors, so no gameplay type grows a public setter for a load's sake.
//
// Members serialise camel-cased in declaration order. A member a later step
// appends carries a default and sits last in its record: a missing member is
// an error (RespectRequiredConstructorParameters), a defaulted one is not, so
// every save an earlier build of the same version wrote still reads.

/// <summary>
/// One campaign, saved (§5.1): the version of this format it was written in,
/// the hash of the tuning it was played under, and the campaign — the outer
/// document, a visit in progress inside it (§6.7, settled.md's Phase 4 pass:
/// the campaign holds the visit). Between runs there is no visit, and that is a
/// whole save.
/// </summary>
/// <param name="Version">The format's version (<see cref="SaveCodec.CurrentVersion"/> when written): a newer one is refused, an older one migrated (<see cref="SaveCodec.Read"/>).</param>
/// <param name="TuningHash">
/// <see cref="TuningJson.Hash"/> of the tuning the campaign was played under —
/// the hash and never the values (§5.3: "the hash costs nothing and answers the
/// only question that matters: are these the same numbers?"). A load under
/// another tuning warns and plays on (<see cref="LoadedCampaign.TuningChanged"/>).
/// </param>
/// <param name="Campaign">The campaign: the party and everything a run's end wrote.</param>
/// <param name="View">What the scene was showing, which it reads back and <c>Logic/</c> never does; null for nothing to say.</param>
public sealed record SaveFile(int Version, string TuningHash, CampaignDto Campaign, ViewDto? View = null);

/// <summary>
/// Presentation the document carries for the scene, which <c>Logic/</c> never
/// reads: which member was active. Phase 5.8 appends whether the turn marches.
/// </summary>
/// <param name="ActiveMemberId">The active member's roster id, or null for the scene's own default.</param>
public sealed record ViewDto(string? ActiveMemberId = null);

/// <summary>
/// The campaign (§6.7): what outlasts every visit. Every list is in the order
/// the campaign hands it out — the party in roster order, and the sets and the
/// locks sorted by id with the ordinal comparer — never a hash container's
/// order, so one campaign is one text.
/// </summary>
/// <param name="Party">The members, in roster order (<see cref="CampaignState.Party"/>).</param>
/// <param name="Seen">Every enchantment id the campaign has met, ordinal-sorted (<see cref="CampaignState.SeenInOrder"/>): ids, never positions in a file (§5.7).</param>
/// <param name="RunCounter">The runs that have counted (<see cref="CampaignState.RunCounter"/>).</param>
/// <param name="Locks">Every lock a clear wrote, by dungeon id (<see cref="CampaignState.LockedInOrder"/>) — an early offer's rewrite as it stands.</param>
/// <param name="Cleared">Every dungeon cleared at least once (<see cref="CampaignState.ClearedInOrder"/>).</param>
/// <param name="Retired">Every dungeon retired for good (<see cref="CampaignState.RetiredInOrder"/>).</param>
/// <param name="LastResult">How the last run ended — the summary the screen between runs shows over its list — or null before any has.</param>
public sealed record CampaignDto(IReadOnlyList<MemberDto> Party, IReadOnlyList<string> Seen, int RunCounter, IReadOnlyList<LockDto> Locks,
    IReadOnlyList<string> Cleared, IReadOnlyList<string> Retired, RunResultDto? LastResult);

/// <summary>A cleared dungeon's lock: the run it can be entered again from (§4.5).</summary>
public sealed record LockDto(string DungeonId, int LockedUntilRun);

/// <summary>
/// One party member (§5.1's Party group): who it is, the spread it was built
/// with — the save's own, never re-read from <c>party.json</c> — its three XP
/// pools, its body, what it carries and where its turn's movement stands.
/// </summary>
/// <param name="Id">The roster's stable id.</param>
/// <param name="ColorIndex">The member's colour, by its place in the roster when it was built.</param>
/// <param name="Stats">The innate spread (§2.1).</param>
/// <param name="WeaponXp">Cumulative weapon XP by class name, in <see cref="WeaponClass"/> order, a class at zero left out — by name, never by position.</param>
/// <param name="HpXp">Cumulative HP XP.</param>
/// <param name="ManaXp">Cumulative mana XP.</param>
/// <param name="Innate">The modifiers the member carries whatever it holds, by name — empty on every member today.</param>
/// <param name="Attunement">The damage type the member is attuned to, by name, or null — none today.</param>
/// <param name="Body">Position, the raw pools, and the statuses.</param>
/// <param name="Inventory">Every slot, the empty ones as null, its length the count the member was built with: a changed carry never resizes a bag.</param>
/// <param name="DistLeft">Movement left this turn, in logic units.</param>
/// <param name="EffectiveMax">This turn's cap.</param>
/// <param name="SavedMovement">What the last turn's end banked.</param>
/// <param name="BudgetBeforeMire">The whole this turn's cap was cut from (<see cref="PartyMemberState.BudgetBeforeMire"/>).</param>
public sealed record MemberDto(string Id, int ColorIndex, StatsDto Stats, IReadOnlyDictionary<string, int> WeaponXp, int HpXp, int ManaXp,
    IReadOnlyDictionary<string, int> Innate, string? Attunement, ActorDto Body, IReadOnlyList<WeaponDto?> Inventory,
    float DistLeft, float EffectiveMax, float SavedMovement, float BudgetBeforeMire);

/// <summary>An innate spread (§2.1), each stat by name.</summary>
public sealed record StatsDto(int Str, int Dex, int Con, int Int);

/// <summary>
/// What every actor shares (<see cref="ActorState"/>): where it stands, in logic
/// units, written as the shortest text that reads back to the same float; its
/// pools as stored — null for a pool nobody has written, which reads as its
/// ceiling, a different state from one written full; and its statuses in the
/// order they landed.
/// </summary>
/// <param name="X">The circle centre's x, in logic units.</param>
/// <param name="Y">The circle centre's y, in logic units.</param>
/// <param name="Hp">Hit points as written, or null: unwritten reads as the maximum.</param>
/// <param name="Mana">Mana as written, or null: unwritten reads as the spendable ceiling, wherever the locks move it.</param>
/// <param name="Alive">Whether it stands.</param>
/// <param name="HeldShots">Overwatch's banked shots.</param>
/// <param name="Statuses">One entry per (type, element), in landing order — the hidden <see cref="StatusEffectType.OverhealPool"/> among them: §5.1's Overheal remainder.</param>
public sealed record ActorDto(float X, float Y, int? Hp, int? Mana, bool Alive, int HeldShots, IReadOnlyList<StatusDto> Statuses);

/// <summary>A status on an actor: its type and its element by name (the element only on a type keyed on one), and its levels.</summary>
public sealed record StatusDto(string Type, string? Element, int Levels);

/// <summary>
/// A weapon as an item: its catalogue id — the statline, class and shape come
/// back from the content by it, so an edit to a row reaches every saved copy —
/// and what is the item's own: <strong>both</strong> its forged and its live
/// modifier sets (§5.1), by modifier name in <see cref="ModifierType"/> order,
/// and its enchantments in attachment order, which is gameplay and never
/// sorted (settled.md: "a migration that rebuilds it in a different sequence is
/// a silent balance change").
/// </summary>
/// <param name="Id">The catalogue id.</param>
/// <param name="Forged">The identity spread, fixed when the item was made: its ceiling is read off it.</param>
/// <param name="Modifiers">The live total, forged plus acquired.</param>
/// <param name="Enchantments">The attached entries, in attachment order.</param>
/// <param name="ForgedEnchantments">How many of them, from the front, are forged.</param>
public sealed record WeaponDto(string Id, IReadOnlyDictionary<string, int> Forged, IReadOnlyDictionary<string, int> Modifiers,
    IReadOnlyList<EnchantmentDto> Enchantments, int ForgedEnchantments);

/// <summary>An attached enchantment: its catalogue id, the tier it is on, and the XP into that tier (§3.3: "a saved instance is two ints").</summary>
public sealed record EnchantmentDto(string Id, int Tier, int Xp);

/// <summary>
/// How the last run ended (<see cref="RunResult"/>): the facts its summary
/// shows between runs, the outcome by name and its spoils as weapon rows —
/// copies, since only what they are is read, never where they went.
/// </summary>
public sealed record RunResultDto(string DungeonId, string Outcome, bool BossDefeated, int DeepestFloor, int? BossFloorRevealed, bool ServiceTick,
    bool MercyTick, int RunCounter, int? LockedUntilRun, bool Retired, IReadOnlyList<WeaponDto> SpoilsKept, IReadOnlyList<WeaponDto> SpoilsLost,
    IReadOnlyList<ClawbackDto> Clawbacks);

/// <summary>What a wipe took back from one pool (<see cref="XpClawback"/>): the member by id, the pool by name, what it held and what it holds.</summary>
public sealed record ClawbackDto(string MemberId, string Pool, int Before, int After);
