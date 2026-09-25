using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.1's Run group and its per-floor group, saved (PHASE5-003–006, -020–026,
/// -134, PHASE4-114): a campaign mid-visit at any safe point becomes a document
/// and comes back field for field — the seed, the depth and the promise as the
/// save has them, the floor, the deepest floor, the clock, the boss's death, the
/// party's XP at the door, the spoils wherever they lie, the turn's counters in
/// one canonical order, and every floor the party has stood on: its rows, its
/// fog, its loose items, and on the current floor what the party can see this
/// turn. A drop already lying on a corpse comes back as it lay. The maps are
/// never in the document: every floor is regenerated from the seed, and enemies
/// are placed only on the floors nobody has reached.
/// <para>
/// Played by <see cref="VisitScript"/>, on the game's own campaign
/// (<see cref="CampaignState.Start"/>) and catalogue weapons only — a save names
/// content by id — with every seed's depth pinned by an assertion before a test
/// leans on it. In the content collection, since several restores run under
/// another tuning, other relations or a reordered weapons file.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class VisitSaveTests
{
    /// <summary>A seed the Spear Hall rolls five floors on, its boss holding a Tower Guard (pinned where a test leans on it).</summary>
    private const long FiveDeep = 4;

    /// <summary>The shipped seed: the tutorial is two floors deep on it, its golem holding a Pinning Lance.</summary>
    private const long GoldenSeed = 2762136374;

    private const StringComparison Ordinal = StringComparison.Ordinal;

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    /// <summary>The restore of <paramref name="text"/> after <paramref name="edit"/> to its tree: refused, and where.</summary>
    private static SaveFormatException RefusedAfter(string text, Action<JsonObject> edit)
    {
        var tree = JsonNode.Parse(text)!.AsObject();
        edit(tree);
        var file = SaveCodec.Read(tree.ToJsonString());
        return Assert.Throws<SaveFormatException>(() => SaveGame.Restore(file, GameContent.Current));
    }

    private static JsonObject VisitNode(JsonObject tree) => tree["campaign"]!["visit"]!.AsObject();

    private static JsonObject FloorNode(JsonObject tree, int index) => VisitNode(tree)["floors"]![index]!.AsObject();

    /// <summary>
    /// The paths a comparison of a played campaign with its reload leaves out: the
    /// sight of every floor nobody stands on. The save keeps the current floor's
    /// sight alone (1b) — any other floor's is stale, and the arrival that next
    /// shows it resets it before anything reads it — so a reload holds none there.
    /// </summary>
    private static Func<string, bool> OtherFloorsSight(DungeonState visit)
        => path => path.StartsWith("campaign.Visit.Floors[", Ordinal) && path.EndsWith("].Fog.Visible", Ordinal)
            && path != $"campaign.Visit.Floors[{visit.FloorIndex}].Fog.Visible";

    /// <summary>A weapon as its content id, both spreads and its entries: what it is under whichever content, for comparing a weapon across two.</summary>
    private static string Describe(Weapon weapon)
        => $"{weapon.Id} forged [{weapon.Forged}] live [{weapon.Modifiers}] {string.Join(",", weapon.Enchantments.Select(e => $"{e.Id}:{e.Tier}:{e.Xp}"))} /{weapon.ForgedEnchantmentCount}";

    /// <summary>Every member name in a document, however deep.</summary>
    private static List<string> MemberNames(JsonNode? node)
    {
        var names = new List<string>();
        void Walk(JsonNode? at)
        {
            switch (at)
            {
                case JsonObject obj:
                    foreach (var (name, value) in obj)
                    {
                        names.Add(name);
                        Walk(value);
                    }
                    break;
                case JsonArray array:
                    foreach (var item in array)
                        Walk(item);
                    break;
            }
        }
        Walk(node);
        return names;
    }

    /// <summary>
    /// A wipe on the floor <paramref name="script"/> stands on: every member at 1 HP
    /// carrying Poison 5, and the turn ended — the ticks take the lot, the floor's
    /// game over reaches the visit's Wiped, and the campaign ends the run.
    /// </summary>
    private static RunResult Wipe(VisitScript script)
    {
        RunResult? ended = null;
        void Heard(RunResult result) => ended = result;
        script.Campaign.RunEnded += Heard;
        var turns = script.Turns;
        foreach (var member in script.Party)
        {
            member.Hp = 1;
            member.ApplyStatus(StatusEffectType.Poison, null, 5);
        }
        turns.EndTurn();
        script.Campaign.RunEnded -= Heard;

        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.Null(script.Campaign.Visit);
        return ended ?? throw new Xunit.Sdk.XunitException("The wipe ended no run.");
    }

    // ── The flagship ─────────────────────────────────────────────────────────

    /// <summary>
    /// A visit some way in, played to the point it is saved at: the Spear Hall on
    /// seed 4 (five floors). B's staff put down on floor 1 — gear, never a spoil. A
    /// dagger dummy on floor 2 put down, back up nine turns later with its gain
    /// rolled, and put down again. Down to floor 5 and the boss put down, its own
    /// drop into B's bag, a spoil; a turn waited out at the bottom and the lair
    /// looked over again; up to floor 4, where two daggers are put down for good —
    /// one's drop taken and put back down (a loose spoil), the other's left lying —
    /// B stands inside a seen halberd's reach and C just outside it, the halberd
    /// Mired, and A, mid-turn, has two attacks spent and Poison on it. Fog explored
    /// on every floor. The save point is floor 4 (index 3), turn 10.
    /// </summary>
    /// <param name="die">
    /// The d20 every floor opens with, <see cref="VisitScript.Ten"/> when omitted.
    /// Everything pinned here is pinned on tens, so a die handed in must roll tens
    /// up to the save point — as Phase 5.7's determinism tests' does, which varies
    /// only after it.
    /// </param>
    internal static VisitScript Flagship(Func<int>? die = null)
    {
        // A party that has earned something before it steps in, so what it held at
        // the door is not what it holds now, nor nothing.
        var campaign = NewCampaign();
        campaign.Party[0].Credit(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 40));
        campaign.Party[2].Credit(new XpCredit(XpPool.Health, null, 9));
        campaign.Party[3].Credit(new XpCredit(XpPool.Mana, null, 12));
        var script = VisitScript.Enter(campaign, "spear_hall", FiveDeep, die);
        var visit = script.Visit;
        Assert.Equal((5, 1), (visit.BossFloor, visit.PromisedStacks));

        // Floor 1: B puts down the staff it carried in, where it stands — gear, never a spoil.
        Assert.Equal("staff_of_renewal", script.Member("B").Inventory[1]!.Id);
        script.Leave("B", 1);

        // Floor 2: a dagger dummy farmed — put down, up again nine turns later with
        // its gain rolled, and put down again.
        script.Down();
        var farmed = script.Row(19, "weakspot_stiletto");
        script.Kill("A", farmed);
        script.Look();
        Assert.Equal((1, 0), (farmed.DefeatCount, farmed.DefeatedAtTurn));
        script.Home();
        script.PassTurns(FarmLadder.ResurrectTurns(1));
        Assert.True(farmed.Alive);
        Assert.Equal((9, 1), (visit.Turn, farmed.RevivalsRolled));
        Assert.True(farmed.RevivalDamage + farmed.RevivalMaxHp > 0);
        script.Kill("A", farmed);
        Assert.Equal((2, 9), (farmed.DefeatCount, farmed.DefeatedAtTurn));

        // Through floors 3 and 4 to the bottom, and the boss put down with its own
        // drop — forged with the Spear Hall's Brace, the floor's stack on it — into
        // B's bag, where the staff was.
        script.Down();
        script.Down();
        script.Down();
        Assert.Equal(4, visit.FloorIndex);
        var boss = visit.Current.Boss!;
        Assert.Equal((38, "tower_guard"), (boss.SpawnIndex, boss.Weapon.Id));
        script.Kill("A", boss);
        Assert.Equal((true, 9), (visit.BossDefeated, visit.BossDefeatedAtTurn));
        Assert.Equal(1, script.Take("B", boss));
        script.Look();

        // A turn waited out at the bottom, the lair looked over again, and up.
        script.Home();
        script.PassTurns(1);
        script.Walk("A", script.Beside(boss));
        script.Look();
        script.Up();
        Assert.Equal((3, 10), (visit.FloorIndex, visit.Turn));

        // Floor 4, the halberd's room: two daggers put down for good, each lying on
        // its drop now the boss is dead — one's taken into A's bag and put back down
        // where A stands, a spoil lying loose; the other's left lying.
        var halberd = script.Row(0, "halberd");
        var taken = script.Row(1, "assassins_fang");
        var lying = script.Row(2, "assassins_fang");
        script.Kill("A", taken);
        Assert.Equal(2, script.Take("A", taken));
        script.Leave("A", 2);
        script.Kill("A", lying);
        Assert.NotNull(script.Turns.GroundItemOf(lying));

        // B walks inside the halberd's reach before anyone sees it, C to just
        // outside; then the room is looked over, and the halberd is seen — B's pair
        // marked inside, C's not (Phase 5.7 walks C in after a reload).
        script.Walk("B", (40, 28));
        script.Walk("C", (41, 26));
        var grid = visit.Current.Map.Grid;
        Assert.True(EnemyAi.CanHit(halberd, script.Member("B"), halberd.Weapon, grid));
        Assert.False(EnemyAi.CanHit(halberd, script.Member("C"), halberd.Weapon, grid));
        script.Look();
        Assert.True(script.Turns.AnyLiveEnemySeenThisTurn);

        // A status on a row (the rows are the floor's stored state), and A mid-turn:
        // two attacks spent, and poisoned.
        halberd.ApplyStatus(StatusEffectType.Mire, null, 2);
        script.Member("A").ApplyStatus(StatusEffectType.Poison, null, 2);
        Assert.Equal(2, script.Turns.AttacksThisTurn(script.Member("A")));
        return script;
    }

    [Fact]
    public void AVisitMidFloorRoundTripsFieldForField()
    {
        // The flagship (PHASE5-003–006, -020–023, PHASE4-114): capture, write, read,
        // restore — and the restored campaign is the campaign, field for field, once
        // its floor is opened as an arrival opens one, and writes the same text.
        var script = Flagship();
        var campaign = script.Campaign;
        var visit = script.Visit;

        string text = Save(campaign);
        var loaded = Load(text);
        Assert.False(loaded.TuningChanged);
        var restored = loaded.Campaign;
        var back = restored.Visit!;

        // The load opens nothing and moves nobody: whoever plays the floor opens it
        // (and imports the turn's counters), and until then the save reads the same.
        Assert.Null(back.Turns);
        Assert.Equal(text, Save(restored));
        new VisitScript(restored).Resume();
        StateAssert.Equivalent(campaign, restored, "campaign", OtherFloorsSight(visit));
        Assert.Equal(text, Save(restored));

        // §5.1's Run group, in the document as plain values: the seed (-003), the
        // floor (-004), the turn (-005), the XP at the door (-006, PHASE4-114) — what
        // the party had earned before it stepped in, by class name, and not what it
        // holds now — and the depth, the promise, the deepest floor and the boss's
        // death as the values a load uses.
        var saved = SaveCodec.Read(text).Campaign.Visit!;
        Assert.Equal(("spear_hall", FiveDeep, 5, 1), (saved.DungeonId, saved.MapSeed, saved.BossFloor, saved.PromisedStacks));
        Assert.Equal((3, 4, 10, true, 9), (saved.FloorIndex, saved.DeepestFloorIndex, saved.Turn, saved.BossDefeated, saved.BossDefeatedAtTurn));
        Assert.Equal(["A", "B", "C", "D"], saved.EntrySnapshot.Members.Select(m => m.MemberId));
        Assert.Equal([("Dagger", 40)], saved.EntrySnapshot.Members[0].WeaponXp.Select(entry => (entry.Key, entry.Value)));
        Assert.Equal((9, 12), (saved.EntrySnapshot.Members[2].HpXp, saved.EntrySnapshot.Members[3].ManaXp));
        Assert.True(campaign.Party[0].WeaponXp[WeaponClass.Dagger] > 40);
        StateAssert.Equivalent(visit.EntrySnapshot, back.EntrySnapshot, "entrySnapshot");

        // Every floor stood on, and only those (-024): all five, the bottom's boss
        // row among the bottom's rows.
        Assert.Equal([0, 1, 2, 3, 4], saved.Floors.Select(f => f.Index));
        Assert.Equal([false, false, false, false, true], saved.Floors.Select(f => f.Enemies.Any(e => e.Boss)));
        Assert.Equal("tower_guard", saved.Floors[4].Enemies.Single(e => e.Boss).Weapon.Id);

        // The rows (-020, -021, -022): position, HP, alive, the defeat turn and
        // count, the ladder's running totals, the weapon and the statuses.
        var farmedRow = saved.Floors[1].Enemies.Single(e => e.SpawnIndex == 19);
        Assert.Equal((false, 9, 2, 1), (farmedRow.Body.Alive, farmedRow.DefeatedAtTurn, farmedRow.DefeatCount, farmedRow.RevivalsRolled));
        Assert.Equal(farmedRow.RevivalDamage + farmedRow.RevivalMaxHp, visit.Floors[1].Enemies[19].RevivalDamage + visit.Floors[1].Enemies[19].RevivalMaxHp);
        var halberdRow = saved.Floors[3].Enemies.Single(e => e.SpawnIndex == 0);
        Assert.Equal(("halberd", true), (halberdRow.Weapon.Id, halberdRow.Body.Alive));
        Assert.Equal((visit.Floors[3].Enemies[0].X, visit.Floors[3].Enemies[0].Y), (halberdRow.Body.X, halberdRow.Body.Y));
        Assert.Equal([new StatusDto("Mire", null, 2)], halberdRow.Body.Statuses);

        // The explored fog on every floor (-023), the current floor's sight and no
        // other's, and the loose items: B's staff on floor 1, gear; the fang's drop
        // on floor 4, a spoil.
        Assert.All(saved.Floors, f => Assert.Contains(f.Seen, row => row.Contains('1')));
        Assert.Equal([false, false, false, true, false], saved.Floors.Select(f => f.Visible is not null));
        Assert.Equal("staff_of_renewal", saved.Floors[0].LooseItems.Single().Weapon.Id);
        Assert.Single(saved.Floors[3].LooseItems);
        for (int n = 0; n < 5; n++)
            Assert.Equal(visit.Floors[n].Fog.Seen.Cast<bool>(), back.Floors[n].Fog.Seen.Cast<bool>());
        Assert.Equal(visit.Current.Fog.Visible.Cast<bool>(), back.Current.Fog.Visible.Cast<bool>());

        // The spoils in the order taken, each by where it is — B's bag, and lying on
        // floor 4 — and each restored as the very weapon now there.
        Assert.Equal([new SpoilDto(Member: "B", Slot: 1), new SpoilDto(Floor: 3, Loose: 0)], saved.Spoils);
        Assert.Equal(2, back.Spoils.Count);
        Assert.Same(restored.Party[1].Inventory[1], back.Spoils[0]);
        Assert.Same(back.Floors[3].LooseItems[0].Weapon, back.Spoils[1]);

        // The drops lying on corpses, pinned as the game answered them: the fang
        // left lying on floor 4, and the farmed dagger dummy on floor 2, down for good
        // since the boss fell. The taken ones lie on nothing.
        var lying = saved.Floors[3].Enemies.Single(e => e.SpawnIndex == 2);
        Assert.NotNull(lying.GroundItem);
        Assert.Equal(Describe(visit.Turns!.GroundItemOf(visit.Current.Enemies[2])!.Weapon), Describe(back.Turns!.GroundItemOf(back.Current.Enemies[2])!.Weapon));
        Assert.NotNull(back.Current.Enemies[2].PinnedDrop);
        Assert.NotNull(farmedRow.GroundItem);
        Assert.Null(saved.Floors[3].Enemies.Single(e => e.SpawnIndex == 1).GroundItem);
        Assert.Null(saved.Floors[4].Enemies.Single(e => e.Boss).GroundItem);

        // The turn's counters: A's two attacks, the halberd seen — and the boss's
        // floor's rows seen earlier this turn, named by floor and spawn.
        var carry = saved.Carry!;
        Assert.Equal(10, carry.Turn);
        Assert.Equal([new CountDto(new ActorRefDto(Member: "A"), 2)], carry.Attacks);
        Assert.Contains(new ActorRefDto(Floor: 3, Spawn: 0), carry.Seen);
        Assert.Contains(new ActorRefDto(Floor: 4, Spawn: 38), carry.Seen);

        // What a floor with no system open lies on is what its system would say,
        // row for row, on every floor of both campaigns (S6's risk 6).
        foreach (var played in new[] { campaign, restored })
        {
            var at = played.Visit!;
            foreach (var floor in at.Floors)
            {
                var system = ReferenceEquals(floor, at.Current)
                    ? at.Turns!
                    : new TurnSystem(floor.Map.Grid, played.Party, floor.Enemies, VisitScript.Ten, floor.Seed, firstTurn: at.Turn, boss: floor.Boss)
                    {
                        ResurrectionActive = !at.BossDefeated,
                    };
                foreach (var row in floor.Enemies)
                    StateAssert.Equivalent(system.GroundItemOf(row), DungeonState.GroundItemOf(floor, row, at.BossDefeated), $"floors[{floor.Index}].enemies[{row.SpawnIndex}]");
            }
        }
    }

    [Fact]
    public void OnlyVisitedFloorsAreSaved_TheRestComeBackFromTheSeed()
    {
        // PHASE5-024, -026: "regenerate the dungeon from the seed on every entry,
        // place enemies only on the first visit, restore saved states afterwards".
        // On floor 2 of five, the document holds floors 1 and 2 and no other; a
        // load stands their saved rows back — a dummy put down stays down, never
        // placed again — and builds floors 3 to 5 from the seed, placed exactly as
        // an entry places them: rows, stairs, lair, the boss and what it holds.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        var visit = script.Visit;
        Assert.Equal(5, visit.BossFloor);
        script.Down();
        var dead = script.Row(19, "weakspot_stiletto");
        script.Kill("A", dead);
        string text = Save(script.Campaign);

        var saved = SaveCodec.Read(text).Campaign.Visit!;
        Assert.Equal([0, 1], saved.Floors.Select(f => f.Index));

        var back = Load(text).Campaign.Visit!;
        Assert.Equal(5, back.Floors.Length);
        Assert.True(back.Floors[1].Visited);
        Assert.False(back.Floors[1].Enemies[19].Alive);
        Assert.Equal(1, back.Floors[1].Enemies[19].DefeatCount);

        var hall = GameContent.Current.Dungeons["spear_hall"];
        for (int n = 2; n < 5; n++)
        {
            Assert.False(back.Floors[n].Visited);
            StateAssert.Equivalent(Floor.Generate(FiveDeep, n, bottom: n == 4, hall, back.PromisedStacks), back.Floors[n], $"floors[{n}]");
            StateAssert.Equivalent(visit.Floors[n], back.Floors[n], $"floors[{n}]");
        }
        Assert.Equal(("tower_guard", 1), (back.Floors[4].Boss!.Weapon.Id, back.Floors[4].Boss!.PromisedStacks));
    }

    [Fact]
    public void NoMapIsEverSerialized()
    {
        // PHASE5-025: "Maps are never serialized — they regenerate from
        // floorSeed(n)." No member of the document names a map's grid, rooms,
        // corridors, start, fog boxes or stairs — five floors saved — and the
        // document stays the size of what a visit changed: the fog is one
        // character a tile, and the rest is rows, far short of a grid a floor.
        var script = Flagship();
        string text = Save(script.Campaign);

        static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
        var map = typeof(MapData).GetProperties().Select(p => p.Name)
            .Concat(typeof(FogBoxSet).GetProperties().Select(p => p.Name))
            .Concat([nameof(Floor.Map), nameof(Floor.FogBoxes), nameof(Floor.UpStairs), nameof(Floor.DownStairs), nameof(Floor.Lair), nameof(Floor.Seed)])
            .Select(Camel)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = MemberNames(JsonNode.Parse(text));
        Assert.Contains("floors", names);
        Assert.DoesNotContain(names, map.Contains);

        // The fog is the only per-tile member, one character a tile; everything
        // else is what the visit changed, a row an enemy. A map's grid written out
        // as numbers, one to a line, is some fifty thousand characters a floor:
        // one of them, never mind a grid and a room grid a floor, would take the
        // document past the bound.
        var saved = SaveCodec.Read(text).Campaign.Visit!;
        int fog = saved.Floors.Sum(f => f.Seen.Sum(row => row.Length) + (f.Visible?.Sum(row => row.Length) ?? 0));
        Assert.Equal(6 * MapGenerator.Rows * MapGenerator.Cols, fog);   // five floors explored, one floor's sight
        Assert.True(text.Length < 64 * 1024 * saved.Floors.Count, $"{text.Length} characters for {saved.Floors.Count} floors");
    }

    [Fact]
    public void TheBossKeepsTheWeaponItHeld()
    {
        // 4D:278, A33: the boss's row is saved with what it holds, and a load
        // restores it — never re-rolls it. Restored under relations that take the
        // sword out of the Spear Hall boss's hands (every sword forges Block, and
        // Brace now excludes it), this seed's boss would be drawn a spear at a new
        // entry; the saved one still holds its Tower Guard, on its dungeon's
        // statline.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        while (script.Floor.DownStairs is not null)
            script.Down();
        var boss = script.Floor.Boss!;
        Assert.Equal(("tower_guard", WeaponClass.Sword), (boss.Weapon.Id, boss.Weapon.Class));
        string text = Save(script.Campaign);

        var blockOrBrace = ShippedContent.Restricted with { Excludes = [.. ShippedContent.Restricted.Excludes, ["Brace", "Block"]] };
        using (TestContent.Use(restricted: blockOrBrace))
        {
            var hall = GameContent.Current.Dungeons["spear_hall"];
            Assert.DoesNotContain(WeaponClass.Sword, GameContent.Current.Dungeons.WieldClasses(hall));
            Assert.Equal(WeaponClass.Spear, DungeonState.RollBoss(FiveDeep, hall).Class);
            Assert.NotEqual("tower_guard", Floor.Generate(FiveDeep, 4, bottom: true, hall, 1).Boss!.Weapon.Id);

            var loaded = Load(text);
            Assert.False(loaded.TuningChanged);
            var restored = loaded.Campaign.Visit!.Floors[4].Boss!;
            Assert.Equal("tower_guard", restored.Weapon.Id);
            Assert.Equal(boss.Weapon.Modifiers, restored.Weapon.Modifiers);
            Assert.Equal((boss.SpawnIndex, boss.X, boss.Y), (restored.SpawnIndex, restored.X, restored.Y));
            Assert.Equal((200, 70f, "Spear Warden", (int?)1), (restored.MaxHp, restored.MoveBudget, restored.Title, restored.PromisedStacks));
            Assert.Equal(ModifierSet.Of((ModifierType.Brace, 3)), restored.Innate);
            Assert.Equal(ModifierType.Brace, restored.Theme);
        }
    }

    [Fact]
    public void TheDepthAndThePromiseAreTheSavesNotTheTunings()
    {
        // 4D:278, L1360-1367, re-pinning PHASE4-134: the boss floor and the stacks
        // its drop was promised are fixed at entry — "a tuning change during the
        // visit moves neither" — and a load uses the saved values. Restored under a
        // tuning that would roll this seed's boss onto floor 3 and price floor 5 at
        // five stacks, the visit is still five floors deep, its promise still one,
        // and the bottom floor is built with the saved promise — generated, when
        // nobody has reached it, and its boss row restored, when the party has.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        Assert.Equal((5, 1), (script.Visit.BossFloor, script.Visit.PromisedStacks));
        string text = Save(script.Campaign);
        var deep = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        while (deep.Floor.DownStairs is not null)
            deep.Down();
        string bottom = Save(deep.Campaign);

        var shallow = ShippedContent.Tuning with { BossFloorMin = 3, BossFloorMax = 4, BossStackOffset = 0 };
        using (TestContent.Use(tuning: shallow))
        {
            var hall = GameContent.Current.Dungeons["spear_hall"];
            Assert.Equal(3, DungeonState.RollBossFloor(FiveDeep, hall));
            Assert.Equal(5, DungeonState.PromisedStacksFor(5));

            var loaded = Load(text);
            Assert.True(loaded.TuningChanged);
            var back = loaded.Campaign.Visit!;
            Assert.Equal((5, 1, 5), (back.BossFloor, back.PromisedStacks, back.Floors.Length));
            Assert.False(back.Floors[4].Visited);
            Assert.Equal(1, back.Floors[4].Boss!.PromisedStacks);
            Assert.Equal(5 * GameContent.Current.Tuning.CooldownRunsPerFloor, back.LockRuns);
            Assert.Equal(text.Replace(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(GameContent.Current.Tuning), Ordinal), Save(loaded.Campaign));

            var reached = Load(bottom).Campaign.Visit!;
            Assert.True(reached.Floors[4].Visited);
            Assert.Equal((5, 1, 1), (reached.BossFloor, reached.PromisedStacks, reached.Floors[4].Boss!.PromisedStacks ?? -1));
        }
    }

    [Fact]
    public void TheTurnsCountersSurviveAReload()
    {
        // 4D:1c: the turn travels — the attacks chosen, the reactions spent, the
        // enemies seen — and so it does across a save. C throws its bandolier's
        // three throws, the lance on floor 1 is seen and braces A as it walks in,
        // and the party hops down and straight back up inside the turn. Saved and
        // reloaded, the floor opens with no throw left, the lance's one brace still
        // spent, and the lance still seen: exactly as the live campaign stands.
        var campaign = NewCampaign();
        campaign.Party[2].Inventory[0] = TestWeapons.Get("bandolier");
        var script = VisitScript.Enter(campaign, "spear_hall", FiveDeep);
        var thrower = script.Member("C");
        Assert.Equal(3, script.Turns.AttacksPerTurn(thrower));
        var javelins = script.Row(11, "softening_javelins");
        var lance = script.Row(10, "pinning_lance");

        script.Walk("C", (10, 24));
        for (int throws = 0; throws < 3; throws++)
            script.Swing("C", javelins);
        Assert.False(script.Turns.HasAttackLeft(thrower));
        Assert.True(javelins.Alive);

        script.Walk("B", (8, 40));
        script.Look();
        int braced = 0;
        script.Turns.EnemyBraceTriggered += enemy => braced += ReferenceEquals(enemy, lance) ? 1 : 0;
        script.Walk("A", (9, 30));
        Assert.Equal(1, braced);

        script.Down();
        script.Up();
        Assert.Equal((0, 0), (script.Visit.FloorIndex, script.Visit.Turn));
        string text = Save(campaign);

        var carry = SaveCodec.Read(text).Campaign.Visit!.Carry!;
        Assert.Contains(new CountDto(new ActorRefDto(Member: "C"), 3), carry.Attacks);
        Assert.Contains(new CountDto(new ActorRefDto(Floor: 0, Spawn: 10), 1), carry.ZoneUses);
        Assert.Contains(new ActorRefDto(Floor: 0, Spawn: 10), carry.Seen);

        var reloaded = new VisitScript(Load(text).Campaign);
        reloaded.Resume();
        foreach (var side in new[] { script, reloaded })
        {
            var turns = side.Turns;
            Assert.Equal(3, turns.AttacksThisTurn(side.Member("C")));
            Assert.False(turns.HasAttackLeft(side.Member("C")));
            Assert.True(turns.AnyLiveEnemySeenThisTurn);
            Assert.Equal(GameContent.Current.Tuning.WeaponSwapCost, turns.SwapCost);

            var sideLance = side.Row(0, 10);
            int fired = 0;
            turns.EnemyBraceTriggered += enemy => fired += ReferenceEquals(enemy, sideLance) ? 1 : 0;
            side.Walk("A", (11, 27));   // out of the lance's reach...
            side.Walk("A", (9, 30));    // ...and back in: its one use this turn is spent
            Assert.Equal(0, fired);
        }
    }

    [Fact]
    public void TheCarryWritesOneTextWhateverOrderItWasBuiltIn()
    {
        // The turn's counters live in hash containers keyed by reference, whose
        // order says nothing and differs between a campaign played through and the
        // same campaign reloaded: the party's zone is exported before the enemies',
        // an earlier floor's entries merged in before a later's. The same counters
        // built in two orders, and merged either way round, write one text — members
        // in roster order, then enemies by floor and spawn index, and a reaction's
        // uses by actor and then modifier — and a text naming one actor twice in a
        // list is refused.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        script.Down();
        var visit = script.Visit;
        var party = script.Party;
        var (a, b, c) = (party[0], party[1], party[2]);
        var upstairs = visit.Floors[0].Enemies[10];
        var here = visit.Floors[1].Enemies[6];
        var there = visit.Floors[1].Enemies[3];
        int turn = visit.Turn;

        static IReadOnlyDictionary<TKey, int> Ordered<TKey>(IEnumerable<(TKey, int)> entries, IEqualityComparer<TKey> comparer) where TKey : notnull
        {
            var table = new Dictionary<TKey, int>(comparer);
            foreach (var (key, count) in entries)
                table.Add(key, count);
            return table;
        }
        IEqualityComparer<ActorState> byReference = ReferenceEqualityComparer.Instance;
        var byPair = EqualityComparer<(ActorState, ModifierType)>.Default;
        (ActorState, int)[] attacks = [(a, 1), (c, 2), (upstairs, 1), (here, 1)];
        (ActorState, int)[] zones = [(b, 1), (there, 2), (upstairs, 1)];
        ((ActorState, ModifierType), int)[] reactions = [((b, ModifierType.Riposte), 1), ((b, ModifierType.Brace), 2), ((here, ModifierType.Riposte), 1)];
        EnemyState[] seen = [there, upstairs, here];

        TurnCarry Build(bool reversed)
        {
            IEnumerable<T> Order<T>(IEnumerable<T> items) => reversed ? items.Reverse() : items;
            return new TurnCarry(turn,
                Ordered(Order(attacks), byReference),
                Ordered(Order(zones), byReference),
                Ordered(Order(reactions), byPair),
                new HashSet<EnemyState>(Order(seen), byReference));
        }

        var file = SaveGame.Capture(script.Campaign);
        string Text(TurnCarry carry)
            => SaveCodec.Write(file with { Campaign = file.Campaign with { Visit = file.Campaign.Visit! with { Carry = SaveMapper.Carry(carry, party, visit) } } });

        string forwards = Text(Build(reversed: false));
        Assert.Equal(forwards, Text(Build(reversed: true)));

        // Merged across two floors either way round: the entries of each, one text.
        var early = new TurnCarry(turn,
            Ordered(new (ActorState, int)[] { (a, 1), (upstairs, 1) }, byReference),
            Ordered(new (ActorState, int)[] { (upstairs, 1) }, byReference),
            Ordered(new ((ActorState, ModifierType), int)[] { ((b, ModifierType.Brace), 2) }, byPair),
            new HashSet<EnemyState>([upstairs], byReference));
        var late = new TurnCarry(turn,
            Ordered(new (ActorState, int)[] { (here, 1), (c, 2) }, byReference),
            Ordered(new (ActorState, int)[] { (there, 2), (b, 1) }, byReference),
            Ordered(new ((ActorState, ModifierType), int)[] { ((here, ModifierType.Riposte), 1), ((b, ModifierType.Riposte), 1) }, byPair),
            new HashSet<EnemyState>([here, there], byReference));
        Assert.Equal(forwards, Text(early.MergedWith(late)));
        Assert.Equal(forwards, Text(late.MergedWith(early)));

        // The canonical order itself.
        var written = SaveMapper.Carry(Build(reversed: true), party, visit)!;
        Assert.Equal(
            [new ActorRefDto(Member: "A"), new ActorRefDto(Member: "C"), new ActorRefDto(Floor: 0, Spawn: 10), new ActorRefDto(Floor: 1, Spawn: 6)],
            written.Attacks.Select(entry => entry.Actor));
        Assert.Equal([new ActorRefDto(Member: "B"), new ActorRefDto(Floor: 0, Spawn: 10), new ActorRefDto(Floor: 1, Spawn: 3)],
            written.ZoneUses.Select(entry => entry.Actor));
        var (first, second) = ModifierType.Brace < ModifierType.Riposte ? ("Brace", "Riposte") : ("Riposte", "Brace");
        Assert.Equal([new ReactionUseDto(new ActorRefDto(Member: "B"), first, first == "Brace" ? 2 : 1),
                new ReactionUseDto(new ActorRefDto(Member: "B"), second, second == "Brace" ? 2 : 1),
                new ReactionUseDto(new ActorRefDto(Floor: 1, Spawn: 6), "Riposte", 1)],
            written.ReactionUses);
        Assert.Equal([new ActorRefDto(Floor: 0, Spawn: 10), new ActorRefDto(Floor: 1, Spawn: 3), new ActorRefDto(Floor: 1, Spawn: 6)], written.Seen);

        // Read back, the counters are the same counters; a list naming one actor twice is refused.
        Assert.Equal(forwards, Save(Load(forwards).Campaign));
        var twice = RefusedAfter(forwards, tree =>
        {
            var list = (JsonArray)VisitNode(tree)["carry"]!["attacks"]!;
            list.Add(list[0]!.DeepClone());
        });
        Assert.Equal("campaign.visit.carry.attacks[4].actor", twice.Where);
        var seenTwice = RefusedAfter(forwards, tree =>
        {
            var list = (JsonArray)VisitNode(tree)["carry"]!["seen"]!;
            list.Add(list[1]!.DeepClone());
        });
        Assert.Equal("campaign.visit.carry.seen[3]", seenTwice.Where);
    }

    [Fact]
    public void TheTurnsLitFogComesBack()
    {
        // 1b: what the party can see this turn is every box a living member has
        // stood in since the turn began — the pick and the marching decision read
        // it — so a member that walks out of a room into a corridor leaves the room
        // lit, the lance in it in sight. Lit afresh from where the party stands now,
        // the lance's tile would be dark; the save keeps the sight bit for bit.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        var floor = script.Floor;
        var lance = script.Row(10, "pinning_lance");
        var (r, c) = Displacer.TileOf(lance);

        script.Walk("A", (8, 40));
        Assert.False(EnemyAi.CanHit(lance, script.Member("A"), lance.Weapon, floor.Map.Grid));
        script.Look();
        Assert.True(floor.Fog.Visible[r, c]);
        script.Walk("A", (10, 24));   // out of the room, into the corridor: a fog box's edge crossed
        script.Look();
        Assert.True(floor.Fog.Visible[r, c]);
        Assert.True(lance.Alive);

        var fresh = new FogState(floor.Fog.Rows, floor.Fog.Cols);
        foreach (var member in script.Party)
        {
            var tile = Displacer.TileOf(member);
            fresh.RevealAt(tile.R, tile.C, floor.FogBoxes.FogBoxes);
        }
        Assert.False(fresh.Visible[r, c]);

        var back = Load(Save(script.Campaign)).Campaign.Visit!.Current;
        Assert.Equal(floor.Fog.Visible.Cast<bool>(), back.Fog.Visible.Cast<bool>());
        Assert.Equal(floor.Fog.Seen.Cast<bool>(), back.Fog.Seen.Cast<bool>());
        Assert.True(back.Fog.Visible[r, c]);
    }

    [Fact]
    public void ASpoilPutDownIsStillASpoilAfterAReload()
    {
        // 4D:1i, L1389-1391: a spoil put down and taken back is the spoil it was —
        // lost to a wipe — and a weapon the party carried in stays gear however
        // often it is put down: never taken. So across a save too: the golem's drop,
        // taken and put down on the tutorial's floor 2, and D's staff put down beside
        // it, both taken back after the reload; the wipe takes the drop and leaves
        // the staff, exactly as without the reload.
        var script = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed);
        Assert.Equal(2, script.Visit.BossFloor);
        script.Down();
        var golem = script.Floor.Boss!;
        Assert.Equal("pinning_lance", golem.Weapon.Id);
        script.Kill("A", golem);
        Assert.Equal(2, script.Take("A", golem));
        var spoil = script.Member("A").Inventory[2]!;
        script.Leave("A", 2);
        Assert.Equal("staff_of_mire", script.Member("D").Inventory[1]!.Id);
        script.Leave("D", 1);
        Assert.Same(spoil, Assert.Single(script.Visit.Spoils));
        string text = Save(script.Campaign);
        Assert.Equal([new SpoilDto(Floor: 1, Loose: 0)], SaveCodec.Read(text).Campaign.Visit!.Spoils);

        var reloaded = new VisitScript(Load(text).Campaign);
        reloaded.Resume();
        var results = new List<RunResult>();
        foreach (var side in new[] { script, reloaded })
        {
            var loose = side.Floor.LooseItems;
            Assert.Equal(["drop", "staff_of_mire"], loose.Select(item => ReferenceEquals(item.Weapon, side.Visit.Spoils[0]) ? "drop" : item.Weapon.Id));
            Assert.Equal(2, side.TakeLoose("A", loose[0]));
            Assert.Equal(1, side.TakeLoose("D", loose[0]));
            var party = side.Party;
            var result = Wipe(side);
            results.Add(result);
            Assert.Equal(RunOutcome.Death, result.Outcome);
            Assert.Empty(result.SpoilsKept);
            Assert.Null(party[0].Inventory[2]);                                 // the spoil taken out of the bag
            Assert.Equal("staff_of_mire", party[3].Inventory[1]!.Id);           // the gear never a spoil
        }
        Assert.Equal(Describe(Assert.Single(results[0].SpoilsLost)), Describe(Assert.Single(results[1].SpoilsLost)));
    }

    [Fact]
    public void AWipeAfterAReloadClawsBackFromTheEntrySnapshot()
    {
        // PHASE5-006, re-pinning PHASE5-007: the party's XP at the door is in the
        // Run group, so a wipe after a reload claws back from it exactly what a
        // wipe without the reload would — DeathXpLossPercent of each pool's gain
        // since the door, and nothing the party walked in with.
        var campaign = NewCampaign();
        campaign.Party[0].Credit(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 60));
        campaign.Party[2].Credit(new XpCredit(XpPool.Health, null, 20));
        campaign.Party[3].Credit(new XpCredit(XpPool.Mana, null, 40));
        var script = VisitScript.Enter(campaign, "tutorial", GoldenSeed);
        script.Kill("A", script.Row(20, "flensing_knife"));
        script.Kill("A", script.Row(3, "assassins_fang"));
        script.Member("C").Credit(new XpCredit(XpPool.Health, null, 9));
        script.Member("D").Credit(new XpCredit(XpPool.Mana, null, 12));
        Assert.NotEqual(0, script.Member("A").WeaponXp[WeaponClass.Dagger]);
        string text = Save(script.Campaign);

        var reloaded = new VisitScript(Load(text).Campaign);
        reloaded.Resume();
        var live = Wipe(script);
        var afterReload = Wipe(reloaded);
        Assert.NotEmpty(live.Clawbacks);
        Assert.Contains(live.Clawbacks, back => back.Pool == nameof(WeaponClass.Dagger));
        Assert.Contains(live.Clawbacks, back => back.Pool == XpSnapshot.HpPool);
        Assert.Contains(live.Clawbacks, back => back.Pool == XpSnapshot.ManaPool);
        Assert.Equal(live.Clawbacks, afterReload.Clawbacks);
        StateAssert.Equivalent(script.Campaign.Party, reloaded.Campaign.Party, "party");

        // Only the gain since the door is clawed, at DeathXpLossPercent: what each
        // pool walked in with — 60 dagger XP, 20 HP XP, 40 mana XP — is kept whole.
        int loss = GameContent.Current.Tuning.DeathXpLossPercent;
        foreach (var (member, pool, atDoor) in new[] { ("A", nameof(WeaponClass.Dagger), 60), ("C", XpSnapshot.HpPool, 20), ("D", XpSnapshot.ManaPool, 40) })
        {
            var clawed = afterReload.Clawbacks.Single(back => back.MemberId == member && back.Pool == pool);
            Assert.Equal(clawed.Before - (clawed.Before - atDoor) * loss / 100, clawed.After);
        }
    }

    [Fact]
    public void AGroundItemReDerivesAfterAReload_ATakenOneStaysTaken()
    {
        // 1c: a drop lying on a corpse is pinned by the save, and under the same
        // tuning the pin is exactly the drop the row would derive — field for field,
        // a dummy's off its own table and the golem's off its dungeon's — while a
        // drop already taken stays taken. A pinned drop taken after the reload is
        // retired like any other.
        var script = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed);
        script.Down();
        var floor = script.Floor;
        script.Kill("A", floor.Boss!);
        var taken = script.Row(22, "hatchet");
        var lying = script.Row(21, "reaver");
        script.Kill("A", taken);
        script.Take("B", taken);
        script.Kill("A", lying);
        string text = Save(script.Campaign);

        var back = new VisitScript(Load(text).Campaign);
        var turns = back.Resume();
        var backFloor = back.Floor;
        var backLying = back.Row(1, 21);
        var backGolem = backFloor.Boss!;
        Assert.NotNull(backLying.PinnedDrop);
        Assert.NotNull(backGolem.PinnedDrop);
        StateAssert.Equivalent(LootTable.Roll(backLying, backFloor.Seed), turns.GroundItemOf(backLying), "reaver");
        StateAssert.Equivalent(script.Turns.GroundItemOf(lying), turns.GroundItemOf(backLying), "reaver");
        StateAssert.Equivalent(BossLoot.Roll(backGolem, backFloor.Seed), turns.GroundItemOf(backGolem), "golem");

        var backTaken = back.Row(1, 22);
        Assert.True(backTaken.DropTaken);
        Assert.Null(backTaken.PinnedDrop);
        Assert.Null(turns.GroundItemOf(backTaken));

        back.Take("C", backLying);
        Assert.True(backLying.DropTaken);
        Assert.Null(turns.GroundItemOf(backLying));
    }

    [Fact]
    public void ARowThatGetsUpLiesOnNothing_ItsNextDropIsItsOwn()
    {
        // A pin is what a corpse lies on, and a corpse that gets up lies on
        // nothing. The flensing-knife dummy on the tutorial's first floor falls at
        // turn 0, due back at turn 9; the party waits nine turns below and puts the
        // golem down at turn 9. The save pins the dummy's drop — it lies dead
        // upstairs with the boss down — but its wait ran out the turn the boss fell,
        // so the climb finds it standing (§4.1's catch-up), and put down for good it
        // lies on its own drop, rolled off the depth it has now: the one the same
        // climb without the reload lands.
        var script = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed);
        var knife = script.Row(20, "flensing_knife");
        script.Kill("A", knife);
        Assert.Equal((1, 0, 9), (knife.DefeatCount, knife.DefeatedAtTurn, FarmLadder.ResurrectTurns(1)));
        script.Down();
        script.Home();
        script.PassTurns(9);
        script.Kill("A", script.Floor.Boss!);
        Assert.Equal(9, script.Visit.BossDefeatedAtTurn);
        string text = Save(script.Campaign);
        Assert.NotNull(SaveCodec.Read(text).Campaign.Visit!.Floors[0].Enemies.Single(e => e.SpawnIndex == 20).GroundItem);

        var back = new VisitScript(Load(text).Campaign);
        back.Resume();
        var row = back.Row(0, 20);
        Assert.NotNull(row.PinnedDrop);

        var drops = new List<Drop>();
        foreach (var (side, dummy) in new[] { (script, knife), (back, row) })
        {
            side.Up();
            Assert.True(dummy.Alive);
            Assert.Null(dummy.PinnedDrop);
            side.Kill("A", dummy);
            var drop = side.Turns.GroundItemOf(dummy)!;
            Assert.Equal(2, drop.DefeatCount);
            StateAssert.Equivalent(LootTable.Roll(dummy, side.Floor.Seed), drop, "drop");
            drops.Add(drop);
        }
        StateAssert.Equivalent(drops[0], drops[1], "drop");
    }

    [Fact]
    public void UnderANewTuningALyingDropIsTheOneSeen_ARevivalFollowsTheNewNumbers()
    {
        // 1c: across a changed tuning, an outcome already shown stays as it was and
        // a future event follows the new numbers. A drop lying untaken is the one
        // the player may have seen, so under a tuning that would now roll every drop
        // with an entry and no farm stacks the reload still lies on the saved weapon
        // — on the floor the party left, which a save writes with no system open
        // there, and saved again under the new numbers, and on the floor's system
        // when the party goes back down to it.
        var script = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed);
        script.Down();
        script.Kill("A", script.Floor.Boss!);
        var reaver = script.Row(21, "reaver");
        script.Kill("A", reaver);
        var shown = script.Turns.GroundItemOf(reaver)!;
        Assert.Empty(shown.Weapon.Enchantments);
        script.Up();
        string text = Save(script.Campaign);

        var generous = ShippedContent.Tuning with { DropEnchantChancePercent = 100, DefeatStackChance = 0 };
        using (TestContent.Use(tuning: generous))
        {
            var loaded = Load(text);
            Assert.True(loaded.TuningChanged);
            Assert.Equal(text.Replace(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(GameContent.Current.Tuning), Ordinal), Save(loaded.Campaign));
            var back = new VisitScript(loaded.Campaign);
            var row = back.Row(1, 21);
            Assert.Equal(Describe(shown.Weapon), Describe(DungeonState.GroundItemOf(back.Visit.Floors[1], row, bossDefeated: true)!.Weapon));
            back.Resume();
            back.Down();
            Assert.Equal(Describe(shown.Weapon), Describe(back.Turns.GroundItemOf(row)!.Weapon));
            Assert.NotEqual(Describe(shown.Weapon), Describe(LootTable.Roll(row, back.Floor.Seed).Weapon));   // what a fresh roll would lay now
        }

        // A revival is a future event (the arrival rule, L1307-1315): with the boss
        // standing, a dummy put down a turn before the save — not due for nine turns
        // under the shipped numbers — gets up as its floor is opened under a tuning
        // that makes it due, and stays down under the numbers it was saved under.
        var farm = VisitScript.Enter(NewCampaign(), "tutorial", GoldenSeed);
        var knife = farm.Row(20, "flensing_knife");
        farm.Kill("A", knife);
        farm.Home();
        farm.PassTurns(1);
        Assert.Equal((1, 0, 1), (knife.DefeatCount, knife.DefeatedAtTurn, farm.Visit.Turn));
        string pending = Save(farm.Campaign);

        var same = new VisitScript(Load(pending).Campaign);
        same.Resume();
        Assert.False(same.Row(0, 20).Alive);

        var quick = ShippedContent.Tuning with { ResurrectTurnsBase = 1, ResurrectTurnsFloor = 1 };
        using (TestContent.Use(tuning: quick))
        {
            var loaded = Load(pending);
            Assert.True(loaded.TuningChanged);
            var back = new VisitScript(loaded.Campaign);
            var row = back.Row(0, 20);
            Assert.False(row.Alive);
            back.Resume();
            Assert.True(row.Alive);
            Assert.Equal(1, row.RevivalsRolled);
        }
    }

    [Fact]
    public void EnemiesAreSavedByWeaponIdNeverByPlace()
    {
        // PHASE5-134: "a positional reference means adding one weapon silently
        // re-rolls every enemy in every existing save". Every row names what it
        // holds by id, so under a weapons.json whose entries run backwards every
        // row of every floor holds its own weapon, and the visit writes the same text.
        var script = Flagship();
        string text = Save(script.Campaign);

        var reversed = ShippedContent.Weapons with { Weapons = ShippedContent.Weapons.Weapons.Reverse().ToArray() };
        using (TestContent.Use(weapons: reversed))
        {
            Assert.NotEqual(ShippedContent.Weapons.Weapons[0].Id, GameContent.Current.Weapons.All[0].Id);
            var loaded = Load(text);
            Assert.False(loaded.TuningChanged);
            var back = loaded.Campaign.Visit!;
            for (int n = 0; n < back.Floors.Length; n++)
                Assert.Equal(script.Visit.Floors[n].Enemies.Select(e => e.Weapon.Id), back.Floors[n].Enemies.Select(e => e.Weapon.Id));
            Assert.Equal(text, Save(loaded.Campaign));
        }
    }

    [Fact]
    public void OpeningARestoredFloorRevivesNobody_AndArmsWhatItLeft()
    {
        // Under the tuning it was saved under, a floor opened after a load finds
        // nobody due — every due dummy rose at the turn's start, and one put down
        // since waits a turn at least — so no row moves; and the zones are re-read
        // from where everyone stands, as they were kept: B, who walked into the
        // lance's reach, is inside it — a step within draws nothing, a step out and
        // back in draws the brace — on the reload as on the live floor.
        var script = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        var javelins = script.Row(11, "softening_javelins");
        var lance = script.Row(10, "pinning_lance");
        script.Kill("A", javelins);
        script.Home();
        script.PassTurns(2);
        Assert.False(javelins.Alive);
        script.Walk("B", (9, 31));
        Assert.True(EnemyAi.CanHit(lance, script.Member("B"), lance.Weapon, script.Floor.Map.Grid));
        script.Look();
        string text = Save(script.Campaign);

        var closed = Load(text).Campaign.Visit!;
        var reloaded = new VisitScript(Load(text).Campaign);
        reloaded.Resume();
        StateAssert.Equivalent(closed.Current.Enemies, reloaded.Floor.Enemies, "rows");
        StateAssert.Equivalent(script.Floor.Enemies, reloaded.Floor.Enemies, "rows");
        Assert.False(reloaded.Row(0, 11).Alive);

        foreach (var side in new[] { script, reloaded })
        {
            var sideLance = side.Row(0, 10);
            int braces = 0;
            side.Turns.EnemyBraceTriggered += enemy => braces += ReferenceEquals(enemy, sideLance) ? 1 : 0;
            side.Walk("B", (9, 32));   // still inside
            Assert.Equal(0, braces);
            side.Walk("B", (11, 27));  // out...
            side.Walk("B", (9, 31));   // ...and back in
            Assert.Equal(1, braces);
        }
    }

    [Fact]
    public void OnlyASafePointIsCaptured()
    {
        // 1f: a save is taken between runs; right after an entry, with no floor
        // open; and in the player phase with nothing in flight. Never inside a raise
        // — a hit's cascade half-settled — and never while the enemies take their
        // turn, its banner included: the save holds none of their working state.
        var campaign = NewCampaign();
        Assert.True(SaveGame.IsSafePoint(campaign));
        var visit = campaign.Enter("spear_hall", FiveDeep);
        Assert.Null(visit.Turns);
        Assert.True(SaveGame.IsSafePoint(campaign));
        Assert.NotNull(SaveGame.Capture(campaign).Campaign.Visit);

        var script = new VisitScript(campaign);
        var turns = script.Open();
        Assert.True(SaveGame.IsSafePoint(campaign));
        Assert.NotNull(SaveGame.Capture(campaign).Campaign.Visit);

        // The enemies' turn: the lance seen far from anyone, so it walks.
        var lance = script.Row(10, "pinning_lance");
        turns.NotifyEnemyVisible(lance, true);
        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.False(SaveGame.IsSafePoint(campaign));
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(campaign));
        for (int step = 0; step < 600 && turns.Phase != TurnPhase.EnemyMoving; step++)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.EnemyMoving, turns.Phase);
        Assert.False(SaveGame.IsSafePoint(campaign));
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(campaign));
        for (int step = 0; step < 600 && turns.Phase != TurnPhase.Player; step++)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.True(SaveGame.IsSafePoint(campaign));

        // Inside a raise, in the player phase: a hit's own applier asks.
        bool? duringHit = null;
        Exception? refused = null;
        turns.EnemyHit += (_, _) =>
        {
            duringHit = SaveGame.IsSafePoint(campaign);
            refused = Record.Exception(() => SaveGame.Capture(campaign));
        };
        script.Walk("A", script.Beside(lance));
        script.Swing("A", lance);
        Assert.False(duringHit);
        Assert.IsType<InvalidOperationException>(refused);
        Assert.True(SaveGame.IsSafePoint(campaign));
    }

    [Fact]
    public void AVisitThatContradictsItselfIsRefused()
    {
        // A save never limps (1d): a visit that could not have been played is
        // refused whole, naming where, rather than restored into a state no play
        // reaches.
        string text = Save(Flagship().Campaign);
        (Action<JsonObject> Edit, string Where)[] impossible =
        [
            // The current floor unsaved: floors are adjacent, and every one down to the deepest is saved.
            (tree => ((JsonArray)VisitNode(tree)["floors"]!).RemoveAt(3), "campaign.visit.floors"),
            (tree => VisitNode(tree)["floorIndex"] = 5, "campaign.visit.floorIndex"),
            // Two boss rows on the bottom floor, and a boss above it.
            (tree => FloorNode(tree, 4)["enemies"]![0]!["boss"] = true, "campaign.visit.floors[4].enemies[38].boss"),
            (tree => FloorNode(tree, 2)["enemies"]![0]!["boss"] = true, "campaign.visit.floors[2].enemies[0].boss"),
            // A spoil in an empty slot, one in two places, and one named twice.
            (tree => VisitNode(tree)["spoils"]![0] = JsonNode.Parse("""{ "member": "C", "slot": 5 }"""), "campaign.visit.spoils[0].slot"),
            (tree => VisitNode(tree)["spoils"]![0] = JsonNode.Parse("""{ "member": "B", "slot": 1, "floor": 3, "loose": 0 }"""), "campaign.visit.spoils[0]"),
            (tree => VisitNode(tree)["spoils"]![1] = JsonNode.Parse("""{ "member": "B", "slot": 1 }"""), "campaign.visit.spoils[1]"),
            // A carry naming a missing spawn, a member among the seen, or another turn.
            (tree => VisitNode(tree)["carry"]!["seen"]![0] = JsonNode.Parse("""{ "floor": 3, "spawn": 99 }"""), "campaign.visit.carry.seen[0].spawn"),
            (tree => VisitNode(tree)["carry"]!["seen"]![0] = JsonNode.Parse("""{ "member": "A" }"""), "campaign.visit.carry.seen[0]"),
            (tree => VisitNode(tree)["carry"]!["turn"] = 9, "campaign.visit.carry.turn"),
            // A short fog row, and one holding something else.
            (tree => FloorNode(tree, 2)["seen"]![5] = "0101", "campaign.visit.floors[2].seen[5]"),
            (tree => FloorNode(tree, 2)["seen"]![5] = new string('x', MapGenerator.Cols), "campaign.visit.floors[2].seen[5]"),
            // Sight on a floor that is not current.
            (tree => FloorNode(tree, 1)["visible"] = FloorNode(tree, 3)["visible"]!.DeepClone(), "campaign.visit.floors[1].visible"),
            // A drop pinned under a living row, and under one whose drop was taken.
            (tree => FloorNode(tree, 3)["enemies"]![0]!["groundItem"] = FloorNode(tree, 3)["enemies"]![2]!["groundItem"]!.DeepClone(), "campaign.visit.floors[3].enemies[0].groundItem"),
            (tree => FloorNode(tree, 3)["enemies"]![1]!["groundItem"] = FloorNode(tree, 3)["enemies"]![2]!["groundItem"]!.DeepClone(), "campaign.visit.floors[3].enemies[1].groundItem"),
            // A spawn index twice on one floor, and a floor out of order.
            (tree => FloorNode(tree, 1)["enemies"]![1]!["spawnIndex"] = 0, "campaign.visit.floors[1].enemies[1].spawnIndex"),
            (tree => FloorNode(tree, 1)["index"] = 2, "campaign.visit.floors[1].index"),
            // A loose item on a wall: the map knows it once it is built.
            (tree => FloorNode(tree, 0)["looseItems"]![0]!["row"] = 0, "campaign.visit.floors[0]"),
            // The boss fallen on a turn the visit has not had, and the XP at the door of another party.
            (tree => VisitNode(tree)["bossDefeatedAtTurn"] = 11, "campaign.visit.bossDefeatedAtTurn"),
            (tree => VisitNode(tree)["entrySnapshot"]!["members"]![1]!["memberId"] = "Z", "campaign.visit.entrySnapshot.members[1].memberId"),
            (tree => VisitNode(tree)["dungeonId"] = "spear_hal", "campaign.visit.dungeonId"),
        ];
        foreach (var (edit, where) in impossible)
            Assert.Equal(where, RefusedAfter(text, edit).Where);
    }
}
