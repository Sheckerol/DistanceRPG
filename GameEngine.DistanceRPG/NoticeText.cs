using System.Text;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The one wrapper every notice goes through before it is drawn (1h): what the
/// data files raised at startup — a <c>tuning.json</c> value clamped, a file
/// missing — under the title screen's name or the between-runs title, over the
/// first floor of a dungeon an override boots straight into or a saved run
/// resumes on, or on the screen a
/// broken content file stops the game on, whose own lines it wraps too. Each
/// notice is upper-cased (the HUD's font is capitals) and wrapped at the width
/// the screens' lines wrap at (<see cref="LineChars"/>, the summary's), its
/// continuation rows indented under it; and the whole is capped at
/// <see cref="MaxRows"/> rows plus one saying how many notices did not fit, so a
/// file with fifty mistakes never pushes the dungeon list off the screen. The
/// log has every one of them.
/// <para>
/// <strong>The save's word is shown whole</strong> (S5's review: 1f meets 1h).
/// The screen between runs leads with what became of the campaign — that it
/// resumed, the numbers changed since it was saved (PHASE5-084), a save set
/// aside, by its full path, and how to try it again — and a notice cut off
/// there would hide the one thing the player has to act on. A floor a saved run
/// resumes on leads with the same word, over its banner (S8's review), and so
/// does the title, with what a load that brought nothing back set aside, or why a
/// NEW CAMPAIGN did not begin, each slot whose campaign a quit could not save,
/// and, at launch, what became of the old autosave's move into a slot (one word:
/// moved, not moved, or half moved). Those notices are few by construction (a
/// load sets at most its own slot's two files aside — its save, then its backup —
/// or says only that the slot takes no saves this session, since a campaign saves
/// into the slot it came from and no load touches another slot's files (the
/// user's ruling of 2026-09-25 on the save slots, R5); NEW CAMPAIGN sets two
/// aside, or says why it began nothing, what would not go back, and the quit's
/// word on the slot it took), so the screen hands their count in
/// (<see cref="Rows"/>'s <c>whole</c>) and they are shown in full, up to
/// <see cref="MaxWholeRows"/>; the cap of <see cref="MaxRows"/> is for what
/// follows them, the data files' notices, which a broken file can make as many
/// of as it likes.
/// </para>
/// </summary>
internal static class NoticeText
{
    /// <summary>The most characters a row runs to: the between-runs summary's width, a little over half the shipped window at its scale.</summary>
    public const int LineChars = 64;

    /// <summary>The most rows of notices shown after any shown whole; past them, one more row says how many are in the log.</summary>
    public const int MaxRows = 3;

    /// <summary>
    /// The most rows the notices shown whole take (<see cref="Rows"/>): the save's
    /// word on the campaign. Measured again once the user's ruling on the save slots
    /// was whole (Phase 5.T6, and its review, which added the title's own cases), in
    /// the player's own save folder under a five- and a twenty-letter user name,
    /// each notice naming its file's full path: NEW CAMPAIGN on the title over a
    /// slot in use, its save and backup set aside, takes nine rows; NO NEW CAMPAIGN
    /// BEGAN IN SLOT 2 on the title, its save stuck and its backup not gone back,
    /// eight (ten with the listing's word on the save another program holds), and
    /// with its first save failed and neither file gone back, eleven; a slot resumed
    /// from its backup under other numbers, its save set aside with a long reason,
    /// eight and nine; the title's word on a load that brought nothing back, the
    /// slot's crashed save and unreadable backup set aside, with a slot's
    /// unsaved-quit word that would not clear, twelve and fourteen — fourteen from
    /// an eleven-letter user name up, so a word play can make fills the ceiling.
    /// <c>--new-campaign</c> over a full save folder, both files set aside and the
    /// old autosave's move refused or left in two halves, takes fourteen under both
    /// names. Of the words measured, one passes the ceiling only where several
    /// failures meet: that title word with one more two-row word on it — a second
    /// slot's unsaved-quit word that would not clear, or the listing's word on a
    /// slot's file another program holds — fourteen and sixteen, past it from an
    /// eight-letter name; and under <c>--new-campaign</c>, NO NEW CAMPAIGN BEGAN IN
    /// SLOT 1 with its own unsaved-quit word, the moved autosave's slot's and the
    /// move's word on it, fifteen with its save stuck and its backup not gone back,
    /// nineteen with its first save failed and neither file gone back. A title's
    /// word grows the same way with every further failure — markers that will not
    /// clear, files another program holds. So the ceiling stands: what passes it
    /// needs several failures at once. Past it the rows are cut and counted like
    /// any others, and the log has every word; with <see cref="MaxRows"/> after
    /// them and the row that counts the rest, the dungeon list still clears a
    /// 720-pixel window, and so does a resumed floor's banner, which moves down under
    /// them (<see cref="DungeonHud.BannerLine"/>). settled.md's entry on the save's
    /// word, R2's on saving by hand, and R5's record the ceiling and why.
    /// </summary>
    public const int MaxWholeRows = 14;

