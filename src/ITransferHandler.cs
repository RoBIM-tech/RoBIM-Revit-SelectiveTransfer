using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public interface ITransferHandler
{
    string CategoryKey { get; }
    string CategoryTitle { get; }
    IReadOnlyList<TransferItem> CollectItems(Document sourceDoc, Document targetDoc);
    void ExecuteTransfer(
        Document sourceDoc,
        Document targetDoc,
        IReadOnlyList<TransferItem> selectedItems,
        ConflictResolution conflictResolution,
        TransferResult result,
        Action<string>? logAction);
}
