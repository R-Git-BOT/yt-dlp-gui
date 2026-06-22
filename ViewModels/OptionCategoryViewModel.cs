using System.Collections.ObjectModel;
using YtDlpGui.Infrastructure;

namespace YtDlpGui.ViewModels;

public sealed class OptionCategoryViewModel : ObservableObject
{
    private bool _isExpanded;
    private bool _isVisible = true;

    public OptionCategoryViewModel(string name, IEnumerable<OptionItemViewModel> options, bool isExpanded = false)
    {
        Name = name;
        Options = new ObservableCollection<OptionItemViewModel>(options);
        _isExpanded = isExpanded;
    }

    public string Name { get; }
    public ObservableCollection<OptionItemViewModel> Options { get; }
    public bool IsGeneralCategory => string.Equals(Name, "一般", StringComparison.OrdinalIgnoreCase);
    public bool IsFormatCategory => string.Equals(Name, "フォーマット", StringComparison.OrdinalIgnoreCase);
    public bool IsOutputTemplateCategory => string.Equals(Name, "出力ファイル名", StringComparison.OrdinalIgnoreCase);

    public int SelectedCount => Options.Count(option => option.IsSelected);
    public int VisibleCount => Options.Count(option => option.IsVisible);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(VisibleCount));
    }
}
