using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>party.json</c> (§5.9): "the four-character starting roster", one member a
/// <see cref="PartyMemberDef"/> under the doc's own names — <c>id</c>,
/// <c>name</c>, <c>stats</c> with <c>STR</c>, <c>DEX</c>, <c>CON</c> and
/// <c>INT</c> in capitals, <c>startingWeaponId</c> — and <c>bagWeaponIds</c>, the
/// record's fourth field beyond §5.9's three: the slot-1 staff the party has
/// always carried, and D's second, debuff one. Read from the text a caller hands
/// in (<c>Logic/</c> never touches the disk) by the strict shared reader
/// (<see cref="JsonShape"/>): every field is required, a spread names all four
/// stats and nothing else, and nothing else may sit beside them.
/// <para>
/// <strong>A starting loadout is content, not a constant</strong> (§5.9,
/// PHASE5-177, -178): which pair splits which stats and which debuff staff D
/// carries are decided in this file, and a new campaign starts with whoever it
/// names. A member names weapons by the id <c>weapons.json</c> gives them, which
/// is why the file loads last. What makes a roster legal — four members, each
/// spread a permutation of 1–4, every weapon there and within the carry, a
/// caster among them — is the validator's (<see cref="ContentValidator.ValidateParty"/>),
/// at the roster's stage of the load.
/// </para>
/// </summary>
public static class PartyJson
{
    public const string FileName = "party.json";

    /// <summary>The one member of the file: the list of members, named for the record.</summary>
    private const string ListName = "members";

    /// <summary>A member's fields, in the record's parameter order — §5.9's names: the order they are written in, and the order a missing one is looked for.</summary>
    private static readonly string[] Fields = ["id", "name", "stats", "startingWeaponId", "bagWeaponIds"];

    /// <summary>A spread's members: the four stats by their §2.1 names, in <see cref="Stat"/> order.</summary>
    private static readonly string[] StatNames = Enum.GetNames<Stat>();

    /// <summary>
    /// Read a <c>party.json</c>'s text into its members, in file order — the order
    /// they are spawned and numbered in. A failure inside a member is reported
    /// against the member's id when it has one to read.
    /// </summary>
    /// <exception cref="ContentException">The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>).</exception>
    public static PartyData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, $"a table holding the {ListName} list");
        shape.Only(root, [ListName], JsonShape.Top, FileName);
        var rows = shape.Array(shape.Required(root, ListName, JsonShape.Top, FileName), ListName, FileName, "a list of party members");

        var members = new List<PartyMemberDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{ListName}[{i}]";
            var row = shape.Object(rows[i], path, path, "a party member: a table of their fields");
            string entry = JsonShape.EntryOf(row, path);
            shape.Only(row, Fields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);

            members.Add(new PartyMemberDef(
                Id: shape.Id(Get("id"), At("id"), entry),
                Name: shape.String(Get("name"), At("name"), entry, "a name in quotes"),
                Stats: Stats(shape, Get("stats"), At("stats"), entry),
                StartingWeaponId: shape.Id(Get("startingWeaponId"), At("startingWeaponId"), entry),
                BagWeaponIds: shape.Ids(Get("bagWeaponIds"), At("bagWeaponIds"), entry, "a list of weapon ids, in slot order")));
        }
        return new PartyData(members);
    }

    /// <summary>A member's spread: <c>STR</c>, <c>DEX</c>, <c>CON</c> and <c>INT</c>, each a whole number, every one named and nothing else — whether they are 1 to 4 in some order is the validator's.</summary>
    private static InnateStats Stats(JsonShape shape, JsonElement value, string path, string entry)
    {
        var table = shape.Object(value, path, entry, "a table of STR, DEX, CON and INT");
        shape.Only(table, StatNames, path, entry);
        int Of(Stat stat) => shape.Int(shape.Required(table, stat.ToString(), path, entry), JsonShape.Member(path, stat.ToString()), entry);
        return new InnateStats(STR: Of(Stat.STR), DEX: Of(Stat.DEX), CON: Of(Stat.CON), INT: Of(Stat.INT));
    }

    /// <summary>
    /// The file for <paramref name="data"/>: a header saying what the file is and
    /// how it is read, then every member in the record's order, one field a line
    /// in the record's parameter order, a spread in <see cref="Stat"/> order on the
    /// one line, with <c>/* provisional */</c> after the id of a member the docs did
    /// not fix (<see cref="JsonText.IdNote"/>; the shipped four are §2.1's table,
    /// verbatim). Canonical, with <c>"\n"</c> line ends, so one roster is one text.
    /// </summary>
    public static string Write(PartyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = new StringBuilder();
        JsonText.Header(text, Header);
        text.Append("{\n");
        text.Append($"  {JsonText.Quote(ListName)}: [\n");
        for (int i = 0; i < data.Members.Count; i++)
        {
            var m = data.Members[i];
            text.Append("    {\n");
            Field(text, "id", JsonText.Quote(m.Id), JsonText.IdNote(m.Id));
            Field(text, "name", JsonText.Quote(m.Name));
            Field(text, "stats", $"{{ {string.Join(", ", m.Stats.Entries.Select(e => $"{JsonText.Quote(e.Stat.ToString())}: {JsonText.Number(e.Value)}"))} }}");
            Field(text, "startingWeaponId", JsonText.Quote(m.StartingWeaponId));
            Field(text, "bagWeaponIds", JsonText.Ids(m.BagWeaponIds), last: true);
            text.Append(i < data.Members.Count - 1 ? "    },\n" : "    }\n");
        }
        text.Append("  ]\n");
        text.Append("}\n");
        return text.ToString();
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
        "party.json - the four who start a campaign, in the order they stand and are numbered (1-4).",
        "",
        "Read once, when the game starts, last of the data files, since it names weapons. A member is",
        "checked, never clamped: stats that are not 1, 2, 3 and 4 in some order, a weapon weapons.json",
        "does not have, more weapons than a member's share of tuning.json's PartyCarrySlots, a party",
        "with no caster in hand, or a field the game does not know stops the game on a screen naming",
        "the file, the member and the rule. Every member names all five fields; comments and trailing",
        "commas are fine. A campaign starts with the party named here, so an edit plays from the next",
        "campaign started.",
        "",
        "id: what everything names the member by - a save. name: what the screen calls them. stats:",
        "STR, DEX, CON and INT, the numbers 1 to 4 in some order - a stat divides the XP a pool needs,",
        "so a 4 learns four times as fast as a 1, and nobody is better endowed, only differently",
        "arranged. startingWeaponId: the weapon in hand, an id from weapons.json. bagWeaponIds: the",
        "weapons in the bag, in slot order, each an id from weapons.json.",
        "",
        "The copy to edit is the one beside the game (bin/Debug/net10.0/party.json - the log prints its",
        "full path). GameEngine.DistanceRPG/Data/party.json is generated from the compiled content",
        "(DistanceRPG --write-data <folder>) and pinned by the tests: a party worth keeping goes into",
        "Logic/Content/ContentDefaults.cs, and the file is generated again. Deleting the copy plays the",
        "built-in party, and the screen says so.",
    ];
}
