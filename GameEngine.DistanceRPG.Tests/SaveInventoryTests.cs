using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The save's inventory of live state (1b), as code: every instance field of
/// every type the save walks is listed here as saved (and where in the
/// document), derived (and from what), or not saved (and why). A field added
/// later — a counter, a cache, a flag — fails this until someone decides which
/// it is, so nothing new slips out of the save by being forgotten. Phase 5.6
/// extends it to the visit's types and 5.8 to the scenes'.
/// </summary>
public class SaveInventoryTests
{
    private enum Kept
    {
        /// <summary>Written to the document, and handed back by a load.</summary>
        Saved,

        /// <summary>Replayed on read from what is saved, or read off the content by a saved id.</summary>
        Derived,

        /// <summary>Not in the document, for the reason given.</summary>
        NotSaved,

        /// <summary>Written to the document's view — presentation the scene reads back, and <c>Logic/</c> never does (Phase 5.8).</summary>
        View,
    }

    private sealed record Row(string Member, Kept Kept, string Why);

    private static readonly Dictionary<Type, Row[]> Inventory = new()
    {
        [typeof(CampaignState)] =
        [
            new("_party", Kept.Saved, "campaign.party[], in roster order"),
            new("_seen", Kept.Saved, "campaign.seen[] = SeenInOrder — never the set's own enumeration"),
            new("RunCounter", Kept.Saved, "campaign.runCounter"),
            new("_lockedUntilRun", Kept.Saved, "campaign.locks[] = LockedInOrder, an early offer's rewrite as it stands"),
            new("_cleared", Kept.Saved, "campaign.cleared[] = ClearedInOrder"),
            new("_retired", Kept.Saved, "campaign.retired[] = RetiredInOrder"),
            new("LastResult", Kept.Saved, "campaign.lastResult: the summary over the list between runs survives a save and a load"),
            new("Visit", Kept.Saved, "campaign.visit: the Run group and every visited floor, or null between runs"),
            new("_stable", Kept.Saved, "campaign.stable[], each weapon whole, in the order they were put there — null for an empty stable (Phase 6a.2)"),
            new("_enchanter", Kept.Saved, "campaign.enchanter[], each deposit whole, in deposit order — null for an empty shop (Phase 6a.3a)"),
            new("DepositsMade", Kept.Saved, "campaign.depositsMade: the next deposit's ordinal (Phase 6a.3a)"),
            new("_onWiped", Kept.NotSaved, "wiring: the subscription Enter — or a load, through Restore — makes on a visit's Wiped, remade with the visit"),
            new("RunEnded", Kept.NotSaved, "wiring: whoever plays the campaign subscribes afresh"),
        ],
        [typeof(ActorState)] =
        [
            new("X", Kept.Saved, "body.x, the shortest text that reads back to the same float"),
            new("Y", Kept.Saved, "body.y, likewise"),
            new("_hp", Kept.Saved, "body.hp as stored (RawHp): null is unwritten, which reads as the maximum"),
            new("_mana", Kept.Saved, "body.mana as stored (RawMana): null is unwritten, which reads as the spendable ceiling"),
            new("Alive", Kept.Saved, "body.alive"),
            new("HeldShots", Kept.Saved, "body.heldShots"),
            new("StatusEffects", Kept.Saved, "body.statuses[] in landing order, OverhealPool among them — §5.1's Overheal remainder"),
            new("Innate", Kept.Saved, "a member's innate: modifiers by name (empty on every member today); an enemy's is re-derived (5.6)"),
            new("Attunement", Kept.Saved, "a member's attunement by name (none today); an enemy's is its dungeon's (5.6)"),
        ],
        [typeof(PartyMemberState)] =
        [
            new("Id", Kept.Saved, "id: the roster's stable id"),
            new("ColorIndex", Kept.Saved, "colorIndex"),
            new("Stats", Kept.Saved, "stats: the save's spread, never re-read from the starting party"),
            new("WeaponXp", Kept.Saved, "weaponXp by class name, zeros left out — never by position"),
            new("HpXp", Kept.Saved, "hpXp"),
            new("ManaXp", Kept.Saved, "manaXp"),
            new("Inventory", Kept.Saved, "inventory[], its length the count the member was built with"),
            new("DistLeft", Kept.Saved, "distLeft: a save mid-turn resumes mid-turn"),
            new("EffectiveMax", Kept.Saved, "effectiveMax"),
            new("SavedMovement", Kept.Saved, "savedMovement"),
            new("BudgetBeforeMire", Kept.Saved, "budgetBeforeMire: the whole this turn's cap was cut from"),
        ],
        [typeof(Weapon)] =
        [
            new("Id", Kept.Saved, "id: the catalogue row"),
            new("Forged", Kept.Saved, "forged, by modifier name: the identity spread a ceiling is read off"),
            new("Modifiers", Kept.Saved, "modifiers, by modifier name: the live total"),
            new("_enchantments", Kept.Saved, "enchantments[] in attachment order, (id, tier, xp) — never sorted"),
            new("ForgedEnchantmentCount", Kept.Saved, "forgedEnchantments: fixed as the weapon was made, and moved only by a transfer taking a forged entry off (Phase 6a.6), which a save of the weapon after it holds"),
            new("Wear", Kept.Saved, "wear: the wear banked since its last service (version 2)"),
            new("WearCashed", Kept.Saved, "wearCashed: the wear ever cashed, which the capacity and its progress are replayed from under the tuning loaded now (version 2)"),
            new("Enchantments", Kept.Derived, "the read-only view of _enchantments"),
            new("Name", Kept.Derived, "the row's, by id: a content edit to a statline reaches every saved copy"),
            new("Class", Kept.Derived, "the row's, by id"),
            new("Role", Kept.Derived, "the row's, by id"),
            new("Range", Kept.Derived, "the row's, by id"),
            new("Damage", Kept.Derived, "the row's, by id"),
            new("Cost", Kept.Derived, "the row's, by id"),
            new("ManaCost", Kept.Derived, "the row's, by id"),
            new("AreaShape", Kept.Derived, "the row's, by id"),
            new("Unique", Kept.Derived, "the row's, by id"),
            new("ResolvedCost", Kept.Derived, "Resolve(): the cost less Light's discount on the live spread"),
            new("ResolvedManaCost", Kept.Derived, "Resolve(): the mana cost less Resonant's"),
        ],
        [typeof(Enchantment)] =
        [
            new("Def", Kept.Saved, "id: the content's own entry by id — the tuned def, so a unique's pin holds"),
            new("_tier", Kept.Saved, "tier"),
            new("Xp", Kept.Saved, "xp: the remainder into the tier"),
        ],
        [typeof(Deposit)] =
        [
            new("Ordinal", Kept.Saved, "enchanter[].ordinal"),
            new("Weapon", Kept.Saved, "enchanter[].weapon, whole"),
            new("Service", Kept.Saved, "enchanter[].service, by name"),
            new("WearAtDeposit", Kept.Saved, "enchanter[].wearAtDeposit: what the deposit cashed, which a take-back returns"),
            new("Runs", Kept.Saved, "enchanter[].runs: fixed as it went in"),
            new("EnchantmentId", Kept.Saved, "enchanter[].enchantment, by id"),
            new("Source", Kept.Saved, "enchanter[].source, whole: a transfer's source, held in the shop as the same deposit (Phase 6a.6)"),
            new("SourceIndex", Kept.Saved, "enchanter[].sourceIndex: a transfer's soul by its place on the source, or on the weapon for a reorder (Phase 6a.6)"),
            new("RunsWorked", Kept.Saved, "enchanter[].runsWorked"),
        ],
        [typeof(ServiceReport)] =
        [
            new("WeaponId", Kept.Saved, "lastResult.services[].weaponId"),
            new("Service", Kept.Saved, "lastResult.services[].service, by name"),
            new("RunsWorked", Kept.Saved, "lastResult.services[].runsWorked: 0 on a stalled report play wrote"),
            new("Runs", Kept.Saved, "lastResult.services[].runs"),
            new("Stalled", Kept.Saved, "lastResult.services[].stalled"),
            new("EnchantmentId", Kept.Saved, "lastResult.services[].enchantment, by id"),
            new("Improved", Kept.Saved, "lastResult.services[].improved and .modifier, each by name: what the improvement roll did (Phase 6a.4)"),
            new("Tier", Kept.Saved, "lastResult.services[].tier: the tier a finished transfer's soul landed at (Phase 6a.6)"),
            new("SourceId", Kept.Saved, "lastResult.services[].sourceId: a transfer's source, by catalogue id (Phase 6a.6)"),
            new("Roll", Kept.NotSaved, "the log's, written at the run's end: the roll as it was drawn, which a save made during the run replays: it holds the visit's map seed and the queued deposit's ordinal, each named in the log where it was made (deposit #n, Phase 6a.5)"),
        ],
        [typeof(Improvement)] =
        [
            new("Kind", Kept.Saved, "lastResult.services[].improved, by name"),
            new("Modifier", Kept.Saved, "lastResult.services[].modifier, by name"),
        ],
        [typeof(WeaponXpBook)] =
        [
            new("_xp", Kept.Saved, "weaponXp: each class by name, zeros left out"),
        ],

        // ── The visit in progress (Phase 5.6) ─────────────────────────────────
        [typeof(DungeonState)] =
        [
            new("Def", Kept.Saved, "visit.dungeonId: the content's dungeon by id"),
            new("MapSeed", Kept.Saved, "visit.mapSeed: every floor, stair and stream derives from it"),
            new("BossFloor", Kept.Saved, "visit.bossFloor: the value a load uses, never rolled again through a tuning that may have changed"),
            new("PromisedStacks", Kept.Saved, "visit.promisedStacks: the value a load uses, never re-derived"),
            new("FloorIndex", Kept.Saved, "visit.floorIndex"),
            new("DeepestFloorIndex", Kept.Saved, "visit.deepestFloorIndex: floors down to it are the visited ones, and saved"),
            new("_turn", Kept.Saved, "visit.turn: the visit's clock (Turn reads it while no system is open)"),
            new("_carry", Kept.Saved, "visit.carry = CurrentCarry(): merged with the open system's counters, this turn's only, in canonical order"),
            new("BossDefeated", Kept.Saved, "visit.bossDefeated"),
            new("BossDefeatedAtTurn", Kept.Saved, "visit.bossDefeatedAtTurn"),
            new("EntrySnapshot", Kept.Saved, "visit.entrySnapshot: weapon XP by class name, HP XP, mana XP — PHASE4-114"),
            new("_spoils", Kept.Saved, "visit.spoils[] in the order taken, each a locator: a bag slot, or a floor's loose item"),
            new("Spoils", Kept.Derived, "the read-only view of _spoils"),
            new("DiceRolled", Kept.Saved, "visit.diceRolled: the visit's own die's position, so a reload rolls on exactly where play left off (Phase 5.7)"),
            new("Floors", Kept.Saved, "visit.floors[]: the visited ones restored on their regenerated terrain, the rest generated from the seed"),
            new("Turns", Kept.NotSaved, "reopened by OpenTurns on load, which imports the carry and catches the floor up as an arrival does"),
            new("FloorReached", Kept.NotSaved, "wiring: whoever plays the visit subscribes afresh"),
            new("Wiped", Kept.NotSaved, "wiring: the campaign's restore subscribes it to the run's end, as Enter does"),
        ],
        [typeof(Floor)] =
        [
            new("Index", Kept.Saved, "floors[].index"),
            new("Seed", Kept.Derived, "FloorSeed(mapSeed, index)"),
            new("Map", Kept.Derived, "regenerated from the seed: maps are never serialized (§5.1)"),
            new("FogBoxes", Kept.Derived, "regenerated with the map"),
            new("UpStairs", Kept.Derived, "regenerated: the map's start"),
            new("DownStairs", Kept.Derived, "regenerated: the stair stream's draw"),
            new("Lair", Kept.Derived, "regenerated: the stair stream's draw, on the bottom floor"),
            new("Fog", Kept.Saved, "floors[].seen (and the current floor's visible)"),
            new("Enemies", Kept.Saved, "floors[].enemies[] on a visited floor; placed afresh on one nobody has stood on"),
            new("Boss", Kept.Saved, "boss: true on that row"),
            new("Visited", Kept.Derived, "a saved floor is a visited one"),
            new("LooseItems", Kept.Saved, "floors[].looseItems[]: weapon and tile, in the order left"),
        ],
        [typeof(FogState)] =
        [
            new("Seen", Kept.Saved, "seen[]: one '0'/'1' string per map row"),
            new("Visible", Kept.Saved, "visible[] on the current floor only — the turn's sight the pick and the march read; any other floor's is reset by the arrival that next shows it"),
            new("Rows", Kept.Derived, "the map's"),
            new("Cols", Kept.Derived, "the map's"),
        ],
        [typeof(TurnCarry)] =
        [
            new("Turn", Kept.Saved, "carry.turn"),
            new("Attacks", Kept.Saved, "carry.attacks[], sorted: members in roster order, then enemies by floor and spawn"),
            new("ZoneUses", Kept.Saved, "carry.zoneUses[], sorted the same way"),
            new("ReactionUses", Kept.Saved, "carry.reactionUses[], by actor, then modifier in enum order"),
            new("Seen", Kept.Saved, "carry.seen[], sorted by floor and spawn"),
        ],
        [typeof(EnemyState)] =
        [
            new("SpawnIndex", Kept.Saved, "enemies[].spawnIndex"),
            new("Weapon", Kept.Saved, "enemies[].weapon: by id with both spreads — the boss's too, restored and never rolled again"),
            new("DefeatedAtTurn", Kept.Saved, "enemies[].defeatedAtTurn"),
            new("TurnsSinceSeen", Kept.Saved, "enemies[].turnsSinceSeen"),
            new("DefeatCount", Kept.Saved, "enemies[].defeatCount"),
            new("RevivalsRolled", Kept.Saved, "enemies[].revivalsRolled"),
            new("RevivalDamage", Kept.Saved, "enemies[].revivalDamage"),
            new("RevivalMaxHp", Kept.Saved, "enemies[].revivalMaxHp"),
            new("DropTaken", Kept.Saved, "enemies[].dropTaken"),
            new("PinnedDrop", Kept.Saved, "enemies[].groundItem: the drop an untaken permanent kill lies on, pinned as the game answered it at the save"),
            new("BaseMaxHp", Kept.Derived, "the dungeon's: a dummy's DummyHp, the boss's statline (BossRow)"),
            new("MoveBudget", Kept.Derived, "the dungeon's: a dummy's EnemyMove, the boss's statline"),
            new("Title", Kept.Derived, "the boss's name from its dungeon; none for rank and file"),
            new("Theme", Kept.Derived, "its dungeon's theme"),
            new("PromisedStacks", Kept.Derived, "the visit's promisedStacks, on the boss row only"),
        ],
        [typeof(TurnSystem)] =
        [
            new("Phase", Kept.NotSaved, "a save is taken in the player phase (SaveGame.IsSafePoint), which OpenTurns opens in"),
            new("TurnCount", Kept.Saved, "visit.turn: OpenTurns opens on it"),
            new("ResurrectionActive", Kept.Derived, "!visit.bossDefeated, set by OpenTurns"),
            new("_seenThisTurn", Kept.Saved, "carry.seen[], sorted"),
            new("_attacksThisTurn", Kept.Saved, "carry.attacks[], sorted"),
            new("_partyZone", Kept.Saved, "its uses in carry.zoneUses[], sorted; its pairs re-derived by SnapshotZones from where everyone stands, kept true continuously"),
            new("_enemyZone", Kept.Saved, "its uses in carry.zoneUses[], sorted; its pairs re-derived as the party's are"),
            new("_ripostes", Kept.Saved, "carry.reactionUses[], sorted"),
            new("_grid", Kept.Derived, "the floor's regenerated map"),
            new("_party", Kept.Derived, "the campaign's party, handed to OpenTurns"),
            new("_enemies", Kept.Derived, "the floor's rows"),
            new("_mapSeed", Kept.Derived, "the floor's seed"),
            new("_rollD20", Kept.Derived, "the visit's own die (DungeonState.RollD20), which OpenTurns hands the floor unless a die is injected — its position saved as visit.diceRolled (Phase 5.7)"),
            new("_zones", Kept.Derived, "the two zones, by side: built by the constructor"),
            new("_rosters", Kept.Derived, "built by the constructor from the party and the rows"),
            new("_sides", Kept.Derived, "built by the constructor"),
            new("_mayReact", Kept.Derived, "built by the constructor"),
            new("_braceFeeds", Kept.Derived, "built by the constructor"),
            new("_onReactionFired", Kept.Derived, "built by the constructor"),
            new("_events", Kept.Derived, "the event table: every behaviour registered by the constructor; empty outside a raise, which a safe point excludes"),
            new("_hitFeeds", Kept.Derived, "built by the constructor"),
            new("_healFeeds", Kept.Derived, "built by the constructor"),
            new("_deathFeeds", Kept.Derived, "built by the constructor"),
            new("_statusFeeds", Kept.Derived, "built by the constructor"),
            new("_movementFeeds", Kept.Derived, "built by the constructor"),
            new("_xpFeeds", Kept.Derived, "built by the constructor"),
            new("_wearFeeds", Kept.Derived, "built by the constructor"),
            new("_defeatFeeds", Kept.Derived, "built by the constructor"),
            new("_dropFeeds", Kept.Derived, "built by the constructor, the boss named by its saved row"),
            new("_heldDeaths", Kept.NotSaved, "empty outside a raise, which a safe point excludes"),
            new("_enemyIdx", Kept.NotSaved, "the enemy phase's working state: the enemy phase is never a save point"),
            new("_timer", Kept.NotSaved, "the enemy phase's working state"),
            new("_enemyBudget", Kept.NotSaved, "the enemy phase's working state"),
            new("_waypoints", Kept.NotSaved, "the enemy phase's working state"),
            new("_waypointIdx", Kept.NotSaved, "the enemy phase's working state"),
            new("_enemyTurnPending", Kept.NotSaved, "the enemy phase's working state"),
            new("_nextEnemyPending", Kept.NotSaved, "the enemy phase's working state"),
            new("TurnEnded", Kept.NotSaved, "an event: whoever plays the floor subscribes"),
            new("PlayerTurnStarted", Kept.NotSaved, "an event"),
            new("CharacterHit", Kept.NotSaved, "an event"),
            new("CharacterDied", Kept.NotSaved, "an event"),
            new("EnemyHit", Kept.NotSaved, "an event"),
            new("EnemyDefeated", Kept.NotSaved, "an event"),
            new("EnemyDropped", Kept.NotSaved, "an event"),
            new("EnemyResurrected", Kept.NotSaved, "an event"),
            new("BossDefeated", Kept.NotSaved, "an event: the visit subscribes it in OpenTurns"),
            new("BraceTriggered", Kept.NotSaved, "an event"),
            new("EnemyBraceTriggered", Kept.NotSaved, "an event"),
            new("CharacterBuffed", Kept.NotSaved, "an event"),
            new("CharacterHealed", Kept.NotSaved, "an event"),
            new("EnemyBuffed", Kept.NotSaved, "an event"),
            new("EnemyHealed", Kept.NotSaved, "an event"),
            new("EnemyFleeing", Kept.NotSaved, "an event"),
            new("ActorStatusTicked", Kept.NotSaved, "an event"),
            new("ActorStatusApplied", Kept.NotSaved, "an event"),
            new("ActorDisplaced", Kept.NotSaved, "an event"),
            new("OpportunistTriggered", Kept.NotSaved, "an event"),
            new("OverwatchTriggered", Kept.NotSaved, "an event"),
            new("RiposteTriggered", Kept.NotSaved, "an event"),
            new("XpCredited", Kept.NotSaved, "an event"),
            new("EnchantmentTiered", Kept.NotSaved, "an event"),
            new("WeaponWornFull", Kept.NotSaved, "an event"),
            new("GameOver", Kept.NotSaved, "an event: the visit subscribes it in OpenTurns"),
        ],
    };

