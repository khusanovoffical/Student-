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
    private CreativeRegistrationWindow? _creativeWindow;
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

        _screenStreamTimer.Interval = TimeSpan.FromMilliseconds(1500);
        _screenStreamTimer.Tick += async (_, _) => await SendScreenThumbnailAsync();

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
        // 1. Tarmoqda serverni avtomatik qidirish
        DiscoveryStatusText.Text = "Tarmoqda server qidirilmoqda...";
        var discovered = await DiscoveryClient.DiscoverServerAsync(TimeSpan.FromSeconds(2));
        if (!string.IsNullOrEmpty(discovered))
        {
            _serverUrl = discovered;
            ServerUrlBox.Text = discovered;
            DiscoveryStatusText.Text = $"Server topildi: {discovered}";
            DiscoveryIndicator.Fill = (SolidColorBrush)FindResource("SuccessBrush");
        }
        else
        {
            var envUrl = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                _serverUrl = envUrl.TrimEnd('/');
                ServerUrlBox.Text = _serverUrl;
                DiscoveryStatusText.Text = $"Server: {_serverUrl}";
            }
        }

        // 2. Dastlabki doimiy qurilma ulanishi
        await InitializeSignalRAsync();

        // 3. NON-NEGOTIABLE TALAB: HAR DOIM YANGI WINDOWS SESSIYASIDA CREATIVE OYNASI OCHILADI
        ShowCreativeRegistration();
    }

    private void ShowCreativeRegistration()
    {
        if (_creativeWindow != null && _creativeWindow.IsLoaded)
            return;

        _creativeWindow = new CreativeRegistrationWindow(async (firstName, lastName) =>
        {
            return await RegisterStudentSessionAsync(firstName, lastName);
        });
        _creativeWindow.Show();
    }

    private async Task<bool> RegisterStudentSessionAsync(string firstName, string lastName)
    {
        try
        {
            _currentFirstName = firstName;
            _currentLastName = lastName;

            // SignalR orqali sessiyani darhol yangilash
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("UpdateStudentSession", _deviceId, firstName, lastName);
            }
            else
            {
                // Fallback REST API
                using var res = await Http.PostAsJsonAsync($"{_serverUrl}/api/students/session", new
                {
                    deviceId = _deviceId,
                    firstName,
                    lastName
                });
                if (!res.IsSuccessStatusCode) return false;
            }

            Dispatcher.Invoke(() =>
            {
                LoginGrid.Visibility = Visibility.Collapsed;
                DashboardGrid.Visibility = Visibility.Visible;

                DashboardStudentName.Text = $"{firstName} {lastName}";
                DashboardDeviceName.Text = $"{Environment.MachineName} · ID: {_deviceId[..Math.Min(8, _deviceId.Length)]}...";
            });

            return true;
        }
        catch
        {
            return false;
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
            await _hubConnection.SendAsync("RegisterStudentDevice", _deviceId, Environment.MachineName);
            if (!string.IsNullOrEmpty(_currentFirstName))
            {
                await _hubConnection.SendAsync("UpdateStudentSession", _deviceId, _currentFirstName, _currentLastName);
            }
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

        // BUYRUQLAR
        _hubConnection.On<string, string>("ReceiveCommand", HandleCommand);
        _hubConnection.On<string, string, string>("ReceiveCommandTargeted", (target, cmd, payload) =>
        {
            if (target == _deviceId || target == "ALL")
                HandleCommand(cmd, payload);
        });

        // O'QITUVCHI TASHABBUSI BILAN CREATIVE OYNASINI OCHISH (Change Student Name)
        _hubConnection.On("OpenCreativeRegistration", () =>
        {
            Dispatcher.Invoke(() =>
            {
                ShowCreativeRegistration();
            });
        });

        // O'QITUVCHI TALABADAN FAYL SO'RAGANDA (Receive File)
        _hubConnection.On("PromptFileSelection", () =>
        {
            Dispatcher.Invoke(() =>
            {
                var picker = new StudentFilePickerWindow(async filePath =>
                {
                    return await UploadFileToTeacherAsync(filePath);
                });
                picker.ShowDialog();
            });
        });

        // MASOFAVIY KIRITISH (Remote Input)
        _hubConnection.On<string>("ReceiveRemoteInput", json =>
        {
            InputSimulator.ProcessRemoteInput(json);
        });

        // FAYL KELGANDA (O'qituvchi yuborgan fayl)
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

        // POLICY YANGILANISHI
        _hubConnection.On<string>("PolicyUpdated", json =>
        {
            _policyEnforcer.UpdateBlockedProcesses(json);
        });

        try
        {
            await _hubConnection.StartAsync();
            await _hubConnection.SendAsync("RegisterStudentDevice", _deviceId, Environment.MachineName);

            ConnectionStatusText.Text = "Serverga ulangan";
            ConnectionDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
        }
        catch { }
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

                case "OpenCreativeRegistration":
                    ShowCreativeRegistration();
                    break;

                case "PromptFileSelection":
                    var picker = new StudentFilePickerWindow(async path => await UploadFileToTeacherAsync(path));
                    picker.ShowDialog();
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

    private async Task<bool> UploadFileToTeacherAsync(string filePath)
    {
        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            var sender = string.IsNullOrWhiteSpace(_currentFirstName) ? "Talaba" : $"{_currentFirstName} {_currentLastName}";
            var url = $"{_serverUrl}/api/files/upload?targetDeviceId=TEACHER&senderType=Student&senderName={Uri.EscapeDataString(sender)}";
            var response = await Http.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
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
            _serverUrl = serverInput;

        SubmitButton.IsEnabled = false;
        StatusText.Text = "Serverga ulanish...";

        var ok = await RegisterStudentSessionAsync(firstName, lastName);
        if (!ok)
        {
            StatusText.Text = "Ulanib bo‘lmadi. Server manzilini tekshiring.";
            SubmitButton.IsEnabled = true;
        }
    }

    private async void UploadFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new StudentFilePickerWindow(async path => await UploadFileToTeacherAsync(path));
        picker.ShowDialog();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _screenStreamTimer.Stop();
        ShowCreativeRegistration();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _screenStreamTimer.Stop();
        _heartbeatTimer.Stop();
        _policyEnforcer.StopMonitoring();
        _lockWindow?.UnlockAndClose();
    }
}
