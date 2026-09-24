using GameEngine.Core;
using GameEngine.Core.UI;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Screen-space HUD for the dungeon scene, drawn with the engine's bitmap
/// <see cref="TextRenderer"/>: movement/weapon readouts, the floor line, the
/// party selector, the enemy nameplates — a boss's among them — and their effect
/// badges, the stairs' labels and the cue at the one underfoot, the click and aim
/// cues, floating combat text, the end-of-turn banner, the inventory panel, the
/// pause menu and the way out's prompt with what leaving now means, the stairs'
/// fade and the banner over it (the floor reached, the trade on the boss floor,
/// the boss's fall), and the run's end — its summary, over the defeat screen when
/// the party wiped. Every number it shows is a resolved one — a swing's cost
/// after Light, a cast's mana after Resonant, a Block as the points it absorbs,
/// a crit window as the roll it needs, Mire as the budget it leaves — never a
/// percentage, a stack count or a base with a discount (§1.1, §1.7); and nothing
/// it shows names how deep the dungeon goes before the party has reached the
/// bottom (§4.3).
/// </summary>
public sealed class DungeonHud
{
    private const float FloatingTextLife = 1.2f;
    private const float FloatingTextRise = 50f;

    /// <summary>How long the arrival banner stays up, and the last stretch of it over which it fades.</summary>
    private const float BannerSeconds = 2.4f;
    private const float BannerFadeSeconds = 0.8f;

    /// <summary>How long a banner with lines under it stays up — the boss floor's trade, the boss's fall: long enough to read them.</summary>
    private const float WarningBannerSeconds = 5f;

    /// <summary>Pixels a character advances at scale 1: the 5-px glyph and its 1-px gap.</summary>
    private const float GlyphAdvance = 6f;

    /// <summary>Spaces between two runs drawn side by side.</summary>
    private const int RunGap = 2;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Yellow = new(1f, 0.87f, 0f, 1f);
    private static readonly Vector4 Grey = new(0.6f, 0.6f, 0.65f, 1f);
    private static readonly Vector4 DarkGrey = new(0.4f, 0.4f, 0.42f, 1f);
    private static readonly Vector4 Red = new(1f, 0.27f, 0.27f, 1f);
    private static readonly Vector4 Orange = new(0.96f, 0.65f, 0.14f, 1f);
    private static readonly Vector4 Cyan = new(0.31f, 0.76f, 0.97f, 1f);
    private static readonly Vector4 Green = new(0.27f, 0.87f, 0.47f, 1f);

    /// <summary>Where a farmed count's colour ends up (<see cref="ThreatColor"/>): a red past the hit colour's, the deepest farm on the floor.</summary>
    private static readonly Vector4 DeepRed = new(1f, 0.12f, 0.1f, 1f);

    /// <summary>A boss's name over its plate: the pale of the stone its golem is cut from, and no other enemy's colour.</summary>
    private static readonly Vector4 Stone = new(0.87f, 0.85f, 0.77f, 1f);

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private readonly FullscreenFade _fade = new();
    private bool _glReady;

    /// <summary>One coloured piece of HUD text; runs drawn side by side read as one line.</summary>
    public readonly record struct Run(string Text, Vector4 Color);

    private sealed record FloatingText(Vector3 WorldPos, IReadOnlyList<Run> Runs, float PixelYOffset)
    {
        public float Age;
    }

    private readonly List<FloatingText> _floatingTexts = new();

    /// <summary>
    /// A banner across the upper third of the screen: a small line over it, the
    /// line itself, any lines of warning under it, and how long it stays up.
    /// </summary>
    private sealed record Banner(string Over, string Main, IReadOnlyList<string> Under, float Seconds);

    // The banner up now (ShowArrival, ShowBossFall), and how long it has been up —
    // taken down early (HideArrival) the moment the party starts off the floor it
    // speaks of.
    private Banner? _banner;
    private float _bannerAge;

    public DungeonHud(Game game)
    {
        _game = game;
    }

    /// <summary>
    /// Put the arrival banner up for the floor <paramref name="visit"/> now
    /// stands on — <c>FLOOR 2</c>, the dungeon's name over it — for
    /// <see cref="BannerSeconds"/>. Drawn over the stairs' fade, so it is there
    /// at black and the floor comes up under it. On the boss floor, with the boss
    /// still standing, the trade is stated under it before the fight
    /// (<see cref="BossFloorBanner"/>), and it stays up long enough to read.
    /// </summary>
    public void ShowArrival(DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var trade = BossFloorBanner(visit);
        _banner = new Banner(visit.Def.Name.ToUpperInvariant(), ArrivalBanner(visit), trade,
            trade.Count > 0 ? WarningBannerSeconds : BannerSeconds);
        _bannerAge = 0f;
    }

    /// <summary>
    /// The boss is down (§4.3): the banner says who fell and what that means —
    /// nothing revives, and the way out is back up through everything farmed
    /// (<see cref="BossFalls"/>) — and what it left lying there, forged with its
    /// dungeon's theme (<see cref="BossDropLine"/>), when its drop is down.
    /// </summary>
    /// <param name="boss">The boss that fell.</param>
    /// <param name="drop">What it left on the floor, or null when there is nothing to name.</param>
    public void ShowBossFall(EnemyState boss, Drop? drop = null)
    {
        ArgumentNullException.ThrowIfNull(boss);
        var (headline, aftermath) = BossFalls(boss);
        IReadOnlyList<string> under = drop is null ? [aftermath] : [aftermath, BossDropLine(drop, boss.Theme)];
        _banner = new Banner("", headline, under, WarningBannerSeconds);
        _bannerAge = 0f;
    }

    /// <summary>
    /// Take the banner down before its time — the arrival's, or the boss's fall:
    /// the party is starting off the floor it speaks of, down or up the stairs or
    /// out by the way out. The banner is drawn over the stairs' fade, which is
    /// what puts the next floor's up at black; left up, the floor being left would
    /// go on naming itself on that black — until the swap on the stairs, and
    /// through the whole fade to the next visit on the way out. The next
    /// <see cref="ShowArrival"/> puts one up as ever.
    /// </summary>
    public void HideArrival() => _banner = null;

    /// <summary>Drop every floating text in the air: they hang over the floor being left, and nothing on the next floor is where they point.</summary>
    public void ClearFloatingTexts() => _floatingTexts.Clear();

    /// <summary>Spawn combat text above a world position, rising and fading out.</summary>
    public void AddFloatingText(Vector3 worldPos, string text, Vector4 color, float pixelYOffset = 0f)
        => _floatingTexts.Add(new FloatingText(worldPos, [new Run(text, color)], pixelYOffset));

    /// <summary>
    /// Spawn one line of differently coloured pieces above a world position,
    /// laid out left to right and centred as a group — a hit's number with
    /// what blocked it on one side and what warded it on the other.
    /// </summary>
    public void AddFloatingRow(Vector3 worldPos, float pixelYOffset, IReadOnlyList<Run> runs)
    {
        if (runs.Count > 0)
            _floatingTexts.Add(new FloatingText(worldPos, runs.ToArray(), pixelYOffset));
    }

    public void Update(float deltaTime)
    {
        for (int i = _floatingTexts.Count - 1; i >= 0; i--)
        {
            _floatingTexts[i].Age += deltaTime;
            if (_floatingTexts[i].Age >= FloatingTextLife)
                _floatingTexts.RemoveAt(i);
        }

        if (_banner is not null)
        {
            _bannerAge += deltaTime;
            if (_bannerAge >= _banner.Seconds)
                _banner = null;
        }
    }

