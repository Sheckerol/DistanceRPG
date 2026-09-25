using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>weapons.json</c> (§5.6): the thirty-two variants and the uniques, one row
/// a <see cref="WeaponDef"/> — its thirteen fields under the record's parameter
/// names camel-cased — "statline, forged spread, area shape, innate enchantment
/// id". Read from the text a caller hands in (<c>Logic/</c> never touches the
/// disk) by the strict shared reader (<see cref="JsonShape"/>): every field is
/// required, <c>role</c>, <c>shape</c> and <c>derivedFrom</c> may be null, and
/// nothing else may sit beside them.
/// <para>
/// <strong>Everything content is an id, everything code is a name</strong>: a
/// weapon is named everywhere by its <c>id</c>, an enchantment it arrives with
/// by the id the game's enchantment catalogue gives it (code:
/// <see cref="EnchantmentDefinitions"/>), and a modifier in its forged
/// spread by its <see cref="ModifierType"/> name, exactly — a name that is no
/// modifier is an id that resolves to nothing
/// (<see cref="ContentValidator.RuleUnknownId"/>). <c>class</c>, <c>role</c> and a
/// shape's <c>kind</c> are the C# member names, exactly. A spread is written in
/// <see cref="ModifierType"/> order, an enchantment's tier only where it is not
/// 1, a shape's dimensions only where they are not 0, and each of those reads
/// back as it was left out.
/// </para>
/// <para>
/// <strong>No class baselines</strong> (bend (b), USER DECISION 4): every row
/// carries its whole forged spread, and a class's baseline is what its four
/// variants share — derived and checked (<see cref="ContentValidator.RuleClassBaseline"/>),
/// never a section of the file, so a spread has one source and nothing can
/// disagree with it. What makes a row legal is the validator's, at the weapons'
/// stage of the load: the relations of <c>restricted.json</c> asked through
/// <see cref="ModifierRules.Allowed"/> — a refusal names the relation
/// (<see cref="ContentValidator.WhyNotAllowed"/>) — the forge limits, the unique
/// derivation, the casters' innates.
/// </para>
/// </summary>
public static class WeaponsJson
{
    public const string FileName = "weapons.json";

    /// <summary>The one member of the file: the list of rows, named for the record.</summary>
    private const string ListName = "weapons";

    /// <summary>A row's fields, in the record's parameter order: the order they are written in, and the order a missing one is looked for.</summary>
    private static readonly string[] Fields =
        ["id", "name", "class", "role", "range", "damage", "cost", "manaCost", "forged", "enchantments", "shape", "unique", "derivedFrom"];

    /// <summary>An enchantment a weapon arrives with: its id, and its tier when that is not 1.</summary>
    private static readonly string[] ReferenceFields = ["id", "tier"];

    /// <summary>A shape's members: its kind, then every dimension <see cref="AreaShape"/> has, in the record's order.</summary>
    private static readonly string[] ShapeFields = ["kind", "radius", "length", "width", "angleDegrees", "targetReach"];

    /// <summary>
    /// The dimensions each kind of shape measures (§1.4, <see cref="AreaShape"/>'s
    /// factories): a dimension a kind does not use is 0, so a file giving one any
    /// other number on that kind is naming a number nothing reads, and is refused.
    /// </summary>
    private static readonly IReadOnlyDictionary<AreaShapeKind, string[]> Dimensions = new Dictionary<AreaShapeKind, string[]>
    {
        [AreaShapeKind.Blast] = ["radius", "targetReach"],
        [AreaShapeKind.Cone] = ["length", "angleDegrees"],
        [AreaShapeKind.Beam] = ["length", "width"],
        [AreaShapeKind.Nova] = ["radius"],
    };

