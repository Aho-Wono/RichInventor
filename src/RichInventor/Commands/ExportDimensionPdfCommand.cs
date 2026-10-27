using System.Drawing;
using Inventor;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

public enum DimensionPdfMode { AspAssembly, SelectedAssembly, ActivePart }

/// <summary>対象の選び方だけを分け、寸法解析とPDF生成は共通化する。</summary>
public sealed class ExportDimensionPdfCommand(Inventor.Application app, DimensionPdfMode mode) : RichCommand(app)
{
    public override string InternalName => mode switch
    {
        DimensionPdfMode.AspAssembly => "RichInventor:DimensionPdfASP",
        DimensionPdfMode.SelectedAssembly => "RichInventor:DimensionPdfSelected",
        _ => "RichInventor:DimensionPdfPart"
    };
    public override string DisplayName => mode switch
    {
        DimensionPdfMode.AspAssembly => "ASP部品の寸法PDF",
        DimensionPdfMode.SelectedAssembly => "選択部品の寸法PDF",
        _ => "開いているパーツの寸法PDF"
    };
    public override string Description => "部品の外形・穴・投影図をまとめた寸法一覧PDFを出力します。";
    public override string ToolTip => DisplayName;
    public override RibbonTargets TargetRibbons => mode == DimensionPdfMode.ActivePart
        ? RibbonTargets.Part : RibbonTargets.Assembly;
    public override RibbonPanelKind Panel => RibbonPanelKind.DimensionPdf;

    protected override Bitmap CreateIcon(int size) => IconFactory.DimensionPdf(size);

    protected override void Execute()
    {
        List<PdfPartTarget> targets;
        string baseName;
        string folder;
        var active = App.ActiveDocument;
        if (mode == DimensionPdfMode.ActivePart)
        {
            if (active is not PartDocument part)
            {
                Dialogs.Warn(App, "パーツ(.ipt)を開いてください。", DisplayName);
                return;
            }
            targets = PdfTargetScanner.FromPart(part);
            baseName = $"{DocumentScanner.BaseNameOf((Inventor.Document)(object)part)}_寸法.pdf";
            folder = DocumentScanner.FolderOf((Inventor.Document)(object)part);
        }
        else
        {
            if (active is not AssemblyDocument assembly)
            {
                Dialogs.Warn(App, "アセンブリ(.iam)を開いてください。", DisplayName);
                return;
            }
            targets = mode == DimensionPdfMode.AspAssembly
                ? PdfTargetScanner.FromAssembly(assembly, "ASP")
                : PdfTargetScanner.FromSelection(assembly);
            var assemblyName = DocumentScanner.BaseNameOf((Inventor.Document)(object)assembly);
            baseName = $"{assemblyName}_{(mode == DimensionPdfMode.AspAssembly ? "ASP寸法一覧" : "選択部品寸法")}.pdf";
            folder = DocumentScanner.FolderOf((Inventor.Document)(object)assembly);
        }
        if (targets.Count == 0)
        {
            Dialogs.Info(App, mode == DimensionPdfMode.SelectedAssembly
                ? "部品を選択してください。" : "対象の部品が見つかりませんでした。", DisplayName);
            return;
        }

        var path = Dialogs.PickPdfPath(App, baseName, folder);
        if (path is null) return;
        var previousStatus = App.StatusBarText;
        try
        {
            App.StatusBarText = $"RichInventor: {targets.Count} 部品の寸法PDFを作成しています...";
            new DimensionPdfExporter(App).Export(targets, path);
        }
        finally { App.StatusBarText = previousStatus; }
        if (Dialogs.ReportAndAskOpen(App,
            $"{targets.Count} 部品の寸法PDFを作成しました。{Environment.NewLine}{path}{Environment.NewLine}",
            false, DisplayName))
            Dialogs.OpenFolder(Path.GetDirectoryName(path)!);
    }
}
