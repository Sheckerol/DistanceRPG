using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The content as one canonical text, so two contents are the same content
/// exactly when their dumps are the same string (the content rework's 1i). One
/// <c>&lt;section&gt; &lt;id&gt;.&lt;field&gt;=&lt;value&gt;</c> line per field:
/// <list type="bullet">
/// <item>every weapon class, in <see cref="WeaponClass"/> order: the kind it
/// plays as, the stats it levels on and its signature (null for a caster) —
/// what a class carries beyond the statline and spread its variants already
/// show. Whether a class casts is its kind (<see cref="Weapon.IsCaster"/> is
/// <c>Kind == Caster</c>), so the kind line holds that too. Read from the code's
/// tables (<see cref="Weapon.KindOf"/>, <see cref="Progression.Governing"/>,
/// <see cref="ContentValidator.Signature"/>) until the classes become rows of
/// <c>weapons.json</c> (Phase 5.R4), and from the content's class rows
/// after;</item>
/// <item>every weapon row in <see cref="WeaponCatalogue.All"/> order, each of its
/// fields — the forged spread in <see cref="ModifierType"/> order, the
/// enchantments it arrives with in attachment order with their tiers, the
/// shape with every dimension;</item>
/// <item>every enchantment row as the loaded catalogue plays it, in its order —
/// after the tuning's dials are laid over it, so a number the catalogue leaves
/// to <c>tuning.json</c> reads as the number that plays;</item>
/// <item>every relation as <paramref name="relations"/> holds it:
/// <c>excludes</c> in its order and each group in its own, <c>requires</c> and
/// <c>kind</c> by key, <c>forgedOnly</c> and <c>neverRolled</c> in their
/// order;</item>
/// <item>every dungeon row and its boss, in the hub's order; every starting
/// member, in roster order;</item>
/// <item>the tuning as <see cref="TuningValues.Of"/> lists it — every key and
/// table member the tuning's hash covers.</item>
/// </list>
/// Canonical: a record's order is kept where it means something and sorted
/// ordinally where it does not, numbers in the invariant culture, and
/// <c>"\n"</c> after every line — so a content that moved nothing dumps to the
/// same text whatever file, record or build it was read from.
/// </summary>
internal static class ContentDump
{
    /// <summary>The dump of <paramref name="content"/>, with <paramref name="relations"/> — the relations it was loaded under, which the content keeps only as the rules built from them.</summary>
    public static string Of(GameContent content, RestrictedData relations)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(relations);
        var lines = new List<string>();
        void Line(string section, string id, string field, string value) => lines.Add($"{section} {id}.{field}={Escape(value)}");

        foreach (var cls in Enum.GetValues<WeaponClass>())
        {
            const string at = "class";
            string name = cls.ToString();
            Line(at, name, "kind", Weapon.KindOf(cls).ToString());
            Line(at, name, "stats", List(Progression.Governing(cls).Select(stat => stat.ToString())));
            Line(at, name, "signature", ContentValidator.Signature.TryGetValue(cls, out var signature) ? signature.ToString() : "null");
        }

        foreach (var weapon in content.Weapons.All)
        {
            const string at = "weapon";
            Line(at, weapon.Id, "name", weapon.Name);
            Line(at, weapon.Id, "class", weapon.Class.ToString());
            Line(at, weapon.Id, "role", Maybe(weapon.Role));
            Line(at, weapon.Id, "range", Number(weapon.Range));
            Line(at, weapon.Id, "damage", Number(weapon.Damage));
            Line(at, weapon.Id, "cost", Number(weapon.Cost));
            Line(at, weapon.Id, "manaCost", Number(weapon.ManaCost));
            Line(at, weapon.Id, "forged", Spread(weapon.Forged));
            Line(at, weapon.Id, "enchantments", List(weapon.Enchantments.Select(e => $"{e.Id} tier {Number(e.Tier)}")));
            Line(at, weapon.Id, "shape", Shape(weapon.Shape));
            Line(at, weapon.Id, "unique", Flag(weapon.Unique));
            Line(at, weapon.Id, "derivedFrom", weapon.DerivedFrom ?? "null");
        }

