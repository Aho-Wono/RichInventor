using System.Text.Json;
using System.Text.Json.Serialization;

namespace RichInventor.Core;

/// <summary>
/// 再ビルドせずに変えられる設定。
/// %APPDATA%\RichInventor\settings.json に置かれ、無ければ既定値で自動生成される。
/// </summary>
public sealed class AddInOptions
{
    /// <summary>3Dプリント対象の目印にするファイル名の文字列。</summary>
    public string PrintMarker { get; set; } = "3DP";

    /// <summary>目印の判定で大文字小文字を無視するか。false なら "3DP" 完全一致のみ。</summary>
    public bool MarkerIgnoreCase { get; set; }

    /// <summary>OldVersions\ / backup\ 配下への参照を無視するか。</summary>
    public bool ExcludeOldVersions { get; set; }

    /// <summary>出力先の既定フォルダ名(アセンブリと同じ階層に作られる)。</summary>
    public string DefaultOutputFolderName { get; set; } = "3DPstl";

    /// <summary>出力単位。5=ミリメートル。<see cref="StlSettings"/> の定数を参照。</summary>
    public int StlUnits { get; set; } = StlSettings.UnitMillimeter;

    /// <summary>メッシュの細かさ。0=高精度。</summary>
    public int StlResolution { get; set; } = StlSettings.ResolutionHigh;

    /// <summary>true=バイナリSTL / false=ASCII STL。</summary>
    public bool StlBinary { get; set; } = true;

    /// <summary>STLに色情報を埋めるか。スライサ互換性のため既定は false。</summary>
    public bool StlExportColor { get; set; }

    [JsonIgnore]
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RichInventor");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Directory, "settings.json");

    public StlSettings ToStlSettings() => new()
    {
        Units = StlUnits,
        Resolution = StlResolution,
        Binary = StlBinary,
        ExportColor = StlExportColor
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>設定を読む。壊れていたり無かったりしたら既定値を返す(例外は投げない)。</summary>
    public static AddInOptions Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AddInOptions>(json, SerializerOptions);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // 設定が壊れていても機能は使えるべきなので、既定値で続行する
        }

        var defaults = new AddInOptions();
        defaults.TrySave();
        return defaults;
    }

    public bool TrySave()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SerializerOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
