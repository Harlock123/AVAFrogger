using AVAFrogger.Models;

namespace AVAFrogger.Engine;

public sealed partial class GameEngine
{
    public const double HopSeconds = 0.13;
    public const double FrogSeconds = 30;
    public const double ReadySeconds = 0.9;
    public const double FirstReadySeconds = 2.4;
    public const double DyingSeconds = 1.8;
    public const double HomeSeconds = 0.6;
    public const double LevelClearSeconds = 3;
    /// <summary>A held direction hops again after this pause.</summary>
    public const double RestSeconds = 0.07;
    /// <summary>Half the frog's width, for what hits it.</summary>
    public const double FrogHalf = 5;

    public List<Lane> Lanes { get; private set; }
    public Bay[] Bays { get; }
    public Frog Frog { get; } = new();
    public Snake? Snake { get; private set; }
    public Lady? Lady { get; private set; }
    /// <summary>Seconds left for this frog to get home.</summary>
    public double TimeLeft { get; private set; } = FrogSeconds;
    /// <summary>Time on the clock that turtles dive and jaws snap by.</summary>
    public double Clock { get; private set; }
    public string? LastDeath { get; private set; }
    /// <summary>The frog drowned (rather than being squashed), for drawing the right end.</summary>
    public bool Drowned { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _readyFor = ReadySeconds;
    private double _flyTimer, _crocTimer, _ladyTimer;
    private bool _timeWarned;
    private double _rest;

    // ----- levels and frogs ---------------------------------------------------------------------

    private void StartLevel(int level)
    {
        Level = level;
        Lanes = Models.Lanes.Build(level);
        foreach (var b in Bays) { b.Filled = false; b.Fly = b.Croc = 0; }
        Snake = level >= 2 ? new Snake { X = 0, Dir = 1 } : null;
        Lady = null;
        _flyTimer = 6;
        _crocTimer = 9;
        _ladyTimer = 12;
        NewFrog();
        SetPhase(Phase.Ready);
        _readyFor = level == 1 ? FirstReadySeconds : ReadySeconds;
    }

    private void NewFrog()
    {
        var f = Frog;
        f.X = Field.Width / 2.0;
        f.Row = Field.Start;
        f.Facing = Dir.Up;
        f.Hopping = false;
        f.HopT = 0;
        f.Best = 0;
        f.Escorting = false;
        TimeLeft = FrogSeconds;
        _timeWarned = false;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        MoveLanes(dt);
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime >= _readyFor) { SetPhase(Phase.Playing); Music(true); }
                return;
            case Phase.Dying:
                Music(false);
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) { GameOver(); return; }
                NewFrog();
                SetPhase(Phase.Ready);
                _readyFor = ReadySeconds;
                return;
            case Phase.Home:
                if (PhaseTime < HomeSeconds) return;
                NewFrog();
                SetPhase(Phase.Playing);
                return;
            case Phase.LevelClear:
                Music(false);
                if (PhaseTime >= LevelClearSeconds) StartLevel(Level + 1);
                return;
        }

        UpdateBays(dt);
        MoveSnake(dt);
        MoveFrog(dt, c);
        if (Phase != Phase.Playing) return;
        Check();
        if (Phase != Phase.Playing) return;

        TimeLeft -= dt;
        if (TimeLeft <= 8 && !_timeWarned) { _timeWarned = true; Play(Sfx.TimeLow); }
        if (TimeLeft <= 0) Die("ran out of time", drowned: false);
    }

    private void Die(string why, bool drowned)
    {
        LastDeath = why;
        Drowned = drowned;
        LossLog.Add($"L{Level}: {why} at row {Frog.Row}, x {Frog.X:0}");
        SetPhase(Phase.Dying);
        Play(drowned ? Sfx.Splash : Sfx.Squash);
        Music(false);
    }

    // ----- the lanes ----------------------------------------------------------------------------

    private void MoveLanes(double dt)
    {
        Clock += dt;
        foreach (var lane in Lanes)
            foreach (var t in lane.Things)
                t.X = Field.Wrap(t.X + lane.Speed * dt);
    }

    public Lane LaneOf(int row) => Lanes.First(l => l.Row == row);

    /// <summary>
    /// What a frog at x on a river row would be standing on at a moment: not a turtle that's under,
    /// not a crocodile's head. Null: the water.
    /// </summary>
    public Thing? Support(int row, double x, double clock)
    {
        foreach (var t in LaneOf(row).Things)
        {
            if (x < t.X + 3 || x > t.X + t.Width - 3) continue;
            if (t.Kind == ThingKind.Turtles && Models.Lanes.DiveStage(t, clock) == 2) continue;
            return t;
        }
        return null;
    }

    /// <summary>The front of a crocodile, which bites when its jaws are open.</summary>
    public static bool OnHead(Thing croc, Lane lane, double x) =>
        lane.Speed > 0 ? x > croc.X + croc.Width - 16 : x < croc.X + 16;

    /// <summary>A car in the frog's way on a road row.</summary>
    public Thing? CarAt(int row, double x)
    {
        foreach (var t in LaneOf(row).Things)
            if (x + FrogHalf > t.X + 1 && x - FrogHalf < t.X + t.Width - 1) return t;
        return null;
    }

    // ----- the frog -----------------------------------------------------------------------------

    private void MoveFrog(double dt, Controls c)
    {
        var f = Frog;
        // On the river the frog rides along, hopping or not.
        int rideRow = f.Hopping && f.HopT < 0.5 ? f.FromRow : f.Row;
        if (Field.IsRiver(rideRow))
        {
            double v = LaneOf(rideRow).Speed * dt;
            f.X += v;
            if (f.Hopping) f.FromX += v;
        }

        if (f.Hopping)
        {
            f.HopT += dt / HopSeconds;
            if (f.HopT < 1) return;
            f.Hopping = false;
            f.HopT = 0;
            _rest = RestSeconds;
            Landed();
            return;
        }

        _rest -= dt;
        if (_rest > 0 || (c.Dx == 0 && c.Dy == 0)) return;
        var dir = c.Dy < 0 ? Dir.Up : c.Dy > 0 ? Dir.Down : c.Dx < 0 ? Dir.Left : Dir.Right;
        f.Facing = dir;
        int row = f.Row + (dir == Dir.Up ? 1 : dir == Dir.Down ? -1 : 0);
        double x = f.X + (dir == Dir.Left ? -Field.Hop : dir == Dir.Right ? Field.Hop : 0);
        if (row < Field.Start) return;
        // On land the frog stays on the screen; on the river it can be carried off.
        if (!Field.IsRiver(f.Row) && (x < 8 || x > Field.Width - 8)) return;
        f.FromX = f.X;
        f.FromRow = f.Row;
        f.X = x;
        f.Row = row;
        f.Hopping = true;
        f.HopT = 0;
        Play(Sfx.Hop);
    }

    private void Landed()
    {
        var f = Frog;
        if (f.Row == Field.Homes) { ReachHome(); return; }
        if (f.Row > f.Best)
        {
            f.Best = f.Row;
            AddScore(10);
        }
        if (Field.IsRiver(f.Row) && Lady is { } lady && Support(f.Row, f.X, Clock) == lady.Log
            && Math.Abs(f.X - (lady.Log.X + lady.Offset)) < 12)
        {
            f.Escorting = true;
            Lady = null;
            Play(Sfx.Lady);
        }
    }

    private void ReachHome()
    {
        var f = Frog;
        var bay = Bays.FirstOrDefault(b => Math.Abs(f.X - b.X) <= 8);
        if (bay is null) { Die("hit the bank", drowned: false); return; }
        if (bay.Filled) { Die("found the home taken", drowned: false); return; }
        if (bay.Croc > 0) { Die("was eaten by the crocodile in the home", drowned: false); return; }
        bay.Filled = true;
        int points = 50 + 10 * (int)(TimeLeft * 2);
        if (bay.Fly > 0) { points += 200; bay.Fly = 0; Play(Sfx.Fly); }
        if (f.Escorting) { points += 200; f.Escorting = false; }
        AddScore(points);
        Popup(new Vec(bay.X, 30), points);
        Play(Sfx.Home);
        if (Bays.All(b => b.Filled))
        {
            AddScore(1000);
            Popup(new Vec(Field.Width / 2.0, 130), 1000, 2.5);
            SetPhase(Phase.LevelClear);
            Play(Sfx.LevelClear);
        }
        else SetPhase(Phase.Home);
    }

    /// <summary>Everything that can end a frog where it stands.</summary>
    private void Check()
    {
        var f = Frog;
        int row = f.Hopping && f.HopT < 0.5 ? f.FromRow : f.Row;
        double x = f.DrawX;
        if (Field.IsRoad(row) && CarAt(row, x) is { } car) { Die($"was hit by a {car.Kind.ToString().ToLowerInvariant()}", drowned: false); return; }
        if (row == Field.Median && Snake is { } s && x + FrogHalf > s.X && x - FrogHalf < s.X + Snake.Length) { Die("was bitten by the snake", drowned: false); return; }
        if (!Field.IsRiver(f.Row) || f.Hopping) return;
        if (f.X < 4 || f.X > Field.Width - 4) { Die("was carried away", drowned: true); return; }
        var under = Support(f.Row, f.X, Clock);
        if (under is null) { Die("fell in the river", drowned: true); return; }
        if (under.Kind == ThingKind.Croc && Models.Lanes.JawsOpen(Clock) && OnHead(under, LaneOf(f.Row), f.X))
            Die("was eaten by a crocodile", drowned: true);
    }

    // ----- the homes, the snake and the lady ----------------------------------------------------

    private void UpdateBays(double dt)
    {
        foreach (var b in Bays)
        {
            b.Fly = Math.Max(0, b.Fly - dt);
            b.Croc = Math.Max(0, b.Croc - dt);
        }
        var empty = Bays.Where(b => !b.Filled && b.Fly <= 0 && b.Croc <= 0).ToList();
        _flyTimer -= dt;
        if (_flyTimer <= 0)
        {
            _flyTimer = 7 + _rng.NextDouble() * 4;
            if (empty.Count > 0) empty[_rng.Next(empty.Count)].Fly = 5;
        }
        if (Level >= 2)
        {
            _crocTimer -= dt;
            if (_crocTimer <= 0)
            {
                _crocTimer = 8 + _rng.NextDouble() * 5;
                empty = Bays.Where(b => !b.Filled && b.Fly <= 0 && b.Croc <= 0).ToList();
                if (empty.Count > 1) empty[_rng.Next(empty.Count)].Croc = 4;
            }
        }
        _ladyTimer -= dt;
        if (Lady is { } lady && (lady.Log.X + lady.Offset > Field.Width + 8)) Lady = null;
        if (_ladyTimer <= 0 && Lady is null && !Frog.Escorting)
        {
            _ladyTimer = 15 + _rng.NextDouble() * 8;
            var log = LaneOf(8).Things.Where(t => t.X > -10 && t.X < 60).FirstOrDefault();
            if (log is not null) Lady = new Lady { Log = log, Offset = log.Width / 2 };
        }
    }

    private void MoveSnake(double dt)
    {
        if (Snake is not { } s) return;
        s.X += s.Dir * 18 * Models.Lanes.Pace(Level) * dt;
        if (s.X < -8) s.Dir = 1;
        if (s.X > Field.Width - Snake.Length + 8) s.Dir = -1;
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    /// <summary>Skips the "ready" pause.</summary>
    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        Music(true);
    }

    internal void JumpTo(int level)
    {
        StartLevel(level);
        Go();
    }

    internal void PutFrog(int row, double x)
    {
        Frog.Row = row;
        Frog.X = x;
        Frog.Hopping = false;
        Frog.Best = Math.Max(Frog.Best, row);
    }

    /// <summary>Clears a lane of everything (for setting up a scene).</summary>
    internal void Empty(int row) => LaneOf(row).Things.Clear();

    internal Thing Place(int row, ThingKind kind, double x, double width, double? dive = null)
    {
        var t = new Thing { Kind = kind, X = x, Width = width, Dive = dive };
        LaneOf(row).Things.Add(t);
        return t;
    }

    internal void SetTime(double seconds) => TimeLeft = seconds;
    internal void Calm()
    {
        _flyTimer = _crocTimer = _ladyTimer = double.MaxValue;
        Snake = null;
    }
    internal void PutLady(Thing log) => Lady = new Lady { Log = log, Offset = log.Width / 2 };
}
