using AVAFrogger.Models;

namespace AVAFrogger.Engine;

public enum Dir { Up, Down, Left, Right }

/// <summary>The frog: where it sits (or where a hop is taking it).</summary>
public sealed class Frog
{
    public double X { get; set; }
    public int Row { get; set; }
    public Dir Facing { get; set; } = Dir.Up;
    /// <summary>Mid-hop: from where, and how far through (0 to 1).</summary>
    public bool Hopping { get; set; }
    public double HopT { get; set; }
    public double FromX { get; set; }
    public int FromRow { get; set; }
    /// <summary>The furthest row reached by this frog (each new one is worth 10).</summary>
    public int Best { get; set; }
    /// <summary>Carrying the lady frog home.</summary>
    public bool Escorting { get; set; }

    /// <summary>Where it's drawn: part-way through a hop, a little lifted.</summary>
    public double DrawY => Hopping
        ? Field.RowY(FromRow) + (Field.RowY(Row) - Field.RowY(FromRow)) * HopT
        : Field.RowY(Row);
    public double DrawX => Hopping ? FromX + (X - FromX) * HopT : X;
}

/// <summary>A home bay along the top.</summary>
public sealed class Bay
{
    public required double X { get; init; }
    public bool Filled { get; set; }
    /// <summary>A fly sitting in it (eat it for 200), seconds left.</summary>
    public double Fly { get; set; }
    /// <summary>A crocodile lurking in it, seconds left: deadly.</summary>
    public double Croc { get; set; }
}

/// <summary>A snake slithering along the median (and, later, along a log).</summary>
public sealed class Snake
{
    public double X { get; set; }
    public int Dir { get; set; } = 1;
    public const double Length = 30;
}

/// <summary>The lady frog, sitting on a log until she's picked up.</summary>
public sealed class Lady
{
    public required Thing Log { get; init; }
    public double Offset { get; set; }
}

public enum EffectKind { Score }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: a direction pressed (dx or dy), or nothing.</summary>
public record struct Controls(int Dx, int Dy);
