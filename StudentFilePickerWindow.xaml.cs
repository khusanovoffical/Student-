using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Student;

public partial class StudentFilePickerWindow : Window
{
    private string? _selectedFilePath;
    private readonly Func<string, Task<bool>> _uploadCallback;

    public StudentFilePickerWindow(Func<string, Task<bool>> uploadCallback)
    {
        InitializeComponent();
        _uploadCallback = uploadCallback;
    }

    private void DropArea_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropAreaBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#3B82F6")!;
        }
    }

    private void DropArea_DragLeave(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#334155")!;
    }

    private void DropArea_Drop(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#334155")!;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                SelectFile(files[0]);
            }
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "O‘qituvchiga yuboriladigan faylni tanlang",
            Filter = "Barcha fayllar (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            SelectFile(dlg.FileName);
        }
    }

    private void SelectFile(string path)
    {
        _selectedFilePath = path;
        var info = new FileInfo(path);
        SelectedFileNameText.Text = info.Name;
        SelectedFileSizeText.Text = $"Hajmi: {FormatBytes(info.Length)}";
        SendConfirmButton.IsEnabled = true;
    }

    private async void SendConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            return;

        SendConfirmButton.IsEnabled = false;
        UploadProgressBar.Visibility = Visibility.Visible;
        UploadProgressBar.IsIndeterminate = true;

        try
        {
            var success = await _uploadCallback(_selectedFilePath);
            if (success)
            {
                MessageBox.Show("Fayl o‘qituvchiga muvaffaqiyatli uzatildi!", "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            else
            {
                MessageBox.Show("Faylni yuborishda xatolik yuz berdi.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
                SendConfirmButton.IsEnabled = true;
                UploadProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            SendConfirmButton.IsEnabled = true;
            UploadProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
