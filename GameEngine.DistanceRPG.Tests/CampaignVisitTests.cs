using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The campaign and the visit it holds (§6.7, §4.1): the party and what it has
/// met are the permanent half and outlive every floor and every visit; the
/// visit is the renewable half — built on entry from the caller's seed, dropped
/// whole on leaving, and regenerated fresh on the next entry, so no farm ever
/// outlives the visit it was built in.
/// </summary>
public class CampaignVisitTests
{
    /// <summary>The shipped seed: the tutorial's first floor on it is the golden dungeon.</summary>
    private const long GoldenSeed = 2762136374;

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    /// <summary>Four fixture members in roster order: the first holding <paramref name="first"/>, the rest a Weakspot Stiletto.</summary>
    private static PartyMemberState[] Party(Weapon? first = null)
        => Ids.Select((id, i) => TestPools.Holding(id, i == 0 && first is not null ? first : TestWeapons.Get("weakspot_stiletto"), colorIndex: i))
            .ToArray();

    private static string GridText(int[,] grid)
    {
        var chars = new char[grid.Length];
        int i = 0;
        foreach (int v in grid) chars[i++] = (char)('0' + v);
        return new string(chars);
    }

    private static IEnumerable<(float X, float Y, string Weapon, int SpawnIndex)> Rows(Floor floor)
        => floor.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex));

    /// <summary>Walk the party to <paramref name="stair"/> without the walk, around living enemies.</summary>
    private static void Gather(DungeonState visit, IReadOnlyList<PartyMemberState> party, (int R, int C) stair)
    {
        var floor = visit.Current;
        var blocked = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, stair.R, stair.C, party.Count, blocked);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = (tiles[i].C * GameConstants.Tile + GameConstants.Tile / 2f, tiles[i].R * GameConstants.Tile + GameConstants.Tile / 2f);
    }

    /// <summary>Stand <paramref name="actor"/> on a free floor tile beside <paramref name="target"/>.</summary>
    private static void StandBeside(Floor floor, ActorState actor, EnemyState target)
    {
        var (r, c) = Displacer.TileOf(target);
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        foreach (var tile in new[] { (R: r, C: c - 1), (R: r, C: c + 1), (R: r - 1, C: c), (R: r + 1, C: c) })
        {
            if (floor.Map.Grid[tile.R, tile.C] != 0 || taken.Contains(tile)) continue;
            (actor.X, actor.Y) = Displacer.CentreOf(tile.R, tile.C);
            return;
        }
        Assert.Fail($"No free floor tile beside the dummy at {(r, c)}.");
    }

    [Fact]
    public void StartBuildsTheRosterOnce_AndMeetsTheLoadout()
    {
        // The campaign starts with the roster built once, in file order — each
        // member off its row, colour by position — and everything they carry met
        // at once (§3.3: the loadout enters the party's hands the moment it is
        // handed over). Every visit is made by these same members.
        var content = GameContent.Current;
        var campaign = CampaignState.Start(content);
        var roster = content.Party.All;

        Assert.Null(campaign.Visit);
        Assert.Equal(GameConstants.PartySize, campaign.Party.Count);
        for (int i = 0; i < roster.Count; i++)
        {
            var (member, def) = (campaign.Party[i], roster[i]);
            Assert.Equal((def.Id, i, def.Stats), (member.Id, member.ColorIndex, member.Stats));
            Assert.Equal(def.StartingWeaponId, member.EquippedWeapon!.Id);
            Assert.Equal(def.BagWeaponIds, member.Inventory.Skip(1).Take(def.BagWeaponIds.Count).Select(w => w!.Id));
            Assert.All(member.Inventory.Skip(1 + def.BagWeaponIds.Count), Assert.Null);
            Assert.Equal((content.Tuning.StartingPool, 0, 0), (member.MaxHp, member.HpXp, member.ManaXp));   // nothing earned yet
        }

        // Met: every entry the loadout carries, and nothing else.
        var carried = campaign.Party
            .SelectMany(m => m.Inventory)
            .OfType<Weapon>()
            .SelectMany(w => w.Enchantments.Select(e => e.Id))
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(carried);
        Assert.Equal(carried, campaign.SeenInOrder);
        var fromTheRoster = roster
            .SelectMany(def => def.BagWeaponIds.Prepend(def.StartingWeaponId))
            .SelectMany(id => TestWeapons.Get(id).Enchantments.Select(e => e.Id))
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal);
        Assert.Equal(fromTheRoster, campaign.SeenInOrder);

        // Once: the same members, whatever visits come and go.
        var members = campaign.Party.ToArray();
        var weapons = members.Select(m => m.EquippedWeapon).ToArray();
        campaign.Enter("tutorial", GoldenSeed);
        campaign.Abandon();
        campaign.Enter("tutorial", 1);
        Assert.Equal(members.Length, campaign.Party.Count);
        for (int i = 0; i < members.Length; i++)
        {
            Assert.Same(members[i], campaign.Party[i]);
            Assert.Same(weapons[i], campaign.Party[i].EquippedWeapon);
        }
    }

    [Fact]
    public void StartTakesOnlyTheCurrentContent()
    {
        // A member is built under the current content — its weapons out of the
        // current catalogue, its bag sized by the current tuning — so a roster
        // read from any other content would be one content's rows carrying
        // another's weapons. The campaign refuses to start that party rather than
        // build it half and half.
        var other = GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, ContentDefaults.Weapons);
        Assert.NotSame(GameContent.Current, other);
        Assert.Throws<ArgumentException>(() => CampaignState.Start(other));
        Assert.Throws<ArgumentNullException>(() => CampaignState.Start(null!));
        Assert.Equal(GameConstants.PartySize, CampaignState.Start(GameContent.Current).Party.Count);
    }

    [Fact]
    public void TheSeenSetOutlivesEveryFloorAndEveryVisit()
    {
        // open-questions.md's "Where CampaignState lives", resolved: the seen set
        // is campaign-wide. An entry met on a floor is still met on the next
        // floor, after the party walks out, and on every visit after — §4.1's
        // reset is the dungeon's and never touches what the campaign has met.
        var campaign = CampaignState.Start(GameContent.Current);
        var loadout = campaign.SeenInOrder.ToArray();
        Assert.DoesNotContain("cold", loadout);
        Assert.DoesNotContain("shocking", loadout);

        var visit = campaign.Enter("tutorial", GoldenSeed);
        var party = campaign.Party;
        visit.OpenTurns(party, () => 10);

        // Picked up on the first floor: met.
        var corpse = visit.Current.Enemies[0];
        corpse.Alive = false;
        var cold = TestWeapons.Get("wand_of_the_nova", DamageType.Cold);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(campaign, corpse, new Drop(cold, 0, false), party[0]));
        Assert.True(campaign.HasSeen("cold"));

        // Down a floor: still met, and a second entry met there.
        Gather(visit, party, visit.Current.DownStairs!.Value);
        visit.Descend(party);
        visit.OpenTurns(party, () => 10);
        Assert.True(campaign.HasSeen("cold"));
        var below = visit.Current.Enemies[0];
        below.Alive = false;
        var shocking = TestWeapons.Get("wand_of_the_nova", DamageType.Shocking);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(campaign, below, new Drop(shocking, 0, false), party[1]));
        var met = campaign.SeenInOrder.ToArray();
        Assert.Contains("cold", met);
        Assert.Contains("shocking", met);

        // Out of the dungeon: the visit is gone, the met set is not.
        campaign.Abandon();
        Assert.Null(campaign.Visit);
        Assert.Equal(met, campaign.SeenInOrder);

        // A new visit, every floor fresh, meets nothing again: it is already met.
        campaign.Enter("tutorial", 1);
        Assert.Equal(met, campaign.SeenInOrder);
        Assert.Equal(loadout.Length + 2, met.Length);
    }

    [Fact]
    public void LeavingDiscardsTheVisit_AndReEntryRegeneratesEveryFloorFresh()
    {
        // PHASE4-004, -006, -007: leaving discards all of it. The campaign holds
        // one visit and never two, dropped whole on leaving, so only the current
        // visit's floors are ever held — the save cannot grow without bound — and
        // a re-entry regenerates every floor from scratch: on a new seed new
        // maps, and on the very same seed the same maps with every row fresh. A
        // dummy's DefeatCount never survives an exit, so a farm is capped at one
        // visit.
        var campaign = new CampaignState(Party(TestWeapons.Make("Maul", range: 60, damage: 60, cost: 30)));
        var party = campaign.Party;
        var first = campaign.Enter("tutorial", GoldenSeed);
        Assert.Same(first, campaign.Visit);

        // Farm a dummy — a clean kill, two cycles on its count — and explore, on the first floor.
        var turns = first.OpenTurns(party, () => 10);
        var farmed = first.Current.Enemies[23];
        Assert.Equal((23, "hatchet"), (farmed.SpawnIndex, farmed.Weapon.Id));
        StandBeside(first.Current, party[0], farmed);
        Assert.True(turns.TryAttack(party[0], farmed));
        Assert.Equal((false, 2), (farmed.Alive, farmed.DefeatCount));
        var start = first.Current.UpStairs;
        first.Current.Fog.RevealAt(start.R, start.C, first.Current.FogBoxes.FogBoxes);
        Assert.True(first.Current.Fog.Seen[start.R, start.C]);

        // Never two visits: the one in progress has to be left first.
        Assert.Throws<InvalidOperationException>(() => campaign.Enter("tutorial", 1));
        Assert.Same(first, campaign.Visit);

        campaign.Abandon();
        Assert.Null(campaign.Visit);
        campaign.Abandon();   // nothing to leave between runs
        Assert.Null(campaign.Visit);

        // A new seed: a new dungeon, nothing of the old one in it.
        var second = campaign.Enter("tutorial", 1);
        Assert.Same(second, campaign.Visit);
        Assert.NotEqual(GridText(first.Floors[0].Map.Grid), GridText(second.Floors[0].Map.Grid));
        Assert.Empty(second.Floors.Intersect(first.Floors));
        Assert.Empty(second.Floors.SelectMany(f => f.Enemies).Intersect(first.Floors.SelectMany(f => f.Enemies)));
        AssertFresh(second);

        // The same seed: the same maps regenerated, and every row fresh — the
        // farmed dummy's twin standing, at zero, and the fog dark.
        campaign.Abandon();
        var again = campaign.Enter("tutorial", GoldenSeed);
        Assert.Equal(first.BossFloor, again.BossFloor);
        for (int n = 0; n < again.Floors.Length; n++)
        {
            Assert.NotSame(first.Floors[n], again.Floors[n]);
            Assert.Equal(GridText(first.Floors[n].Map.Grid), GridText(again.Floors[n].Map.Grid));
        }
        var twin = again.Floors[0].Enemies[23];
        Assert.NotSame(farmed, twin);
        Assert.Equal((farmed.SpawnIndex, farmed.Weapon.Id), (twin.SpawnIndex, twin.Weapon.Id));
        AssertFresh(again);
        Assert.Equal(0, again.Turn);
    }

    /// <summary>A visit nobody has touched: every row standing, unfarmed and untaken, and every floor's fog dark.</summary>
    private static void AssertFresh(DungeonState visit)
    {
        Assert.All(visit.Floors.SelectMany(f => f.Enemies), e =>
        {
            Assert.True(e.Alive);
            Assert.Equal((0, 0, 0, 0, -1, false), (e.DefeatCount, e.RevivalsRolled, e.RevivalDamage, e.RevivalMaxHp, e.DefeatedAtTurn, e.DropTaken));
            Assert.Equal(e.MaxHp, e.Hp);
        });
        Assert.All(visit.Floors, f => Assert.DoesNotContain(true, f.Fog.Seen.Cast<bool>()));
        Assert.Equal((0, 0, false), (visit.FloorIndex, visit.DeepestFloorIndex, visit.BossDefeated));
    }

    [Fact]
    public void EnteringRestsTheParty()
    {
        // The party walks into every dungeon whole: alive, HP and mana full,
        // every status shed, a turn's budget with nothing banked — taken at the
        // entry and not when the last run ended, so a run's end shows the fallen
        // as fallen. The permanent half is untouched: what they carry, what they
        // have learned.
        var campaign = new CampaignState(Party());
        var (a, b) = (campaign.Party[0], campaign.Party[1]);
        a.Inventory[1] = TestWeapons.Get("staff_of_renewal");
        a.WeaponXp[WeaponClass.Dagger] = 40;
        a.HpXp += 7;
        a.ManaXp += 9;
        var visit = campaign.Enter("tutorial", GoldenSeed);
        visit.OpenTurns(campaign.Party, () => 10);

        // A run's wear on them.
        a.Hp = 12;
        a.Mana = 5;
        a.ApplyStatus(StatusEffectType.Poison, null, 4);
        a.DistLeft = 3f;
        a.SavedMovement = 40f;
        a.EffectiveMax = 200f;
        a.HeldShots = 1;
        b.Alive = false;
        b.Hp = 0;

        // Leaving rests nobody.
        campaign.Abandon();
        Assert.Equal((12, false), (a.Hp, b.Alive));

        var kept = campaign.Party.Select(m => (m.HpXp, m.ManaXp, m.WeaponXp[WeaponClass.Dagger], string.Join(",", m.Inventory.Select(w => w?.Id)))).ToArray();
        campaign.Enter("tutorial", 1);

        Assert.All(campaign.Party, m =>
        {
            Assert.True(m.Alive);
            Assert.Equal(m.MaxHp, m.Hp);
            Assert.Equal(m.UsableMaxMana, m.Mana);
            Assert.Empty(m.StatusEffects);
            Assert.Equal((GameConstants.MaxDistance, GameConstants.MaxDistance, 0f, 0), (m.DistLeft, m.EffectiveMax, m.SavedMovement, m.HeldShots));
        });
        Assert.Equal(TestPools.FixtureHp, a.Hp);
        Assert.Equal(kept, campaign.Party.Select(m => (m.HpXp, m.ManaXp, m.WeaponXp[WeaponClass.Dagger], string.Join(",", m.Inventory.Select(w => w?.Id)))));
    }

    [Fact]
    public void TheSeedIsTheCallersAndTheSameSeedIsTheSameDungeon()
    {
        // The campaign rolls nothing: the seed is whoever enters', drawn per
        // entry — which is what re-rolls a failed dungeon's depth — and handed on
        // untouched, so the same seed is the same dungeon on every floor, on two
        // campaigns as on one, and the shipped seed still reaches the shipped map.
        const long Seed = 1234567;
        var hall = GameContent.Current.Dungeons["spear_hall"];
        var one = new CampaignState(Party()).Enter("spear_hall", Seed);
        var two = new CampaignState(Party()).Enter("spear_hall", Seed);

        Assert.Equal((Seed, Seed), (one.MapSeed, two.MapSeed));
        Assert.Equal(DungeonState.RollBossFloor(Seed, hall), one.BossFloor);
        Assert.Equal(one.BossFloor, two.BossFloor);
        for (int n = 0; n < one.Floors.Length; n++)
        {
            Assert.NotSame(one.Floors[n], two.Floors[n]);
            Assert.Equal(GridText(one.Floors[n].Map.Grid), GridText(two.Floors[n].Map.Grid));
            Assert.Equal(Rows(one.Floors[n]), Rows(two.Floors[n]));
            Assert.Equal(
                (one.Floors[n].UpStairs, one.Floors[n].DownStairs, one.Floors[n].Lair),
                (two.Floors[n].UpStairs, two.Floors[n].DownStairs, two.Floors[n].Lair));
        }

        // Another seed, another dungeon.
        var other = new CampaignState(Party()).Enter("spear_hall", 7654321);
        Assert.NotEqual(GridText(one.Floors[0].Map.Grid), GridText(other.Floors[0].Map.Grid));

        // And the shipped seed's first floor is the golden dungeon, row for row.
        var golden = GoldenData.Instance;
        var shipped = new CampaignState(Party()).Enter("tutorial", golden.MapSeed);
        var map = shipped.Floors[0].Map;
        for (int r = 0; r < MapGenerator.Rows; r++)
        {
            var row = new char[MapGenerator.Cols];
            for (int c = 0; c < MapGenerator.Cols; c++)
                row[c] = map.Grid[r, c] == 1 ? '1' : '0';
            Assert.Equal(golden.GridRows[r], new string(row));
        }
        Assert.Equal((golden.PlayerStart[0], golden.PlayerStart[1]), map.PlayerStart);
    }
}
