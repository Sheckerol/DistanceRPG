using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>enchantments.json</c> (§5.7): the catalogue and the unique souls, one
/// entry an <see cref="EnchantmentDef"/> — its eleven fields under the record's
/// parameter names camel-cased, the doc's <c>unique</c> among them. Read from
/// the text a caller hands in (<c>Logic/</c> never touches the disk) by the
/// strict shared reader (<see cref="JsonShape"/>): every field is required,
/// <c>trigger</c>, <c>applies</c> and <c>damageType</c> may be null, and nothing
/// else may sit beside them — there is no <c>condition</c> field, since when an
/// entry fires is its effect kind's, not a second answer beside it (bend (c)).
/// <para>
/// <strong>The effect is named, never written</strong> (§5.7): an entry names an
/// <see cref="EffectKind"/> and supplies its parameters, so a new entry naming
/// an existing kind needs no code — and a name that is no kind is refused under
/// <see cref="ContentValidator.RuleUnknownEffect"/>, since a typo there would be
/// a silent no-op. Enum values are the C# member names, exactly. The rules an
/// entry must keep — a trigger never zero, a trigger or a status never both, the
/// unique flag against <c>neverRolled</c> — are the catalogue's
/// (<see cref="EnchantmentCatalogue"/>), run by the load at the enchantments stage.
/// </para>
/// <para>
/// <strong>One owner for a dialled number: the dial</strong> (1h). The
/// percentage of every id <see cref="Tuning.ApplyPercent"/> names, and Arcane's
/// potency (<see cref="Tuning.ArcanePotency"/>), are <c>tuning.json</c>'s: the
/// catalogue lays the dial over the row. The row keeps its field — the base the
/// validator checks, and the number for every id no dial names — and the writer
/// says, after each dialled field, which key sets it.
/// </para>
/// </summary>
public static class EnchantmentsJson
{
    public const string FileName = "enchantments.json";

    /// <summary>The one member of the file: the list of entries, named for the record.</summary>
    private const string ListName = "enchantments";

    /// <summary>An entry's fields, in the record's parameter order: the order they are written in, and the order a missing one is looked for.</summary>
    private static readonly string[] Fields =
        ["id", "name", "effect", "targets", "lock", "trigger", "potency", "applyPercent", "applies", "damageType", "unique"];

