using System.Collections.ObjectModel;
using YtDlpGui.Infrastructure;
using YtDlpGui.Models;

namespace YtDlpGui.ViewModels;

public sealed class OptionItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _value = "";
    private bool _isVisible = true;

    public OptionItemViewModel(YtDlpOptionDefinition definition)
    {
        Definition = definition;
        Choices = new ObservableCollection<string>(definition.Choices);
        if (Choices.Count > 0)
        {
            _value = Choices[0];
        }
    }

    public YtDlpOptionDefinition Definition { get; }
    public ObservableCollection<string> Choices { get; }

    public string PrimarySwitch => Definition.PrimarySwitch;
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public string ArgumentName => Definition.ArgumentName ?? "";
    public bool RequiresArgument => Definition.RequiresArgument;
    public bool UseChoiceInput => RequiresArgument && Choices.Count > 0;
    public bool UseTextInput => RequiresArgument && Choices.Count == 0;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
