namespace AVAFrogger.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's, in rows 16 pixels tall. Row 0
/// is the bank the frog starts on; 1 to 5 are the road; 6 the median; 7 to 11 the river; 12 the
/// homes along the top.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const double Step = 1.0 / 60;
    public const int Rows = 13;
    public const int Start = 0, FirstRoad = 1, LastRoad = 5, Median = 6, FirstRiver = 7, LastRiver = 11, Homes = 12;
    public const double Hop = 16;

    /// <summary>
    /// Lane objects come round again after this many pixels: the screen plus room for the longest log,
    /// so anything leaving one side is wholly out of sight before it comes back on the other.
    /// </summary>
    public const double Period = 336;
    public const double OffLeft = -112;

    /// <summary>The middle of a row, top to bottom.</summary>
    public static double RowY(int row) => row == Homes ? 44 : 240 - 16 * row;
    public static bool IsRoad(int row) => row is >= FirstRoad and <= LastRoad;
    public static bool IsRiver(int row) => row is >= FirstRiver and <= LastRiver;

    /// <summary>The five bays along the top.</summary>
    public static readonly double[] Bays = [20, 66, 112, 158, 204];

    /// <summary>A position brought back into the lane's span.</summary>
    public static double Wrap(double x) => ((x - OffLeft) % Period + Period) % Period + OffLeft;
}
