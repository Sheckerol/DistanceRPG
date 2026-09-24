using System.Text;
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
/// </summary>
public static class DungeonsJson
{
    public const string FileName = "dungeons.json";

    /// <summary>The one member of the file: the list of rows, named for the record.</summary>
    private const string ListName = "dungeons";

    /// <summary>A row's fields, in the record's parameter order — §5.8's names: the order they are written in, and the order a missing one is looked for.</summary>
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

    /// <summary>
    /// The file for <paramref name="data"/>: a header saying what the file is and
    /// how it is read, then every row in the record's order, one field a line in
    /// the record's parameter order, the boss's fields indented under it, with
    /// <c>/* provisional */</c> after the id of a row — a dungeon or a boss — the
    /// docs did not fix (<see cref="JsonText.IdNote"/>). Canonical, with
    /// <c>"\n"</c> line ends, so one set of dungeons is one text.
    /// </summary>
    public static string Write(DungeonsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = new StringBuilder();
        JsonText.Header(text, Header);
        text.Append("{\n");
        text.Append($"  {JsonText.Quote(ListName)}: [\n");
        for (int i = 0; i < data.Dungeons.Count; i++)
        {
            var d = data.Dungeons[i];
            const string row = "      ", boss = "        ";
            text.Append("    {\n");
            Field(text, row, "id", JsonText.Quote(d.Id), JsonText.IdNote(d.Id));
            Field(text, row, "name", JsonText.Quote(d.Name));
            Field(text, row, "theme", JsonText.Quote(d.Theme.ToString()));
            Field(text, row, "attunement", d.Attunement is { } element ? JsonText.Quote(element.ToString()) : "null");
            Field(text, row, "bossFloorMin", d.BossFloorMin is { } min ? JsonText.Number(min) : "null");
            Field(text, row, "bossFloorMax", d.BossFloorMax is { } max ? JsonText.Number(max) : "null");
            text.Append($"{row}\"boss\": {{\n");
            Field(text, boss, "id", JsonText.Quote(d.Boss.Id), JsonText.IdNote(d.Boss.Id));
            Field(text, boss, "name", JsonText.Quote(d.Boss.Name));
            Field(text, boss, "maxHp", JsonText.Number(d.Boss.MaxHp));
            Field(text, boss, "movementBudget", JsonText.Number(d.Boss.MovementBudget));
            Field(text, boss, "innate", JsonText.Modifiers(d.Boss.Innate), last: true);
            text.Append($"{row}}},\n");
            Field(text, row, "retiresOnClear", d.RetiresOnClear ? "true" : "false", last: true);
            text.Append(i < data.Dungeons.Count - 1 ? "    },\n" : "    }\n");
        }
        text.Append("  ]\n");
        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>One field's line at <paramref name="indent"/>, with the comma JSON wants after every field but the last, and <paramref name="note"/> after it when there is one.</summary>
    private static void Field(StringBuilder text, string indent, string name, string value, string? note = null, bool last = false)
    {
        text.Append($"{indent}{JsonText.Quote(name)}: {value}{(last ? "" : ",")}");
        if (note is not null)
            text.Append(' ').Append(note);
        text.Append('\n');
    }

    private static readonly string[] Header =
    [
        "dungeons.json - the dungeons the screen between runs lists, in its order, each with its boss.",
        "",
        "Read once, when the game starts, after weapons.json. A dungeon is checked, never clamped: a",
        "theme that is no class's signature, a boss floor range that is inverted or deep enough to pay",
        "a farm's stacks, a boss without its theme, two dungeons that retire on clear, or a field the",
        "game does not know stops the game on a screen naming the file, the dungeon and the rule. Every",
        "dungeon names all eight fields, and its boss all five; comments and trailing commas are fine.",
        "A dungeon is an edit here, never a rebuild: start the game again to play it.",
        "",
        "id: what everything names the dungeon by - a lock, a save. name: what the screen calls it.",
        "theme: a modifier - the boss carries it whatever it holds and forges it onto its drop, and every",
        "other drop there may roll it; a class's signature, so the dungeon has one unique to hunt.",
        "attunement: the damage type everything it spawns is attuned to (Flaming, Cold, Shocking or",
        "Acidic), or null. bossFloorMin, bossFloorMax: the floors the boss is rolled between, or null",
        "for tuning.json's BossFloorMin and BossFloorMax. boss: its id, name, maxHp, movementBudget (in",
        "logic units a turn, 32 a tile) and innate modifiers, each to its stack count, the theme among",
        "them. retiresOnClear: beating the boss closes the dungeon for good - the tutorial's, and only",
        "one dungeon may.",
        "",
        "There is no drop table: what a boss drops is computed from its theme and restricted.json, so a",
        "relation edited there moves it with no edit here. A dungeon or a boss whose id is marked",
        "provisional is a first guess the docs did not fix, expected to move after play.",
        "",
        "The copy to edit is the one beside the game (bin/Debug/net10.0/dungeons.json - the log prints",
        "its full path). GameEngine.DistanceRPG/Data/dungeons.json is generated from the compiled",
        "content (DistanceRPG --write-data <folder>) and pinned by the tests: a dungeon worth keeping",
        "goes into Logic/Content/ContentDefaults.cs, and the file is generated again. Deleting the copy",
        "plays the built-in dungeons, and the screen says so.",
    ];
}
