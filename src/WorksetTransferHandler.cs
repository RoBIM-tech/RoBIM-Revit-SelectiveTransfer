using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public sealed class WorksetTransferHandler : ITransferHandler
{
    public string CategoryKey => "worksets";
    public string CategoryTitle => "작업 세트 (Workset)";

    public IReadOnlyList<TransferItem> CollectItems(Document sourceDoc, Document targetDoc)
    {
        var items = new List<TransferItem>();
        if (!sourceDoc.IsWorkshared)
        {
            return items;
        }

        var targetWorksetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (targetDoc.IsWorkshared)
        {
            var targetCollector = new FilteredWorksetCollector(targetDoc).OfKind(WorksetKind.UserWorkset);
            foreach (var ws in targetCollector)
            {
                targetWorksetNames.Add(ws.Name);
            }
        }

        var sourceCollector = new FilteredWorksetCollector(sourceDoc).OfKind(WorksetKind.UserWorkset);
        foreach (var ws in sourceCollector)
        {
            bool exists = targetWorksetNames.Contains(ws.Name);
            items.Add(new TransferItem
            {
                Id = ws.Id.IntegerValue.ToString(),
                Name = ws.Name,
                Category = CategoryTitle,
                ExistsInTarget = exists,
                IsSelected = !exists // 신규 항목은 기본 선택, 기존 항목은 기본 해제
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

        if (!targetDoc.IsWorkshared)
        {
            string msg = "대상 프로젝트가 작업 공유(Workshared) 모델이 아니어서 작업 세트를 생성할 수 없습니다.";
            result.ErrorCount += selectedItems.Count;
            result.Messages.Add(msg);
            logAction?.Invoke(msg);
            return;
        }

        var existingNames = new HashSet<string>(
            new FilteredWorksetCollector(targetDoc).OfKind(WorksetKind.UserWorkset).Select(w => w.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in selectedItems)
        {
            string targetName = item.Name;
            if (existingNames.Contains(targetName))
            {
                if (conflictResolution == ConflictResolution.Skip)
                {
                    result.SkippedCount++;
                    logAction?.Invoke($"[건너뜀] 기존 작업 세트 유지: {item.Name}");
                    continue;
                }
                else
                {
                    // Rename
                    int suffix = 1;
                    while (existingNames.Contains($"{item.Name}_{suffix}"))
                    {
                        suffix++;
                    }
                    targetName = $"{item.Name}_{suffix}";
                }
            }

            try
            {
                Workset.Create(targetDoc, targetName);
                existingNames.Add(targetName);
                result.CreatedCount++;
                logAction?.Invoke($"[생성 완료] 작업 세트: {targetName}");
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                string err = $"[오류] '{targetName}' 작업 세트 생성 실패: {ex.Message}";
                result.Messages.Add(err);
                logAction?.Invoke(err);
            }
        }
    }
}
