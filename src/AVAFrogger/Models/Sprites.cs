namespace AVAFrogger.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The hero is drawn row by row; the bigger and
/// rounder things are painted from ellipses and boxes into a pixel grid, which keeps them easy to read
/// and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['g'] = 0xFF3CD84A, // frog green
        ['G'] = 0xFF1E8A2A, // dark green
        ['y'] = 0xFFFFE14A, // yellow
        ['w'] = 0xFFF4F4F4, // white
        ['k'] = 0xFF14141C, // black
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['p'] = 0xFFFF7AC8, // pink
        ['P'] = 0xFFB8408A, // dark pink
        ['b'] = 0xFF8A5A2A, // log brown
        ['B'] = 0xFF5A3A16, // dark brown
        ['l'] = 0xFFC8935A, // light wood
        ['c'] = 0xFF4FD7FF, // cyan
        ['u'] = 0xFF2A5AE8, // blue
        ['m'] = 0xFF9A9AAA, // steel
        ['M'] = 0xFF5A5A6A, // dark steel
        ['o'] = 0xFFFF8A1E, // orange
        ['t'] = 0xFFE83A2A, // turtle shell red
        ['T'] = 0xFF7A1A12, // shell pattern
        ['v'] = 0xFFB070FF, // violet
        ['s'] = 0xFF2E9A3A, // snake green
    };

    // ----- the frog (facing up; turned for the other ways) --------------------------------------

    private static Canvas FrogBody(char body, char dark, bool stretched)
    {
        var c = new Canvas(16, 16);
        if (stretched)
            c.Box(1, 0, 3, 3, dark).Box(12, 0, 14, 3, dark).Box(0, 12, 3, 15, dark).Box(12, 12, 15, 15, dark);
        else
            c.Box(1, 2, 3, 5, dark).Box(12, 2, 14, 5, dark).Box(1, 10, 3, 14, dark).Box(12, 10, 14, 14, dark);
        return c.Ellipse(8, 8.5, 5.5, 6.5, body)
            .Ellipse(8, 9.5, 3, 3.5, dark).Ellipse(8, 9.5, 2.2, 2.6, body)
            .Box(4, 2, 6, 4, 'w').Box(9, 2, 11, 4, 'w').Dot(5, 3, 'k').Dot(10, 3, 'k');
    }

    public static readonly Sprite FrogSit = FrogBody('g', 'G', false).Done();
    public static readonly Sprite FrogHop = FrogBody('g', 'G', true).Done();
    public static readonly Sprite LadySit = FrogBody('p', 'P', false).Done();
    /// <summary>A frog safe home, grinning.</summary>
    public static readonly Sprite FrogHome = FrogBody('g', 'G', false).Box(5, 11, 10, 11, 'k').Dot(4, 10, 'k').Dot(11, 10, 'k').Done();

    /// <summary>Squashed: a flattened splat; then a little skull.</summary>
    public static readonly Sprite Splat = new Canvas(16, 16).Ellipse(8, 8, 7, 4, 'G').Ellipse(8, 8, 5, 3, 'g')
        .Box(1, 3, 2, 5, 'G').Box(13, 3, 14, 5, 'G').Box(1, 11, 2, 13, 'G').Box(13, 11, 14, 13, 'G').Done();
    public static readonly Sprite Skull = new Canvas(16, 16).Ellipse(8, 6.5, 5, 5, 'w').Box(5, 10, 10, 12, 'w')
        .Box(5, 5, 6, 7, 'k').Box(9, 5, 10, 7, 'k').Dot(7, 9, 'k').Dot(8, 9, 'k').Dot(6, 12, 'k').Dot(8, 12, 'k')
        .Box(1, 13, 14, 14, 'w').Box(1, 2, 2, 3, 'w').Box(13, 2, 14, 3, 'w').Done();
    /// <summary>Drowned: rings spreading on the water, three frames.</summary>
    public static readonly Sprite[] Splash =
    [
        new Canvas(16, 16).Ellipse(8, 8, 3, 3, 'w').Ellipse(8, 8, 1.5, 1.5, '.').Done(),
        new Canvas(16, 16).Ellipse(8, 8, 5.5, 5.5, 'c').Ellipse(8, 8, 4.2, 4.2, '.').Ellipse(8, 8, 2, 2, 'w').Done(),
        new Canvas(16, 16).Ellipse(8, 8, 7.8, 7.8, 'c').Ellipse(8, 8, 6.6, 6.6, '.').Dot(4, 4, 'w').Dot(11, 11, 'w').Dot(11, 4, 'w').Done(),
    ];

    // ----- the road (all facing right; mirrored for the left-going lanes) ------------------------

    public static readonly Sprite Sedan = new Canvas(16, 13).Box(1, 3, 14, 9, 'y').Box(4, 1, 10, 3, 'y').Box(5, 2, 9, 3, 'c')
        .Box(11, 4, 12, 8, 'c').Box(0, 2, 3, 3, 'M').Box(0, 9, 3, 10, 'M').Box(10, 1, 13, 2, 'M').Box(10, 10, 13, 11, 'M')
        .Dot(15, 4, 'w').Dot(15, 8, 'w').Done();

    public static readonly Sprite Tractor = new Canvas(16, 14).Box(2, 4, 12, 9, 'u').Box(3, 2, 7, 11, 'm')
        .Ellipse(4, 2, 3, 2, 'k').Ellipse(4, 12, 3, 2, 'k').Box(11, 1, 13, 2, 'k').Box(11, 11, 13, 12, 'k')
        .Box(13, 4, 15, 9, 'y').Box(5, 5, 6, 8, 'w').Done();

    public static readonly Sprite Coupe = new Canvas(16, 13).Box(1, 3, 14, 9, 'p').Box(5, 2, 10, 10, 'P').Box(6, 3, 9, 9, 'c')
        .Box(1, 1, 4, 2, 'M').Box(1, 10, 4, 11, 'M').Box(11, 1, 14, 2, 'M').Box(11, 10, 14, 11, 'M').Dot(15, 5, 'y').Dot(15, 7, 'y').Done();

    public static readonly Sprite Racer = new Canvas(16, 13).Box(0, 5, 15, 7, 'w').Box(3, 3, 12, 9, 'w').Box(6, 4, 9, 8, 'r')
        .Box(1, 1, 4, 2, 'k').Box(1, 10, 4, 11, 'k').Box(11, 1, 14, 2, 'k').Box(11, 10, 14, 11, 'k').Box(0, 3, 1, 9, 'r').Done();

    public static readonly Sprite Truck = new Canvas(32, 14).Box(0, 2, 23, 11, 'w').Box(1, 3, 22, 10, 'm')
        .Box(25, 3, 31, 10, 'o').Box(28, 4, 30, 9, 'c').Box(23, 5, 25, 8, 'M')
        .Box(3, 0, 6, 1, 'k').Box(3, 12, 6, 13, 'k').Box(15, 0, 18, 1, 'k').Box(15, 12, 18, 13, 'k').Box(26, 1, 29, 2, 'k').Box(26, 11, 29, 12, 'k').Done();

    // ----- the river ------------------------------------------------------------------------------

    /// <summary>A swimming turtle, two strokes; then going under, and just the ripples.</summary>
    private static Canvas Turtle(bool stroke) => new Canvas(16, 13)
        .Box(stroke ? 2 : 3, stroke ? 0 : 1, stroke ? 4 : 5, stroke ? 2 : 3, 'g').Box(stroke ? 2 : 3, stroke ? 10 : 9, stroke ? 4 : 5, stroke ? 12 : 11, 'g')
        .Box(stroke ? 10 : 9, stroke ? 1 : 0, stroke ? 12 : 11, stroke ? 3 : 2, 'g').Box(stroke ? 10 : 9, stroke ? 9 : 10, stroke ? 12 : 11, stroke ? 11 : 12, 'g')
        .Ellipse(1.5, 6.5, 2, 2, 'g')
        .Ellipse(8.5, 6.5, 6, 5, 't').Box(5, 6, 12, 6, 'T').Box(8, 3, 8, 10, 'T').Dot(0, 6, 'k');

    public static readonly Sprite[] TurtleSwim = [Turtle(true).Done(), Turtle(false).Done()];
    public static readonly Sprite TurtleSinking = new Canvas(16, 13).Ellipse(8.5, 6.5, 5, 4, 't').Ellipse(8.5, 6.5, 6.5, 5.5, 'c')
        .Ellipse(8.5, 6.5, 4.5, 3.5, 't').Box(6, 6, 11, 6, 'T').Done();
    public static readonly Sprite TurtleUnder = new Canvas(16, 13).Ellipse(8, 6.5, 6.5, 4, 'c').Ellipse(8, 6.5, 5, 2.8, '.').Done();

    /// <summary>A crocodile swimming right: jaws shut, and open.</summary>
    private static Canvas Croc(bool open)
    {
        var c = new Canvas(48, 14)
            .Box(0, 6, 8, 7, 'G').Box(4, 5, 12, 8, 'G')
            .Ellipse(22, 7, 12, 5, 'G').Ellipse(22, 7, 10, 3.5, 's')
            .Box(30, 4, 38, 9, 'G').Box(14, 1, 16, 2, 'G').Box(14, 11, 16, 12, 'G').Box(26, 1, 28, 2, 'G').Box(26, 11, 28, 12, 'G')
            .Dot(36, 4, 'y').Dot(36, 9, 'y');
        if (open)
            c.Box(38, 2, 47, 4, 'G').Box(38, 9, 47, 11, 'G').Box(39, 5, 46, 8, 'r').Dot(41, 4, 'w').Dot(44, 4, 'w').Dot(41, 9, 'w').Dot(44, 9, 'w');
        else
            c.Box(38, 4, 47, 9, 'G').Box(39, 6, 46, 7, 'k');
        return c;
    }

    public static readonly Sprite CrocShut = Croc(false).Done();
    public static readonly Sprite CrocOpen = Croc(true).Done();

    /// <summary>A crocodile's head peering out of a home.</summary>
    public static readonly Sprite CrocHead = new Canvas(16, 16).Ellipse(8, 10, 7, 5, 'G').Box(2, 11, 13, 12, 'w')
        .Ellipse(4.5, 5, 2.5, 2.5, 'G').Ellipse(11.5, 5, 2.5, 2.5, 'G').Dot(4, 5, 'y').Dot(11, 5, 'y').Done();

    public static readonly Sprite[] Snake =
    [
        new Canvas(30, 8).Box(0, 3, 4, 4, 's').Box(4, 2, 9, 3, 's').Box(9, 4, 14, 5, 's').Box(14, 2, 19, 3, 's').Box(19, 4, 23, 5, 's')
            .Ellipse(26, 4, 4, 3, 's').Dot(27, 3, 'y').Box(29, 4, 29, 4, 'r').Done(),
        new Canvas(30, 8).Box(0, 4, 4, 5, 's').Box(4, 4, 9, 5, 's').Box(9, 2, 14, 3, 's').Box(14, 4, 19, 5, 's').Box(19, 2, 23, 3, 's')
            .Ellipse(26, 4, 4, 3, 's').Dot(27, 3, 'y').Done(),
    ];

    public static readonly Sprite Fly = new Canvas(12, 10).Ellipse(3.5, 3, 3, 2.5, 'w').Ellipse(8.5, 3, 3, 2.5, 'w')
        .Ellipse(6, 6, 2.5, 3.5, 'k').Dot(5, 4, 'r').Dot(6, 4, 'r').Done();
}
