using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using Student.Services;

namespace Student;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private HubConnection? _hubConnection;
    private LockWindow? _lockWindow;
    private readonly PolicyEnforcer _policyEnforcer = new();
    private readonly DispatcherTimer _screenStreamTimer = new();
    private readonly DispatcherTimer _heartbeatTimer = new();

    private string _deviceId = "";
    private string _currentFirstName = "";
    private string _currentLastName = "";
    private string _serverUrl = "http://localhost:5123";
    private bool _isScreenStreaming = false;

    public ObservableCollection<ReceivedFileItem> ReceivedFiles { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ReceivedFilesList.ItemsSource = ReceivedFiles;

        _deviceId = DeviceIdProvider.GetDeviceId();

        // Screen streaming timer (har 1.5 soniyada o'qituvchiga thumbnail yuboradi)
        _screenStreamTimer.Interval = TimeSpan.FromMilliseconds(1500);
        _screenStreamTimer.Tick += async (_, _) => await SendScreenThumbnailAsync();

        // Heartbeat timer (har 15 soniyada)
        _heartbeatTimer.Interval = TimeSpan.FromSeconds(15);
        _heartbeatTimer.Tick += async (_, _) =>
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                try { await _hubConnection.SendAsync("Heartbeat", _deviceId); } catch { }
            }
        };
        _heartbeatTimer.Start();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Tarmoqda avtomatik serverni qidirish
        DiscoveryStatusText.Text = "Tarmoqda server qidirilmoqda...";
        var discovered = await DiscoveryClient.DiscoverServerAsync(TimeSpan.FromSeconds(2.5));
        if (!string.IsNullOrEmpty(discovered))
        {
            _serverUrl = discovered;
            ServerUrlBox.Text = discovered;
            DiscoveryStatusText.Text = $"Server topildi: {discovered}";
            DiscoveryIndicator.Fill = (SolidColorBrush)FindResource("SuccessBrush");
        }
        else
        {
            // Muhit o'zgaruvchisidan yoki saqlangan fayldan o'qish
            var envUrl = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                _serverUrl = envUrl.TrimEnd('/');
                ServerUrlBox.Text = _serverUrl;
                DiscoveryStatusText.Text = $"Server: {_serverUrl}";
            }
            else
            {
                DiscoveryStatusText.Text = "Lokal server qidirilmoqda (standart localhost:5123)";
            }
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();
        var serverInput = ServerUrlBox.Text.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            StatusText.Text = "Iltimos, ism va familiyangizni kiriting.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(serverInput))
        {
            _serverUrl = serverInput;
        }

        SubmitButton.IsEnabled = false;
        StatusText.Text = "Serverga ulanish...";

        try
        {
            _currentFirstName = firstName;
            _currentLastName = lastName;

            // 1. API orqali ro'yxatdan o'tish
            using var regResponse = await Http.PostAsJsonAsync($"{_serverUrl}/api/students/register", new
            {
                firstName,
                lastName,
                deviceId = _deviceId,
                deviceName = Environment.MachineName
            });

            if (!regResponse.IsSuccessStatusCode)
            {
                StatusText.Text = $"Xatolik: Server javobi {(int)regResponse.StatusCode}";
                SubmitButton.IsEnabled = true;
                return;
            }

            // 2. SignalR Hub ga ulanish
            await InitializeSignalRAsync();

            // 3. Ilovalar monitoringini boshlash
            _policyEnforcer.StartMonitoring(blockedName =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"'{blockedName}' ilovasi maktab qoidalari bo‘yicha cheklangan.",
                        "Maktab nazorati", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            // 4. Dashboardga o'tish
            LoginGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;

            DashboardStudentName.Text = $"{firstName} {lastName}";
            DashboardDeviceName.Text = $"{Environment.MachineName} · ID: {_deviceId[..Math.Min(8, _deviceId.Length)]}...";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Serverga ulanib bo‘lmadi: {ex.Message}";
            SubmitButton.IsEnabled = true;
        }
    }

    private async Task InitializeSignalRAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_serverUrl}/hubs/school")
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hubConnection.Reconnecting += _ =>
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatusText.Text = "Qayta ulanmoqda...";
                ConnectionDot.Fill = (SolidColorBrush)FindResource("MutedBrush");
            });
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async _ =>
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatusText.Text = "Serverga ulangan";
                ConnectionDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            });
            await _hubConnection.SendAsync("RegisterStudent", _deviceId, Environment.MachineName, _currentFirstName, _currentLastName);
        };

        _hubConnection.Closed += _ =>
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatusText.Text = "Aloqa uzildi";
                ConnectionDot.Fill = (SolidColorBrush)FindResource("DangerBrush");
            });
            return Task.CompletedTask;
        };

        // BUYRUQLARNI QABUL QILISH
        _hubConnection.On<string, string>("ReceiveCommand", HandleCommand);
        _hubConnection.On<string, string, string>("ReceiveCommandTargeted", (target, cmd, payload) =>
        {
            if (target == _deviceId || target == "ALL")
                HandleCommand(cmd, payload);
        });

        // REMOTE INPUT (sichqoncha/klaviatura)
        _hubConnection.On<string>("ReceiveRemoteInput", json =>
        {
            InputSimulator.ProcessRemoteInput(json);
        });

        // FAYL KELGANDA
        _hubConnection.On<JsonElement>("FileAvailable", async elem =>
        {
            try
            {
                var fileId = elem.GetProperty("id").GetString();
                var fileName = elem.GetProperty("originalFileName").GetString() ?? "file";
                var senderName = elem.GetProperty("senderName").GetString() ?? "O‘qituvchi";
                var target = elem.GetProperty("targetDeviceId").GetString();

                if (target == "ALL" || target == _deviceId)
                {
                    await DownloadIncomingFileAsync(fileId, fileName, senderName);
                }
            }
            catch { }
        });

        // POLICY YANGILANGANDA
        _hubConnection.On<string>("PolicyUpdated", json =>
        {
            _policyEnforcer.UpdateBlockedProcesses(json);
        });

        await _hubConnection.StartAsync();
        await _hubConnection.SendAsync("RegisterStudent", _deviceId, Environment.MachineName, _currentFirstName, _currentLastName);

        ConnectionStatusText.Text = "Serverga ulangan";
        ConnectionDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
    }

    private void HandleCommand(string cmd, string payload)
    {
        Dispatcher.Invoke(() =>
        {
            switch (cmd)
            {
                case "Lock":
                    if (_lockWindow == null || !_lockWindow.IsLoaded)
                    {
                        _lockWindow = new LockWindow(payload);
                        _lockWindow.Show();
                    }
                    else
                    {
                        _lockWindow.UpdateMessage(payload);
                    }
                    LessonStateText.Text = "Kompyuter qulflangan";
                    break;

                case "Unlock":
                    _lockWindow?.UnlockAndClose();
                    _lockWindow = null;
                    LessonStateText.Text = "Faol ish rejimi";
                    break;

                case "Restart":
                    try { Process.Start(new ProcessStartInfo("shutdown", "/r /t 5") { CreateNoWindow = true }); } catch { }
                    break;

                case "Shutdown":
                    try { Process.Start(new ProcessStartInfo("shutdown", "/s /t 5") { CreateNoWindow = true }); } catch { }
                    break;

                case "StartScreenStream":
                    _isScreenStreaming = true;
                    ScreenSharingBanner.Visibility = Visibility.Visible;
                    _screenStreamTimer.Start();
                    break;

                case "StopScreenStream":
                    _isScreenStreaming = false;
                    ScreenSharingBanner.Visibility = Visibility.Collapsed;
                    _screenStreamTimer.Stop();
                    break;

                case "Notification":
                    MessageBox.Show(payload, "O‘qituvchidan xabar", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        });
    }

    private async Task SendScreenThumbnailAsync()
    {
        if (!_isScreenStreaming || _hubConnection?.State != HubConnectionState.Connected)
            return;

        try
        {
            var base64 = ScreenCaptureService.CaptureScreenAsBase64Jpeg(480, 50L);
            if (!string.IsNullOrEmpty(base64))
            {
                await _hubConnection.SendAsync("SendScreenThumbnail", _deviceId, base64);
            }
        }
        catch { }
    }

    private async Task DownloadIncomingFileAsync(string? fileId, string fileName, string senderName)
    {
        if (string.IsNullOrEmpty(fileId)) return;

        try
        {
            var saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SchoolControl_Files");
            Directory.CreateDirectory(saveDir);
            var savePath = Path.Combine(saveDir, fileName);

            var bytes = await Http.GetByteArrayAsync($"{_serverUrl}/api/files/download/{fileId}");
            await File.WriteAllBytesAsync(savePath, bytes);

            Dispatcher.Invoke(() =>
            {
                ReceivedFiles.Insert(0, new ReceivedFileItem
                {
                    FileName = fileName,
                    FilePath = savePath,
                    Info = $"{senderName} · {DateTime.Now:HH:mm}"
                });

                MessageBox.Show($"O‘qituvchidan yangi fayl qabul qilindi: {fileName}\nSaqlandi: {savePath}",
                    "Yangi fayl", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch { }
    }

    private async void UploadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "O‘qituvchiga topshiriq yuborish",
            Filter = "Barcha fayllar (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var fileBytes = await File.ReadAllBytesAsync(dlg.FileName);
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                content.Add(fileContent, "file", Path.GetFileName(dlg.FileName));

                var url = $"{_serverUrl}/api/files/upload?targetDeviceId=TEACHER&senderType=Student&senderName={Uri.EscapeDataString(_currentFirstName + " " + _currentLastName)}";
                var response = await Http.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Fayl muvaffaqiyatli yuborildi!", "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Faylni yuborishda xatolik yuz berdi.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fayl yuborilmadi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _screenStreamTimer.Stop();
        _policyEnforcer.StopMonitoring();
        _hubConnection?.DisposeAsync();
        _hubConnection = null;

        DashboardGrid.Visibility = Visibility.Collapsed;
        LoginGrid.Visibility = Visibility.Visible;
        SubmitButton.IsEnabled = true;
        StatusText.Text = "";
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _screenStreamTimer.Stop();
        _heartbeatTimer.Stop();
        _policyEnforcer.StopMonitoring();
        _lockWindow?.UnlockAndClose();
    }
}

public sealed class ReceivedFileItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Info { get; set; } = "";
}
