using System.Drawing;
using System.Text;
using Inventor;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Commands;

public enum GroundMode { All, Selected }

/// <summary>
/// アセンブリ直下のパーツ／サブアセンブリ(全部、または選択したもの)から基準部品を 1 つ選び、
/// ほかの部品の面積が最大の平面を、基準部品の最大平面にフラッシュ拘束する。
/// </summary>
public sealed class GroundLargestFaceCommand(Inventor.Application app, GroundMode mode) : RichCommand(app)
{
    public override string InternalName => mode == GroundMode.Selected
        ? "RichInventor:GroundLargestFaceSelected"
        : "RichInventor:GroundLargestFace";
    public override string DisplayName => mode == GroundMode.Selected ? "選択部品を接地" : "最大平面を接地";
    public override string Description => mode == GroundMode.Selected
        ? "選択したパーツ／サブアセンブリから基準部品を選び、ほかの部品の面積が最大の平面を基準部品の最大平面にフラッシュ拘束します。"
        : "アセンブリ直下のパーツ／サブアセンブリから基準部品を選び、ほかの部品の面積が最大の平面を基準部品の最大平面にフラッシュ拘束します。";
    public override string ToolTip => mode == GroundMode.Selected
        ? "選択した部品の最大平面を基準部品の最大平面にそろえる"
        : "各部品の最大平面を基準部品の最大平面にそろえる";
    public override RibbonTargets TargetRibbons => RibbonTargets.Assembly;
    public override RibbonPanelKind Panel => RibbonPanelKind.Placement;

    protected override Bitmap CreateIcon(int size) => mode == GroundMode.Selected
        ? IconFactory.GroundSelected(size)
        : IconFactory.GroundLargestFace(size);

    protected override void Execute()
    {
        var title = $"{Dialogs.Title} - {DisplayName}";
        if (App.ActiveDocument is not AssemblyDocument assembly)
        {
            Dialogs.Warn(App, "アセンブリ(.iam)を開いた状態で実行してください。", title);
            return;
        }

        var definition = assembly.ComponentDefinition;
        if (definition.ActiveOccurrence is not null)
        {
            Dialogs.Warn(App,
                "部品をインプレース編集中です。" + Environment.NewLine +
                "編集を終了してアセンブリに戻ってから実行してください。", title);
            return;
        }

        HashSet<string>? selected = null;
        if (mode == GroundMode.Selected)
        {
            selected = GroundAligner.SelectedTopLevelNames(assembly);
            if (selected.Count == 0)
            {
                Dialogs.Info(App,
                    "接地する部品が選択されていません。" + Environment.NewLine +
                    "モデルツリーまたは画面上でパーツやサブアセンブリを選択してください。", title);
                return;
            }
        }

        GroundPlan plan;
        var previous = App.StatusBarText;
        try
        {
            App.StatusBarText = "RichInventor: 各部品の最大平面を探しています...";
            plan = GroundAligner.Plan(assembly, selected);
        }
        finally
        {
            App.StatusBarText = previous;
        }

        if (plan.Base is not { } basis || plan.TargetCount == 0)
        {
            var none = new StringBuilder();
            none.AppendLine("基準部品のほかに接地できる部品がありませんでした。");
            if (mode == GroundMode.Selected)
            {
                none.AppendLine("基準にする部品と、そろえたい部品を合わせて 2 個以上選択してください。");
            }
            if (plan.Base is not null) none.AppendLine($"基準部品: {plan.Base.Name}");
            GroundAligner.AppendList(none, "スキップした項目:", plan.SkipLines);
            Dialogs.Info(App, none.ToString(), title);
            return;
        }

        var message = new StringBuilder();
        var subject = mode == GroundMode.Selected ? "選択した" : "アセンブリ直下の";
        message.AppendLine($"基準: {basis.Name} の最大平面 ({basis.AreaCm2 * 100:0.#} mm²)");
        if (plan.BaseNeedsGround) message.AppendLine("  (基準部品を固定します)");
        message.AppendLine();
        message.AppendLine($"{subject} {plan.TargetCount} 個の部品の最大平面を、この面にフラッシュ拘束します。");
        message.AppendLine();
        if (plan.UngroundCount > 0) message.AppendLine($"  固定を解除: {plan.UngroundCount} 個");
        if (plan.ReplaceCount > 0) message.AppendLine($"  前回の接地拘束を付け直し: {plan.ReplaceCount} 個");
        if (plan.SkipLines.Count > 0) message.AppendLine($"  スキップ: {plan.SkipLines.Count} 個(理由は完了後に表示します)");
        message.AppendLine();
        message.AppendLine("実行後は Ctrl+Z 1 回でまとめて元に戻せます。続行しますか?");
        if (!Dialogs.Confirm(App, message.ToString(), title)) return;

        GroundResult result;
        try
        {
            App.StatusBarText = $"RichInventor: {plan.TargetCount} 個の部品を拘束しています...";
            result = GroundAligner.Apply(App, plan);
        }
        finally
        {
            App.StatusBarText = previous;
        }

        var report = result.Format($"{result.Added} 個の部品の最大平面を {basis.Name} の最大平面に拘束しました。");
        if (result.Failed > 0)
        {
            Dialogs.Warn(App, report, title);
        }
        else
        {
            Dialogs.Info(App, report, title);
        }
    }
}