    // ── The scenes (Phase 5.8) ───────────────────────────────────────────────
    //
    // A scene is a view of the campaign, and almost everything in one is rebuilt
    // by showing it: the floor's objects and caches by the arrival lifecycle, the
    // list by the campaign. What a save must carry for a scene is in the
    // document's view — the active member, whether the turn marches — and the
    // turn's own sight is the floor's (visible[]). The rest is named here, each
    // with its reason, so a scene field added later fails until someone decides.

    private static readonly Dictionary<Type, Row[]> SceneInventory = new()
    {
        [typeof(DungeonScene)] =
        [
            // Saved in the view: the two decisions a turn has made that a resume must not make again.
            new("_activeIdx", Kept.View, "view.activeMemberId: the member by roster id, read back on a resume (the first living member if it has fallen)"),
            new("_marchingThisTurn", Kept.View, "view.marchingThisTurn: decided at a turn's start or a stairs arrival and read back on a resume, never decided afresh — it gates the march and the turn's automatic end"),

            // Wiring: what the scene was built with.
            new("_camera", Kept.NotSaved, "wiring: the game's camera"),
            new("_game", Kept.NotSaved, "wiring: the game window"),
            new("_hud", Kept.NotSaved, "presentation: the HUD, built with the scene"),
            new("_campaign", Kept.Derived, "the campaign the save restores — the scene is a view of it"),
            new("_visit", Kept.Derived, "the campaign's visit, restored by the save"),
            new("_host", Kept.NotSaved, "wiring: the game"),
            new("_resume", Kept.NotSaved, "what the host hands a resuming scene: the save's view and the launch's notices, read on the first arrival"),

            // The logic clock.
            new("_logicClock", Kept.NotSaved, "the clock's remainder, under one fixed step: a save is taken at a player-phase safe point, where the turn system's step does nothing, and a settle runs whole steps"),

            // The floor being played: rebuilt by the arrival lifecycle from what the visit holds.
            new("_floor", Kept.Derived, "the visit's current floor, taken up by the arrival"),
            new("_map", Kept.Derived, "the floor's regenerated map"),
            new("_fogBoxes", Kept.Derived, "the floor's regenerated fog boxes"),
            new("_fog", Kept.Derived, "the floor's fog, its seen[] and the current floor's visible[] saved"),
            new("_fogOverlay", Kept.NotSaved, "presentation: rebuilt by the arrival"),
            new("_turns", Kept.Derived, "the system OpenTurns opens on the arrival, the turn's counters imported from the save's carry"),
            new("_floorObjects", Kept.NotSaved, "presentation: rebuilt by the arrival"),
            new("_stairMarkers", Kept.NotSaved, "presentation: rebuilt by the arrival from the regenerated stairs"),
            new("_mistEmitter", Kept.NotSaved, "presentation: fog animation"),
            new("_wispEmitter", Kept.NotSaved, "presentation: fog animation"),
            new("_pendingMistBursts", Kept.NotSaved, "presentation: fog animation"),
            new("_fxRng", Kept.NotSaved, "presentation: the effects' own die, which decides nothing"),
            new("_wispTimer", Kept.NotSaved, "presentation: fog animation"),
            new("_party", Kept.NotSaved, "presentation: the party's stand-ins, rebuilt from the campaign's members"),
            new("_lastFogTile", Kept.NotSaved, "rebuilt by the arrival: every living member's tile, which a resume lights from"),
            new("_enemies", Kept.NotSaved, "presentation: the enemies' stand-ins, rebuilt from the floor's rows"),
            new("_lastEnemyTile", Kept.NotSaved, "the enemy-phase sighting's tiles: taken afresh at every turn's end (TakeEnemyTiles), so a resumed turn measures the enemies' walk as the session that saved it would have"),
            new("_groundItems", Kept.NotSaved, "presentation: the markers, laid again by the arrival — a lying drop from its row's pin, a loose item from the floor's list"),

            // The march: presentation, rebuilt the way every march (re-)entry builds it.
            new("_march", Kept.NotSaved, "a resumed march starts a fresh line: the followers tuck in behind the leader, as every march entry and leader switch does"),
            new("_marchLeader", Kept.NotSaved, "a resumed march starts a fresh line"),
            new("_wasMarching", Kept.NotSaved, "a resumed march starts a fresh line"),

            // The stairs' fade, the run's end, and leaving play.
            new("_transit", Kept.NotSaved, "a stairs fade in flight: a quit mid-fade saves whichever floor the Logic stands on"),
            new("_transitClock", Kept.NotSaved, "a stairs fade in flight"),
            new("_transitStair", Kept.NotSaved, "a stairs fade in flight"),
            new("_runOverSent", Kept.NotSaved, "the run's end is the campaign's (lastResult): saved between runs, and shown by the screen between runs"),
            new("_endedRun", Kept.NotSaved, "the run's end is the campaign's (lastResult): saved between runs, and shown by the screen between runs"),
            new("_leaving", Kept.NotSaved, "play has been left from the pause menu, for the title or the window's close: the scene is on its way out, never a save point's concern"),

            // Input, the camera and the menus.
            new("_up", Kept.NotSaved, "input: a key held"),
            new("_down", Kept.NotSaved, "input: a key held"),
            new("_left", Kept.NotSaved, "input: a key held"),
            new("_right", Kept.NotSaved, "input: a key held"),
            new("_mousePos", Kept.NotSaved, "input: the mouse"),
            new("_cameraTarget", Kept.NotSaved, "presentation: the camera, aimed at the active member on the arrival"),
            new("_cameraDistance", Kept.NotSaved, "presentation: the camera's zoom"),
            new("_activeMenu", Kept.NotSaved, "a menu: a save is never taken on the run's summary, and a resume opens on none"),
            new("_leaveArmed", Kept.NotSaved, "a menu: the bag's X, disarmed whenever the bag is not open"),
            new("_answer", Kept.NotSaved, "a menu: what the pause menu answers under its lines, until the next key or until the menu closes"),
            new("LastBankedMovement", Kept.NotSaved, "presentation: the END OF TURN banner's figure"),
            new("HoveredEnemy", Kept.NotSaved, "presentation: the hover, refreshed every frame"),
            new("HoveredMember", Kept.NotSaved, "presentation: the hover"),
            new("HoverCue", Kept.NotSaved, "presentation: the hover"),
            new("AimCue", Kept.NotSaved, "presentation: the hover"),
            new("AimCaught", Kept.NotSaved, "presentation: the hover"),

            // The d20 cue bookkeeping: which rolls the cues read — the die itself is the visit's, and saved (diceRolled).
            new("_lastRoll", Kept.NotSaved, "the d20 cues' bookkeeping: presentation — the die is the visit's own, its position saved as visit.diceRolled"),
            new("_rollCount", Kept.NotSaved, "the d20 cues' bookkeeping"),
            new("_castRollCalledAt", Kept.NotSaved, "the d20 cues' bookkeeping"),
            new("_watchCastRoll", Kept.NotSaved, "the d20 cues' bookkeeping: true only inside a cast"),
            new("_castRoll", Kept.NotSaved, "the d20 cues' bookkeeping"),
            new("_castRollAt", Kept.NotSaved, "the d20 cues' bookkeeping"),
            new("_riderLevels", Kept.NotSaved, "the rider beats' bookkeeping: presentation, taken afresh at every roll"),
        ],
        [typeof(BetweenRunsScene)] =
        [
            new("_game", Kept.NotSaved, "wiring: the game window, whose close a dev-override session's QUIT GAME is"),
            new("_campaign", Kept.Derived, "the campaign the save restores — the scene is a view of it"),
            new("_host", Kept.NotSaved, "wiring: the game"),
            new("_hud", Kept.NotSaved, "presentation: the HUD, built with the scene"),
            new("_listings", Kept.Derived, "the campaign's list (CampaignState.Listings), read when the screen is made"),
            new("_chosen", Kept.NotSaved, "a load opens on the campaign's default choice, as every return from a run does"),
            new("_leaving", Kept.NotSaved, "the screen is on its way out: never a save point's concern"),
            new("_menu", Kept.NotSaved, "a menu: the one ESC opens over the hub, closed on every showing"),
            new("_answer", Kept.NotSaved, "a menu: what the menu answers under its lines, until the next key or until the menu closes"),
            new("NoticeRows", Kept.NotSaved, "this showing's notices: the log keeps them, and a load has its own to say"),
            new("_notices", Kept.NotSaved, "this showing's notices, a failed save of the hub's laid ahead of them: the log keeps them"),
            new("_shownWhole", Kept.NotSaved, "how many of this showing's notices are shown whole"),
            new("_weapons", Kept.NotSaved, "the WEAPONS page: its rows are the campaign's, read afresh; its choice, its window and a carry are presentation (HubWeapons, below)"),
            new("_onWeapons", Kept.NotSaved, "which page is up: every showing opens on DUNGEONS"),
            new("_refusal", Kept.NotSaved, "what the last key was refused with, until the next key"),
            new("_panel", Kept.NotSaved, "the deposit panel or the take-back ask (Phase 6a.5): the screen's alone, like a carry — the campaign changes only at the ENTER that deposits or takes back, so a save never sees one, and every showing opens with none up (DepositPanel, below)"),
        ],
        [typeof(HubWeapons)] =
        [
            new("_campaign", Kept.Derived, "the campaign the save restores — the page's rows are read off it afresh each time"),
            new("_chosen", Kept.NotSaved, "the page's choice: every showing opens on the first row it can choose"),
            new("_top", Kept.NotSaved, "the window's first row: presentation, kept to the choice"),
            new("_carried", Kept.NotSaved, "a carry is the page's alone and moves nothing: the campaign changes only as it is put down (CampaignState.Move), so a save never sees one, and a quit mid-carry loses nothing"),
        ],
        [typeof(DepositPanel)] =
        [
            new("Weapon", Kept.Derived, "the weapon the panel is about: the campaign's, where the save keeps it — in a bag, the stable or the shop"),
            new("From", Kept.Derived, "where the weapon lay as the panel opened, as the campaign has it — none on a take-back's ask — and asked again at the ENTER (DepositPanel.RefusalNow)"),
            new("TakingBack", Kept.Derived, "the deposit a take-back asks about: the campaign's shop, saved"),
            new("_services", Kept.Derived, "the services as the enchanter answers them (Enchanter.ServiceRefusal), asked of the campaign as the panel opens"),
            new("_offers", Kept.Derived, "the offer as the enchanter answers it (Enchanter.Offers), asked of the campaign as the panel opens"),
            new("_moves", Kept.Derived, "the souls a MOVE could take as the enchanter answers them (Enchanter.TransferOffers), asked of the campaign as the panel opens, and asked again at the ENTER (Phase 6a.6)"),
            new("_step", Kept.NotSaved, "which question is up: a panel is the screen's alone, and nothing is deposited until the confirm's ENTER"),
            new("_service", Kept.NotSaved, "the service chosen: nothing is deposited until the confirm's ENTER"),
            new("_chosen", Kept.NotSaved, "the offer or the soul chosen: nothing is deposited until the confirm's ENTER"),
            new("_top", Kept.NotSaved, "the offer list's or the source list's window: presentation, kept to the choice"),
        ],
    };

