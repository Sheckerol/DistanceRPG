using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The numbers the suite's worked examples were written against: what a test
/// stands on without testing it — its premise, stated as its own input (the
/// content rework's R5) — so the master copy may move any of them and no test
/// that stands on them fails. It is never compared with the master and the game
/// never reads it: the files in <c>GameEngine.DistanceRPG/Data</c> are the
/// master, and the suites that are about the shipped content read those
/// (<see cref="ShippedContent"/>). A suite whose scenarios stand on these numbers
/// — a push that moves a tile, a dagger that hits for 15 into Block 3, a seed that
/// rolls a dungeon five floors deep — makes them current for each of its tests
/// (<see cref="TestContent.UsePremise"/>).
/// <list type="bullet">
/// <item>The tuning: <c>TestData/saves/pre-rework-tuning.json</c>, tuning.json at
/// commit <c>febe036</c> byte for byte — the numbers the worked examples were
/// written against, and the ones the pre-rework saves were played under — read by
/// the game's own reader over the shipped tuning, so a key the file predates
/// keeps the master's number.</item>
/// <item>The weapon classes' statlines, the two staves that name their own mana,
/// and the wands' shapes: phase-1-weapons.md §1.2–§1.4's tables — the numbers
/// REWORK DECISION 3 pins against the master in <c>WeaponContentTests</c> and
/// <c>WeaponClassTests</c>.</item>
/// <item>The bosses' statlines: the golem's, which phase-5-save.md fixes (80 HP, a
/// movement budget of 40), and the four provisional bosses' first guesses (200,
/// 70).</item>
/// </list>
/// Every other part of the content — the relations, the rows, the enchantment
/// catalogue, the starting party — is the shipped content's: a premise states
/// numbers, never rows.
/// </summary>
internal static class Premise
{
    private static readonly Lazy<Tuning> TuningHolder = new(() =>
        TuningJson.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", "pre-rework-tuning.json")), ShippedContent.Tuning).Tuning);

    private static readonly Lazy<WeaponsData> WeaponsHolder = new(() => Set(ShippedContent.Weapons,
    [
        // §1.2: range, damage, cost; the martial classes cast nothing.
        ("Dagger", 40, 15, 30, 0),
        ("Sword", 80, 10, 50, 0),
        ("Spear", 128, 7, 55, 0),
        ("Axe", 60, 18, 60, 0),
        ("Ranged", 320, 5, 30, 0),
        ("Throwing", 190, 9, 15, 0),
        // §1.3 and §1.4: every staff and every wand alike, but for the staves' own mana below.
        ("Staff", 100, 0, 40, 20),
        ("Wand", 160, 8, 45, 20),
    ]));

    private static readonly Lazy<DungeonsData> DungeonsHolder = new(() => SetBosses(ShippedContent.Dungeons,
    [
        ("tutorial", 80, 40),
        ("spear_hall", 200, 70),
        ("long_gallery", 200, 70),
        ("knife_warrens", 200, 70),
        ("quiver_pits", 200, 70),
    ]));

    private static readonly Lazy<GameContent> ContentHolder = new(() => TestContent.Load(Tuning, weapons: Weapons, dungeons: Dungeons));

    /// <summary>The tuning the worked examples were written against.</summary>
    public static Tuning Tuning => TuningHolder.Value;

    /// <summary>The shipped weapons with the statlines and shapes the worked examples were written against.</summary>
    public static WeaponsData Weapons => WeaponsHolder.Value;

    /// <summary>The shipped dungeons with the boss statlines the worked examples were written against.</summary>
    public static DungeonsData Dungeons => DungeonsHolder.Value;

    /// <summary>The movement budget a member opens a turn on under the premise, in logic units.</summary>
    public static float Budget => Tuning.MovementBudget;

    /// <summary>The shipped content under the premise, loaded once: what <see cref="TestContent.UsePremise"/> makes current.</summary>
    public static GameContent Content => ContentHolder.Value;

    private static WeaponsData Set(WeaponsData weapons, (string Class, int Range, int Damage, int Cost, int ManaCost)[] statlines)
    {
        foreach (var (name, range, damage, cost, manaCost) in statlines)
        {
            var cls = Enum.Parse<WeaponClass>(name);
            weapons = SetClass(weapons, cls, "range", range);
            weapons = SetClass(weapons, cls, "damage", damage);
            weapons = SetClass(weapons, cls, "cost", cost);
            weapons = SetClass(weapons, cls, "manaCost", manaCost);
        }

        // §1.3: the Staff of Renewal and the Staff of Mire name their own mana.
        weapons = SetWeapon(weapons, "staff_of_renewal", "manaCost", 15);
        weapons = SetWeapon(weapons, "staff_of_mire", "manaCost", 25);

        // §1.4's four shapes, in logic units.
        weapons = SetWeapon(weapons, "wand_of_the_blast", "radius", 48);
        weapons = SetWeapon(weapons, "wand_of_the_blast", "targetReach", 160);
        weapons = SetWeapon(weapons, "wand_of_the_cone", "length", 128);
        weapons = SetWeapon(weapons, "wand_of_the_cone", "angleDegrees", 90);
        weapons = SetWeapon(weapons, "wand_of_the_beam", "length", 224);
        weapons = SetWeapon(weapons, "wand_of_the_beam", "width", 32);
        weapons = SetWeapon(weapons, "wand_of_the_nova", "radius", 96);
        return weapons;
    }

    /// <summary>
    /// <paramref name="cls"/>'s row with <paramref name="field"/> set, and every
    /// weapon of the class that plays the row's number with it: a variant that
    /// names its own keeps it, and so does a unique made from one, which takes its
    /// variant's statline.
    /// </summary>
    internal static WeaponsData SetClass(WeaponsData weapons, WeaponClass cls, string field, int value)
    {
        var row = weapons.ClassOf(cls) ?? throw new ArgumentException($"The weapons hold no {cls} row.", nameof(cls));
        int played = Field(row.Range, row.Damage, row.Cost, row.ManaCost, field);
        return new WeaponsData(
            weapons.Classes.Select(r => r.Class != cls ? r : field switch
            {
                "range" => r with { Range = value },
                "damage" => r with { Damage = value },
                "cost" => r with { Cost = value },
                _ => r with { ManaCost = value },
            }).ToList(),
            weapons.Weapons.Select(w => w.Class == cls && Field(w.Range, w.Damage, w.Cost, w.ManaCost, field) == played ? WithField(w, field, value) : w).ToList());
    }

    /// <summary>The weapon <paramref name="id"/>'s own <paramref name="field"/> set — a statline field, or a wand's dimension — and every unique made from it with it.</summary>
    internal static WeaponsData SetWeapon(WeaponsData weapons, string id, string field, int value)
    {
        if (weapons.Weapons.All(w => w.Id != id))
            throw new ArgumentException($"The weapons hold no {id}.", nameof(id));
        return weapons with
        {
            Weapons = weapons.Weapons.Select(w => w.Id != id && w.DerivedFrom != id ? w : field switch
            {
                "radius" => w with { Shape = w.Shape! with { Radius = value } },
                "length" => w with { Shape = w.Shape! with { Length = value } },
                "width" => w with { Shape = w.Shape! with { Width = value } },
                "angleDegrees" => w with { Shape = w.Shape! with { AngleDegrees = value } },
                "targetReach" => w with { Shape = w.Shape! with { TargetReach = value } },
                _ => WithField(w, field, value),
            }).ToList(),
        };
    }

    private static DungeonsData SetBosses(DungeonsData dungeons, (string Dungeon, int MaxHp, int MovementBudget)[] bosses)
    {
        foreach (var (id, maxHp, movementBudget) in bosses)
        {
            if (dungeons.Dungeons.All(d => d.Id != id))
                throw new ArgumentException($"The dungeons hold no {id}.", nameof(bosses));
            dungeons = new DungeonsData(dungeons.Dungeons
                .Select(d => d.Id != id ? d : d with { Boss = d.Boss with { MaxHp = maxHp, MovementBudget = movementBudget } })
                .ToList());
        }
        return dungeons;
    }

    private static int Field(int range, int damage, int cost, int manaCost, string field) => field switch
    {
        "range" => range,
        "damage" => damage,
        "cost" => cost,
        "manaCost" => manaCost,
        _ => throw new ArgumentException($"A statline has no field {field}.", nameof(field)),
    };

    private static WeaponDef WithField(WeaponDef weapon, string field, int value) => field switch
    {
        "range" => weapon with { Range = value },
        "damage" => weapon with { Damage = value },
        "cost" => weapon with { Cost = value },
        "manaCost" => weapon with { ManaCost = value },
        _ => throw new ArgumentException($"A statline has no field {field}.", nameof(field)),
    };
}