    /// <summary>How a notice's continuation rows start, so a wrapped notice reads as one.</summary>
    private const string Indent = "  ";

    /// <summary>
    /// The rows <paramref name="notices"/> are drawn as, in order: each wrapped
    /// under <see cref="LineChars"/>. The first <paramref name="whole"/> of them
    /// are shown in full, up to <see cref="MaxWholeRows"/> rows; the rest take at
    /// most <see cref="MaxRows"/> rows after them. Then <c>+N MORE - SEE THE LOG</c>
    /// for the notices not shown whole. None for none.
    /// </summary>
    /// <param name="notices">The notices, in the order they are shown.</param>
    /// <param name="whole">How many of <paramref name="notices"/>, from the first, are shown whole: the save's word on the campaign, which the screen between runs and a resumed floor lead with. None by default: every notice is capped.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="whole"/> is negative.</exception>
    public static IReadOnlyList<string> Rows(IReadOnlyList<string> notices, int whole = 0)
    {
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentOutOfRangeException.ThrowIfNegative(whole);
        var rows = new List<string>();
        int wholeRows = 0;   // the rows the notices shown whole took: the cap counts from there
        for (int i = 0; i < notices.Count; i++)
        {
            bool shownWhole = i < whole;
            int ceiling = shownWhole ? MaxWholeRows : wholeRows + MaxRows;
            foreach (string row in Wrap(notices[i] ?? ""))
            {
                if (rows.Count == ceiling)
                {
                    rows.Add(More(notices.Count - i));
                    return rows;
                }
                rows.Add(row);
            }
            if (shownWhole)
                wholeRows = rows.Count;
        }
        return rows;
    }

    /// <summary>The last row when notices did not fit: how many, and where they all are.</summary>
    internal static string More(int count) => $"+{count} MORE - SEE THE LOG";

    /// <summary>
    /// One notice — or one line of the screen that stops the game — as rows,
    /// upper-cased, each under <see cref="LineChars"/>: words kept whole where
    /// they fit, a word longer than a row cut, every row after the first
    /// indented. Uncapped: <see cref="Rows"/> is what caps a list of notices.
    /// The HUD's font is capitals and has no backslash — it would draw a gap — so
    /// a backslash is drawn as a slash: a Windows path, such as a set-aside
    /// save's, still reads as one, and Windows reads it the same. Any whitespace
    /// separates words — a line break or a tab included — so no row ever holds
    /// one: a notice is one line, wrapped here and nowhere else.
    /// </summary>
    internal static IReadOnlyList<string> Wrap(string notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var rows = new List<string>();
        var row = new StringBuilder();
        foreach (string word in notice.ToUpperInvariant().Replace('\\', '/').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string rest = word;
            while (rest.Length > 0)
            {
                if (row.Length == 0 && rows.Count > 0)
                    row.Append(Indent);
                bool fresh = row.Length == (rows.Count > 0 ? Indent.Length : 0);
                int room = LineChars - row.Length - (fresh ? 0 : 1);
                if (rest.Length <= room)
                {
                    if (!fresh) row.Append(' ');
                    row.Append(rest);
                    rest = "";
                }
                else if (!fresh)
                {
                    rows.Add(row.ToString());   // the word starts the next row
                    row.Clear();
                }
                else
                {
                    row.Append(rest[..room]);   // longer than a whole row: cut where the row ends
                    rest = rest[room..];
                    rows.Add(row.ToString());
                    row.Clear();
                }
            }
        }
        if (row.Length > (rows.Count > 0 ? Indent.Length : 0))
            rows.Add(row.ToString());
        return rows;
    }
}
