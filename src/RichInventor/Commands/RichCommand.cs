using System.Drawing;
using Inventor;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

[Flags]
public enum RibbonTargets
{
    None = 0,
    Part = 1,
    Assembly = 2,
    All = Part | Assembly
}

/// <summary>ボタンを置くパネル。</summary>
public enum RibbonPanelKind
{
    Stl,
    DimensionPdf,
    Placement
}

/// <summary>
/// リボンボタン 1 個ぶん。ButtonDefinition の生成とイベント購読の面倒を引き受け、
/// 派生クラスは <see cref="Execute"/> に中身だけ書けばよい。
/// </summary>
public abstract class RichCommand(Inventor.Application application)
{
    protected Inventor.Application App { get; } = application;

    private ButtonDefinition? _button;
    private ButtonDefinitionSink_OnExecuteEventHandler? _handler;

    public abstract string InternalName { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract string ToolTip { get; }
    public abstract RibbonTargets TargetRibbons { get; }
    public virtual RibbonPanelKind Panel => RibbonPanelKind.Stl;

    /// <summary>指定サイズのアイコンを描く(16 と 32 が要求される)。</summary>
    protected abstract Bitmap CreateIcon(int size);

    public ButtonDefinition Button =>
        _button ?? throw new InvalidOperationException($"{InternalName} はまだ登録されていません。");

    /// <summary>ButtonDefinition を作ってイベントを繋ぐ。</summary>
    public void Register(string clientId)
    {
        var definitions = App.CommandManager.ControlDefinitions;

        // アドインを再ロードした場合、前回の定義が残っていることがある。
        // 同じ InternalName で作り直すと例外になるので、あれば再利用する。
        _button = TryGetExisting(definitions, InternalName);

        if (_button is null)
        {
            object? small = null;
            object? large = null;
            try
            {
                using var smallBitmap = CreateIcon(16);
                using var largeBitmap = CreateIcon(32);
                small = IconFactory.ToPictureDisp(smallBitmap);
                large = IconFactory.ToPictureDisp(largeBitmap);
            }
            catch
            {
                // アイコンが作れなくてもボタン自体は機能させる
            }

            _button = definitions.AddButtonDefinition(
                DisplayName,
                InternalName,
                CommandTypesEnum.kShapeEditCmdType,
                clientId,
                Description,
                ToolTip,
                small!,
                large!,
                ButtonDisplayEnum.kDisplayTextInLearningMode);
        }

        _handler = OnExecuteInternal;
        ((ButtonDefinitionSink_Event)_button).OnExecute += _handler;
    }

    /// <summary>イベントを外して定義を消す。Deactivate から呼ぶ。</summary>
    public void Unregister()
    {
        if (_button is null) return;

        try
        {
            if (_handler is not null)
            {
                ((ButtonDefinitionSink_Event)_button).OnExecute -= _handler;
            }
        }
        catch
        {
            // Inventor 終了中は失敗しうる。放置してよい。
        }

        try
        {
            _button.Delete();
        }
        catch
        {
            // 同上
        }

        _handler = null;
        _button = null;
    }

    private static ButtonDefinition? TryGetExisting(ControlDefinitions definitions, string internalName)
    {
        try
        {
            return definitions[internalName] as ButtonDefinition;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// COM のコールバック境界。ここで例外を止めないと Inventor 側に伝播して
    /// 不安定になるので、必ず捕まえてダイアログにする。
    /// </summary>
    private void OnExecuteInternal(NameValueMap context)
    {
        try
        {
            Execute();
        }
        catch (Exception ex)
        {
            Dialogs.Error(App,
                $"{DisplayName} の実行中にエラーが発生しました。" + Environment.NewLine + Environment.NewLine + ex.Message,
                $"{Dialogs.Title} - {DisplayName}");
        }
    }

    protected abstract void Execute();

    // ---- 派生クラス向けの共通処理 ----

    protected AddInOptions Options => AddInOptions.Load();

    /// <summary>書き出しを実行し、結果ダイアログまで出す。</summary>
    protected void RunExport(IReadOnlyList<ExportTarget> targets, string outputFolder, StlSettings settings, string headline)
    {
        var exporter = new StlExporter(App);
        ExportResult result;

        // 件数が多いと数十秒かかるのでカーソルを砂時計にしておく
        var previous = App.StatusBarText;
        try
        {
            App.StatusBarText = $"RichInventor: {targets.Count} 件を STL に書き出しています...";
            result = exporter.ExportAll(targets, outputFolder, settings);
        }
        finally
        {
            App.StatusBarText = previous;
        }

        if (Dialogs.ReportAndAskOpen(App, result.Format(headline), result.Failed > 0,
                $"{Dialogs.Title} - {DisplayName}"))
        {
            Dialogs.OpenFolder(result.OutputFolder);
        }
    }
}
