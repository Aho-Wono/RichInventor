using System.Drawing;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

/// <summary>
/// アセンブリで選択したパーツまたはサブアセンブリを STL にする。
/// パーツは個別に、サブアセンブリは選択単位で結合して出力する。
/// </summary>
public sealed class ExportStlSelectedCommand(Inventor.Application app) : RichCommand(app)
{
    public override string InternalName => "RichInventor:ExportStlSelected";
    public override string DisplayName => "選択部品を出力";
    public override string Description =>
        "アセンブリで選択したパーツまたはサブアセンブリを STL に書き出します。";
    public override string ToolTip => "選択した部品を STL に書き出す";
    public override RibbonTargets TargetRibbons => RibbonTargets.Assembly;

    protected override Bitmap CreateIcon(int size) => IconFactory.ExportSelected(size);

    protected override void Execute()
    {
        var doc = App.ActiveDocument;
        if (doc is null || !DocumentScanner.IsAssembly(doc))
        {
            Dialogs.Warn(App,
                "アセンブリ(.iam)を開いた状態で実行してください。",
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        var targets = DocumentScanner.FromSelection(doc);
        if (targets.Count == 0)
        {
            Dialogs.Info(App,
                "書き出す部品が選択されていません。" + Environment.NewLine +
                "モデルツリーまたは画面上でパーツやサブアセンブリを選択してください。",
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        var options = Options;
        var defaultFolder = Path.Combine(
            DocumentScanner.FolderOf(doc), options.DefaultOutputFolderName);
        var folder = Dialogs.PickFolder(App,
            $"STL の出力先フォルダを選んでください({targets.Count} 件)", defaultFolder);
        if (string.IsNullOrEmpty(folder)) return;

        RunExport(targets, folder, options.ToStlSettings(),
            $"選択した {targets.Count} 件を STL に書き出しました。");
    }
}
