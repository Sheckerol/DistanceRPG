using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A visit played by script, with no scene: the moves a player makes — walking
/// somewhere, looking about, swinging, taking a drop, putting a weapon down,
/// taking the stairs, ending turns — on the campaign's own visit, through the
/// Logic calls the scene makes for them. Every actor is named by a stable id —
/// a member by its roster id, an enemy by its floor index and spawn index — and
/// never by an object, so one script plays alike on a campaign and on the
/// campaign a save brought back (Phase 5.6's round trips, and 5.7's play after a
/// reload). The shapes are <c>RunOutcomeTests</c>' and <c>DungeonStateTests</c>'.
/// <para>
/// <strong>What the scene does, the script does.</strong> A walk ends where it
/// ends and goes through <see cref="TurnSystem.NotifyCharacterMoved"/>, so the
/// threat zones stay true; looking lights the fog boxes every living member
/// stands in and tells the floor's system which rows are in sight
/// (<see cref="Look"/>, the scene's fog and enemy-visibility update); an arrival
/// lights only where the party stands (the scene's <c>RevealArrival</c>); a turn's
/// end resets the sight to where the party stands (the scene's
/// <c>ResetFogVisibility</c>). A blow the party's catalogue weapons could not
/// land in one swing is helped by setting the target's HP to 1 first — a save
/// names content by id, so the party holds catalogue weapons only.
/// </para>
/// </summary>
internal sealed class VisitScript
{
    /// <summary>A roll that neither crits nor fumbles.</summary>
    public static int Ten() => 10;

    /// <summary>The simulated seconds a turn's end is given to reach the next player phase, at the suite's 1/30 s step.</summary>
    private const float TurnSeconds = 20f;

    private const float Step = 1f / 30f;

