using System.Diagnostics;
using System.Text.Json;

namespace Student.Services;

public sealed class PolicyEnforcer
{
    private static readonly HashSet<string> SystemSafeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "taskmgr", "dwm", "csrss", "smss", "winlogon", "services", "lsass", "svchost", "fontdrvhost"
    };

    private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;

    public void UpdateBlockedProcesses(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            lock (_blockedProcesses)
            {
                _blockedProcesses.Clear();
                foreach (var item in list)
                {
                    var clean = Path.GetFileNameWithoutExtension(item).Trim();
                    if (!string.IsNullOrEmpty(clean) && !SystemSafeProcesses.Contains(clean))
                    {
                        _blockedProcesses.Add(clean);
                    }
                }
            }
        }
        catch
        {
            // Json parse xatoligi
        }
    }

    public void StartMonitoring(Action<string> onProcessBlocked)
    {
        StopMonitoring();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    List<string> targets;
                    lock (_blockedProcesses)
                    {
                        targets = [.. _blockedProcesses];
                    }

                    if (targets.Count > 0)
                    {
                        var running = Process.GetProcesses();
                        foreach (var p in running)
                        {
                            try
                            {
                                if (targets.Contains(p.ProcessName) && !SystemSafeProcesses.Contains(p.ProcessName))
                                {
                                    p.Kill();
                                    onProcessBlocked?.Invoke(p.ProcessName);
                                }
                            }
                            catch
                            {
                                // Jarayon yopilishiga ruxsat yetmasligi yoki allaqachon yopilgan bo'lishi mumkin
                            }
                        }
                    }
                }
                catch
                {
                    // Monitor sikli davom etadi
                }

                await Task.Delay(2500, token);
            }
        }, token);
    }

    public void StopMonitoring()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
