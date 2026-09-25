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
/// The relations live in this file alone (the user's ruling of 2026-09-25): the
/// shipped <c>GameEngine.DistanceRPG/Data/restricted.json</c> is the master copy,
/// hand-kept, with the reason for each group and row written beside it; the game
/// carries it built in and plays that copy when none is beside the executable.
/// No copy of a relation is kept in code.
/// </para>
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

    /// <summary>The file's five keys, in §5.5's order: the order the file holds them in, and the order a failure is looked for.</summary>
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
}
