using System.Runtime.InteropServices;
using Inventor;
using RichInventor.Commands;
using RichInventor.UI;

namespace RichInventor;

/// <summary>
/// Inventor アドインのエントリポイント。
///
/// Inventor は .addin マニフェストの ClassId でこのクラスを見つけて生成し、
/// 起動時に <see cref="Activate"/> を、終了時に <see cref="Deactivate"/> を呼ぶ。
/// ClassId は下の Guid 属性と RichInventor.addin の &lt;ClassId&gt; を一致させること。
/// </summary>
[Guid(ClientId)]
[ComVisible(true)]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    /// <summary>マニフェストの ClassId / ClientId と同じ値。</summary>
    public const string ClientId = "0165C313-EE82-48FF-9894-36CAF30D298F";

    /// <summary>Inventor の API は波括弧つきの形式を要求する。</summary>
    public const string ClientIdBraced = "{" + ClientId + "}";

    private Inventor.Application? _app;
    private readonly List<RichCommand> _commands = [];
    private RibbonBuilder? _ribbon;

    public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
    {
        _app = addInSiteObject.Application;

        _commands.Add(new ExportStlActiveCommand(_app));
        _commands.Add(new ExportStl3DpCommand(_app));
        _commands.Add(new ExportStlSelectedCommand(_app));
        _commands.Add(new ExportDimensionPdfCommand(_app, DimensionPdfMode.AspAssembly));
        _commands.Add(new ExportDimensionPdfCommand(_app, DimensionPdfMode.SelectedAssembly));
        _commands.Add(new ExportDimensionPdfCommand(_app, DimensionPdfMode.ActivePart));
        _commands.Add(new GroundLargestFaceCommand(_app, GroundMode.All));
        _commands.Add(new GroundLargestFaceCommand(_app, GroundMode.Selected));
        _commands.Add(new OpenSettingsCommand(_app));

        foreach (var command in _commands)
        {
            command.Register(ClientIdBraced);
        }

        // firstTime を見ずに毎回組み立てる。RibbonBuilder 側で「あれば使う」に
        // なっているので重複せず、ユーザーがリボンをリセットしても復活する。
        _ribbon = new RibbonBuilder(_app, ClientIdBraced);
        _ribbon.Build(_commands);
    }

    public void Deactivate()
    {
        _ribbon?.Teardown();
        _ribbon = null;

        foreach (var command in _commands)
        {
            command.Unregister();
        }
        _commands.Clear();

        _app = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>旧 API。Inventor 2009 以降は使われない。</summary>
    public void ExecuteCommand(int commandID)
    {
    }

    /// <summary>他のプログラムに公開する自動化オブジェクト。今は無し。</summary>
    public object? Automation => null;
}
