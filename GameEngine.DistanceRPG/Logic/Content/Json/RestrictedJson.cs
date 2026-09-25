using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>restricted.json</c> (§5.5): what may coexist on a weapon and what may be
/// rolled, for modifiers and enchantments alike — <see cref="RestrictedData"/>'s
/// five keys under the doc's own names, <c>excludes</c>, <c>requires</c>,
/// <c>kind</c>, <c>forgedOnly</c> and <c>neverRolled</c>. Read from the text a
/// caller hands in (<c>Logic/</c> never touches the disk) by the strict shared
/// reader (<see cref="JsonShape"/>): every key is required, nothing else may sit
/// beside them, and every id is a string. Whether an id resolves, a group
/// excludes itself or a <c>requires</c> loops is the validator's
/// (<see cref="ContentValidator.ValidateRestricted"/>), run by the load at the
/// relations' stage, against every modifier and every id of the game's
/// enchantment catalogue (<see cref="EnchantmentDefinitions"/>, code).
/// <para>
/// Groups are lists rather than pairs (§5.5): a group is one line, and the
/// loader closes it into its directed exclusions both ways
/// (<see cref="ModifierRules.Symmetric"/>). <c>kind</c>'s values are
/// <see cref="RestrictedData.MeleeKind"/>, <see cref="RestrictedData.RangedKind"/>
/// and <see cref="RestrictedData.CasterKind"/>, exactly; any other text reads,
/// and the validator refuses it under <see cref="ContentValidator.RuleKindValue"/>.
/// </para>
/// </summary>
public static class RestrictedJson
{
    public const string FileName = "restricted.json";

    /// <summary>The file's five keys, in §5.5's order: the order it is written in, and the order a failure is looked for.</summary>
    private static readonly string[] Keys = ["excludes", "requires", "kind", "forgedOnly", "neverRolled"];

