using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAFrogger.Engine;
using AVAFrogger.Models;

namespace AVAFrogger.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAFrogger/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Green = Brush(0xFF3CD84A);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    private const uint Water = 0xFF0C1C6E, Road = 0xFF0E0E14, Verge = 0xFF5A2A8A, VergeLight = 0xFF7A44AA;
    private const uint Hedge = 0xFF1E7A2A, HedgeLight = 0xFF3CB84A;

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawBoard(dc);
            DrawLanes(dc);
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the board ----------------------------------------------------------------------------

    private void DrawBoard(DrawingContext dc)
    {
        var g = Engine;
        Fill(dc, 0xFF000000, 0, 0, Field.Width, Field.Height);
        Fill(dc, Water, 0, 32, Field.Width, 104);
        // The hedge along the top, with the five homes let into it.
        Fill(dc, Hedge, 0, 28, Field.Width, 24);
        for (double x = 0; x < Field.Width; x += 8) Fill(dc, HedgeLight, x + 1, 30 + (x / 8 % 2) * 3, 4, 3);
        foreach (var b in g.Bays) Fill(dc, Water, b.X - 10, 34, 20, 18);
        // The median and the starting bank.
        foreach (double y in new[] { 136.0, 232.0 })
        {
            Fill(dc, Verge, 0, y, Field.Width, 16);
            for (double x = 0; x < Field.Width; x += 8) Fill(dc, VergeLight, x + (y / 8 % 2) * 4, y + 3, 3, 3);
        }
        Fill(dc, Road, 0, 152, Field.Width, 80);
        for (int lane = 1; lane < 5; lane++)
            for (double x = 4; x < Field.Width; x += 24) Fill(dc, 0xFF3A3A48, x, 232 - lane * 16, 10, 1);
    }

    private void DrawLanes(DrawingContext dc)
    {
        var g = Engine;
        foreach (var lane in g.Lanes)
        {
            double y = Field.RowY(lane.Row);
            bool left = lane.Speed < 0;
            foreach (var t in lane.Things)
            {
                switch (t.Kind)
                {
                    case ThingKind.Log:
                        DrawLog(dc, t.X, y, t.Width);
                        break;
                    case ThingKind.Turtles:
                        int stage = Lanes.DiveStage(t, g.Clock);
                        var art = stage switch
                        {
                            0 => Sprites.TurtleSwim[(int)Math.Abs(Math.Floor(g.Clock * 3 + t.X / 40)) % 2],
                            2 => Sprites.TurtleUnder,
                            _ => Sprites.TurtleSinking,
                        };
                        for (double tx = t.X; tx < t.X + t.Width - 1; tx += 16) DrawSprite(dc, art, tx, y - 6.5, flip: !left);
                        break;
                    case ThingKind.Croc:
                        DrawSprite(dc, Lanes.JawsOpen(g.Clock) ? Sprites.CrocOpen : Sprites.CrocShut, t.X, y - 7, flip: left);
                        break;
                    default:
                        var car = t.Kind switch
                        {
                            ThingKind.Sedan => Sprites.Sedan,
                            ThingKind.Tractor => Sprites.Tractor,
                            ThingKind.Coupe => Sprites.Coupe,
                            ThingKind.Racer => Sprites.Racer,
                            _ => Sprites.Truck,
                        };
                        DrawSprite(dc, car, t.X, y - car.Height / 2.0, flip: left);
                        break;
                }
            }
        }
        if (g.Lady is { } lady) DrawSprite(dc, Sprites.LadySit, lady.Log.X + lady.Offset - 8, Field.RowY(8) - 8);
        if (g.Snake is { } s) DrawSprite(dc, Sprites.Snake[(int)(g.Clock * 5) % 2], s.X, Field.RowY(Field.Median) - 4, flip: s.Dir < 0);
    }

    private static void DrawLog(DrawingContext dc, double x, double y, double w)
    {
        Fill(dc, 0xFF8A5A2A, x + 2, y - 6, w - 4, 12);
        Fill(dc, 0xFF5A3A16, x + 2, y - 6, w - 4, 1);
        Fill(dc, 0xFF5A3A16, x + 2, y + 5, w - 4, 1);
        for (double bx = x + 8; bx < x + w - 8; bx += 14) Fill(dc, 0xFF5A3A16, bx, y - 2 + (bx / 14 % 2) * 3, 7, 1);
        dc.DrawEllipse(Brush(0xFFC8935A), new Pen(Brush(0xFF5A3A16), 2), P(x + 3, y), 3 * Scale, 6 * Scale);
        dc.DrawEllipse(Brush(0xFF8A5A2A), null, P(x + w - 2, y), 2.5 * Scale, 6 * Scale);
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var b in g.Bays)
        {
            if (b.Filled) DrawSprite(dc, Sprites.FrogHome, b.X - 8, 35);
            else if (b.Croc > 0) DrawSprite(dc, Sprites.CrocHead, b.X - 8, 35);
            else if (b.Fly > 0) DrawSprite(dc, Sprites.Fly, b.X - 6, 38);
        }

        DrawFrog(dc);

        foreach (var fx in g.Effects)
            Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - fx.Age * 6), 28, Cyan, center: true);

        // The time left, along the bottom.
        double frac = Math.Clamp(g.TimeLeft / GameEngine.FrogSeconds, 0, 1);
        Fill(dc, g.TimeLeft <= 8 ? 0xFFFF3D3D : 0xFF3CD84A, Field.Width - 30 - 150 * frac, 249, 150 * frac, 6);
        Text(dc, "TIME", P(Field.Width - 16, 252), 24, Yellow, center: true);

        var notice = P(Field.Width / 2.0, Field.RowY(Field.Median));
        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.LevelClear) Banner(dc, $"LEVEL {g.Level} CLEARED!", "1000 BONUS");
                else if (g.Phase == Phase.Ready && g.Level > 1 && g.Frog.Best == 0 && g.Bays.All(b => !b.Filled)) Banner(dc, $"LEVEL {g.Level}", null);
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", notice, 30, Red, center: true);
                break;
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xE0000000, 24, 126, 176, sub is null ? 22 : 34);
        Text(dc, title, P(Field.Width / 2.0, 137), 44, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 152), 24, White, center: true);
    }

    private void DrawFrog(DrawingContext dc)
    {
        var g = Engine;
        var f = g.Frog;
        double x = f.DrawX, y = f.DrawY;
        if (g.Phase == Phase.Dying)
        {
            double t = g.PhaseTime;
            if (g.Drowned)
            {
                if (t < 1.2) DrawSprite(dc, Sprites.Splash[Math.Min(2, (int)(t / 0.4))], x - 8, y - 8);
                else DrawSprite(dc, Sprites.Skull, x - 8, y - 8);
            }
            else DrawSprite(dc, t < 0.8 ? Sprites.Splat : Sprites.Skull, x - 8, y - 8);
            return;
        }
        if (g.Phase is Phase.Home or Phase.LevelClear) return;
        if (g.Phase == Phase.Ready && (int)(_clock * 8) % 2 == 0) return;
        double angle = f.Facing switch { Dir.Right => 90, Dir.Down => 180, Dir.Left => 270, _ => 0 };
        DrawSprite(dc, f.Hopping ? Sprites.FrogHop : Sprites.FrogSit, x - 8, y - 8, angle: angle);
        if (f.Escorting) DrawSprite(dc, Sprites.LadySit, x - 8, y - 11, angle: angle, px: 0.75, offsetCentre: true);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored, turned or shrunk.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, double angle = 0, double px = 1, bool offsetCentre = false)
    {
        if (offsetCentre) { x += s.Width * (1 - px) / 2; y += s.Height * (1 - px) / 2; }
        var centre = P(x + s.Width * px / 2.0, y + s.Height * px / 2.0);
        using var turn = angle != 0
            ? (IDisposable?)dc.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y))
            : null;
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
