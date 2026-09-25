using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The weapon types are a section of <c>weapons.json</c> (the content rework, 1d;
/// the user's ruling of 2026-09-25, "weapon types ... fully in json", which rules
/// Phase 5 USER DECISION 4): each of the eight classes is a row — the kind it
/// plays as, the stats it levels on, its signature, its baseline and its
/// statline — and every question the game asks a class is a read of the rows.
/// Re-basing a class is one edit to its row; a number a variant names as its own
/// is its alone (REWORK DECISION 1); a row's kind and stats are what plays; and a
/// row missing or misshapen stops the game naming <c>weapons.json</c>, the class
/// and the rule. In the content collection: the cases that ask
/// <see cref="Weapon.Kind"/> or <see cref="Progression.Governing"/> make their
/// content current (<see cref="TestContent.Use"/>), since both read
/// <see cref="GameContent.Current"/>.
/// </summary>
[Collection(TestContent.Collection)]
public class WeaponClassTests
{
    private static readonly ContentTexts Shipped = DataTexts.Shipped;

    private static readonly string ShippedWeapons = DataTexts.Shipped.Weapons!;

    /// <summary>A <c>weapons.json</c> in the flat format the class section replaced (before Phase 5.R4): one list of weapons, each row naming every field, the Flensing Knife as it was written.</summary>
    private const string FlatFormat = """
        /* weapons.json - every weapon: the thirty-two variants, four to each of the eight classes, then the uniques. */
        {
          "weapons": [
            {
              "id": "flensing_knife",
              "name": "Flensing Knife",
              "class": "Dagger",
              "role": "Efficiency",
              "range": 40,
              "damage": 15,
              "cost": 30,
              "manaCost": 0,
              "forged": { "CritWindow": 1, "CritMultiplier": 1, "Light": 1 },
              "enchantments": [],
              "shape": null,
              "unique": false,
              "derivedFrom": null
            }
          ]
        }
        """;

    private static (string? File, bool BuiltIn, string Entry, string Rule) Stamp(ContentException error)
        => (error.File, error.BuiltIn, error.EntryId, error.Rule);

    /// <summary>The content the game plays with <paramref name="weapons"/> beside it and the shipped files for the rest — loaded as the game loads it, with nothing to say.</summary>
    private static GameContent Plays(string weapons)
    {
        var load = ShippedData.Load(Shipped with { Weapons = weapons });
        Assert.Null(load.Error);
        Assert.Empty(load.Notices);
        return load.Content!;
    }

    /// <summary>Why the game does not start with <paramref name="weapons"/> beside it: the load's error, never an exception out of it.</summary>
    private static ContentException Fails(string weapons)
    {
        var load = ShippedData.Load(Shipped with { Weapons = weapons });
        Assert.Null(load.Content);
        return load.Error ?? throw new Xunit.Sdk.XunitException("the load has neither content nor an error");
    }

    [Fact]
    public void AClassRowIsWhatTheClassCarries()
    {
        // §1.2-§1.4's class tables (phase-1-weapons.md: each class heading's stat
        // and baseline, "Attack cost tracks weight", 1.3's "all share Range 100 and
        // Cost 40", 1.4's "all share Damage 8, Cost 45, Mana 20"), held literally:
        // REWORK DECISION 3's doc-fixed pins, so a master edit that moves one fails
        // here by name, and its doc moves with it.
        (WeaponClass Class, WeaponKind Kind, Stat[] Stats, ModifierType? Signature, (ModifierType Type, int Stacks)[] Baseline,
            int Range, int Damage, int Cost, int ManaCost)[] table =
        [
            (Dagger, WeaponKind.Melee, [Stat.DEX], CritWindow, [(CritWindow, 1), (CritMultiplier, 1)], 40, 15, 30, 0),
            (Sword, WeaponKind.Melee, [Stat.STR, Stat.DEX], Block, [(Block, 1), (Push, 1)], 80, 10, 50, 0),
            (Spear, WeaponKind.Melee, [Stat.STR], Brace, [(Brace, 1), (Longshot, 1)], 128, 7, 55, 0),
            (Axe, WeaponKind.Melee, [Stat.STR], Cleave, [(Cleave, 1), (Opportunist, 1)], 60, 18, 60, 0),
            (Ranged, WeaponKind.Ranged, [Stat.DEX], Longshot, [(CritWindow, 1), (Longshot, 1)], 320, 5, 30, 0),
            (Throwing, WeaponKind.Ranged, [Stat.STR], Charges, [(CritMultiplier, 1), (Charges, 1)], 190, 9, 15, 0),
            (Staff, WeaponKind.Caster, [Stat.INT], null, [(Resonant, 1)], 100, 0, 40, 20),
            (Wand, WeaponKind.Caster, [Stat.INT], null, [(Resonant, 1)], 160, 8, 45, 20),
        ];

        // Every class has its row, in the enum's order, and each row is the table's.
        var rows = WeaponsJson.Read(ShippedWeapons).Classes;
        Assert.Equal(table.Select(t => t.Class), rows.Select(row => row.Class));
        foreach (var (row, t) in rows.Zip(table))
        {
            Assert.Equal((t.Kind, t.Signature, t.Range, t.Damage, t.Cost, t.ManaCost), (row.Kind, row.Signature, row.Range, row.Damage, row.Cost, row.ManaCost));
            Assert.Equal(t.Stats, row.Stats);
            Assert.Equal(t.Baseline.OrderBy(b => b.Type), row.Baseline.OrderBy(b => b.Key).Select(b => (b.Key, b.Value)));
        }

        // And the game reads them: the kind a weapon of the class plays as, the
        // stats it levels on, and the class a dungeon's theme finds by its signature.
        using (TestContent.Use())
        {
            var weapons = GameContent.Current.Weapons;
            Assert.Equal(rows.Select(row => row.Class), weapons.Classes.Select(row => row.Class));
            foreach (var t in table)
            {
                var variant = weapons.ByClass(t.Class)[0];
                Assert.Equal(t.Kind, TestWeapons.Get(variant.Id, t.Class == Wand ? DamageType.Cold : null).Kind);
                Assert.Equal(t.Kind, weapons.KindOf(t.Class));
                Assert.Equal(t.Stats, Progression.Governing(t.Class));
                if (t.Signature is { } signature)
                    Assert.Equal(t.Class, weapons.ClassSignedBy(signature));
            }
            foreach (var dungeon in GameContent.Current.Dungeons.All)
                Assert.Equal(table.Single(t => t.Signature == dungeon.Theme).Class, weapons.ClassSignedBy(dungeon.Theme));
        }
    }

    [Fact]
    public void ReBasingAClassIsOneEdit()
    {
        // The baseline is the class's row (Phase 5 USER DECISION 4, ruled): one
        // edit to the Dagger row — its second modifier CritMultiplier made Pin —
        // re-bases all four daggers and the two uniques made from them, and the
        // game loads with nothing to say.
        string[] daggers = ["flensing_knife", "assassins_fang", "disarming_kris", "weakspot_stiletto", "widowmaker", "flensing_knife_unique"];
        var shipped = ShippedContent.Content.Weapons;
        Assert.Equal(daggers, shipped.All.Where(w => w.Class == Dagger).Select(w => w.Id));

        var rebased = Plays(DataTexts.InClass(ShippedWeapons, Dagger, "\"CritMultiplier\": ", "\"Pin\": ")).Weapons;
        foreach (string id in daggers)
        {
            var was = shipped[id].Forged.ToDictionary(f => f.Key == CritMultiplier ? Pin : f.Key, f => f.Value);
            Assert.Equal(was.OrderBy(f => f.Key), rebased[id].Forged.OrderBy(f => f.Key));
            Assert.False(rebased[id].Forged.ContainsKey(CritMultiplier), id);
        }
        Assert.All(rebased.All.Where(w => w.Class != Dagger),
            w => Assert.Equal(shipped[w.Id].Forged.OrderBy(f => f.Key), w.Forged.OrderBy(f => f.Key)));   // the Throwing class's CritMultiplier stays

        // The statline likewise: the Dagger row's damage moved reaches the four and
        // their two uniques, and no other weapon.
        int damage = ShippedContent.Weapons.ClassOf(Dagger)!.Damage;
        var heavier = Plays(DataTexts.InClass(ShippedWeapons, Dagger, $"\"damage\": {damage}", $"\"damage\": {damage + 2}")).Weapons;
        Assert.All(daggers, id => Assert.Equal(damage + 2, heavier[id].Damage));
        Assert.All(heavier.All.Where(w => w.Class != Dagger), w => Assert.Equal(shipped[w.Id].Damage, w.Damage));
    }

    [Fact]
    public void AVariantsOwnNumberIsItsAlone()
    {
        // REWORK DECISION 1 (variant statline override): a variant may name any of
        // range, damage, cost and manaCost as its own, and that number plays in
        // place of its class's; the shipped file does so only for the Staff of
        // Renewal's and the Staff of Mire's mana. So the Staff row's manaCost moved
        // moves Warding and Blight, and Rotwood, made from Blight — and not Renewal
        // or Mire, nor the unique made from Renewal.
        var staff = ShippedContent.Weapons.ClassOf(Staff)!;
        var shipped = ShippedContent.Content.Weapons;
        Assert.All(new[] { "staff_of_renewal", "staff_of_mire" }, id => Assert.NotEqual(staff.ManaCost, shipped[id].ManaCost));

        int moved = staff.ManaCost + 2;
        var content = Plays(DataTexts.InClass(ShippedWeapons, Staff, $"\"manaCost\": {staff.ManaCost}", $"\"manaCost\": {moved}"));
        Assert.All(new[] { "staff_of_warding", "staff_of_blight", "rotwood" }, id => Assert.Equal(moved, content.Weapons[id].ManaCost));
        Assert.All(new[] { "staff_of_renewal", "staff_of_mire", "staff_of_renewal_unique" },
            id => Assert.Equal(shipped[id].ManaCost, content.Weapons[id].ManaCost));

        // A variant's own number is its alone the other way too: the Staff of
        // Mire's mana moved moves nothing else.
        int mire = shipped["staff_of_mire"].ManaCost;
        var ownEdit = Plays(DataTexts.InRow(ShippedWeapons, "staff_of_mire", $"\"manaCost\": {mire}", $"\"manaCost\": {mire + 5}"));
        Assert.Equal(mire + 5, ownEdit.Weapons["staff_of_mire"].ManaCost);
        Assert.All(ownEdit.Weapons.All.Where(w => w.Id != "staff_of_mire"), w => Assert.Equal(shipped[w.Id].ManaCost, w.ManaCost));
    }

    [Fact]
    public void AClassesKindIsItsRows()
    {
        // The kind is the row's: the Sword row made ranged makes a sword a ranged
        // weapon, so Brace — kept to melee weapons by restricted.json — is refused
        // on it, and the Spear Hall's drop table, computed from the relations over
        // the rows, loses the Sword with no dungeons.json edit.
        string ranged = DataTexts.InClass(ShippedWeapons, Sword, "\"kind\": \"melee\"", "\"kind\": \"ranged\"");
        var content = Plays(ranged);
        var shipped = ShippedContent.Content;
        var theme = shipped.Dungeons["spear_hall"].Theme;
        Assert.Equal(Brace, theme);
        Assert.Contains(Sword, shipped.Dungeons.ThemeClasses(theme));
        Assert.Equal(shipped.Dungeons.ThemeClasses(theme).Where(cls => cls != Sword), content.Dungeons.ThemeClasses(theme));
        foreach (var (before, after) in shipped.Dungeons.All.Zip(content.Dungeons.All))
            DungeonsJsonTests.Same(before, after);

        var melee = TestWeapons.Get("arming_sword");
        Assert.Equal(WeaponKind.Melee, melee.Kind);
        Assert.True(GameContent.Current.Modifiers.Allowed(Brace, melee.Kind, melee.Modifiers));
        using (TestContent.Use(weapons: WeaponsJson.Read(ranged)))
        {
            var sword = TestWeapons.Get("arming_sword");
            Assert.Equal(WeaponKind.Ranged, sword.Kind);
            Assert.False(sword.IsCaster);
            Assert.False(GameContent.Current.Modifiers.Allowed(Brace, sword.Kind, sword.Modifiers));
        }
    }

    [Fact]
    public void AClassesStatsAreItsRows()
    {
        // The stats a class levels on are its row's: the Spear row levelling on DEX
        // has C — the party's STR 4, DEX 2 — level spears on DEX 2, and climb the
        // ladder at DEX 2's pace rather than STR 4's (the ladder a member's
        // proficiency replays: Progression.Ladder over its governing stat).
        var c = StartingParty.Data.Members.Single(member => member.Id == "C").Stats;
        int perLevel = ShippedContent.Tuning.WeaponXpPerLevel;
        Assert.Equal(c.STR, Progression.GoverningStat(Spear, c));
        Assert.Equal(Progression.XpToNext(perLevel, c.STR), Progression.Ladder(0, Progression.GoverningStat(Spear, c)).XpToNext);

        string dexterous = DataTexts.InClass(ShippedWeapons, Spear, "\"stats\": [\"STR\"]", "\"stats\": [\"DEX\"]");
        Plays(dexterous);
        using (TestContent.Use(weapons: WeaponsJson.Read(dexterous)))
        {
            Assert.Equal([Stat.DEX], Progression.Governing(Spear));
            Assert.Equal(2, Progression.GoverningStat(Spear, c));
            Assert.Equal(Progression.XpToNext(perLevel, 2), Progression.Ladder(0, Progression.GoverningStat(Spear, c)).XpToNext);
            Assert.Equal(c.STR, Progression.GoverningStat(Axe, c));   // the Axe row still levels on STR
        }
    }

    [Fact]
    public void AClassRowMissingOrMisshapenStopsTheGame()
    {
        // Every case is a weapons.json beside the game, loaded as the game loads it:
        // the game does not start, and the load says why — weapons.json, the class,
        // the rule — never an exception out of it. The reader refuses what it
        // cannot compose over (a class with no row, a name that is no class, a kind
        // or a stat that is no word it knows, the flat format), as the file's parse
        // failure; the validator the rest, the class rows first.
        static string WithoutRows(string text, params string[] ids) => ids.Aggregate(text, DataTexts.WithoutRow);
        (string Text, string Entry, string Rule)[] cases =
        [
            // No Wand row.
            (DataTexts.WithoutClass(ShippedWeapons, Wand), "Wand", ContentValidator.RuleEveryClassHasARow),
            // A name that is no class, pointing at the one it means.
            (DataTexts.Edit(ShippedWeapons, "\"Ranged\": {", "\"Bow\": {"), "Bow", ContentValidator.RuleFileShape),
            // A kind that is none of the three words: the relations' rule for them.
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"kind\": \"melee\"", "\"kind\": \"Melee\""), "Dagger", ContentValidator.RuleKindValue),
            // A class the code does not cast with, casting; and one it does, not.
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"kind\": \"melee\"", "\"kind\": \"caster\""), "Dagger", ContentValidator.RuleCasterClass),
            (DataTexts.InClass(ShippedWeapons, Staff, "\"kind\": \"caster\"", "\"kind\": \"melee\""), "Staff", ContentValidator.RuleCasterClass),
            // Stats: none, one named twice, CON — and a name that is no stat.
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"stats\": [\"DEX\"]", "\"stats\": []"), "Dagger", ContentValidator.RuleClassStats),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"stats\": [\"DEX\"]", "\"stats\": [\"DEX\", \"DEX\"]"), "Dagger", ContentValidator.RuleClassStats),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"stats\": [\"DEX\"]", "\"stats\": [\"CON\"]"), "Dagger", ContentValidator.RuleClassStats),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"stats\": [\"DEX\"]", "\"stats\": [\"STAMINA\"]"), "Dagger", ContentValidator.RuleFileShape),
            // The Ranged row signed CritWindow over its CritWindow and Longshot
            // baseline: a sound baseline, and the Dagger's signature.
            (DataTexts.InClass(ShippedWeapons, Ranged, "\"signature\": \"Longshot\"", "\"signature\": \"CritWindow\""), "Ranged", ContentValidator.RuleClassSignatureDistinct),
            // A martial row with no signature, and a Dagger baseline without CritWindow.
            (DataTexts.InClass(ShippedWeapons, Sword, "\"signature\": \"Block\"", "\"signature\": null"), "Sword", ContentValidator.RuleClassBaseline),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"CritWindow\": ", "\"Pin\": "), "Dagger", ContentValidator.RuleClassBaseline),
            // A caster row naming a signature.
            (DataTexts.InClass(ShippedWeapons, Staff, "\"signature\": null", "\"signature\": \"Resonant\""), "Staff", ContentValidator.RuleCasterForged),
            // Every Spear variant gone — and Hoplite's Wall, made from the Phalanx
            // Spear, with them, or the reader refuses it first for deriving from
            // nothing: the placer draws spearmen, so the class must field one.
            (WithoutRows(ShippedWeapons, "hoplites_wall", "skirmishers_pike", "phalanx_spear", "halberd", "pinning_lance"), "Spear", ContentValidator.RuleCodeNamedWeaponsExist),
            // The flat format the class section replaced.
            (FlatFormat, WeaponsJson.FileName, ContentValidator.RuleFileShape),
        ];
        foreach (var (text, entry, rule) in cases)
            Assert.Equal((WeaponsJson.FileName, false, entry, rule), Stamp(Fails(text)));

        // The reader's own refusals, read directly, say what they are: the class
        // missing, the class meant, the way back from the flat format.
        var noWand = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.WithoutClass(ShippedWeapons, Wand)));
        Assert.Equal(("Wand", ContentValidator.RuleEveryClassHasARow, "weapons.json: classes: has no Wand row - each of the eight classes has one"),
            (noWand.EntryId, noWand.Rule, noWand.Detail));
        var bow = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.Edit(ShippedWeapons, "\"Ranged\": {", "\"Bow\": {")));
        Assert.Equal("weapons.json: classes.Bow: is not a class - did you mean Ranged?", bow.Detail);
        var flat = Assert.Throws<ContentException>(() => WeaponsJson.Read(FlatFormat));
        Assert.Equal("weapons.json: classes: this file predates the classes section - delete it and the built-in weapons play, or run --write-data <exe folder>",
            flat.Detail);
        var spear = Fails(WithoutRows(ShippedWeapons, "hoplites_wall", "skirmishers_pike", "phalanx_spear", "halberd", "pinning_lance"));
        Assert.Contains("EnemyPlacer.PlacedClasses", spear.Detail);

        // Records built in code bypass the reader, and meet the same rule in the
        // validator: a Wand row missing, or a Dagger row given twice.
        var noWandRow = ShippedContent.Weapons with { Classes = ShippedContent.Weapons.Classes.Where(row => row.Class != Wand).ToList() };
        Assert.Equal((WeaponsJson.FileName, false, "Wand", ContentValidator.RuleEveryClassHasARow),
            Stamp(Assert.Throws<ContentException>(() => TestContent.Load(weapons: noWandRow))));
        var twoDaggerRows = ShippedContent.Weapons with { Classes = [.. ShippedContent.Weapons.Classes, ShippedContent.Weapons.ClassOf(Dagger)!] };
        Assert.Equal((WeaponsJson.FileName, false, "Dagger", ContentValidator.RuleEveryClassHasARow),
            Stamp(Assert.Throws<ContentException>(() => TestContent.Load(weapons: twoDaggerRows))));
    }
}
