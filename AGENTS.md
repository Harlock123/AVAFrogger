# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A road-and-river crossing game in the style of Konami and Sega's 1981 arcade game Frogger, in C# on .NET 10 and
Avalonia 12. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). Everything in it
is original: the pixel art is drawn in code, and the sounds and tunes are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAFrogger.slnx                      # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAFrogger.slnx                       # every test runs headless: no window opens
dotnet run --project src/AVAFrogger               # play it; --no-audio for silence
FROGGER_SHOTS=$PWD/Images dotnet test tests/AVAFrogger.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVAFrogger/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAFrogger/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, high scores) and the phases of a game.
  - `GameEngine.Play.cs` steps the crossing: the lanes, the frog's hops, the homes, flies, crocodiles and the
    other extras.
  - `Entities.cs` has the frog and `Controls`.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests. It tries each hop a
    little way ahead in time before taking it.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAFrogger/Models/`: `Field.cs` (the 224x256 screen in 16-pixel rows, and the fixed step), `Lanes.cs`
  (the cars, logs, turtles and crocodiles in each lane) and `Sprites.cs` (the pixel art).
- `src/AVAFrogger/Views/`: `GameCanvas` (split over partial files) scales the game to the 1024x768 frame, draws
  it and the side panels, and runs the 60 Hz timer.
- `src/AVAFrogger/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVAFrogger.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay and screenshot tests.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances in fixed 1/60 s steps (`Field.Step`), independent of the frame
  rate, so a seed always plays the same game. Draw random numbers from the engine's seeded `_rng`, never from
  `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it
  without a window. `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** A hop is one of four ways, and the latest key held wins. A negative `Dy`
  is up the screen, towards the homes (the engine turns it into the next row up). Test new controls through
  `InputHandler`, not by setting `Controls` directly.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAFrogger`); tests keep
  the table in a temporary folder, and the screenshot tests use a silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
