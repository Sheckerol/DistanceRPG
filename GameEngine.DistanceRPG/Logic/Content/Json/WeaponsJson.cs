using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// <c>weapons.json</c> (§5.6): the eight weapon classes, the thirty-two variants
/// and the uniques, in three sections (the content rework, 1d; the user's ruling
/// of 2026-09-25: "weapon types, weapon modifiers, rolled weapons ... fully in
/// json").
/// <list type="bullet">
/// <item><c>classes</c> — a table of each <see cref="WeaponClass"/> member, by its
/// C# name exactly, to its row (<see cref="WeaponClassDef"/>): <c>kind</c>
/// (<c>melee</c>, <c>ranged</c> or <c>caster</c>), <c>stats</c> (the
/// <see cref="Stat"/> names it levels on), <c>signature</c> (a modifier, or
/// null), <c>baseline</c> (modifiers to stack counts) and the statline —
/// <c>range</c>, <c>damage</c>, <c>cost</c>, <c>manaCost</c> — and, on a caster's
/// row, <c>dropOrder</c>: the ids of the variants its drop rolls between, in
/// draw order (a field a row may leave out; the validator wants it on a caster
/// and nowhere else). Every member has a row, and nothing else does.</item>
/// <item><c>variants</c> — a list of the weapons a drop or a dummy carries:
/// <c>id</c>, <c>name</c>, <c>class</c>, <c>role</c>, <c>adds</c> (the modifiers
/// it adds to its class's baseline), <c>enchantments</c> and <c>shape</c>; and,
/// optionally, any of the four statline fields, which then play instead of the
/// class's (REWORK DECISION 1, variant statline override).</item>
/// <item><c>uniques</c> — a list of the artifacts: <c>id</c>, <c>name</c>,
/// <c>derivedFrom</c> (a variant's id), <c>raises</c> (modifiers set to a stack
/// count) and <c>enchantments</c>.</item>
/// </list>
/// <para>
/// <strong>The reader composes; the rest of the game reads the whole row.</strong>
/// A variant becomes a <see cref="WeaponDef"/> whose statline is its own number
/// where it names one and its class's otherwise, and whose forged spread is the
/// class baseline plus <c>adds</c>, summed per modifier (a Purity variant's
/// <c>adds</c> deepens the signature). A unique becomes its variant with its own
/// id, name and enchantments, no role, the unique flag, <c>derivedFrom</c>, and
/// each <c>raises</c> entry <em>set</em> on the variant's spread. The rows come
/// back as the variants then the uniques, each in file order — the order
/// <see cref="WeaponCatalogue.All"/> keeps. So re-basing a class is one edit, to
/// its row.
/// </para>
/// <para>
/// Read from the text a caller hands in (<c>Logic/</c> never touches the disk)
/// by the strict shared reader (<see cref="JsonShape"/>): every field is
/// required — <c>role</c>, <c>signature</c> and <c>shape</c> may be null — but a
/// variant's statline and a class's drop order, and nothing else may sit beside
/// them.
/// <strong>Everything content is an id, everything code is a name</strong>: a
/// weapon is named everywhere by its <c>id</c>, an enchantment it arrives with by
/// the id the game's enchantment catalogue gives it (code:
/// <see cref="EnchantmentDefinitions"/>), and a modifier by its
/// <see cref="ModifierType"/> name, exactly — a name that is no modifier is an id
/// that resolves to nothing (<see cref="ContentValidator.RuleUnknownId"/>). A
/// class, a role, a stat and a shape's <c>kind</c> are the C# member names,
/// exactly. An enchantment's tier may be left out where it is 1, and a shape's
/// dimensions where they are 0.
/// </para>
/// <para>
/// Four refusals are the reader's, as the file's parse failure, because
/// composing cannot go on past them and nothing but a
/// <see cref="ContentException"/> may leave here: a <c>classes</c> section that
/// does not give every class a row (<see cref="ContentValidator.RuleEveryClassHasARow"/>,
/// naming the first class missing), a <c>kind</c> that is none of the three words
/// (<see cref="ContentValidator.RuleKindValue"/>, the relations' rule for the same
/// words), a stack a variant <c>adds</c> outside its range
/// (<see cref="ContentValidator.RuleNumberInRange"/>: summed, the number the file
/// wrote would be lost, and could wrap), and a unique whose <c>derivedFrom</c>
/// names no variant (<see cref="ContentValidator.RuleUniqueDerivedFrom"/>). A file in the flat
/// format this one replaced — a <c>weapons</c> list and no <c>classes</c> — is
/// refused saying so, and how to get the built-in weapons back. What else makes
/// a row legal is the validator's (<see cref="ContentValidator.ValidateWeapons"/>),
/// at the weapons' stage of the load: the class rows first, then the relations
/// of <c>restricted.json</c> asked through <see cref="ModifierRules.Allowed"/>, the
/// forge limits, the variants against their class's baseline, the unique
/// derivation, the casters' innates, and every number in its range.
/// </para>
/// <para>
/// The shipped <c>GameEngine.DistanceRPG/Data/weapons.json</c> is the master copy
/// of every class and weapon (the user's ruling of 2026-09-25): hand-kept, with
/// the notes on each class and unique written beside them; the game carries it
/// built in and plays that copy when none is beside the executable. No weapon or
/// class number is kept in code.
/// </para>
/// </summary>
public static class WeaponsJson
{
    public const string FileName = "weapons.json";

