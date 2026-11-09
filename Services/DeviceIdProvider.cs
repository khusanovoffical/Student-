using System.IO;
using Microsoft.Win32;

namespace Student.Services;

public static class DeviceIdProvider
{
    private static string? _cachedId;

    public static string GetDeviceId()
    {
        if (!string.IsNullOrEmpty(_cachedId))
            return _cachedId;

        // 1. Registry dan MachineGuid olishga urinish
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var val = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(val) && Guid.TryParse(val, out var guid))
            {
                _cachedId = guid.ToString("N");
                return _cachedId;
            }
        }
        catch
        {
            // Registry ruxsati bo'lmasa, faylga o'tamiz
        }

        // 2. LocalAppData faylida saqlangan GUID
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolControl");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "device-guid.txt");

        if (File.Exists(file))
        {
            var content = File.ReadAllText(file).Trim();
            if (Guid.TryParse(content, out var guid))
            {
                _cachedId = guid.ToString("N");
                return _cachedId;
            }
        }

        // 3. Yangi doimiy GUID yaratish
        var newId = Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(file, newId);
        }
        catch
        {
            // Agar yozib bo'lmasa ham qaytaramiz
        }

        _cachedId = newId;
        return _cachedId;
    }
}
