using Inventor;
using RichInventor.Commands;

namespace RichInventor.UI;

/// <summary>
/// リボンに [RichInventor] タブを作り、コマンドのボタンを並べる。
///
/// Inventor はリボンの構成をユーザープロファイルに保存するため、アドインを
/// 再ロードすると同じタブ/パネルが既に存在することがある。作り直すと重複するので、
/// すべて「あれば使う、無ければ作る」で処理している。
/// </summary>
public sealed class RibbonBuilder(Inventor.Application app, string clientId)
{
    private const string TabInternalName = "RichInventor:Tab";
    private const string TabDisplayName = "RichInventor";

    /// <summary>タブに並べるパネル。この順で左から作られる。</summary>
    private static readonly (RibbonPanelKind Kind, string InternalName, string DisplayName)[] Panels =
    [
        (RibbonPanelKind.Stl, "RichInventor:Panel:Stl", "STL 出力"),
        (RibbonPanelKind.DimensionPdf, "RichInventor:Panel:DimensionPdf", "寸法PDF"),
        (RibbonPanelKind.Placement, "RichInventor:Panel:Placement", "配置")
    ];

    /// <summary>タブを出すリボン。パーツとアセンブリの両方で使えるようにする。</summary>
    private static readonly (string Name, RibbonTargets Target)[] TargetRibbons =
    [
        ("Part", RibbonTargets.Part),
        ("Assembly", RibbonTargets.Assembly)
    ];

    private readonly Inventor.Application _app = app;
    private readonly string _clientId = clientId;

    public void Build(IReadOnlyList<RichCommand> commands)
    {
        var manager = _app.UserInterfaceManager;

        foreach (var (ribbonName, ribbonTarget) in TargetRibbons)
        {
            Ribbon ribbon;
            try
            {
                ribbon = manager.Ribbons[ribbonName];
            }
            catch
            {
                // その環境に存在しないリボンは飛ばす
                continue;
            }

            var tab = GetOrCreateTab(ribbon);
            if (tab is null) continue;

            foreach (var (kind, internalName, displayName) in Panels)
            {
                var members = commands
                    .Where(c => c.Panel == kind && (c.TargetRibbons & ribbonTarget) != 0)
                    .ToList();

                // このリボンに出すボタンが無いパネルは作らない(旧版が残した空のパネルも消す)
                if (members.Count == 0)
                {
                    DeletePanelIfPresent(tab, internalName);
                    continue;
                }

                var panel = GetOrCreatePanel(tab, internalName, displayName);
                if (panel is null) continue;

                foreach (var command in commands)
                {
                    if (members.Contains(command))
                    {
                        AddButtonIfMissing(panel, command);
                    }
                    else
                    {
                        RemoveButtonIfPresent(panel, command);
                    }
                }
            }
        }
    }

    /// <summary>作ったパネルを消す。タブは他に何も無ければ一緒に消す。</summary>
    public void Teardown()
    {
        var manager = _app.UserInterfaceManager;

        foreach (var (ribbonName, _) in TargetRibbons)
        {
            try
            {
                var ribbon = manager.Ribbons[ribbonName];
                var tab = FindTab(ribbon);
                if (tab is null) continue;

                foreach (var (_, internalName, _) in Panels)
                {
                    FindPanel(tab, internalName)?.Delete();
                }

                if (tab.RibbonPanels.Count == 0)
                {
                    tab.Delete();
                }
            }
            catch
            {
                // Inventor 終了中は各種操作が失敗しうる。UI の後始末なので握りつぶす。
            }
        }
    }

    private RibbonTab? GetOrCreateTab(Ribbon ribbon)
    {
        var existing = FindTab(ribbon);
        if (existing is not null) return existing;

        try
        {
            return ribbon.RibbonTabs.Add(
                TabDisplayName,
                TabInternalName,
                _clientId,
                TargetTabInternalName: "",
                InsertBeforeTargetTab: false,
                Contextual: false);
        }
        catch
        {
            return null;
        }
    }

    private RibbonPanel? GetOrCreatePanel(RibbonTab tab, string internalName, string displayName)
    {
        var existing = FindPanel(tab, internalName);
        if (existing is not null) return existing;

        try
        {
            return tab.RibbonPanels.Add(
                displayName,
                internalName,
                _clientId,
                TargetPanelInternalName: "",
                InsertBeforeTargetPanel: false);
        }
        catch
        {
            return null;
        }
    }

    private static RibbonTab? FindTab(Ribbon ribbon)
    {
        try
        {
            return ribbon.RibbonTabs[TabInternalName];
        }
        catch
        {
            return null;
        }
    }

    private static RibbonPanel? FindPanel(RibbonTab tab, string internalName)
    {
        try
        {
            return tab.RibbonPanels[internalName];
        }
        catch
        {
            return null;
        }
    }

    private static void DeletePanelIfPresent(RibbonTab tab, string internalName)
    {
        try
        {
            FindPanel(tab, internalName)?.Delete();
        }
        catch
        {
            // Inventor が UI を更新中なら次回の起動で消えればよい
        }
    }

    private static void AddButtonIfMissing(RibbonPanel panel, RichCommand command)
    {
        try
        {
            // 既にあるなら何もしない(再ロード時の重複を防ぐ)
            var _ = panel.CommandControls[command.InternalName];
            return;
        }
        catch
        {
            // 見つからない = まだ追加されていない
        }

        try
        {
            panel.CommandControls.AddButton(
                command.Button,
                UseLargeIcon: true,
                ShowText: true,
                TargetControlInternalName: "",
                InsertBeforeTargetControl: false);
        }
        catch
        {
            // 1 つ追加できなくても他のボタンは並べる
        }
    }

    /// <summary>
    /// 旧バージョンが別のリボンに保存したコントロールを消す。
    /// ButtonDefinition 自体は共有されるため、パネル上のコントロールだけを削除する。
    /// </summary>
    private static void RemoveButtonIfPresent(RibbonPanel panel, RichCommand command)
    {
        try
        {
            panel.CommandControls[command.InternalName].Delete();
        }
        catch
        {
            // 見つからない、または Inventor が UI を更新中なら何もしない
        }
    }
}
