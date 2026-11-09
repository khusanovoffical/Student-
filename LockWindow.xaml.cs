using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Student;

public partial class LockWindow : Window
{
    private bool _allowClose = false;

    public LockWindow(string? message = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(message))
        {
            LockMessageText.Text = message;
        }
        Closing += LockWindow_Closing;
    }

    public void UpdateMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            LockMessageText.Text = message;
    }

    public void UnlockAndClose()
    {
        _allowClose = true;
        Close();
    }

    private void LockWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true; // Yopilishiga ruxsat berilmaydi
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Alt+F4 yoki boshqa tugmalarni to'sish
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && e.SystemKey == Key.F4)
        {
            e.Handled = true;
        }
    }

    private void EmergencyUnlock_Click(object sender, RoutedEventArgs e)
    {
        var pass = UnlockPasswordBox.Password.Trim();
        // Favqulodda ochish paroli: standart o'qituvchi kaliti yoki school2026
        if (pass == "school2026" || pass == "admin123")
        {
            UnlockAndClose();
        }
        else
        {
            MessageBox.Show("Noto‘g‘ri parol!", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
            UnlockPasswordBox.Password = "";
        }
    }
}
