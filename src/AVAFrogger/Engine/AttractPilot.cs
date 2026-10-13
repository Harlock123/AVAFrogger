using AVAFrogger.Models;

namespace AVAFrogger.Engine;

/// <summary>
/// The computer player for attract mode and the tests. Every lane moves at a steady speed, so it plays
/// each possible hop forward a little way in time: a hop is safe if no car is there while the frog
/// lands and for a moment after, and on the river if there's something to stand on that won't sink or
/// carry it off the screen. It hops up when that's safe, otherwise waits, sidesteps or backs off, and
/// on the top of the river lines up with an empty home before the last hop.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private const double Linger = 0.45;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing || g.Frog.Hopping) return default;
        var f = g.Frog;
        var home = NearestHome(g, f.X);

        bool Can(int dx, int dy) => Safe(g, f.Row, f.X, f.Row - dy, f.X + dx * Field.Hop, hop: true);
        // The last hop only into an empty home it's lined up with.
        bool canUp = f.Row + 1 < Field.Homes ? Can(0, -1)
            : f.Row + 1 == Field.Homes && Aligned(g, f.X + Carry(g, f.Row) * GameEngine.HopSeconds) is not null;
        // From the row below the top, go up only where the top row's flow will carry it to the home.
        if (canUp && f.Row == Field.LastRiver - 1 && home is { } target)
        {
            double flow = Carry(g, Field.LastRiver);
            double ahead = (target - f.X) * Math.Sign(flow);
            canUp = ahead is > -12 and < 70 || !Safe(g, f.Row, f.X, f.Row, f.X, hop: false);
        }
        if (canUp) return new Controls(0, -1);

        bool canStay = Safe(g, f.Row, f.X, f.Row, f.X, hop: false);
        // On the top row of the river, work along towards an empty home (the log carries it too).
        double drift = f.Row == Field.LastRiver ? Carry(g, f.Row) * 0.3 : 0;
        int toward = home is null || Math.Abs(home.Value - (f.X + drift)) < 8 ? 0 : Math.Sign(home.Value - (f.X + drift));
        if (f.Row == Field.LastRiver && toward != 0 && Can(toward, 0)) return new Controls(toward, 0);
        if (canStay) return default;
        foreach (int dx in toward >= 0 ? new[] { 1, -1 } : [-1, 1])
            if (Can(dx, 0)) return new Controls(dx, 0);
        if (f.Row > 0 && Can(0, 1)) return new Controls(0, 1);
        return default;
    }

    private static double Carry(GameEngine g, int row) => Field.IsRiver(row) ? g.LaneOf(row).Speed : 0;

    private static double? NearestHome(GameEngine g, double x) =>
        g.Bays.Where(b => !b.Filled && b.Croc <= 0).OrderBy(b => Math.Abs(b.X - x)).Select(b => (double?)b.X).FirstOrDefault();

    /// <summary>An empty, croc-free home this x would land in, if any.</summary>
    private static Bay? Aligned(GameEngine g, double x) =>
        g.Bays.FirstOrDefault(b => !b.Filled && b.Croc <= 0 && Math.Abs(b.X - x) <= 5);

    /// <summary>Plays the frog forward from (row, x) to (toRow, toX) and a little after: does it come through?</summary>
    private static bool Safe(GameEngine g, int row, double x, int toRow, double toX, bool hop)
    {
        if (toRow < Field.Start || toRow > Field.Homes) return false;
        if (!Field.IsRiver(row) && (toX < 8 || toX > Field.Width - 8)) return false;
        if (toRow == Field.Homes) return true; // judged by the caller: lined up with an empty home
        const double dt = 1.0 / 30;
        double hopTime = hop ? GameEngine.HopSeconds : 0;
        double fx = x, lx = toX;
        for (double t = 0; t <= hopTime + Linger; t += dt)
        {
            bool landed = t >= hopTime;
            int r = !hop || t >= hopTime / 2 ? toRow : row;
            double px = hop && !landed ? fx + (lx - fx) * (t / hopTime) : lx;
            if (Field.IsRoad(r) && Car(g, r, px, t)) return false;
            if (r == Field.Median && g.Snake is { } s)
            {
                double sx = s.X + s.Dir * 18 * Lanes.Pace(g.Level) * t;
                if (px + GameEngine.FrogHalf + 2 > sx && px - GameEngine.FrogHalf - 2 < sx + Snake.Length) return false;
            }
            if (Field.IsRiver(r) && landed)
            {
                if (px < 8 || px > Field.Width - 8) return false;
                if (!Stands(g, r, px, t)) return false;
            }
            // Carried by the river meanwhile.
            double carry = Carry(g, r) * dt;
            fx += carry;
            lx += carry;
        }
        return true;
    }

    private static bool Car(GameEngine g, int row, double x, double t)
    {
        var lane = g.LaneOf(row);
        foreach (var c in lane.Things)
        {
            double cx = Field.Wrap(c.X + lane.Speed * t);
            if (x + GameEngine.FrogHalf + 2 > cx && x - GameEngine.FrogHalf - 2 < cx + c.Width) return true;
        }
        return false;
    }

    private static bool Stands(GameEngine g, int row, double x, double t)
    {
        var lane = g.LaneOf(row);
        foreach (var th in lane.Things)
        {
            double tx = Field.Wrap(th.X + lane.Speed * t);
            if (x < tx + 4 || x > tx + th.Width - 4) continue;
            // Not on turtles that are under, or about to go under.
            if (th.Kind == ThingKind.Turtles && (Lanes.DiveStage(th, g.Clock + t) == 2 || Lanes.DiveStage(th, g.Clock + t + 0.3) == 2)) continue;
            if (th.Kind == ThingKind.Croc)
            {
                bool head = lane.Speed > 0 ? x > tx + th.Width - 18 : x < tx + 18;
                if (head) continue;
            }
            return true;
        }
        return false;
    }
}
