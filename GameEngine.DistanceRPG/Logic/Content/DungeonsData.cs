namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A dungeon's boss as §5.8 describes it: "statline plus innate modifiers".
/// It wields a weapon like a dummy does, rolled at entry from what its
/// dungeon's theme can be forged onto
/// (<see cref="DungeonCatalogue.WieldClasses"/>), so no weapon is named here.
/// </summary>
/// <param name="Id">The stable string id the boss is referred to by.</param>
/// <param name="Name">The name its plate reads.</param>
/// <param name="MaxHp">Its hit points; at least 1 (<see cref="ContentValidator.RuleBossStatline"/>).</param>
/// <param name="MovementBudget">Its movement per turn, in logic units (32 per tile) — the golem's 40 against a dummy's 100 is what "slow" means; at least 1.</param>
/// <param name="Innate">
/// Modifiers carried on the actor rather than on its weapon (§4.3): the theme
/// among them, whatever it holds. Innate is not forged — it raises no weapon's
/// ceiling, its own included — so nothing a weapon's forge bounds bounds it;
/// only a stack that would do nothing (<see cref="ModifierRules.NeverStacked"/>)
/// or a count below 1 is refused.
/// </param>
public sealed record BossDef(string Id, string Name, int MaxHp, int MovementBudget, IReadOnlyDictionary<ModifierType, int> Innate);

/// <summary>
/// One row of §5.8 <c>dungeons.json</c>: name, theme, attunement, the boss's
/// floor range, the boss, and the flag the tutorial needs. What is not here is
/// as deliberate as what is: the drop table is computed from the theme and the
/// relations and never authored (<see cref="DungeonCatalogue.ThemeClasses"/>);
/// depth is a range rolled at entry, never a stored floor; and the cooldown is
/// <c>BossFloor × CooldownRunsPerFloor</c>, one rule for every dungeon rather
/// than a field of each.
/// </summary>
/// <param name="Id">The stable string id everything refers to the dungeon by — the doc's <c>DungeonId</c>, a string as every content id is.</param>
/// <param name="Name">The display name.</param>
/// <param name="Theme">
/// The one guaranteed modifier (§4.3): innate on the boss, forged onto the
/// boss's drop, and in the roll pool of every other drop in the dungeon. A
/// class signature, so the dungeon has a unique to hunt
/// (<see cref="ContentValidator.RuleThemeIsClassSignature"/>).
/// </param>
/// <param name="Attunement">
/// The damage type everything the dungeon spawns is attuned to, or none (§5.8).
/// Its own field and independent of <paramref name="Theme"/>: a modifier theme
/// does not attune anything by itself.
/// </param>
/// <param name="BossFloorMin">The shallowest boss floor, a 1-based floor number; null reads <see cref="Tuning.BossFloorMin"/>.</param>
/// <param name="BossFloorMax">The deepest boss floor; null reads <see cref="Tuning.BossFloorMax"/>. The tutorial pins both to 2.</param>
/// <param name="Boss">The boss's statline and innate modifiers.</param>
/// <param name="RetiresOnClear">
/// Beating the boss removes the dungeon for good rather than locking it
/// (§4.3's tutorial): exactly one dungeon carries it, and it pins its floor.
/// </param>
public sealed record DungeonDef(string Id, string Name, ModifierType Theme, DamageType? Attunement,
    int? BossFloorMin, int? BossFloorMax, BossDef Boss, bool RetiresOnClear);

/// <summary>
/// The §5.8 <c>dungeons.json</c> shape: the dungeon rows in file order. Phase 4
/// ships a compiled instance (<see cref="ContentDefaults.Dungeons"/>); Phase 5
/// adds only deserialisation.
/// </summary>
public sealed record DungeonsData(IReadOnlyList<DungeonDef> Dungeons);
