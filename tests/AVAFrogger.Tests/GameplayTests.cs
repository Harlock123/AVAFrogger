using AVAFrogger.Engine;
using AVAFrogger.Models;

namespace AVAFrogger.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "frog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A game under way: no flies, crocodiles, snake or lady to complicate things.</summary>
    private GameEngine Calm(int level = 1, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(level);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    /// <summary>One hop: press, then let it land.</summary>
    private static void Hop(GameEngine g, int dx, int dy)
    {
        g.Step(new Controls(dx, dy));
        Steps(g, 12);
    }

    private static readonly int Up = -1;

    [Fact]
    public void The_board_has_a_road_a_median_a_river_and_five_homes()
    {
        var lanes = Lanes.Build(1);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 7, 8, 9, 10, 11 }, lanes.Select(l => l.Row).OrderBy(r => r));
        Assert.All(lanes.Where(l => Field.IsRoad(l.Row)), l => Assert.All(l.Things, t => Assert.True(t.IsCar)));
        Assert.All(lanes.Where(l => Field.IsRiver(l.Row)), l => Assert.All(l.Things, t => Assert.False(t.IsCar)));
        Assert.Equal(5, Field.Bays.Length);
        // Anything leaving one side is out of sight before it comes back on the other.
        Assert.True(Field.Period >= Field.Width + lanes.SelectMany(l => l.Things).Max(t => t.Width));
        // Faster each level, and a crocodile from level 2.
        Assert.True(Lanes.Build(3)[0].Speed < lanes[0].Speed);
        Assert.DoesNotContain(lanes.SelectMany(l => l.Things), t => t.Kind == ThingKind.Croc);
        Assert.Contains(Lanes.Build(2).SelectMany(l => l.Things), t => t.Kind == ThingKind.Croc);
    }

    [Fact]
    public void A_frog_hops_a_row_at_a_time_and_scores_for_new_rows_only()
    {
        var g = Calm();
        g.Empty(1);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Hop(g, 0, Up);
        Assert.Equal(1, g.Frog.Row);
        Assert.Equal(10, g.Score);
        Assert.Contains(Sfx.Hop, heard);
        Hop(g, 0, 1);
        Assert.Equal(0, g.Frog.Row);
        Hop(g, 0, Up);
        Assert.Equal(10, g.Score);
        double x = g.Frog.X;
        Hop(g, 1, 0);
        Assert.Equal(x + Field.Hop, g.Frog.X);
        // Not off the side of the bank.
        g.PutFrog(0, 12);
        Hop(g, -1, 0);
        Assert.Equal(12, g.Frog.X);
    }

    [Fact]
    public void A_car_squashes_the_frog_and_the_next_frog_starts_at_the_bottom()
    {
        var g = Calm();
        g.Empty(1);
        g.Place(1, ThingKind.Sedan, 140, 16);
        Hop(g, 0, Up);
        Steps(g, 60);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("sedan", g.LastDeath);
        Assert.False(g.Drowned);
        Steps(g, (int)(60 * (GameEngine.DyingSeconds + GameEngine.ReadySeconds)) + 2);
        Assert.Equal(2, g.Lives);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(0, g.Frog.Row);
        Assert.InRange(g.TimeLeft, GameEngine.FrogSeconds - 0.5, GameEngine.FrogSeconds);
    }

    [Fact]
    public void Logs_carry_the_frog_and_the_water_drowns_it()
    {
        var g = Calm();
        g.Empty(7);
        var log = g.Place(7, ThingKind.Log, 100, 64);
        g.PutFrog(Field.Median, 130);
        Hop(g, 0, Up);
        Assert.Equal(Phase.Playing, g.Phase);
        double x = g.Frog.X;
        Steps(g, 30);
        Assert.Equal(x + g.LaneOf(7).Speed * 0.5, g.Frog.X, 0);
        Assert.Equal(log, g.Support(7, g.Frog.X, g.Clock));

        g = Calm();
        g.Empty(7);
        g.PutFrog(Field.Median, 130);
        Hop(g, 0, Up);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.True(g.Drowned);
    }

    [Fact]
    public void Riding_off_the_edge_of_the_screen_is_the_end()
    {
        var g = Calm();
        g.Empty(8);
        g.Place(8, ThingKind.Log, 180, 48);
        g.PutFrog(Field.Median + 1, 200);
        g.Empty(7);
        g.Place(7, ThingKind.Log, 180, 48);
        g.PutFrog(8, 200);
        for (int i = 0; i < 60 * 3 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("carried away", g.LastDeath);
    }

    [Fact]
    public void Diving_turtles_hold_a_frog_until_they_go_under()
    {
        var turtles = new Thing { Kind = ThingKind.Turtles, X = 0, Width = 48, Dive = 0 };
        Assert.Equal(0, Lanes.DiveStage(turtles, 1));
        Assert.Equal(1, Lanes.DiveStage(turtles, Lanes.DiveUp + 0.1));
        Assert.Equal(2, Lanes.DiveStage(turtles, Lanes.DiveUp + Lanes.DiveSink + 0.1));
        Assert.Equal(3, Lanes.DiveStage(turtles, Lanes.DiveCycle - 0.1));

        var g = Calm();
        g.Empty(7);
        double offset = -(g.Clock % Lanes.DiveCycle);
        g.Place(7, ThingKind.Turtles, 120, 48, dive: offset);
        g.PutFrog(7, 140);
        Steps(g, (int)(60 * Lanes.DiveUp));
        Assert.Equal(Phase.Playing, g.Phase);
        Steps(g, (int)(60 * (Lanes.DiveSink + 0.2)));
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.True(g.Drowned);
    }

    [Fact]
    public void A_crocodiles_back_is_safe_but_its_open_jaws_are_not()
    {
        var g = Calm(level: 2);
        var lane = g.LaneOf(11);
        var croc = lane.Things.Single(t => t.Kind == ThingKind.Croc);
        Assert.False(GameEngine.OnHead(croc, lane, croc.X + 10));
        Assert.True(GameEngine.OnHead(croc, lane, croc.X + croc.Width - 6));
        // With the crocodile well on the screen and its jaws shut, the frog on its head waits for them to open.
        while (croc.X < 20 || croc.X > 100 || Lanes.JawsOpen(g.Clock)) g.Step(default);
        g.PutFrog(11, croc.X + croc.Width - 6);
        for (int i = 0; i < 60 * 3 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("crocodile", g.LastDeath);
    }

    [Fact]
    public void Home_is_worth_fifty_plus_the_time_left_and_a_fly_is_200_more()
    {
        var g = Calm();
        g.Empty(11);
        g.Place(11, ThingKind.Log, 0, 224);
        var bay = g.Bays[2];
        bay.Fly = 3;
        g.PutFrog(11, bay.X);
        g.SetTime(20);
        int before = g.Score;
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Hop(g, 0, Up);
        Assert.True(bay.Filled);
        Assert.InRange(g.Score - before, 50 + 10 * 38 + 200, 50 + 10 * 40 + 200);
        Assert.Contains(Sfx.Fly, heard);
        Assert.Contains(Sfx.Home, heard);
        Assert.Equal(Phase.Home, g.Phase);
        Steps(g, (int)(60 * GameEngine.HomeSeconds) + 2);
        Assert.Equal(0, g.Frog.Row);
    }

    [Fact]
    public void The_bank_a_full_home_and_a_lurking_crocodile_are_all_fatal()
    {
        void Try(Action<GameEngine> setup, double x, string why)
        {
            var g = Calm();
            g.Empty(11);
            g.Place(11, ThingKind.Log, 0, 224);
            setup(g);
            g.PutFrog(11, x);
            Hop(g, 0, Up);
            Assert.Equal(Phase.Dying, g.Phase);
            Assert.Contains(why, g.LastDeath);
        }
        Try(_ => { }, 43, "bank");
        Try(g => g.Bays[0].Filled = true, Field.Bays[0], "taken");
        Try(g => g.Bays[1].Croc = 3, Field.Bays[1], "crocodile");
    }

    [Fact]
    public void All_five_home_is_a_thousand_and_the_next_level_is_faster()
    {
        var g = Calm();
        g.Empty(11);
        g.Place(11, ThingKind.Log, 0, 224);
        double speed = g.LaneOf(1).Speed;
        for (int i = 0; i < 4; i++) g.Bays[i].Filled = true;
        g.PutFrog(11, g.Bays[4].X);
        int before = g.Score;
        Hop(g, 0, Up);
        Assert.Equal(Phase.LevelClear, g.Phase);
        Assert.True(g.Score >= before + 1050);
        Steps(g, (int)(60 * GameEngine.LevelClearSeconds) + 2);
        Assert.Equal(2, g.Level);
        Assert.All(g.Bays, b => Assert.False(b.Filled));
        Assert.True(Math.Abs(g.LaneOf(1).Speed) > Math.Abs(speed));
        Assert.NotNull(g.Snake);
    }

    [Fact]
    public void The_lady_frog_rides_home_for_200()
    {
        var g = Calm();
        g.Empty(8);
        var log = g.Place(8, ThingKind.Log, 100, 48);
        g.PutLady(log);
        g.Empty(7);
        g.Place(7, ThingKind.Log, 100, 64);
        g.PutFrog(7, 124);
        Hop(g, 0, Up);
        Assert.True(g.Frog.Escorting);
        Assert.Null(g.Lady);

        g.Empty(11);
        g.Place(11, ThingKind.Log, 0, 224);
        g.PutFrog(11, g.Bays[0].X);
        g.Frog.Escorting = true;
        g.SetTime(0.4);
        int before = g.Score;
        Hop(g, 0, Up);
        Assert.Equal(before + 50 + 200, g.Score);
    }

    [Fact]
    public void The_snake_on_the_median_bites()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(2);
        Assert.NotNull(g.Snake);
        g.PutFrog(Field.Median, g.Snake!.X + 10);
        g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("snake", g.LastDeath);
    }

    [Fact]
    public void Running_out_of_time_costs_a_frog()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.SetTime(8.5);
        Steps(g, 60);
        Assert.Contains(Sfx.TimeLow, heard);
        Steps(g, 60 * 8);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("time", g.LastDeath);
    }

    [Fact]
    public void A_bonus_frog_at_twenty_thousand()
    {
        var g = Calm();
        g.AddScore(19999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_gets_frogs_home_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(3, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(3);
        int homes = 0;
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing; i++)
        {
            int before = g.Bays.Count(b => b.Filled);
            g.Step(pilot.Decide(g));
            if (g.Bays.Count(b => b.Filled) > before) homes++;
        }
        Assert.True(homes >= 5, $"{homes} home; lost: {string.Join("; ", g.LossLog)}");
        // Then sit still in the road until the frogs run out.
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(new Controls(0, i % 30 == 0 ? -1 : 0));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Level} {g.Score} {g.Lives} {g.Frog.X:0.###} {g.Frog.Row} {string.Join("", g.Bays.Select(b => b.Filled ? 1 : 0))}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 8)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Music, heard);
        Assert.Contains(Sfx.Hop, heard);
        g.Idle((int)(60 * GameEngine.AttractSeconds));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.Music or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_stops_the_music()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Pause();
        Assert.Equal(Sfx.MusicStop, heard[^1]);
        double clock = g.Clock;
        Steps(g, 30, new Controls(0, -1));
        Assert.Equal(clock, g.Clock);
        Assert.Equal(0, g.Frog.Row);
        g.Resume();
        Assert.Equal(Sfx.Music, heard[^1]);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop).Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.Equal((16, 16), (Sprites.FrogSit.Width, Sprites.FrogSit.Height));
        Assert.Equal((32, 14), (Sprites.Truck.Width, Sprites.Truck.Height));
        Assert.Equal(48, Sprites.CrocOpen.Width);
        Assert.Equal(3, Sprites.Splash.Length);
        foreach (var s in new[] { Sprites.FrogSit, Sprites.Sedan, Sprites.Truck, Sprites.CrocOpen, Sprites.TurtleSwim[0], Sprites.Fly })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
    }
}
