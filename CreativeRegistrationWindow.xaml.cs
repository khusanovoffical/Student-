using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Student;

public partial class CreativeRegistrationWindow : Window
{
    private bool _allowClose = false;
    private readonly Func<string, string, Task<bool>> _onRegisterCallback;

    public CreativeRegistrationWindow(Func<string, string, Task<bool>> onRegisterCallback)
    {
        InitializeComponent();
        _onRegisterCallback = onRegisterCallback;
        Closing += CreativeRegistrationWindow_Closing;
        Loaded += (_, _) => FirstNameBox.Focus();
    }

    private async void EnterButton_Click(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender == FirstNameBox && string.IsNullOrWhiteSpace(LastNameBox.Text))
            {
                LastNameBox.Focus();
            }
            else
            {
                await SubmitAsync();
            }
        }
    }

    private async Task SubmitAsync()
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            StatusText.Text = "Iltimos, ism va familiyangizni to‘liq kiriting.";
            return;
        }

        EnterButton.IsEnabled = false;
        StatusText.Text = "Serverga ulanmoqda...";

        try
        {
            var success = await _onRegisterCallback(firstName, lastName);
            if (success)
            {
                _allowClose = true;
                Close();
            }
            else
            {
                StatusText.Text = "Serverga ma’lumot yetkazilmadi. Qayta urinib ko‘ring.";
                EnterButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Xatolik: {ex.Message}";
            EnterButton.IsEnabled = true;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Alt+F4 yoki tizim oynasini yopish urinishlarini to'sish
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && e.SystemKey == Key.F4)
        {
            e.Handled = true;
        }
    }

    private void CreativeRegistrationWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }

    private void EmergencyUnlock_Click(object sender, RoutedEventArgs e)
    {
        var pass = AdminPasswordBox.Password.Trim();
        if (pass == "school2026" || pass == "admin123")
        {
            _allowClose = true;
            Close();
        }
        else
        {
            MessageBox.Show("Parol noto‘g‘ri!", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
            AdminPasswordBox.Password = "";
        }
    }
}