    /// <summary>A field's member name: an auto-property's backing field is its property, a field-like event its event.</summary>
    private static string MemberOf(FieldInfo field)
    {
        string name = field.Name;
        return name.StartsWith('<') && name.EndsWith(">k__BackingField", StringComparison.Ordinal) ? name[1..name.IndexOf('>')] : name;
    }

    [Fact]
    public void EveryFieldIsSavedDerivedOrNamedAsNot()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var (type, rows) in Inventory)
        {
            var fields = type.GetFields(Declared).Select(MemberOf).Order(StringComparer.Ordinal).ToArray();
            var listed = rows.Select(row => row.Member).Order(StringComparer.Ordinal).ToArray();
            Assert.True(listed.Distinct().Count() == listed.Length, $"{type.Name} lists a member twice");
            Assert.True(fields.SequenceEqual(listed),
                $"{type.Name}: every instance field is saved, derived or named as not. Unlisted: [{string.Join(", ", fields.Except(listed))}]; listed but gone: [{string.Join(", ", listed.Except(fields))}]");
            Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Why), $"{type.Name}.{row.Member} says neither where nor why"));
        }

        // The scan reads real fields, the raw pools among them: the ones a save
        // must keep below the public state, which reads them as their ceilings —
        // and, from the visit, the pin under a corpse and the turn's counters.
        Assert.Contains("_hp", typeof(ActorState).GetFields(Declared).Select(MemberOf));
        Assert.Contains("BudgetBeforeMire", typeof(PartyMemberState).GetFields(Declared).Select(MemberOf));
        Assert.Contains("PinnedDrop", typeof(EnemyState).GetFields(Declared).Select(MemberOf));
        Assert.Contains("_carry", typeof(DungeonState).GetFields(Declared).Select(MemberOf));
    }

    [Fact]
    public void EverySceneFieldIsViewDerivedOrNamedAsNot()
    {
        // 1b's scene rows, as code (Phase 5.8): every instance field of the two
        // scenes — and of the WEAPONS page the screen between runs holds (Phase
        // 6a.2), and of its deposit panel (Phase 6a.5) — is in the document's view,
        // derived from what the save restores, or not saved for the reason given — so
        // a scene field added later fails here until someone decides whether a resume
        // needs it.
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var (type, rows) in SceneInventory)
        {
            var fields = type.GetFields(Declared).Select(MemberOf).Order(StringComparer.Ordinal).ToArray();
            var listed = rows.Select(row => row.Member).Order(StringComparer.Ordinal).ToArray();
            Assert.True(listed.Distinct().Count() == listed.Length, $"{type.Name} lists a member twice");
            Assert.True(fields.SequenceEqual(listed),
                $"{type.Name}: every instance field is in the view, derived or named as not saved. Unlisted: [{string.Join(", ", fields.Except(listed))}]; listed but gone: [{string.Join(", ", listed.Except(fields))}]");
            Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Why), $"{type.Name}.{row.Member} says neither where nor why"));
            Assert.DoesNotContain(rows, row => row.Kept == Kept.Saved);   // a scene's is the view, never the document's state
        }

        // The view is the two decisions 1b names, and ViewDto holds exactly them: the
        // document's view has a member for each scene field kept in it, and the
        // gameplay types keep nothing in it.
        Assert.Equal(["_activeIdx", "_marchingThisTurn"],
            SceneInventory.Values.SelectMany(rows => rows).Where(row => row.Kept == Kept.View).Select(row => row.Member).Order(StringComparer.Ordinal));
        Assert.Equal(["ActiveMemberId", "MarchingThisTurn"], typeof(ViewDto).GetConstructors().Single().GetParameters().Select(p => p.Name));
        Assert.DoesNotContain(Inventory.Values.SelectMany(rows => rows), row => row.Kept == Kept.View);
    }
}