    /// <param name="campaign">The campaign whose visit the script plays.</param>
    /// <param name="die">The d20 every floor the script opens rolls; <see cref="Ten"/> when omitted.</param>
    /// <param name="visitsOwnDie">
    /// Open every floor with no die at all, so it rolls the visit's own
    /// (<see cref="DungeonState.RollD20"/>) — the die a save keeps the position of
    /// (Phase 5.7). Not beside <paramref name="die"/>.
    /// </param>
    public VisitScript(CampaignState campaign, Func<int>? die = null, bool visitsOwnDie = false)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (visitsOwnDie && die is not null)
            throw new ArgumentException("A script's floors roll an injected die or the visit's own, never both.", nameof(die));
        Campaign = campaign;
        Die = visitsOwnDie ? null : die ?? Ten;
    }

    public CampaignState Campaign { get; }

    /// <summary>The d20 every floor this script opens rolls, handed to <see cref="DungeonState.OpenTurns"/>: null hands none, and the floor rolls the visit's own.</summary>
    public Func<int>? Die { get; }

    public DungeonState Visit => Campaign.Visit ?? throw new InvalidOperationException("No visit is in progress.");

    public TurnSystem Turns => Visit.Turns ?? throw new InvalidOperationException("No floor's system is open.");

    public Floor Floor => Visit.Current;

    public IReadOnlyList<PartyMemberState> Party => Campaign.Party;

    /// <summary>The member with roster id <paramref name="id"/>.</summary>
    public PartyMemberState Member(string id) => Party.Single(member => member.Id == id);

    /// <summary>The row with spawn index <paramref name="spawn"/> on floor index <paramref name="floor"/>.</summary>
    public EnemyState Row(int floor, int spawn) => Visit.Floors[floor].Enemies.Single(row => row.SpawnIndex == spawn);

    /// <summary>The row with spawn index <paramref name="spawn"/> on the current floor, pinned to the weapon the seed gave it before anything leans on it.</summary>
    public EnemyState Row(int spawn, string weaponId)
    {
        var row = Row(Visit.FloorIndex, spawn);
        Assert.Equal((spawn, weaponId), (row.SpawnIndex, row.Weapon.Id));
        return row;
    }

    // ── Entering, and opening a floor ────────────────────────────────────────

    /// <summary>Enter <paramref name="dungeonId"/> on <paramref name="seed"/>, open its first floor and arrive on it.</summary>
    public static VisitScript Enter(CampaignState campaign, string dungeonId, long seed, Func<int>? die = null, bool visitsOwnDie = false)
    {
        campaign.Enter(dungeonId, seed);
        var script = new VisitScript(campaign, die, visitsOwnDie);
        script.Open();
        return script;
    }

    /// <summary>
    /// Open the floor the visit stands on, as the scene does on every arrival
    /// after the stairs, and light it where the party stands.
    /// </summary>
    public TurnSystem Open()
    {
        var turns = Visit.OpenTurns(Party, Die);
        Arrive();
        return turns;
    }

    /// <summary>
    /// Open the floor a load brought back — its system imports the saved
    /// counters and catches the floor up, as an arrival's does — keeping the
    /// sight the save kept rather than lighting it afresh: what the party could
    /// see this turn is part of the turn (Phase 5.8's resume keeps it too).
    /// </summary>
    public TurnSystem Resume() => Visit.OpenTurns(Party, Die);

    // ── Moving ───────────────────────────────────────────────────────────────

    /// <summary>Put <paramref name="actor"/> on <paramref name="tile"/>'s centre, as a walk ending there would.</summary>
    public static void Put(ActorState actor, (int R, int C) tile) => (actor.X, actor.Y) = Displacer.CentreOf(tile.R, tile.C);

    /// <summary>Member <paramref name="id"/> walks to <paramref name="tile"/>: the move goes through the one move path, so every zone its end crosses answers.</summary>
    public void Walk(string id, (int R, int C) tile)
    {
        var member = Member(id);
        Put(member, tile);
        Turns.NotifyCharacterMoved(member);
    }

    /// <summary>The party walks to stand round <paramref name="tile"/> — around living enemies, never on them — each member in roster order.</summary>
    public void Gather((int R, int C) tile)
    {
        var taken = Floor.Enemies.Where(row => row.Alive).Select(Displacer.TileOf).ToHashSet();
        var tiles = GridUtils.FindPartySpawnTiles(Floor.Map.Grid, tile.R, tile.C, Party.Count, taken);
        for (int i = 0; i < Party.Count; i++)
        {
            Put(Party[i], tiles[i]);
            if (Party[i].Alive)
                Turns.NotifyCharacterMoved(Party[i]);
        }
    }

    /// <summary>Back to the floor's arrival room, where the placer puts nobody: where the party waits turns out.</summary>
    public void Home() => Gather(Floor.UpStairs);

    /// <summary>A free floor tile beside <paramref name="target"/>: left, right, above, below.</summary>
    public (int R, int C) Beside(EnemyState target)
    {
        var (r, c) = Displacer.TileOf(target);
        var taken = Floor.Enemies.Where(row => row.Alive).Select(Displacer.TileOf).ToHashSet();
        foreach (var tile in new[] { (R: r, C: c - 1), (R: r, C: c + 1), (R: r - 1, C: c), (R: r + 1, C: c) })
            if (Floor.Map.Grid[tile.R, tile.C] == 0 && !taken.Contains(tile))
                return tile;
        throw new Xunit.Sdk.XunitException($"No free floor tile beside the enemy at {(r, c)}.");
    }

    // ── Looking ──────────────────────────────────────────────────────────────

    /// <summary>
    /// What the scene does when a member steps onto a tile: light every fog box
    /// a living member stands in (the explored memory with it), then tell the
    /// floor's system which rows stand on a lit tile — a row in sight is seen
    /// this turn, which arms its zone (no ambushes from the fog).
    /// </summary>
    public void Look()
    {
        var floor = Floor;
        foreach (var member in Party)
        {
            if (!member.Alive) continue;
            var (r, c) = Displacer.TileOf(member);
            floor.Fog.RevealAt(r, c, floor.FogBoxes.FogBoxes);
        }
        foreach (var row in floor.Enemies)
        {
            var (r, c) = Displacer.TileOf(row);
            Turns.NotifyEnemyVisible(row, floor.Fog.Visible[r, c]);
        }
    }

    /// <summary>An arrival, as the scene makes one: whatever the floor could see when the party last stood on it is dark, and it is lit where the party now stands.</summary>
    private void Arrive()
    {
        Floor.Fog.ResetVisibility();
        Look();
    }

    // ── Fighting ─────────────────────────────────────────────────────────────

    /// <summary>Member <paramref name="id"/> swings at <paramref name="target"/> from where it stands, and the swing is allowed.</summary>
    public void Swing(string id, EnemyState target) => Assert.True(Turns.TryAttack(Member(id), target), $"{id} could not swing at spawn {target.SpawnIndex}");

    /// <summary>
    /// Member <paramref name="id"/> walks up beside <paramref name="target"/> and
    /// puts it down with one swing: the target is at 1 HP first, since what the
    /// roster holds is catalogue weapons and a farm's HP is not what this is about.
    /// </summary>
    public void Kill(string id, EnemyState target)
    {
        Walk(id, Beside(target));
        target.Hp = 1;
        Swing(id, target);
        Assert.False(target.Alive, $"spawn {target.SpawnIndex} still stands");
    }

    // ── Taking and leaving ───────────────────────────────────────────────────

    /// <summary>Member <paramref name="id"/> takes what <paramref name="corpse"/> lies on into its bag, a spoil of the visit; the slot it landed in.</summary>
    public int Take(string id, EnemyState corpse)
    {
        var drop = Turns.GroundItemOf(corpse) ?? throw new Xunit.Sdk.XunitException($"Spawn {corpse.SpawnIndex} lies on nothing.");
        int slot = Pickup.Take(Campaign, corpse, drop, Member(id), Visit);
        Assert.NotEqual(Pickup.BagFull, slot);
        return slot;
    }

    /// <summary>Member <paramref name="id"/> puts down the weapon in bag slot <paramref name="slot"/> where it stands.</summary>
    public LooseItem Leave(string id, int slot) => Floor.Leave(Member(id), slot);

    /// <summary>Member <paramref name="id"/> takes <paramref name="item"/> back off the current floor; the slot it landed in.</summary>
    public int TakeLoose(string id, LooseItem item)
    {
        int slot = Pickup.TakeLoose(Campaign, Floor, item, Member(id));
        Assert.NotEqual(Pickup.BagFull, slot);
        return slot;
    }

    // ── The stairs ───────────────────────────────────────────────────────────

    /// <summary>Gather at the down-stairs, take them, and open the floor below.</summary>
    public TurnSystem Down()
    {
        Gather(Floor.DownStairs!.Value);
        Visit.Descend(Party);
        return Open();
    }

    /// <summary>Gather at the up-stairs, take them, and open the floor above.</summary>
    public TurnSystem Up()
    {
        Gather(Floor.UpStairs);
        Visit.Ascend(Party);
        return Open();
    }

    // ── Turns ────────────────────────────────────────────────────────────────

    /// <summary>
    /// End the turn and run the enemies' to the next player phase, at the
    /// suite's 1/30 s step unless another is named — the logic clock's own
    /// (<see cref="SaveGame.LogicStepSeconds"/>) for a script that must play as
    /// the game plays. The turn's end resets the sight to where the party
    /// stands, as the scene's does.
    /// </summary>
    public void EndTurn(float step = Step)
    {
        var turns = Turns;
        turns.EndTurn();
        Floor.Fog.ResetVisibility();
        Look();
        for (float t = 0f; t < TurnSeconds && turns.Phase is not (TurnPhase.Player or TurnPhase.GameOver); t += step)
            turns.Update(step);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    /// <summary>End <paramref name="count"/> turns, each one run to the next player phase.</summary>
    public void PassTurns(int count)
    {
        for (int i = 0; i < count; i++)
            EndTurn();
    }
}
