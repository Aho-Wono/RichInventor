using Inventor;

namespace RichInventor.Core;

/// <summary>
/// Inventor の STL トランスレータを叩いて STL を書き出す。
/// 全機能の共通の出口はここ。新しい書き出し系の機能もここを通す。
/// </summary>
public sealed class StlExporter(Inventor.Application application)
{
    /// <summary>STL トランスレータの AddIn ID (Inventor 共通の固定値)。</summary>
    public const string TranslatorAddInId = "{533E9A98-FC3B-11D4-8E7E-0010B541CD80}";

    private readonly Inventor.Application _app = application;

    /// <summary>
    /// 1 ドキュメントを STL に書き出す。
    /// パーツでもアセンブリでも呼べる。アセンブリを渡すと 1 つの STL に結合される。
    /// </summary>
    public void ExportDocument(Document source, string targetPath, StlSettings settings)
    {
        var translator = GetTranslator();

        var transient = _app.TransientObjects;

        var context = transient.CreateTranslationContext();
        context.Type = IOMechanismEnum.kFileBrowseIOMechanism;

        // 既定オプションを取得してから、変えたいものだけ上書きする。
        // 自前で全項目を Add すると Inventor 側の項目追加に追従できないため。
        var options = transient.CreateNameValueMap();
        if (!translator.HasSaveCopyAsOptions[source, context, options])
        {
            throw new InvalidOperationException($"このドキュメントは STL に書き出せません: {source.DisplayName}");
        }

        options.Value["ExportUnits"] = settings.Units;
        options.Value["Resolution"] = settings.Resolution;
        options.Value["OutputFileType"] = settings.Binary ? 0 : 1;
        options.Value["ExportColor"] = settings.ExportColor;

        if (settings.Resolution == StlSettings.ResolutionCustom)
        {
            if (settings.SurfaceDeviation > 0) options.Value["SurfaceDeviation"] = settings.SurfaceDeviation;
            if (settings.NormalDeviation > 0) options.Value["NormalDeviation"] = settings.NormalDeviation;
            if (settings.MaxEdgeLength > 0) options.Value["MaxEdgeLength"] = settings.MaxEdgeLength;
            if (settings.AspectRatio > 0) options.Value["AspectRatio"] = settings.AspectRatio;
        }

        var medium = transient.CreateDataMedium();
        medium.FileName = targetPath;

        translator.SaveCopyAs(source, context, options, medium);

        // Inventor は失敗しても例外を出さないことがあるので、実際にできたか確かめる
        if (!File.Exists(targetPath))
        {
            throw new IOException($"書き出しは呼べたがファイルができていない: {targetPath}");
        }
    }

    /// <summary>対象リストをまとめて書き出す。1 件失敗しても残りは続行する。</summary>
    public ExportResult ExportAll(IReadOnlyList<ExportTarget> targets, string outputFolder, StlSettings settings)
    {
        var result = new ExportResult { OutputFolder = outputFolder };

        Directory.CreateDirectory(outputFolder);

        // 別フォルダの同名パーツが混ざったときに上書きし合わないよう連番を振る
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            var name = SanitizeFileName(target.BaseName);
            if (used.TryGetValue(name, out var count))
            {
                used[name] = count + 1;
                name = $"{name}_{count + 1}";
            }
            else
            {
                used[name] = 1;
            }

            var path = Path.Combine(outputFolder, name + ".stl");
            var existed = File.Exists(path);

            try
            {
                ExportDocument(target.Document, path, settings);
                if (existed)
                {
                    result.Overwritten++;
                    result.Lines.Add($"{name}.stl  (上書き)");
                }
                else
                {
                    result.Created++;
                    result.Lines.Add($"{name}.stl");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{target.BaseName} : {ex.Message}");
            }
        }

        return result;
    }

    private TranslatorAddIn GetTranslator()
    {
        TranslatorAddIn? translator = null;
        try
        {
            translator = (TranslatorAddIn)_app.ApplicationAddIns.ItemById[TranslatorAddInId];
        }
        catch
        {
            // 下の null チェックでまとめて扱う
        }

        if (translator is null)
        {
            throw new InvalidOperationException(
                "STL トランスレータが見つかりません。Inventor のアドイン設定を確認してください。");
        }

        if (!translator.Activated)
        {
            translator.Activate();
        }

        return translator;
    }

    /// <summary>Windows のファイル名に使えない文字を _ に置き換える。</summary>
    public static string SanitizeFileName(string name)
    {
        var sanitized = name;
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }
        sanitized = sanitized.Trim();
        return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
    }
}
