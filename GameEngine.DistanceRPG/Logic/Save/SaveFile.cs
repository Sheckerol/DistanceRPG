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
/// <param name="SavedAt">
/// When the save was written (the user's ruling of 2026-09-25 on the save model,
/// R2): the UTC instant as round-trip text (<c>"O"</c>,
/// <c>2026-09-25T14:32:00.0000000Z</c>), handed in by the edge — <c>Logic/</c>
/// never reads the clock (<see cref="SaveGame.Capture"/>) — or null for a save
/// that records none, as every save written before it does. The most recent
/// save is the one with the latest time. A save of the campaign a slot already
/// holds writes nothing (<see cref="SaveCodec.Untimed"/>), so a save keeps the
/// time its state was first written — but for a save by hand, which rewrites
/// this time alone (the user's ruling of 2026-09-25 on the save slots, R5). Raw
/// text a hand can edit, so a restore
/// refuses one that is no such time (<see cref="SaveGame.SavedAtOf"/>). Appended
/// by Phase 5.S5, with its default, last: a document written before it reads as
/// a save with no time.
/// </param>
public sealed record SaveFile(int Version, string TuningHash, CampaignDto Campaign, ViewDto? View = null, string? SavedAt = null);

/// <summary>
/// Presentation the document carries for the scene, which <c>Logic/</c> never
/// reads: which member was active, and whether the turn marches. A scene
/// resuming the visit reads both back rather than deciding them afresh, since
/// both are decisions the turn already made.
/// </summary>
/// <param name="ActiveMemberId">The active member's roster id, or null for the scene's own default.</param>
/// <param name="MarchingThisTurn">
/// Whether the turn in progress was granted the march at its start or on its
/// arrival (Phase 5.8). The scene decides it only there, and it changes nothing
/// after, so a resume reads it back: decided afresh, a turn the party marched
/// could come back halted, or one a fight had split back marching. A live
/// enemy seen this turn still halts the march, from the turn's own counters
/// (<see cref="CarryDto.Seen"/>). Appended by Phase 5.8, with its default.
/// </param>
public sealed record ViewDto(string? ActiveMemberId = null, bool MarchingThisTurn = false);

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
/// <param name="Visit">
/// The visit in progress (<see cref="CampaignState.Visit"/>): §5.1's Run group and
/// its visited floors — or null between runs, which is a whole save (§6.7).
/// Appended by Phase 5.6, so it carries its default and a document written
/// before it reads as a campaign between runs.
/// </param>
public sealed record CampaignDto(IReadOnlyList<MemberDto> Party, IReadOnlyList<string> Seen, int RunCounter, IReadOnlyList<LockDto> Locks,
    IReadOnlyList<string> Cleared, IReadOnlyList<string> Retired, RunResultDto? LastResult, VisitDto? Visit = null);

/// <summary>A cleared dungeon's lock: the run it can be entered again from (§4.5).</summary>
public sealed record LockDto(string DungeonId, int LockedUntilRun);

/// <summary>
/// One party member (§5.1's Party group): who it is, the spread it was built
/// with — the save's own, never re-read from the starting party — its three XP
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

// ── The visit in progress (§5.1's Run group and its visited floors) ─────────

