using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RoBIM.Common;

namespace RoBIM.Feature;

[Transaction(TransactionMode.Manual)]
public sealed class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        var uiApp = data.Application;
        var doc = uiApp.ActiveUIDocument?.Document;

        if (doc is null)
        {
            TaskDialog.Show("선택적 표준 전송", "열려 있는 Revit 프로젝트 문서가 없습니다.");
            return Result.Cancelled;
        }

        var status = ReleaseMetadata.Resolve(null, "selective-transfer", "0.1.0-test.1");

        try
        {
            var engine = new TransferEngine(uiApp.Application, doc);
            var window = new TransferWindow(engine, status.Version);
            window.ShowDialog();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
