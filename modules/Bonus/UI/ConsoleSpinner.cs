using System.Diagnostics;

namespace RPGGameMaster.UI;

/// <summary>
/// Animated console spinner with elapsed-time display.
/// Shows <c>⠹ label... (3.2s)</c> while working, then <c>✓ label (4.1s)</c> on completion.
/// <para>
/// Usage:
/// <code>
/// await using (ConsoleSpinner.Start("[WorldArchitect] Generating location..."))
/// {
///     result = await AgentRunner.RunAgent(agent, prompt, ct);
/// }
/// </code>
/// </para>
/// </summary>
internal sealed class ConsoleSpinner : IAsyncDisposable
{
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly string _label;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _animationTask;
    private readonly int _cursorTop;
    private readonly bool _interactive;

    private ConsoleSpinner(string label)
    {
        _interactive = !Console.IsOutputRedirected;

        // Truncate label so the full rendered line fits one console row:
        //   "  ⠋ {label} (999.9s)"  →  prefix(4) + suffix(~10) + margin(1) = 15 chars overhead
        if (_interactive)
        {
            var maxLabel = Math.Max(20, Console.BufferWidth - 15);
            _label = label.Length > maxLabel
                ? string.Concat(label.AsSpan(0, maxLabel - 3), "...")
                : label;
        }
        else
        {
            _label = label;
        }

        if (_interactive)
        {
            // Reserve a line for the spinner, capture its position
            Console.Write($"  {Frames[0]} {_label}");
            _cursorTop = Console.CursorTop;
            Console.CursorVisible = false;
            _animationTask = AnimateAsync(_cts.Token);
        }
        else
        {
            // Non-interactive (redirected output): just print the label once
            Console.WriteLine($"  ⚙ {_label}");
            _animationTask = Task.CompletedTask;
        }
    }

    /// <summary>Create and start a new spinner. Dispose to stop and show completion.</summary>
    public static ConsoleSpinner Start(string label) => new(label);

    private async Task AnimateAsync(CancellationToken ct)
    {
        var frame = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(ct))
            {
                frame = (frame + 1) % Frames.Length;
                Render(Frames[frame], ConsoleColor.DarkGray);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose
        }
    }

    private void Render(string icon, ConsoleColor color)
    {
        lock (Frames) // Simple lock to avoid interleaving with other Console writes
        {
            var savedLeft = Console.CursorLeft;
            var savedTop = Console.CursorTop;

            Console.SetCursorPosition(0, _cursorTop);
            Console.ForegroundColor = color;
            Console.Write($"  {icon} {_label} ({_stopwatch.Elapsed.TotalSeconds:F1}s)");

            // Clear any leftover characters from previous longer renders
            var remaining = Console.BufferWidth - Console.CursorLeft - 1;
            if (remaining > 0)
                Console.Write(new string(' ', remaining));

            Console.ResetColor();

            // Restore cursor to where it was (in case other code writes)
            Console.SetCursorPosition(savedLeft, savedTop);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_interactive) return;

        // Stop animation
        await _cts.CancelAsync();
        try { await _animationTask; } catch { /* swallow */ }
        _cts.Dispose();

        // Overwrite spinner line with completion marker
        lock (Frames)
        {
            Console.SetCursorPosition(0, _cursorTop);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"  ✓ {_label} ({_stopwatch.Elapsed.TotalSeconds:F1}s)");

            var remaining = Console.BufferWidth - Console.CursorLeft - 1;
            if (remaining > 0)
                Console.Write(new string(' ', remaining));

            Console.ResetColor();
            Console.WriteLine(); // Move below the completed line
            Console.CursorVisible = true;
        }
    }
}