    /// <summary>The three sections, in the order the file holds them and the order they are read.</summary>
    private const string ClassesName = "classes";
    private const string VariantsName = "variants";
    private const string UniquesName = "uniques";

    /// <summary>The one member of the flat format this file replaced: a file holding it and no <see cref="ClassesName"/> predates the class section.</summary>
    private const string FlatListName = "weapons";

    /// <summary>What a file in the flat format is told: the built-in copy plays once it is gone, or the edge writes the built-in copy over it.</summary>
    internal const string PredatesTheClasses =
        "this file predates the classes section - delete it and the built-in weapons play, or run --write-data <exe folder>";

    private static readonly string[] Sections = [ClassesName, VariantsName, UniquesName];

    /// <summary>A class row's fields, in the record's order: the order the file holds them in, and the order a missing one is looked for — all but the last required, and the drop order a caster's row names.</summary>
    private static readonly string[] ClassFields = ["kind", "stats", "signature", "baseline", "range", "damage", "cost", "manaCost", DropOrderName];

    /// <summary>A caster row's drop order (<see cref="WeaponClassDef.DropOrder"/>): the one class field a row may leave out, since a martial row names none.</summary>
    private const string DropOrderName = "dropOrder";

    /// <summary>A variant's fields: the required seven in the order they are looked for, then the statline it may name as its own.</summary>
    private static readonly string[] VariantFields = ["id", "name", "class", "role", "adds", "enchantments", "shape", "range", "damage", "cost", "manaCost"];

    /// <summary>A unique's fields, in the order they are looked for.</summary>
    private static readonly string[] UniqueFields = ["id", "name", "derivedFrom", "raises", "enchantments"];

    /// <summary>An enchantment a weapon arrives with: its id, and its tier when that is not 1.</summary>
    private static readonly string[] ReferenceFields = ["id", "tier"];

    /// <summary>A shape's members: its kind, then every dimension <see cref="AreaShape"/> has, in the record's order.</summary>
    private static readonly string[] ShapeFields = ["kind", "radius", "length", "width", "angleDegrees", "targetReach"];

