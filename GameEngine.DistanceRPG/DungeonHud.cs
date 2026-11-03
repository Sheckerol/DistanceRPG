using GameEngine.Core;
using GameEngine.Core.UI;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Screen-space HUD for the dungeon scene, drawn with the engine's bitmap
/// <see cref="TextRenderer"/>: movement/weapon readouts, the floor line, the
/// party selector, the enemy nameplates — a boss's among them — and their effect
/// badges, the stairs' labels and the cue at the one underfoot, what lies on the
/// floor (a corpse's drop, or a weapon the party put down), the click and aim
/// cues, floating combat text, the end-of-turn banner, the inventory panel and
/// its "leave" (X, then the slot), the pause menu — close, save by hand, quit to
/// the title, and the slot the run saves itself into (<see cref="PauseMenuLayout"/>,
/// which the screen between runs draws too) — and the way out's prompt with what
/// leaving now means, the stairs' fade and the banner over it (the floor reached,
/// the trade on the boss floor, the boss's fall, <c>RESUMED</c> over a floor a
/// save brought back), and the run's end —
/// its summary, over the defeat screen when the party wiped, in the lines the
/// between-runs screen repeats above the dungeon list (<see cref="BetweenRunsHud"/>).
/// Every number it shows is a resolved one — a swing's cost
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

    // The HUD's palette, which the between-runs screen shares (BetweenRunsHud):
    // a run's summary reads in the same colours on both screens.
    internal static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    internal static readonly Vector4 Yellow = new(1f, 0.87f, 0f, 1f);
    internal static readonly Vector4 Grey = new(0.6f, 0.6f, 0.65f, 1f);
    internal static readonly Vector4 DarkGrey = new(0.4f, 0.4f, 0.42f, 1f);
    internal static readonly Vector4 Red = new(1f, 0.27f, 0.27f, 1f);
    internal static readonly Vector4 Orange = new(0.96f, 0.65f, 0.14f, 1f);
    internal static readonly Vector4 Cyan = new(0.31f, 0.76f, 0.97f, 1f);
    internal static readonly Vector4 Green = new(0.27f, 0.87f, 0.47f, 1f);

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

    /// <summary>A line rising over a world position, and fading as it rises.</summary>
    /// <param name="Speaker">Whoever it is said over, on the answer to a key or a click (<see cref="AddAnswer"/>) and on a beat said of a member (<see cref="AddBeat"/>); null on everything else.</param>
    /// <param name="Answer">Whether it is the speaker's answer, which the next answer replaces.</param>
    private sealed record FloatingText(Vector3 WorldPos, IReadOnlyList<Run> Runs, float PixelYOffset, object? Speaker = null, bool Answer = false)
    {
        public float Age;

        /// <summary>How opaque it is at its age: whole when raised, gone at <see cref="FloatingTextLife"/>.</summary>
        public float Alpha
        {
            get
            {
                float t = Age / FloatingTextLife;
                return 1f - t * t;
            }
        }
    }

    private readonly List<FloatingText> _floatingTexts = new();

    /// <summary>
    /// A banner across the upper third of the screen: a small line over it, the
    /// line itself, any lines of warning under it, and how long it stays up — and,
    /// over a floor a save brought back, <see cref="ResumedLine"/> over all of it
    /// (<see cref="Header"/>). It sits under the notices that were up when it went
    /// up, <see cref="NoticeRows"/> rows lifted <see cref="NoticeLift"/>
    /// (<see cref="BannerLine"/>), and keeps that place until it comes down, so it
    /// never jumps when they fade first.
    /// </summary>
    private sealed record Banner(string Over, string Main, IReadOnlyList<string> Under, float Seconds, string Header = "",
        int NoticeRows = 0, float NoticeLift = 0f);

    /// <summary>What a resumed visit's banner says over the floor banner (1f): <c>RESUMED</c>, where the screen between runs says <c>CAMPAIGN RESUMED FROM SLOT 2</c>.</summary>
    internal const string ResumedLine = "RESUMED";

    /// <summary>How long a resumed floor's banner stays up: long enough to read what it resumed and the trade under it.</summary>
    private const float ResumedBannerSeconds = WarningBannerSeconds;

    /// <summary>The <see cref="ResumedLine"/> header: its scale, and how far over the banner's over-line it sits.</summary>
    private const float HeaderScale = 3f;
    internal const float HeaderLift = 34f;

    /// <summary>How far the banner's over-line (the dungeon's name, on arrival) sits over its main line.</summary>
    internal const float OverLineRise = 30f;

    // The banner up now (ShowArrival, ShowResume, ShowBossFall), and how long it has been up —
    // taken down early (HideArrival) the moment the party starts off the floor it
    // speaks of.
    private Banner? _banner;
    private float _bannerAge;

    /// <summary>How long the startup notices stay over the first floor, and the last stretch of it over which they fade: long enough to read the four rows a capped list runs to (<see cref="NoticeLife"/>).</summary>
    private const float NoticeSeconds = 12f;
    private const float NoticeFadeSeconds = 2f;

    /// <summary>How much longer the notices stay up for each row past a capped list's four: the save's word, shown whole over a resumed floor, can run to a dozen.</summary>
    private const float NoticeSecondsPerRow = 2f;

    /// <summary>The notices' rows: their scale, their pitch, and the gap they keep above the banner's line over it.</summary>
    private const float NoticeScale = 1.5f;
    internal const float NoticePitch = 18f;
    private const float NoticeGap = 12f;

    /// <summary>
    /// The highest a notice row is drawn: under the two readouts along the top —
    /// the MOVE line, and the weapon line at 42 under it, drawn to its first line
    /// alone while a notice is up (<see cref="WeaponLinesShown"/>) — so a notice is
    /// never drawn over them or off the window. When the rows reach it, the banner
    /// moves down to make room (<see cref="BannerLine"/>).
    /// </summary>
    internal const float NoticeTopMargin = 62f;

    // The notices up now (ShowNotices, ShowResume, ShowSaveFailed): what they say
    // and how many of them, from the first, are shown whole — the save's word — so
    // a save that fails can be said among them; the rows NoticeText laid them out
    // as, how long they have been up and how long they stay; and how far they sit
    // over the banner's over-line — clear of RESUMED on a resumed floor, for as
    // long as they are up, which outlasts the banner.
    private IReadOnlyList<string> _notices = [];
    private int _noticesWhole;
    private IReadOnlyList<string> _noticeRows = [];
    private float _noticeAge;
    private float _noticeSeconds;
    private float _noticeLift;

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
        PutUp(new Banner(visit.Def.Name.ToUpperInvariant(), ArrivalBanner(visit), trade,
            trade.Count > 0 ? WarningBannerSeconds : BannerSeconds));
    }

    /// <summary>Put <paramref name="banner"/> up under the notices up now, which it keeps its place under until it comes down (<see cref="Banner"/>).</summary>
    private void PutUp(Banner banner)
    {
        _banner = banner with { NoticeRows = _noticeRows.Count, NoticeLift = _noticeLift };
        _bannerAge = 0f;
    }

    /// <summary>
    /// Put up what a floor a save brought back says (1f, Phase 5.8): the floor's
    /// own arrival banner — the dungeon's name over the floor reached, the trade
    /// under it on the boss floor (<see cref="ShowArrival"/>) — with
    /// <see cref="ResumedLine"/> over it all, up long enough to read; and the
    /// game's notices over that. First the save's word on the campaign —
    /// resumed from its backup, the numbers changed, a file set aside with its
    /// full path and how to try it again — <strong>shown whole</strong>, as the
    /// screen between runs shows it (S8's review: a notice cut off here would hide
    /// the one thing the player has to act on). Then what the data files raised,
    /// capped as every screen caps it (<see cref="NoticeText.Rows"/>), the log
    /// keeping every word. The banner moves down to make room when the word runs
    /// long (<see cref="BannerLine"/>). The load's word that it resumed the slot's
    /// own save, which the screen between runs leads with — <c>CAMPAIGN RESUMED
    /// FROM SLOT 2</c>, <c>... FROM THE OLD AUTOSAVE</c>, whatever slot it names
    /// (<see cref="CampaignBoot.SaysResumedFromItsSave"/>) — is the banner's own
    /// word here, so it is not said twice, and is not counted among the notices
    /// shown whole; resumed from the backup, the word says so, whole.
    /// </summary>
    /// <param name="visit">The visit resumed, standing on the floor it was saved on.</param>
    /// <param name="notices">What the game has to say, in order: the save's word, then what the data files raised.</param>
    /// <param name="leading">How many of <paramref name="notices"/>, from the first, are the save's word on the campaign (<see cref="StartupNotices.Leading"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="leading"/> is negative.</exception>
    public void ShowResume(DungeonState visit, IReadOnlyList<string> notices, int leading)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentOutOfRangeException.ThrowIfNegative(leading);

        var said = new List<string>(notices.Count);
        int whole = 0;
        for (int i = 0; i < notices.Count; i++)
        {
            if (CampaignBoot.SaysResumedFromItsSave(notices[i]))
                continue;   // RESUMED over the banner says it
            if (i < leading)
                whole++;
            said.Add(notices[i]);
        }
        LayNotices(said, whole, HeaderLift);   // first: the banner goes up under them

        ShowArrival(visit);
        _banner = _banner! with { Header = ResumedLine, Seconds = MathF.Max(_banner.Seconds, ResumedBannerSeconds) };
    }

    /// <summary>The banner up now, its lines top to bottom — <see cref="ResumedLine"/> first on a resumed floor, then the line over the banner, the banner, and what is under it — or none once it is down: what the HUD draws, for a test to read with no window.</summary>
    internal IReadOnlyList<string> BannerLines => _banner is not { } banner
        ? []
        : [.. new[] { banner.Header, banner.Over }.Where(line => line.Length > 0), banner.Main, .. banner.Under];

    /// <summary>Where the banner up now is drawn in a window <paramref name="windowHeight"/> pixels tall — under the notices it went up under (<see cref="BannerLine"/>) — or null once it is down: for a test to read with no window.</summary>
    internal float? BannerLineIn(float windowHeight)
        => _banner is { } banner ? BannerLine(windowHeight, banner.NoticeRows, banner.NoticeLift) : null;

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
        PutUp(new Banner("", headline, under, WarningBannerSeconds));
    }

    /// <summary>
    /// Take the banner down before its time — the arrival's, or the boss's fall:
    /// the party is starting off the floor it speaks of, down or up the stairs or
    /// out by the way out. The banner is drawn over the stairs' fade, which is
    /// what puts the next floor's up at black; left up, the floor being left would
    /// go on naming itself on that black — until the swap on the stairs, and
    /// through the whole fade to the screen between runs on the way out. The next
    /// <see cref="ShowArrival"/> puts one up as ever.
    /// </summary>
    public void HideArrival() => _banner = null;

    /// <summary>
    /// Put the startup notices up over the arrival banner — what the data files
    /// raised: a <c>tuning.json</c> value clamped or fitted, a file missing — for a
    /// dungeon the game booted straight into, which never passes the screen
    /// between runs that says them otherwise. Wrapped and capped by
    /// <see cref="NoticeText.Rows"/>, the one wrapper every notice goes through,
    /// and up for as long as they take to read (<see cref="NoticeLife"/>), fading
    /// at the end.
    /// </summary>
    public void ShowNotices(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        LayNotices(notices, whole: 0, lift: 0f);
    }

    /// <summary>
    /// Say over the floor being played, at once, that a save could not be written
    /// (1f): <paramref name="notice"/>, the save's word, shown whole ahead of the
    /// notices up now, all of them up long enough to read from now
    /// (<see cref="NoticeLife"/>). The next screen between runs says it too; here it
    /// is said where the player is, so a run whose saves fail is never played on
    /// as though it were being saved. A notice already up is not said twice — an
    /// autosave that fails at every turn's start fails with the same words — and
    /// only stays up from now. The rows sit clear of <see cref="ResumedLine"/> when
    /// the banner up now carries it, and a banner up now that went up under fewer
    /// rows moves down under these (<see cref="BannerLine"/>), so no row is drawn
    /// over it; it never moves up.
    /// </summary>
    /// <param name="notice">The failed write's notice (<see cref="CampaignBoot.NotSaved"/>), or that of a file the write set aside first (<see cref="CampaignBoot.SetAsideNotice"/>), said alike.</param>
    public void ShowSaveFailed(string notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notice);
        bool up = _noticeRows.Count > 0;
        IReadOnlyList<string> said = up ? _notices : [];
        int whole = up ? _noticesWhole : 0;
        float lift = up ? _noticeLift : _banner is { Header.Length: > 0 } ? HeaderLift : 0f;
        if (said.Take(whole).Contains(notice, StringComparer.Ordinal))
            LayNotices(said, whole, lift);
        else
            LayNotices([notice, .. said], whole + 1, lift);

        if (_banner is { } banner && _noticeRows.Count > banner.NoticeRows)
            _banner = banner with { NoticeRows = _noticeRows.Count, NoticeLift = _noticeLift };
    }

    /// <summary>
    /// Put <paramref name="notices"/> up over the banner as <see cref="NoticeText.Rows"/>
    /// lays them out, the first <paramref name="whole"/> of them shown whole,
    /// <paramref name="lift"/> over its over-line, for as long as they take to read
    /// (<see cref="NoticeLife"/>).
    /// </summary>
    private void LayNotices(IReadOnlyList<string> notices, int whole, float lift)
    {
        _notices = notices;
        _noticesWhole = whole;
        _noticeRows = NoticeText.Rows(notices, whole);
        _noticeAge = 0f;
        _noticeSeconds = NoticeLife(_noticeRows.Count);
        _noticeLift = lift;
    }

    /// <summary>
    /// How long <paramref name="rows"/> rows of notices stay up:
    /// <see cref="NoticeSeconds"/> for a list capped as every screen caps one —
    /// three rows and the one that counts the rest — and
    /// <see cref="NoticeSecondsPerRow"/> more for each row past that, so the
    /// save's word, shown whole over a resumed floor, is up long enough to read.
    /// </summary>
    internal static float NoticeLife(int rows)
        => NoticeSeconds + Math.Max(0, rows - (NoticeText.MaxRows + 1)) * NoticeSecondsPerRow;

    /// <summary>The notice rows up now, as <see cref="ShowNotices"/>, <see cref="ShowResume"/> or <see cref="ShowSaveFailed"/> laid them out: what the HUD draws over the banner.</summary>
    internal IReadOnlyList<string> NoticeRows => _noticeRows;

    /// <summary>How far the notice rows up now sit over the banner's over-line: <see cref="HeaderLift"/> on a resumed floor, clear of <see cref="ResumedLine"/>, and none elsewhere.</summary>
    internal float NoticeLift => _noticeLift;

    /// <summary>
    /// Where the banner's line — the top of its main line — sits in a window
    /// <paramref name="windowHeight"/> pixels tall, under <paramref name="noticeRows"/>
    /// rows of notices lifted <paramref name="noticeLift"/> over its over-line. It
    /// sits a third of the way down, as it always has, while the rows fit between
    /// it and the readouts along the top. When they do not, it sits lower by as
    /// much as they need (<see cref="NoticeTopMargin"/>). The save's word, shown
    /// whole over a resumed floor, can run to a dozen rows: no row of it is drawn
    /// over the readouts, off the window, or over the banner.
    /// </summary>
    internal static float BannerLine(float windowHeight, int noticeRows, float noticeLift)
    {
        float line = windowHeight / 3f;
        if (noticeRows <= 0)
            return line;
        return line + MathF.Max(0f, NoticeTopMargin - NoticesTopOver(line, noticeRows, noticeLift));
    }

    /// <summary>Where the first of <paramref name="noticeRows"/> notice rows sits in a window <paramref name="windowHeight"/> pixels tall: a gap over the banner's over-line, lifted <paramref name="noticeLift"/> more, over a banner at <see cref="BannerLine"/>, and never higher than <see cref="NoticeTopMargin"/>.</summary>
    internal static float NoticesTop(float windowHeight, int noticeRows, float noticeLift)
        => NoticesTopOver(BannerLine(windowHeight, noticeRows, noticeLift), noticeRows, noticeLift);

    /// <summary>Where the first of <paramref name="rows"/> notice rows sits over a banner whose line is <paramref name="line"/>: the last row ends a gap over the over-line, lifted <paramref name="lift"/> more.</summary>
    private static float NoticesTopOver(float line, int rows, float lift)
        => line - OverLineRise - lift - NoticeGap - rows * NoticePitch;

    /// <summary>Drop every floating text in the air: they hang over the floor being left, and nothing on the next floor is where they point.</summary>
    public void ClearFloatingTexts() => _floatingTexts.Clear();

    /// <summary>Spawn combat text above a world position, rising and fading out.</summary>
    public void AddFloatingText(Vector3 worldPos, string text, Vector4 color, float pixelYOffset = 0f)
        => _floatingTexts.Add(new FloatingText(worldPos, [new Run(text, color)], pixelYOffset));

    /// <summary>
    /// Spawn the answer to a key or a click over <paramref name="speaker"/>: why it
    /// did nothing, or what it just did. It takes the place of any answer still
    /// rising over the same speaker, so there is one answer a speaker, the latest.
    /// A refusal and the act that follows it never stack on one spot. Without this,
    /// <c>SLOT 3 IS EMPTY</c> and then <c>LEFT HATCHET HERE</c> would stack over the
    /// member at the middle of the screen, which is where the open bag's panel is.
    /// Every other rising line (a hit, a beat, a level) is left alone. While the
    /// speaker's bag is open the answer is said in the panel, never over the floor
    /// (<see cref="BagLine"/>).
    /// </summary>
    /// <param name="speaker">Whoever the answer is said over: the scene object it rises from.</param>
    public void AddAnswer(object speaker, Vector3 worldPos, string text, Vector4 color, float pixelYOffset)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        _floatingTexts.RemoveAll(ft => ft.Answer && ReferenceEquals(ft.Speaker, speaker));
        _floatingTexts.Add(new FloatingText(worldPos, [new Run(text, color)], pixelYOffset, speaker, Answer: true));
    }

    /// <summary>
    /// Spawn a beat said of <paramref name="speaker"/> that answers no key — a level
    /// it bought — rising as any beat does, beside whatever answer is rising over it,
    /// which it never replaces. A key pressed in the bag can raise one (a drink's heal
    /// buys a point of max HP), so while the speaker's bag is open it is said in the
    /// panel after the answer (<see cref="BagLine"/>), never over the floor.
    /// </summary>
    /// <param name="speaker">The member it is said of: the scene object it rises from.</param>
    public void AddBeat(object speaker, Vector3 worldPos, string text, Vector4 color, float pixelYOffset)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        _floatingTexts.Add(new FloatingText(worldPos, [new Run(text, color)], pixelYOffset, speaker));
    }

    /// <summary>
    /// What the open bag says in its own row (<see cref="InventoryAnswerDrop"/>) for
    /// <paramref name="speaker"/>, the member whose bag it is: the answer still rising
    /// over it (<see cref="AddAnswer"/>), then every beat said of it
    /// (<see cref="AddBeat"/>), oldest first, side by side — each faded as it would
    /// fade in the air, and gone when it would be. Empty when nothing is being said.
    /// <para>
    /// The answer to a key pressed in the bag — a drink, a pour, a weapon left, a
    /// refusal — rises from the member, and the member stands under the middle of the
    /// screen, where the panel is: drawn over the floor it ran into the panel's own
    /// header rows and could not be read (the audit of Phase 6b). So while the bag is
    /// open it is said here, and nothing rises over the floor
    /// (<see cref="RisingLines"/>).
    /// </para>
    /// </summary>
    /// <param name="speaker">The member whose bag is open: the scene object its answers rise from.</param>
    internal IReadOnlyList<Run> BagLine(object speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        var said = _floatingTexts.Where(ft => ReferenceEquals(ft.Speaker, speaker)).OrderByDescending(ft => ft.Answer);   // stable: beats keep their order
        return said.SelectMany(ft => ft.Runs.Select(run => run with { Color = run.Color with { W = run.Color.W * ft.Alpha } })).ToArray();
    }

    /// <summary>
    /// The lines that rise over the floor now, as <see cref="DrawFloatingTexts"/>
    /// draws them: every one — and none while the bag is open
    /// (<paramref name="bagOpen"/>), whose answers are said in the panel
    /// (<see cref="BagLine"/>). A line over the floor would run into the panel's own
    /// rows there: the member stands under it, and the panel has no backing. Each
    /// still ages, so a line still in the air when the bag closes is drawn again.
    /// </summary>
    internal IReadOnlyList<IReadOnlyList<Run>> RisingLines(bool bagOpen) => InTheAir(bagOpen).Select(ft => ft.Runs).ToArray();

    /// <summary>The floating texts <see cref="RisingLines"/> names: every one, and none while the bag is open.</summary>
    private IEnumerable<FloatingText> InTheAir(bool bagOpen) => bagOpen ? [] : _floatingTexts;

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

        if (_noticeRows.Count > 0)
        {
            _noticeAge += deltaTime;
            if (_noticeAge >= _noticeSeconds)
            {
                _notices = [];
                _noticesWhole = 0;
                _noticeRows = [];
            }
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

        DrawTopReadouts(w, active, turns, roomy: !scene.AnyMenuOpen && _noticeRows.Count == 0);
        DrawFloorLine(scene.Visit);
        DrawPartySelector(scene);
        if (ended is null)
        {
            // The world's own labels: the plates, the stairs, what lies on the
            // floor. Every menu is drawn clear of them. Over the dimmed floor they
            // would read as the menu's own lines: a weapon just put down would be
            // named across the bag's slots, and the way out's label would sit in
            // its own question. None of them is what the menu is asking about.
            // The rising lines are drawn clear of the bag too: a key pressed in it
            // answers in the panel's own row (BagLine), since the member it rises
            // from stands under the panel, and an answer over the floor ran into
            // the panel's header rows (the audit of Phase 6b).
            // A run's summary is drawn clear of all of it: the run is over.
            if (!scene.AnyMenuOpen)
            {
                DrawEnemyLabels(camera, scene, w, h);
                DrawStairs(camera, scene, w, h);
                DrawGroundItems(camera, scene, w, h);
            }
            DrawAllyCue(camera, scene, w, h);
            DrawFloatingTexts(camera, w, h, bagOpen: scene.InventoryOpen);
            DrawAimCue(scene);
        }

        if (turns.Phase == TurnPhase.TurnEnding)
        {
            DrawCentered(w, h / 2f - 40f, "END OF TURN!", 4f, Orange);
            if (scene.LastBankedMovement > 0f)
                DrawCentered(w, h / 2f + 8f, $"+{scene.LastBankedMovement:0} SAVED", 2.5f, White);
        }

        if (scene.InventoryOpen)
            DrawInventory(w, h, active, turns, scene.LeaveArmed, BagLine(scene.ActiveCharacter));

        if (scene.PauseMenuOpen)
            DrawMenu(w, h, scene);

        if (scene.ActiveMenu == GameMenu.ConfirmLeave)
            DrawConfirmLeave(w, h, scene.Visit, scene.Party.Sum(member => member.State.Potions.Count()));

        if (ended is not null)
        {
            DrawRunSummary(w, h, ended);   // a wipe's included: its headline is the defeat
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
        DrawNotices(scene, w, h);
        _text.Flush(_game.ShaderManager);
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The two readouts along the top: the MOVE line, and the weapon line under it
    /// (<see cref="TopWeaponLines"/>), as many of its lines as <see cref="WeaponLinesShown"/>
    /// gives — every one while nothing is over the floor (<paramref name="roomy"/>),
    /// and its first alone under a menu or the notices, which the band under it is
    /// kept for.
    /// </summary>
    private void DrawTopReadouts(int w, PartyMemberState active, TurnSystem turns, bool roomy)
    {
        // Mire deep enough to take the whole budget is paralysis, called out beside the budget it emptied.
        var budget = new List<Run> { new($"[{active.Id}] MOVE: {MathF.Ceiling(active.DistLeft)} / {active.EffectiveMax:0}", White) };
        if (IsParalysed(active))
            budget.Add(new Run("PARALYSED", Red));
        budget.Add(new Run($"  {ManaReadout(active)}", White));
        DrawRunsCenteredAt(w / 2f, 14f, 2f, budget);

        if (active.EquippedWeapon == null)
        {
            DrawCentered(w, WeaponLineTop, $"[{active.Id}] (NO WEAPON)", WeaponLineScale, Yellow);
            return;
        }
        var lines = TopWeaponLines(active, turns.AttacksThisTurn(active), w);
        int shown = WeaponLinesShown(lines.Count, roomy);
        for (int i = 0; i < shown; i++)
        {
            var line = i == shown - 1 && shown < lines.Count ? CutShort(lines[i], WeaponLineScale, WeaponLineWidth(w)) : lines[i];
            DrawRunsCenteredAt(w / 2f, WeaponLineTop + i * WeaponLinePitch, WeaponLineScale, line);
        }
    }

    // The weapon line along the top: where its first line sits, under the MOVE
    // line; its scale; the pitch its wrapped lines keep, a line's glyphs and a gap;
    // the margin it keeps from either side of the window, the floor line's; and the
    // clearance its last line keeps over the party selector.
    internal const float WeaponLineTop = 42f;
    internal const float WeaponLineScale = 1.5f;
    internal const float WeaponLinePitch = 16f;
    private const float WeaponLineMargin = 14f;
    private const float WeaponLineClearance = 6f;

    /// <summary>How wide a line of the weapon line may run in a window <paramref name="windowWidth"/> pixels wide: the window, less the margin either side.</summary>
    internal static float WeaponLineWidth(float windowWidth) => windowWidth - 2f * WeaponLineMargin;

    /// <summary>
    /// The most lines the weapon line wraps to on screen: as many as fit between its
    /// first and the party selector down the left, which the wrapped lines, centred
    /// and as wide as the window, would otherwise run through — five.
    /// </summary>
    internal static int WeaponLineMost
        => (int)MathF.Floor((SelectorTop - WeaponLineClearance - WeaponLineTop - GlyphHeight * WeaponLineScale) / WeaponLinePitch) + 1;

    /// <summary>
    /// How many of the weapon line's <paramref name="lines"/> are drawn: all of them,
    /// up to <see cref="WeaponLineMost"/>, while the floor is <paramref name="roomy"/>
    /// — no menu over it and no notice up — and otherwise the first alone. A menu
    /// or a notice keeps the band under the top readouts for itself
    /// (<see cref="InventoryTopMargin"/>, <see cref="SummaryFloor"/>,
    /// <see cref="NoticeTopMargin"/>), and the bag, the one menu that speaks of the
    /// weapon, says it whole. A line cut short ends <c>...</c> (<see cref="CutShort"/>).
    /// </summary>
    internal static int WeaponLinesShown(int lines, bool roomy)
        => Math.Min(lines, roomy ? WeaponLineMost : 1);

    /// <summary>
    /// The weapon in <paramref name="active"/>'s hand as the top line reads it
    /// (<see cref="WeaponRuns"/>, led by the member's tag), wrapped to the window
    /// (<see cref="Wrap"/>): its name, statline, wear and modifiers first, and then
    /// its enchantments, each whole on a line — so a weapon carrying the most there
    /// are, each with its lock and bar and asleep, is read to its last entry, where
    /// one line centred on the window lost both its ends. Empty for an empty hand.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Run>> TopWeaponLines(PartyMemberState active, int attacksThisTurn, float windowWidth)
    {
        ArgumentNullException.ThrowIfNull(active);
        return active.EquippedWeapon is { } weapon
            ? Wrap(WeaponRuns($"[{active.Id}] ", weapon, active, attacksThisTurn, WeaponLineColor(0)), WeaponLineScale, WeaponLineWidth(windowWidth))
            : [];
    }

    /// <summary>
    /// <paramref name="runs"/> as lines no wider than <paramref name="width"/> at
    /// <paramref name="scale"/>, drawn <see cref="RunGap"/> apart: each run on the
    /// line it starts on while it fits there, and otherwise first on the next — a
    /// run is never split. A run wider than a line alone has one to itself.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Run>> Wrap(IReadOnlyList<Run> runs, float scale, float width)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var lines = new List<IReadOnlyList<Run>>();
        var line = new List<Run>();
        foreach (var run in runs)
        {
            if (line.Count > 0 && RunsWidth([.. line, run], scale) > width)
            {
                lines.Add(line);
                line = [];
            }
            line.Add(run);
        }
        if (line.Count > 0)
            lines.Add(line);
        return lines;
    }

    /// <summary>A line with more after it than is drawn: ended <c>...</c> in grey, as many of its last runs dropped as the mark needs to fit <paramref name="width"/>.</summary>
    internal static IReadOnlyList<Run> CutShort(IReadOnlyList<Run> line, float scale, float width)
    {
        ArgumentNullException.ThrowIfNull(line);
        var more = new Run("...", Grey);
        var cut = line.ToList();
        while (cut.Count > 1 && RunsWidth([.. cut, more], scale) > width)
            cut.RemoveAt(cut.Count - 1);
        cut.Add(more);
        return cut;
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
    /// next time (PHASE4-026), said only where the dungeon's range spans more
    /// than one floor (<see cref="DungeonCatalogue.HasFixedDepth"/>), so it never
    /// promises a re-roll that cannot happen. The number is the visit's, never
    /// worked out here: the lock a clear writes is <c>RunCounter + LockRuns</c>,
    /// the same rule, so the promise cannot drift from what is applied. The depth
    /// is the party's to know by now: they are standing on it.
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

        string locks = $"BEATING IT LOCKS {dungeon} FOR {Runs(visit.LockRuns)}";
        return GameContent.Current.Dungeons.HasFixedDepth(visit.Def)
            ? [waits, locks]   // as deep on every visit: leaving re-rolls nothing
            : [waits, locks, "LEAVE WITHOUT IT AND THE DEPTH RE-ROLLS"];
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
        string name = drop.Weapon.ShownName;
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
        return drop.Weapon.Unique ? $"UNIQUE: {drop.Weapon.ShownName}!" : null;
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
    /// that answer it — never a number key, since the pause menu's number keys
    /// save the game and leave play for the title.
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
    /// Never under a menu (<see cref="Render"/>): standing on the way out, its label
    /// would land among the lines under the way out's own question.
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

    /// <summary>"Leave the dungeon?", over the dimmed floor: the question with its keys, then what leaving now means (<see cref="LeavePreview"/>), the party's <paramref name="potionsCarried"/> among it — the last line, what it costs whatever else it means, in grey.</summary>
    private void DrawConfirmLeave(int w, int h, DungeonState visit, int potionsCarried)
    {
        float top = h / 2f - 60f;
        DrawCentered(w, top, LeavePrompt(), 2.5f, Yellow);
        var lines = LeavePreview(visit, potionsCarried);
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
    /// is a clear: <c>THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY</c> — while the
    /// party carries a potion, which spoils on the way out whatever the run was,
    /// <c>... - YOU KEEP THE WEAPONS YOU CARRY</c> (<see cref="ClearKeepsLine"/>) —
    /// and the dungeon closes for good (the tutorial) or locks for as many runs as its
    /// floor (<see cref="DungeonState.LockRuns"/>, the number the clear will
    /// write). With it standing it is a retreat: <c>NO BOSS - A RETREAT: XP KEPT,
    /// THE DEPTH RE-ROLLS</c> — the re-roll said only where there is one, so the
    /// tutorial, two floors on every visit, says <c>NO BOSS - A RETREAT: XP
    /// KEPT</c> (<see cref="DungeonCatalogue.HasFixedDepth"/>) — and whether the
    /// party got deep enough for the run to count (§6.3's mercy, from
    /// <see cref="Tuning.MercyFloor"/>) — a rule, never the depth. Then, when the
    /// party carries any, that its potions spoil on the way out (§6.6, Phase 6b.3:
    /// <see cref="PotionsSpoilLine"/>). And either way, the whole visit
    /// (<see cref="ResetLine"/>), last.
    /// </summary>
    /// <param name="visit">The visit the party is leaving.</param>
    /// <param name="potionsCarried">How many potions the party's bags hold: each spoils as the run ends, so the prompt warns while one is left to drink.</param>
    internal static IReadOnlyList<string> LeavePreview(DungeonState visit, int potionsCarried = 0)
    {
        ArgumentNullException.ThrowIfNull(visit);
        string dungeon = visit.Def.Name.ToUpperInvariant();
        var lines = new List<string>();
        if (visit.BossDefeated)
        {
            string clear = visit.Def.RetiresOnClear
                ? $"{dungeon} CLOSES FOR GOOD"
                : $"{dungeon} LOCKS FOR {Runs(visit.LockRuns)}";
            lines.AddRange([ClearKeepsLine(potionsCarried), clear]);
        }
        else
        {
            var content = GameContent.Current;
            string retreat = content.Dungeons.HasFixedDepth(visit.Def)
                ? "NO BOSS - A RETREAT: XP KEPT"   // as deep on every visit: nothing re-rolls
                : "NO BOSS - A RETREAT: XP KEPT, THE DEPTH RE-ROLLS";
            bool counts = visit.DeepestFloorIndex + 1 >= content.Tuning.MercyFloor;
            lines.AddRange([retreat, counts ? "DEEP ENOUGH TO COUNT AS A RUN" : "TOO SHALLOW TO COUNT AS A RUN"]);
        }
        if (potionsCarried > 0)
            lines.Add(PotionsSpoilLine(potionsCarried));
        lines.Add(ResetLine);
        return lines;
    }

    /// <summary>
    /// The way out's first line with the boss down: what the clear keeps. <c>THE BOSS
    /// IS DOWN - YOU KEEP WHAT YOU CARRY</c>, and while the party carries a potion —
    /// which spoils on the way out, the line under it says (<see cref="PotionsSpoilLine"/>)
    /// — <c>THE BOSS IS DOWN - YOU KEEP THE WEAPONS YOU CARRY</c>, so the two never say
    /// opposite things of the bags (the audit of Phase 6b).
    /// </summary>
    /// <param name="potionsCarried">How many potions the party's bags hold.</param>
    internal static string ClearKeepsLine(int potionsCarried)
        => potionsCarried > 0 ? "THE BOSS IS DOWN - YOU KEEP THE WEAPONS YOU CARRY" : "THE BOSS IS DOWN - YOU KEEP WHAT YOU CARRY";

    /// <summary>What leaving does to the potions carried (§6.6): <c>THE 2 POTIONS CARRIED SPOIL ON THE WAY OUT</c>, or <c>THE POTION CARRIED SPOILS ON THE WAY OUT</c>.</summary>
    internal static string PotionsSpoilLine(int carried)
        => carried == 1 ? "THE POTION CARRIED SPOILS ON THE WAY OUT" : $"THE {carried} POTIONS CARRIED SPOIL ON THE WAY OUT";

    /// <summary>A number of runs as the HUD says it: <c>1 RUN</c>, <c>7 RUNS</c>.</summary>
    internal static string Runs(int n) => $"{n} {(n == 1 ? "RUN" : "RUNS")}";

    // ── The run's end ────────────────────────────────────────────────────────

    /// <summary>A wiped run's headline (§4.4), on its summary and on the defeat screen: the run is over, not the game.</summary>
    internal const string DefeatHeadline = "DEFEATED";

    /// <summary>
    /// The one key a run's end answers: the summary is modal, ENTER moves on to
    /// the between-runs screen, and ESC does nothing there — leaving play is the
    /// menu's to offer, and the screen between runs opens it. What the run wrote is
    /// saved once the frame it ended in is over, the summary with it, so the screen
    /// between runs shows it again when the campaign is next loaded from the title.
    /// </summary>
    internal const string ContinueHint = "ENTER TO CONTINUE";

    /// <summary>
    /// A run's summary as its end shows it over the last floor (§4.4): the run's
    /// own facts, a line a fact and the headline first — the lines the
    /// between-runs screen shows again above the dungeon list
    /// (<see cref="BetweenRunsHud.SummaryLines"/>, where they are written) — and
    /// last the key the panel answers, <c>ENTER TO CONTINUE</c>, never ESC. The
    /// key names <paramref name="next"/>, the next run's dungeon, when a caller
    /// knows it. The dungeon scene names none: ENTER goes on to the between-runs
    /// screen, and the next dungeon is chosen there. The enchanter's services are
    /// named here at most <see cref="DungeonServiceLines"/> — the panel has no cut,
    /// and the hub, which has, names more — and the rest counted.
    /// </summary>
    /// <param name="result">The run's end, as the campaign returned it.</param>
    /// <param name="next">The next run's dungeon, by name; null names none.</param>
    internal static IReadOnlyList<Run> RunSummaryLines(RunResult result, string? next = null)
    {
        var lines = new List<Run>(BetweenRunsHud.SummaryLines(result, DungeonServiceLines));
        lines.Add(new Run(next is null ? ContinueHint : $"{ContinueHint} - NEXT: {next.ToUpperInvariant()}", White));
        return lines;
    }

    /// <summary>How many of the enchanter's finished and worked services the run's end panel names before it says how many more (Phase 6a.3a): the panel shows every line it is handed, so it names fewer than the hub.</summary>
    internal const int DungeonServiceLines = 2;

    // The run's summary panel: the headline's top, and the step to the first line
    // under it; each line's step, and the least it closes up to; the gap over the
    // key; the scales; and the clearance the key keeps over the window's bottom.
    private const float SummaryFloor = 80f;
    private const float SummaryHeadlineStep = 56f;
    private const float SummaryLineStep = 22f;
    private const float SummaryLeastLineStep = 16f;   // a line's glyphs at SummaryLineScale, 11.2 px, and a clear gap under them
    private const float SummaryKeyGap = 12f;
    private const float SummaryHeadlineScale = 5f;
    private const float SummaryLineScale = 1.6f;
    private const float SummaryKeyScale = 2f;
    internal const float SummaryBottomClearance = 16f;

    /// <summary>
    /// Where the run's summary panel starts in a window <paramref name="h"/> pixels
    /// tall, for <paramref name="lines"/> lines, the headline and the key among them:
    /// above the middle, and never over the top readouts — and lifted from there, as
    /// far as the top readouts allow, when a long summary (a wipe's lost haul and
    /// claw-backs, and the enchanter's lines) would take the key past the window's
    /// bottom. The panel has no cut, so every line it is handed is drawn.
    /// </summary>
    internal static float RunSummaryTop(float h, int lines)
        => MathF.Max(SummaryFloor, MathF.Min(h / 2f - 170f, h - SummaryBottomClearance - RunSummaryHeight(h, lines)));

    /// <summary>
    /// The step between the run's summary lines in a window <paramref name="h"/>
    /// pixels tall, for <paramref name="lines"/> lines: the full step, unless the
    /// panel lifted as far as the top readouts allow would still take the key past
    /// the window's bottom — then as close as the lines must sit to end above it,
    /// and never closer than <see cref="SummaryLeastLineStep"/>. Only the longest
    /// wipes close up: every member clawed back in more pools than one line holds,
    /// beside a full haul lost and the enchanter's lines.
    /// </summary>
    internal static float RunSummaryStep(float h, int lines)
    {
        int steps = Math.Max(0, lines - 2);
        if (steps == 0)
            return SummaryLineStep;
        float room = h - SummaryBottomClearance - SummaryFloor - SummaryHeadlineStep - SummaryKeyGap - GlyphHeight * SummaryKeyScale;
        return Math.Clamp(room / steps, SummaryLeastLineStep, SummaryLineStep);
    }

    /// <summary>How tall the run's summary panel is in a window <paramref name="h"/> pixels tall for <paramref name="lines"/> lines, from the headline's top to the key's bottom, the lines at their step (<see cref="RunSummaryStep"/>).</summary>
    internal static float RunSummaryHeight(float h, int lines)
        => SummaryHeadlineStep + Math.Max(0, lines - 2) * RunSummaryStep(h, lines) + SummaryKeyGap + GlyphHeight * SummaryKeyScale;

    /// <summary>
    /// The run's summary over the dimmed floor (<see cref="RunSummaryLines"/>):
    /// the headline large, each line under it, and the key last, a step apart.
    /// Anchored above the middle, never over the top readouts, lifted when a long
    /// one would run off the bottom (<see cref="RunSummaryTop"/>), and closed up
    /// when lifting is not enough (<see cref="RunSummaryStep"/>).
    /// </summary>
    private void DrawRunSummary(int w, int h, RunResult result)
    {
        var lines = RunSummaryLines(result);
        float y = RunSummaryTop(h, lines.Count);
        float step = RunSummaryStep(h, lines.Count);
        DrawCentered(w, y, lines[0].Text, SummaryHeadlineScale, lines[0].Color);
        y += SummaryHeadlineStep;
        for (int i = 1; i < lines.Count; i++)
        {
            bool last = i == lines.Count - 1;
            if (last) y += SummaryKeyGap;
            DrawCentered(w, y, lines[i].Text, last ? SummaryKeyScale : SummaryLineScale, lines[i].Color);
            y += step;
        }
    }

    /// <summary>
    /// The banner, fading over its last <see cref="BannerFadeSeconds"/>: the
    /// line over it (the dungeon's name, on arrival), the line itself (the floor
    /// reached, the boss fallen), and any warning under it (the boss floor's
    /// trade, what the fall means). Not over a menu or the game-over screen, whose
    /// words are at the same height and are the ones being asked about; and never
    /// over the fade away from the floor it speaks of, which takes it down as it
    /// starts (<see cref="HideArrival"/>). A third of the way down, or lower under
    /// notices that need the room (<see cref="BannerLine"/>).
    /// </summary>
    private void DrawBanner(DungeonScene scene, int w, int h)
    {
        if (_banner is not { } banner || scene.AnyMenuOpen || scene.Turns.Phase == TurnPhase.GameOver) return;
        float alpha = Math.Clamp((banner.Seconds - _bannerAge) / BannerFadeSeconds, 0f, 1f);
        float y = BannerLine(h, banner.NoticeRows, banner.NoticeLift);
        if (banner.Header.Length > 0)
            DrawCentered(w, y - OverLineRise - HeaderLift, banner.Header, HeaderScale, Green with { W = alpha });
        if (banner.Over.Length > 0)
            DrawCentered(w, y - OverLineRise, banner.Over, 2f, Grey with { W = alpha });
        DrawCentered(w, y, banner.Main, 5f, White with { W = alpha });
        float underY = y + 52f;
        foreach (string line in banner.Under)
        {
            DrawCentered(w, underY, line, 2f, Yellow with { W = alpha });
            underY += 26f;
        }
    }

    /// <summary>
    /// The notices (<see cref="ShowNotices"/>, <see cref="ShowResume"/>, and a save
    /// that failed, <see cref="ShowSaveFailed"/>),
    /// over the arrival banner — above the line over it, and above
    /// <see cref="ResumedLine"/> on a resumed floor, clear of the top readouts
    /// (<see cref="NoticesTop"/>) — in the warning's orange, fading over their last
    /// <see cref="NoticeFadeSeconds"/>. Like the banner, not over a menu or the
    /// game-over screen.
    /// </summary>
    private void DrawNotices(DungeonScene scene, int w, int h)
    {
        if (_noticeRows.Count == 0 || scene.AnyMenuOpen || scene.Turns.Phase == TurnPhase.GameOver) return;
        float alpha = Math.Clamp((_noticeSeconds - _noticeAge) / NoticeFadeSeconds, 0f, 1f);
        float y = NoticesTop(h, _noticeRows.Count, _noticeLift);
        foreach (string row in _noticeRows)
        {
            DrawCentered(w, y, row, NoticeScale, Orange with { W = alpha });
            y += NoticePitch;
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
        return $"{name} [{enemy.Weapon.ShownName}]";
    }

    /// <summary>How far apart the labels of two items on one tile stack: a label's height and a hair.</summary>
    private const float GroundItemLabelPitch = 13f;

    /// <summary>
    /// What is lying on the floor: over each ground item in sight, the weapon it
    /// is — a drop's with the farm's depth it was collected at, a weapon the party
    /// put down as itself — and, on the one the active member is standing on, the
    /// key that takes it. Nothing is drawn for an item on a fogged tile: it is
    /// exactly as visible as the tile it lies on, and nothing is drawn under a menu
    /// (<see cref="Render"/>). Where several lie on one tile — a full bag's weak
    /// weapon put down on the drop it makes room for — their labels stack upward in
    /// the order <c>G</c> takes them, the next one lowest, just above its key. That
    /// is the order the scene hands them over in
    /// (<see cref="DungeonScene.GroundItems"/>, <see cref="DungeonScene.InTakingOrder"/>),
    /// so the stack and the key cannot disagree.
    /// </summary>
    private void DrawGroundItems(Camera camera, DungeonScene scene, int w, int h)
    {
        var target = scene.PickupTarget;
        var stacked = new Dictionary<(int R, int C), int>();
        foreach (var item in scene.GroundItems)
        {
            if (!item.Marker.IsActive) continue;
            if (!WorldToScreen(camera, item.Marker.Position + Vector3.UnitY * 0.5f, w, h, out var px)) continue;

            int under = stacked.GetValueOrDefault(item.Tile);
            stacked[item.Tile] = under + 1;
            DrawRunsCenteredAt(px.X, px.Y - 14f - under * GroundItemLabelPitch, 1.5f, ItemRuns(item));
            if (item == target)
                DrawCenteredAt(px.X, px.Y + 2f, DungeonScene.PickupHint, 1.4f, White);
        }
    }

    /// <summary>An item on the floor's label, by what it is: a drop's (<see cref="GroundItemRuns"/>), or a weapon the party put down (<see cref="LooseItemRuns"/>).</summary>
    private static IReadOnlyList<Run> ItemRuns(GroundItem item) => item switch
    {
        { Loose: { } loose } => LooseItemRuns(loose),
        { Drop: { } drop } => GroundItemRuns(drop),
        _ => [],   // neither: nothing the scene lays, and nothing to name
    };

    /// <summary>
    /// A weapon the party put down (§4.4), as the floor labels it: its name, a
    /// unique's in the gold every other readout gives it, and nothing else. It
    /// carries no <c>xN</c>, because the farm's depth was the dummy's and not the
    /// weapon's, and no corpse's drop is made up to borrow a label from. It is
    /// labelled as what it is: a weapon someone left here.
    /// </summary>
    internal static IReadOnlyList<Run> LooseItemRuns(LooseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return [new Run(item.Weapon.ShownName, item.Weapon.Unique ? Orange : White)];
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
        var runs = new List<Run> { new(drop.Weapon.ShownName, drop.Weapon.Unique ? Orange : White) };
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

    /// <summary>The lines rising over the floor (<see cref="RisingLines"/>): none while the bag is open, whose answers it says itself.</summary>
    private void DrawFloatingTexts(Camera camera, int w, int h, bool bagOpen)
    {
        foreach (var ft in InTheAir(bagOpen))
        {
            if (!WorldToScreen(camera, ft.WorldPos + Vector3.UnitY * 1.2f, w, h, out var px)) continue;
            float y = px.Y + ft.PixelYOffset - FloatingTextRise * ft.Age / FloatingTextLife;
            DrawRunsCenteredAt(px.X, y, FloatingTextScale, ft.Runs, ft.Alpha);
        }
    }

    /// <summary>A rising line's scale, over the floor and in the bag's answer row alike.</summary>
    internal const float FloatingTextScale = 2f;

    // Inventory panel geometry. The panel was a fixed height until §2.2, so a
    // top fixed at the middle of the window always had room under it. It grows
    // now, by a row per class the member is levelling or holding, and what a
    // short window loses first are the lines under the rows: what a swap costs
    // and which key equips.

    /// <summary>The panel's title, <c>INVENTORY - CHAR A</c>, at the panel's top: its scale.</summary>
    internal const float InventoryTitleScale = 3f;

    /// <summary>The header row under the title — <c>EQUIPPED</c>, and X's hint on the right — how far under the top, and its scale.</summary>
    internal const float InventoryHeaderDrop = 46f;
    internal const float InventoryHeaderScale = 1.2f;

    /// <summary>
    /// The answer row (<see cref="BagLine"/>): how far under the top it sits, in the
    /// band between the title's glyphs and the header row, which nothing else is
    /// drawn in. At <see cref="FloatingTextScale"/> it clears the title by 5 px and
    /// the header row by 6.
    /// </summary>
    internal const float InventoryAnswerDrop = 26f;

    /// <summary>The first slot's line, under the title and the EQUIPPED label.</summary>
    internal const float InventorySlotsTop = 70f;

    /// <summary>A slot: its name, and the statline under it.</summary>
    private const float InventorySlotPitch = 46f;

    /// <summary>
    /// A slot's statline: how far under the name it starts, how far in — past the
    /// slot's number — its scale, and the pitch of each line it wraps to past its
    /// first, which a slot grows by (<see cref="SlotDetailLines"/>).
    /// </summary>
    private const float InventoryDetailDrop = 18f;
    private const float InventoryDetailIndent = 3 * GlyphAdvance * 2f;
    internal const float InventoryDetailScale = 1.3f;
    private const float InventoryDetailPitch = 12f;

    /// <summary>The PROGRESSION label over the rows.</summary>
    private const float InventoryLabelHeight = 16f;

    /// <summary>One pool or proficiency row.</summary>
    private const float InventoryRowPitch = 14f;

    /// <summary>The swap line under the last row, and the key hint under that.</summary>
    private const float InventorySwapGap = 16f;
    private const float InventoryHintGap = 26f;

    /// <summary>What a drink costs, under the swap line while the bag holds a potion (<see cref="DrinkCostLine"/>): the key hint moves down by this.</summary>
    private const float InventoryDrinkPitch = 18f;

    /// <summary>The last line's own glyphs, so a height covers what the panel draws and not just where it starts.</summary>
    private const float InventoryLineHeight = 12f;

    /// <summary>Half the panel's height as it has always been centred, while the window has room for it.</summary>
    private const float InventoryCentredOffset = 150f;

    /// <summary>Clearance kept under the panel for the help line at <c>h - 24</c>.</summary>
    private const float InventoryBottomMargin = 32f;

    /// <summary>Clearance kept over it for the two top readouts, which the panel never slides under: the weapon line is its first line alone while a menu is open (<see cref="WeaponLinesShown"/>).</summary>
    private const float InventoryTopMargin = 56f;

    /// <summary>How far either side of the middle the panel's column runs: its slots start this far left, and the header row's right-hand hint ends this far right.</summary>
    private const float InventoryHalfWidth = 190f;

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
    /// HUD is laid out for either. A slot whose statline wraps
    /// (<see cref="SlotDetailLines"/>) grows the panel by
    /// <paramref name="wrappedLines"/> — every slot's lines past its first —
    /// as a class row does: slid up first, then rows dropped. So does the line
    /// that says what a drink costs, under the swap line while the bag holds a
    /// potion (<paramref name="drinkLine"/>, <see cref="DrinkCostLine"/>).
    /// </summary>
    internal static InventoryLayout LayoutInventory(float windowHeight, int slots, int rowCount, int wrappedLines = 0, bool drinkLine = false)
    {
        float fixedHeight = InventorySlotsTop + slots * InventorySlotPitch + wrappedLines * InventoryDetailPitch + InventoryLabelHeight
            + InventorySwapGap + (drinkLine ? InventoryDrinkPitch : 0f) + InventoryHintGap + InventoryLineHeight;
        float floorLine = windowHeight - InventoryBottomMargin;
        float top = MathF.Min(windowHeight / 2f - InventoryCentredOffset,
                              floorLine - fixedHeight - rowCount * InventoryRowPitch);
        top = MathF.Max(InventoryTopMargin, top);
        int rows = Math.Clamp((int)MathF.Floor((floorLine - top - fixedHeight) / InventoryRowPitch), 0, rowCount);
        return new InventoryLayout(top, rows, top + fixedHeight + rows * InventoryRowPitch);
    }

    /// <summary>How wide a slot's statline may run in a window <paramref name="windowWidth"/> pixels wide: from where it starts, right of the slot's number, to the weapon line's margin from the window's right.</summary>
    internal static float InventoryDetailWidth(float windowWidth)
        => windowWidth - WeaponLineMargin - (windowWidth / 2f - InventoryHalfWidth + InventoryDetailIndent);

    /// <summary>
    /// A bag slot's statline as the bag panel draws it under the weapon's name: the
    /// weapon's runs as they read in <paramref name="member"/>'s hands
    /// (<see cref="WeaponRuns"/>) less the name, which is the line above, wrapped to
    /// <paramref name="width"/> (<see cref="Wrap"/>) — so the weapon in hand's
    /// enchantments, each with its lock and bar, run to their last rather than off
    /// the window's right.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Run>> SlotDetailLines(Weapon weapon, PartyMemberState member, int attacksThisTurn, Vector4 color, float width)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var runs = WeaponRuns("", weapon, member, attacksThisTurn, color);
        runs.RemoveAt(0);   // the name is the line above
        return Wrap(runs, InventoryDetailScale, width);
    }

    /// <summary>
    /// The inventory panel: the member's slots with their statlines, the
    /// progression rows, what a swap costs, and the key hint. With the bag's X
    /// armed (<paramref name="leaving"/>), the key hint asks which weapon to put
    /// down (<see cref="LeaveHint"/>) instead of naming the equip keys. When it is
    /// not armed, the header row names X on the right (<see cref="LeaveKeyHint"/>)
    /// wherever X would arm it: in the party's phase, with a bag to put a weapon
    /// down from. A potion's slot names the potion in its colour
    /// (<see cref="SlotName"/>) and says under it what it restores now
    /// (<see cref="PotionDetail"/>); while the bag holds one both hints say it is
    /// poured out (<see cref="PourKeyHint"/>, PHASE 6b DECISION 1), the key hint
    /// that its number drinks it (<see cref="EquipHint"/>), and a line under the
    /// swap line what a drink costs (<see cref="DrinkCostLine"/>). What a key just
    /// did, or why it did nothing — and any level it bought — is said in a row of its
    /// own under the title (<paramref name="answer"/>, <see cref="BagLine"/>), never
    /// over the floor under the panel.
    /// </summary>
    private void DrawInventory(int w, int h, PartyMemberState active, TurnSystem turns, bool leaving, IReadOnlyList<Run> answer)
    {
        float cx = w / 2f;

        // Under the slots: what every pool and every ladder is doing (§2.2).
        // The statlines above price the items; these rows price the member.
        var rows = new List<Run>();
        foreach (string row in PoolRows(active))
            rows.Add(new Run(row, White));
        foreach (string row in ProficiencyRows(active))
            rows.Add(new Run(row, Cyan));

        // Each weapon's statline as it reads in this member's hands, wrapped to the
        // room right of its slot's number: a slot grows by the lines it wraps to.
        // A potion's says what it restores now, on one line.
        int attacks = turns.AttacksThisTurn(active);
        var details = active.Inventory
            .Select((item, slot) => item switch
            {
                Weapon weapon => SlotDetailLines(weapon, active, attacks, WeaponLineColor(slot), InventoryDetailWidth(w)),
                Potion potion => PotionDetailLines(potion, active),
                _ => [],
            })
            .ToArray();
        bool potions = active.Potions.Any();
        int wrapped = details.Sum(lines => Math.Max(0, lines.Count - 1));

        var layout = LayoutInventory(h, active.Inventory.Length, rows.Count, wrapped, drinkLine: potions);
        float top = layout.Top;
        float left = cx - InventoryHalfWidth;

        DrawCentered(w, top, $"INVENTORY - CHAR {active.Id}", InventoryTitleScale, Cyan);
        DrawRunsCenteredAt(cx, top + InventoryAnswerDrop, FloatingTextScale, answer);   // already faded
        _text.DrawText("EQUIPPED", left, top + InventoryHeaderDrop, InventoryHeaderScale, Grey);
        if (!leaving && turns.Phase == TurnPhase.Player && active.Inventory.Length > 1)
        {
            // Right-aligned on the header row, clear of EQUIPPED and of every slot line under it.
            string hint = potions ? PourKeyHint : LeaveKeyHint;
            float width = hint.Length * GlyphAdvance * InventoryHeaderScale;
            _text.DrawText(hint, cx + InventoryHalfWidth - width, top + InventoryHeaderDrop, InventoryHeaderScale, Grey);
        }

        float y = top + InventorySlotsTop;
        for (int slot = 0; slot < active.Inventory.Length; slot++)
        {
            var item = active.Inventory[slot];
            if (item == null)
            {
                _text.DrawText($"{slot + 1}  - EMPTY -", left, y, 2f, DarkGrey);
                y += InventorySlotPitch;
                continue;
            }

            // The name, then a weapon's statline and modifiers under it as they read in this member's hands.
            var name = SlotName(slot, item);
            _text.DrawText(name.Text, left, y, 2f, name.Color);
            for (int line = 0; line < details[slot].Count; line++)
                DrawRuns(left + InventoryDetailIndent, y + InventoryDetailDrop + line * InventoryDetailPitch, InventoryDetailScale, details[slot][line]);
            y += InventorySlotPitch + Math.Max(0, details[slot].Count - 1) * InventoryDetailPitch;
        }

        float rowY = y;
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

        // A drink costs movement in combat and out (§3.4): said under the swap line while there is one to drink.
        float hintY = swapY + InventoryHintGap;
        if (potions)
        {
            var drink = DrinkCostLine(turns.PotionUseCost, active.DistLeft);
            DrawCentered(w, swapY + InventoryDrinkPitch, drink.Text, 1.5f, drink.Color);
            hintY += InventoryDrinkPitch;
        }

        if (leaving)
            DrawCentered(w, hintY, LeaveHint(active.Inventory.Length, potions), 1.5f, Yellow);
        else
            DrawCentered(w, hintY, EquipHint(active.Inventory.Length, potions), 1.5f, Grey);
    }

    /// <summary>
    /// What the bag panel prints for a slot that holds something: its number — the
    /// key that reaches it, so the hand is 1 — and the item's name, in the item's
    /// colour. A weapon is orange when it is a unique and otherwise in its line's
    /// colour (<see cref="WeaponLineColor"/>); a potion is green for health and cyan
    /// for mana (<see cref="PotionColor"/>): <c>3  HEALTH POTION Q2</c>.
    /// </summary>
    /// <param name="slot">The slot, 0 being the hand.</param>
    /// <param name="item">What lies there.</param>
    internal static Run SlotName(int slot, IItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var color = item switch
        {
            Weapon weapon => weapon.Unique ? Orange : WeaponLineColor(slot),
            Potion potion => PotionColor(potion.Type),
            _ => White,
        };
        return new Run($"{slot + 1}  {item.ShownName}", color);
    }

    /// <summary>A potion's colour wherever the dungeon names it: green for health, cyan for mana, the colours of the two pools it fills.</summary>
    internal static Vector4 PotionColor(PotionType type) => type == PotionType.Health ? Green : Cyan;

    /// <summary>
    /// What a potion in the bag restores now, under its name (§3.4): the share it
    /// offers of <paramref name="member"/>'s maximum as it stands
    /// (<see cref="Alchemist.Share(Potion, ActorState)"/>) — <c>RESTORES 12 OF 30
    /// HP</c>, <c>RESTORES 32 OF 160 MANA</c> — and at a whole pool,
    /// <c>RESTORES ALL 30 HP</c>, or past it, <c>RESTORES ALL 30 HP - 6 OVER</c>:
    /// the overshoot, which only Overheal keeps. Mana is a share of the pool the
    /// member has earned (PHASE 6b DECISION 3), but a drink fills no more than the
    /// equipped locks leave spendable (<see cref="ActorState.UsableMaxMana"/>), so
    /// where they reserve some the line is said against that pool — <c>RESTORES 12
    /// OF 17 SPENDABLE MANA</c>, <c>RESTORES ALL 17 SPENDABLE MANA - 15 OVER</c> —
    /// the share unchanged (the audit of Phase 6b).
    /// </summary>
    internal static string PotionDetail(Potion potion, ActorState member)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(member);
        int share = Alchemist.Share(potion, member);
        int max = Alchemist.MaxOf(potion.Type, member);
        string pool = potion.Type == PotionType.Health ? "HP" : "MANA";
        if (potion.Type == PotionType.Mana && member.UsableMaxMana < max)
            (max, pool) = (member.UsableMaxMana, "SPENDABLE MANA");   // what the drink can take: TurnSystem.TryDrink's cap
        if (share < max)
            return $"RESTORES {share} OF {max} {pool}";
        return share == max ? $"RESTORES ALL {max} {pool}" : $"RESTORES ALL {max} {pool} - {share - max} OVER";
    }

    /// <summary>A potion's slot's detail as the bag panel draws it under the name: <see cref="PotionDetail"/>, one line in the potion's colour.</summary>
    internal static IReadOnlyList<IReadOnlyList<Run>> PotionDetailLines(Potion potion, ActorState member)
        => [[new Run(PotionDetail(potion, member), PotionColor(potion.Type))]];

    /// <summary>
    /// What a drink costs, under the swap line while the bag holds a potion: a
    /// drink costs <paramref name="cost"/> movement in combat and out (PHASE 6b
    /// DECISION 2) — <c>A DRINK COSTS 40 MOVE</c>, grey; red and
    /// <c>... - NOT ENOUGH LEFT</c> when the member has less, since the cost is
    /// paid whole or the drink refused; <c>DRINKING IS FREE</c> at a cost of 0.
    /// </summary>
    internal static Run DrinkCostLine(int cost, float distLeft)
    {
        if (cost <= 0) return new Run("DRINKING IS FREE", Grey);
        return distLeft < cost
            ? new Run($"A DRINK COSTS {cost} MOVE - NOT ENOUGH LEFT", Red)
            : new Run($"A DRINK COSTS {cost} MOVE", Grey);
    }

    /// <summary>
    /// The beat over a member who has just drunk: the potion, and what the pool
    /// gained — <c>DRANK HEALTH POTION Q2 - +12 HP</c>, <c>DRANK MANA POTION Q1 -
    /// +6 MANA</c> — or, at a full pool, <c>DRANK HEALTH POTION Q2 - NOTHING TO
    /// RESTORE</c>: drinking at full is allowed, and wasted (§3.4).
    /// </summary>
    internal static string DrankBeat(PotionDrink drink)
    {
        ArgumentNullException.ThrowIfNull(drink);
        string gained = drink.Restored > 0
            ? $"+{drink.Restored} {(drink.Potion.Type == PotionType.Health ? "HP" : "MANA")}"
            : "NOTHING TO RESTORE";
        return $"DRANK {drink.Potion.ShownName} - {gained}";
    }

    /// <summary>What a potion's number says over the member when the drink's whole cost is not left (§3.4): <c>NOT ENOUGH MOVE TO DRINK - IT COSTS 40</c>. Nothing is drunk, and nothing spent.</summary>
    internal static string DrinkShortCue(int cost) => $"NOT ENOUGH MOVE TO DRINK - IT COSTS {cost}";

    /// <summary>
    /// The inventory panel's key hint: the number keys that equip a bag slot —
    /// each the number the panel prints beside that slot, so the hand is 1 and
    /// the bag runs from 2 — then the key that closes the bag. For §4.4's six
    /// slots, <c>PRESS 2-6 TO EQUIP - I TO CLOSE</c>. The range is read off the
    /// member's slots and the scene's own highest binding
    /// (<see cref="DungeonScene.HighestSlotKey"/>), so it names every slot a key
    /// reaches and no key that does nothing. While the bag holds a potion
    /// (<paramref name="potion"/>) the same keys drink it:
    /// <c>PRESS 2-6 TO EQUIP OR DRINK - I TO CLOSE</c>.
    /// </summary>
    /// <param name="slots">The member's slots, the hand among them.</param>
    /// <param name="potion">Whether the bag holds a potion.</param>
    internal static string EquipHint(int slots, bool potion = false)
        => BagKeys(slots) is { } keys ? $"PRESS {keys} TO {(potion ? "EQUIP OR DRINK" : "EQUIP")} - I TO CLOSE" : "I TO CLOSE";   // a hand and no bag: nothing to swap in

    /// <summary>
    /// The number keys that reach a bag slot, as a hint prints them — <c>2-6</c>
    /// for §4.4's six slots, <c>2</c> for a bag of one — or null for a hand and no
    /// bag. Each key is the number the panel prints beside its slot, so the hand
    /// is 1 and never among them. The range stops at the scene's highest binding
    /// (<see cref="DungeonScene.HighestSlotKey"/>), so it names no key that does
    /// nothing. Equipping and putting down read the same keys
    /// (<see cref="EquipHint"/>, <see cref="LeaveHint"/>).
    /// </summary>
    private static string? BagKeys(int slots)
    {
        int last = Math.Min(slots, DungeonScene.HighestSlotKey);
        return last switch
        {
            < 2 => null,
            2 => "2",
            _ => $"2-{last}",
        };
    }

    /// <summary>
    /// The bag's key hint while its X is armed (§4.4): which weapon to put down,
    /// by the same number keys that equip, and how to back out. For six slots it
    /// is <c>LEAVE WHICH? 2-6 - X CANCELS</c>. It replaces <see cref="EquipHint"/>
    /// on the panel for as long as X is armed. It names no key for the hand,
    /// because the weapon in hand is never put down this way. While the bag holds
    /// a potion (<paramref name="potion"/>) it says the same keys pour one out —
    /// <c>LEAVE OR POUR OUT WHICH? 2-6 - X CANCELS</c> — since a potion is never
    /// left (PHASE 6b DECISION 1).
    /// </summary>
    /// <param name="slots">The member's slots, the hand among them.</param>
    /// <param name="potion">Whether the bag holds a potion.</param>
    internal static string LeaveHint(int slots, bool potion = false)
        => BagKeys(slots) is { } keys
            ? $"{(potion ? "LEAVE OR POUR OUT WHICH?" : "LEAVE WHICH?")} {keys} - X CANCELS"
            : "NOTHING TO LEAVE - X CANCELS";

    /// <summary>
    /// What the panel's header row says while X is not armed, so the key can be
    /// found at all: X puts a weapon down where the member stands (§4.4). It is
    /// the way a full bag makes room.
    /// </summary>
    internal const string LeaveKeyHint = "X TO LEAVE ONE HERE";

    /// <summary>
    /// <see cref="LeaveKeyHint"/> while the bag holds a potion: X puts a weapon
    /// down, or pours a potion out — gone, never left on the floor (PHASE 6b
    /// DECISION 1) — and either makes room.
    /// </summary>
    internal const string PourKeyHint = "X TO LEAVE OR POUR ONE OUT";

    /// <summary>The beat over a member who has just poured a potion out: <c>POURED OUT THE HEALTH POTION Q2</c>. It is gone: nothing lies on the floor, and nothing comes back.</summary>
    internal static string PouredBeat(Potion potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        return $"POURED OUT THE {potion.ShownName}";
    }

    /// <summary>
    /// What an armed bag number says when its slot holds nothing: <c>SLOT 3 IS
    /// EMPTY</c>, by the number the panel prints for <paramref name="slot"/> (the
    /// key that was pressed). X stays armed for another pick.
    /// </summary>
    /// <param name="slot">The slot, 0 being the hand.</param>
    internal static string EmptySlotCue(int slot) => $"SLOT {slot + 1} IS EMPTY";

    /// <summary>
    /// What <c>G</c> says when the bag has no room (§4.4): a carry limit, not a
    /// fault — <c>BAG FULL - 6 CARRIED</c>, loot sharing the slots the weapons are
    /// carried in — and how to make room: open the bag and put one down. While the
    /// bag holds a potion (<paramref name="potion"/>) X pours one out too, as the
    /// panel's hints say (<see cref="PourKeyHint"/>): <c>BAG FULL - 6 CARRIED - I
    /// THEN X LEAVES OR POURS ONE OUT</c>, since a potion is never left (PHASE 6b
    /// DECISION 1).
    /// </summary>
    /// <param name="carried">The member's slots, every one of them full.</param>
    /// <param name="potion">Whether the bag holds a potion.</param>
    internal static string BagFullCue(int carried, bool potion = false)
        => $"BAG FULL - {carried} CARRIED - I THEN X {(potion ? "LEAVES OR POURS ONE OUT" : "LEAVES ONE")}";

    /// <summary><see cref="BagFullCue(int, bool)"/> for <paramref name="member"/>'s full bag: its slots, and whether one of them holds a potion.</summary>
    internal static string BagFullCue(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return BagFullCue(member.Inventory.Length, member.Potions.Any());
    }

    /// <summary>The beat over a member who has just put a weapon down: <c>LEFT HATCHET HERE</c>, so it reads as a thing done on purpose, answered by <c>TOOK HATCHET</c> when it is taken back.</summary>
    internal static string LeftBeat(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return $"LEFT {weapon.ShownName} HERE";
    }

    // ── The menu: the pause menu, and the same menu between runs ─────────────

    /// <summary>
    /// What the menu says under its options (1f): the run saves itself at every
    /// turn's start and every arrival — into the slot the campaign came from, which
    /// SAVE GAME saves too, so the player always knows which slot is theirs (the
    /// user's ruling of 2026-09-25 on the save slots, R5): <c>PROGRESS SAVES TO SLOT
    /// 2 AT EVERY TURN</c>, <c>... TO THE OLD AUTOSAVE ...</c>, <c>... TO THE SCRATCH
    /// SLOT ...</c>. With no slot to name, <c>PROGRESS SAVES AT EVERY TURN</c>.
    /// </summary>
    /// <param name="savesInto">The slot the campaign saves into, by its name (<see cref="ISaveHost.SavesInto"/>).</param>
    internal static string PauseSavesLine(string savesInto)
        => string.IsNullOrWhiteSpace(savesInto) ? "PROGRESS SAVES AT EVERY TURN" : $"PROGRESS SAVES TO {savesInto} AT EVERY TURN";

    /// <summary>The menu's title, over its options.</summary>
    internal const string PausedTitle = "PAUSED";

    /// <summary>Under the menu's options and its line: ESC closes it.</summary>
    internal const string EscToClose = "ESC TO CLOSE";

    /// <summary>A glyph's height at scale 1: the font is 5 x 7.</summary>
    private const float GlyphHeight = 7f;

    // The menu's block, laid out from its top: the title, the options a pitch
    // apart from where they start, the two lines under the last option, and the
    // answer under them. The block sits where the menu always has, a little above
    // the middle, and rises as far as it needs.
    private const float MenuTitleScale = 4f;
    private const float MenuOptionsTop = 66f;
    private const float MenuOptionPitch = 40f;
    private const float MenuOptionScale = 2f;
    private const float MenuLineScale = 1.5f;
    private const float MenuLinePitch = 24f;
    private const float MenuAnswerGap = 30f;
    private const float MenuAnswerPitch = 18f;
    private const float MenuAnswerScale = 1.5f;
    private const float MenuCentredOffset = 90f;

    /// <summary>The highest the menu's block rises, and the room it keeps along the bottom — clear of the dungeon's help line at <c>h - 24</c> and the hub's keys at <c>h - 28</c>.</summary>
    internal const float MenuTopMargin = 16f;
    internal const float MenuBottomMargin = 32f;

    /// <summary>One row of the menu as a screen draws it, centred on the window: its text, how far down, and its scale and colour.</summary>
    internal readonly record struct MenuRow(string Text, float Y, float Scale, Vector4 Color)
    {
        /// <summary>The row's width in pixels.</summary>
        public float Width => Text.Length * GlyphAdvance * Scale;

        /// <summary>Where the row's glyphs end, downward.</summary>
        public float Bottom => Y + GlyphHeight * Scale;
    }

    /// <summary>
    /// The menu, row by row, in a window <paramref name="w"/> by <paramref name="h"/>
    /// (the user's ruling of 2026-09-25 on the save model, R2(d), R2(e)): the pause
    /// menu, and the same menu over the hub between runs (<see cref="PauseMenu"/>).
    /// The options under <see cref="PausedTitle"/>, a pitch apart —
    /// <see cref="PauseMenu.SaveLine"/> dimmed where nothing can be saved by hand
    /// (<paramref name="canSave"/>), 2 then saying why — and <strong>their two lines
    /// under the last of them</strong>, however many there are: the slot the
    /// campaign saves into (<paramref name="savesLine"/>, <see cref="PauseSavesLine"/>),
    /// then <see cref="EscToClose"/>. Then the answer (<paramref name="answer"/>) —
    /// what a save by hand came to, or why 2 saved nothing — wrapped as a notice is,
    /// green for a save that went through and orange otherwise
    /// (<see cref="AnswerColor"/>), inside the menu: a notice is never drawn over a
    /// menu, and would age out unseen behind it. SAVE GAME opens no list: it saves
    /// the campaign's own slot (the user's ruling of 2026-09-25 on the save slots,
    /// R5). The block sits where the menu always has, a little above the middle, and
    /// rises as far as it needs to end clear of the line along the bottom — never
    /// above <see cref="MenuTopMargin"/>. Pure, so what is drawn is tested
    /// (<c>SaveMenuTests</c>); only the drawing is not.
    /// </summary>
    /// <param name="w">The window's width, in pixels.</param>
    /// <param name="h">The window's height, in pixels.</param>
    /// <param name="options">The menu's options, top to bottom (<see cref="PauseMenu.Options"/>).</param>
    /// <param name="canSave">Whether 2 can save now: a safe point.</param>
    /// <param name="savesLine">The first line under the options: where the campaign saves (<see cref="PauseSavesLine"/>).</param>
    /// <param name="answer">What the menu answers under its lines, or null for nothing.</param>
    internal static IReadOnlyList<MenuRow> PauseMenuLayout(float w, float h, IReadOnlyList<string> options, bool canSave, string savesLine, string? answer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(savesLine);

        // Laid out from a top of 0, then moved down to where the block sits.
        var rows = new List<MenuRow> { new(PausedTitle, 0f, MenuTitleScale, Cyan) };
        for (int i = 0; i < options.Count; i++)
            rows.Add(new MenuRow(options[i], MenuOptionsTop + i * MenuOptionPitch, MenuOptionScale,
                options[i] == PauseMenu.SaveLine && !canSave ? DarkGrey : White));
        float lines = MenuOptionsTop + options.Count * MenuOptionPitch;
        rows.Add(new MenuRow(savesLine, lines, MenuLineScale, Grey));
        rows.Add(new MenuRow(EscToClose, lines + MenuLinePitch, MenuLineScale, Grey));
        if (answer is not null)
        {
            var color = AnswerColor(answer);
            float y = lines + MenuLinePitch + MenuAnswerGap;
            foreach (string row in NoticeText.Wrap(answer))
            {
                rows.Add(new MenuRow(row, y, MenuAnswerScale, color));
                y += MenuAnswerPitch;
            }
        }

        float bottom = rows.Max(row => row.Bottom);
        float top = MathF.Max(MenuTopMargin, MathF.Min(h / 2f - MenuCentredOffset, h - MenuBottomMargin - bottom));
        return rows.Select(row => row with { Y = row.Y + top }).ToList();
    }

    /// <summary>The answer's colour: green for a save by hand that went through (<see cref="CampaignBoot.SavedTo"/>), the warning's orange for one that did not, or for 2 saying why it saved nothing.</summary>
    internal static Vector4 AnswerColor(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.StartsWith(CampaignBoot.SavedPrefix, StringComparison.Ordinal) ? Green : Orange;
    }

    /// <summary>
    /// The pause menu (<see cref="PauseMenuLayout"/>): its options, the slot the
    /// campaign saves into, and what a save by hand came to. Stopping resurrection is the
    /// boss's doing (§4.3), and nothing else's — the dev switch that stood in for it
    /// went when the boss arrived — so what the dungeon has become is the floor
    /// line's to say (<see cref="FloorLine"/>).
    /// </summary>
    private void DrawMenu(int w, int h, DungeonScene scene)
    {
        foreach (var row in PauseMenuLayout(w, h, PauseMenu.Options(scene.HasTitle), scene.CanSaveNow, PauseSavesLine(scene.SavesInto), scene.MenuAnswer))
            _text.DrawText(row.Text, w / 2f - row.Width / 2f, row.Y, row.Scale, row.Color);
    }

    // ── Weapon readouts ──────────────────────────────────────────────────────

    /// <summary>
    /// A weapon as runs, read as one line where it fits: its name (a unique's in
    /// gold) and resolved statline, its wear against its capacity
    /// (<see cref="WearReadout"/>: in the line's colour, and green once full,
    /// <see cref="WearColor"/>), its modifiers, then its enchantments in attachment
    /// order — a caster's innate, a unique's souls. Each modifier and each
    /// enchantment is a run of its own, the first of each led <c>* </c>, so a line
    /// too wide for the window wraps between entries (<see cref="Wrap"/>) and never
    /// through one; side by side, <see cref="RunGap"/> apart, they read as the
    /// readouts join them.
    /// </summary>
    internal static List<Run> WeaponRuns(string prefix, Weapon weapon, ActorState? wielder, int attacksThisTurn, Vector4 color)
    {
        var runs = new List<Run>
        {
            new($"{prefix}{weapon.ShownName}", weapon.Unique ? Orange : color),
            new(WeaponStats(weapon), color),
            new(WearReadout(weapon), WearColor(weapon, color)),
        };
        runs.AddRange(ModifierEntries(weapon, wielder, attacksThisTurn).Select((entry, i) => new Run(i == 0 ? $"* {entry}" : entry, White)));
        runs.AddRange(EnchantmentEntries(weapon, wielder).Select((entry, i) => new Run(i == 0 ? $"* {entry}" : entry, Cyan)));
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
    /// A weapon's wear as the weapon line reads it (§6.1): the wear banked over
    /// the capacity, <c>WEAR 12/31</c>, and <c>WEAR 31/31 FULL</c> once it is full
    /// — a weapon that banks nothing more until the enchanter spends it, and plays
    /// on exactly as it did. Wear counts one way, the work done: the game never
    /// says durability (P6:51–65). A capacity a tuning lowered below the wear
    /// reads full too, over its old wear: nothing takes wear away.
    /// </summary>
    internal static string WearReadout(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return $"WEAR {weapon.Wear}/{weapon.WearCapacity}{(weapon.WornFull ? " FULL" : "")}";
    }

    /// <summary>
    /// The colour a weapon's wear reads in on a line drawn in
    /// <paramref name="line"/>: short of full, the line's own, so it reads as
    /// part of the statline; once full, green, so the cap says so plainly on
    /// every line a weapon is drawn on (P6:81–85) — the yellow line of the weapon
    /// in hand (<see cref="WeaponLineColor"/>), the only one that banks wear and
    /// so where a weapon fills, as on a white one — and never a unique's orange
    /// or an enchantment's cyan.
    /// </summary>
    internal static Vector4 WearColor(Weapon weapon, Vector4 line) => weapon.WornFull ? Green : line;

    /// <summary>The colour a weapon's line reads in, by its bag slot: the weapon in hand (slot 0) yellow, on the top line and in the bag alike, the rest white.</summary>
    internal static Vector4 WeaponLineColor(int slot) => slot == 0 ? Yellow : White;

    /// <summary>
    /// A weapon's wear said whole, for a panel with room for a sentence: how far
    /// it is from full — <c>WEAR 12 OF 31 - 19 MORE FILLS IT</c> — or, once full,
    /// what to do about it: <c>WEAR 31 OF 31 - FULL - TIME TO ROTATE IT INTO THE
    /// SHOP</c> (P6:81–85, "a weapon at its cap should say so plainly"). Drawn in
    /// <see cref="WearColor"/>.
    /// </summary>
    internal static string WearLine(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int capacity = weapon.WearCapacity;
        return weapon.WornFull
            ? $"WEAR {weapon.Wear} OF {capacity} - FULL - TIME TO ROTATE IT INTO THE SHOP"
            : $"WEAR {weapon.Wear} OF {capacity} - {capacity - weapon.Wear} MORE FILLS IT";
    }

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
        => string.Join("  ", ModifierEntries(weapon, wielder, attacksThisTurn));

    /// <summary>The entries <see cref="ModifierReadout"/> joins, each on its own: <c>BLOCK 6</c>, <c>CRIT 19+ x3</c>.</summary>
    internal static IReadOnlyList<string> ModifierEntries(Weapon weapon, ActorState? wielder = null, int attacksThisTurn = 0)
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
        return parts;
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
        => member.Inventory.Any(item => item is Weapon weapon && weapon.Class == cls);

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
        => string.Join("  ", EnchantmentEntries(weapon, wielder));

    /// <summary>The entries <see cref="EnchantmentReadout"/> joins, each on its own: <c>VAMPIRIC T3 LOCK 60 XP 0/60</c>.</summary>
    internal static IReadOnlyList<string> EnchantmentEntries(Weapon weapon, ActorState? wielder = null)
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
        return parts;
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
