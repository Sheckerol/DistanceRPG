using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>dungeons.json</c> (§5.8): the dungeons the screen between runs lists, one
/// row a <see cref="DungeonDef"/> under the doc's own names — <c>theme</c>,
/// <c>attunement</c>, <c>bossFloorMin</c>/<c>bossFloorMax</c>, <c>boss</c> and
/// <c>retiresOnClear</c> — the boss a <see cref="BossDef"/>: "statline plus
/// innate modifiers". Read from the text a caller hands in (<c>Logic/</c> never
/// touches the disk) by the strict shared reader (<see cref="JsonShape"/>): every
/// field is required, <c>attunement</c> and the two floors may be null, and
/// nothing else may sit beside them.
/// <para>
/// <strong>There is no drop table</strong> (§5.8, PHASE5-164): what a boss drops
/// is computed from its theme and the relations
/// (<see cref="DungeonCatalogue.ThemeClasses"/>), so a <c>dropTable</c> member is
/// refused as one the shape does not have — "authoring it by hand would let the
/// two disagree" — and a <c>restricted.json</c> edit moves a table with no edit
/// here. A theme and a boss's innate modifiers are <see cref="ModifierType"/>
/// names, exactly — a name that is none is an id that resolves to nothing
/// (<see cref="ContentValidator.RuleUnknownId"/>) — and an attunement a
/// <see cref="DamageType"/> name or null. A null floor reads the tuning's range
/// (<see cref="Tuning.BossFloorMin"/>, <see cref="Tuning.BossFloorMax"/>). What
/// makes a row legal — a theme that is a class signature with a unique to hunt,
/// a floor range the tutorial pins, one dungeon that retires — is the
/// validator's (<see cref="ContentValidator.ValidateDungeons"/>), at the
/// dungeons' stage of the load. A failure inside a boss is reported against the
/// dungeon that names it, as the validator reports one.
/// </para>
/// <para>
/// The shipped <c>GameEngine.DistanceRPG/Data/dungeons.json</c> is the master
/// copy of every dungeon (the user's ruling of 2026-09-25): hand-kept, with the
/// notes on each boss written beside it; the game carries it built in and plays
/// that copy when none is beside the executable. No dungeon is kept in code.
/// </para>
/// </summary>
public static class DungeonsJson
{
    public const string FileName = "dungeons.json";

    /// <summary>The one member of the file: the list of rows, named for the record.</summary>
    private const string ListName = "dungeons";

    /// <summary>A row's fields, in the record's parameter order — §5.8's names: the order the file holds them in, and the order a missing one is looked for.</summary>
    private static readonly string[] Fields =
        ["id", "name", "theme", "attunement", "bossFloorMin", "bossFloorMax", "boss", "retiresOnClear"];

    /// <summary>A boss's fields, in the record's parameter order.</summary>
    private static readonly string[] BossFields = ["id", "name", "maxHp", "movementBudget", "innate"];

    /// <summary>
    /// Read a <c>dungeons.json</c>'s text into its rows, in file order. A failure
    /// inside a row — its boss's included — is reported against the dungeon's id
    /// when it has one to read.
    /// </summary>
    /// <exception cref="ContentException">The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>), or a theme or an innate modifier names no <see cref="ModifierType"/> (<see cref="ContentValidator.RuleUnknownId"/>).</exception>
    public static DungeonsData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, $"a table holding the {ListName} list");
        shape.Only(root, [ListName], JsonShape.Top, FileName);
        var rows = shape.Array(shape.Required(root, ListName, JsonShape.Top, FileName), ListName, FileName, "a list of dungeons");

        var dungeons = new List<DungeonDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{ListName}[{i}]";
            var row = shape.Object(rows[i], path, path, "a dungeon: a table of its fields");
            string entry = JsonShape.EntryOf(row, path);
            shape.Only(row, Fields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);

            dungeons.Add(new DungeonDef(
                Id: shape.Id(Get("id"), At("id"), entry),
                Name: shape.String(Get("name"), At("name"), entry, "a name in quotes"),
                Theme: shape.Enum<ModifierType>(Get("theme"), At("theme"), entry, ContentValidator.RuleUnknownId),
                Attunement: shape.EnumOrNull<DamageType>(Get("attunement"), At("attunement"), entry),
                BossFloorMin: shape.IntOrNull(Get("bossFloorMin"), At("bossFloorMin"), entry),
                BossFloorMax: shape.IntOrNull(Get("bossFloorMax"), At("bossFloorMax"), entry),
                Boss: Boss(shape, Get("boss"), At("boss"), entry),
                RetiresOnClear: shape.Bool(Get("retiresOnClear"), At("retiresOnClear"), entry)));
        }
        return new DungeonsData(dungeons);
    }

    /// <summary>A dungeon's boss: its id, name, statline and innate modifiers, every field required.</summary>
    private static BossDef Boss(JsonShape shape, JsonElement value, string path, string entry)
    {
        var boss = shape.Object(value, path, entry, "a boss: a table of its fields");
        shape.Only(boss, BossFields, path, entry);

        JsonElement Get(string name) => shape.Required(boss, name, path, entry);
        string At(string name) => JsonShape.Member(path, name);

        return new BossDef(
            Id: shape.Id(Get("id"), At("id"), entry),
            Name: shape.String(Get("name"), At("name"), entry, "a name in quotes"),
            MaxHp: shape.Int(Get("maxHp"), At("maxHp"), entry),
            MovementBudget: shape.Int(Get("movementBudget"), At("movementBudget"), entry),
            Innate: shape.Modifiers(Get("innate"), At("innate"), entry, "a table of modifiers to stack counts"));
    }
}
