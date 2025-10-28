using ModSyncTool.Helpers;

namespace ModSyncTool.ViewModels;

public sealed class RemoteUrlDialogViewModel : ObservableObject
{
    private string _url = string.Empty;

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }
}
