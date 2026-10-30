using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public sealed class ViewTemplateTransferHandler : ITransferHandler
{
    public string CategoryKey => "viewtemplates";
    public string CategoryTitle => "뷰 템플릿 (View Template)";

    public IReadOnlyList<TransferItem> CollectItems(Document sourceDoc, Document targetDoc)
    {
        var items = new List<TransferItem>();

        var targetTemplateNames = new HashSet<string>(
            new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        var sourceTemplates = new FilteredElementCollector(sourceDoc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .ToList();

        foreach (var vt in sourceTemplates)
        {
            bool exists = targetTemplateNames.Contains(vt.Name);
            items.Add(new TransferItem
            {
                Id = vt.Id.Value.ToString(),
                Name = vt.Name,
                Category = CategoryTitle,
                SourceElementId = vt.Id,
                ExistsInTarget = exists,
                IsSelected = !exists
            });
        }

        return items.OrderBy(x => x.ExistsInTarget).ThenBy(x => x.Name).ToList();
    }

    public void ExecuteTransfer(
        Document sourceDoc,
        Document targetDoc,
        IReadOnlyList<TransferItem> selectedItems,
        ConflictResolution conflictResolution,
        TransferResult result,
        Action<string>? logAction)
    {
        if (selectedItems.Count == 0) return;

        var targetTemplateNames = new HashSet<string>(
            new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        var idsToCopy = new List<ElementId>();
        foreach (var item in selectedItems)
        {
            if (item.SourceElementId is null) continue;

            if (targetTemplateNames.Contains(item.Name) && conflictResolution == ConflictResolution.Skip)
            {
                result.SkippedCount++;
                logAction?.Invoke($"[건너뜀] 기존 뷰 템플릿 유지: {item.Name}");
                continue;
            }

            idsToCopy.Add(item.SourceElementId);
        }

        if (idsToCopy.Count == 0) return;

        try
        {
            var options = new CopyPasteOptions();
            var copiedIds = ElementTransformUtils.CopyElements(sourceDoc, idsToCopy, targetDoc, Transform.Identity, options);
            result.CreatedCount += copiedIds.Count;
            logAction?.Invoke($"[복사 완료] 뷰 템플릿 {copiedIds.Count}개 전송 완료");
        }
        catch (Exception ex)
        {
            result.ErrorCount += idsToCopy.Count;
            string err = $"[오류] 뷰 템플릿 복사 실패: {ex.Message}";
            result.Messages.Add(err);
            logAction?.Invoke(err);
        }
    }
}
