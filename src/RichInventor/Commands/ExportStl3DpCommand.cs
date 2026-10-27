using System.Drawing;
using System.Text;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

/// <summary>
/// 機能② 開いているアセンブリが参照しているパーツを再帰的にたどり、
/// ファイル名に目印(既定 "3DP")が入っているものを一括で STL にする。
/// </summary>
public sealed class ExportStl3DpCommand(Inventor.Application app) : RichCommand(app)
{
    public override string InternalName => "RichInventor:ExportStl3DP";
    public override string DisplayName => "3DP 部品を出力";
    public override string Description =>
        "アセンブリ内のファイル名に \"3DP\" を含むパーツを、サブアセンブリの中まで探して一括で STL に書き出します。";
    public override string ToolTip => "3DP 部品をまとめて STL に書き出す";
    public override RibbonTargets TargetRibbons => RibbonTargets.Assembly;

    protected override Bitmap CreateIcon(int size) => IconFactory.Export3Dp(size);

    protected override void Execute()
    {
        var doc = App.ActiveDocument;

        if (doc is null || !DocumentScanner.IsAssembly(doc))
        {
            Dialogs.Warn(App,
                "アセンブリ(.iam)を開いた状態で実行してください。" + Environment.NewLine + Environment.NewLine +
                "選択した部品を STL にしたい場合は [選択部品を出力] を使ってください。",
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        var options = Options;
        var marker = options.PrintMarker;

        var targets = DocumentScanner.CollectParts(
            doc, marker, options.MarkerIgnoreCase, options.ExcludeOldVersions);

        if (targets.Count == 0)
        {
            Dialogs.Info(App,
                $"{DocumentScanner.BaseNameOf(doc)} の中に、ファイル名に \"{marker}\" を含むパーツが" + Environment.NewLine +
                "見つかりませんでした。" + Environment.NewLine + Environment.NewLine +
                $"・3D プリントする部品のファイル名に \"{marker}\" が入っているか確認してください" + Environment.NewLine +
                "・小文字表記も拾いたい場合は、設定の MarkerIgnoreCase を true にしてください" + Environment.NewLine +
                $"  ({AddInOptions.FilePath})",
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        var message = new StringBuilder();
        message.AppendLine($"{DocumentScanner.BaseNameOf(doc)} の中に \"{marker}\" 部品が {targets.Count} 件見つかりました。");
        message.AppendLine();

        const int max = 30;
        foreach (var target in targets.Take(max))
        {
            message.AppendLine("    " + target.BaseName + ".ipt");
        }
        if (targets.Count > max)
        {
            message.AppendLine($"    ... 他 {targets.Count - max} 件");
        }

        message.AppendLine();
        message.Append("これらを STL に書き出しますか?");

        if (!Dialogs.Confirm(App, message.ToString(), $"{Dialogs.Title} - {DisplayName}")) return;

        // 既定はアセンブリと同じ階層の 3DPstl\
        var defaultFolder = Path.Combine(DocumentScanner.FolderOf(doc), options.DefaultOutputFolderName);

        var folder = Dialogs.PickFolder(App, $"STL の出力先フォルダを選んでください({targets.Count} 件)", defaultFolder);
        if (string.IsNullOrEmpty(folder)) return;

        RunExport(targets, folder, options.ToStlSettings(),
            $"{DocumentScanner.BaseNameOf(doc)} の \"{marker}\" 部品を STL に書き出しました。");
    }
}
