using System.ComponentModel;
using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public enum ConflictResolution
{
    [Description("건너뛰기 (기존 유지)")]
    Skip,
    [Description("새 이름으로 복사")]
    Rename
}

public sealed class TransferItem : INotifyPropertyChanged
{
    private bool isSelected = true;

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool ExistsInTarget { get; init; }
    public string StatusDescription => ExistsInTarget ? "기존 존재" : "신규";
    public ElementId? SourceElementId { get; init; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected != value)
            {
                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TransferResult
{
    public int TotalSelected { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Messages { get; } = [];
}
