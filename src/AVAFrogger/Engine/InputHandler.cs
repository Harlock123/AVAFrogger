using Avalonia.Input;

namespace AVAFrogger.Engine;

/// <summary>Keyboard and gamepad boiled down to the controls: the way to hop (the latest key held wins).</summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    private static (int, int) Way(Key k) => k switch
    {
        Key.Up or Key.W => (0, -1),
        Key.Down or Key.S => (0, 1),
        Key.Left or Key.A => (-1, 0),
        Key.Right or Key.D => (1, 0),
        _ => (0, 0),
    };

    public Controls Controls
    {
        get
        {
            for (int i = _held.Count - 1; i >= 0; i--)
                if (Way(_held[i]) is (var dx, var dy) && (dx != 0 || dy != 0)) return new Controls(dx, dy);
            if (_pad.DPadUp || _pad.LeftStickY < -0.5f) return new Controls(0, -1);
            if (_pad.DPadDown || _pad.LeftStickY > 0.5f) return new Controls(0, 1);
            if (_pad.DPadLeft || _pad.LeftStickX < -0.5f) return new Controls(-1, 0);
            if (_pad.DPadRight || _pad.LeftStickX > 0.5f) return new Controls(1, 0);
            return default;
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