    public void Render(Camera camera, DungeonScene scene)
    {
        if (!_glReady)
        {
            _text.Initialize();
            _fade.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        var turns = scene.Turns;
        var active = scene.ActiveCharacter.State;
        var ended = scene.ActiveMenu == GameMenu.RunSummary ? scene.EndedRun : null;

        // Dim layers go first so all text draws on top of them.
        if (turns.Phase == TurnPhase.GameOver)
            _fade.Draw(_game.ShaderManager, 0.7f);
        else if (scene.AnyMenuOpen)
            _fade.Draw(_game.ShaderManager, 0.65f);

        DrawTopReadouts(w, active, turns);
        DrawFloorLine(scene.Visit);
        DrawPartySelector(scene);
        if (ended is null)
        {
            // The world's own labels, which a run's summary is drawn clear of:
            // the run they label is over.
            DrawEnemyLabels(camera, scene, w, h);
            DrawStairs(camera, scene, w, h);
            DrawGroundItems(camera, scene, w, h);
            DrawAllyCue(camera, scene, w, h);
            DrawFloatingTexts(camera, w, h);
            DrawAimCue(scene);
        }

        if (turns.Phase == TurnPhase.TurnEnding)
        {
            DrawCentered(w, h / 2f - 40f, "END OF TURN!", 4f, Orange);
            if (scene.LastBankedMovement > 0f)
                DrawCentered(w, h / 2f + 8f, $"+{scene.LastBankedMovement:0} SAVED", 2.5f, White);
        }

        if (scene.InventoryOpen)
            DrawInventory(w, h, active, turns);

        if (scene.PauseMenuOpen)
            DrawMenu(w, h);

        if (scene.ActiveMenu == GameMenu.ConfirmLeave)
            DrawConfirmLeave(w, h, scene.Visit);

        if (ended is not null)
        {
            DrawRunSummary(w, h, ended, scene.NextRunName);   // a wipe's included: its headline is the defeat
        }
        else if (turns.Phase == TurnPhase.GameOver)
        {
            DrawCentered(w, h / 2f - 60f, DefeatHeadline, 6f, Red);
            DrawCentered(w, h / 2f + 20f, ContinueHint, 2f, Grey);
        }
        else
        {
            DrawCentered(w, h - 24f, HelpLine(active), 1.2f, Grey);
        }

        _text.Flush(_game.ShaderManager);

        // The stairs' fade goes over everything drawn so far, the world and the
        // HUD alike — the floor line changes at black, never in view — and the
        // banner over the fade, so the arrival's is up at black and the new floor
        // comes in under it. Only ever the new floor's: the banner of the floor
        // being left came down when its fade began (HideArrival).
        _fade.Draw(_game.ShaderManager, scene.TransitFade);
        DrawBanner(scene, w, h);
        _text.Flush(_game.ShaderManager);
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private void DrawTopReadouts(int w, PartyMemberState active, TurnSystem turns)
    {
        // Mire deep enough to take the whole budget is paralysis, called out beside the budget it emptied.
        var budget = new List<Run> { new($"[{active.Id}] MOVE: {MathF.Ceiling(active.DistLeft)} / {active.EffectiveMax:0}", White) };
        if (IsParalysed(active))
            budget.Add(new Run("PARALYSED", Red));
        budget.Add(new Run($"  {ManaReadout(active)}", White));
        DrawRunsCenteredAt(w / 2f, 14f, 2f, budget);

        var weapon = active.EquippedWeapon;
        if (weapon == null)
            DrawCentered(w, 42f, $"[{active.Id}] (NO WEAPON)", 1.5f, Yellow);
        else
            DrawRunsCenteredAt(w / 2f, 42f, 1.5f, WeaponRuns($"[{active.Id}] ", weapon, active, turns.AttacksThisTurn(active), Yellow));
    }

    /// <summary>
    /// The budget line's mana: what is in the pool, over the ceiling a spend can
    /// actually reach. Where nothing is reserved that ceiling is the earned pool
    /// and the line reads as it always has. Where an equipped entry reserves
    /// part of it (§3.3) the spendable ceiling is the figure and the earned pool
    /// follows it — <c>MANA: 20 / 20 (100)</c> — because a bar that tops out at
    /// 20 beside a printed 100 is the one place the locks are invisible to the
    /// player; which entry took it is the <c>LOCK</c> the weapon line already
    /// prints (<see cref="EnchantmentReadout"/>).
    /// </summary>
    internal static string ManaReadout(ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.PaidLocks > 0
            ? $"MANA: {actor.Mana} / {actor.UsableMaxMana} ({actor.MaxMana})"
            : $"MANA: {actor.Mana} / {actor.MaxMana}";
    }

    // ── The floor, its stairs, and leaving ──────────────────────────────────

    /// <summary>The floor line, top left: which dungeon, which floor (<see cref="FloorLine"/>).</summary>
    private void DrawFloorLine(DungeonState visit)
        => _text.DrawText(FloorLine(visit), 14f, 14f, 1.5f, Cyan);

    /// <summary>
    /// Where the party is, as the floor line prints it: the dungeon and the
    /// floor <em>number</em> — the index plus one — <c>THE TUTORIAL - FLOOR 1</c>.
    /// <strong>Never the depth</strong> (§4.3, PHASE4-024): the boss floor is
    /// rolled at entry and not disclosed, the player learns it only by reaching
    /// it, so the line is never "floor 3 of 7", never "3/7", and reads the same
    /// on floor 3 of a five-floor visit as of a ten-floor one. That is what keeps
    /// the depth from being scummed: nothing on screen tells a shallow roll from
    /// a deep one before the bottom is under the party's feet.
    /// <para>
    /// Once the boss has fallen the line says what the dungeon has become, on
    /// every floor for the rest of the visit — <c>THE TUTORIAL - FLOOR 1 - NOTHING
    /// REVIVES</c> — since the climb out is fought against a floor that no longer
    /// replenishes, and every corpse on it is lying on what it carried (§4.3).
    /// </para>
    /// </summary>
    internal static string FloorLine(DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        string line = $"{visit.Def.Name.ToUpperInvariant()} - {ArrivalBanner(visit)}";
        return visit.BossDefeated ? $"{line} - {NothingRevives}" : line;
    }

    /// <summary>What the dungeon is once its boss is down (§4.3): said on the floor line, and in the boss's fall.</summary>
    private const string NothingRevives = "NOTHING REVIVES";

    /// <summary>A boss by the name its plate reads, for the lines that speak of it: <c>STONE GOLEM</c>.</summary>
    private static string BossName(EnemyState boss) => boss.Title?.ToUpperInvariant() ?? "BOSS";

    /// <summary>
    /// The trade the boss floor states before the fight (§4.3, settled's backlog:
    /// "surface the boss-floor lockout/re-roll trade-off ... before a player
    /// commits"), under its arrival banner: who waits here, and what beating it
    /// costs — the tutorial closes for good (§4.3: it is the one dungeon the game
    /// is allowed to warn you about); any other dungeon locks for
    /// <see cref="DungeonState.LockRuns"/> — as many runs as its floor, at the
    /// shipped tuning — while leaving without the fight re-rolls the depth for
    /// next time (PHASE4-026). The number is the visit's, never worked out here:
    /// the lock a clear writes is <c>RunCounter + LockRuns</c>, the same rule, so
    /// the promise cannot drift from what is applied. The depth is the party's to
    /// know by now: they are standing on it.
    /// <para>
    /// Empty everywhere else — above the bottom, where saying so would disclose
    /// how deep the dungeon goes (PHASE4-024), and on the bottom once the boss is
    /// down, when there is no trade left to make.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<string> BossFloorBanner(DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        if (visit.Current.Boss is not { Alive: true } boss || visit.BossDefeated)
            return [];

        string waits = $"THE {BossName(boss)} WAITS ON THIS FLOOR";
        string dungeon = visit.Def.Name.ToUpperInvariant();
        if (visit.Def.RetiresOnClear)
            return [waits, $"BEATING IT CLOSES {dungeon} FOR GOOD"];

        int runs = visit.LockRuns;
        return [waits, $"BEATING IT LOCKS {dungeon} FOR {runs} {(runs == 1 ? "RUN" : "RUNS")}", "LEAVE WITHOUT IT AND THE DEPTH RE-ROLLS"];
    }

    /// <summary>
    /// What the boss's fall says (§4.3): who fell, and what it means —
    /// <c>THE STONE GOLEM FALLS</c>, then <c>NOTHING REVIVES - FIGHT YOUR WAY OUT</c>:
    /// the farm is over, the dungeon is finite, and the boss does not end the run
    /// (§4.4) — the party turns round and climbs back through everything it farmed.
    /// </summary>
    internal static (string Headline, string Aftermath) BossFalls(EnemyState boss)
    {
        ArgumentNullException.ThrowIfNull(boss);
        return ($"THE {BossName(boss)} FALLS", $"{NothingRevives} - FIGHT YOUR WAY OUT");
    }

    /// <summary>
    /// What the boss left on the floor, as its fall states it (§4.3): the weapon,
    /// and what makes it the boss's — <c>DROPPED: HUNTING BOW - FORGED WITH
    /// BLOCK</c>, the theme forged onto whatever class it came as. When the graft
    /// made it its class's unique the line says that instead — <c>DROPPED: THE
    /// BULWARK - UNIQUE</c> — the unique being the theme at its deepest.
    /// </summary>
    /// <param name="drop">The boss's drop.</param>
    /// <param name="theme">The boss's dungeon's theme; null names the weapon alone.</param>
    internal static string BossDropLine(Drop drop, ModifierType? theme)
    {
        ArgumentNullException.ThrowIfNull(drop);
        string name = drop.Weapon.Name.ToUpperInvariant();
        if (drop.Weapon.Unique) return $"DROPPED: {name} - UNIQUE";
        return theme is { } t ? $"DROPPED: {name} - FORGED WITH {ModifierName(t)}" : $"DROPPED: {name}";
    }

    /// <summary>
    /// The beat a unique drop earns the moment it lands (§1.5): its name, called
    /// out — <c>UNIQUE: THE BULWARK!</c> — however it came: the farm's lottery on
    /// a permanent kill, or the boss's graft taking the Purity variant of its
    /// theme's own class to ×3. Null for every other drop, which its gold-less
    /// name on the floor says enough about.
    /// </summary>
    internal static string? UniqueBeat(Drop drop)
    {
        ArgumentNullException.ThrowIfNull(drop);
        return drop.Weapon.Unique ? $"UNIQUE: {drop.Weapon.Name.ToUpperInvariant()}!" : null;
    }

    /// <summary>A modifier by name in the HUD's capitals, its words apart: <c>BLOCK</c>, <c>CRIT WINDOW</c>, <c>LONGSHOT</c>.</summary>
    internal static string ModifierName(ModifierType type)
    {
        string raw = type.ToString();
        var name = new System.Text.StringBuilder(raw.Length + 2);
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && char.IsUpper(raw[i]))
                name.Append(' ');
            name.Append(char.ToUpperInvariant(raw[i]));
        }
        return name.ToString();
    }

    /// <summary>The arrival banner's line: <c>FLOOR 2</c>, the floor number reached and nothing about how many there are (§4.3).</summary>
    internal static string ArrivalBanner(DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return $"FLOOR {visit.FloorIndex + 1}";
    }

    /// <summary>What each kind of stair is labelled, on its slab once its tile has been seen.</summary>
    private static readonly IReadOnlyDictionary<Stair, string> StairLabels = new Dictionary<Stair, string>
    {
        [Stair.Down] = "STAIRS DOWN",
        [Stair.Up] = "STAIRS UP",
        [Stair.Exit] = "WAY OUT",
    };

    /// <summary>What E does on each kind of stair, when the party may take it: a verb with its key.</summary>
    private static readonly IReadOnlyDictionary<Stair, string> StairVerbs = new Dictionary<Stair, string>
    {
        [Stair.Down] = "E: DESCEND",
        [Stair.Up] = "E: CLIMB",
        [Stair.Exit] = "E: LEAVE THE DUNGEON",
    };

    /// <summary>
    /// A stair's label, by what it does: <c>STAIRS DOWN</c>, <c>STAIRS UP</c>,
    /// and the first floor's up-stairs <c>WAY OUT</c> — the kind is the visit's
    /// (<see cref="DungeonState.StairsOf"/>), so a label never says up where the
    /// stair leads out.
    /// </summary>
    internal static string StairLabel(Stair kind)
        => StairLabels.GetValueOrDefault(kind, kind.ToString().ToUpperInvariant());

    /// <summary>
    /// The cue at the stair the active member stands on, given what the visit
    /// said to it (<see cref="DungeonState.CanUse"/>): the stair's verb with its
    /// key when the party may take it — <c>E: DESCEND</c>, <c>E: CLIMB</c>,
    /// <c>E: LEAVE THE DUNGEON</c> — or, with the party short, how short it is,
    /// <c>GATHER THE PARTY - 2 OF 4 HERE</c>: <paramref name="gathered"/> of the
    /// <paramref name="living"/> members within reach of it, the fallen counted
    /// in neither, since they are carried. Null when it is not the party's turn:
    /// the cue is drawn only in the player phase, and there E says nothing.
    /// </summary>
    internal static string? StairCue(StairUse stair, StairRefusal? refusal, int gathered, int living) => refusal switch
    {
        null => StairVerbs.GetValueOrDefault(stair.Kind, $"E: {stair.Kind.ToString().ToUpperInvariant()}"),
        StairRefusal.PartyNotGathered => $"GATHER THE PARTY - {gathered} OF {living} HERE",
        _ => null,
    };

    /// <summary>
    /// The question the way out asks before the party leaves, with the keys
    /// that answer it — never a number key, since 2 on the pause menu is the
    /// exit from the game.
    /// </summary>
    internal static string LeavePrompt() => "LEAVE THE DUNGEON? E OR ENTER: YES - ESC: NO";

    // A stair's label and its cue hang under the near edge of its tile — the
    // edge toward the camera, which looks up the map (-Z) — so at any zoom they
    // clear the slab, whoever stands on it, the nameplate of a dummy standing
    // on it, and a ground item's name and PRESS G on the same tile.
    private const float StairLabelOffset = 6f;
    private const float StairCueOffset = 22f;

    /// <summary>
    /// Each stair of the floor, labelled in its colour once its tile has been
    /// seen — explored, it shows through the shroud, and its label is how the way
    /// back is found — and at the stair the active member stands on, what E
    /// would do there, or why not (<see cref="DungeonScene.ActiveStairPrompt"/>).
    /// </summary>
    private void DrawStairs(Camera camera, DungeonScene scene, int w, int h)
    {
        var seen = scene.Visit.Current.Fog.Seen;
        var prompt = scene.ActiveStairPrompt;
        foreach (var marker in scene.StairMarkers)
        {
            var (r, c) = marker.Stair.Tile;
            if (!seen[r, c]) continue;
            var nearEdge = marker.Slab.Position + Vector3.UnitZ * (WorldSpace.UnitsPerTile / 2f);
            if (!WorldToScreen(camera, nearEdge, w, h, out var px)) continue;

            DrawCenteredAt(px.X, px.Y + StairLabelOffset, StairLabel(marker.Stair.Kind), 1.4f, DungeonScene.StairColor(marker.Stair.Kind));
            if (prompt is { } p && p.Stair == marker.Stair)
                DrawCenteredAt(px.X, px.Y + StairCueOffset, p.Cue, 1.4f, p.Ready ? White : Grey);
        }
    }

    /// <summary>"Leave the dungeon?", over the dimmed floor: the question with its keys, then what leaving now means (<see cref="LeavePreview"/>) — the last line, what it costs whatever else it means, in grey.</summary>
    private void DrawConfirmLeave(int w, int h, DungeonState visit)
    {
        float top = h / 2f - 60f;
        DrawCentered(w, top, LeavePrompt(), 2.5f, Yellow);
        var lines = LeavePreview(visit);
        float y = top + 44f;
        for (int i = 0; i < lines.Count; i++)
        {
            DrawCentered(w, y, lines[i], 1.5f, i == lines.Count - 1 ? Grey : White);
            y += 22f;
        }
    }

    /// <summary>What every leave costs, whatever else it means: the visit, whole (§4.1).</summary>
    private const string ResetLine = "EVERY FLOOR RESETS - THE NEXT VISIT IS A NEW DUNGEON";

    /// <summary>
    /// What leaving now means, under the way out's question (§4.4) — said before
    /// the player commits, since the answer ends the run. With the boss down it
    /// is a clear: <c>THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY</c>, and the
    /// dungeon closes for good (the tutorial) or locks for as many runs as its
    /// floor (<see cref="DungeonState.LockRuns"/>, the number the clear will
    /// write). With it standing it is a retreat: <c>NO BOSS - A RETREAT: XP KEPT,
    /// THE DEPTH RE-ROLLS</c>, and whether the party got deep enough for the run
    /// to count (§6.3's mercy, from <see cref="Tuning.MercyFloor"/>) — a rule, never
    /// the depth. And either way, the whole visit (<see cref="ResetLine"/>), last.
    /// </summary>
    internal static IReadOnlyList<string> LeavePreview(DungeonState visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        string dungeon = visit.Def.Name.ToUpperInvariant();
        if (visit.BossDefeated)
        {
            string clear = visit.Def.RetiresOnClear
                ? $"{dungeon} CLOSES FOR GOOD"
                : $"{dungeon} LOCKS FOR {Runs(visit.LockRuns)}";
            return ["THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY", clear, ResetLine];
        }

        bool counts = visit.DeepestFloorIndex + 1 >= GameContent.Current.Tuning.MercyFloor;
        return
        [
            "NO BOSS - A RETREAT: XP KEPT, THE DEPTH RE-ROLLS",
            counts ? "DEEP ENOUGH TO COUNT AS A RUN" : "TOO SHALLOW TO COUNT AS A RUN",
            ResetLine,
        ];
    }

    /// <summary>A number of runs as the HUD says it: <c>1 RUN</c>, <c>7 RUNS</c>.</summary>
    private static string Runs(int n) => $"{n} {(n == 1 ? "RUN" : "RUNS")}";

    // ── The run's end ────────────────────────────────────────────────────────

    /// <summary>A wiped run's headline (§4.4), on its summary and on the defeat screen: the run is over, not the game.</summary>
    internal const string DefeatHeadline = "DEFEATED";

    /// <summary>
    /// The one key a run's end answers: the summary is modal, ENTER moves on to
    /// the next run, and ESC does nothing there — what the run just wrote lives
    /// only in memory until it is saved, so it never offers a quit.
    /// </summary>
    internal const string ContinueHint = "ENTER TO CONTINUE";

    /// <summary>How many characters a summary line runs to before a list on it wraps: a little over half the shipped window at the summary's scale.</summary>
    private const int SummaryLineChars = 64;

    /// <summary>
    /// A run's summary (§4.4), a line a fact, the headline first and the key
    /// last: what the run was — <c>CLEARED</c>, <c>RETREATED</c>, <c>DEFEATED</c> —
    /// where and how deep, the boss (its floor only where the party reached it,
    /// §4.3), the spoils kept or lost by name, the XP kept or what the wipe clawed
    /// back member by member, whether the run counted and the counter it stands
    /// at (§6.3), the lock or the closing a clear wrote, and <c>ENTER TO
    /// CONTINUE</c> — naming <paramref name="next"/>, the dungeon the next run
    /// goes into, when there is one — and never ESC.
    /// </summary>
    /// <param name="result">The run's end, as the campaign returned it.</param>
    /// <param name="next">The next run's dungeon, by name; null names none.</param>
    internal static IReadOnlyList<Run> RunSummaryLines(RunResult result, string? next = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var content = GameContent.Current;
        var def = content.Dungeons.TryGet(result.DungeonId, out var found) ? found : null;
        string dungeon = (def?.Name ?? result.DungeonId).ToUpperInvariant();
        string boss = def?.Boss.Name.ToUpperInvariant() ?? "BOSS";

        var lines = new List<Run>
        {
            result.Outcome switch
            {
                RunOutcome.BossKill => new Run("CLEARED", Green),
                RunOutcome.Retreat => new Run("RETREATED", Yellow),
                _ => new Run(DefeatHeadline, Red),
            },
            new Run($"{dungeon} - DEEPEST FLOOR {result.DeepestFloor}", Grey),
        };

        // The boss, and the depth only where the party stood on it (§4.3).
        if (result.BossDefeated)
            lines.Add(new Run(result.BossFloorRevealed is { } fell ? $"THE {boss} FELL ON FLOOR {fell}" : $"THE {boss} FELL", Green));
        else if (result.BossFloorRevealed is { } waited)
            lines.Add(new Run($"THE {boss} WAITED ON FLOOR {waited} - THE DEPTH RE-ROLLS NEXT TIME", Grey));
        else
            lines.Add(new Run($"THE {boss} STILL STANDS - THE DEPTH RE-ROLLS NEXT TIME", Grey));

        // What was carried home, or lost with the party.
        if (result.SpoilsKept.Count > 0)
            lines.AddRange(Wrapped("SPOILS KEPT:", result.SpoilsKept.Select(w => w.Name.ToUpperInvariant()), Orange));
        if (result.SpoilsLost.Count > 0)
            lines.AddRange(Wrapped("SPOILS LOST:", result.SpoilsLost.Select(w => w.Name.ToUpperInvariant()), Red));
        if (result.SpoilsKept.Count == 0 && result.SpoilsLost.Count == 0)
            lines.Add(new Run(result.BossDefeated ? "NO SPOILS CARRIED" : "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS", Grey));

        // The XP: kept, or the share of each pool's gain a wipe took back, member by member.
        if (result.Outcome != RunOutcome.Death)
            lines.Add(new Run("XP KEPT", Green));
        else if (result.Clawbacks.Count == 0)
            lines.Add(new Run("XP KEPT - NOTHING CLAWED BACK", Green));   // nothing gained, or a share of 0
        else
        {
            lines.Add(new Run("XP CLAWED BACK:", Red));
            foreach (var member in result.Clawbacks.GroupBy(c => c.MemberId))
                lines.AddRange(Wrapped($"{member.Key}:", member.Select(c => $"{c.Pool.ToUpperInvariant()} -{c.Before - c.After}"), Red));
        }

        // Whether the run counted (§6.3), and where the counter stands.
        string counted = result.ServiceTick
            ? result.MercyTick ? "RUN COUNTED - A MERCY FOR FAILING DEEP" : "RUN COUNTED"
            : $"RUN NOT COUNTED - A FAILED RUN COUNTS FROM FLOOR {content.Tuning.MercyFloor}";
        lines.Add(new Run($"{counted} - {Runs(result.RunCounter)} SO FAR", Cyan));

        // What the clear did to the dungeon.
        if (result.Retired)
            lines.Add(new Run($"{dungeon} IS CLOSED FOR GOOD", Yellow));
        else if (result.LockedUntilRun is { } until)
            lines.Add(new Run($"{dungeon} IS LOCKED FOR {Runs(until - result.RunCounter)}", Yellow));

        lines.Add(new Run(next is null ? ContinueHint : $"{ContinueHint} - NEXT: {next.ToUpperInvariant()}", White));
        return lines;
    }

    /// <summary>A list under a heading, as many summary lines as it takes: the heading and the items on the first, the rest indented under it, comma-separated.</summary>
    private static IEnumerable<Run> Wrapped(string heading, IEnumerable<string> items, Vector4 color)
    {
        var line = new System.Text.StringBuilder(heading);
        bool first = true;
        foreach (string item in items)
        {
            string piece = first ? $" {item}" : $", {item}";
            if (!first && line.Length + piece.Length > SummaryLineChars)
            {
                yield return new Run(line.Append(',').ToString(), color);
                line.Clear().Append("  ").Append(item);
            }
            else
            {
                line.Append(piece);
            }
            first = false;
        }
        yield return new Run(line.ToString(), color);
    }

    /// <summary>
    /// The run's summary over the dimmed floor (<see cref="RunSummaryLines"/>):
    /// the headline large, each line under it, and the key last, a step apart.
    /// Anchored above the middle, never over the top readouts.
    /// </summary>
    private void DrawRunSummary(int w, int h, RunResult result, string? next)
    {
        var lines = RunSummaryLines(result, next);
        float y = MathF.Max(80f, h / 2f - 170f);
        DrawCentered(w, y, lines[0].Text, 5f, lines[0].Color);
        y += 56f;
        for (int i = 1; i < lines.Count; i++)
        {
            bool last = i == lines.Count - 1;
            if (last) y += 12f;
            DrawCentered(w, y, lines[i].Text, last ? 2f : 1.6f, lines[i].Color);
            y += 22f;
        }
    }

    /// <summary>
    /// The banner, fading over its last <see cref="BannerFadeSeconds"/>: the
    /// line over it (the dungeon's name, on arrival), the line itself (the floor
    /// reached, the boss fallen), and any warning under it (the boss floor's
    /// trade, what the fall means). Not over a menu or the game-over screen, whose
    /// words are at the same height and are the ones being asked about; and never
    /// over the fade away from the floor it speaks of, which takes it down as it
    /// starts (<see cref="HideArrival"/>).
    /// </summary>
    private void DrawBanner(DungeonScene scene, int w, int h)
    {
        if (_banner is not { } banner || scene.AnyMenuOpen || scene.Turns.Phase == TurnPhase.GameOver) return;
        float alpha = Math.Clamp((banner.Seconds - _bannerAge) / BannerFadeSeconds, 0f, 1f);
        float y = h / 3f;
        if (banner.Over.Length > 0)
            DrawCentered(w, y - 30f, banner.Over, 2f, Grey with { W = alpha });
        DrawCentered(w, y, banner.Main, 5f, White with { W = alpha });
        float underY = y + 52f;
        foreach (string line in banner.Under)
        {
            DrawCentered(w, underY, line, 2f, Yellow with { W = alpha });
            underY += 26f;
        }
    }

    // Party selector layout, shared between drawing and click hit-testing.
    private const float SelectorX = 14f;
    private const float SelectorTop = 130f;
    private const float SelectorRowPitch = 30f;
    private const float SelectorRowBand = 26f; // clickable height within each row's pitch
    private const float SelectorWidth = 150f;

    /// <summary>
    /// Hit-test the party selector rows against a mouse position in window
    /// pixels. The rows double as click-to-select buttons.
    /// </summary>
    public static bool HitPartySelector(Vector2 mouse, int partyCount, out int index)
    {
        index = -1;
        if (mouse.X < SelectorX - 4f || mouse.X > SelectorX + SelectorWidth) return false;
        float rel = mouse.Y - (SelectorTop - 5f);
        if (rel < 0f) return false;
        int row = (int)(rel / SelectorRowPitch);
        if (row >= partyCount || rel - row * SelectorRowPitch > SelectorRowBand) return false;
        index = row;
        return true;
    }

    private void DrawPartySelector(DungeonScene scene)
    {
        int hovered = -1;
        bool hasHover = !scene.AnyMenuOpen
            && HitPartySelector(scene.MousePos, scene.Party.Count, out hovered);

        for (int i = 0; i < scene.Party.Count; i++)
        {
            var state = scene.Party[i].State;
            bool isActive = i == scene.ActiveIndex;
            var color = !state.Alive ? DarkGrey : scene.PartyColor(i);
            if (hasHover && hovered == i && state.Alive)
                color = Vector4.Lerp(color, White, 0.5f);
            string marker = isActive ? ">" : " ";
            string row = $"{marker}{i + 1} {state.Id} {state.Hp}";
            float y = SelectorTop + i * SelectorRowPitch;
            _text.DrawText(row, SelectorX, y, 2f, color);

            // Its effect badges beside it, and a banked Overwatch shot, which fires on the enemy's turn.
            var badges = new List<Run>(StatusBadgeRuns(state));
            if (state.HeldShots > 0)
                badges.Add(new Run($"HOLD {state.HeldShots}", Cyan));
            DrawRuns(SelectorX + (row.Length + 1) * GlyphAdvance * 2f, y + 2f, 1.5f, badges);
        }

        // Hovering a member's row spells out what each of its statuses does to
        // it: Mire against the whole its turn start cuts, banked movement included.
        if (hasHover && scene.Party[hovered].State is { Alive: true } member)
        {
            float y = SelectorTop + scene.Party.Count * SelectorRowPitch + 4f;
            foreach (var line in StatusLegend(member, scene.BudgetBeforeMire(member)))
            {
                _text.DrawText(line.Text, SelectorX, y, 1.3f, line.Color);
                y += 13f;
            }
        }
    }

    private void DrawEnemyLabels(Camera camera, DungeonScene scene, int w, int h)
    {
        var boss = scene.Visit.Current.Boss;
        foreach (var obj in scene.Enemies)
        {
            if (!obj.IsActive || !obj.State.Alive) continue; // fogged or dead
            var anchor = obj.Position + Vector3.UnitY * (0.9f * obj.Size);   // a boss's box is taller, and so is where its plate hangs
            if (!WorldToScreen(camera, anchor, w, h, out var px)) continue;

            var enemy = obj.State;
            string label = EnemyTitle(enemy);
            // The boss's name is the one plate on the floor that is not a dummy's: larger, and in the stone its golem is cut from.
            bool isBoss = enemy == boss;
            float titleScale = isBoss ? 1.8f : 1.5f;
            float titleY = px.Y - (isBoss ? 21f : 18f);
            // Inside the held wand's shape as aimed right now: a click there catches it.
            if (scene.AimCaught.Contains(enemy))
                DrawCenteredAt(px.X, titleY, $"> {label} <", titleScale, Yellow);
            else
                DrawCenteredAt(px.X, titleY, label, titleScale, isBoss ? Stone : enemy.Weapon.IsCaster ? Cyan : Orange);

            DrawRunsCenteredAt(px.X, px.Y, 1.5f, NameplateRuns(enemy));

            if (obj != scene.HoveredEnemy) continue;

            // Under the mouse: what a click would do (or why it would not), then what each status does.
            float y = px.Y + 16f;
            if (scene.HoverCue is { } cue)
            {
                DrawCenteredAt(px.X, y, cue.Label, 1.4f, cue.Ready ? White : Grey);
                y += 14f;
            }
            foreach (var line in StatusLegend(enemy, enemy.MoveBudget))   // Mire cuts this enemy's own budget: a golem's 40, a dummy's 100
            {
                DrawCenteredAt(px.X, y, line.Text, 1.2f, line.Color);
                y += 12f;
            }
        }
    }

    /// <summary>
    /// The title line over an enemy's plate: what it is, and what it is holding
    /// in brackets — <c>DUMMY [Arming Sword]</c>, <c>HEALER [Staff of Renewal]</c>,
    /// and a boss by its own name, <c>STONE GOLEM [Hatchet]</c> (§4.3: it wields
    /// a weapon like a dummy does). HEALER is what mends (the innate's status
    /// restores HP); every other caster, warding or debuffing, is CASTER.
    /// </summary>
    internal static string EnemyTitle(EnemyState enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        string name = enemy.Title?.ToUpperInvariant()
            ?? (enemy.IsHealer ? "HEALER" : enemy.Weapon.IsCaster ? "CASTER" : "DUMMY");
        return $"{name} [{enemy.Weapon.Name}]";
    }

    /// <summary>
    /// What is lying on the floor: over each ground item in sight, the weapon it
    /// is and the farm's depth it was collected at, and — on the one the active
    /// member is standing on — the key that takes it. Nothing is drawn for an
    /// item on a fogged tile: it is exactly as visible as the corpse it came off.
    /// </summary>
    private void DrawGroundItems(Camera camera, DungeonScene scene, int w, int h)
    {
        var target = scene.PickupTarget;
        foreach (var item in scene.GroundItems)
        {
            if (!item.Marker.IsActive) continue;
            if (!WorldToScreen(camera, item.Marker.Position + Vector3.UnitY * 0.5f, w, h, out var px)) continue;

            DrawRunsCenteredAt(px.X, px.Y - 14f, 1.5f, GroundItemRuns(item.Drop));
            if (item == target)
                DrawCenteredAt(px.X, px.Y + 2f, DungeonScene.PickupHint, 1.4f, White);
        }
    }

    /// <summary>
    /// A weapon on the floor: its name — a unique's in the gold every other
    /// readout gives it — and, beside it, <c>xN</c>, the farm's depth it was
    /// collected at, in the same shape a dummy's plate wears it
    /// (<see cref="NameplateRuns"/>), so what the player was farming for and what
    /// they are being handed read as one thing (§3.2). A drop at zero shows no
    /// quality, because there is none — it is simply the weapon the dummy had.
    /// <para>
    /// <strong>The number is one higher than the last plate the player saw, and
    /// that is a decided reading rather than an accident of order.</strong> The
    /// extraction kill is a defeat like every other: it advances
    /// <see cref="EnemyState.DefeatCount"/> (the Killed applier, §3.2) before the
    /// drop is rolled off it, so "kill it forty times" hands over a drop at 40 and
    /// the unique roll resolves at 40. There is no plate on screen for it to
    /// contradict — <see cref="DrawEnemyLabels"/> draws none for the dead — so
    /// what the player last read over the living dummy was <c>x39</c>, and the
    /// swing that put it down for good is the fortieth cycle.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<Run> GroundItemRuns(Drop drop)
    {
        ArgumentNullException.ThrowIfNull(drop);
        var runs = new List<Run> { new(drop.Weapon.Name, drop.Weapon.Unique ? Orange : White) };
        if (drop.DefeatCount > 0)
            runs.Add(new Run($"x{drop.DefeatCount}", Orange));
        return runs;
    }

    /// <summary>A support staff's cue over the ally under the mouse: the cast a click would make, or why it would not.</summary>
    private void DrawAllyCue(Camera camera, DungeonScene scene, int w, int h)
    {
        if (scene.HoveredMember is not { } member || scene.HoverCue is not { } cue) return;
        if (!WorldToScreen(camera, member.Position + Vector3.UnitY * 0.9f, w, h, out var px)) return;
        DrawCenteredAt(px.X, px.Y - 18f, cue.Label, 1.4f, cue.Ready ? Green : Grey);
    }

    /// <summary>A wand's aim cue at the cursor: the shape and the bodies a click there would catch, or why it would not cast.</summary>
    private void DrawAimCue(DungeonScene scene)
    {
        if (scene.AimCue is not { } cue) return;
        _text.DrawText(cue.Label, scene.MousePos.X + 18f, scene.MousePos.Y + 12f, 1.5f, cue.Ready ? Yellow : Grey);
    }

    private void DrawFloatingTexts(Camera camera, int w, int h)
    {
        foreach (var ft in _floatingTexts)
        {
            if (!WorldToScreen(camera, ft.WorldPos + Vector3.UnitY * 1.2f, w, h, out var px)) continue;
            float t = ft.Age / FloatingTextLife;
            float alpha = 1f - t * t;
            float y = px.Y + ft.PixelYOffset - FloatingTextRise * t;
            DrawRunsCenteredAt(px.X, y, 2f, ft.Runs, alpha);
        }
    }

    // Inventory panel geometry. The panel was a fixed height until §2.2, so a
    // top fixed at the middle of the window always had room under it. It grows
    // now, by a row per class the member is levelling or holding, and what a
    // short window loses first are the lines under the rows: what a swap costs
    // and which key equips.

    /// <summary>The first slot's line, under the title and the EQUIPPED label.</summary>
    private const float InventorySlotsTop = 70f;

    /// <summary>A slot: its name, and the statline under it.</summary>
    private const float InventorySlotPitch = 46f;

    /// <summary>The PROGRESSION label over the rows.</summary>
    private const float InventoryLabelHeight = 16f;

    /// <summary>One pool or proficiency row.</summary>
    private const float InventoryRowPitch = 14f;

    /// <summary>The swap line under the last row, and the key hint under that.</summary>
    private const float InventorySwapGap = 16f;
    private const float InventoryHintGap = 26f;

    /// <summary>The last line's own glyphs, so a height covers what the panel draws and not just where it starts.</summary>
    private const float InventoryLineHeight = 12f;

    /// <summary>Half the panel's height as it has always been centred, while the window has room for it.</summary>
    private const float InventoryCentredOffset = 150f;

    /// <summary>Clearance kept under the panel for the help line at <c>h - 24</c>.</summary>
    private const float InventoryBottomMargin = 32f;

    /// <summary>Clearance kept over it for the two top readouts, which the panel never slides under.</summary>
    private const float InventoryTopMargin = 56f;

    /// <summary>Where the inventory panel opens, how many of its progression rows there is room for, and the line past its last glyph.</summary>
    internal readonly record struct InventoryLayout(float Top, int Rows, float Bottom);

    /// <summary>
    /// The panel's vertical fit in a window <paramref name="windowHeight"/>
    /// pixels tall, for a member with <paramref name="slots"/> slots and
    /// <paramref name="rowCount"/> progression rows to print (§2.2): it keeps
    /// its centred place while the whole of it fits, slides up — never under
    /// the top readouts — when it does not, and only past that drops rows it
    /// has no room for, from the end of the list, rather than the lines that
    /// name the swap cost and the equip keys. A row-less panel needs
    /// <c>228 + 46 × slots</c> px of window (the top margin, the panel's fixed
    /// lines, its slots and the help line's clearance) — about 370 at Phase 3's
    /// three slots, about 500 at §4.4's six; the shipped 1280 x 720 window holds
    /// six with every row printed. Below that even a row-less panel is taller
    /// than the room under the readouts, and the panel is then anchored as high
    /// as it goes and clipped: that is a window smaller than the rest of the
    /// HUD is laid out for either.
    /// </summary>
    internal static InventoryLayout LayoutInventory(float windowHeight, int slots, int rowCount)
    {
        float fixedHeight = InventorySlotsTop + slots * InventorySlotPitch + InventoryLabelHeight
            + InventorySwapGap + InventoryHintGap + InventoryLineHeight;
        float floorLine = windowHeight - InventoryBottomMargin;
        float top = MathF.Min(windowHeight / 2f - InventoryCentredOffset,
                              floorLine - fixedHeight - rowCount * InventoryRowPitch);
        top = MathF.Max(InventoryTopMargin, top);
        int rows = Math.Clamp((int)MathF.Floor((floorLine - top - fixedHeight) / InventoryRowPitch), 0, rowCount);
        return new InventoryLayout(top, rows, top + fixedHeight + rows * InventoryRowPitch);
    }

    private void DrawInventory(int w, int h, PartyMemberState active, TurnSystem turns)
    {
        float cx = w / 2f;

        // Under the slots: what every pool and every ladder is doing (§2.2).
        // The statlines above price the items; these rows price the member.
        var rows = new List<Run>();
        foreach (string row in PoolRows(active))
            rows.Add(new Run(row, White));
        foreach (string row in ProficiencyRows(active))
            rows.Add(new Run(row, Cyan));

        var layout = LayoutInventory(h, active.Inventory.Length, rows.Count);
        float top = layout.Top;
        float left = cx - 190f;

        DrawCentered(w, top, $"INVENTORY - CHAR {active.Id}", 3f, Cyan);
        _text.DrawText("EQUIPPED", left, top + 46f, 1.2f, Grey);

        for (int slot = 0; slot < active.Inventory.Length; slot++)
        {
            float y = top + InventorySlotsTop + slot * InventorySlotPitch;
            var weapon = active.Inventory[slot];
            if (weapon == null)
            {
                _text.DrawText($"{slot + 1}  - EMPTY -", left, y, 2f, DarkGrey);
                continue;
            }

            // The name, then the statline and modifiers under it as they read in this member's hands.
            var color = slot == 0 ? Yellow : White;
            _text.DrawText($"{slot + 1}  {weapon.Name}", left, y, 2f, weapon.Unique ? Orange : color);
            var detail = WeaponRuns("", weapon, active, turns.AttacksThisTurn(active), color);
            detail.RemoveAt(0); // the name is the line above
            DrawRuns(left + 3 * GlyphAdvance * 2f, y + 18f, 1.3f, detail);
        }

        float rowY = top + InventorySlotsTop + active.Inventory.Length * InventorySlotPitch;
        _text.DrawText("PROGRESSION", left, rowY, 1.2f, Grey);
        rowY += InventoryLabelHeight;
        for (int i = 0; i < layout.Rows; i++)
        {
            _text.DrawText(rows[i].Text, left, rowY, 1.5f, rows[i].Color);
            rowY += InventoryRowPitch;
        }

        // A swap is free out of combat and costs movement in it (§1.2): say which before the key is pressed.
        float swapY = rowY + InventorySwapGap;
        int cost = turns.SwapCost;
        if (cost <= 0)
            DrawCentered(w, swapY, "OUT OF COMBAT - SWAPPING IS FREE", 1.5f, Grey);
        else if (active.DistLeft < cost)
            DrawCentered(w, swapY, $"IN COMBAT - A SWAP COSTS {cost} MOVE - NOT ENOUGH LEFT", 1.5f, Red);
        else
            DrawCentered(w, swapY, $"IN COMBAT - A SWAP COSTS {cost} MOVE", 1.5f, Orange);

        DrawCentered(w, swapY + InventoryHintGap, EquipHint(active.Inventory.Length), 1.5f, Grey);
    }

    /// <summary>
    /// The inventory panel's key hint: the number keys that equip a bag slot —
    /// each the number the panel prints beside that slot, so the hand is 1 and
    /// the bag runs from 2 — then the key that closes the bag. For §4.4's six
    /// slots, <c>PRESS 2-6 TO EQUIP - I TO CLOSE</c>. The range is read off the
    /// member's slots and the scene's own highest binding
    /// (<see cref="DungeonScene.HighestSlotKey"/>), so it names every slot a key
    /// reaches and no key that does nothing.
    /// </summary>
    internal static string EquipHint(int slots)
    {
        int last = Math.Min(slots, DungeonScene.HighestSlotKey);
        return last switch
        {
            < 2 => "I TO CLOSE",                             // a hand and no bag: nothing to swap in
            2 => "PRESS 2 TO EQUIP - I TO CLOSE",
            _ => $"PRESS 2-{last} TO EQUIP - I TO CLOSE",
        };
    }

    /// <summary>
    /// The pause menu: close it, or leave the game. Stopping resurrection is the
    /// boss's doing (§4.3), and nothing else's — the dev switch that stood in for
    /// it went when the boss arrived — so what the dungeon has become is the
    /// floor line's to say (<see cref="FloorLine"/>).
    /// </summary>
    private void DrawMenu(int w, int h)
    {
        float top = h / 2f - 90f;
        DrawCentered(w, top, "PAUSED", 4f, Cyan);
        DrawCentered(w, top + 66f, "1  CLOSE", 2f, White);
        DrawCentered(w, top + 106f, "2  EXIT GAME", 2f, White);
        DrawCentered(w, top + 156f, "ESC TO CLOSE", 1.5f, Grey);
    }

    // ── Weapon readouts ──────────────────────────────────────────────────────

    /// <summary>
    /// A weapon as one line of runs: its name (a unique's in gold) and resolved
    /// statline, its modifiers, then its enchantments in attachment order — a
    /// caster's innate, a unique's souls.
    /// </summary>
    private static List<Run> WeaponRuns(string prefix, Weapon weapon, ActorState? wielder, int attacksThisTurn, Vector4 color)
    {
        var runs = new List<Run>
        {
            new($"{prefix}{weapon.Name}", weapon.Unique ? Orange : color),
            new(WeaponStats(weapon), color),
        };
        string modifiers = ModifierReadout(weapon, wielder, attacksThisTurn);
        if (modifiers.Length > 0)
            runs.Add(new Run($"* {modifiers}", White));
        string enchantments = EnchantmentReadout(weapon, wielder);
        if (enchantments.Length > 0)
            runs.Add(new Run($"* {enchantments}", Cyan));
        return runs;
    }

    /// <summary>
    /// The statline as the player reads it: the resolved costs — what a swing or
    /// a cast takes off the movement budget and the mana pool, Light's and
    /// Resonant's discounts already in them — never a base and a percentage
    /// (<c>COST 27</c>, not <c>30 -10%</c>). A staff drops damage for mana; a wand
    /// names its shape where the others give their range.
    /// </summary>
    internal static string WeaponStats(Weapon weapon)
        => weapon.AreaShape is { } shape
            ? $"DMG {weapon.Damage}  {ShapeName(shape)}  COST {weapon.ResolvedCost}  MANA {weapon.ResolvedManaCost}"
            : weapon.IsCaster
                ? $"RNG {weapon.Range}  COST {weapon.ResolvedCost}  MANA {weapon.ResolvedManaCost}"
                : $"DMG {weapon.Damage}  RNG {weapon.Range}  COST {weapon.ResolvedCost}";

    /// <summary>
    /// How a modifier reads when its number alone would mislead: the crit pair
    /// as one <c>CRIT</c> entry, the two discounts as flags (their numbers are
    /// already the cost and the mana), Charges as the attacks left of the
    /// cap, Overwatch as the shots held once banked. Every other modifier
    /// reads <c>NAME value</c>, so a new one needs no entry here.
    /// </summary>
    private static readonly IReadOnlyDictionary<ModifierType, Func<ReadoutContext, string>> ModifierFormats =
        new Dictionary<ModifierType, Func<ReadoutContext, string>>
        {
            [ModifierType.CritWindow] = CritText,
            [ModifierType.CritMultiplier] = CritText,
            [ModifierType.Light] = _ => "LIGHT",
            [ModifierType.Resonant] = _ => "RESONANT",
            [ModifierType.Charges] = c => c.Equipped
                ? $"CHARGES {Math.Max(0, c.Value(ModifierType.Charges) - c.AttacksThisTurn)}/{c.Value(ModifierType.Charges)}"
                : $"CHARGES {c.Value(ModifierType.Charges)}",
            [ModifierType.Overwatch] = c => c.Equipped && c.Wielder!.HeldShots > 0
                ? $"OVERWATCH {c.Wielder.HeldShots} HELD"
                : $"OVERWATCH {c.Value(ModifierType.Overwatch)}",
        };

    /// <summary>The §1.7 table's order, which the readout lists modifiers in.</summary>
    private static readonly ModifierType[] ModifierOrder = Enum.GetValues<ModifierType>();

    /// <summary>A weapon's modifiers as they resolve for a wielder: the weapon's stacks plus the wielder's innate ones, resolved together.</summary>
    private sealed record ReadoutContext(Weapon Weapon, ActorState? Wielder, bool Equipped, int AttacksThisTurn)
    {
        public int Stacks(ModifierType t) => Weapon.Stacks(t) + (Wielder?.Innate.Stacks(t) ?? 0);

        public int Value(ModifierType t) => GameContent.Current.Modifiers.Resolve(t, Stacks(t));
    }

    /// <summary>
    /// The weapon's modifiers as the player reads them — resolved values, in
    /// the §1.7 table's order, for the weapon in <paramref name="wielder"/>'s
    /// hands: <c>BLOCK 6  PUSH 1</c>, <c>CRIT 19+ x3  LIGHT</c>. The equipped
    /// weapon's Charges count the attacks left this turn after
    /// <paramref name="attacksThisTurn"/> (<c>CHARGES 2/3</c>) and its
    /// Overwatch the shots held (<c>OVERWATCH 1 HELD</c>); a weapon in the bag
    /// shows its caps. Empty for a weapon with no stacks.
    /// </summary>
    internal static string ModifierReadout(Weapon weapon, ActorState? wielder = null, int attacksThisTurn = 0)
    {
        var context = new ReadoutContext(weapon, wielder,
            Equipped: wielder != null && ReferenceEquals(wielder.EquippedWeapon, weapon), attacksThisTurn);
        var parts = new List<string>();
        foreach (var type in ModifierOrder)
        {
            if (context.Stacks(type) <= 0) continue;
            string text = ModifierFormats.TryGetValue(type, out var format)
                ? format(context)
                : $"{type.ToString().ToUpperInvariant()} {context.Value(type)}";
            if (!parts.Contains(text)) // the crit pair reads as one entry
                parts.Add(text);
        }
        return string.Join("  ", parts);
    }

    /// <summary>
    /// The crit window and multiplier as one entry: the natural roll that crits
    /// (<c>19+</c>; a plain <c>20</c> with no window) and what a crit multiplies
    /// by — the damage for a weapon that hits, the levels for a staff's cast.
    /// </summary>
    private static string CritText(ReadoutContext c)
    {
        int threshold = CombatRules.NaturalTwenty - c.Value(ModifierType.CritWindow);
        int multiplier = c.Weapon.IsCaster && c.Weapon.AreaShape == null
            ? CombatRules.CastCritLevelMultiplier
            : c.Value(ModifierType.CritMultiplier);
        return $"CRIT {threshold}{(threshold < CombatRules.NaturalTwenty ? "+" : "")} x{multiplier}";
    }

    // ── Progression readouts ─────────────────────────────────────────────────

    /// <summary>The space between two fields of a progression row, wide enough that the eye reads them as separate numbers at 5 px.</summary>
    private const string Gap = "   ";

    /// <summary>
    /// The active member's two pools as the inventory panel prints them (§2.2):
    /// the HP bar with the XP into its next point beside it, and mana's XP
    /// alone — the mana pool itself belongs to the budget line at the top of
    /// the screen, so no number is printed twice.
    /// </summary>
    internal static IReadOnlyList<string> PoolRows(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var health = member.HealthPool;
        var mana = member.ManaPool;
        return
        [
            $"HP {member.Hp}/{member.MaxHp}{Gap}XP {health.XpIntoNext}/{health.XpToNext}",
            $"MANA XP {mana.XpIntoNext}/{mana.XpToNext}",
        ];
    }

    /// <summary>
    /// One row per weapon class the member is levelling or holding — XP earned
    /// in it, or a weapon of it in a slot — in <see cref="WeaponClass"/> order:
    /// the level, the XP into the next, and what the level pays,
    /// <c>DAGGER 3   XP 45/300   DMG +1   COST -1</c>. The two effects appear
    /// once they are something: both are zero at the level everyone starts at
    /// (§2.2), and a row of zeroes says nothing.
    /// <para>
    /// Numbers rather than drawn bars: at the 5-px font a bar is a three-pixel
    /// smear, and a string-returning readout is what the HUD's tests can hold
    /// to, as <see cref="WeaponStats"/> and <see cref="ModifierReadout"/> are. A
    /// bar, if one is ever wanted, draws beside the row.
    /// </para>
    /// The cost here is the <em>discount</em>, not a cost: the statline above
    /// prints the item's own <see cref="Weapon.ResolvedCost"/> — the same number
    /// in anyone's hands — and this row says what this wielder takes off it.
    /// </summary>
    internal static IReadOnlyList<string> ProficiencyRows(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var rows = new List<string>();
        foreach (var cls in ProficiencyOrder)
        {
            if (member.WeaponXp[cls] <= 0 && !Holds(member, cls)) continue;
            var ladder = member.Proficiency(cls);
            string row = $"{ClassName(cls)} {ladder.Level}{Gap}XP {ladder.XpIntoNext}/{ladder.XpToNext}";
            int damage = Progression.DamageBonus(ladder.Level);
            if (damage > 0) row += $"{Gap}DMG +{damage}";
            int discount = Progression.MovementDiscount(ladder.Level);
            if (discount > 0) row += $"{Gap}COST -{discount}";
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>The order the rows are listed in, which is the order a save and the XP book both enumerate.</summary>
    private static readonly WeaponClass[] ProficiencyOrder = Enum.GetValues<WeaponClass>();

    /// <summary>True when a weapon of <paramref name="cls"/> sits in one of the member's slots: a class in hand or in the bag is listed before it has earned anything.</summary>
    private static bool Holds(PartyMemberState member, WeaponClass cls)
        => member.Inventory.Any(w => w?.Class == cls);

    private static string ClassName(WeaponClass cls) => cls.ToString().ToUpperInvariant();

    /// <summary>
    /// What a credit that bought a point or a level says over the member who
    /// earned it (§2.2): the class and the level now wielded at, or the pool and
    /// the ceiling now carried. Every number is read back off the member, so the
    /// beat states progression and decides none of it.
    /// </summary>
    internal static string LevelUpLabel(PartyMemberState member, XpCredit credit)
    {
        ArgumentNullException.ThrowIfNull(member);
        return credit.Pool switch
        {
            XpPool.Weapon when credit.Class is { } cls => $"{ClassName(cls)} {member.Proficiency(cls).Level}!",
            XpPool.Health => $"MAX HP {member.MaxHp}!",
            XpPool.Mana => $"MAX MANA {member.MaxMana}!",
            _ => "LEVEL UP!",
        };
    }

    /// <summary>
    /// The weapon's enchantments in attachment order, the tier after any above
    /// 1 (<c>VAMPIRIC T3</c>). Empty for none.
    /// <para>
    /// In <paramref name="wielder"/>'s hands each entry also names what it takes
    /// out of that wielder's pool — <c>LOCK 60</c> — and an entry whose lock
    /// went unpaid is marked <c>(ASLEEP)</c>, since a dormant entry is a
    /// non-event the player has otherwise no way to see (§3.1). A lock is a
    /// reservation out of a <em>pool</em>, so it reads only where there is one:
    /// an item in the bag, or one asked about with no wielder, prints its
    /// entries alone — the same split <see cref="ModifierReadout"/> makes
    /// between a weapon in hand and a weapon in the bag.
    /// </para>
    /// <para>
    /// An equipped entry also shows how far the mana in the tier it is on has got
    /// it — <c>XP 9/20</c> — because tier is earned by use (§3.3) and the bar is
    /// the only thing that says a swing is buying anything. <strong>It prints
    /// from zero</strong>: <c>XP 0/20</c> is what says a weapon just taken up can
    /// be deepened at all, and an entry that crosses its bar exactly would
    /// otherwise lose the row at the one moment it has most to say. The bar is
    /// <see cref="Enchantment.TierCost"/> at the <em>wielder's</em> INT — the
    /// price the climb charges rather than the unfloored
    /// <see cref="Enchantment.XpToNextTier"/>, so the readout and the ladder can
    /// never state two figures — and like the lock it reads only where there is a
    /// wielder. A unique prints what it has banked and no bar, since it is pinned
    /// at tier 1 and the mana buys nothing; with nothing banked it prints
    /// neither, there being no progress to state and no next tier to be partway
    /// to.
    /// </para>
    /// </summary>
    internal static string EnchantmentReadout(Weapon weapon, ActorState? wielder = null)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        bool equipped = wielder != null && ReferenceEquals(wielder.EquippedWeapon, weapon);
        int stat = (wielder as PartyMemberState)?.Stats.INT ?? InnateStats.Low;
        var parts = new List<string>();
        for (int i = 0; i < weapon.Enchantments.Count; i++)
        {
            var entry = weapon.Enchantments[i];
            string text = entry.Def.Name.ToUpperInvariant();
            if (entry.Tier > 1) text += $" T{entry.Tier}";
            if (equipped)
            {
                text += $" LOCK {entry.EffectiveLock}";
                if (!entry.Unique)
                    text += $" XP {entry.Xp}/{entry.TierCost(stat)}";
                else if (entry.Xp > 0)
                    text += $" XP {entry.Xp}";
                if (wielder!.IsDormant(i)) text += " (ASLEEP)";
            }
            parts.Add(text);
        }
        return string.Join("  ", parts);
    }

    private static string ShapeName(AreaShape shape) => shape.Kind.ToString().ToUpperInvariant();

    // ── Help line ────────────────────────────────────────────────────────────

    /// <summary>Who a staff's click casts on, by the side its effect lands on.</summary>
    private static readonly IReadOnlyDictionary<TargetSide, string> CastSides = new Dictionary<TargetSide, string>
    {
        [TargetSide.Ally] = "ALLY",
        [TargetSide.Enemy] = "ENEMY",
        [TargetSide.Any] = "ANYONE",
    };

    /// <summary>
    /// How each wand shape is aimed (§1.4): a Blast at a point within its reach,
    /// a Cone or a Beam in a direction, a Nova not at all — it sits on the caster.
    /// </summary>
    private static readonly IReadOnlyDictionary<AreaShapeKind, Func<AreaShape, string>> AimHints =
        new Dictionary<AreaShapeKind, Func<AreaShape, string>>
        {
            [AreaShapeKind.Blast] = s => $"CLICK A SPOT IN {CombatRules.TilesSpanned(s.TargetReach)} TILES: BLAST",
            [AreaShapeKind.Cone] = _ => "CLICK A DIRECTION: CONE",
            [AreaShapeKind.Beam] = _ => "CLICK A DIRECTION: BEAM",
            [AreaShapeKind.Nova] = _ => "N: NOVA AROUND YOU",
        };

    /// <summary>
    /// The bottom help line for what the active member holds: how its weapon is
    /// used — a swing, a staff's cast on the side its effect lands on, a wand's
    /// aim by its shape, Overwatch's hold on a ranged weapon that has it — then
    /// the keys every turn shares, the stairs' E among them (§4.2).
    /// </summary>
    internal static string HelpLine(PartyMemberState active)
    {
        var parts = new List<string> { "WASD MOVE" };
        var weapon = active.EquippedWeapon;
        if (weapon?.AreaShape is { } shape)
            parts.Add(AimHints.TryGetValue(shape.Kind, out var hint) ? hint(shape) : $"CLICK TO AIM: {ShapeName(shape)}");
        else if (weapon?.Innate is { } innate)
            parts.Add($"CLICK {CastSides.GetValueOrDefault(innate.Def.Targets, "TARGET")}: CAST {innate.Def.Name.ToUpperInvariant()}");
        else if (weapon != null)
            parts.Add("CLICK ENEMY: ATTACK");
        if (weapon is { Kind: WeaponKind.Ranged } && active.Value(ModifierType.Overwatch) > 0)
            parts.Add("O: HOLD FIRE");
        parts.Add("SPACE END TURN - E STAIRS - I BAG - TAB/1-4 SWITCH - SCROLL ZOOM - ESC MENU");
        return string.Join(" - ", parts);
    }

    // ── Statuses ─────────────────────────────────────────────────────────────

    /// <summary>Four letters a badge, so a row of them fits the 5-px font.</summary>
    private static readonly IReadOnlyDictionary<StatusEffectType, string> Badges = new Dictionary<StatusEffectType, string>
    {
        [StatusEffectType.Ward] = "WARD",
        [StatusEffectType.Poison] = "POIS",
        [StatusEffectType.Mire] = "MIRE",
        [StatusEffectType.Sundered] = "SUND",
        [StatusEffectType.Weakened] = "WEAK",
        [StatusEffectType.Bleeding] = "BLED",
        [StatusEffectType.Searing] = "SEAR",
        [StatusEffectType.Softened] = "SOFT",
    };

    /// <summary>Full names where the type's own would read badly; every other status is named by its type.</summary>
    private static readonly IReadOnlyDictionary<StatusEffectType, string> StatusNames = new Dictionary<StatusEffectType, string>
    {
        [StatusEffectType.Regeneration] = "REGEN",
    };

    /// <summary>Each status's badge colour: mending green, shielding cyan, each damage-over-time and debuff in its own hue.</summary>
    private static readonly IReadOnlyDictionary<StatusEffectType, Vector4> StatusColors = new Dictionary<StatusEffectType, Vector4>
    {
        [StatusEffectType.Regeneration] = Green,
        [StatusEffectType.Ward] = Cyan,
        [StatusEffectType.Poison] = new(0.62f, 0.85f, 0.25f, 1f),
        [StatusEffectType.Mire] = new(0.8f, 0.64f, 0.38f, 1f),
        [StatusEffectType.Sundered] = new(1f, 0.4f, 0.4f, 1f),
        [StatusEffectType.Weakened] = new(1f, 0.6f, 0.75f, 1f),
        [StatusEffectType.Bleeding] = new(0.9f, 0.2f, 0.25f, 1f),
        [StatusEffectType.Searing] = new(1f, 0.55f, 0.2f, 1f),
        [StatusEffectType.Softened] = new(0.95f, 0.85f, 0.45f, 1f),
    };

    /// <summary>
    /// An element-keyed status by the element that lit it — Flaming lingers as
    /// Burning, Cold as Frostbite, one mechanism with two names (§1.5): its
    /// badge, its full name and its colour. An element with no entry reads as
    /// the status itself.
    /// </summary>
    private static readonly IReadOnlyDictionary<DamageType, (string Badge, string Name, Vector4 Color)> ElementStatuses =
        new Dictionary<DamageType, (string Badge, string Name, Vector4 Color)>
        {
            [DamageType.Flaming] = ("BURN", "BURNING", new Vector4(1f, 0.5f, 0.15f, 1f)),
            [DamageType.Cold] = ("FRST", "FROSTBITE", new Vector4(0.62f, 0.87f, 1f, 1f)),
        };

    /// <summary>Statuses the player never sees: the overheal pool is banked surplus on its way to Ward, not a state to play around.</summary>
    private static readonly IReadOnlySet<StatusEffectType> HiddenStatuses = new HashSet<StatusEffectType> { StatusEffectType.OverhealPool };

    /// <summary>
    /// What a status does, where its levels alone do not say it: Ward's
    /// capacity, Mire's budget left out of the budget it cuts (the second
    /// argument, the caller's to name), the flavour of the two crit riders (§1.6's
    /// table: getting crit opens you up, getting crit rattles your swing), the
    /// Block a Softened target has lost. A status that ticks at its turn's end
    /// is described by its row instead (<see cref="Describe"/>).
    /// </summary>
    private static readonly IReadOnlyDictionary<StatusEffectType, Func<ActorState, float, StatusEffect, string>> Descriptions =
        new Dictionary<StatusEffectType, Func<ActorState, float, StatusEffect, string>>
        {
            [StatusEffectType.Ward] = (_, _, e) => $"ABSORBS THE NEXT {EffectOf(e)} DAMAGE",
            [StatusEffectType.Mire] = (actor, budget, _) => IsParalysed(actor)
                ? "PARALYSED - NO MOVEMENT UNTIL IT DECAYS"
                : $"MOVEMENT {StatusBehaviours.MiredBudget(actor, budget):0} OF {budget:0}",
            [StatusEffectType.Sundered] = (_, _, e) => $"GETTING CRIT OPENS YOU UP - TAKES +{EffectOf(e)} A HIT",
            [StatusEffectType.Weakened] = (_, _, e) => $"GETTING CRIT RATTLES YOUR SWING - DEALS -{EffectOf(e)} A HIT",
            [StatusEffectType.Softened] = (_, _, e) => $"BLOCK -{EffectOf(e)} UNTIL THE ROUND ENDS",
        };

    /// <summary>
    /// An enemy's plate: its hit points out of a maximum its revivals may have
    /// raised, <c>xN</c> once it has been put down at least once, the damage its
    /// revivals have added (<c>+9 DMG</c>), what it carries on itself whatever it
    /// holds (a boss's <c>BLOCK 9</c>, <see cref="InnateReadout"/>), then the same
    /// effect badges a party row carries — riders land on enemies too. A farmed
    /// dummy reads <c>62/74  x7  +9 DMG</c>; the golem <c>80/80  BLOCK 9</c>.
    /// <para>
    /// The count has to read as a threat level and a reward tier at once (§3.2),
    /// "which is honest, because that is exactly what it is": one number saying
    /// both how deep the drop it is carrying has grown and how much harder it
    /// hits than the one beside it. It sits between the pool and the badges so
    /// the two things a player reads before deciding to swing — what is left of
    /// it, and how many times they have done this — are adjacent.
    /// </para>
    /// <para>
    /// <strong>And the threat half is shown, not only implied</strong> (§4.5,
    /// PHASE4-137): "a bare 7 communicates the reward half and hides the half
    /// that kills you". The earned statline stands next to the count — the HP the
    /// farm added is already in the maximum, and the damage it added follows the
    /// count — and the count reddens as it climbs (<see cref="ThreatColor"/>), so
    /// "further is worse" reads with no denominator: the count is unbounded, and
    /// never prints as <c>n/5</c> (PHASE4-139).
    /// </para>
    /// <para>
    /// <c>xN</c> with an ASCII x, the <c>CRIT 19+ x3</c> convention this HUD
    /// already uses: the 5x7 font carries no multiplication sign, and
    /// <see cref="TextRenderer"/> skips a glyph it does not have, so a typographic
    /// one would silently draw the bare number.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<Run> NameplateRuns(EnemyState enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        var plate = new List<Run> { new($"{enemy.Hp}/{enemy.MaxHp}", White) };
        if (enemy.DefeatCount > 0)
            plate.Add(new Run($"x{enemy.DefeatCount}", ThreatColor(enemy.DefeatCount)));
        if (enemy.RevivalDamage > 0)
            plate.Add(new Run($"+{enemy.RevivalDamage} DMG", Red));
        string innate = InnateReadout(enemy);
        if (innate.Length > 0)
            plate.Add(new Run(innate, Stone));
        plate.AddRange(StatusBadgeRuns(enemy));
        return plate;
    }

    /// <summary>
    /// The colour a farmed count is printed in (§3.2, §4.5): orange at the first
    /// defeat, reddening with every one after it, and fully red from
    /// <see cref="Tuning.UniqueChanceMidpoint"/> on — the depth at which a farm is
    /// hot both ways, a coin-flip's unique chance on a dummy that hits
    /// accordingly. Monotonic: a deeper count is never less red. That is how the
    /// plate says "further is worse" without a denominator (PHASE4-139), while
    /// the number itself stays a bare <c>xN</c>.
    /// </summary>
    internal static Vector4 ThreatColor(int defeatCount)
    {
        int hot = Math.Max(2, GameContent.Current.Tuning.UniqueChanceMidpoint);
        float t = Math.Clamp((defeatCount - 1) / (float)(hot - 1), 0f, 1f);
        return Vector4.Lerp(Orange, DeepRed, t);
    }

    /// <summary>
    /// What an actor carries on itself whatever it holds (§4.3's innate
    /// modifiers — the golem's Block), resolved as a hit meets it: weapon plus
    /// innate, in the readout's own words, so a golem holding a hatchet reads
    /// <c>BLOCK 9</c> and one holding a Tower Guard <c>BLOCK 15</c> — the Block its
    /// blows are absorbed by, never its innate stacks alone. Empty for an actor
    /// with nothing innate, which is every dummy.
    /// </summary>
    internal static string InnateReadout(ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var parts = new List<string>();
        foreach (var (type, _) in actor.Innate.Entries)
        {
            string text = ModifierFormats.TryGetValue(type, out var format) && actor.EquippedWeapon is { } held
                ? format(new ReadoutContext(held, actor, Equipped: false, AttacksThisTurn: 0))
                : $"{type.ToString().ToUpperInvariant()} {actor.Value(type)}";
            if (!parts.Contains(text)) // the crit pair reads as one entry
                parts.Add(text);
        }
        return string.Join("  ", parts);
    }

    /// <summary>
    /// The beat over a dummy the moment it gets up stronger (§4.5, PHASE4-138):
    /// what this revival's rolls added — <c>STRONGER +3 DMG +8 HP</c> — so the
    /// player connects the count going up with the statline going up. Null when
    /// a revival added nothing, which none does: every cycle rolls one gain or both.
    /// </summary>
    internal static string? RevivalBeat(int damageGained, int healthGained)
    {
        var gains = new List<string>(2);
        if (damageGained > 0) gains.Add($"+{damageGained} DMG");
        if (healthGained > 0) gains.Add($"+{healthGained} HP");
        return gains.Count == 0 ? null : $"STRONGER {string.Join(" ", gains)}";
    }

    /// <summary>
    /// The actor's statuses as coloured badges in the order they landed —
    /// <c>SUND 2  WEAK 1  MIRE 4  WARD 5</c>, a heal-over-time as <c>+3</c>, a
    /// lingering element by the element that lit it (<c>BURN 3</c>,
    /// <c>FRST 2</c>) — led by <c>PARA</c> when Mire has taken the whole
    /// movement budget. The hidden overheal pool is not shown. Empty when clean.
    /// </summary>
    internal static IReadOnlyList<Run> StatusBadgeRuns(ActorState actor)
    {
        var runs = new List<Run>();
        if (IsParalysed(actor))
            runs.Add(new Run("PARA", Red));
        foreach (var e in actor.StatusEffects)
            if (!HiddenStatuses.Contains(e.Type))
                runs.Add(new Run(BadgeText(e), StatusColor(e)));
        return runs;
    }

    /// <summary>
    /// One line per status on the actor saying what it does to it right now,
    /// in its badge's colour: <c>SUNDERED 2: GETTING CRIT OPENS YOU UP - TAKES
    /// +2 A HIT</c>, <c>BURNING 3: 3 DAMAGE AT TURN END</c>, <c>MIRE 4: MOVEMENT
    /// 60 OF 100</c>. Numbers are resolved against the levels and the tuning;
    /// Mire's against <paramref name="budgetBeforeMire"/>, the movement budget
    /// it cuts, which the caller names for the actor it draws — an enemy's turn
    /// allowance, a party member's allowance plus what it banked — so the
    /// legend never asks the actor its kind. Empty when clean.
    /// </summary>
    internal static IReadOnlyList<Run> StatusLegend(ActorState actor, float budgetBeforeMire)
    {
        var lines = new List<Run>();
        foreach (var e in actor.StatusEffects)
        {
            if (HiddenStatuses.Contains(e.Type)) continue;
            string what = Describe(actor, budgetBeforeMire, e);
            string name = $"{StatusName(e.Type, e.Element)} {e.Levels}";
            lines.Add(new Run(what.Length > 0 ? $"{name}: {what}" : name, StatusColor(e)));
        }
        return lines;
    }

    /// <summary>A status by name — REGEN, WARD, POISON, SUNDERED, and a lingering element by its element (BURNING, FROSTBITE) — for legends, beats and tick text.</summary>
    internal static string StatusName(StatusEffectType type, DamageType? element)
        => ElementStatus(type, element) is { } named
            ? named.Name
            : StatusNames.GetValueOrDefault(type, type.ToString().ToUpperInvariant());

    /// <summary>Whether Mire has cut the actor's movement budget to nothing — the paralysis past its tenth level, released by the same decay (§1.5).</summary>
    internal static bool IsParalysed(ActorState actor) => StatusBehaviours.MiredBudget(actor, 1f) <= 0f;

    /// <summary>A heal-over-time — its row restores HP — keeps the old <c>+N</c>; every other status reads <c>NAME N</c>.</summary>
    private static string BadgeText(StatusEffect e)
    {
        if (StatusRules.Of(e.Type).RestoresHp)
            return $"+{e.Levels}";
        string badge = ElementStatus(e.Type, e.Element) is { } named
            ? named.Badge
            : Badges.GetValueOrDefault(e.Type, e.Type.ToString().ToUpperInvariant());
        return $"{badge} {e.Levels}";
    }

    private static Vector4 StatusColor(StatusEffect e)
        => ElementStatus(e.Type, e.Element) is { } named ? named.Color : StatusColors.GetValueOrDefault(e.Type, White);

    /// <summary>The element-specific face of a status keyed on the element that lit it (the row's flag, not its type), or null.</summary>
    private static (string Badge, string Name, Vector4 Color)? ElementStatus(StatusEffectType type, DamageType? element)
        => StatusRules.KeysOnElement(type) && element is { } lit && ElementStatuses.TryGetValue(lit, out var named) ? named : null;

    /// <summary>What a status does, resolved: its entry in <see cref="Descriptions"/>, else what its row does at its turn's end — heal or harm by the effect of its levels.</summary>
    private static string Describe(ActorState actor, float budgetBeforeMire, StatusEffect e)
    {
        if (Descriptions.TryGetValue(e.Type, out var describe))
            return describe(actor, budgetBeforeMire, e);
        var rule = StatusRules.Of(e.Type);
        if (rule.Trigger == StatusTrigger.TurnEnd && rule.OnTrigger == OnTrigger.TickAndDecrement)
            return rule.RestoresHp ? $"HEALS {EffectOf(e)} AT TURN END" : $"{EffectOf(e)} DAMAGE AT TURN END";
        return "";
    }

    /// <summary>The whole effect of a status's levels: levels times the tuned effect of one.</summary>
    private static int EffectOf(StatusEffect e) => e.Levels * StatusRules.EffectPerLevel(e.Type);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void DrawCentered(int viewportW, float y, string text, float scale, Vector4 color)
        => DrawCenteredAt(viewportW / 2f, y, text, scale, color);

    private void DrawCenteredAt(float cx, float y, string text, float scale, Vector4 color)
    {
        float width = text.Length * GlyphAdvance * scale;
        _text.DrawText(text, cx - width / 2f, y, scale, color);
    }

    /// <summary>Draw runs left to right from <paramref name="x"/>, <see cref="RunGap"/> spaces apart, faded by <paramref name="alpha"/>.</summary>
    private void DrawRuns(float x, float y, float scale, IReadOnlyList<Run> runs, float alpha = 1f)
    {
        foreach (var run in runs)
        {
            _text.DrawText(run.Text, x, y, scale, run.Color with { W = run.Color.W * alpha });
            x += (run.Text.Length + RunGap) * GlyphAdvance * scale;
        }
    }

    private void DrawRunsCenteredAt(float cx, float y, float scale, IReadOnlyList<Run> runs, float alpha = 1f)
        => DrawRuns(cx - RunsWidth(runs, scale) / 2f, y, scale, runs, alpha);

    private static float RunsWidth(IReadOnlyList<Run> runs, float scale)
        => runs.Count == 0 ? 0f : (runs.Sum(r => r.Text.Length) + RunGap * (runs.Count - 1)) * GlyphAdvance * scale;

    private static bool WorldToScreen(Camera camera, Vector3 world, int w, int h, out Vector2 pixels)
    {
        var clip = new Vector4(world, 1f) * (camera.ViewMatrix * camera.ProjectionMatrix);
        if (clip.W <= 0.001f)
        {
            pixels = default;
            return false;
        }
        var ndc = clip.Xyz / clip.W;
        pixels = new Vector2((ndc.X * 0.5f + 0.5f) * w, (1f - (ndc.Y * 0.5f + 0.5f)) * h);
        return true;
    }

    public void Dispose()
    {
        _text.Dispose();
        _fade.Dispose();
    }
}