    /// <summary>
    /// Read a <c>weapons.json</c>'s text into its rows, in file order. A failure
    /// inside a row is reported against the row's id when it has one to read.
    /// </summary>
    /// <exception cref="ContentException">The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>), or a forged modifier names no <see cref="ModifierType"/> (<see cref="ContentValidator.RuleUnknownId"/>).</exception>
    public static WeaponsData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, $"a table holding the {ListName} list");
        shape.Only(root, [ListName], JsonShape.Top, FileName);
        var rows = shape.Array(shape.Required(root, ListName, JsonShape.Top, FileName), ListName, FileName, "a list of weapons");

        var weapons = new List<WeaponDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{ListName}[{i}]";
            var row = shape.Object(rows[i], path, path, "a weapon: a table of its fields");
            string entry = JsonShape.EntryOf(row, path);
            shape.Only(row, Fields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);

            weapons.Add(new WeaponDef(
                Id: shape.Id(Get("id"), At("id"), entry),
                Name: shape.String(Get("name"), At("name"), entry, "a name in quotes"),
                Class: shape.Enum<WeaponClass>(Get("class"), At("class"), entry),
                Role: shape.EnumOrNull<VariantRole>(Get("role"), At("role"), entry),
                Range: shape.Int(Get("range"), At("range"), entry),
                Damage: shape.Int(Get("damage"), At("damage"), entry),
                Cost: shape.Int(Get("cost"), At("cost"), entry),
                ManaCost: shape.Int(Get("manaCost"), At("manaCost"), entry),
                Forged: shape.Modifiers(Get("forged"), At("forged"), entry, "a table of modifiers to stack counts"),
                Enchantments: References(shape, Get("enchantments"), At("enchantments"), entry),
                Shape: Shape(shape, Get("shape"), At("shape"), entry),
                Unique: shape.Bool(Get("unique"), At("unique"), entry),
                DerivedFrom: shape.IdOrNull(Get("derivedFrom"), At("derivedFrom"), entry)));
        }
        return new WeaponsData(weapons);
    }

    /// <summary>The enchantments a weapon arrives with, in attachment order — which is gameplay, and never reordered (§1.7): each an id, and a tier that is 1 when left out.</summary>
    private static List<EnchantmentRef> References(JsonShape shape, JsonElement value, string path, string entry)
    {
        var items = shape.Array(value, path, entry, "a list of the enchantments it arrives with");
        var references = new List<EnchantmentRef>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            string at = $"{path}[{i}]";
            var item = shape.Object(items[i], at, entry, "an enchantment: a table of its id and, when it is not 1, its tier");
            shape.Only(item, ReferenceFields, at, entry);
            string id = shape.Id(shape.Required(item, "id", at, entry), JsonShape.Member(at, "id"), entry);
            int tier = shape.Optional(item, "tier") is { } given ? shape.Int(given, JsonShape.Member(at, "tier"), entry) : 1;
            references.Add(new EnchantmentRef(id, tier));
        }
        return references;
    }

    /// <summary>
    /// A wand's area, or null: its kind, and the dimensions that kind measures,
    /// each 0 when left out. A dimension the kind does not measure may be written
    /// as the 0 it is, and as nothing else.
    /// </summary>
    private static AreaShape? Shape(JsonShape shape, JsonElement value, string path, string entry)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        var table = shape.Object(value, path, entry, "null, or a shape: a table of its kind and dimensions");
        shape.Only(table, ShapeFields, path, entry);
        var kind = shape.Enum<AreaShapeKind>(shape.Required(table, "kind", path, entry), JsonShape.Member(path, "kind"), entry);

        var measured = Dimensions[kind];
        foreach (var member in table.EnumerateObject())
        {
            if (member.Name == "kind" || measured.Contains(member.Name, StringComparer.Ordinal))
                continue;
            string at = JsonShape.Member(path, member.Name);
            if (shape.Int(member.Value, at, entry) != 0)
                throw shape.Fail(entry, at, $"is not a dimension of a {kind} - a {kind} measures {string.Join(" and ", measured)}");
        }

        int Dimension(string name) => shape.Optional(table, name) is { } given ? shape.Int(given, JsonShape.Member(path, name), entry) : 0;
        return new AreaShape(kind, Radius: Dimension("radius"), Length: Dimension("length"), Width: Dimension("width"),
            AngleDegrees: Dimension("angleDegrees"), TargetReach: Dimension("targetReach"));
    }

    /// <summary>
    /// The file for <paramref name="data"/>: a header saying what the file is and
    /// how it is read, then every row in the record's order, one field a line in
    /// the record's parameter order — a class's variants under the class's name,
    /// the uniques under theirs — with <c>/* provisional */</c> after the id of a
    /// row the docs did not fix (<see cref="JsonText.IdNote"/>). A spread, an
    /// enchantment list and a shape each sit on the one line. Canonical, with
    /// <c>"\n"</c> line ends, so one catalogue is one text.
    /// </summary>
    public static string Write(WeaponsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = new StringBuilder();
        JsonText.Header(text, Header);
        text.Append("{\n");
        text.Append($"  {JsonText.Quote(ListName)}: [\n");
        string? heading = null;
        for (int i = 0; i < data.Weapons.Count; i++)
        {
            var w = data.Weapons[i];
            string group = w.Unique ? "The uniques" : w.Class.ToString();
            if (group != heading)
            {
                text.Append($"    /* ---- {group} ---- */\n");
                heading = group;
            }
            text.Append("    {\n");
            Field(text, "id", JsonText.Quote(w.Id), JsonText.IdNote(w.Id));
            Field(text, "name", JsonText.Quote(w.Name));
            Field(text, "class", JsonText.Quote(w.Class.ToString()));
            Field(text, "role", w.Role is { } role ? JsonText.Quote(role.ToString()) : "null");
            Field(text, "range", JsonText.Number(w.Range));
            Field(text, "damage", JsonText.Number(w.Damage));
            Field(text, "cost", JsonText.Number(w.Cost));
            Field(text, "manaCost", JsonText.Number(w.ManaCost));
            Field(text, "forged", JsonText.Modifiers(w.Forged));
            Field(text, "enchantments", References(w.Enchantments));
            Field(text, "shape", w.Shape is { } area ? Shape(area) : "null");
            Field(text, "unique", w.Unique ? "true" : "false");
            Field(text, "derivedFrom", w.DerivedFrom is { } variant ? JsonText.Quote(variant) : "null", last: true);
            text.Append(i < data.Weapons.Count - 1 ? "    },\n" : "    }\n");
        }
        text.Append("  ]\n");
        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>An enchantment list on one line: <c>[{ "id": "serrated" }, { "id": "vampiric", "tier": 3 }]</c>, a tier of 1 left out; or <c>[]</c>.</summary>
    private static string References(IReadOnlyList<EnchantmentRef> references)
        => $"[{string.Join(", ", references.Select(r => r.Tier == 1
            ? $"{{ \"id\": {JsonText.Quote(r.Id)} }}"
            : $"{{ \"id\": {JsonText.Quote(r.Id)}, \"tier\": {JsonText.Number(r.Tier)} }}"))}]";

    /// <summary>A shape on one line: its kind, then every dimension that is not 0, in the record's order.</summary>
    private static string Shape(AreaShape area)
    {
        var members = new List<string> { $"\"kind\": {JsonText.Quote(area.Kind.ToString())}" };
        foreach (var (name, value) in new[]
        {
            ("radius", area.Radius), ("length", area.Length), ("width", area.Width),
            ("angleDegrees", area.AngleDegrees), ("targetReach", area.TargetReach),
        })
            if (value != 0)
                members.Add($"{JsonText.Quote(name)}: {JsonText.Number(value)}");
        return $"{{ {string.Join(", ", members)} }}";
    }

    /// <summary>One field's line, with the comma JSON wants after every field but the last, and <paramref name="note"/> after it when there is one.</summary>
    private static void Field(StringBuilder text, string name, string value, string? note = null, bool last = false)
    {
        text.Append($"      {JsonText.Quote(name)}: {value}{(last ? "" : ",")}");
        if (note is not null)
            text.Append(' ').Append(note);
        text.Append('\n');
    }

    private static readonly string[] Header =
    [
        "weapons.json - every weapon: the thirty-two variants, four to each of the eight classes, then",
        "the uniques.",
        "",
        "Read once, when the game starts, after restricted.json. A weapon is checked, never clamped: a",
        "spread restricted.json does not allow, a modifier forged past its limit, a unique that is not",
        "its variant raised, or a field the game does not know stops the game on a screen naming the",
        "file, the weapon and the rule - and, when a relation refused it, that restricted.json relation.",
        "Every weapon names all thirteen fields; comments and trailing commas are fine. A weapon is an",
        "edit here, never a rebuild: start the game again to play it.",
        "",
        "id: what everything names the weapon by - the starting party (code), a drop, a save. name: what",
        "the screen calls it. class: Dagger, Sword, Spear, Axe, Ranged, Throwing, Staff or Wand. role: a",
        "martial variant's - Efficiency, Purity, Control or Support - or null for a caster or a unique.",
        "range: the reach, in logic units (32 a tile). damage: a hit, before modifiers. cost: the",
        "movement an attack or a cast takes. manaCost: the mana a cast takes, 0 for a martial weapon.",
        "forged: the modifiers it is forged with, each to its stack count - a class's baseline is what",
        "its four variants share, so re-basing a class is an edit to all four. enchantments: what it",
        "arrives with, in order, each an id from the game's enchantment catalogue with its tier (1 when",
        "left out). shape: a wand's area - Blast (radius, targetReach), Cone (length, angleDegrees), Beam",
        "(length, width) or Nova (radius), in logic units - or null. unique: its variant with one",
        "modifier raised to x3, carrying a soul. derivedFrom: the variant a unique is made from, or null.",
        "",
        "arming_sword (what a dummy holds) and staff_of_renewal (a crowded room's healer) are named by",
        "the game's code and must be here. A weapon whose id is marked provisional is a first guess the",
        "docs did not fix, expected to move after play.",
        "",
        "The copy to edit is the one beside the game (bin/Debug/net10.0/weapons.json - the log prints",
        "its full path). GameEngine.DistanceRPG/Data/weapons.json is generated from the compiled",
        "content (DistanceRPG --write-data <folder>) and pinned by the tests: a weapon worth keeping",
        "goes into Logic/Content/ContentDefaults.cs, and the file is generated again. Deleting the copy",
        "plays the built-in weapons, and the screen says so.",
    ];
}