    /// <summary>
    /// Read a <c>restricted.json</c>'s text into the relations it declares.
    /// </summary>
    /// <exception cref="ContentException">The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>): not JSON, not a table of the five keys, a key missing, or a value of the wrong kind — named by its path in the file.</exception>
    public static RestrictedData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, "a table of the five relations");
        shape.Only(root, Keys, JsonShape.Top, FileName);

        var groups = shape.Array(shape.Required(root, "excludes", JsonShape.Top, "excludes"), "excludes", "excludes", "a list of groups");
        var excludes = new List<string[]>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
            excludes.Add(shape.Ids(groups[i], $"excludes[{i}]", "excludes", "a group: a list of ids"));

        var requiresTable = shape.Object(shape.Required(root, "requires", JsonShape.Top, "requires"), "requires", "requires",
            "a table of each id to the ids it needs");
        var requires = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (id, needs) in shape.Members(requiresTable, "requires", "requires"))
            requires[id] = shape.Ids(needs, $"requires.{id}", id, "a list of the ids it needs");

        var kindTable = shape.Object(shape.Required(root, "kind", JsonShape.Top, "kind"), "kind", "kind",
            "a table of each id to melee, ranged or caster");
        var kind = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, value) in shape.Members(kindTable, "kind", "kind"))
            kind[id] = shape.String(value, $"kind.{id}", id, "melee, ranged or caster in quotes");

        string[] forgedOnly = shape.Ids(shape.Required(root, "forgedOnly", JsonShape.Top, "forgedOnly"), "forgedOnly", "forgedOnly", "a list of ids");
        string[] neverRolled = shape.Ids(shape.Required(root, "neverRolled", JsonShape.Top, "neverRolled"), "neverRolled", "neverRolled", "a list of ids");
        return new RestrictedData(excludes, requires, kind, forgedOnly, neverRolled);
    }

    /// <summary>
    /// The file for <paramref name="data"/>: a header saying what the file is and
    /// how it is read, then the five keys in §5.5's order, each under a comment
    /// saying what it holds. Canonical, so one record is one text:
    /// <c>requires</c> and <c>kind</c> in ordinal key order, the lists in the
    /// record's own order, one group or row a line, <c>"\n"</c> line ends.
    /// </summary>
    public static string Write(RestrictedData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = new StringBuilder();
        JsonText.Header(text, Header);
        text.Append("{\n");

        JsonText.Comment(text, KeyNotes["excludes"], Indent);
        text.Append($"{Indent}\"excludes\": ").Append(Lines(data.Excludes.Select(JsonText.Ids))).Append(",\n\n");

        JsonText.Comment(text, KeyNotes["requires"], Indent);
        text.Append($"{Indent}\"requires\": ")
            .Append(Rows(data.Requires.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{JsonText.Quote(r.Key)}: {JsonText.Ids(r.Value)}")))
            .Append(",\n\n");

        JsonText.Comment(text, KeyNotes["kind"], Indent);
        text.Append($"{Indent}\"kind\": ")
            .Append(Rows(data.Kind.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{JsonText.Quote(k.Key)}: {JsonText.Quote(k.Value)}")))
            .Append(",\n\n");

        JsonText.Comment(text, KeyNotes["forgedOnly"], Indent);
        text.Append($"{Indent}\"forgedOnly\": {JsonText.Ids(data.ForgedOnly)},\n\n");

        JsonText.Comment(text, KeyNotes["neverRolled"], Indent);
        text.Append($"{Indent}\"neverRolled\": {JsonText.Ids(data.NeverRolled)}\n");

        text.Append("}\n");
        return text.ToString();
    }

    private const string Indent = "  ";

    /// <summary>A list whose items are each a line of their own, or <c>[]</c> for none.</summary>
    private static string Lines(IEnumerable<string> items)
    {
        var all = items.ToList();
        return all.Count == 0 ? "[]" : "[\n" + string.Join(",\n", all.Select(item => Indent + Indent + item)) + $"\n{Indent}]";
    }

    /// <summary>A table whose rows are each a line of their own, or <c>{}</c> for none.</summary>
    private static string Rows(IEnumerable<string> rows)
    {
        var all = rows.ToList();
        return all.Count == 0 ? "{}" : "{\n" + string.Join(",\n", all.Select(row => Indent + Indent + row)) + $"\n{Indent}}}";
    }

    /// <summary>What each key holds, written above it: §5.5's table, in the file's words.</summary>
    private static readonly IReadOnlyDictionary<string, string> KeyNotes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["excludes"] = "Exclusion groups: every id in a group excludes every other, both ways - a group of three is six "
            + "exclusions - so a group is one line and cannot be half-declared. The threat zone, displacement, block response, "
            + "crit rider and currency groups, then the opposed damage types: an element is opposed by exactly one other.",
        ["requires"] = "Dependencies, one way: an id to the ids it cannot exist without on the same weapon - Riposte and "
            + "BlockWeaken need Block, Rout needs Cleave, and a lingering element needs its element ahead of it.",
        ["kind"] = "Weapon-type restrictions: a modifier to melee, ranged or caster, in lower case. A modifier not named "
            + "here goes on any kind of weapon. Modifiers only: an enchantment here stops the game.",
        ["forgedOnly"] = "Deepened where it is already forged, never added from zero - a safety rule: Charges granted to a "
            + "bow would be a cap where there was none. Each must be a modifier forged on some weapon; an enchantment "
            + "here stops the game.",
        ["neverRolled"] = "Never granted by any roll, and never copied - an economy rule: the unique enchantments. Every "
            + "enchantment the game's catalogue marks unique is here, and nothing else is.",
    };

    private static readonly string[] Header =
    [
        "restricted.json - what may sit together on a weapon, and what may be rolled, for modifiers and",
        "enchantments alike. An id is a modifier's name (Block, CritWindow) or an enchantment's id from",
        "the game's enchantment catalogue (flaming, burning); kind and forgedOnly name modifiers alone.",
        "",
        "Read once, when the game starts, and checked before anything else is: an id that names nothing, a",
        "group naming an id twice, a requires that loops or that asks for what the same id excludes stops",
        "the game on a screen naming the file, the entry and the rule - and so does a weapon or an",
        "enchantment an edit here makes illegal. Unlike tuning.json nothing here is clamped: a key the",
        "game does not know, or a value of the wrong kind, stops the game too. Comments and trailing",
        "commas are fine. A relation is an edit here, never a rebuild: start the game again to play it.",
        "",
        "The copy to edit is the one beside the game (bin/Debug/net10.0/restricted.json - the log prints",
        "its full path). GameEngine.DistanceRPG/Data/restricted.json is generated from the compiled",
        "content (DistanceRPG --write-data <folder>) and pinned by the tests: a relation worth keeping",
        "goes into Logic/Content/ContentDefaults.cs, and the file is generated again. Deleting the copy",
        "plays the built-in relations, and the screen says so.",
    ];
}