    /// <summary>
    /// The words a designer may reach for when a class is named otherwise in the
    /// code, and the class each means: the docs' "Sword &amp; Shield" (§1.2), and
    /// what the Ranged class's weapons are. Never another name for a class — the
    /// file names each by its member — but the pointer a refusal gives, the way
    /// <see cref="TuningSchema.Suggest"/> points at a key.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, WeaponClass> ClassSpellings =
        new Dictionary<string, WeaponClass>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sword & Shield"] = WeaponClass.Sword,
            ["SwordAndShield"] = WeaponClass.Sword,
            ["Shield"] = WeaponClass.Sword,
            ["Bow"] = WeaponClass.Ranged,
            ["Bows"] = WeaponClass.Ranged,
        };

    /// <summary>
    /// The dimensions each kind of shape measures (§1.4, <see cref="AreaShape"/>'s
    /// factories): a dimension a kind does not use is 0, so a file giving one any
    /// other number on that kind is naming a number nothing reads, and is refused.
    /// Each one it measures is held to its range by the validator
    /// (<see cref="ContentBounds.Dimension"/>), which reads this table too.
    /// </summary>
    internal static readonly IReadOnlyDictionary<AreaShapeKind, string[]> Dimensions = new Dictionary<AreaShapeKind, string[]>
    {
        [AreaShapeKind.Blast] = ["radius", "targetReach"],
        [AreaShapeKind.Cone] = ["length", "angleDegrees"],
        [AreaShapeKind.Beam] = ["length", "width"],
        [AreaShapeKind.Nova] = ["radius"],
    };

    /// <summary>
    /// Read a <c>weapons.json</c>'s text into its class rows and its composed
    /// weapon rows: the classes first, every one of them, then the variants over
    /// them, then the uniques over the variants. A failure inside a class row is
    /// reported against the class, and one inside a weapon against the weapon's id
    /// when it has one to read.
    /// </summary>
    /// <exception cref="ContentException">
    /// The text is not the documented shape (<see cref="ContentValidator.RuleFileShape"/>);
    /// a modifier names no <see cref="ModifierType"/> (<see cref="ContentValidator.RuleUnknownId"/>);
    /// a class has no row (<see cref="ContentValidator.RuleEveryClassHasARow"/>), or a
    /// row's kind is no kind (<see cref="ContentValidator.RuleKindValue"/>); a variant
    /// adds a stack outside its range (<see cref="ContentValidator.RuleNumberInRange"/>);
    /// or a unique derives from no variant (<see cref="ContentValidator.RuleUniqueDerivedFrom"/>).
    /// </exception>
    public static WeaponsData Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new JsonShape(FileName);
        var root = shape.Object(shape.Parse(text), JsonShape.Top, FileName, "a table of the classes, the variants and the uniques");

        // The flat format this one replaced, beside the executable: an old copy
        // edited there, or one an earlier --write-data wrote. Said as what it is,
        // with the way back, rather than as a member the shape does not have.
        if (shape.Optional(root, ClassesName) is null && shape.Optional(root, FlatListName) is not null)
            throw shape.Fail(FileName, ClassesName, PredatesTheClasses);
        shape.Only(root, Sections, JsonShape.Top, FileName);

        var classes = Classes(shape, shape.Required(root, ClassesName, JsonShape.Top, FileName));
        var variants = Variants(shape, shape.Required(root, VariantsName, JsonShape.Top, FileName), classes);
        var uniques = Uniques(shape, shape.Required(root, UniquesName, JsonShape.Top, FileName), variants);
        return new WeaponsData(classes, [.. variants, .. uniques]);
    }

    /// <summary>
    /// The <c>classes</c> section: a row for each class the file names — a name
    /// that is no class refused, pointing at the one it is nearest, a name twice
    /// refused — and then every class there is, or the first one missing, in the
    /// enum's order, is the file's failure. The rows come back in the enum's order.
    /// </summary>
    private static WeaponClassDef[] Classes(JsonShape shape, JsonElement value)
    {
        var table = shape.Object(value, ClassesName, FileName, "a table of each class to its row");
        string[] names = Enum.GetNames<WeaponClass>();
        var rows = new WeaponClassDef?[names.Length];
        foreach (var member in table.EnumerateObject())
        {
            string path = JsonShape.Member(ClassesName, member.Name);
            if (!names.Contains(member.Name, StringComparer.Ordinal))
            {
                string? near = ClassSpellings.TryGetValue(member.Name, out var meant) ? meant.ToString() : TuningSchema.Nearest(member.Name, names);
                throw shape.Fail(member.Name, path, near is null
                    ? $"is not a class - the classes are {string.Join(", ", names)}"
                    : $"is not a class - did you mean {near}?");
            }
            var cls = Enum.Parse<WeaponClass>(member.Name);
            if (rows[(int)cls] is not null)
                throw shape.Fail(member.Name, path, "is named twice");
            rows[(int)cls] = ClassRow(shape, member.Value, path, cls);
        }

        foreach (var cls in Enum.GetValues<WeaponClass>())
            if (rows[(int)cls] is null)
                throw shape.Fail(cls.ToString(), ClassesName, $"has no {cls} row - each of the eight classes has one",
                    ContentValidator.RuleEveryClassHasARow);
        return rows.Select(row => row!).ToArray();
    }

    /// <summary>
    /// One class's row: every field required but the drop order, a signature that
    /// may be null, and a kind that is one of the three words. A drop order left
    /// out, or written as null, is none — what a martial row has; whether the
    /// class should have one is the validator's
    /// (<see cref="ContentValidator.RuleCasterVariantPool"/>), which sees records
    /// built in code too.
    /// </summary>
    private static WeaponClassDef ClassRow(JsonShape shape, JsonElement value, string path, WeaponClass cls)
    {
        string entry = cls.ToString();
        var row = shape.Object(value, path, entry, "a class: a table of what every weapon of it shares");
        shape.Only(row, ClassFields, path, entry);

        JsonElement Get(string name) => shape.Required(row, name, path, entry);
        string At(string name) => JsonShape.Member(path, name);

        string kindText = shape.String(Get("kind"), At("kind"), entry, "melee, ranged or caster in quotes");
        if (!RestrictedData.TryParseKind(kindText, out var kind))
            throw shape.Fail(entry, At("kind"),
                $"is the text {kindText}, not {RestrictedData.MeleeKind}, {RestrictedData.RangedKind} or {RestrictedData.CasterKind} (lower case)",
                ContentValidator.RuleKindValue);

        var statItems = shape.Array(Get("stats"), At("stats"), entry, "a list of the stats it levels on");
        var stats = new Stat[statItems.Count];
        for (int i = 0; i < statItems.Count; i++)
            stats[i] = shape.Enum<Stat>(statItems[i], $"{At("stats")}[{i}]", entry);

        return new WeaponClassDef(
            Class: cls,
            Kind: kind,
            Stats: stats,
            Signature: shape.EnumOrNull<ModifierType>(Get("signature"), At("signature"), entry, ContentValidator.RuleUnknownId),
            Baseline: shape.Modifiers(Get("baseline"), At("baseline"), entry, "a table of modifiers to stack counts"),
            Range: shape.Int(Get("range"), At("range"), entry),
            Damage: shape.Int(Get("damage"), At("damage"), entry),
            Cost: shape.Int(Get("cost"), At("cost"), entry),
            ManaCost: shape.Int(Get("manaCost"), At("manaCost"), entry),
            DropOrder: shape.Optional(row, DropOrderName) is { ValueKind: not JsonValueKind.Null } order
                ? shape.Ids(order, At(DropOrderName), entry, "a list of the ids of the variants its drop rolls between")
                : null);
    }

    /// <summary>
    /// The <c>variants</c> section, each composed over its class's row: the
    /// statline its own where it names one and the class's otherwise, the forged
    /// spread the baseline plus what it adds.
    /// </summary>
    private static List<WeaponDef> Variants(JsonShape shape, JsonElement value, IReadOnlyList<WeaponClassDef> classes)
    {
        var rows = shape.Array(value, VariantsName, FileName, "a list of variants");
        var variants = new List<WeaponDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{VariantsName}[{i}]";
            var row = shape.Object(rows[i], path, path, "a variant: a table of its fields");
            string entry = JsonShape.EntryOf(row, path);
            shape.Only(row, VariantFields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);
            int Own(string name, int classes) => shape.Optional(row, name) is { } given ? shape.Int(given, At(name), entry) : classes;

            string id = shape.Id(Get("id"), At("id"), entry);
            string name = shape.String(Get("name"), At("name"), entry, "a name in quotes");
            var cls = shape.Enum<WeaponClass>(Get("class"), At("class"), entry);
            var role = shape.EnumOrNull<VariantRole>(Get("role"), At("role"), entry);
            var adds = shape.Modifiers(Get("adds"), At("adds"), entry, "a table of the modifiers it adds to its class's baseline");
            RequireAddsInRange(shape, adds, At("adds"), entry);
            var enchantments = References(shape, Get("enchantments"), At("enchantments"), entry);
            var area = Shape(shape, Get("shape"), At("shape"), entry);

            var of = classes[(int)cls];   // every class has its row by now
            variants.Add(new WeaponDef(
                Id: id, Name: name, Class: cls, Role: role,
                Range: Own("range", of.Range),
                Damage: Own("damage", of.Damage),
                Cost: Own("cost", of.Cost),
                ManaCost: Own("manaCost", of.ManaCost),
                Forged: Summed(of.Baseline, adds),
                Enchantments: enchantments,
                Shape: area,
                Unique: false,
                DerivedFrom: null));
        }
        return variants;
    }

    /// <summary>
    /// The <c>uniques</c> section, each composed over the variant it names: the
    /// variant's class, statline and shape, its spread with every modifier it
    /// raises set to the count given, and the unique's own id, name and
    /// enchantments. A <c>derivedFrom</c> naming no variant is the file's failure.
    /// </summary>
    private static List<WeaponDef> Uniques(JsonShape shape, JsonElement value, IReadOnlyList<WeaponDef> variants)
    {
        var byId = new Dictionary<string, WeaponDef>(StringComparer.Ordinal);
        foreach (var variant in variants)
            byId.TryAdd(variant.Id, variant);   // a second row of one id is the validator's to refuse

        var rows = shape.Array(value, UniquesName, FileName, "a list of uniques");
        var uniques = new List<WeaponDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = $"{UniquesName}[{i}]";
            var row = shape.Object(rows[i], path, path, "a unique: a table of its fields");
            string entry = JsonShape.EntryOf(row, path);
            shape.Only(row, UniqueFields, path, entry);

            JsonElement Get(string name) => shape.Required(row, name, path, entry);
            string At(string name) => JsonShape.Member(path, name);

            string id = shape.Id(Get("id"), At("id"), entry);
            string name = shape.String(Get("name"), At("name"), entry, "a name in quotes");
            string derivedFrom = shape.Id(Get("derivedFrom"), At("derivedFrom"), entry);
            var raises = shape.Modifiers(Get("raises"), At("raises"), entry, "a table of the modifiers it raises to stack counts");
            var enchantments = References(shape, Get("enchantments"), At("enchantments"), entry);

            if (!byId.TryGetValue(derivedFrom, out var variant))
            {
                string? near = TuningSchema.Nearest(derivedFrom, byId.Keys);
                throw shape.Fail(entry, At("derivedFrom"), $"is {derivedFrom}, which is no variant here"
                    + (near is null ? "" : $" - did you mean {near}?"), ContentValidator.RuleUniqueDerivedFrom);
            }

            uniques.Add(variant with
            {
                Id = id,
                Name = name,
                Role = null,
                Forged = Raised(variant.Forged, raises),
                Enchantments = enchantments,
                Unique = true,
                DerivedFrom = derivedFrom,
            });
        }
        return uniques;
    }

    /// <summary>
    /// Every stack a variant adds lies in its range (<see cref="ContentValidator.RuleNumberInRange"/>,
    /// <see cref="ContentBounds.Adds"/>), asked as it is read and before anything
    /// sums it: once summed onto the class's baseline, a raw -1 would read as a
    /// composed stack one short, a 0 on a baseline modifier would add nothing in
    /// silence, and the largest whole number would wrap. So the refusal names the
    /// number the file wrote, at its path (<c>variants[6].adds.Riposte: 0 is
    /// outside 1 to 3 - …</c>), walked in <see cref="ModifierType"/> order so the
    /// one reported is the same every run.
    /// </summary>
    private static void RequireAddsInRange(JsonShape shape, IReadOnlyDictionary<ModifierType, int> adds, string path, string entry)
    {
        var bound = ContentBounds.Adds;
        foreach (var (t, n) in adds.OrderBy(kv => kv.Key))
            if (n < bound.Min || n > bound.Max)
                throw shape.Fail(entry, JsonShape.Member(path, t.ToString()), ContentBounds.Outside(bound, n), ContentValidator.RuleNumberInRange);
    }

    /// <summary>A variant's forged spread: its class's baseline plus what it adds, summed per modifier, in <see cref="ModifierType"/> order. What it adds is in its range by now (<see cref="RequireAddsInRange"/>), so the sum stays well inside a whole number.</summary>
    private static IReadOnlyDictionary<ModifierType, int> Summed(IReadOnlyDictionary<ModifierType, int> baseline, IReadOnlyDictionary<ModifierType, int> adds)
    {
        var spread = new Dictionary<ModifierType, int>();
        foreach (var t in Enum.GetValues<ModifierType>())
            if (baseline.TryGetValue(t, out int under) | adds.TryGetValue(t, out int added))
                spread[t] = under + added;
        return spread;
    }

    /// <summary>A unique's forged spread: its variant's, with every modifier it raises set to the count given — not added to — in <see cref="ModifierType"/> order.</summary>
    private static IReadOnlyDictionary<ModifierType, int> Raised(IReadOnlyDictionary<ModifierType, int> variant, IReadOnlyDictionary<ModifierType, int> raises)
    {
        var spread = new Dictionary<ModifierType, int>();
        foreach (var t in Enum.GetValues<ModifierType>())
            if (raises.TryGetValue(t, out int raised))
                spread[t] = raised;
            else if (variant.TryGetValue(t, out int kept))
                spread[t] = kept;
        return spread;
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
}
