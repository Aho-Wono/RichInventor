using System.Windows.Forms;

namespace RichInventor.UI;

/// <summary>
/// Inventor のメインウィンドウを親にしてダイアログを出す。
/// 親を指定しないと、ダイアログが Inventor の裏に隠れて操作不能になることがある。
/// </summary>
public sealed class InventorOwner(IntPtr handle) : IWin32Window
{
    public IntPtr Handle { get; } = handle;

    public static InventorOwner Of(Inventor.Application app) => new(new IntPtr(app.MainFrameHWND));
}

public static class Dialogs
{
    public const string Title = "RichInventor";

    public static void Info(Inventor.Application app, string message, string? title = null) =>
        MessageBox.Show(InventorOwner.Of(app), message, title ?? Title,
            MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(Inventor.Application app, string message, string? title = null) =>
        MessageBox.Show(InventorOwner.Of(app), message, title ?? Title,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(Inventor.Application app, string message, string? title = null) =>
        MessageBox.Show(InventorOwner.Of(app), message, title ?? Title,
            MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(Inventor.Application app, string message, string? title = null) =>
        MessageBox.Show(InventorOwner.Of(app), message, title ?? Title,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>結果を出しつつ「フォルダを開くか」を聞く。</summary>
    public static bool ReportAndAskOpen(Inventor.Application app, string message, bool hadFailures, string? title = null)
    {
        var icon = hadFailures ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
        return MessageBox.Show(
            InventorOwner.Of(app),
            message + Environment.NewLine + "出力先フォルダを開きますか?",
            title ?? Title,
            MessageBoxButtons.YesNo,
            icon) == DialogResult.Yes;
    }

    /// <summary>出力先フォルダを選ばせる。キャンセルなら null。</summary>
    public static string? PickFolder(Inventor.Application app, string description, string defaultPath)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        // 既定フォルダがまだ無い場合はその親を開いておく(空振りを防ぐ)
        if (!string.IsNullOrEmpty(defaultPath))
        {
            if (Directory.Exists(defaultPath))
            {
                dialog.SelectedPath = defaultPath;
            }
            else
            {
                var parent = SafeParentOf(defaultPath);
                if (parent is not null) dialog.SelectedPath = parent;
            }
        }

        return dialog.ShowDialog(InventorOwner.Of(app)) == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public static string? PickPdfPath(Inventor.Application app, string fileName, string initialFolder)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "寸法PDFの保存先",
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            FileName = fileName,
            InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : null,
            OverwritePrompt = true
        };
        return dialog.ShowDialog(InventorOwner.Of(app)) == DialogResult.OK ? dialog.FileName : null;
    }

    private static string? SafeParentOf(string path)
    {
        try
        {
            var parent = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(parent) && Directory.Exists(parent) ? parent : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>エクスプローラでフォルダを開く。</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // フォルダを開けなくても書き出しは成功しているので、ここでは何もしない
        }
    }
}
