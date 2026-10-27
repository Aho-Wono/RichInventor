using Inventor;

namespace RichInventor.Core;

/// <summary>書き出し 1 件ぶんの対象。</summary>
public sealed class ExportTarget(Document document, string baseName)
{
    /// <summary>書き出す Inventor ドキュメント。</summary>
    public Document Document { get; } = document;

    /// <summary>拡張子なしの出力ファイル名。</summary>
    public string BaseName { get; } = baseName;

    /// <summary>一覧表示用の補足(例: サブアセンブリ)。</summary>
    public string Note { get; init; } = string.Empty;

    public override string ToString() =>
        string.IsNullOrEmpty(Note) ? BaseName : $"{BaseName}  ({Note})";
}
