using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace Student.Services;

public static class InputSimulator
{
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void ProcessRemoteInput(string inputJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(inputJson);
            var root = doc.RootElement;
            var eventType = root.GetProperty("type").GetString();

            if (eventType == "MouseMove" && root.TryGetProperty("x", out var xElem) && root.TryGetProperty("y", out var yElem))
            {
                // Normalizatsiyalangan 0.0 - 1.0 koordinatalar
                double normX = xElem.GetDouble();
                double normY = yElem.GetDouble();
                int realX = (int)(normX * SystemParameters.PrimaryScreenWidth);
                int realY = (int)(normY * SystemParameters.PrimaryScreenHeight);
                SetCursorPos(realX, realY);
            }
            else if (eventType == "MouseDown" && root.TryGetProperty("button", out var btnElem))
            {
                var btn = btnElem.GetString();
                if (btn == "Right") mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                else mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            }
            else if (eventType == "MouseUp" && root.TryGetProperty("button", out var btnElem2))
            {
                var btn = btnElem2.GetString();
                if (btn == "Right") mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                else mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
            else if (eventType == "KeyDown" && root.TryGetProperty("keyCode", out var keyElem))
            {
                byte vk = (byte)keyElem.GetInt32();
                keybd_event(vk, 0, 0, 0);
            }
            else if (eventType == "KeyUp" && root.TryGetProperty("keyCode", out var keyElem2))
            {
                byte vk = (byte)keyElem2.GetInt32();
                keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
            }
        }
        catch
        {
            // Xavfsiz xatolikni yutish
        }
    }
}