/// <summary>
/// The visit in progress (<see cref="DungeonState"/>): §5.1's Run group — the
/// map seed, the floor the party is on, the turn, the party's XP at the door —
/// with everything else a load must use as it was rather than work out again —
/// the depth, the promise, where the visit's die stands — and every floor the
/// party has stood on. Maps are never here (§5.1: "they
/// regenerate from <c>floorSeed(n)</c>"): a load builds every floor's terrain
/// from <see cref="MapSeed"/>, stands the saved rows on the visited ones, and
/// places enemies only on the floors nobody has reached (§5.1's "place enemies
/// only on the first visit, restore saved states afterwards").
/// </summary>
/// <param name="DungeonId">The dungeon, by its content id.</param>
/// <param name="MapSeed">The seed the visit was entered on: every floor, stair and stream derives from it (PHASE5-003).</param>
/// <param name="BossFloor">
/// The boss floor, a 1-based floor number and the dungeon's depth — the value a
/// load uses, never rolled again, so a tuning edit since moves no depth (§4.3:
/// "a tuning change during the visit moves neither").
/// </param>
/// <param name="PromisedStacks">The stacks the boss's drop was promised at entry — the value a load uses, never re-derived through a tuning that may have changed.</param>
/// <param name="FloorIndex">The 0-based floor the party is on (PHASE5-004).</param>
/// <param name="DeepestFloorIndex">The deepest floor the party has stood on: floors are adjacent only, so every floor down to it is a visited one, and the document holds exactly those.</param>
/// <param name="Turn">The visit's one clock (PHASE5-005).</param>
/// <param name="BossDefeated">Whether the boss has fallen: from then on nothing revives, and every corpse lies on its drop.</param>
/// <param name="BossDefeatedAtTurn">The turn it fell, or -1 while it stands.</param>
/// <param name="EntrySnapshot">The party's XP as it stood at the door (PHASE5-006, PHASE4-114): what a wipe claws back against.</param>
/// <param name="Spoils">
/// This visit's spoils in the order taken (<see cref="DungeonState.Spoils"/>),
/// each by where it is now — a bag slot, or lying on a floor — so a load hands
/// back the very weapon that is there, and a spoil put down stays the spoil it was.
/// </param>
/// <param name="Carry">The counters of the turn in progress (<see cref="TurnCarry"/>), this turn's only, in one canonical order; null when there are none.</param>
/// <param name="Floors">Every visited floor, index 0 first, down to <see cref="DeepestFloorIndex"/>.</param>
/// <param name="DiceRolled">
/// How many d20s the visit's own die has rolled (<see cref="DungeonState.DiceRolled"/>):
/// the die's position, the one stream position a save holds — so the next roll
/// after a load is the roll play without the load would have made next, and a
/// reload is never a reroll. Appended by Phase 5.7, with its default: a document
/// written before it reads as a die not yet rolled.
/// </param>
public sealed record VisitDto(string DungeonId, long MapSeed, int BossFloor, int PromisedStacks, int FloorIndex, int DeepestFloorIndex, int Turn,
    bool BossDefeated, int BossDefeatedAtTurn, XpSnapshotDto EntrySnapshot, IReadOnlyList<SpoilDto> Spoils, CarryDto? Carry, IReadOnlyList<FloorDto> Floors,
    int DiceRolled = 0);

/// <summary>
/// One visited floor (§5.1's per-floor group): what a visit changed there, and
/// nothing a seed gives back — the enemy rows, the explored fog, the weapons
/// the party put down — and, on the floor the party stands on, what it can see
/// this turn.
/// </summary>
/// <param name="Index">The 0-based floor index: its seed is <c>floorSeed(mapSeed, index)</c>.</param>
/// <param name="Enemies">Every row in spawn order, the boss's among them on the bottom floor (PHASE5-020–022).</param>
/// <param name="Seen">The explored fog (PHASE5-023): one string per map row, <c>'1'</c> for a tile ever seen and <c>'0'</c> for one never seen.</param>
/// <param name="LooseItems">The weapons put down here, in the order they were left: each whole, and the tile it lies on.</param>
/// <param name="Visible">
/// What the party can see this turn — every box a living member has stood in
/// since the turn began — in <see cref="Seen"/>'s encoding, on the current floor
/// only; null on every other floor, whose sight is reset by the arrival that
/// next shows it. Appended by Phase 5.6, with its default.
/// </param>
public sealed record FloorDto(int Index, IReadOnlyList<EnemyDto> Enemies, IReadOnlyList<string> Seen, IReadOnlyList<LooseItemDto> LooseItems,
    IReadOnlyList<string>? Visible = null);

