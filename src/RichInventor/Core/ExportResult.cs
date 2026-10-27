using System.Text;

namespace RichInventor.Core;

/// <summary>書き出し結果の集計。呼び出し側はこれを整形してダイアログに出すだけでよい。</summary>
public sealed class ExportResult
{
    /// <summary>新規に作成した件数。</summary>
    public int Created { get; set; }

    /// <summary>既存ファイルを上書きした件数。</summary>
    public int Overwritten { get; set; }

    /// <summary>失敗した件数。</summary>
    public int Failed { get; set; }

    public string OutputFolder { get; set; } = string.Empty;

    public List<string> Lines { get; } = [];

    public List<string> Errors { get; } = [];

    public int Total => Created + Overwritten + Failed;

    /// <summary>ダイアログ本文を組み立てる。</summary>
    public string Format(string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine();
        sb.AppendLine($"  新規 {Created} 件 / 上書き {Overwritten} 件 / 失敗 {Failed} 件  (計 {Total} 件)");
        sb.AppendLine($"  出力先: {OutputFolder}");

        if (Lines.Count > 0)
        {
            sb.AppendLine();
            // 一覧が長すぎるとダイアログが画面外に出るので上限を設ける
            const int max = 30;
            foreach (var line in Lines.Take(max))
            {
                sb.AppendLine("    " + line);
            }
            if (Lines.Count > max)
            {
                sb.AppendLine($"    ... 他 {Lines.Count - max} 件");
            }
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("エラーが発生した項目:");
            foreach (var e in Errors)
            {
                sb.AppendLine("    " + e);
            }
        }

        return sb.ToString();
    }
}