        foreach (var entry in content.Enchantments.All)
        {
            const string at = "enchantment";
            Line(at, entry.Id, "name", entry.Name);
            Line(at, entry.Id, "effect", entry.Effect.ToString());
            Line(at, entry.Id, "targets", entry.Targets.ToString());
            Line(at, entry.Id, "lock", Number(entry.Lock));
            Line(at, entry.Id, "trigger", entry.Trigger is { } trigger ? Number(trigger) : "null");
            Line(at, entry.Id, "potency", Number(entry.Potency));
            Line(at, entry.Id, "applyPercent", Number(entry.ApplyPercent));
            Line(at, entry.Id, "applies", Maybe(entry.Applies));
            Line(at, entry.Id, "damageType", Maybe(entry.DamageType));
            Line(at, entry.Id, "unique", Flag(entry.Unique));
        }

        const string relation = "restricted";
        for (int i = 0; i < relations.Excludes.Count; i++)
            Line(relation, "excludes", Number(i), List(relations.Excludes[i]));
        foreach (var (id, needs) in relations.Requires.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            Line(relation, "requires", id, List(needs));
        foreach (var (id, kind) in relations.Kind.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            Line(relation, "kind", id, kind);
        for (int i = 0; i < relations.ForgedOnly.Length; i++)
            Line(relation, "forgedOnly", Number(i), relations.ForgedOnly[i]);
        for (int i = 0; i < relations.NeverRolled.Length; i++)
            Line(relation, "neverRolled", Number(i), relations.NeverRolled[i]);

        foreach (var dungeon in content.Dungeons.All)
        {
            const string at = "dungeon";
            Line(at, dungeon.Id, "name", dungeon.Name);
            Line(at, dungeon.Id, "theme", dungeon.Theme.ToString());
            Line(at, dungeon.Id, "attunement", Maybe(dungeon.Attunement));
            Line(at, dungeon.Id, "bossFloorMin", dungeon.BossFloorMin is { } min ? Number(min) : "null");
            Line(at, dungeon.Id, "bossFloorMax", dungeon.BossFloorMax is { } max ? Number(max) : "null");
            Line(at, dungeon.Id, "boss.id", dungeon.Boss.Id);
            Line(at, dungeon.Id, "boss.name", dungeon.Boss.Name);
            Line(at, dungeon.Id, "boss.maxHp", Number(dungeon.Boss.MaxHp));
            Line(at, dungeon.Id, "boss.movementBudget", Number(dungeon.Boss.MovementBudget));
            Line(at, dungeon.Id, "boss.innate", Spread(dungeon.Boss.Innate));
            Line(at, dungeon.Id, "retiresOnClear", Flag(dungeon.RetiresOnClear));
        }

        foreach (var member in content.Party.All)
        {
            const string at = "party";
            Line(at, member.Id, "name", member.Name);
            Line(at, member.Id, "stats", List(member.Stats.Entries.Select(stat => $"{stat.Stat} {Number(stat.Value)}")));
            Line(at, member.Id, "startingWeaponId", member.StartingWeaponId);
            Line(at, member.Id, "bagWeaponIds", List(member.BagWeaponIds));
        }

        foreach (var (name, value) in TuningValues.Of(content.Tuning))
            lines.Add($"tuning {name}={Escape(value)}");

        return string.Concat(lines.Select(line => line + "\n"));
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Maybe<TEnum>(TEnum? value) where TEnum : struct, Enum => value is { } some ? some.ToString() : "null";

    private static string List(IEnumerable<string> items) => "[" + string.Join(", ", items) + "]";

    /// <summary>A spread of stack counts in <see cref="ModifierType"/> order — the order the enum declares, never a dictionary's.</summary>
    private static string Spread(IReadOnlyDictionary<ModifierType, int> spread)
        => List(spread.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} x{Number(entry.Value)}"));

    private static string Shape(AreaShape? shape) => shape is null
        ? "null"
        : $"{shape.Kind} radius {Number(shape.Radius)} length {Number(shape.Length)} width {Number(shape.Width)} angleDegrees {Number(shape.AngleDegrees)} targetReach {Number(shape.TargetReach)}";

    /// <summary>A value as one line: a line break or a backslash inside it written as an escape, so every field stays exactly one line.</summary>
    private static string Escape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
