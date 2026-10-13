using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAFrogger.Engine;
using AVAFrogger.Models;
using AVAFrogger.Views;
using Controls = AVAFrogger.Engine.Controls;

namespace AVAFrogger.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set FROGGER_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "frog-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("FROGGER_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAFrogger-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_crossing()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        // Into the river, with a frog or two already home.
        for (int i = 0; i < 60 * 90 && g.State == GameState.Playing; i++)
        {
            g.Step(pilot.Decide(g));
            if (g.Bays.Count(b => b.Filled) >= 2 && g.Phase == Phase.Playing && g.Frog.Row == 9 && !g.Frog.Hopping) break;
        }
        Shot(w, c, "river");
        w.Close();
    }

    [AvaloniaFact]
    public void The_road_and_a_later_level()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.Go();
        g.Calm();
        g.Idle(60 * 3);
        g.PutFrog(3, 120);
        g.Step(default);
        Shot(w, c, "road");

        g.JumpTo(3);
        g.Bays[1].Filled = g.Bays[3].Filled = true;
        g.Idle(60 * 4);
        g.Bays[2].Fly = 4;
        g.Bays[4].Croc = 4;
        g.PutFrog(Field.Median, g.Snake!.X > 112 ? 40 : 184);
        g.Step(default);
        Assert.Equal(Phase.Playing, g.Phase);
        Shot(w, c, "level3");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing; i++) g.Step(new Controls(0, i % 40 == 0 ? -1 : 0));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        w.Close();

        (w, c, g) = Open(seed: 6);
        g.StartGame();
        var pilot = new AttractPilot(6);
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing; i++) g.Step(new Controls(0, i % 40 == 0 ? -1 : 0));
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials)
        {
            g.Initials!.Type('A');
            g.Initials.Type('V');
            Shot(w, c, "initials");
            g.Initials.Type('A');
            g.Idle(5);
        }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
