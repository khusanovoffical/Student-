using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace Student;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new();
    private bool _submitting;

    public MainWindow() => InitializeComponent();

    private async void LastNameBox_LostFocus(object sender, RoutedEventArgs e) =>
        await SubmitAsync();

    private async void LastNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        if (_submitting) return;

        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();

        if (firstName.Length == 0 || lastName.Length == 0)
            return;

        if (firstName.Length > 60 || lastName.Length > 60)
        {
            StatusText.Text = "Ism va familiya 60 belgidan oshmasligi kerak.";
            return;
        }

        _submitting = true;
        StatusText.Text = "Ma’lumot saqlanmoqda…";

        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SchoolControl");
            Directory.CreateDirectory(folder);

            var profile = new StudentProfile(firstName, lastName, DateTimeOffset.Now);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(folder, "student-profile.json"), json);

            var apiBase = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL")
                ?? "http://localhost:5080";

            try
            {
                using var response = await Http.PostAsJsonAsync(
                    $"{apiBase.TrimEnd('/')}/api/students/register",
                    new { firstName, lastName });

                if (response.IsSuccessStatusCode)
                    StatusText.Text = "Ma’lumot saqlandi va maktab serveriga yuborildi.";
                else
                    StatusText.Text = "Ma’lumot qurilmada saqlandi. Server javobi: " + (int)response.StatusCode;
            }
            catch (HttpRequestException)
            {
                StatusText.Text = "Ma’lumot qurilmada saqlandi. Server hozircha ulanmagan.";
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
        finally
        {
            _submitting = false;
        }
    }
}

public sealed record StudentProfile(string FirstName, string LastName, DateTimeOffset SavedAt);