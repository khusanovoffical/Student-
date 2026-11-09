using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace Student;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private bool _submitting;

    public MainWindow() => InitializeComponent();

    private async void LastNameBox_LostFocus(object sender, RoutedEventArgs e) => await SubmitAsync();
    private async void LastNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await SubmitAsync(); }
    }

    private async Task SubmitAsync()
    {
        if (_submitting) return;
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();
        if (firstName.Length == 0 || lastName.Length == 0) return;
        if (firstName.Length > 60 || lastName.Length > 60)
        {
            StatusText.Text = "Ism va familiya 60 belgidan oshmasligi kerak.";
            return;
        }

        _submitting = true;
        StatusText.Text = "Ma’lumot saqlanmoqda…";
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolControl");
            Directory.CreateDirectory(folder);
            var profile = new StudentProfile(firstName, lastName, Environment.MachineName, DateTimeOffset.Now);
            await File.WriteAllTextAsync(Path.Combine(folder, "student-profile.json"),
                JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

            var apiBase = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL")?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(apiBase))
                apiBase = ReadServerUrl(folder);

            if (string.IsNullOrWhiteSpace(apiBase))
            {
                StatusText.Text = "Profil qurilmada saqlandi. Maktab server manzilini IT mutaxassisi sozlashi kerak.";
                return;
            }

            try
            {
                using var response = await Http.PostAsJsonAsync($"{apiBase}/api/students/register",
                    new { firstName, lastName, deviceName = Environment.MachineName });
                StatusText.Text = response.IsSuccessStatusCode
                    ? "Ma’lumot saqlandi va maktab serveriga yuborildi."
                    : $"Ma’lumot qurilmada saqlandi. Server javobi: {(int)response.StatusCode}";
            }
            catch (HttpRequestException)
            {
                StatusText.Text = "Ma’lumot qurilmada saqlandi. Maktab serveriga ulanib bo‘lmadi.";
            }
            catch (TaskCanceledException)
            {
                StatusText.Text = "Ma’lumot qurilmada saqlandi. Server javob bermadi.";
            }
        }
        catch (Exception)
        {
            StatusText.Text = "Ma’lumotni saqlab bo‘lmadi. Qayta urinib ko‘ring.";
        }
        finally { _submitting = false; }
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

public sealed record StudentProfile(string FirstName, string LastName, string DeviceName, DateTimeOffset SavedAt);