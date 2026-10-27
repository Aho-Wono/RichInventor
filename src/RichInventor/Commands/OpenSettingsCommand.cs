using System.Drawing;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

/// <summary>設定ファイル(settings.json)を既定のエディタで開く。</summary>
public sealed class OpenSettingsCommand(Inventor.Application app) : RichCommand(app)
{
    public override string InternalName => "RichInventor:OpenSettings";
    public override string DisplayName => "設定";
    public override string Description => "RichInventor の設定ファイル(settings.json)を開きます。";
    public override string ToolTip => "設定ファイルを開く";
    public override RibbonTargets TargetRibbons => RibbonTargets.All;

    protected override Bitmap CreateIcon(int size) => IconFactory.Settings(size);

    protected override void Execute()
    {
        // 無ければ既定値で作られる
        var options = AddInOptions.Load();
        if (!File.Exists(AddInOptions.FilePath) && !options.TrySave())
        {
            Dialogs.Error(App,
                "設定ファイルを作成できませんでした:" + Environment.NewLine + AddInOptions.FilePath,
                $"{Dialogs.Title} - {DisplayName}");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AddInOptions.FilePath,
                UseShellExecute = true
            });
        }
        catch
        {
            // 関連付けが無い環境ではフォルダだけ開く
            Dialogs.OpenFolder(AddInOptions.Directory);
        }
    }
}