/// <summary>
/// One enemy row as a visit left it (§5.1, §3.2): its body, what it holds — the
/// boss's too, restored and never rolled again — and the farm's whole history
/// as accumulated numbers, never a stream position. Its statline, its theme and
/// its attunement are its dungeon's and come back from the content; a boss's
/// promise is the visit's.
/// </summary>
/// <param name="SpawnIndex">Its index in the floor's spawn list: what seeds its drop and its revival ladder.</param>
/// <param name="Boss">Whether this row is the floor's boss (the bottom floor's alone).</param>
/// <param name="Body">Position, the raw pools, alive, held shots and statuses.</param>
/// <param name="Weapon">What it holds, by id with both spreads (PHASE5-022, -134): never a position in a file.</param>
/// <param name="DefeatedAtTurn">The turn it last fell, or -1 if it never has.</param>
/// <param name="TurnsSinceSeen">Turns since a member last saw it.</param>
/// <param name="DefeatCount">How many times it has been put down.</param>
/// <param name="RevivalsRolled">Revival cycles already applied.</param>
/// <param name="RevivalDamage">Damage the revival ladder has accumulated (PHASE5-021).</param>
/// <param name="RevivalMaxHp">Maximum HP the revival ladder has accumulated (PHASE5-021).</param>
/// <param name="DropTaken">Whether its drop has been taken off the floor.</param>
/// <param name="GroundItem">
/// The drop it lies on, pinned: written only for a row lying on an untaken drop
/// — the boss down, the row dead, the drop not taken — as the game answered it
/// at the save, so the weapon a player may already have seen is the one there
/// after a load, whatever the tuning or the content says by then. Null on every
/// other row. Appended by Phase 5.6, with its default.
/// </param>
public sealed record EnemyDto(int SpawnIndex, bool Boss, ActorDto Body, WeaponDto Weapon, int DefeatedAtTurn, int TurnsSinceSeen,
    int DefeatCount, int RevivalsRolled, int RevivalDamage, int RevivalMaxHp, bool DropTaken, DropDto? GroundItem = null);

/// <summary>The drop an untaken permanent kill lies on, pinned (<see cref="Drop"/>): the weapon, and whether the unique roll won. Its farm depth is the row's.</summary>
public sealed record DropDto(WeaponDto Weapon, bool WasUniqueRoll);

/// <summary>A weapon the party put down (<see cref="LooseItem"/>): the weapon whole, and the tile it lies on — row and column.</summary>
public sealed record LooseItemDto(WeaponDto Weapon, int Row, int Col);

/// <summary>
/// Where a spoil is (<see cref="DungeonState.Spoils"/>): in a member's bag —
/// <see cref="Member"/> and <see cref="Slot"/> — or lying on a floor —
/// <see cref="Floor"/> and <see cref="Loose"/>, its place in that floor's loose
/// items. Exactly one of the two.
/// </summary>
public sealed record SpoilDto(string? Member = null, int? Slot = null, int? Floor = null, int? Loose = null);

/// <summary>An actor in the turn's counters: a member by roster id, or an enemy by its floor's index and its spawn index. Exactly one of the two.</summary>
public sealed record ActorRefDto(string? Member = null, int? Floor = null, int? Spawn = null);

/// <summary>
/// The counters of the turn in progress (<see cref="TurnCarry"/>): the attacks
/// chosen, the threat-zone uses, the reaction uses and the enemies seen, all of
/// them this turn's. The live counters are keyed by reference in hash
/// containers, whose order says nothing, so every list here is written in one
/// canonical order: members in roster order, then enemies by floor and spawn
/// index, and a reaction's uses by its actor and then its modifier, in
/// <see cref="ModifierType"/> order. One turn is one text, however it was played.
/// </summary>
/// <param name="Turn">The visit's turn these counters were spent in: the visit's own.</param>
/// <param name="Attacks">Attacks chosen per actor — what Charges caps.</param>
/// <param name="ZoneUses">Threat-zone uses per reactor, either side's.</param>
/// <param name="ReactionUses">Riposte-style reaction uses per actor and modifier.</param>
/// <param name="Seen">The enemies a member has laid eyes on this turn.</param>
public sealed record CarryDto(int Turn, IReadOnlyList<CountDto> Attacks, IReadOnlyList<CountDto> ZoneUses, IReadOnlyList<ReactionUseDto> ReactionUses,
    IReadOnlyList<ActorRefDto> Seen);

/// <summary>A count spent by one actor this turn.</summary>
public sealed record CountDto(ActorRefDto Actor, int Count);

/// <summary>A reaction's uses spent by one actor this turn: the actor, the modifier by name, and how many.</summary>
public sealed record ReactionUseDto(ActorRefDto Actor, string Modifier, int Count);

/// <summary>The party's XP as it stood at the door (<see cref="XpSnapshot"/>): each member's three pools, in roster order.</summary>
public sealed record XpSnapshotDto(IReadOnlyList<MemberXpDto> Members);

/// <summary>One member's pools at the door (<see cref="MemberXp"/>): weapon XP by class name, a class at zero left out, then HP XP and mana XP.</summary>
public sealed record MemberXpDto(string MemberId, IReadOnlyDictionary<string, int> WeaponXp, int HpXp, int ManaXp);
