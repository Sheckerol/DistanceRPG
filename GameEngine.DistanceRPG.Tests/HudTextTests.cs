using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The HUD's text — presentation, but it encodes rules the docs fix: a cost
/// reads as the resolved number, never a percentage (§1.1, §1.3); modifiers
/// read as resolved values for the weapon in a wielder's hands, weapon plus
/// innate; a lingering element's status is named by its element; Mire past
/// the whole budget is paralysis; and the two crit riders carry their flavour
/// (§1.6). The drawing itself is untested — bar the one arithmetic the
/// inventory panel does, which decides whether what it draws fits the window
/// it is drawn in (§2.2); these are its strings.
/// </summary>
public class HudTextTests
{
    private static PartyMemberState Holding(string weaponId, DamageType? element = null)
        => TestPools.Holding("A", TestWeapons.Get(weaponId, element));

    private static string[] Badges(ActorState actor) => DungeonHud.StatusBadgeRuns(actor).Select(r => r.Text).ToArray();

    private static string[] Plate(EnemyState enemy) => DungeonHud.NameplateRuns(enemy).Select(r => r.Text).ToArray();

    private static string[] Item(Drop drop) => DungeonHud.GroundItemRuns(drop).Select(r => r.Text).ToArray();

    /// <summary>Four fixture members in roster order, a Weakspot Stiletto each: a party to walk a real visit's floors with.</summary>
    private static PartyMemberState[] Party()
        => new[] { "A", "B", "C", "D" }
            .Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i))
            .ToArray();

    /// <summary>
    /// Stand the first <paramref name="count"/> members round <paramref name="stair"/>
    /// as a party that walked there would — around living enemies, never on them —
    /// and leave the rest where they are.
    /// </summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) stair, int count)
    {
        var floor = visit.Current;
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, stair.R, stair.C, count, taken);
        for (int i = 0; i < count; i++)
            (party[i].X, party[i].Y) = Displacer.CentreOf(tiles[i].R, tiles[i].C);
    }

    /// <summary>Gather the whole party at the down-stairs, take them, and open the floor below.</summary>
    private static void GoDown(DungeonState visit, IReadOnlyList<PartyMemberState> party)
    {
        Gather(visit, party, visit.Current.DownStairs!.Value, party.Count);
        visit.Descend(party);
        visit.OpenTurns(party, () => 10);
    }

    [Fact]
    public void WeaponStats_ShowTheResolvedCosts_NeverAPercentage()
    {
        Assert.Equal("DMG 15  RNG 40  COST 27", DungeonHud.WeaponStats(TestWeapons.Get("flensing_knife")));      // 30 less Light x1
        Assert.Equal("RNG 100  COST 40  MANA 13", DungeonHud.WeaponStats(TestWeapons.Get("staff_of_renewal")));  // 15 less Resonant x1
        Assert.Equal("DMG 8  NOVA  COST 45  MANA 18", DungeonHud.WeaponStats(TestWeapons.Get("wand_of_the_nova", DamageType.Flaming)));
    }

    [Fact]
    public void ModifierReadout_IsResolvedValuesInTableOrder_TheCritPairOneEntry()
    {
        Assert.Equal("CRIT 19+ x3  LIGHT", DungeonHud.ModifierReadout(TestWeapons.Get("flensing_knife")));
        Assert.Equal("BLOCK 6  PUSH 1", DungeonHud.ModifierReadout(TestWeapons.Get("tower_guard")));
        Assert.Equal("CLEAVE 1  SPLITTING 6  OPPORTUNIST 1", DungeonHud.ModifierReadout(TestWeapons.Get("reaver")));
        Assert.Equal("CRIT 17+ x3", DungeonHud.ModifierReadout(TestWeapons.Get("widowmaker")));
        Assert.Equal("RESONANT", DungeonHud.ModifierReadout(TestWeapons.Get("staff_of_mire")));
    }

    [Fact]
    public void ModifierReadout_CountsTheEquippedWeaponsChargesLeft_AndItsShotsHeld()
    {
        var thrower = Holding("bandolier");
        Assert.Equal("CRIT 20 x3  CHARGES 2/3", DungeonHud.ModifierReadout(thrower.EquippedWeapon!, thrower, attacksThisTurn: 1));
        Assert.Equal("CRIT 20 x3  CHARGES 3", DungeonHud.ModifierReadout(TestWeapons.Get("bandolier"), thrower));   // in the bag: the cap

        var archer = Holding("crossbow");
        Assert.Equal("CRIT 19+ x2  LONGSHOT 1  OVERWATCH 1", DungeonHud.ModifierReadout(archer.EquippedWeapon!, archer));
        archer.HeldShots = 1;
        Assert.Equal("CRIT 19+ x2  LONGSHOT 1  OVERWATCH 1 HELD", DungeonHud.ModifierReadout(archer.EquippedWeapon!, archer));
    }

    [Fact]
    public void ModifierReadout_ReadsWeaponPlusInnate()
    {
        var golem = Holding("tower_guard");
        golem.Innate = ModifierSet.Of((Block, 1));
        Assert.Equal("BLOCK 9  PUSH 1", DungeonHud.ModifierReadout(golem.EquippedWeapon!, golem));
    }

    [Fact]
    public void EnchantmentReadout_KeepsAttachmentOrder_AndShowsTiersAboveOne()
        => Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(TestWeapons.Get("flensing_knife_unique")));

    [Fact]
    public void EnchantmentReadout_MarksTheLockAndWhatIsAsleep()
    {
        // In a wielder's hands each entry names what it takes out of that
        // wielder's pool, and one whose lock went unpaid says so: a dormant
        // entry is a non-event the player has otherwise no way to see (section
        // 3.1). The Nameless Knife is the case that bites -- 20 + 60 + 25 wanted
        // of a hundred-point pool, so the last of the three sleeps.
        var knife = TestWeapons.Get("flensing_knife_unique");
        var member = TestPools.Char("A");
        member.Inventory[0] = knife;

        Assert.Equal((80, 20), (member.PaidLocks, member.UsableMaxMana));
        Assert.Equal("SERRATED LOCK 20  VAMPIRIC T3 LOCK 60 XP 0/60  OVERHEAL LOCK 25 (ASLEEP)",
            DungeonHud.EnchantmentReadout(knife, member));

        // Grown past all three, nothing is asleep and the locks still read.
        while (member.IsDormant(2))
            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));
        Assert.Equal("SERRATED LOCK 20  VAMPIRIC T3 LOCK 60 XP 0/60  OVERHEAL LOCK 25",
            DungeonHud.EnchantmentReadout(knife, member));

        // A lock is a reservation out of a pool, so it reads only where there is
        // one: the same item in the bag, or asked about with no wielder at all,
        // prints its entries alone.
        var bagged = TestPools.Char("B");
        bagged.Inventory[1] = knife;
        Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(knife, bagged));
        Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(knife));
    }

    [Fact]
    public void EnchantmentReadout_ShowsTheTierAndItsProgress()
    {
        // Tier is earned by use (section 3.3), and the bar is the only thing
        // that says a swing is buying anything: an equipped entry reads XP n/m
        // beside its lock, where m is that tier's own cost over the wielder's INT
        // -- the same number the climb charges, TierCost, so the row and the
        // ladder cannot state two prices.
        var knife = TestWeapons.Get("flensing_knife_unique");
        var member = TestPools.Char("A");
        member.Inventory[0] = knife;
        while (member.IsDormant(2))
            member.Credit(new XpCredit(XpPool.Mana, null, member.ManaPool.XpToNext));

        // Nothing spent yet, and the bar still reads: XP 0/60 is what says this
        // circle can be deepened at all, and an entry that crosses its bar
        // exactly lands back here rather than losing the row at the moment it has
        // just earned a tier. A unique has no next tier, so it prints no bar --
        // and with nothing banked, nothing at all.
        Assert.Equal("SERRATED LOCK 20  VAMPIRIC T3 LOCK 60 XP 0/60  OVERHEAL LOCK 25",
            DungeonHud.EnchantmentReadout(knife, member));

        // One swing's worth of triggers: Serrated and Overheal are uniques, so
        // they bank a figure and print no bar -- there is no next tier to be
        // partway to -- while Vampiric is climbing its tier-3 bar of sixty.
        knife.CreditEnchantment(0, 2, InnateStats.Low);
        knife.CreditEnchantment(1, 2, InnateStats.Low);
        knife.CreditEnchantment(2, 8, InnateStats.Low);
        Assert.Equal("SERRATED LOCK 20 XP 2  VAMPIRIC T3 LOCK 60 XP 2/60  OVERHEAL LOCK 25 XP 8",
            DungeonHud.EnchantmentReadout(knife, member));

        // The bar is the wielder's INT dividing the tier's cost, so it is the
        // wielder's number: a wizard holding the same knife is a quarter of the
        // way where a member with no nature is a thirtieth.
        var wizard = TestPools.Char("B", stats: new InnateStats(STR: 1, DEX: 2, CON: 3, INT: 4));
        wizard.Inventory[0] = knife;
        while (wizard.IsDormant(2))
            wizard.Credit(new XpCredit(XpPool.Mana, null, wizard.ManaPool.XpToNext));
        Assert.Contains("VAMPIRIC T3 LOCK 60 XP 2/15", DungeonHud.EnchantmentReadout(knife, wizard));

        // Like the lock, progress reads only where there is a wielder: in the
        // bag or asked about with none, the row is the entry's name and tier.
        Assert.Equal("SERRATED  VAMPIRIC T3  OVERHEAL", DungeonHud.EnchantmentReadout(knife));
    }

    [Fact]
    public void ManaReadout_NamesTheSpendableCeilingWhereTheLocksTookThePool()
    {
        // The budget line is where a lock would otherwise be invisible: with the
        // Nameless Knife in hand the bar tops out at 20 forever, and a reader
        // told only "/ 100" has no way to see why. So where anything is reserved
        // the spendable ceiling is the figure and the earned pool follows it;
        // where nothing is, the line is the one it has always been.
        var plain = TestPools.Char("A");
        Assert.Equal((0, 100, 100), (plain.PaidLocks, plain.UsableMaxMana, plain.Mana));
        Assert.Equal("MANA: 100 / 100", DungeonHud.ManaReadout(plain));

        var locked = TestPools.Char("B");
        locked.Inventory[0] = TestWeapons.Get("flensing_knife_unique");
        Assert.Equal((80, 20, 20), (locked.PaidLocks, locked.UsableMaxMana, locked.Mana));
        Assert.Equal("MANA: 20 / 20 (100)", DungeonHud.ManaReadout(locked));
    }

    [Fact]
    public void StatusBadges_NameALingeringElementByItsElement_HideThePool_AndLeadWithParalysis()
    {
        var enemy = new EnemyState();
        enemy.ApplyStatus(Searing, DamageType.Flaming, 3);
        enemy.ApplyStatus(Searing, DamageType.Cold, 2);
        enemy.ApplyStatus(Regeneration, null, 4);
        enemy.ApplyStatus(OverhealPool, null, 3);
        Assert.Equal(new[] { "BURN 3", "FRST 2", "+4" }, Badges(enemy));
        Assert.All(Badges(enemy), badge => Assert.True(badge.Split(' ')[0].Length <= 4, badge));   // four letters fit the 5-px font

        enemy.ApplyStatus(Mire, null, 10);   // ten levels at 10% each: no budget left at all
        Assert.True(DungeonHud.IsParalysed(enemy));
        Assert.Equal(new[] { "PARA", "BURN 3", "FRST 2", "+4", "MIRE 10" }, Badges(enemy));
    }

    [Fact]
    public void Nameplate_ReadsAsAThreatAndARewardAtOnce()
    {
        // A dummy nobody has put down yet reads as it always did.
        Assert.Equal(new[] { "50/50" }, Plate(new EnemyState()));

        // A farmed one carries its DefeatCount, and that one number is honest
        // about being two things: the depth of the drop it is holding and how
        // much harder it hits than the dummy beside it (3.2). The maximum it is
        // read against is its own, revivals included, and the damage its revivals
        // added stands beside the count: the earned statline (4.5, PHASE4-137).
        var farmed = new EnemyState { DefeatCount = 4, RevivalMaxHp = 17, RevivalDamage = 6 };
        Assert.Equal(new[] { "67/67", "x4", "+6 DMG" }, Plate(farmed));

        // It sits between the pool and the badges, so what is left of it and how
        // many times this has happened are adjacent.
        farmed.Hp = 12;
        farmed.ApplyStatus(Sundered, null, 2);
        Assert.Equal(new[] { "12/67", "x4", "+6 DMG", "SUND 2" }, Plate(farmed));
    }

    [Fact]
    public void ABossPlateNamesItAndWhatItCarries()
    {
        // The golem's plate names it and what it is holding — it wields a weapon
        // like a dummy does (§4.3) — and reads its HP and the Block it carries on
        // itself, as a hit meets it: weapon plus innate.
        var tutorial = GameContent.Current.Dungeons.Tutorial;
        var golem = DungeonState.BossRow(tutorial, TestWeapons.Get("hatchet"));
        Assert.Equal("STONE GOLEM [Hatchet]", DungeonHud.EnemyTitle(golem));
        Assert.Equal(new[] { "80/80", "BLOCK 9" }, Plate(golem));
        Assert.Equal("BLOCK 9", DungeonHud.InnateReadout(golem));

        golem.Hp = 62;
        Assert.Equal(new[] { "62/80", "BLOCK 9" }, Plate(golem));

        // The Block its blows meet, a sword's included: never its own stacks alone.
        Assert.Equal(new[] { "80/80", "BLOCK 12" }, Plate(DungeonState.BossRow(tutorial, TestWeapons.Get("arming_sword"))));
        Assert.Equal(new[] { "80/80", "BLOCK 15" }, Plate(DungeonState.BossRow(tutorial, TestWeapons.Get("tower_guard"))));

        // Another dungeon's boss reads its own theme in the readout's own words:
        // the Knife Mother's CritWindow x3 on a Weakspot Stiletto's x1 is 16+.
        var warrens = GameContent.Current.Dungeons["knife_warrens"];
        var mother = DungeonState.BossRow(warrens, TestWeapons.Get("weakspot_stiletto"));
        Assert.Equal("KNIFE MOTHER [Weakspot Stiletto]", DungeonHud.EnemyTitle(mother));
        Assert.Equal("CRIT 16+ x3", DungeonHud.InnateReadout(mother));

        // The golem the shipped seed's tutorial generates.
        var bottom = Floor.Generate(2762136374, 1, bottom: true, tutorial);
        Assert.Equal("STONE GOLEM [Pinning Lance]", DungeonHud.EnemyTitle(bottom.Boss!));
        Assert.Equal(new[] { "80/80", "BLOCK 9" }, Plate(bottom.Boss!));

        // Rank and file are named by what they hold, as they always were, and
        // carry nothing on themselves to print.
        Assert.Equal("DUMMY [Arming Sword]", DungeonHud.EnemyTitle(new EnemyState()));
        Assert.Equal("HEALER [Staff of Renewal]", DungeonHud.EnemyTitle(new EnemyState { Weapon = TestWeapons.Get("staff_of_renewal") }));
        Assert.Equal("CASTER [Staff of Mire]", DungeonHud.EnemyTitle(new EnemyState { Weapon = TestWeapons.Get("staff_of_mire") }));
        Assert.Equal("", DungeonHud.InnateReadout(new EnemyState()));
    }

    [Fact]
    public void AFarmedPlateShowsTheStatlineItEarned()
    {
        // PHASE4-119, -137: the count is on the plate so the choice of which dummy
        // to go back for is informed, and it reads as a threat as well as a reward:
        // the earned statline stands next to it. Farmed three cycles through the
        // turn system itself — on a seed whose first rolls are Damage, then Both —
        // the HP its revivals added is in the maximum it is read against, and the
        // damage they added follows the count.
        var member = TestPools.Holding("A", TestWeapons.Make("Chisel", range: 60, damage: 25, cost: 20),
            x: 5 * GameConstants.Tile + 16, y: 5 * GameConstants.Tile + 16);
        var dummy = new EnemyState
        {
            X = 5 * GameConstants.Tile + 16 + 40f,
            Y = 5 * GameConstants.Tile + 16,
            Weapon = TestWeapons.Get("hatchet"),
        };
        var turns = new TurnSystem(new int[20, 20], new[] { member }, new[] { dummy }, () => 10, mapSeed: 1);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            while (dummy.Alive)
                Assert.True(turns.TryAttack(member, dummy));
            while (!dummy.Alive)
            {
                turns.EndTurn();
                for (float t = 0f; t < 3f; t += 1f / 30f)
                    turns.Update(1f / 30f);
            }
        }

        Assert.Equal(3, dummy.DefeatCount);
        Assert.True(dummy.RevivalDamage > 0 && dummy.RevivalMaxHp > 0);
        Assert.Equal(GameConstants.DummyHp + dummy.RevivalMaxHp, dummy.MaxHp);
        Assert.Equal(new[] { $"{dummy.MaxHp}/{dummy.MaxHp}", "x3", $"+{dummy.RevivalDamage} DMG" }, Plate(dummy));
        Assert.Equal(dummy.RevivalDamage, dummy.BonusDamage);   // what the plate says it adds is what its swings add

        // Hurt, the pool comes first; the count, what it hits for, then its badges.
        dummy.Hp = 9;
        dummy.ApplyStatus(Sundered, null, 1);
        Assert.Equal(new[] { $"9/{dummy.MaxHp}", "x3", $"+{dummy.RevivalDamage} DMG", "SUND 1" }, Plate(dummy));

        // A dummy whose revivals have added no damage prints none: the HP half is its maximum.
        Assert.Equal(new[] { "58/58", "x1" }, Plate(new EnemyState { DefeatCount = 1, RevivalMaxHp = 8 }));
    }

    [Fact]
    public void TheCountReddensAsItClimbs_WithNoDenominator()
    {
        // PHASE4-139: the count is unbounded, so it never renders as n/5 or any
        // fraction; "further is worse" is made legible by colour instead. The count
        // is a bare xN at every depth, and its colour only ever reddens — never
        // less red one cycle deeper — until the farm's unique midpoint, where it is
        // as red as it gets and stays there.
        var previous = DungeonHud.ThreatColor(1);
        for (int n = 1; n <= 60; n++)
        {
            var count = DungeonHud.NameplateRuns(new EnemyState { DefeatCount = n })[1];
            Assert.Equal($"x{n}", count.Text);
            Assert.DoesNotContain("/", count.Text);
            Assert.Equal(DungeonHud.ThreatColor(n), count.Color);

            var color = DungeonHud.ThreatColor(n);
            Assert.True(color.Y <= previous.Y && color.X >= previous.X, $"x{n} is less red than x{n - 1}");
            previous = color;
        }

        int hot = GameContent.Current.Tuning.UniqueChanceMidpoint;
        Assert.True(DungeonHud.ThreatColor(hot).Y < DungeonHud.ThreatColor(1).Y);
        Assert.True(DungeonHud.ThreatColor(hot / 2).Y < DungeonHud.ThreatColor(1).Y);
        Assert.True(DungeonHud.ThreatColor(hot).Y < DungeonHud.ThreatColor(hot / 2).Y);
        Assert.Equal(DungeonHud.ThreatColor(hot), DungeonHud.ThreatColor(hot + 40));

        // Nothing on a plate reads as a fraction but the pool's own HP.
        var farmed = new EnemyState { DefeatCount = 12, RevivalDamage = 9, RevivalMaxHp = 24 };
        Assert.Single(Plate(farmed), run => run.Contains('/'));
    }

    [Fact]
    public void ARevivalSaysWhatItGained()
    {
        // PHASE4-138: a beat when a revival rolls its bonus, so the player
        // connects the count going up with the dummy getting stronger.
        Assert.Equal("STRONGER +3 DMG +8 HP", DungeonHud.RevivalBeat(3, 8));
        Assert.Equal("STRONGER +3 DMG", DungeonHud.RevivalBeat(3, 0));
        Assert.Equal("STRONGER +8 HP", DungeonHud.RevivalBeat(0, 8));
        Assert.Null(DungeonHud.RevivalBeat(0, 0));

        // Through a real revival: what the row gained between going down and
        // getting up — a clean kill's two cycles, on a seed whose first two roll
        // Damage then Both — is what the beat says.
        var member = TestPools.Holding("A", TestWeapons.Make("Maul", range: 60, damage: 50, cost: 30),
            x: 5 * GameConstants.Tile + 16, y: 5 * GameConstants.Tile + 16);
        var dummy = new EnemyState
        {
            X = 5 * GameConstants.Tile + 16 + 40f,
            Y = 5 * GameConstants.Tile + 16,
            Weapon = TestWeapons.Get("hatchet"),
        };
        var turns = new TurnSystem(new int[20, 20], new[] { member }, new[] { dummy }, () => 10, mapSeed: 1);
        string? beat = null;
        var before = (dummy.RevivalDamage, dummy.RevivalMaxHp);
        turns.EnemyResurrected += e => beat = DungeonHud.RevivalBeat(e.RevivalDamage - before.RevivalDamage, e.RevivalMaxHp - before.RevivalMaxHp);
        Assert.True(turns.TryAttack(member, dummy));
        Assert.Equal((false, 2), (dummy.Alive, dummy.DefeatCount));
        while (!dummy.Alive)
        {
            turns.EndTurn();
            for (float t = 0f; t < 3f; t += 1f / 30f)
                turns.Update(1f / 30f);
        }

        Assert.True(dummy.RevivalDamage > 0 && dummy.RevivalMaxHp > 0);
        Assert.Equal($"STRONGER +{dummy.RevivalDamage} DMG +{dummy.RevivalMaxHp} HP", beat);
    }

    [Fact]
    public void TheBossFloorBannerStatesTheTradeBeforeTheFight()
    {
        // Settled's backlog: surface the boss floor's trade in the encounter
        // before a player commits (§4.3). Arriving on the boss floor with the boss
        // standing, the banner says who waits there and what beating it costs —
        // the tutorial closes for good; anywhere else the dungeon locks for as many
        // runs as its floor, and leaving without the fight re-rolls the depth.
        // Above the bottom it says nothing, which would disclose the depth; and
        // once the boss is down there is no trade left to state.
        var party = Party();
        var tutorial = DungeonState.Enter(GameContent.Current.Dungeons.Tutorial, 2762136374, party);
        tutorial.OpenTurns(party, () => 10);
        Assert.Empty(DungeonHud.BossFloorBanner(tutorial));
        GoDown(tutorial, party);
        Assert.Equal(new[] { "THE STONE GOLEM WAITS ON THIS FLOOR", "BEATING IT CLOSES THE TUTORIAL FOR GOOD" },
            DungeonHud.BossFloorBanner(tutorial));
        tutorial.RecordBossDefeated();
        Assert.Empty(DungeonHud.BossFloorBanner(tutorial));

        // The Spear Hall on a seed it rolls seven floors on: nothing on the way
        // down, then the lock — seven runs, the floor times the tuning's runs a
        // floor, as the visit itself states it — and the re-roll.
        var spearHall = GameContent.Current.Dungeons["spear_hall"];
        var hall = DungeonState.Enter(spearHall, 7, party);
        Assert.Equal(7, hall.BossFloor);
        hall.OpenTurns(party, () => 10);
        while (hall.Current.DownStairs is not null)
        {
            Assert.Empty(DungeonHud.BossFloorBanner(hall));
            GoDown(hall, party);
        }
        Assert.Equal(7, hall.LockRuns);
        Assert.Equal(new[]
        {
            "THE SPEAR WARDEN WAITS ON THIS FLOOR",
            "BEATING IT LOCKS THE SPEAR HALL FOR 7 RUNS",
            "LEAVE WITHOUT IT AND THE DEPTH RE-ROLLS",
        }, DungeonHud.BossFloorBanner(hall));
    }

    [Fact]
    public void WhenTheBossFallsNothingRevives_AndTheFloorLineSaysSo()
    {
        // The boss's fall says who fell and what it means, and from then on the
        // floor line says what the dungeon has become — on every floor, up to the
        // way out — and still never how deep it goes.
        var tutorial = GameContent.Current.Dungeons.Tutorial;
        var golem = DungeonState.BossRow(tutorial, TestWeapons.Get("hatchet"));
        Assert.Equal(("THE STONE GOLEM FALLS", "NOTHING REVIVES - FIGHT YOUR WAY OUT"), DungeonHud.BossFalls(golem));

        var party = Party();
        var visit = DungeonState.Enter(tutorial, 2762136374, party);
        visit.OpenTurns(party, () => 10);
        GoDown(visit, party);
        Assert.Equal("THE TUTORIAL - FLOOR 2", DungeonHud.FloorLine(visit));

        visit.RecordBossDefeated();
        Assert.Equal("THE TUTORIAL - FLOOR 2 - NOTHING REVIVES", DungeonHud.FloorLine(visit));
        Assert.Equal("FLOOR 2", DungeonHud.ArrivalBanner(visit));

        Gather(visit, party, visit.Current.UpStairs, party.Length);
        visit.Ascend(party);
        visit.OpenTurns(party, () => 10);
        string line = DungeonHud.FloorLine(visit);
        Assert.Equal("THE TUTORIAL - FLOOR 1 - NOTHING REVIVES", line);
        Assert.DoesNotContain("/", line);
        Assert.DoesNotContain("OF", line.Split(' '));
    }

    [Fact]
    public void AUniqueDropIsCalledOut_AndTheBossesDropNamesItsTheme()
    {
        // 4.3's presentation: a unique's name lands with a beat - whether the
        // farm's lottery paid out or the boss's graft took its theme's Purity
        // variant to x3 - and the boss's fall says what it left, forged with its
        // dungeon's theme, of whatever class it came as.
        var bulwark = new Drop(TestWeapons.Get("the_bulwark"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.Equal("UNIQUE: THE BULWARK!", DungeonHud.UniqueBeat(bulwark));
        Assert.Equal("UNIQUE: WIDOWMAKER!", DungeonHud.UniqueBeat(new Drop(TestWeapons.Get("widowmaker"), 40, WasUniqueRoll: true)));
        Assert.Null(DungeonHud.UniqueBeat(new Drop(TestWeapons.Get("tower_guard"), 7, WasUniqueRoll: false)));

        var bow = new Drop(GameContent.Current.Weapons.Instantiate("hunting_bow", forgedGraft: Block), DefeatCount: 0, WasUniqueRoll: false);
        Assert.Equal("DROPPED: HUNTING BOW - FORGED WITH BLOCK", DungeonHud.BossDropLine(bow, Block));
        Assert.Equal("DROPPED: THE BULWARK - UNIQUE", DungeonHud.BossDropLine(bulwark, Block));
        var knife = new Drop(GameContent.Current.Weapons.Instantiate("flensing_knife", forgedGraft: CritWindow), 0, false);
        Assert.Equal("DROPPED: FLENSING KNIFE - FORGED WITH CRIT WINDOW", DungeonHud.BossDropLine(knife, CritWindow));
        Assert.Equal("DROPPED: FLENSING KNIFE", DungeonHud.BossDropLine(knife, null));

        // Every name the HUD gives a theme is the modifier's own words, in capitals.
        Assert.Equal(new[] { "BLOCK", "BRACE", "LONGSHOT", "CRIT WINDOW", "CHARGES" },
            new[] { Block, Brace, Longshot, CritWindow, Charges }.Select(DungeonHud.ModifierName));

        // On the floor, the boss's drop is a weapon like any other: its name - a
        // unique's in gold - and no count, since nobody farmed it.
        Assert.Equal(new[] { "Hunting Bow" }, Item(bow));
        Assert.Equal(new[] { "The Bulwark" }, Item(bulwark));
    }

    [Fact]
    public void AGroundItemNamesTheWeaponAndItsQuality()
    {
        // A weapon lying where its carrier fell says which weapon it is and the
        // farm's depth it was collected at - xN, in the shape a dummy's plate
        // wears it, so what the player farmed for and what they are being handed
        // read as one thing (3.2, 3.5). The count includes the kill that
        // extracted it, so it is one higher than the last plate the player read
        // over the living dummy; a dead one draws no plate to disagree with.
        var plain = new Drop(TestWeapons.Get("arming_sword"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.Equal(new[] { "Arming Sword" }, Item(plain));   // nothing farmed it: no quality to print

        var farmed = new Drop(TestWeapons.Get("tower_guard"), DefeatCount: 7, WasUniqueRoll: false);
        Assert.Equal(new[] { "Tower Guard", "x7" }, Item(farmed));

        // A unique is named in the gold every other readout gives it, rather than
        // by a word the label would have to find room for.
        var won = new Drop(TestWeapons.Get("widowmaker"), DefeatCount: 22, WasUniqueRoll: true);
        Assert.Equal(new[] { "Widowmaker", "x22" }, Item(won));
        Assert.NotEqual(DungeonHud.GroundItemRuns(farmed)[0].Color, DungeonHud.GroundItemRuns(won)[0].Color);
    }

    [Fact]
    public void ALooseItemIsLabelledAsItself()
    {
        // A weapon the party put down (4.4) is labelled by what it is, and by
        // nothing a corpse's drop carries. It has no xN, because the farm's depth
        // was the dummy's and not the weapon's. It has no corpse behind it: a
        // loose item is a weapon and a tile. The label is its name alone, a
        // unique's in the same gold as everywhere else.
        static string[] Loose(LooseItem item) => DungeonHud.LooseItemRuns(item).Select(r => r.Text).ToArray();

        var farmed = new Drop(TestWeapons.Get("tower_guard"), DefeatCount: 7, WasUniqueRoll: false);
        Assert.Equal(new[] { "Tower Guard", "x7" }, Item(farmed));                          // on its corpse it wore the farm's depth
        Assert.Equal(new[] { "Tower Guard" }, Loose(new LooseItem(farmed.Weapon, (5, 5))));   // put down, it is the weapon alone

        var won = new Drop(TestWeapons.Get("widowmaker"), DefeatCount: 22, WasUniqueRoll: true);
        var unique = DungeonHud.LooseItemRuns(new LooseItem(won.Weapon, (5, 5)));
        Assert.Equal(new[] { "Widowmaker" }, unique.Select(r => r.Text));
        Assert.Equal(DungeonHud.GroundItemRuns(won)[0].Color, unique[0].Color);
        Assert.NotEqual(DungeonHud.LooseItemRuns(new LooseItem(farmed.Weapon, (5, 5)))[0].Color, unique[0].Color);

        // The record is the weapon and its tile. It has no corpse, no drop and no count to read a label from.
        Assert.Equal(new[] { nameof(LooseItem.Tile), nameof(LooseItem.Weapon) },
            typeof(LooseItem).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));

        // On a real floor: a member puts its bag weapon down, and the label is its name alone.
        var party = Party();
        party[0].Inventory[1] = TestWeapons.Get("tower_guard");
        var visit = DungeonState.Enter(GameContent.Current.Dungeons.Tutorial, 2762136374, party);
        visit.OpenTurns(party, () => 10);
        var left = visit.Current.Leave(party[0], 1);
        Assert.Equal(new[] { "Tower Guard" }, Loose(left));
        Assert.DoesNotContain(Loose(left), text => text.StartsWith('x'));
    }

    [Fact]
    public void StatusLegend_ResolvesEachStatus_AndGivesTheRidersTheirFlavour()
    {
        var enemy = new EnemyState();
        enemy.ApplyStatus(Sundered, null, 2);
        enemy.ApplyStatus(Weakened, null, 1);
        enemy.ApplyStatus(Mire, null, 4);
        enemy.ApplyStatus(Poison, null, 3);
        enemy.ApplyStatus(Searing, DamageType.Flaming, 2);
        Assert.Equal(new[]
        {
            "SUNDERED 2: GETTING CRIT OPENS YOU UP - TAKES +2 A HIT",
            "WEAKENED 1: GETTING CRIT RATTLES YOUR SWING - DEALS -1 A HIT",
            "MIRE 4: MOVEMENT 60 OF 100",   // an enemy's 100 cut 10% a level: the budget left, never the percentage
            "POISON 3: 3 DAMAGE AT TURN END",
            "BURNING 2: 2 DAMAGE AT TURN END",
        }, DungeonHud.StatusLegend(enemy, GameConstants.EnemyMove).Select(r => r.Text));
    }

    [Fact]
    public void MireLegend_ForAMember_MatchesTheCapItsTurnStartSet()
    {
        var member = Holding("tower_guard");
        member.ApplyStatus(Mire, null, 4);
        member.SavedMovement = 40;   // banked last turn: this turn's whole is 200, and Mire cuts all of it
        member.StartTurn();
        Assert.Equal(120f, member.EffectiveMax);   // the MOVE readout's cap
        Assert.Equal("MIRE 4: MOVEMENT 120 OF 200",
            DungeonHud.StatusLegend(member, ShippedContent.Budget + 40).Single().Text);
    }

    [Fact]
    public void StatusName_IsWhatBeatsAndTicksSay()
    {
        Assert.Equal("SUNDERED", DungeonHud.StatusName(Sundered, null));
        Assert.Equal("WEAKENED", DungeonHud.StatusName(Weakened, null));
        Assert.Equal("BURNING", DungeonHud.StatusName(Searing, DamageType.Flaming));
        Assert.Equal("FROSTBITE", DungeonHud.StatusName(Searing, DamageType.Cold));
        Assert.Equal("REGEN", DungeonHud.StatusName(Regeneration, null));
    }

    [Fact]
    public void HelpLine_FollowsTheHeldWeapon()
    {
        Assert.Contains("CLICK ENEMY: ATTACK", DungeonHud.HelpLine(Holding("tower_guard")));
        Assert.Contains("CLICK ENEMY: CAST MIRE", DungeonHud.HelpLine(Holding("staff_of_mire")));
        Assert.Contains("CLICK ALLY: CAST REGENERATION", DungeonHud.HelpLine(Holding("staff_of_renewal")));
        Assert.Contains("CLICK A SPOT IN 5 TILES: BLAST", DungeonHud.HelpLine(Holding("wand_of_the_blast", DamageType.Cold)));
        Assert.Contains("CLICK A DIRECTION: BEAM", DungeonHud.HelpLine(Holding("wand_of_the_beam", DamageType.Shocking)));
        Assert.Contains("N: NOVA AROUND YOU", DungeonHud.HelpLine(Holding("wand_of_the_nova", DamageType.Acidic)));
        Assert.Contains("O: HOLD FIRE", DungeonHud.HelpLine(Holding("crossbow")));
        Assert.DoesNotContain("O: HOLD FIRE", DungeonHud.HelpLine(Holding("longbow")));
    }

    [Fact]
    public void PoolRows_ShowTheBarAndItsXp()
    {
        // The HP row carries the bar and the XP into its next point; the mana
        // row carries the XP alone, because the pool itself is already on the
        // budget line at the top of the screen and no number is printed twice.
        // A fixture's bars are the suite's 100, and at no innate nature a point
        // costs a whole bar.
        var member = Holding("tower_guard");
        member.HpXp += 4;
        member.ManaXp += 12;
        Assert.Equal(new[] { "HP 100/100   XP 4/100", "MANA XP 12/100" }, DungeonHud.PoolRows(member));

        member.Hp -= 8;
        Assert.Equal("HP 92/100   XP 4/100", DungeonHud.PoolRows(member)[0]);
    }

    [Fact]
    public void ProficiencyRows_ShowLevelXpAndTheBonus()
    {
        // A class's row is the level, how far into the next, and what the level
        // pays: +floor(L / 2) damage and -floor(L / 3) movement, each appearing
        // once it is something. Numbers, not drawn bars - at 5 px a bar is a
        // three-pixel smear, and a string is what this file can hold to.
        var member = Holding("weakspot_stiletto");
        Assert.Equal("DAGGER 1   XP 0/100", Assert.Single(DungeonHud.ProficiencyRows(member)));

        member.WeaponXp[WeaponClass.Dagger] = 145;   // 100 leaves level 1, 45 into the 200 that leaves level 2
        Assert.Equal("DAGGER 2   XP 45/200   DMG +1", Assert.Single(DungeonHud.ProficiencyRows(member)));

        member.WeaponXp[WeaponClass.Dagger] = 345;   // level 3, where the movement discount joins it
        Assert.Equal("DAGGER 3   XP 45/300   DMG +1   COST -1", Assert.Single(DungeonHud.ProficiencyRows(member)));
    }

    [Fact]
    public void ProficiencyRows_ListTheClassesTheMemberIsLevellingOrHolding()
    {
        // A class earns a row by being practised or by being carried, in
        // WeaponClass order - the order the XP book and a save both enumerate.
        var member = Holding("staff_of_renewal");
        member.Inventory[1] = TestWeapons.Get("great_axe");
        member.WeaponXp[WeaponClass.Dagger] = 40;

        Assert.Equal(new[] { "DAGGER 1   XP 40/100", "AXE 1   XP 0/100", "STAFF 1   XP 0/100" },
            DungeonHud.ProficiencyRows(member));
        Assert.DoesNotContain(DungeonHud.ProficiencyRows(member), row => row.StartsWith("WAND", StringComparison.Ordinal));
    }

    [Fact]
    public void WeaponStats_PrintsTheItemsCost_TheProficiencyRowPrintsTheDiscount()
    {
        // Two numbers, and the panel says which is which. The statline is the
        // item as it reads in anyone's hands - the same string for a weapon in
        // the bag as for one equipped - so it keeps the weapon's own resolved
        // cost; the wielder's discount reads on the proficiency row, and what
        // the gate and the charge actually take is the two together.
        var member = Holding("flensing_knife");
        member.WeaponXp[WeaponClass.Dagger] = 345;   // level 3: one off the cost
        var knife = member.EquippedWeapon!;

        Assert.Equal("DMG 15  RNG 40  COST 27", DungeonHud.WeaponStats(knife));
        Assert.Contains("COST -1", Assert.Single(DungeonHud.ProficiencyRows(member)));
        Assert.Equal(26, member.MovementCost(knife));
    }

    [Fact]
    public void LevelUpLabel_NamesWhatTheCreditBought()
    {
        // The beat over whoever earned it: the class and the level now wielded
        // at, or the pool and the ceiling now carried. Every number is read back
        // off the member, so the callout states progression and decides none of
        // it - no XP rule lives in the HUD or the scene.
        var member = Holding("weakspot_stiletto");
        member.WeaponXp[WeaponClass.Dagger] = 100;

        Assert.Equal("DAGGER 2!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 100)));
        Assert.Equal("MAX HP 100!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Health, null, 100)));
        Assert.Equal("MAX MANA 100!", DungeonHud.LevelUpLabel(member, new XpCredit(XpPool.Mana, null, 100)));
    }

    [Fact]
    public void InventoryLayout_KeepsTheSwapLineAndTheKeyHintOnScreen()
    {
        // The panel grows downward by a row per class a member is levelling or
        // holding, so the lines a short window loses are the ones under the
        // rows - what a swap costs, and which key equips. It opens where it
        // always has while the whole of it fits, slides up when it does not,
        // and only past that drops rows off the end of the list.
        const int slots = 3;
        const int most = 2 + 8;   // the two pools and every weapon class at once

        var roomy = DungeonHud.LayoutInventory(720f, slots, most);
        Assert.Equal(720f / 2f - 150f, roomy.Top);   // where the panel has always opened
        Assert.Equal(most, roomy.Rows);

        var tight = DungeonHud.LayoutInventory(520f, slots, most);
        Assert.True(tight.Top < 520f / 2f - 150f);   // slid up to make the room
        Assert.Equal(most, tight.Rows);              // and every row still printed

        var cramped = DungeonHud.LayoutInventory(420f, slots, most);
        Assert.True(cramped.Rows < most);            // the tail of the list goes before the key hint does

        // The invariant, over every window the settings allow (320 x 240 up) and
        // every row count a member can reach: what is drawn clears the help line
        // at h - 24, or the panel has already dropped every row it could.
        for (int h = 240; h <= 1600; h += 2)
            for (int rows = 0; rows <= most; rows++)
            {
                var layout = DungeonHud.LayoutInventory(h, slots, rows);
                Assert.InRange(layout.Rows, 0, rows);
                Assert.True(layout.Top >= 56f, $"h={h} rows={rows}: over the top readouts");
                Assert.True(layout.Bottom <= h - 24f || layout.Rows == 0, $"h={h} rows={rows}: past the help line");
            }
    }

    [Fact]
    public void InventoryLayout_SixSlotsFitA720Window()
    {
        // 4.4's six slots a member (PHASE4-141) in the shipped 1280 x 720 window:
        // the panel slides up to make room for the three extra slots, and still
        // prints every progression row the layout keeps, with the swap line and
        // the key hint clear of the help line at h - 24.
        int slots = PartyMemberState.InventorySlots;
        Assert.Equal(6, slots);
        Assert.Equal(slots, TestPools.Char("A").Inventory.Length);
        const int most = 2 + 8;   // the two pools and every weapon class at once

        for (int rows = 0; rows <= most; rows++)
        {
            var layout = DungeonHud.LayoutInventory(720f, slots, rows);
            Assert.Equal(rows, layout.Rows);                          // every row printed
            Assert.True(layout.Top >= 56f, $"rows={rows}: over the top readouts");
            Assert.True(layout.Bottom <= 720f - 24f, $"rows={rows}: past the help line");
        }

        var full = DungeonHud.LayoutInventory(720f, slots, most);
        Assert.True(full.Top < 720f / 2f - 150f);   // slid up from where the three-slot panel opened

        // And past 720 the invariant the three-slot panel keeps holds for six too:
        // what is drawn clears the help line, or every row it could drop has gone.
        for (int h = 240; h <= 1600; h += 2)
            for (int rows = 0; rows <= most; rows++)
            {
                var layout = DungeonHud.LayoutInventory(h, slots, rows);
                Assert.InRange(layout.Rows, 0, rows);
                Assert.True(layout.Top >= 56f, $"h={h} rows={rows}: over the top readouts");
                Assert.True(layout.Bottom <= h - 24f || layout.Rows == 0, $"h={h} rows={rows}: past the help line");
            }
    }

    [Fact]
    public void TheEquipHintNamesEveryBagSlot()
    {
        // The panel numbers slots from 1 -- the hand is 1 -- and a number key equips
        // the bag slot printed with it, so six slots are equipped with 2-6 (PHASE4-143).
        Assert.Equal("PRESS 2-6 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(PartyMemberState.InventorySlots));
        Assert.Equal("PRESS 2-6 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(TestPools.Char("A").Inventory.Length));

        // It is read off the slots, so a carry tuned differently reads differently:
        // Phase 3's three, a bag of one, and no bag at all.
        Assert.Equal("PRESS 2-3 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(3));
        Assert.Equal("PRESS 2 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(2));
        Assert.Equal("I TO CLOSE", DungeonHud.EquipHint(1));

        // It never names a key the scene does not bind: past the last number key,
        // it names the keys there are.
        Assert.Equal(9, DungeonScene.HighestSlotKey);
        Assert.Equal("PRESS 2-8 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(8));
        Assert.Equal("PRESS 2-9 TO EQUIP - I TO CLOSE", DungeonHud.EquipHint(12));
    }

    [Fact]
    public void TheLeaveHintNamesItsKeys()
    {
        // In the bag, X arms "leave" (4.4), and while it is armed the key hint
        // asks which weapon to put down: by the bag's own numbers, the same keys
        // that equip, read off the same slots, with X again to back out. Never 1:
        // the weapon in hand is not put down this way.
        int slots = PartyMemberState.InventorySlots;
        Assert.Equal("LEAVE WHICH? 2-6 - X CANCELS", DungeonHud.LeaveHint(slots));
        Assert.Equal("LEAVE WHICH? 2-6 - X CANCELS", DungeonHud.LeaveHint(TestPools.Char("A").Inventory.Length));

        var words = DungeonHud.LeaveHint(slots).Split([' ', '?'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("X", words);
        Assert.Contains("CANCELS", words);
        Assert.DoesNotContain("1", DungeonHud.LeaveHint(slots));

        // The same keys the equip hint names, whatever depth the carry is tuned to:
        // Phase 3's three, a bag of one, the deepest the number row reaches, and
        // no key past it. A hand and no bag has nothing to put down.
        foreach (int n in new[] { 2, 3, 6, 8, 12 })
        {
            string keys = DungeonHud.EquipHint(n).Split(' ')[1];   // PRESS 2-6 TO EQUIP: the keys
            Assert.Equal($"LEAVE WHICH? {keys} - X CANCELS", DungeonHud.LeaveHint(n));
        }
        Assert.Equal("LEAVE WHICH? 2 - X CANCELS", DungeonHud.LeaveHint(2));
        Assert.Equal("LEAVE WHICH? 2-9 - X CANCELS", DungeonHud.LeaveHint(12));
        Assert.Equal("NOTHING TO LEAVE - X CANCELS", DungeonHud.LeaveHint(1));

        // Unarmed, the panel names the key, so X can be found at all. An armed
        // number on an empty slot says so by the number pressed, and a weapon put
        // down says what was left, answered by TOOK when it is taken back.
        Assert.Equal("X TO LEAVE ONE HERE", DungeonHud.LeaveKeyHint);
        Assert.Equal("SLOT 3 IS EMPTY", DungeonHud.EmptySlotCue(2));
        Assert.Equal("SLOT 6 IS EMPTY", DungeonHud.EmptySlotCue(5));
        Assert.Equal("LEFT HATCHET HERE", DungeonHud.LeftBeat(TestWeapons.Get("hatchet")));

        // And a full bag's refusal is still a carry limit, not a fault, and now
        // says how to make room: open the bag, and X.
        string full = DungeonHud.BagFullCue(slots);
        Assert.Equal("BAG FULL - 6 CARRIED - I THEN X LEAVES ONE", full);
        Assert.Contains("X", full.Split(' '));
        Assert.Contains("I", full.Split(' '));
    }

    [Fact]
    public void TheFloorLineNamesTheFloorAndNeverTheDepth()
    {
        // PHASE4-024: the boss floor is rolled at entry and not disclosed - the
        // player learns the depth only by reaching the bottom, which is what keeps
        // the depth-scaled payout from being scummed. So the floor line and the
        // arrival banner name the dungeon and the floor number, and nothing that
        // would tell a deep roll from a shallow one: no "of N", no "3/7".
        var spearHall = GameContent.Current.Dungeons["spear_hall"];
        const long SevenDeep = 7;   // a seed the Spear Hall rolls seven floors on
        var party = Party();
        var visit = DungeonState.Enter(spearHall, SevenDeep, party);
        Assert.Equal(7, visit.BossFloor);
        visit.OpenTurns(party, () => 10);

        Assert.Equal("THE SPEAR HALL - FLOOR 1", DungeonHud.FloorLine(visit));
        Assert.Equal("FLOOR 1", DungeonHud.ArrivalBanner(visit));

        GoDown(visit, party);
        GoDown(visit, party);
        Assert.Equal(2, visit.FloorIndex);
        Assert.Equal("THE SPEAR HALL - FLOOR 3", DungeonHud.FloorLine(visit));
        Assert.Equal("FLOOR 3", DungeonHud.ArrivalBanner(visit));

        // Every floor above the bottom, the third among them: the number of the
        // floor stood on, and never the seven, a slash, or an "of".
        while (visit.Current.DownStairs is not null)
        {
            foreach (string shown in new[] { DungeonHud.FloorLine(visit), DungeonHud.ArrivalBanner(visit) })
            {
                Assert.Contains($"FLOOR {visit.FloorIndex + 1}", shown);
                Assert.DoesNotContain("7", shown);
                Assert.DoesNotContain("/", shown);
                Assert.DoesNotContain("OF", shown.Split(' '));
            }
            GoDown(visit, party);
        }

        // Reached, the bottom is simply the floor the party stands on: it is its
        // number now because the party is there, not because the line knew.
        Assert.Equal(visit.BossFloor, visit.FloorIndex + 1);
        Assert.Equal("THE SPEAR HALL - FLOOR 7", DungeonHud.FloorLine(visit));

        // And the line is the same on the same floor of any depth: a five-floor
        // visit's first floor reads exactly as a ten-floor one's.
        var shallow = DungeonState.Enter(spearHall, 4, party);
        var deep = DungeonState.Enter(spearHall, 6, party);
        Assert.Equal((5, 10), (shallow.BossFloor, deep.BossFloor));
        Assert.Equal(DungeonHud.FloorLine(shallow), DungeonHud.FloorLine(deep));
        Assert.Equal(DungeonHud.ArrivalBanner(shallow), DungeonHud.ArrivalBanner(deep));
    }

    [Fact]
    public void EveryStairSaysWhatItDoes_OrWhyNot()
    {
        // Each stair is labelled by what it does (4.2) - the first floor's
        // up-stairs are the way out, every other floor's the way up, and the
        // bottom has no way down - and standing on one, the cue says what E does
        // there, or why the party cannot take it yet.
        Assert.Equal("STAIRS DOWN", DungeonHud.StairLabel(Stair.Down));
        Assert.Equal("STAIRS UP", DungeonHud.StairLabel(Stair.Up));
        Assert.Equal("WAY OUT", DungeonHud.StairLabel(Stair.Exit));

        var tile = (R: 5, C: 5);
        Assert.Equal("E: DESCEND", DungeonHud.StairCue(new StairUse(Stair.Down, tile), null, 4, 4));
        Assert.Equal("E: CLIMB", DungeonHud.StairCue(new StairUse(Stair.Up, tile), null, 4, 4));
        Assert.Equal("E: LEAVE THE DUNGEON", DungeonHud.StairCue(new StairUse(Stair.Exit, tile), null, 4, 4));
        Assert.Equal("GATHER THE PARTY - 2 OF 4 HERE",
            DungeonHud.StairCue(new StairUse(Stair.Down, tile), StairRefusal.PartyNotGathered, 2, 4));

        // Outside the party's turn the cue is not drawn and E says nothing, so
        // there is nothing for it to say.
        Assert.Null(DungeonHud.StairCue(new StairUse(Stair.Down, tile), StairRefusal.NotThePlayersTurn, 4, 4));

        // On a real visit the kinds are the visit's, and so are the numbers. The
        // tutorial's first floor holds the way out and a way down...
        var party = Party();
        var visit = DungeonState.Enter(GameContent.Current.Dungeons.Tutorial, 2762136374, party);
        visit.OpenTurns(party, () => 10);
        var first = visit.Current;
        Assert.Equal(new[] { "WAY OUT", "STAIRS DOWN" },
            DungeonState.StairsOf(first).Select(s => DungeonHud.StairLabel(s.Kind)));

        // ...and two of the four gathered at the down-stairs are two short, the
        // count the cue prints being the visit's own: the living within reach, of
        // the living. The fallen are carried, so they are counted in neither.
        var down = DungeonState.StairsOf(first).Single(s => s.Kind == Stair.Down);
        Gather(visit, party, down.Tile, 2);
        var refusal = visit.CanUse(down, party);
        Assert.Equal(StairRefusal.PartyNotGathered, refusal);
        Assert.Equal("GATHER THE PARTY - 2 OF 4 HERE",
            DungeonHud.StairCue(down, refusal, visit.Gathered(party, down.Tile), party.Count(m => m.Alive)));
        party[3].Alive = false;
        Assert.Equal("GATHER THE PARTY - 2 OF 3 HERE",
            DungeonHud.StairCue(down, visit.CanUse(down, party), visit.Gathered(party, down.Tile), party.Count(m => m.Alive)));
        party[3].Alive = true;

        // All four there, the cue is the verb; and below, on the tutorial's
        // bottom floor, the only stair leads back up.
        Gather(visit, party, down.Tile, party.Length);
        Assert.Equal("E: DESCEND",
            DungeonHud.StairCue(down, visit.CanUse(down, party), visit.Gathered(party, down.Tile), party.Length));
        visit.Descend(party);
        visit.OpenTurns(party, () => 10);
        Assert.Null(visit.Current.DownStairs);
        Assert.Equal(new[] { "STAIRS UP" }, DungeonState.StairsOf(visit.Current).Select(s => DungeonHud.StairLabel(s.Kind)));
    }

    [Fact]
    public void HelpLine_NamesTheStairsKey()
    {
        // E takes the stairs whatever the member is holding, so the keys every
        // turn shares name it - for a swing, a cast, a wand's aim, a held shot,
        // and an empty hand alike.
        Assert.Contains("E STAIRS", DungeonHud.HelpLine(Holding("tower_guard")));
        Assert.Contains("E STAIRS", DungeonHud.HelpLine(Holding("staff_of_renewal")));
        Assert.Contains("E STAIRS", DungeonHud.HelpLine(Holding("wand_of_the_nova", DamageType.Acidic)));
        Assert.Contains("E STAIRS", DungeonHud.HelpLine(Holding("crossbow")));
        Assert.Contains("E STAIRS", DungeonHud.HelpLine(TestPools.Holding("A", null)));
    }

    [Fact]
    public void TheLeavePromptNamesItsKeys()
    {
        // Leaving discards the whole visit (4.1), so the way out asks first, and
        // the question names the keys that answer it: E or ENTER for yes, ESC for
        // no. Never a number: 2 on the pause menu is the exit from the game.
        string prompt = DungeonHud.LeavePrompt();
        Assert.Equal("LEAVE THE DUNGEON? E OR ENTER: YES - ESC: NO", prompt);

        var words = prompt.Split([' ', ':', '-', '?'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("E", words);
        Assert.Contains("ENTER", words);
        Assert.Contains("ESC", words);
        Assert.DoesNotContain("2", prompt);
        Assert.DoesNotContain(prompt, char.IsDigit);
    }

    [Fact]
    public void TheLeavePromptSaysWhatLeavingCosts()
    {
        // The way out's question previews the answer (4.4), since the answer ends
        // the run: with the boss down, a clear - what you carry goes home, and the
        // dungeon closes for good or locks for its floor in runs; with it standing,
        // a retreat - the XP kept, the depth re-rolled where it can be, and whether
        // the party got deep enough for the run to count (6.3's mercy) - a rule,
        // never the depth. And either way the whole visit goes. The tutorial is two
        // floors deep on every visit (PHASE4-077), so its retreat re-rolls nothing
        // and says nothing of a new depth.
        const string reset = "EVERY FLOOR RESETS - THE NEXT VISIT IS A NEW DUNGEON";
        var party = Party();
        var tutorial = DungeonState.Enter(GameContent.Current.Dungeons.Tutorial, 2762136374, party);
        tutorial.OpenTurns(party, () => 10);
        Assert.Equal(new[] { "NO BOSS - A RETREAT: XP KEPT", "TOO SHALLOW TO COUNT AS A RUN", reset },
            DungeonHud.LeavePreview(tutorial));
        GoDown(tutorial, party);
        Assert.Equal("TOO SHALLOW TO COUNT AS A RUN", DungeonHud.LeavePreview(tutorial)[1]);   // its two floors never reach the bar
        tutorial.RecordBossDefeated();
        Assert.Equal(new[] { "THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY", "THE TUTORIAL CLOSES FOR GOOD", reset },
            DungeonHud.LeavePreview(tutorial));

        // The Spear Hall, seven floors deep: three floors down is deep enough to
        // count, and nothing says how deep it goes until its boss is down, when the
        // lock it writes is the floor's.
        var hall = DungeonState.Enter(GameContent.Current.Dungeons["spear_hall"], 7, party);
        Assert.Equal(7, hall.BossFloor);
        hall.OpenTurns(party, () => 10);
        GoDown(hall, party);
        GoDown(hall, party);
        var retreat = DungeonHud.LeavePreview(hall);
        Assert.Equal(new[] { "NO BOSS - A RETREAT: XP KEPT, THE DEPTH RE-ROLLS", "DEEP ENOUGH TO COUNT AS A RUN", reset }, retreat);
        Assert.DoesNotContain(retreat, line => line.Contains('7'));
        hall.RecordBossDefeated();
        Assert.Equal(new[] { "THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY", "THE SPEAR HALL LOCKS FOR 7 RUNS", reset },
            DungeonHud.LeavePreview(hall));
    }

    [Fact]
    public void TheRunSummaryNamesTheOutcome()
    {
        // 4.4's three outcomes, as the run's end shows them: what the run was, where
        // and how deep, the boss - its floor only where the party reached it - the
        // spoils kept or lost by name, the XP kept or clawed back member by member,
        // whether the run counted, the lock or the closing, and ENTER TO CONTINUE:
        // the one key the summary answers. Never ESC.
        static string[] Lines(RunResult result, string? next = null) => DungeonHud.RunSummaryLines(result, next).Select(r => r.Text).ToArray();
        var bulwark = TestWeapons.Get("the_bulwark");
        var tower = TestWeapons.Get("tower_guard");

        var cleared = new RunResult("tutorial", RunOutcome.BossKill, BossDefeated: true, DeepestFloor: 2, BossFloorRevealed: 2,
            ServiceTick: true, MercyTick: false, RunCounter: 1, LockedUntilRun: null, Retired: true,
            SpoilsKept: [bulwark, tower], SpoilsLost: [], Clawbacks: []);
        Assert.Equal(new[]
        {
            "CLEARED",
            "THE TUTORIAL - DEEPEST FLOOR 2",
            "THE STONE GOLEM FELL ON FLOOR 2",
            "SPOILS KEPT: THE BULWARK, TOWER GUARD",
            "XP KEPT",
            "RUN COUNTED - 1 RUN SO FAR",
            "THE TUTORIAL IS CLOSED FOR GOOD",
            "ENTER TO CONTINUE - NEXT: THE SPEAR HALL",
        }, Lines(cleared, "The Spear Hall"));

        // A fighting retreat from floor 3 of a dungeon seven deep: counted as a
        // mercy, nothing to keep, and the depth never named.
        var retreat = new RunResult("spear_hall", RunOutcome.Retreat, false, 3, null, true, true, 4, null, false, [], [], []);
        Assert.Equal(new[]
        {
            "RETREATED",
            "THE SPEAR HALL - DEEPEST FLOOR 3",
            "THE SPEAR WARDEN STILL STANDS - THE DEPTH RE-ROLLS NEXT TIME",
            "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS",
            "XP KEPT",
            "RUN COUNTED - A MERCY FOR FAILING DEEP - 4 RUNS SO FAR",
            "ENTER TO CONTINUE",
        }, Lines(retreat));
        Assert.DoesNotContain(Lines(retreat), line => line.Contains('7'));

        // Killed the boss and died on the way out: the clear stands, the spoils
        // and a share of the XP do not.
        var died = new RunResult("spear_hall", RunOutcome.Death, true, 5, 5, true, false, 3, 8, false, [], [tower],
            [new XpClawback("A", "Dagger", 345, 323), new XpClawback("A", XpSnapshot.HpPool, 90, 84), new XpClawback("C", "Axe", 30, 15)]);
        Assert.Equal(new[]
        {
            DungeonHud.DefeatHeadline,
            "THE SPEAR HALL - DEEPEST FLOOR 5",
            "THE SPEAR WARDEN FELL ON FLOOR 5",
            "SPOILS LOST: TOWER GUARD",
            "XP CLAWED BACK:",
            "A: DAGGER -22, HP -6",
            "C: AXE -15",
            "RUN COUNTED - 3 RUNS SO FAR",
            "THE SPEAR HALL IS LOCKED FOR 5 RUNS",
            "ENTER TO CONTINUE",
        }, Lines(died));
        Assert.Equal("DEFEATED", DungeonHud.DefeatHeadline);

        // Dead in the tutorial, the golem standing: nothing counted, and nothing
        // clawed back, the run having gained nothing (or the share being 0). Its two
        // floors never reach the mercy floor and never change, so the summary points
        // at no floor 3 it does not have and promises no new depth.
        var shallow = new RunResult("tutorial", RunOutcome.Death, false, 2, null, false, false, 0, null, false, [], [], []);
        Assert.Equal(new[]
        {
            "DEFEATED",
            "THE TUTORIAL - DEEPEST FLOOR 2",
            "THE STONE GOLEM STILL STANDS",
            "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS",
            "XP KEPT - NOTHING CLAWED BACK",
            "RUN NOT COUNTED - 0 RUNS SO FAR",
            "ENTER TO CONTINUE",
        }, Lines(shallow));

        // Walked out past the golem it found: its floor named, and still no re-roll.
        var waited = new RunResult("tutorial", RunOutcome.Retreat, false, 2, 2, false, false, 0, null, false, [], [], []);
        Assert.Equal(new[]
        {
            "RETREATED",
            "THE TUTORIAL - DEEPEST FLOOR 2",
            "THE STONE GOLEM WAITED ON FLOOR 2",
            "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS",
            "XP KEPT",
            "RUN NOT COUNTED - 0 RUNS SO FAR",
            "ENTER TO CONTINUE",
        }, Lines(waited));

        // A dungeon that rolls its depth says both: the re-roll, and the rule a
        // shallow failure missed - or, from its boss floor, the floor it found.
        var turnedBack = new RunResult("spear_hall", RunOutcome.Retreat, false, 2, null, false, false, 4, null, false, [], [], []);
        Assert.Equal(new[]
        {
            "RETREATED",
            "THE SPEAR HALL - DEEPEST FLOOR 2",
            "THE SPEAR WARDEN STILL STANDS - THE DEPTH RE-ROLLS NEXT TIME",
            "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS",
            "XP KEPT",
            "RUN NOT COUNTED - A FAILED RUN COUNTS FROM FLOOR 3 - 4 RUNS SO FAR",
            "ENTER TO CONTINUE",
        }, Lines(turnedBack));
        var faced = new RunResult("spear_hall", RunOutcome.Retreat, false, 7, 7, true, true, 5, null, false, [], [], []);
        Assert.Equal("THE SPEAR WARDEN WAITED ON FLOOR 7 - THE DEPTH RE-ROLLS NEXT TIME", Lines(faced)[2]);

        // A long haul wraps rather than running off the panel, every name once.
        var haul = Enumerable.Range(0, 12).Select(_ => TestWeapons.Get("weakspot_stiletto")).Append(bulwark).ToArray();
        var hauled = Lines(cleared with { SpoilsKept = haul });
        var spoils = hauled.SkipWhile(l => !l.StartsWith("SPOILS KEPT:", StringComparison.Ordinal)).TakeWhile(l => l != "XP KEPT").ToArray();
        Assert.True(spoils.Length > 1);
        Assert.All(hauled, line => Assert.True(line.Length <= 70, $"'{line}' runs off the panel"));
        Assert.Equal(12, spoils.Sum(l => l.Split("WEAKSPOT STILETTO").Length - 1));
        Assert.Single(spoils, l => l.Contains("THE BULWARK"));

        // ENTER is the one way on, on every ending: never ESC, never a quit.
        foreach (var result in new[] { cleared, retreat, died, shallow, waited, turnedBack, faced })
        {
            var lines = Lines(result);
            Assert.StartsWith(DungeonHud.ContinueHint, lines[^1], StringComparison.Ordinal);
            var words = lines.SelectMany(l => l.Split([' ', ':', '-', ','], StringSplitOptions.RemoveEmptyEntries));
            Assert.DoesNotContain("ESC", words);
            Assert.DoesNotContain("QUIT", words);
        }
        Assert.Equal("ENTER TO CONTINUE", DungeonHud.ContinueHint);
    }

    [Fact]
    public void AnOverrideLaunchShowsTheStartupNotices()
    {
        // A dev override boots straight into a dungeon, past the screen between
        // runs that says the startup notices otherwise (1h, 1i): the scene hands
        // the host's notices to its HUD over the first floor's banner, and the rows
        // it draws are the one wrapper's rows of them. The HUD lays them out with
        // no window — the drawing is untested, as ever — so it is built without one.
        var load = ShippedData.Load(ShippedData.Texts with { Tuning = """{ "MovementUnitsPerMana": 0, "PartyCarrySlots": 4 }""" });   // beside the shipped content files: the two notices are the tuning's
        Assert.Equal(2, load.Notices.Count);

        // The scene the host builds with them has them up for its first floor.
        // One built with none has none. Nothing a scene does before Initialize
        // touches a window or a camera, so these are built with neither, over a
        // real campaign with a real visit entered.
        var campaign = CampaignState.Start(GameContent.Current);
        campaign.Enter(GameContent.Current.Dungeons.Tutorial.Id, mapSeed: 7);
        var booted = new DungeonScene(camera: null!, game: null!, campaign, new NoRunHost(), load.Notices);
        Assert.Equal(NoticeText.Rows(load.Notices), booted.NoticeRows);
        Assert.Equal("TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1", booted.NoticeRows[0]);
        Assert.Empty(new DungeonScene(camera: null!, game: null!, campaign, new NoRunHost()).NoticeRows);
        Assert.Empty(new DungeonScene(camera: null!, game: null!, campaign, new NoRunHost(), []).NoticeRows);

        var hud = new DungeonHud(game: null!);
        Assert.Empty(hud.NoticeRows);
        hud.ShowNotices(load.Notices);
        Assert.Equal(NoticeText.Rows(load.Notices), hud.NoticeRows);
        Assert.Equal("TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1", hud.NoticeRows[0]);
        Assert.Contains(hud.NoticeRows, row => row.Contains("USING 8", StringComparison.Ordinal));

        // Up long enough to read, then gone: the log keeps every one of them.
        hud.Update(5f);
        Assert.Equal(NoticeText.Rows(load.Notices), hud.NoticeRows);
        hud.Update(10f);
        Assert.Empty(hud.NoticeRows);

        // Nothing to say, nothing shown.
        hud.ShowNotices([]);
        Assert.Empty(hud.NoticeRows);
    }

    [Fact]
    public void ThePauseMenuSavesAndQuits_AndSaysItAutosaves()
    {
        // 1f, 1i (Phase 5.8; Phase 5.5's ThePauseMenuSaysARunInProgressIsNotSavedYet
        // until the run in progress was saved): the pause menu's quit saves the run
        // and quits, and a relaunch resumes it where it stood. Under the options the
        // menu says there is nothing to save by hand: the run saves itself at every
        // turn's start and every arrival.
        Assert.Equal("2  SAVE AND QUIT", DungeonHud.PauseQuitLine);
        Assert.Equal(["1  CLOSE", DungeonHud.PauseQuitLine], DungeonHud.PauseMenuOptions());
        Assert.Equal("PROGRESS SAVES AT EVERY TURN", DungeonHud.PauseSavesLine);

        // Each option leads with the number key that takes it — 1 closes the menu,
        // 2 saves and quits — and nothing on it says a quit loses the run any more,
        // or calls it an exit that saves nothing.
        var options = DungeonHud.PauseMenuOptions();
        for (int i = 0; i < options.Count; i++)
            Assert.StartsWith($"{i + 1}  ", options[i], StringComparison.Ordinal);
        Assert.DoesNotContain(options, option => option.Contains("EXIT GAME", StringComparison.Ordinal));
        Assert.DoesNotContain(options.Append(DungeonHud.PauseSavesLine), line => line.Contains("NOT SAVED", StringComparison.Ordinal));
        Assert.Contains("SAVE", DungeonHud.PauseQuitLine, StringComparison.Ordinal);

        // Every line of the menu fits the font: capitals, digits, spaces, dashes.
        Assert.All(options.Append(DungeonHud.PauseSavesLine), line => Assert.Matches("^[A-Z0-9 -]+$", line));
    }

    [Fact]
    public void AResumedVisitSaysSo_WithItsNotices()
    {
        // 1f, 1i: a launch that resumes a run in progress opens on the floor the
        // save stood on, with RESUMED over the floor banner — where the screen
        // between runs says CAMPAIGN RESUMED — and the launch's notices over it:
        // the save's word whole, as the screen between runs shows it (S8's
        // review), then what the data files raised, capped as every screen caps
        // it (1h). The plain CAMPAIGN RESUMED is the banner's own word there, so it
        // is not said twice; resumed from the backup, the notice says so. Built
        // with no window, as a scene is read.
        var campaign = CampaignState.Start(GameContent.Current);
        var visit = campaign.Enter("spear_hall", mapSeed: 4);
        Assert.Equal(5, visit.BossFloor);   // seed 4 rolls the Spear Hall five deep
        visit.OpenTurns(campaign.Party, () => 10);
        GoDown(visit, campaign.Party);
        var loaded = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign, new ViewDto("B", MarchingThisTurn: true)))),
            GameContent.Current);
        Assert.Equal((1, (TurnSystem?)null), (loaded.Campaign.Visit!.FloorIndex, loaded.Campaign.Visit.Turns));

        // The save's word is the first two, as the game counts it (StartupNotices.Leading).
        string[] notices = [BetweenRunsHud.Resumed, CampaignBoot.TuningChanged, "tuning.json: MovementUnitsPerMana 0 is below 1 - using 1"];
        var scene = new DungeonScene(camera: null!, game: null!, loaded.Campaign, new NoRunHost(), new ResumeView(loaded.View, 2, notices));
        Assert.Equal([DungeonHud.ResumedLine, "THE SPEAR HALL", "FLOOR 2"], scene.BannerLines);
        Assert.Equal("RESUMED", DungeonHud.ResumedLine);
        Assert.Equal(NoticeText.Rows(notices.Skip(1).ToArray(), whole: 1), scene.NoticeRows);
        Assert.Equal(CampaignBoot.TuningChanged, string.Join(" ", scene.NoticeRows.Take(2).Select(row => row.Trim())));
        Assert.Equal("TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1", scene.NoticeRows[^1]);
        Assert.DoesNotContain(scene.NoticeRows, row => row.Contains(BetweenRunsHud.Resumed, StringComparison.Ordinal));

        // From the backup it says so, the longer line kept and shown whole with the
        // numbers; the data files' notices after them are capped at three rows and
        // the rest counted, as over any floor.
        var hud = new DungeonHud(game: null!);
        string[] many = [CampaignBoot.ResumedFromBackup, CampaignBoot.TuningChanged, "one", "two", "three", "four"];
        hud.ShowResume(loaded.Campaign.Visit, many, leading: 2);
        Assert.Equal(NoticeText.Rows(many, whole: 2), hud.NoticeRows);
        Assert.Equal(CampaignBoot.ResumedFromBackup, hud.NoticeRows[0]);
        Assert.Equal(CampaignBoot.TuningChanged, string.Join(" ", hud.NoticeRows.Skip(1).Take(2).Select(row => row.Trim())));
        Assert.Equal([.. NoticeText.Rows([CampaignBoot.ResumedFromBackup, CampaignBoot.TuningChanged]), "ONE", "TWO", "THREE", NoticeText.More(1)],
            hud.NoticeRows);

        // On the boss floor the trade stays under it; a floor entered fresh has no
        // RESUMED over its banner. The banner is up long enough to read, then gone.
        var bottom = CampaignState.Start(GameContent.Current);
        var deep = bottom.Enter("spear_hall", mapSeed: 4);
        deep.OpenTurns(bottom.Party, () => 10);
        while (deep.Current.DownStairs is not null)
            GoDown(deep, bottom.Party);
        hud.ShowResume(deep, [], leading: 0);
        Assert.Equal([DungeonHud.ResumedLine, "THE SPEAR HALL", "FLOOR 5", .. DungeonHud.BossFloorBanner(deep)], hud.BannerLines);
        Assert.Empty(hud.NoticeRows);
        hud.ShowArrival(deep);
        Assert.Equal(["THE SPEAR HALL", "FLOOR 5", .. DungeonHud.BossFloorBanner(deep)], hud.BannerLines);
        hud.ShowResume(deep, [], leading: 0);
        hud.Update(4.9f);
        Assert.NotEmpty(hud.BannerLines);
        hud.Update(0.2f);
        Assert.Empty(hud.BannerLines);
    }

    [Fact]
    public void AResumedFloorShowsTheSavesWordWhole_AndItsBannerMakesRoom()
    {
        // S8's review (1f over 1h). A launch may set campaign.json aside, because
        // it could not be read or crashed its own resume, and resume the backup's
        // run in progress. It then lands in the dungeon, and the file set aside is
        // the one thing the player has to act on. So the resumed floor shows the
        // save's word whole, as the screen between runs does, with the full path
        // and how to try it again. Only what the data files raised after it is
        // capped. Built with no window, as a scene is read.
        var campaign = CampaignState.Start(GameContent.Current);
        var visit = campaign.Enter("spear_hall", mapSeed: 4);
        visit.OpenTurns(campaign.Party, () => 10);
        GoDown(visit, campaign.Party);
        var loaded = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign, new ViewDto("B")))), GameContent.Current);
        var resumed = loaded.Campaign.Visit!;

        const string path = @"C:\Users\a-player\AppData\Local\DistanceRPG\saves\campaign.crashed-1.json";
        string[] saysWhole =
        [
            CampaignBoot.ResumedFromBackup,
            CampaignBoot.TuningChanged,
            CampaignBoot.SetAsideNotice(SaveStore.FileName, "crashed the game the last time it was resumed", path),
        ];
        string[] data = [.. Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1")];
        var scene = new DungeonScene(camera: null!, game: null!, loaded.Campaign, new NoRunHost(),
            new ResumeView(loaded.View, saysWhole.Length, [.. saysWhole, .. data]));
        var whole = saysWhole.SelectMany(NoticeText.Wrap).ToList();   // every word of every notice, the path and TRY AGAIN among them
        Assert.InRange(whole.Count, NoticeText.MaxRows + 2, NoticeText.MaxWholeRows);   // more than the cap of three alone would show
        Assert.Equal([.. whole, .. NoticeText.Rows(data).Take(NoticeText.MaxRows), NoticeText.More(2)], scene.NoticeRows);
        Assert.Contains("C:/USERS/A-PLAYER/APPDATA/LOCAL/DISTANCERPG/SAVES/CAMPAIGN.CRASHED-1.JSON",
            string.Concat(scene.NoticeRows.Select(row => row.Trim())), StringComparison.Ordinal);   // cut across two rows, but all there
        Assert.Contains(CampaignBoot.TryAgain, string.Join(" ", scene.NoticeRows.Select(row => row.Trim())), StringComparison.Ordinal);

        // S8 first capped every notice here. That showed the resumed line and the
        // numbers, and of the file set aside only a count of the notices in the log.
        Assert.Equal([.. whole.Take(NoticeText.MaxRows), NoticeText.More(6)], NoticeText.Rows([.. saysWhole, .. data]));

        // The plain CAMPAIGN RESUMED is RESUMED over the banner, and is not counted
        // among what is shown whole. Resumed from campaign.json itself, the numbers
        // are the save's word, and the data files' notices are capped after them.
        var hud = new DungeonHud(game: null!);
        hud.ShowResume(resumed, [BetweenRunsHud.Resumed, CampaignBoot.TuningChanged, .. data], leading: 2);
        Assert.Equal([.. NoticeText.Wrap(CampaignBoot.TuningChanged), .. NoticeText.Rows(data).Take(NoticeText.MaxRows), NoticeText.More(2)],
            hud.NoticeRows);

        // Where the rows go in the shipped 1280 x 720 window. A short list sits over
        // the banner, which stays where it has always been, a third of the way down,
        // and the rows are lifted clear of RESUMED over it.
        const float h = 720f;
        hud.ShowResume(resumed, [BetweenRunsHud.Resumed, CampaignBoot.TuningChanged, "one"], leading: 2);
        Assert.Equal(3, hud.NoticeRows.Count);
        Assert.Equal(DungeonHud.HeaderLift, hud.NoticeLift);
        Assert.Equal(h / 3f, hud.BannerLineIn(h));
        Assert.True(DungeonHud.NoticesTop(h, hud.NoticeRows.Count, hud.NoticeLift) > DungeonHud.NoticeTopMargin);

        // The whole word does not fit there. The banner moves down to make room: the
        // rows start just under the readouts along the top and end clear of RESUMED.
        hud.ShowResume(resumed, [.. saysWhole, .. data], leading: saysWhole.Length);
        var rows = hud.NoticeRows;
        Assert.Equal(scene.NoticeRows, rows);
        float line = Assert.NotNull(hud.BannerLineIn(h));
        float top = DungeonHud.NoticesTop(h, rows.Count, hud.NoticeLift);
        Assert.True(line > h / 3f, $"the banner stayed at {line} under {rows.Count} rows");
        Assert.Equal(DungeonHud.NoticeTopMargin, top);
        Assert.True(top + rows.Count * DungeonHud.NoticePitch <= line - DungeonHud.OverLineRise - DungeonHud.HeaderLift, "the rows run into RESUMED");

        // The most rows the save's word can take is every row shown whole, the three
        // the data files get after it, and the count of the rest. Even then the
        // banner stays in the upper two thirds of the window, so the boss floor's
        // trade under it clears the help line along the bottom.
        Assert.InRange(DungeonHud.BannerLine(h, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1, DungeonHud.HeaderLift), h / 3f, h * 2f / 3f);

        // The rows stay up long enough to read: twelve seconds for a capped list,
        // and more for every row past its four. A banner put up under them keeps its
        // place until it comes down, a stair arrival while they are up included. So
        // it never jumps when the rows go first.
        Assert.Equal(12f, DungeonHud.NoticeLife(NoticeText.MaxRows + 1));
        float life = DungeonHud.NoticeLife(rows.Count);
        Assert.True(life > DungeonHud.NoticeLife(NoticeText.MaxRows + 1));
        hud.Update(life - 1f);
        Assert.Equal(rows, hud.NoticeRows);
        Assert.Empty(hud.BannerLines);                  // the resumed banner is down, and the word still up
        hud.ShowArrival(resumed);                       // the party takes the stairs while it is
        Assert.Equal(line, hud.BannerLineIn(h));
        hud.Update(1.5f);
        Assert.Empty(hud.NoticeRows);                   // the word has gone
        Assert.NotEmpty(hud.BannerLines);               // the arrival's banner has not,
        Assert.Equal(line, hud.BannerLineIn(h));        // and it stays where it went up
        hud.Update(1f);
        Assert.Empty(hud.BannerLines);
        hud.ShowArrival(resumed);                       // the next goes up where banners always have
        Assert.Equal(h / 3f, hud.BannerLineIn(h));
    }

    [Fact]
    public void AFailedSaveIsSaidOverTheFloorAtOnce()
    {
        // The audit's pass (1f): a save the host could not write while a run is
        // played is said over the floor straight away — where the player is, not
        // only on the screen between runs once the run is over — as the save's
        // word, shown whole ahead of whatever is up. Built with no window, as a
        // scene is read.
        string held = CampaignBoot.WriteFailed("another program has it open");
        var campaign = CampaignState.Start(GameContent.Current);
        var visit = campaign.Enter("spear_hall", mapSeed: 4);
        visit.OpenTurns(campaign.Party, () => 10);
        var scene = new DungeonScene(camera: null!, game: null!, campaign, new NoRunHost());
        Assert.Empty(scene.NoticeRows);
        scene.SaveFailed(held);
        Assert.Equal(NoticeText.Wrap(held), scene.NoticeRows);
        Assert.Equal(held, string.Join(" ", scene.NoticeRows.Select(row => row.Trim())));

        // Over what is up already — a dev override's startup notices, here — it
        // leads, whole, and what was up stays under it, capped as ever.
        string[] data = [.. Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1")];
        var hud = new DungeonHud(game: null!);
        hud.ShowNotices(data);
        hud.ShowSaveFailed(held);
        Assert.Equal([.. NoticeText.Wrap(held), .. NoticeText.Rows(data)], hud.NoticeRows);

        // The same words again — an autosave that fails at every turn's start fails
        // with the same words — are said once: what is up stays up, for its whole
        // life from now.
        var rows = hud.NoticeRows;
        hud.Update(10f);
        hud.ShowSaveFailed(held);
        Assert.Equal(rows, hud.NoticeRows);
        hud.Update(DungeonHud.NoticeLife(rows.Count) - 0.5f);
        Assert.Equal(rows, hud.NoticeRows);
        hud.Update(1f);
        Assert.Empty(hud.NoticeRows);

        // Once the rows are down it is said alone; another reason leads the one up.
        hud.ShowSaveFailed(held);
        Assert.Equal(NoticeText.Wrap(held), hud.NoticeRows);
        string full = CampaignBoot.WriteFailed("the disk is full");
        hud.ShowSaveFailed(full);
        Assert.Equal([.. NoticeText.Wrap(full), .. NoticeText.Wrap(held)], hud.NoticeRows);

        // Over a floor's arrival banner the rows sit over it as ever, and the banner
        // stays a third of the way down in the shipped 1280 x 720 window.
        const float h = 720f;
        var arrived = new DungeonHud(game: null!);
        arrived.ShowArrival(visit);
        arrived.ShowSaveFailed(held);
        Assert.Equal(0f, arrived.NoticeLift);
        Assert.Equal(h / 3f, arrived.BannerLineIn(h));
        Assert.True(DungeonHud.NoticesTop(h, arrived.NoticeRows.Count, arrived.NoticeLift) > DungeonHud.NoticeTopMargin);

        // Over a resumed floor with nothing else to say, the rows sit clear of
        // RESUMED over its banner.
        var quiet = new DungeonHud(game: null!);
        quiet.ShowResume(visit, [], leading: 0);
        quiet.ShowSaveFailed(held);
        Assert.Equal(NoticeText.Wrap(held), quiet.NoticeRows);
        Assert.Equal(DungeonHud.HeaderLift, quiet.NoticeLift);

        // Over a resumed floor's save's word it joins that word, whole, and the
        // banner, which went up under fewer rows, moves down under these — never up —
        // so no row is drawn over RESUMED.
        const string path = @"C:\Users\a-player\AppData\Local\DistanceRPG\saves\campaign.crashed-1.json";
        string[] saysWhole =
        [
            CampaignBoot.ResumedFromBackup,
            CampaignBoot.TuningChanged,
            CampaignBoot.SetAsideNotice(SaveStore.FileName, "crashed the game the last time it was resumed", path),
        ];
        var resumed = new DungeonHud(game: null!);
        resumed.ShowResume(visit, [.. saysWhole, .. data], leading: saysWhole.Length);
        float before = Assert.NotNull(resumed.BannerLineIn(h));
        resumed.ShowSaveFailed(held);
        Assert.Equal(NoticeText.Rows([held, .. saysWhole, .. data], whole: saysWhole.Length + 1), resumed.NoticeRows);
        Assert.Equal(DungeonHud.HeaderLift, resumed.NoticeLift);
        float line = Assert.NotNull(resumed.BannerLineIn(h));
        Assert.True(line > before, $"the banner stayed at {line} under {resumed.NoticeRows.Count} rows");
        Assert.Equal(DungeonHud.BannerLine(h, resumed.NoticeRows.Count, resumed.NoticeLift), line);
        float top = DungeonHud.NoticesTop(h, resumed.NoticeRows.Count, resumed.NoticeLift);
        Assert.Equal(DungeonHud.NoticeTopMargin, top);
        Assert.True(top + resumed.NoticeRows.Count * DungeonHud.NoticePitch <= line - DungeonHud.OverLineRise - DungeonHud.HeaderLift, "the rows run into RESUMED");
    }

    [Fact]
    public void TheMireLegendReadsTheBudgetTheTurnWasCutFrom()
    {
        // 1b: the whole a Mire legend states is the member's own — what its turn's
        // start cut the cap from, which the save carries — and never a cache
        // beside it that a reload loses. In the player phase it is the member's
        // BudgetBeforeMire; from the turn's end on, the coming turn's: the budget
        // plus what the member has just banked. Exactly what the scene's cache of
        // the bank gave in every phase, and the same after a reload.
        var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
        (member.X, member.Y) = Displacer.CentreOf(5, 5);
        member.ApplyStatus(Mire, null, 3);
        var turns = new TurnSystem(new int[12, 20], [member], Array.Empty<EnemyState>(), () => 10);   // nobody to take the enemies' turn: it comes straight round
        var tuning = GameContent.Current.Tuning;
        Assert.Equal(160f, DungeonScene.BudgetBeforeMire(member, TurnPhase.Player, tuning));   // no turn has started: the budget alone

        member.DistLeft = 100f;
        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.Equal(50f, member.SavedMovement);
        Assert.Equal(210f, DungeonScene.BudgetBeforeMire(member, turns.Phase, tuning));   // the coming turn's whole
        for (int step = 0; step < 10_000 && turns.Phase != TurnPhase.Player; step++)
            turns.Update(SaveGame.LogicStepSeconds);
        Assert.Equal(TurnPhase.Player, turns.Phase);

        // The turn has started: the bank is spent, and the whole it was cut from is
        // the member's — what the MOVE readout's cap was cut from, Mire's cut a
        // share of it by level (two levels left of the three: a round decays one).
        Assert.Equal(0f, member.SavedMovement);
        Assert.Equal(210f, DungeonScene.BudgetBeforeMire(member, TurnPhase.Player, tuning));
        Assert.Equal(StatusBehaviours.MiredBudget(member, 210f), member.EffectiveMax);
        Assert.Equal($"MIRE {member.StatusLevel(Mire)}: MOVEMENT {member.EffectiveMax:0} OF 210",
            DungeonHud.StatusLegend(member, DungeonScene.BudgetBeforeMire(member, TurnPhase.Player, tuning)).Single().Text);

        // A tuned budget moves the coming turn's whole, and never the whole a turn
        // already cut: that is the member's.
        var tuned = tuning with { MovementBudget = 200 };
        member.SavedMovement = 30f;
        Assert.Equal(230f, DungeonScene.BudgetBeforeMire(member, TurnPhase.EnemyMoving, tuned));
        Assert.Equal(210f, DungeonScene.BudgetBeforeMire(member, TurnPhase.Player, tuned));

        // And the same after a reload: the whole is the member's own, and the save
        // carries it, where a cache beside the member began a resumed session at
        // the bare budget.
        var campaign = CampaignState.Start(GameContent.Current);
        var saved = campaign.Party[0];
        saved.SavedMovement = 44f;
        saved.StartTurn();
        var back = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign.Party[0];
        Assert.Equal(204f, DungeonScene.BudgetBeforeMire(saved, TurnPhase.Player, tuning));
        Assert.Equal(204f, DungeonScene.BudgetBeforeMire(back, TurnPhase.Player, tuning));
    }

    /// <summary>The host of a scene that is built and read, never played: none of its runs ever ends, and it never saves.</summary>
    private sealed class NoRunHost : IRunHost
    {
        public void RunOver() => throw new InvalidOperationException("A scene built to be read is never played to its end.");

        public void RequestSave() => throw new InvalidOperationException("A scene built to be read is never played, so it never asks for a save.");
    }
}
