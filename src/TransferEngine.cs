using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public sealed class TransferEngine
{
    private readonly Application app;
    private readonly Document targetDoc;
    private readonly List<ITransferHandler> handlers = [];

    public TransferEngine(Application app, Document targetDoc)
    {
        this.app = app;
        this.targetDoc = targetDoc;
        handlers.Add(new WorksetTransferHandler());
        handlers.Add(new ViewTemplateTransferHandler());
    }

    public IReadOnlyList<ITransferHandler> Handlers => handlers;

    public Dictionary<string, IReadOnlyList<TransferItem>> ScanSource(string sourceFilePath)
    {
        var result = new Dictionary<string, IReadOnlyList<TransferItem>>();
        var openOptions = CreateSafeOpenOptions();

        Document? sourceDoc = null;
        try
        {
            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(sourceFilePath);
            sourceDoc = app.OpenDocumentFile(modelPath, openOptions);
            foreach (var handler in handlers)
            {
                result[handler.CategoryKey] = handler.CollectItems(sourceDoc, targetDoc);
            }
        }
        finally
        {
            sourceDoc?.Close(false);
        }

        return result;
    }

    public TransferResult Execute(
        string sourceFilePath,
        Dictionary<string, IReadOnlyList<TransferItem>> selectedByCategory,
        ConflictResolution conflictResolution,
        Action<string>? logAction)
    {
        var result = new TransferResult();
        int total = selectedByCategory.Values.Sum(items => items.Count(i => i.IsSelected));
        result.TotalSelected = total;

        if (total == 0)
        {
            logAction?.Invoke("선택된 전송 항목이 없습니다.");
            return result;
        }

        var openOptions = CreateSafeOpenOptions();
        Document? sourceDoc = null;

        try
        {
            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(sourceFilePath);
            sourceDoc = app.OpenDocumentFile(modelPath, openOptions);

            using (var tx = new Transaction(targetDoc, "선택적 표준 전송 (Selective Transfer)"))
            {
                tx.Start();

                foreach (var handler in handlers)
                {
                    if (selectedByCategory.TryGetValue(handler.CategoryKey, out var items))
                    {
                        var toTransfer = items.Where(i => i.IsSelected).ToList();
                        if (toTransfer.Count > 0)
                        {
                            logAction?.Invoke($"[{handler.CategoryTitle}] 처리 중 ({toTransfer.Count}개)...");
                            handler.ExecuteTransfer(sourceDoc, targetDoc, toTransfer, conflictResolution, result, logAction);
                        }
                    }
                }

                tx.Commit();
            }

            logAction?.Invoke($"[완료] 총 {result.CreatedCount}개 생성/복사, {result.SkippedCount}개 건너뜀, {result.ErrorCount}개 오류.");
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            string msg = $"[전송 실패] 트랜잭션 오류: {ex.Message}";
            result.Messages.Add(msg);
            logAction?.Invoke(msg);
        }
        finally
        {
            sourceDoc?.Close(false);
        }

        return result;
    }

    private static OpenOptions CreateSafeOpenOptions()
    {
        var opt = new OpenOptions
        {
            Audit = false,
            DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
        };
        return opt;
    }
}