    /// <summary>
    /// Read an <c>enchantments.json</c>'s text into its entries, in file order.
    /// A failure is reported against the entry's id when it has one to read.
    /// </summary>
    /// <exception cref="ContentException">The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>), or an effect names no <see cref="EffectKind"/> (<see cref="ContentValidator.RuleUnknownEffect"/>).</exception>
    public static EnchantmentsData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, $"a table holding the {ListName} list");
        shape.Only(root, [ListName], JsonShape.Top, FileName);
        var rows = shape.Array(shape.Required(root, ListName, JsonShape.Top, FileName), ListName, FileName, "a list of entries");

        var entries = new List<EnchantmentDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{ListName}[{i}]";
            var row = shape.Object(rows[i], path, path, "an entry: a table of its fields");

            // Name the entry by its id wherever a failure in it is reported, when
            // it has one that reads; the id's own failure comes in field order.
            string entry = row.TryGetProperty("id", out var peek) && peek.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(peek.GetString()) ? peek.GetString()! : path;
            shape.Only(row, Fields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);

            entries.Add(new EnchantmentDef(
                Id: shape.Id(Get("id"), At("id"), entry),
                Name: shape.String(Get("name"), At("name"), entry, "a name in quotes"),
                Effect: shape.Enum<EffectKind>(Get("effect"), At("effect"), entry, ContentValidator.RuleUnknownEffect),
                Targets: shape.Enum<TargetSide>(Get("targets"), At("targets"), entry),
                Lock: shape.Int(Get("lock"), At("lock"), entry),
                Trigger: shape.IntOrNull(Get("trigger"), At("trigger"), entry),
                Potency: shape.Int(Get("potency"), At("potency"), entry),
                ApplyPercent: shape.Int(Get("applyPercent"), At("applyPercent"), entry),
                Applies: shape.EnumOrNull<StatusEffectType>(Get("applies"), At("applies"), entry),
                DamageType: shape.EnumOrNull<DamageType>(Get("damageType"), At("damageType"), entry),
                Unique: shape.Bool(Get("unique"), At("unique"), entry)));
        }
        return new EnchantmentsData(entries);
    }

    /// <summary>
    /// The file for <paramref name="data"/>: a header saying what the file is and
    /// how it is read, then every entry in the record's order, one field a line in
    /// the record's parameter order, with <c>/* tuning.json … sets this */</c>
    /// after each field a dial in <paramref name="dials"/> owns
    /// (<see cref="ApplyPercentDial"/>, <see cref="PotencyDial"/>). Canonical, with
    /// <c>"\n"</c> line ends, so one catalogue is one text.
    /// </summary>
    public static string Write(EnchantmentsData data, Tuning dials)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(dials);
        var text = new StringBuilder();
        JsonText.Header(text, Header);
        text.Append("{\n");
        text.Append($"  {JsonText.Quote(ListName)}: [\n");
        for (int i = 0; i < data.Enchantments.Count; i++)
        {
            var e = data.Enchantments[i];
            text.Append("    {\n");
            Field(text, "id", JsonText.Quote(e.Id));
            Field(text, "name", JsonText.Quote(e.Name));
            Field(text, "effect", JsonText.Quote(e.Effect.ToString()));
            Field(text, "targets", JsonText.Quote(e.Targets.ToString()));
            Field(text, "lock", JsonText.Number(e.Lock));
            Field(text, "trigger", e.Trigger is { } trigger ? JsonText.Number(trigger) : "null");
            Field(text, "potency", JsonText.Number(e.Potency), PotencyDial(e.Id));
            Field(text, "applyPercent", JsonText.Number(e.ApplyPercent), ApplyPercentDial(dials, e.Id));
            Field(text, "applies", e.Applies is { } status ? JsonText.Quote(status.ToString()) : "null");
            Field(text, "damageType", e.DamageType is { } type ? JsonText.Quote(type.ToString()) : "null");
            Field(text, "unique", e.Unique ? "true" : "false", last: true);
            text.Append(i < data.Enchantments.Count - 1 ? "    },\n" : "    }\n");
        }
        text.Append("  ]\n");
        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>
    /// The <c>tuning.json</c> key that owns <paramref name="id"/>'s
    /// <c>applyPercent</c> under <paramref name="dials"/> — <c>ApplyPercent.flaming</c>
    /// — or null when no dial names the id and the row's own number plays. A dial
    /// names an id when the tuning's table does or the compiled one does, which is
    /// the overlay the catalogue reads (<see cref="Tuning.Lookup"/>).
    /// </summary>
    internal static string? ApplyPercentDial(Tuning dials, string id)
    {
        ArgumentNullException.ThrowIfNull(dials);
        return dials.ApplyPercent.ContainsKey(id) || ContentDefaults.Tuning.ApplyPercent.ContainsKey(id)
            ? $"{nameof(Tuning.ApplyPercent)}.{id}"
            : null;
    }

    /// <summary>The <c>tuning.json</c> key that owns <paramref name="id"/>'s <c>potency</c> — <c>ArcanePotency</c>, for Arcane's row alone — or null.</summary>
    internal static string? PotencyDial(string id) => id == Tuning.ArcaneId ? nameof(Tuning.ArcanePotency) : null;

    /// <summary>What the file says after a field a dial owns: which key sets it.</summary>
    internal static string DialNote(string key) => $"/* {TuningJson.FileName} {key} sets this */";

    /// <summary>One field's line, with the comma JSON wants after every field but the last and, for a dialled field, the note saying which key sets it.</summary>
    private static void Field(StringBuilder text, string name, string value, string? dial = null, bool last = false)
    {
        text.Append($"      {JsonText.Quote(name)}: {value}{(last ? "" : ",")}");
        if (dial is not null)
            text.Append(' ').Append(DialNote(dial));
        text.Append('\n');
    }

    private static readonly string[] Header =
    [
        "enchantments.json - the enchantment catalogue and the unique souls, one entry each.",
        "",
        "Read once, when the game starts, after restricted.json. An entry is checked, never clamped: a",
        "field the game does not know, a value of the wrong kind, a trigger of 0, or a unique missing from",
        "restricted.json's neverRolled stops the game on a screen naming the file, the entry and the",
        "rule. Every entry names all eleven fields; comments and trailing commas are fine. An entry is",
        "an edit here, never a rebuild: start the game again to play it.",
        "",
        "id: what everything names the entry by - weapons, restricted.json, a save. name: what the screen",
        "calls it. effect: the behaviour it fires, one of the game's EffectKind names - the behaviour",
        "itself is code. targets: Ally, Enemy or Any. lock: the mana it reserves while equipped, per tier.",
        "trigger: the mana one fire costs, or null for an entry that applies a status (its price rides",
        "the levels it lands). potency: the number its effect scales. applyPercent: the share of the",
        "source that becomes status levels. applies: the status it lands, or null. damageType: the",
        "element an elemental entry carries, or null. unique: the tier is pinned at 1.",
        "",
        "A field marked \"tuning.json ... sets this\" is a number tuning.json owns: the game plays the",
        "dial there whatever the field says, and the screen says so when the two differ. Turn the dial.",
        "",
        "The copy to edit is the one beside the game (bin/Debug/net10.0/enchantments.json - the log",
        "prints its full path). GameEngine.DistanceRPG/Data/enchantments.json is generated from the",
        "compiled content (DistanceRPG --write-data <folder>) and pinned by the tests: an entry worth",
        "keeping goes into Logic/Content/ContentDefaults.cs, and the file is generated again. Deleting",
        "the copy plays the built-in catalogue, and the screen says so.",
    ];
}
