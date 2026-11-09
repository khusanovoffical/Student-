using System.Text.Json;
using System.Windows;

namespace Student;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void Enter_Click(object sender, RoutedEventArgs e)
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            StatusText.Text = "Iltimos, ism va familiyangizni to‘liq kiriting.";
            return;
        }

        if (firstName.Length > 60 || lastName.Length > 60)
        {
            StatusText.Text = "Ism va familiya 60 belgidan oshmasligi kerak.";
            return;
        }

        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolControl");
            Directory.CreateDirectory(folder);
            var profile = new StudentProfile(firstName, lastName, DateTimeOffset.Now);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(folder, "student-profile.json"), json);
            StatusText.Text = "Ma’lumot qurilmada saqlandi. Serverga ulanish keyingi bosqichda sozlanadi.";
        }
        catch (Exception)
        {
            StatusText.Text = "Ma’lumotni saqlab bo‘lmadi. Qayta urinib ko‘ring.";
        }
    }
}

public sealed record StudentProfile(string FirstName, string LastName, DateTimeOffset SavedAt);