using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The weapon types are a section of <c>weapons.json</c> (the content rework, 1d;
/// the user's ruling of 2026-09-25, "weapon types ... fully in json", which rules
/// Phase 5 USER DECISION 4): each of the eight classes is a row — the kind it
/// plays as, the stats it levels on, its signature, its baseline, its statline
/// and, for a caster, the order its drop rolls its variants in (the rework's
/// audit) — and every question the game asks a class is a read of the rows.
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

    /// <summary>The Staff row's drop order as the shipped file writes it, whatever its ids: the one line the edits below change.</summary>
    private static string StaffDropOrder(string text)
    {
        var order = System.Text.RegularExpressions.Regex.Matches(text, "\"dropOrder\": \\[[^\\]]*\"staff_of_[^\\]]*\\]");
        Assert.True(order.Count == 1, $"the Staff row's dropOrder is in the text {order.Count} times, not once");
        return order[0].Value;
    }

    /// <summary>A fifth staff, written as a variant of the file's own, after the Staff of Mire.</summary>
    private static string WithAFifthStaff(string text) => DataTexts.Edit(text,
        "\"enchantments\": [{ \"id\": \"mire\" }], \"shape\": null\n    },",
        "\"enchantments\": [{ \"id\": \"mire\" }], \"shape\": null\n    },\n    {\n      \"id\": \"staff_of_embers\",\n"
        + "      \"name\": \"Staff of Embers\", \"class\": \"Staff\", \"role\": null, \"adds\": {},\n"
        + "      \"enchantments\": [{ \"id\": \"poison\" }], \"shape\": null\n    },");

    [Fact]
    public void ACastersRowIsItsBaselineAndItsDropOrder()
    {
        // The rework's audit: what a caster's row carries plays, as a martial row's
        // does. Its baseline is its own — the load asks it what it asks any baseline
        // and no longer holds it to Resonant x1 — so the Staff row forging Block
        // beside its Resonant re-bases every staff, the uniques made from them too,
        // and loads with nothing to say.
        string[] staves = ["staff_of_renewal", "staff_of_warding", "staff_of_blight", "staff_of_mire", "rotwood", "staff_of_renewal_unique"];
        var shipped = ShippedContent.Content.Weapons;
        Assert.Equal(staves, shipped.All.Where(w => w.Class == Staff).Select(w => w.Id));
        var blocking = Plays(DataTexts.InClass(ShippedWeapons, Staff, "\"baseline\": { \"Resonant\": 1 }", "\"baseline\": { \"Resonant\": 1, \"Block\": 1 }")).Weapons;
        Assert.All(staves, id => Assert.Equal(
            shipped[id].Forged.Append(new(Block, 1)).OrderBy(f => f.Key),
            blocking[id].Forged.OrderBy(f => f.Key)));
        Assert.All(blocking.All.Where(w => w.Class != Staff),
            w => Assert.Equal(shipped[w.Id].Forged.OrderBy(f => f.Key), w.Forged.OrderBy(f => f.Key)));   // the Wand's Resonant stays alone

        // Its drop order is its own too — weapons.json's, where it was a list in
        // code: a fifth staff written into the variants and ordered after the four
        // is one the draw can land on, with no code edit.
        string order = StaffDropOrder(ShippedWeapons);
        string ordered = DataTexts.InClass(WithAFifthStaff(ShippedWeapons), Staff, order, order[..^1] + ", \"staff_of_embers\"]");
        var five = Plays(ordered).Weapons;
        Assert.Equal("staff_of_embers", five.Class(Staff).DropOrder![^1]);
        Assert.Equal(shipped.Class(Staff).DropOrder!.Count + 1, LootTable.VariantCount(five, Staff));
        Assert.Equal("staff_of_embers", LootTable.VariantOf(five, Staff, LootTable.VariantCount(five, Staff) - 1).Id);

        // A staff the order leaves out is a variant that never drops, not a fault:
        // written in and not ordered, it loads and the draw is as it was; and a
        // shipped staff taken out of the order loads, and drops no more.
        var unordered = Plays(WithAFifthStaff(ShippedWeapons)).Weapons;
        Assert.Equal(shipped.Class(Staff).DropOrder, unordered.Class(Staff).DropOrder);
        Assert.Contains(unordered["staff_of_embers"], unordered.ByClass(Staff));
        var noMire = Plays(DataTexts.InClass(ShippedWeapons, Staff, order, order.Replace(", \"staff_of_mire\"", "", StringComparison.Ordinal))).Weapons;
        Assert.DoesNotContain("staff_of_mire", noMire.Class(Staff).DropOrder!);
        Assert.Equal(shipped.Class(Staff).DropOrder!.Count - 1, LootTable.VariantCount(noMire, Staff));
        Assert.Contains(noMire["staff_of_mire"], noMire.ByClass(Staff));
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
            // A baseline the forge refuses, asked of the row as a weapon's spread is
            // asked — the reader sums it into every variant, and no variant's row
            // holds it: a stack of 0; Brace on the Ranged row, which restricted.json
            // keeps to melee weapons; x4, past any forge limit a tuning can set.
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"CritMultiplier\": 1", "\"CritMultiplier\": 0"), "Dagger", ContentValidator.RuleForgedStackCount),
            (DataTexts.InClass(ShippedWeapons, Ranged, "\"CritWindow\": 1", "\"Brace\": 1"), "Ranged", ContentValidator.RuleForgedNotAllowed),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"CritMultiplier\": 1", "\"CritMultiplier\": 4"), "Dagger", ContentValidator.RuleMaxForged),
            // A martial row with no signature, and a Dagger baseline without CritWindow.
            (DataTexts.InClass(ShippedWeapons, Sword, "\"signature\": \"Block\"", "\"signature\": null"), "Sword", ContentValidator.RuleClassBaseline),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"CritWindow\": ", "\"Pin\": "), "Dagger", ContentValidator.RuleClassBaseline),
            // A caster row naming a signature, or forging nothing — a caster's two
            // axes are a modifier and its innate — and a staff forged with more
            // than its class's baseline.
            (DataTexts.InClass(ShippedWeapons, Staff, "\"signature\": null", "\"signature\": \"Resonant\""), "Staff", ContentValidator.RuleCasterForged),
            (DataTexts.InClass(ShippedWeapons, Staff, "\"baseline\": { \"Resonant\": 1 }", "\"baseline\": {}"), "Staff", ContentValidator.RuleCasterForged),
            (DataTexts.InRow(ShippedWeapons, "staff_of_warding", "\"adds\": {}", "\"adds\": { \"Block\": 1 }"), "staff_of_warding", ContentValidator.RuleCasterForged),
            // A caster's drop order: left out; not a list; naming a wand, a staff
            // unique, or a staff twice; and a martial row naming one at all.
            (DataTexts.InClass(ShippedWeapons, Staff, ",\n      " + StaffDropOrder(ShippedWeapons), ""), "Staff", ContentValidator.RuleCasterVariantPool),
            (DataTexts.InClass(ShippedWeapons, Staff, StaffDropOrder(ShippedWeapons), "\"dropOrder\": \"staff_of_renewal\""), "Staff", ContentValidator.RuleFileShape),
            (DataTexts.InClass(ShippedWeapons, Staff, "\"staff_of_renewal\", ", "\"staff_of_renewal\", \"wand_of_the_blast\", "), "wand_of_the_blast", ContentValidator.RuleCasterVariantPool),
            (DataTexts.InClass(ShippedWeapons, Staff, "\"staff_of_renewal\", ", "\"staff_of_renewal\", \"rotwood\", "), "rotwood", ContentValidator.RuleCasterVariantPool),
            (DataTexts.InClass(ShippedWeapons, Staff, "\"staff_of_renewal\", ", "\"staff_of_renewal\", \"staff_of_renewal\", "), "staff_of_renewal", ContentValidator.RuleCasterVariantPool),
            (DataTexts.InClass(ShippedWeapons, Dagger, "\"signature\": \"CritWindow\"", "\"signature\": \"CritWindow\", \"dropOrder\": [\"flensing_knife\"]"), "Dagger", ContentValidator.RuleCasterVariantPool),
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

        // A baseline's fault says it is the baseline's, and a relation's names the
        // relation, as a weapon's does.
        Assert.Equal("baseline CritMultiplier x0",
            Fails(DataTexts.InClass(ShippedWeapons, Dagger, "\"CritMultiplier\": 1", "\"CritMultiplier\": 0")).Detail);
        Assert.Equal("baseline Brace x1 on a Ranged weapon beside Brace x1, Longshot x1 - restricted.json kind Brace: melee",
            Fails(DataTexts.InClass(ShippedWeapons, Ranged, "\"CritWindow\": 1", "\"Brace\": 1")).Detail);
        Assert.Equal($"baseline CritMultiplier x4, MaxForged {ShippedContent.Tuning.MaxForged[CritMultiplier]}",
            Fails(DataTexts.InClass(ShippedWeapons, Dagger, "\"CritMultiplier\": 1", "\"CritMultiplier\": 4")).Detail);

        // Records built in code bypass the reader, and meet the same rule in the
        // validator: a Wand row missing, or a Dagger row given twice.
        var noWandRow = ShippedContent.Weapons with { Classes = ShippedContent.Weapons.Classes.Where(row => row.Class != Wand).ToList() };
        Assert.Equal((WeaponsJson.FileName, false, "Wand", ContentValidator.RuleEveryClassHasARow),
            Stamp(Assert.Throws<ContentException>(() => TestContent.Load(weapons: noWandRow))));
        var twoDaggerRows = ShippedContent.Weapons with { Classes = [.. ShippedContent.Weapons.Classes, ShippedContent.Weapons.ClassOf(Dagger)!] };
        Assert.Equal((WeaponsJson.FileName, false, "Dagger", ContentValidator.RuleEveryClassHasARow),
            Stamp(Assert.Throws<ContentException>(() => TestContent.Load(weapons: twoDaggerRows))));

        // And a class that fields no weapon has its baseline asked all the same:
        // every bow gone, and the Ranged row forging Brace.
        var noBows = ShippedContent.Weapons with
        {
            Classes = ShippedContent.Weapons.Classes
                .Select(row => row.Class == Ranged ? row with { Baseline = new Dictionary<ModifierType, int> { [Brace] = 1, [Longshot] = 1 } } : row)
                .ToList(),
            Weapons = ShippedContent.Weapons.Weapons.Where(w => w.Class != Ranged).ToList(),
        };
        Assert.Equal((WeaponsJson.FileName, false, "Ranged", ContentValidator.RuleForgedNotAllowed),
            Stamp(Assert.Throws<ContentException>(() => TestContent.Load(weapons: noBows))));
    }
}
