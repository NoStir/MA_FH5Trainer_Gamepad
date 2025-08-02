using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MA_FH5Trainer.Cheats;
using MA_FH5Trainer.Models;
using MA_FH5Trainer.Resources.Keybinds;
using Memory;
using System.Windows.Media;
using static System.Diagnostics.FileVersionInfo;
using static System.IO.Path;
using static MA_FH5Trainer.Resources.Cheats;
using static MA_FH5Trainer.Resources.Memory;
using Timer = System.Timers.Timer;
using MA_FH5Trainer.ViewModels.Pages;
using MA_FH5Trainer.ViewModels.SubPages.SelfVehicle;
using MA_FH5Trainer.Views;
using MahApps.Metro.Controls;
using Environment = System.Environment;

namespace MA_FH5Trainer.ViewModels.Windows;

public partial class MainWindowViewModel : ObservableObject
{
    private bool _isInitialized;
    Timer m_timer = new Timer();

    [ObservableProperty]
    private GlobalHotkey m_selectedHotkey = new("DUmb", ModifierKeys.None, Key.None, () => {});

    public ObservableCollection<GlobalHotkey> Hotkeys { get; } = [];

    private const double WindowCornerRadiusSize = 7.5;

    [ObservableProperty]
    private string _applicationTitle = string.Empty;

    [ObservableProperty]
    private string _attachedText = string.Empty;

    [ObservableProperty]
    private string _platformText = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    [ObservableProperty]
    private string _processNameText = string.Empty;

    [ObservableProperty]
    private string _processIdText = string.Empty;

    [ObservableProperty]
    private string _trainerVersion = string.Empty;

    [ObservableProperty]
    private Brush _attachedBrush = Brushes.Red;

    [ObservableProperty]
    private Brush _versionBrush = Brushes.White;

    [ObservableProperty]
    private string _versionTooltipText = string.Empty;

    [ObservableProperty]
    private bool _showVersionWarning;

    [ObservableProperty]
    private bool _attached;

    [ObservableProperty]
    private bool _tuningScanSuccess;

    [ObservableProperty]
    private bool _tuningScanToBeDone = true;

    [ObservableProperty]
    private bool _tuningScanInProgress;

    [ObservableProperty]
    private bool _hotkeysEnabled;

    [ObservableProperty]
    private CornerRadius _windowCornerRadius = new(WindowCornerRadiusSize);

    [ObservableProperty]
    private CornerRadius _topBarCornerRadius = new(WindowCornerRadiusSize, WindowCornerRadiusSize, 0, 0);

    [ObservableProperty]
    private CornerRadius _sideBarCornerRadius = new(0, 0, 0, WindowCornerRadiusSize);

    [ObservableProperty]
    private ExpandersView? _expandersView;

    private static readonly object s_InitLock = new();
    private static readonly object s_TimerLock = new();

