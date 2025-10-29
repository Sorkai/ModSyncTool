using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ModSyncTool.Helpers;

namespace ModSyncTool.Models;

public sealed class PublishFileItem : ObservableObject
{
    private bool _isSelected = true;
    private string _downloadSegment = string.Empty;
    private string? _overrideBaseUrl;
    private bool _suppressSelectionChange;

    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public ObservableCollection<PublishFileItem> Children { get; } = new();

    public PublishFileItem? Parent { get; private set; }

    public PublishFileItem()
    {
        Children.CollectionChanged += OnChildrenChanged;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }
            SetProperty(ref _isSelected, value);

            if (_suppressSelectionChange)
            {
                return;
            }

            // 当当前节点被勾选/取消时，向下传递到所有子节点
            if (IsDirectory && Children.Count > 0)
            {
                PropagateToChildren(value);
            }

            // 向上刷新父级的选中状态
            Parent?.UpdateSelectionFromChildren();
            // 自身与链路的三态显示刷新
            RaiseSelectionStateChangedUpwards();
        }
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

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var obj in e.NewItems)
            {
                if (obj is PublishFileItem child)
                {
                    child.Parent = this;
                    child.PropertyChanged += OnChildPropertyChanged;
                    // 新增子项时，默认与父级保持一致
                    child._suppressSelectionChange = true;
                    try { child.IsSelected = this.IsSelected; }
                    finally { child._suppressSelectionChange = false; }
                }
            }
        }

        if (e.OldItems != null)
        {
            foreach (var obj in e.OldItems)
            {
                if (obj is PublishFileItem child)
                {
                    child.PropertyChanged -= OnChildPropertyChanged;
                    if (child.Parent == this)
                    {
                        child.Parent = null;
                    }
                }
            }
        }

        // 子集合变化后，刷新父级的合成状态
        UpdateSelectionFromChildren();
        RaiseSelectionStateChangedUpwards();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsSelected))
        {
            UpdateSelectionFromChildren();
            RaiseSelectionStateChangedUpwards();
        }
    }

    private void PropagateToChildren(bool value)
    {
        foreach (var child in Children)
        {
            child._suppressSelectionChange = true;
            try
            {
                child.IsSelected = value;
            }
            finally
            {
                child._suppressSelectionChange = false;
            }

            if (child.IsDirectory && child.Children.Count > 0)
            {
                child.PropagateToChildren(value);
            }

            // 通知子项（及其祖先）三态状态已变更，刷新 UI 绑定到 SelectionState 的复选框
            child.RaiseSelectionStateChangedUpwards();
        }
    }

    private void UpdateSelectionFromChildren()
    {
        if (!IsDirectory || Children.Count == 0)
        {
            Parent?.UpdateSelectionFromChildren();
            return;
        }

        bool allSelected = true;
        foreach (var child in Children)
        {
            if (!child.IsSelected)
            {
                allSelected = false;
            }
        }

        // 规则：父级选中 = 所有子级都选中；否则为未选中。
        // 如需三态，可扩展一个 IsIndeterminate（bool?）并绑定到 CheckBox。
        bool newSelected = allSelected;

        if (_isSelected != newSelected)
        {
            _suppressSelectionChange = true;
            try { _isSelected = newSelected; OnPropertyChanged(nameof(IsSelected)); }
            finally { _suppressSelectionChange = false; }
        }

        // 继续向上汇总
        Parent?.UpdateSelectionFromChildren();
        RaiseSelectionStateChangedUpwards();
    }

    // 三态显示属性：true=全选，false=全不选，null=部分选
    public bool? SelectionState
    {
        get
        {
            if (!IsDirectory)
            {
                return IsSelected;
            }

            if (Children.Count == 0)
            {
                return false;
            }

            bool anyTrue = false;
            bool anyFalse = false;
            foreach (var child in Children)
            {
                var state = child.SelectionState;
                if (state == true) anyTrue = true; else if (state == false) anyFalse = true; else { anyTrue = anyTrue || true; anyFalse = anyFalse || true; }
                if (anyTrue && anyFalse) return null;
            }

            if (anyTrue && !anyFalse) return true;
            if (!anyTrue && anyFalse) return false;
            return null;
        }
        set
        {
            // 将三态设置转化为对整棵子树的强制应用：
            // - null: 根据交互规则，置为 false（清空）
            // - true/false: 统一选中/取消
            bool target = value.HasValue ? value.Value : false;

            ApplySelectionToSubtree(target);
            Parent?.UpdateSelectionFromChildren();
            RaiseSelectionStateChangedUpwards();
        }
    }

    private void RaiseSelectionStateChangedUpwards()
    {
        OnPropertyChanged(nameof(SelectionState));
        Parent?.RaiseSelectionStateChangedUpwards();
    }

    // 强制将当前节点及所有子孙节点的选中状态设置为 target，
    // 无视 IsSelected 属性中的“值相等早退”，确保在中间态下也能生效。
    private void ApplySelectionToSubtree(bool target)
    {
        // 直接写入字段并触发通知，避免 IsSelected 中的早退逻辑阻断向下传播
        _suppressSelectionChange = true;
        try
        {
            if (_isSelected != target)
            {
                _isSelected = target;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        finally
        {
            _suppressSelectionChange = false;
        }

        if (IsDirectory && Children.Count > 0)
        {
            foreach (var child in Children)
            {
                child.ApplySelectionToSubtree(target);
            }
        }

        // 通知当前节点的三态属性已变化（叶子绑定 SelectionState，目录也需要刷新）
        OnPropertyChanged(nameof(SelectionState));
    }
}
