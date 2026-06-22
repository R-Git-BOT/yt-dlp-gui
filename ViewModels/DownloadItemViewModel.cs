using System.Globalization;
using YtDlpGui.Infrastructure;

namespace YtDlpGui.ViewModels;

public sealed class DownloadItemViewModel : ObservableObject
{
    private string _status = "待機中";
    private double _progress;
    private string _speed = "-";
    private string _site = "-";

    public DownloadItemViewModel(int index, string url)
    {
        Index = index;
        Url = url;
        Site = InferSite(url);
    }

    public int Index { get; }
    public string Url { get; }

    public string Site
    {
        get => _site;
        set => SetProperty(ref _site, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (SetProperty(ref _progress, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string ProgressText => Progress.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public string Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    private static string InferSite(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "") : "-";
    }
}