    public MainWindowViewModel()
    {
        lock (s_InitLock)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            InitializeViewModel();
        }
    }

    public void Close()
    {
        m_timer.Stop();
        m_timer.Dispose();
    }

    private bool m_firstInit = true;

    private void ZeroGameText()
    {
        AttachedText = "Off";
        AttachedBrush = Brushes.Red;
        VersionBrush = Brushes.White;
        VersionTooltipText = "";
        ProcessNameText = "Forza Horizon 5";
        ProcessIdText = "0";
        PlatformText = "None";
        VersionText = "Unknown";
        ShowVersionWarning = false;

        TuningScanSuccess = false;
        TuningScanInProgress = false;
        TuningScanToBeDone = true;

        if (!m_firstInit)
        {
            // The language will cry if this isn't called from an
            // STA Thread and throw an exception
            Application.Current.Dispatcher.BeginInvoke(() => {
                ExpandersView = new ExpandersView();
            });
        }
        else
        {
            m_firstInit = false;
        }
    }

    public void MakeExpandersView()
    {
        ExpandersView = new ExpandersView();
    }

    private async void InitializeViewModel()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version == null)
        {
            Environment.Exit(0);
            return;
        }

        ApplicationTitle = "Merika's FH5 Trainer";
        TrainerVersion = "v" + version.ToString();
        ZeroGameText();

        SetupAttach();
        await CheckForUpdates();
    }

    private const string GitHubRepoUrl = "https://api.github.com/repos/szaaamerik/MA_FH5Trainer/releases/latest";
    private const string GitUpdate = "https://github.com/szaaamerik/MA_FH5Trainer/releases";

    private static async Task<string?> CheckGit()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Opera/9.00 (Nintendo Wii; 1309-9)");

        try
        {
            var response = await httpClient.GetAsync(GitHubRepoUrl);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(json);
            var root = release.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                return string.Empty;
            }

            return tagNameElement.ValueKind != JsonValueKind.String ? string.Empty : tagNameElement.GetString();
        }
        catch (HttpRequestException ex)
        {
            #if true
                _ = ex;
            #else
                MessageBox.Show($"Error fetching latest release: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Information);
            #endif
        }

        return string.Empty;
    }
    private static void CompareVer(string? version)
    {
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (string.IsNullOrEmpty(version) || assemblyVersion == null)
        {
            #if false
            MessageBox.Show("Failed to fetch version information.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            #endif
            return;
        }

        if (Version.Parse(version) <= assemblyVersion)
        {
            return;
        }

        var result = MessageBox.Show($"Update to version {version}", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Process.Start("explorer.exe", $"{GitUpdate}");
        Environment.Exit(1);
    }

    private static async Task CheckForUpdates()
    {
        var version = await CheckGit();
        CompareVer(version);
    }

    [RelayCommand]
    private static async Task CheckUpdates_Command()
    {
        var version = await CheckGit();
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (string.IsNullOrEmpty(version) || assemblyVersion == null)
        {
            MessageBox.Show("Failed to fetch version information.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (Version.Parse(version) <= assemblyVersion)
        {
            MessageBox.Show("The tool is up to date.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"Update to version {version}", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Process.Start("explorer.exe", $"{GitUpdate}");
        Environment.Exit(1);
    }

    [RelayCommand]
    private static Task OpenURL(object url)
    {
        if (url is not string urlAsString)
        {
            return Task.CompletedTask;
        }

        Process.Start(new ProcessStartInfo(urlAsString) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private void SetupAttach()
    {
        m_timer.Interval = 1_000;
        m_timer.Elapsed += (_, _) =>
        {
            lock (s_TimerLock)
            {
                string processName = "forzahorizon5.exe";
                if (Attached)
                {
                    int procId = Mem.GetProcIdFromName(processName);
                    if (procId > 0)
                    {
                        return;
                    }

                    var coll = g_CachedInstances.Where(kv => typeof(ICheatsBase).IsAssignableFrom(kv.Key));
                    foreach (var cheatInstance in coll)
                    {
                        ((ICheatsBase)cheatInstance.Value).Reset();
                    }

                    Attached = false;
                    ZeroGameText();
                }
                else
                {
                    AttachedBrush = AttachedBrush == Brushes.Red ? Brushes.Transparent : Brushes.Red;
                    Mem.OpenProcessResults open = GetInstance().OpenProcess(processName);
                    if (open != Mem.OpenProcessResults.Success && open != Mem.OpenProcessResults.ProcessNotFound)
                    {
                        MessageBox.Show("Failed to open the process. Reason: " + open.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    GvpMaker(processName);
                    Attached = true;
                }
            }
        };

        m_timer.Start();
    }

    private void GvpMaker(string name)
    {
        var process = GetInstance().MProc.Process;
        if (process.MainModule == null)
        {
            return;
        }

        string platform = "";
        string update = "";
        var gamePath = process.MainModule.FileName;

        try
        {
            if (gamePath.Contains("Microsoft.624F8B84B80"))
            {
                platform = "Microsoft/UWP";
                var filePath = Combine(GetDirectoryName(gamePath) ?? string.Empty, "appxmanifest.xml");
                var xml = XElement.Load(filePath);
                var descendants = xml.Descendants().Where(e => e.Name.LocalName == "Identity");
                var version = descendants.Select(e => e.Attribute("Version")).FirstOrDefault();
                update = version == null ? "Unknown" : version.Value;
            }
            else
            {
                var filePath = Combine(GetDirectoryName(gamePath) ?? string.Empty, "OnlineFix64.dll");
                platform = File.Exists(filePath) ? "OnlineFix" : "Steam";
                update = GetVersionInfo(process.MainModule.FileName).FileVersion ?? "Unknown";
            }
        }
        catch
        {
            if (string.IsNullOrEmpty(platform))
            {
                platform = "Unknown";
            }

            if (string.IsNullOrEmpty(update))
            {
                update = "Unknown";
            }
        }

        var type = GetTypeFromName(name);
        var smoothName = GetNameFromProcType(type);

        var gvp = GameVerPlat.GetInstance();
        gvp.Name = smoothName;
        gvp.Platform = platform;
        gvp.Update = update;
        gvp.Type = type;

        AttachedText = "On";
        ProcessNameText = smoothName;
        ProcessIdText = GetInstance().MProc.ProcessId.ToString();
        PlatformText = platform;
        VersionText = update;
        AttachedBrush = Brushes.Lime;
    }

    private static GameVerPlat.GameType GetTypeFromName(string name)
    {
        return name switch
        {
            "forzahorizon5.exe" => GameVerPlat.GameType.Fh5,
            _ => GameVerPlat.GameType.None
        };
    }

    private static string GetNameFromProcType(GameVerPlat.GameType type)
    {
        return type switch
        {
            GameVerPlat.GameType.Fh5 => "Forza Horizon 5",
            _ => string.Empty
        };
    }

}
