using System.Drawing;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

/// <summary>
/// 開いているアクティブなパーツを STL にする。
/// </summary>
public sealed class ExportStlActiveCommand(Inventor.Application app) : RichCommand(app)
{
    public override string InternalName => "RichInventor:ExportStlActive";
    public override string DisplayName => "開いているパーツを出力";
    public override string Description => "現在アクティブなパーツを STL に書き出します。";
    public override string ToolTip => "開いているパーツを STL に書き出す";
    public override RibbonTargets TargetRibbons => RibbonTargets.Part;

    protected override Bitmap CreateIcon(int size) => IconFactory.ExportActive(size);

    protected override void Execute()
    {
        var doc = App.ActiveDocument;
        if (doc is null || !DocumentScanner.IsPart(doc))
        {
            Dialogs.Warn(App,
                "パーツ(.ipt)を開いた状態で実行してください。",
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        var options = Options;
        var name = DocumentScanner.BaseNameOf(doc);
        var targets = new List<ExportTarget> { new(doc, name) };
        var defaultFolder = DocumentScanner.FolderOf(doc);

        var folder = Dialogs.PickFolder(App, "STL の出力先フォルダを選んでください(1 件)", defaultFolder);
        if (string.IsNullOrEmpty(folder)) return;

        RunExport(targets, folder, options.ToStlSettings(), $"{name} を STL に書き出しました。");
    }
}
