using System.Collections.ObjectModel;
using ModSyncTool.Helpers;

namespace ModSyncTool.Models;

public sealed class PublishFileItem : ObservableObject
{
    private bool _isSelected = true;
    private string _downloadSegment = string.Empty;
    private string? _overrideBaseUrl;

    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public ObservableCollection<PublishFileItem> Children { get; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DownloadSegment
    {
        get => _downloadSegment;
        set => SetProperty(ref _downloadSegment, value);
    }

    public string? OverrideBaseUrl
    {
        get => _overrideBaseUrl;
        set => SetProperty(ref _overrideBaseUrl, value);
    }
}
