using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

namespace Student;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private bool _submitting;
    private readonly string _profilePath;

    public MainWindow()
    {
        InitializeComponent();
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolControl");
        Directory.CreateDirectory(folder);
        _profilePath = Path.Combine(folder, "student-profile.json");

        // Each app launch starts a fresh student-use session. Keep the device ID,
        // but do not silently reuse the previous student's name.
        StatusText.Text = "Iltimos, hozir kompyuterdan foydalanayotgan o‘quvchi ma’lumotlarini kiriting.";
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async Task SubmitAsync()
    {
        if (_submitting) return;
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();
        if (firstName.Length == 0 || lastName.Length == 0)
        {
            StatusText.Text = "Davom etish uchun ism va familiyani kiriting.";
            return;
        }
        if (firstName.Length > 60 || lastName.Length > 60)
        {
            StatusText.Text = "Ism va familiya 60 belgidan oshmasligi kerak.";
            return;
        }

        _submitting = true;
        SubmitButton.IsEnabled = false;
        StatusText.Text = "Maktab tizimiga ulanish…";
        try
        {
            var folder = Path.GetDirectoryName(_profilePath)!;
            var existing = File.Exists(_profilePath)
                ? JsonSerializer.Deserialize<StudentProfile>(await File.ReadAllTextAsync(_profilePath))
                : null;
            var deviceId = existing?.DeviceId;
            if (string.IsNullOrWhiteSpace(deviceId))
                deviceId = Guid.NewGuid().ToString("N");

            var profile = new StudentProfile(firstName, lastName, Environment.MachineName, deviceId, DateTimeOffset.UtcNow);
            var apiBase = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL")?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(apiBase))
                apiBase = ReadServerUrl(folder);

            if (string.IsNullOrWhiteSpace(apiBase))
            {
                await SaveProfileAsync(profile);
                StatusText.Text = "Profil saqlandi. Server manzilini IT mutaxassisi sozlashi kerak.";
                return;
            }

            try
            {
                using var response = await Http.PostAsJsonAsync($"{apiBase}/api/students/register",
                    new { firstName, lastName, deviceId, deviceName = Environment.MachineName });
                if (response.IsSuccessStatusCode)
                {
                    await SaveProfileAsync(profile);
                    StatusText.Text = "Maktab tizimiga ulandingiz. Bu oynani yopishingiz mumkin.";
                }
                else
                {
                    StatusText.Text = $"Server javobi: {(int)response.StatusCode}. Ma’lumot qayta yuborilishi kerak.";
                }
            }
            catch (HttpRequestException)
            {
                StatusText.Text = "Maktab serveriga ulanib bo‘lmadi. Internet va server sozlamasini tekshiring.";
            }
            catch (TaskCanceledException)
            {
                StatusText.Text = "Server javob bermadi. Keyinroq qayta urinib ko‘ring.";
            }
        }
        catch (Exception)
        {
            StatusText.Text = "Ma’lumotni saqlab bo‘lmadi. Qayta urinib ko‘ring.";
        }
        finally
        {
            _submitting = false;
            SubmitButton.IsEnabled = true;
        }
    }

    private async Task SaveProfileAsync(StudentProfile profile)
    {
        await File.WriteAllTextAsync(_profilePath,
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? ReadServerUrl(string folder)
    {
        var path = Path.Combine(folder, "server-url.txt");
        if (!File.Exists(path)) return null;
        var value = File.ReadAllText(path).Trim().TrimEnd('/');
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value : null;
    }
}

public sealed record StudentProfile(
    string FirstName,
    string LastName,
    string DeviceName,
    string DeviceId,
    DateTimeOffset SavedAt);
