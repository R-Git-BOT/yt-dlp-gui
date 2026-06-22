using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using YtDlpGui.Infrastructure;
using YtDlpGui.Models;
using YtDlpGui.Services;

namespace YtDlpGui.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly HashSet<string> HiddenForDownloadSwitches = new(StringComparer.OrdinalIgnoreCase)
    {
        "--help",
        "--version",
        "--update",
        "--no-update",
        "--update-to",
        "--list-extractors",
        "--extractor-descriptions",
        "--dump-user-agent",
        "--list-impersonate-targets",
        "--simulate",
        "--skip-download",
        "--print",
        "--print-to-file",
        "--dump-json",
        "--dump-single-json",
        "--dump-pages",
        "--write-pages",
        "--print-traffic",
        "--list-formats",
        "--list-subs",
        "--list-thumbnails"
    };

    private readonly YtDlpHelpParser _helpParser = new();
    private readonly OptionCatalogService _catalogService = new();
    private readonly OptionMergeService _mergeService = new();
    private readonly SettingsService _settingsService = new();
    private readonly DownloadRunner _downloadRunner = new();
    private YtDlpOptionCatalog? _optionCatalog;
    private CancellationTokenSource? _downloadCancellation;
    private bool _hasLoadedAutoSettings;
    private string _urlText = "";
    private string _searchText = "";
    private string _logText = "";
    private string _statusMessage = "準備完了";
    private string _ytDlpPath = "yt-dlp";
    private string _outputPath = "";
    private FormatPresetOption? _selectedFormatPreset;
    private string _formatSelector = "";
    private string _formatSort = "";
    private string _formatResolution = "指定なし";
    private string _formatExtension = "指定なし";
    private string _formatSize = "指定なし";
    private string _mergeOutputFormat = "";
    private string _outputTemplate = "%(title)s [%(id)s].%(ext)s";
    private bool _isApplyingFormatPreset;
    private bool _showEnabledOnly;
    private bool _isDownloading;
    private bool _isFormatBuilderEnabled;
    private bool _isFormatDirectEdit;
    private bool _isOutputTemplateBuilderEnabled;
    private bool _formatSortForce;
    private bool _formatSortReset;
    private bool _videoMultistreams;
    private bool _audioMultistreams;
    private string _activeSection = "ダウンロード";

    public MainViewModel()
    {
        Categories = new ObservableCollection<OptionCategoryViewModel>();
        Queue = new ObservableCollection<DownloadItemViewModel>();
        NavigationItems = new ObservableCollection<string> { "ダウンロード", "検索", "ブラウザ", "履歴", "設定", "拡張機能", "ログ", "About" };
        FormatPresets = new ObservableCollection<FormatPresetOption>(CreateFormatPresets());
        FormatResolutions = new ObservableCollection<string> { "指定なし", "2160p以下", "1440p以下", "1080p以下", "720p以下", "480p以下" };
        FormatExtensions = new ObservableCollection<string> { "指定なし", "mp4/m4a優先", "mp4のみ", "webm優先" };
        FormatSizes = new ObservableCollection<string> { "指定なし", "50MB以下", "100MB以下", "500MB以下", "容量小さめ" };
        FormatMergeOutputFormats = new ObservableCollection<string> { "", "mp4", "mkv", "webm", "mov", "flv" };
        _selectedFormatPreset = FormatPresets.FirstOrDefault(item => item.Name == "既定に任せる") ?? FormatPresets.FirstOrDefault();
        OutputTemplateFields = new ObservableCollection<OutputTemplateField>(CreateOutputTemplateFields());

        RefreshOptionsCommand = new AsyncRelayCommand(LoadOptionsAsync);
        StartDownloadCommand = new AsyncRelayCommand(StartDownloadAsync, () => !IsDownloading);
        StopDownloadCommand = new RelayCommand(StopDownload, () => IsDownloading);
        ResetCommand = new RelayCommand(ResetOptions);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
        FormatUrlListCommand = new RelayCommand(FormatUrlList);
        ClearQueueCommand = new RelayCommand(ClearQueue, () => Queue.Count > 0 && !IsDownloading);
        ClearLogCommand = new RelayCommand(() => LogText = "");
        SaveLogCommand = new RelayCommand(SaveLog);
        SelectNavigationCommand = new RelayCommand(parameter => ActiveSection = parameter?.ToString() ?? "ダウンロード");
        OpenHelpCommand = new RelayCommand(OpenHelp);
        OpenFormatSelectionHelpCommand = new RelayCommand(OpenFormatSelectionHelp);
        OpenOutputTemplateHelpCommand = new RelayCommand(OpenOutputTemplateHelp);
        AppendOutputTemplateFieldCommand = new RelayCommand(AppendOutputTemplateField);
        ApplyDefaultOutputTemplateCommand = new RelayCommand(ApplyDefaultOutputTemplate);
        ShowVersionInfoCommand = new AsyncRelayCommand(ShowVersionInfoAsync);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);

        _ = LoadCatalogOptionsAsync();
    }

    public ObservableCollection<OptionCategoryViewModel> Categories { get; }
    public ObservableCollection<DownloadItemViewModel> Queue { get; }
    public ObservableCollection<string> NavigationItems { get; }
    public ObservableCollection<FormatPresetOption> FormatPresets { get; }
    public ObservableCollection<string> FormatResolutions { get; }
    public ObservableCollection<string> FormatExtensions { get; }
    public ObservableCollection<string> FormatSizes { get; }
    public ObservableCollection<string> FormatMergeOutputFormats { get; }
    public ObservableCollection<OutputTemplateField> OutputTemplateFields { get; }

    public AsyncRelayCommand RefreshOptionsCommand { get; }
    public AsyncRelayCommand StartDownloadCommand { get; }
    public RelayCommand StopDownloadCommand { get; }
    public RelayCommand ResetCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand LoadSettingsCommand { get; }
    public RelayCommand FormatUrlListCommand { get; }
    public RelayCommand ClearQueueCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand SaveLogCommand { get; }
    public RelayCommand SelectNavigationCommand { get; }
    public RelayCommand OpenHelpCommand { get; }
    public RelayCommand OpenFormatSelectionHelpCommand { get; }
    public RelayCommand OpenOutputTemplateHelpCommand { get; }
    public RelayCommand AppendOutputTemplateFieldCommand { get; }
    public RelayCommand ApplyDefaultOutputTemplateCommand { get; }
    public AsyncRelayCommand ShowVersionInfoCommand { get; }
    public AsyncRelayCommand CheckUpdatesCommand { get; }

    public string UrlText
    {
        get => _urlText;
        set
        {
            if (SetProperty(ref _urlText, value))
            {
                OnPropertyChanged(nameof(UrlCount));
                RefreshCommandPreview();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string YtDlpPath
    {
        get => _ytDlpPath;
        set
        {
            if (SetProperty(ref _ytDlpPath, value))
            {
                RefreshCommandPreview();
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                RefreshCommandPreview();
            }
        }
    }

    public bool ShowEnabledOnly
    {
        get => _showEnabledOnly;
        set
        {
            if (SetProperty(ref _showEnabledOnly, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                StartDownloadCommand.RaiseCanExecuteChanged();
                StopDownloadCommand.RaiseCanExecuteChanged();
                ClearQueueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsFormatBuilderEnabled
    {
        get => _isFormatBuilderEnabled;
        set
        {
            if (SetProperty(ref _isFormatBuilderEnabled, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public bool IsFormatDirectEdit
    {
        get => _isFormatDirectEdit;
        set
        {
            if (SetProperty(ref _isFormatDirectEdit, value))
            {
                OnPropertyChanged(nameof(IsFormatPresetMode));
                if (!value && SelectedFormatPreset is not null)
                {
                    ApplyFormatPreset(SelectedFormatPreset);
                }

                RefreshFormatBindings();
            }
        }
    }

    public bool IsFormatPresetMode => !IsFormatDirectEdit;

    public FormatPresetOption? SelectedFormatPreset
    {
        get => _selectedFormatPreset;
        set
        {
            if (SetProperty(ref _selectedFormatPreset, value) && value is not null && !IsFormatDirectEdit)
            {
                ApplyFormatPreset(value);
            }
        }
    }

    public string FormatSelector
    {
        get => _formatSelector;
        set
        {
            if (SetProperty(ref _formatSelector, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public string FormatSort
    {
        get => _formatSort;
        set
        {
            if (SetProperty(ref _formatSort, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public string FormatResolution
    {
        get => _formatResolution;
        set
        {
            if (SetProperty(ref _formatResolution, value))
            {
                OnFormatConditionChanged();
            }
        }
    }

    public string FormatExtension
    {
        get => _formatExtension;
        set
        {
            if (SetProperty(ref _formatExtension, value))
            {
                OnFormatConditionChanged();
            }
        }
    }

    public string FormatSize
    {
        get => _formatSize;
        set
        {
            if (SetProperty(ref _formatSize, value))
            {
                OnFormatConditionChanged();
            }
        }
    }

    public bool FormatSortForce
    {
        get => _formatSortForce;
        set
        {
            if (SetProperty(ref _formatSortForce, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public bool FormatSortReset
    {
        get => _formatSortReset;
        set
        {
            if (SetProperty(ref _formatSortReset, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public bool VideoMultistreams
    {
        get => _videoMultistreams;
        set
        {
            if (SetProperty(ref _videoMultistreams, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public bool AudioMultistreams
    {
        get => _audioMultistreams;
        set
        {
            if (SetProperty(ref _audioMultistreams, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public string MergeOutputFormat
    {
        get => _mergeOutputFormat;
        set
        {
            if (SetProperty(ref _mergeOutputFormat, value))
            {
                RefreshFormatBindings();
            }
        }
    }

    public string FormatGeneratedArguments
    {
        get
        {
            if (!IsFormatBuilderEnabled)
            {
                return "フォーマット設定は無効です";
            }

            var args = BuildFormatArguments().ToList();
            return args.Count == 0 ? "yt-dlpの既定設定を使用" : string.Join(" ", args.Select(Quote));
        }
    }

    public bool IsOutputTemplateBuilderEnabled
    {
        get => _isOutputTemplateBuilderEnabled;
        set
        {
            if (SetProperty(ref _isOutputTemplateBuilderEnabled, value))
            {
                RefreshOutputTemplateBindings();
            }
        }
    }

    public string OutputTemplate
    {
        get => _outputTemplate;
        set
        {
            if (SetProperty(ref _outputTemplate, value))
            {
                RefreshOutputTemplateBindings();
            }
        }
    }

    public string OutputTemplateGeneratedArguments
    {
        get
        {
            if (!IsOutputTemplateBuilderEnabled)
            {
                return "出力ファイル名設定は無効です";
            }

            return string.IsNullOrWhiteSpace(OutputTemplate)
                ? "--output のテンプレートを入力してください"
                : $"--output {Quote(OutputTemplate.Trim())}";
        }
    }

    public string ActiveSection
    {
        get => _activeSection;
        set
        {
            if (SetProperty(ref _activeSection, value))
            {
                StatusMessage = value switch
                {
                    "ブラウザ" => "ブラウザCookieやログインセッション関連は「認証」カテゴリで設定できます",
                    "拡張機能" => "拡張機能は将来の追加処理・連携用の領域です",
                    "ログ" => "ログ領域を確認中",
                    _ => $"{value} を表示中"
                };
            }
        }
    }

    public string CommandPreview
    {
        get
        {
            var args = BuildBaseArguments().ToList();
            var urls = GetUrls().Take(3).ToList();
            var command = Quote(YtDlpPath);
            foreach (var argument in args)
            {
                command += " " + Quote(argument);
            }

            foreach (var url in urls)
            {
                command += " " + Quote(url);
            }

            if (GetUrls().Count > urls.Count)
            {
                command += " ...";
            }

            return command;
        }
    }

    public int UrlCount => GetUrls().Count;
    public int SelectedOptionCount => Categories.Sum(category => category.Options.Count(option => option.IsSelected));
    public string QueueSummary => $"{Queue.Count}件のURL";

    private async Task LoadCatalogOptionsAsync()
    {
        try
        {
            _optionCatalog = await _catalogService.LoadAsync(CancellationToken.None);
            var result = _mergeService.FromCatalogOnly(_optionCatalog);
            ApplyOptionDefinitions(result.Options, $"同梱カタログから {result.CatalogCount} 個のオプションを表示しています");
            await LoadAutoSettingsOnceAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "同梱カタログを読み込めませんでした";
            AppendLog($"[WARN] 同梱カタログを読み込めませんでした: {ex.Message}");
        }
    }

    private async Task LoadOptionsAsync()
    {
        if (_optionCatalog is null && Categories.Count == 0)
        {
            await LoadCatalogOptionsAsync();
        }

        StatusMessage = "yt-dlp --help と照合中...";
        var helpResult = await _helpParser.LoadHelpOptionsAsync(YtDlpPath, CancellationToken.None);
        if (!helpResult.Success)
        {
            StatusMessage = _optionCatalog is null
                ? helpResult.Status
                : $"{helpResult.Status} 同梱カタログを表示しています";
            AppendLog($"[WARN] {StatusMessage}");

            if (_optionCatalog is null)
            {
                var (fallbackDefinitions, fallbackStatus) = await _helpParser.LoadOptionsAsync(YtDlpPath, CancellationToken.None);
                ApplyOptionDefinitions(fallbackDefinitions, fallbackStatus);
            }

            return;
        }

        if (_optionCatalog is null)
        {
            ApplyOptionDefinitions(helpResult.Options, helpResult.Status);
            return;
        }

        var mergeResult = _mergeService.Merge(_optionCatalog, helpResult.Options);
        var status = $"{helpResult.Status}。{mergeResult.Summary}";
        ApplyOptionDefinitions(mergeResult.Options, status);
    }

    private void ApplyOptionDefinitions(IReadOnlyList<YtDlpOptionDefinition> definitions, string status)
    {
        var selectedStates = Categories
            .SelectMany(category => category.Options)
            .ToDictionary(
                option => option.PrimarySwitch,
                option => new SavedOptionState { IsSelected = option.IsSelected, Value = option.Value },
                StringComparer.OrdinalIgnoreCase);
        var expandedStates = Categories
            .ToDictionary(category => category.Name, category => category.IsExpanded, StringComparer.OrdinalIgnoreCase);

        Application.Current.Dispatcher.Invoke(() =>
        {
            Categories.Clear();
            var visibleDefinitions = definitions
                .Where(option => !HiddenForDownloadSwitches.Contains(option.PrimarySwitch))
                .ToList();

            var grouped = visibleDefinitions
                .Select((option, index) => new { Option = option, Index = index })
                .GroupBy(item => item.Option.Category)
                .OrderBy(group => CategoryOrder(group.Key))
                .ThenBy(group => group.Min(item => item.Index))
                .ThenBy(group => group.Key);

            var categoryIndex = 0;
            var formatCategoryExpanded = expandedStates.TryGetValue("フォーマット", out var wasFormatExpanded)
                ? wasFormatExpanded
                : true;
            Categories.Add(new OptionCategoryViewModel("フォーマット", Array.Empty<OptionItemViewModel>(), formatCategoryExpanded));
            categoryIndex++;
            var outputTemplateCategoryExpanded = expandedStates.TryGetValue("出力ファイル名", out var wasOutputTemplateExpanded)
                ? wasOutputTemplateExpanded
                : true;
            Categories.Add(new OptionCategoryViewModel("出力ファイル名", Array.Empty<OptionItemViewModel>(), outputTemplateCategoryExpanded));
            categoryIndex++;

            foreach (var group in grouped)
            {
                var options = group.Select(item =>
                {
                    var definition = item.Option;
                    var option = new OptionItemViewModel(definition);
                    if (selectedStates.TryGetValue(option.PrimarySwitch, out var state))
                    {
                        option.IsSelected = state.IsSelected;
                        option.Value = state.Value;
                    }

                    option.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName is nameof(OptionItemViewModel.IsSelected) or nameof(OptionItemViewModel.Value))
                        {
                            RefreshCommandPreview();
                            ApplyFilter();
                        }
                    };
                    return option;
                });
                var isExpanded = expandedStates.TryGetValue(group.Key, out var expanded)
                    ? expanded
                    : false;
                Categories.Add(new OptionCategoryViewModel(group.Key, options, isExpanded));
                categoryIndex++;
            }

            ApplyFilter();
            RefreshCommandPreview();
            var displayStatus = $"{status}（表示 {visibleDefinitions.Count} 件）";
            StatusMessage = displayStatus;
            AppendLog($"[INFO] {displayStatus}");
        });
    }

    private async Task StartDownloadAsync()
    {
        RebuildQueue();
        if (Queue.Count == 0)
        {
            StatusMessage = "URLを入力してください";
            return;
        }

        IsDownloading = true;
        _downloadCancellation = new CancellationTokenSource();
        StatusMessage = "ダウンロード中...";

        try
        {
            await _downloadRunner.RunQueueAsync(YtDlpPath, BuildBaseArguments().ToList(), Queue, AppendLog, _downloadCancellation.Token);
            StatusMessage = "すべての処理が完了しました";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ダウンロードを停止しました";
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] {ex.Message}");
            StatusMessage = "エラーが発生しました";
        }
        finally
        {
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
            IsDownloading = false;
        }
    }

    private void StopDownload()
    {
        _downloadCancellation?.Cancel();
        _downloadRunner.KillCurrentProcess();
    }

    private async Task SaveSettingsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "設定を保存",
            Filter = "yt-dlp GUI 設定 (*.json)|*.json|JSON (*.json)|*.json",
            FileName = "yt-dlp-gui-settings.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _settingsService.SaveAsync(dialog.FileName, CaptureSettings());
        StatusMessage = "設定を保存しました";
    }

    private async Task LoadSettingsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "設定を読み込み",
            Filter = "yt-dlp GUI 設定 (*.json)|*.json|JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var settings = await _settingsService.LoadAsync(dialog.FileName);
        ApplySettings(settings);
        StatusMessage = "設定を読み込みました";
    }

    public async Task SaveAutoSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAutoAsync(CaptureSettings());
        }
        catch (Exception ex)
        {
            AppendLog($"[WARN] 前回状態を保存できませんでした: {ex.Message}");
        }
    }

    private async Task LoadAutoSettingsOnceAsync()
    {
        if (_hasLoadedAutoSettings)
        {
            return;
        }

        _hasLoadedAutoSettings = true;

        try
        {
            var settings = await _settingsService.TryLoadAutoAsync();
            if (settings is null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ApplySettings(settings);
                StatusMessage = "前回の設定を復元しました";
            });
        }
        catch (Exception ex)
        {
            AppendLog($"[WARN] 前回状態を復元できませんでした: {ex.Message}");
        }
    }

    private async Task ShowVersionInfoAsync()
    {
        var appVersion = GetAppVersion();
        var ytDlpVersion = await GetYtDlpVersionAsync();
        MessageBox.Show(
            $"アプリバージョン: {appVersion}{Environment.NewLine}yt-dlpバージョン: {ytDlpVersion}",
            "バージョン情報",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task CheckUpdatesAsync()
    {
        StatusMessage = "アップデートを確認中...";
        var appVersion = GetAppVersion();
        var ytDlpVersion = await GetYtDlpVersionAsync();
        var latestYtDlpVersion = await GetLatestYtDlpVersionAsync();

        var message = new StringBuilder();
        message.AppendLine($"アプリバージョン: {appVersion}");
        message.AppendLine("アプリの更新確認先はまだ設定されていません。");
        message.AppendLine();
        message.AppendLine($"yt-dlp 現在: {ytDlpVersion}");
        message.AppendLine($"yt-dlp 最新: {latestYtDlpVersion}");

        if (!latestYtDlpVersion.StartsWith("取得失敗", StringComparison.OrdinalIgnoreCase) &&
            !ytDlpVersion.StartsWith("取得失敗", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ytDlpVersion.Trim(), latestYtDlpVersion.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            message.AppendLine();
            message.AppendLine("yt-dlpの新しいバージョンがある可能性があります。");
        }

        MessageBox.Show(
            message.ToString(),
            "アップデート確認",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        StatusMessage = "アップデート確認が完了しました";
    }

    private void ResetOptions()
    {
        foreach (var option in Categories.SelectMany(category => category.Options))
        {
            option.IsSelected = false;
            option.Value = option.Choices.FirstOrDefault() ?? "";
        }

        IsFormatBuilderEnabled = false;
        IsFormatDirectEdit = false;
        SelectedFormatPreset = FormatPresets.FirstOrDefault(item => item.Name == "既定に任せる") ?? FormatPresets.FirstOrDefault();
        if (SelectedFormatPreset is not null)
        {
            ApplyFormatPreset(SelectedFormatPreset);
        }
        IsOutputTemplateBuilderEnabled = false;
        OutputTemplate = "%(title)s [%(id)s].%(ext)s";

        SearchText = "";
        ShowEnabledOnly = false;
        ApplyFilter();
        RefreshCommandPreview();
        StatusMessage = "設定をリセットしました";
    }

    private AppSettings CaptureSettings()
    {
        return new AppSettings
        {
            YtDlpPath = YtDlpPath,
            OutputPath = OutputPath,
            UrlText = UrlText,
            SearchText = SearchText,
            ActiveSection = ActiveSection,
            ShowEnabledOnly = ShowEnabledOnly,
            Format = new SavedFormatSettings
            {
                IsEnabled = IsFormatBuilderEnabled,
                IsDirectEdit = IsFormatDirectEdit,
                PresetName = SelectedFormatPreset?.Name ?? "",
                FormatSelector = FormatSelector,
                FormatSort = FormatSort,
                Resolution = FormatResolution,
                Extension = FormatExtension,
                Size = FormatSize,
                FormatSortForce = FormatSortForce,
                FormatSortReset = FormatSortReset,
                VideoMultistreams = VideoMultistreams,
                AudioMultistreams = AudioMultistreams,
                MergeOutputFormat = MergeOutputFormat
            },
            OutputTemplate = new SavedOutputTemplateSettings
            {
                IsEnabled = IsOutputTemplateBuilderEnabled,
                Template = OutputTemplate
            },
            Options = Categories
                .SelectMany(category => category.Options)
                .ToDictionary(
                    option => option.PrimarySwitch,
                    option => new SavedOptionState { IsSelected = option.IsSelected, Value = option.Value },
                    StringComparer.OrdinalIgnoreCase),
            ExpandedCategories = Categories
                .ToDictionary(
                    category => category.Name,
                    category => category.IsExpanded,
                    StringComparer.OrdinalIgnoreCase)
        };
    }

    private void ApplySettings(AppSettings settings)
    {
        YtDlpPath = string.IsNullOrWhiteSpace(settings.YtDlpPath) ? "yt-dlp" : settings.YtDlpPath;
        OutputPath = settings.OutputPath ?? "";
        UrlText = settings.UrlText ?? "";
        ActiveSection = string.IsNullOrWhiteSpace(settings.ActiveSection) ? "ダウンロード" : settings.ActiveSection;
        ApplyFormatSettings(settings.Format ?? new SavedFormatSettings());
        ApplyOutputTemplateSettings(settings.OutputTemplate ?? new SavedOutputTemplateSettings());
        var optionStates = settings.Options ?? new Dictionary<string, SavedOptionState>(StringComparer.OrdinalIgnoreCase);
        var expandedStates = settings.ExpandedCategories ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in Categories.SelectMany(category => category.Options))
        {
            if (optionStates.TryGetValue(option.PrimarySwitch, out var state))
            {
                option.IsSelected = state.IsSelected;
                option.Value = state.Value ?? "";
            }
        }

        foreach (var category in Categories)
        {
            if (expandedStates.TryGetValue(category.Name, out var isExpanded))
            {
                category.IsExpanded = isExpanded;
            }
        }

        SearchText = settings.SearchText ?? "";
        ShowEnabledOnly = settings.ShowEnabledOnly;
        ApplyFilter();
        RefreshCommandPreview();
    }

    private IEnumerable<string> BuildBaseArguments()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            yield return "--paths";
            yield return OutputPath;
        }

        foreach (var argument in BuildFormatArguments())
        {
            yield return argument;
        }

        foreach (var argument in BuildOutputTemplateArguments())
        {
            yield return argument;
        }

        var formatSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--format",
            "--format-sort",
            "--format-sort-reset",
            "--format-sort-force",
            "--no-format-sort-force",
            "--video-multistreams",
            "--no-video-multistreams",
            "--audio-multistreams",
            "--no-audio-multistreams",
            "--merge-output-format"
        };

        foreach (var option in Categories.SelectMany(category => category.Options).Where(option => option.IsSelected))
        {
            if (IsFormatBuilderEnabled && formatSwitches.Contains(option.PrimarySwitch))
            {
                continue;
            }

            if (IsOutputTemplateBuilderEnabled && string.Equals(option.PrimarySwitch, "--output", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return option.PrimarySwitch;
            if (option.RequiresArgument && !string.IsNullOrWhiteSpace(option.Value))
            {
                yield return option.Value;
            }
        }
    }

    private IEnumerable<string> BuildOutputTemplateArguments()
    {
        if (!IsOutputTemplateBuilderEnabled || string.IsNullOrWhiteSpace(OutputTemplate))
        {
            yield break;
        }

        yield return "--output";
        yield return OutputTemplate.Trim();
    }

    private IEnumerable<string> BuildFormatArguments()
    {
        if (!IsFormatBuilderEnabled)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(FormatSelector))
        {
            yield return "--format";
            yield return FormatSelector.Trim();
        }

        if (!string.IsNullOrWhiteSpace(FormatSort))
        {
            yield return "--format-sort";
            yield return FormatSort.Trim();
        }

        if (FormatSortReset)
        {
            yield return "--format-sort-reset";
        }

        if (FormatSortForce)
        {
            yield return "--format-sort-force";
        }

        if (VideoMultistreams)
        {
            yield return "--video-multistreams";
        }

        if (AudioMultistreams)
        {
            yield return "--audio-multistreams";
        }

        if (!string.IsNullOrWhiteSpace(MergeOutputFormat))
        {
            yield return "--merge-output-format";
            yield return MergeOutputFormat.Trim();
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        foreach (var category in Categories)
        {
            foreach (var option in category.Options)
            {
                var matchesSearch = string.IsNullOrWhiteSpace(query)
                    || option.PrimarySwitch.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || option.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || option.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchesEnabled = !ShowEnabledOnly || option.IsSelected;
                option.IsVisible = matchesSearch && matchesEnabled;
            }

            var formatBuilderMatches = category.IsFormatCategory
                && (string.IsNullOrWhiteSpace(query)
                    || "フォーマット".Contains(query, StringComparison.OrdinalIgnoreCase)
                    || "format".Contains(query, StringComparison.OrdinalIgnoreCase)
                    || FormatSelector.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || FormatSort.Contains(query, StringComparison.OrdinalIgnoreCase));
            var formatBuilderVisible = formatBuilderMatches && (!ShowEnabledOnly || IsFormatBuilderEnabled);
            var outputTemplateMatches = category.IsOutputTemplateCategory
                && (string.IsNullOrWhiteSpace(query)
                    || "出力ファイル名".Contains(query, StringComparison.OrdinalIgnoreCase)
                    || "output".Contains(query, StringComparison.OrdinalIgnoreCase)
                    || OutputTemplate.Contains(query, StringComparison.OrdinalIgnoreCase));
            var outputTemplateVisible = outputTemplateMatches && (!ShowEnabledOnly || IsOutputTemplateBuilderEnabled);
            category.IsVisible = category.Options.Any(option => option.IsVisible) || formatBuilderVisible || outputTemplateVisible;
            category.RefreshCounts();
        }

        OnPropertyChanged(nameof(SelectedOptionCount));
    }

    private void RebuildQueue()
    {
        var currentUrls = GetUrls();
        Queue.Clear();
        for (var i = 0; i < currentUrls.Count; i++)
        {
            Queue.Add(new DownloadItemViewModel(i + 1, currentUrls[i]));
        }

        OnPropertyChanged(nameof(UrlCount));
        OnPropertyChanged(nameof(QueueSummary));
        ClearQueueCommand.RaiseCanExecuteChanged();
    }

    private void ClearQueue()
    {
        Queue.Clear();
        OnPropertyChanged(nameof(QueueSummary));
        ClearQueueCommand.RaiseCanExecuteChanged();
        StatusMessage = "キューをクリアしました";
    }

    private void FormatUrlList()
    {
        var urls = ExtractUrls(UrlText);
        UrlText = string.Join(Environment.NewLine, urls);
        OnPropertyChanged(nameof(UrlCount));
        RefreshCommandPreview();
        StatusMessage = urls.Count == 0
            ? "整形できるURLが見つかりませんでした"
            : $"{urls.Count}件のURLを一覧形式に整形しました";
    }

    private List<string> GetUrls()
    {
        return ExtractUrls(UrlText);
    }

    private static List<string> ExtractUrls(string text)
    {
        var urls = new List<string>();
        var tokens = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var matches = Regex.Matches(token, @"https?://", RegexOptions.IgnoreCase);
            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : token.Length;
                var url = token[start..end].Trim();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    urls.Add(url);
                }
            }
        }

        return urls;
    }

    private void RefreshCommandPreview()
    {
        OnPropertyChanged(nameof(CommandPreview));
        OnPropertyChanged(nameof(SelectedOptionCount));
    }

    private void RefreshFormatBindings()
    {
        OnPropertyChanged(nameof(FormatGeneratedArguments));
        ApplyFilter();
        RefreshCommandPreview();
    }

    private void RefreshOutputTemplateBindings()
    {
        OnPropertyChanged(nameof(OutputTemplateGeneratedArguments));
        ApplyFilter();
        RefreshCommandPreview();
    }

    private void ApplyOutputTemplateSettings(SavedOutputTemplateSettings settings)
    {
        IsOutputTemplateBuilderEnabled = settings.IsEnabled;
        OutputTemplate = string.IsNullOrWhiteSpace(settings.Template)
            ? "%(title)s [%(id)s].%(ext)s"
            : settings.Template;
    }

    private void AppendOutputTemplateField(object? parameter)
    {
        var token = parameter switch
        {
            OutputTemplateField field => field.Token,
            string value => value,
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        OutputTemplate = string.IsNullOrWhiteSpace(OutputTemplate)
            ? token
            : $"{OutputTemplate}{token}";
        IsOutputTemplateBuilderEnabled = true;
    }

    private void ApplyDefaultOutputTemplate()
    {
        OutputTemplate = "%(title)s [%(id)s].%(ext)s";
        IsOutputTemplateBuilderEnabled = true;
    }

    private void ApplyFormatPreset(FormatPresetOption preset)
    {
        if (string.Equals(preset.Name, "カスタム", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _isApplyingFormatPreset = true;
        try
        {
            FormatResolution = preset.Resolution;
            FormatExtension = preset.Extension;
            FormatSize = preset.Size;
            FormatSortForce = preset.FormatSortForce;
            FormatSortReset = preset.FormatSortReset;
            VideoMultistreams = preset.VideoMultistreams;
            AudioMultistreams = preset.AudioMultistreams;
            MergeOutputFormat = preset.MergeOutputFormat;

            if (string.Equals(preset.Name, "既定に任せる", StringComparison.OrdinalIgnoreCase))
            {
                FormatSelector = "";
                FormatSort = "";
            }
            else
            {
                GenerateFormatFromConditions(preset.FormatSelector, preset.FormatSort);
            }
        }
        finally
        {
            _isApplyingFormatPreset = false;
        }

        RefreshFormatBindings();
    }

    private void ApplyFormatSettings(SavedFormatSettings settings)
    {
        IsFormatBuilderEnabled = settings.IsEnabled;
        var preset = FormatPresets.FirstOrDefault(item => string.Equals(item.Name, settings.PresetName, StringComparison.OrdinalIgnoreCase))
            ?? FormatPresets.FirstOrDefault(item => item.Name == "既定に任せる")
            ?? FormatPresets.FirstOrDefault();
        SelectedFormatPreset = preset;
        IsFormatDirectEdit = settings.IsDirectEdit;

        if (settings.IsDirectEdit)
        {
            _isApplyingFormatPreset = true;
            try
            {
                FormatSelector = settings.FormatSelector;
                FormatSort = settings.FormatSort;
                FormatResolution = string.IsNullOrWhiteSpace(settings.Resolution) ? "指定なし" : settings.Resolution;
                FormatExtension = string.IsNullOrWhiteSpace(settings.Extension) ? "指定なし" : settings.Extension;
                FormatSize = string.IsNullOrWhiteSpace(settings.Size) ? "指定なし" : settings.Size;
                FormatSortForce = settings.FormatSortForce;
                FormatSortReset = settings.FormatSortReset;
                VideoMultistreams = settings.VideoMultistreams;
                AudioMultistreams = settings.AudioMultistreams;
                MergeOutputFormat = settings.MergeOutputFormat;
            }
            finally
            {
                _isApplyingFormatPreset = false;
            }

            RefreshFormatBindings();
        }
        else if (preset is not null)
        {
            ApplyFormatPreset(preset);
        }
    }

    private void OnFormatConditionChanged()
    {
        if (_isApplyingFormatPreset)
        {
            return;
        }

        var custom = FormatPresets.FirstOrDefault(item => string.Equals(item.Name, "カスタム", StringComparison.OrdinalIgnoreCase));
        if (custom is not null && !ReferenceEquals(SelectedFormatPreset, custom))
        {
            SetProperty(ref _selectedFormatPreset, custom, nameof(SelectedFormatPreset));
        }

        GenerateFormatFromConditions();
        RefreshFormatBindings();
    }

    private void GenerateFormatFromConditions(string? presetFormat = null, string? presetSort = null)
    {
        var selector = string.IsNullOrWhiteSpace(presetFormat) ? "bv*+ba/b" : presetFormat;
        var sortParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(presetSort))
        {
            sortParts.AddRange(presetSort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        switch (FormatExtension)
        {
            case "mp4/m4a優先":
                selector = "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b";
                AddSort("ext");
                if (string.IsNullOrWhiteSpace(MergeOutputFormat))
                {
                    MergeOutputFormat = "mp4";
                }
                break;
            case "mp4のみ":
                selector = "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]";
                if (string.IsNullOrWhiteSpace(MergeOutputFormat))
                {
                    MergeOutputFormat = "mp4";
                }
                break;
            case "webm優先":
                selector = "bv*[ext=webm]+ba/b[ext=webm]/bv*+ba/b";
                AddSort("ext:webm");
                break;
        }

        switch (FormatResolution)
        {
            case "2160p以下":
                AddSort("res:2160");
                break;
            case "1440p以下":
                AddSort("res:1440");
                break;
            case "1080p以下":
                AddSort("res:1080");
                break;
            case "720p以下":
                AddSort("res:720");
                break;
            case "480p以下":
                AddSort("res:480");
                break;
        }

        switch (FormatSize)
        {
            case "50MB以下":
                selector = $"({selector})[filesize<50M]/b[filesize<50M]/w";
                break;
            case "100MB以下":
                selector = $"({selector})[filesize<100M]/b[filesize<100M]/w";
                break;
            case "500MB以下":
                selector = $"({selector})[filesize<500M]/b[filesize<500M]/w";
                break;
            case "容量小さめ":
                AddSort("+size");
                AddSort("+br");
                break;
        }

        FormatSelector = selector;
        FormatSort = string.Join(",", sortParts);

        void AddSort(string value)
        {
            if (!sortParts.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                sortParts.Add(value);
            }
        }
    }

    private static IReadOnlyList<FormatPresetOption> CreateFormatPresets()
    {
        return new[]
        {
            new FormatPresetOption(
                "カスタム",
                "条件や直接入力を手動で調整しています。",
                "",
                ""),
            new FormatPresetOption(
                "既定に任せる",
                "yt-dlpの既定フォーマット選択を使います。",
                "",
                ""),
            new FormatPresetOption(
                "最高画質",
                "映像と音声を最良の組み合わせで取得し、必要に応じて結合します。",
                "bv*+ba/b",
                ""),
            new FormatPresetOption(
                "最高画質 + 最高音質を結合",
                "動画のみの最高画質と音声のみの最高音質を優先して結合します。",
                "bv+ba/b",
                ""),
            new FormatPresetOption(
                "MP4/M4A優先",
                "mp4映像とm4a音声を優先し、なければ通常の最高品質へ戻します。",
                "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b",
                "ext",
                Extension: "mp4/m4a優先",
                MergeOutputFormat: "mp4"),
            new FormatPresetOption(
                "1080p以下",
                "1080p以下で最も良いフォーマットを選びます。",
                "bv*+ba/b",
                "res:1080",
                Resolution: "1080p以下"),
            new FormatPresetOption(
                "720p以下",
                "720p以下で最も良いフォーマットを選びます。",
                "bv*+ba/b",
                "res:720",
                Resolution: "720p以下"),
            new FormatPresetOption(
                "容量小さめ",
                "サイズとビットレートが小さいものを優先します。",
                "b",
                "+size,+br",
                Size: "容量小さめ"),
            new FormatPresetOption(
                "最小サイズ動画",
                "サイズ、ビットレート、解像度、FPSの順で小さいものを優先します。",
                "b",
                "+size,+br,+res,+fps",
                Size: "容量小さめ"),
            new FormatPresetOption(
                "音声のみ",
                "音声のみの最良フォーマットを選択します。",
                "ba/bestaudio",
                "")
        };
    }

    private static IReadOnlyList<OutputTemplateField> CreateOutputTemplateFields()
    {
        return new[]
        {
            new OutputTemplateField("タイトル", "%(title)s", "動画タイトル"),
            new OutputTemplateField("ID", "%(id)s", "動画ID"),
            new OutputTemplateField("拡張子", "%(ext)s", "出力ファイルの拡張子"),
            new OutputTemplateField("投稿者", "%(uploader)s", "投稿者名"),
            new OutputTemplateField("投稿日", "%(upload_date)s", "投稿日 YYYYMMDD"),
            new OutputTemplateField("プレイリスト", "%(playlist)s", "プレイリスト名"),
            new OutputTemplateField("番号", "%(playlist_index)s", "プレイリスト内の番号"),
            new OutputTemplateField("チャンネル", "%(channel)s", "チャンネル名"),
            new OutputTemplateField("サイト", "%(extractor)s", "抽出サイト名"),
            new OutputTemplateField("区切り", " - ", "見やすい区切り文字"),
            new OutputTemplateField("ID括弧", " [%(id)s]", "タイトル末尾にIDを付ける"),
            new OutputTemplateField("フォルダ", "%(uploader)s/%(title)s [%(id)s].%(ext)s", "投稿者フォルダ配下へ保存")
        };
    }

    private void AppendLog(string line)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}";
        });
    }

    private void SaveLog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "ログを保存",
            Filter = "Log (*.log)|*.log|Text (*.txt)|*.txt",
            FileName = $"yt-dlp-gui-{DateTime.Now:yyyyMMdd-HHmmss}.log"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, LogText, Encoding.UTF8);
            StatusMessage = "ログを保存しました";
        }
    }

    private static void OpenHelp()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/yt-dlp/yt-dlp#usage-and-options",
            UseShellExecute = true
        });
    }

    private static void OpenFormatSelectionHelp()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/yt-dlp/yt-dlp#format-selection",
            UseShellExecute = true
        });
    }

    private static void OpenOutputTemplateHelp()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/yt-dlp/yt-dlp#output-template",
            UseShellExecute = true
        });
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "不明";
    }

    private async Task<string> GetYtDlpVersionAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(YtDlpPath) ? "yt-dlp" : YtDlpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("--version");
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output;
            }

            return $"取得失敗 ({(string.IsNullOrWhiteSpace(error) ? $"exit code {process.ExitCode}" : error)})";
        }
        catch (Exception ex)
        {
            return $"取得失敗 ({ex.Message})";
        }
    }

    private static async Task<string> GetLatestYtDlpVersionAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("yt-dlp-gui");
            using var response = await client.GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            return document.RootElement.TryGetProperty("tag_name", out var tag)
                ? tag.GetString() ?? "不明"
                : "不明";
        }
        catch (Exception ex)
        {
            return $"取得失敗 ({ex.Message})";
        }
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    private static int CategoryOrder(string category)
    {
        string[] order =
        {
            "フォーマット",
            "出力ファイル名",
            "一般",
            "ネットワーク",
            "地域制限",
            "動画選択",
            "ダウンロード",
            "ファイル名・出力",
            "サムネイル",
            "ショートカット",
            "ログ・シミュレーション",
            "回避策",
            "形式・動画",
            "字幕",
            "認証",
            "後処理",
            "SponsorBlock",
            "抽出",
            "Preset Aliases",
            "音声",
            "プレイリスト",
            "デバッグ・上級者向け"
        };
        var index = Array.IndexOf(order, category);
        return index < 0 ? 100 : index;
    }
}
