using Inventor;

namespace RichInventor.Core;

/// <summary>ドキュメントから「STL にしたいもの」を集める。</summary>
public static class DocumentScanner
{
    public static bool IsPart(Document? doc) =>
        doc is not null && doc.DocumentType == DocumentTypeEnum.kPartDocumentObject;

    public static bool IsAssembly(Document? doc) =>
        doc is not null && doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject;

    /// <summary>拡張子なしファイル名。未保存なら表示名で代用する。</summary>
    public static string BaseNameOf(Document? doc)
    {
        if (doc is null) return "unnamed";

        var full = FullNameOf(doc);
        return !string.IsNullOrEmpty(full)
            ? Path.GetFileNameWithoutExtension(full)
            : Path.GetFileNameWithoutExtension(doc.DisplayName);
    }

    /// <summary>ドキュメントが置かれているフォルダ。未保存なら空文字。</summary>
    public static string FolderOf(Document? doc)
    {
        var full = FullNameOf(doc);
        return string.IsNullOrEmpty(full) ? string.Empty : Path.GetDirectoryName(full) ?? string.Empty;
    }

    private static string FullNameOf(Document? doc)
    {
        if (doc is null) return string.Empty;
        try
        {
            return doc.FullFileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// ユーザーがウィンドウを開いているパーツだけを返す。
    ///
    /// Inventor はアセンブリを開くと、参照している部品(ネジ、ベアリング、コンテンツセンター品)も
    /// Application.Documents に載せるが、これらはウィンドウを持たないので Views.Count が 0 になる。
    /// この判定がないと、アセンブリを 1 つ開いただけで数十個のネジまで STL 化してしまう。
    /// (実測: 39 ドキュメント中、Views.Count &gt; 0 は 2 件だけだった)
    /// </summary>
    public static List<Document> OpenedParts(Inventor.Application app)
    {
        var list = new List<Document>();
        foreach (Document doc in app.Documents)
        {
            if (IsPart(doc) && doc.Views.Count > 0)
            {
                list.Add(doc);
            }
        }
        return list;
    }

    /// <summary>
    /// 選択セットから対象を取り出す。
    /// 部品そのものを選んでいても、その面やエッジを選んでいても拾えるようにしてある。
    /// 同じ部品を複数インスタンス選んでも 1 件にまとめる。
    /// </summary>
    public static List<ExportTarget> FromSelection(Document doc)
    {
        var list = new List<ExportTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var obj in doc.SelectSet)
        {
            var occurrence = obj as ComponentOccurrence ?? ContainingOccurrenceOf(obj);
            if (occurrence is null) continue;

            Document? referenced;
            try
            {
                referenced = occurrence.Definition.Document as Document;
            }
            catch
            {
                continue;
            }
            if (referenced is null) continue;

            var key = FullNameOf(referenced);
            if (string.IsNullOrEmpty(key)) key = referenced.DisplayName;
            if (!seen.Add(key)) continue;

            list.Add(new ExportTarget(referenced, BaseNameOf(referenced))
            {
                Note = IsAssembly(referenced) ? "サブアセンブリ(1つのSTLに結合)" : string.Empty
            });
        }

        return list;
    }

    /// <summary>面やエッジを選んでいた場合に、それが属するオカレンスまで遡る。</summary>
    public static ComponentOccurrence? ContainingOccurrenceOf(object obj)
    {
        try
        {
            // FaceProxy / EdgeProxy など多くの型が持つプロパティ。
            // 型ごとにキャストするより遅延バインドの方が対象を取りこぼさない。
            var value = obj.GetType().InvokeMember(
                "ContainingOccurrence",
                System.Reflection.BindingFlags.GetProperty,
                binder: null,
                target: obj,
                args: null);
            return value as ComponentOccurrence;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// アセンブリが参照している全パーツを集める。
    ///
    /// AllReferencedDocuments は直接参照も間接参照もすべて返し、同じファイルは 1 回しか返さない。
    /// つまりサブアセンブリの再帰探索と同一部品の重複排除がこれ 1 つで両方片付く。
    /// (実測: 直下オカレンス 41 個のアセンブリで 35 件 = パーツ 32 + サブアセンブリ 3 を返した)
    /// </summary>
    /// <param name="matchText">ファイル名にこの文字列を含むものだけ拾う。空なら全部。</param>
    /// <param name="ignoreCase">上の判定で大文字小文字を無視するか。</param>
    /// <param name="excludeOldVersions">OldVersions\ / backup\ 配下への参照を無視するか。</param>
    public static List<ExportTarget> CollectParts(
        Document assembly,
        string matchText,
        bool ignoreCase,
        bool excludeOldVersions)
    {
        var list = new List<ExportTarget>();
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (Document referenced in assembly.AllReferencedDocuments)
        {
            if (!IsPart(referenced)) continue;

            var full = FullNameOf(referenced);
            if (string.IsNullOrEmpty(full)) continue;

            if (excludeOldVersions)
            {
                var lower = full.ToLowerInvariant();
                if (lower.Contains(@"\oldversions\") || lower.Contains(@"\backup\")) continue;
            }

            if (matchText.Length > 0)
            {
                var fileName = Path.GetFileName(full);
                if (fileName.IndexOf(matchText, comparison) < 0) continue;
            }

            list.Add(new ExportTarget(referenced, Path.GetFileNameWithoutExtension(full)));
        }

        list.Sort((a, b) => string.Compare(a.BaseName, b.BaseName, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
