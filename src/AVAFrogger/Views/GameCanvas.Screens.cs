using Avalonia;
using Avalonia.Media;
using AVAFrogger.Engine;
using AVAFrogger.Models;

namespace AVAFrogger.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: the scores and spare frogs on the left; the level and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "1UP", new Point(lx, 60), 30, Yellow, center: true);
        Text(dc, (playing ? g.Score : 0).ToString("D5"), new Point(lx, 92), 34, Red, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 160), 30, Yellow, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D5"), new Point(lx, 192), 34, Red, center: true);

        if (playing)
        {
            int spare = g.Lives - (g.Phase is Phase.Dying ? 0 : 1);
            for (int i = 0; i < Math.Min(spare, 5); i++) DrawPanelSprite(dc, Sprites.FrogSit, lx - 56 + i * 28, 690);
            Text(dc, "LEVEL", new Point(rx, 60), 30, Yellow, center: true);
            Text(dc, g.Level.ToString(), new Point(rx, 92), 34, White, center: true);
            Text(dc, "HOME", new Point(rx, 160), 30, Yellow, center: true);
            Text(dc, $"{g.Bays.Count(b => b.Filled)} / 5", new Point(rx, 192), 34, Green, center: true);
        }
        Text(dc, "AVAFROGGER", new Point(rx, 560), 24, Dim, center: true);
        Text(dc, "Arrows or WASD hop", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Enter or Space starts", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 1.5;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xE0000010), new Rect(P(10, top), P(Field.Width - 10, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 4, 252);
        Text(dc, "AVA FROGGER", P(cx, 22), 80, Green, center: true);
        Text(dc, "AFTER THE 1981 ARCADE CLASSIC", P(cx, 37), 26, Dim, center: true);
        DrawSprite(dc, Sprites.FrogSit, cx - 8, 46);
        Text(dc, "GET FIVE FROGS SAFELY HOME", P(cx, 72), 30, White, center: true);
        Text(dc, "ACROSS THE ROAD, THEN OVER THE RIVER", P(cx, 82), 24, White, center: true);
        Row(Sprites.FrogSit, 100, "EACH HOP FORWARD", "10");
        Row(Sprites.FrogHome, 113, "HOME, PLUS 10 PER HALF-SECOND LEFT", "50+");
        Row(Sprites.Fly, 126, "A FLY IN THE HOME", "200");
        Row(Sprites.LadySit, 139, "BRING THE LADY FROG HOME", "200");
        Row(Sprites.FrogHome, 152, "ALL FIVE HOME", "1000");
        Text(dc, "DON'T FALL IN THE RIVER, DON'T GET RUN OVER,", P(cx, 170), 24, Yellow, center: true);
        Text(dc, "AND MIND THE TURTLES THAT DIVE", P(cx, 180), 24, Yellow, center: true);
        Text(dc, "BONUS FROG AT 20000", P(cx, 194), 26, Cyan, center: true);
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 216), 32, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 240), 22, Dim, center: true);

        void Row(Sprites.Sprite s, double y, string name, string points)
        {
            DrawSprite(dc, s, 26 - s.Width / 2.0, y - s.Height / 2.0);
            Text(dc, name, P(40, y), 25, White);
            Text(dc, points, P(212, y), 25, White, right: true);
        }
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 10, 236);
        Text(dc, "TOP FROGS", P(cx, 30), 60, Green, center: true);
        Text(dc, "RANK    SCORE   NAME  LEVEL", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Green : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}  {e.Stage,4}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
