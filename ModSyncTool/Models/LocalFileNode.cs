using System.Collections.ObjectModel;

namespace ModSyncTool.Models;

public sealed class LocalFileNode
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public LocalFileStatus Status { get; set; }

    public bool IsDirectory { get; set; }

    public ObservableCollection<LocalFileNode> Children { get; } = new();
}
