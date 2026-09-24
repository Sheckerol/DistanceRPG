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
            new("LastResult", Kept.Saved, "campaign.lastResult: the summary over the list between runs survives a relaunch"),
            new("Visit", Kept.Saved, "campaign.visit: the Run group and every visited floor, or null between runs"),
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
            new("Stats", Kept.Saved, "stats: the save's spread, never re-read from party.json"),
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
            new("ForgedEnchantmentCount", Kept.Saved, "forgedEnchantments"),
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
            new("_rollD20", Kept.NotSaved, "the die is handed in by whoever opens the floor (the visit's own saved die is Phase 5.7's)"),
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
            new("GameOver", Kept.NotSaved, "an event: the visit subscribes it in OpenTurns"),
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
}
