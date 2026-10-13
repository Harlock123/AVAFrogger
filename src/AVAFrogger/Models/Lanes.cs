namespace AVAFrogger.Models;

public enum ThingKind { Sedan, Tractor, Coupe, Racer, Truck, Log, Turtles, Croc }

/// <summary>A car, log, raft of turtles or crocodile; X is its left edge.</summary>
public sealed class Thing
{
    public required ThingKind Kind { get; init; }
    public double X { get; set; }
    public required double Width { get; init; }
    /// <summary>Turtles that dive: where in the diving cycle they start.</summary>
    public double? Dive { get; init; }

    public bool IsCar => Kind is ThingKind.Sedan or ThingKind.Tractor or ThingKind.Coupe or ThingKind.Racer or ThingKind.Truck;
}

/// <summary>One lane: a row, a speed (pixels a second, + is right), and what travels in it.</summary>
public sealed class Lane
{
    public required int Row { get; init; }
    public required double Speed { get; init; }
    public List<Thing> Things { get; } = [];
}

/// <summary>
/// The lanes for each level. The traffic and the river speed up level by level; from level 2 a
/// crocodile swims among the top row's logs and more turtles dive; from level 3 a second racer.
/// </summary>
public static class Lanes
{
    /// <summary>Diving turtles: up, going down, under, coming up, in seconds.</summary>
    public const double DiveUp = 2.6, DiveSink = 0.5, DiveUnder = 0.9, DiveRise = 0.5;
    public const double DiveCycle = DiveUp + DiveSink + DiveUnder + DiveRise;

    public static double Pace(int level) => Math.Min(1.9, 1 + 0.13 * (level - 1));

    public static double Width(ThingKind k, int turtles = 3) => k switch
    {
        ThingKind.Truck => 32,
        ThingKind.Croc => 48,
        ThingKind.Turtles => 16 * turtles,
        _ => 16,
    };

    public static List<Lane> Build(int level)
    {
        double p = Pace(level);
        var lanes = new List<Lane>();
        Lane L(int row, double speed)
        {
            var lane = new Lane { Row = row, Speed = speed * p };
            lanes.Add(lane);
            return lane;
        }
        void Put(Lane lane, ThingKind kind, double width, params double[] xs)
        {
            foreach (var x in xs) lane.Things.Add(new Thing { Kind = kind, X = x, Width = width });
        }

        // Evenly spaced round the lane's loop, starting from an offset.
        double[] Spread(int n, double from) => Enumerable.Range(0, n).Select(i => Field.Wrap(from + i * Field.Period / n)).ToArray();

        // The road.
        Put(L(1, -24), ThingKind.Sedan, 16, Spread(3, 20));
        Put(L(2, 20), ThingKind.Tractor, 16, Spread(3, 0));
        Put(L(3, -30), ThingKind.Coupe, 16, Spread(3, 60));
        Put(L(4, 60), ThingKind.Racer, 16, Spread(level >= 3 ? 2 : 1, 10));
        Put(L(5, -32), ThingKind.Truck, 32, Spread(2, 30));

        // The river.
        var turtles3 = L(7, -26);
        var t3 = Spread(4, 0);
        for (int i = 0; i < 4; i++)
            turtles3.Things.Add(new Thing { Kind = ThingKind.Turtles, X = t3[i], Width = 48, Dive = i == 1 || (level >= 3 && i == 3) ? i * 1.1 : null });
        Put(L(8, 22), ThingKind.Log, 48, Spread(3, 0));
        Put(L(9, 44), ThingKind.Log, 112, Spread(2, 0));
        var turtles2 = L(10, -30);
        var t2 = Spread(4, 30);
        for (int i = 0; i < 4; i++)
            turtles2.Things.Add(new Thing { Kind = ThingKind.Turtles, X = t2[i], Width = 32, Dive = i == 2 || (level >= 2 && i == 0) ? i * 0.9 + 0.4 : null });
        var top = L(11, 30);
        var tops = Spread(3, 0);
        Put(top, ThingKind.Log, 64, tops[0], tops[1]);
        if (level >= 2) Put(top, ThingKind.Croc, 48, tops[2]);
        else Put(top, ThingKind.Log, 64, tops[2]);
        return lanes;
    }

    /// <summary>Where a diving raft is in its cycle at a moment: 0 up, 1 going down, 2 under, 3 coming up.</summary>
    public static int DiveStage(Thing t, double time)
    {
        if (t.Dive is not { } offset) return 0;
        double c = ((time + offset) % DiveCycle + DiveCycle) % DiveCycle;
        return c < DiveUp ? 0 : c < DiveUp + DiveSink ? 1 : c < DiveUp + DiveSink + DiveUnder ? 2 : 3;
    }

    /// <summary>A crocodile's jaws: open for a second, shut for a second and a half.</summary>
    public static bool JawsOpen(double time) => time % 2.5 < 1;
}
