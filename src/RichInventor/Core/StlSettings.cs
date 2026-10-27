namespace RichInventor.Core;

/// <summary>
/// STL 書き出しの設定。既定値は 3D プリント用に振ってある。
/// </summary>
public sealed class StlSettings
{
    // ---- ExportUnits の値 (Inventor 2025.4 で実測して確定) ----
    public const int UnitInch = 2;
    public const int UnitFoot = 3;
    public const int UnitCentimeter = 4;
    public const int UnitMillimeter = 5;
    public const int UnitMeter = 6;
    public const int UnitMicron = 7;

    // ---- Resolution の値 (Inventor 2025.4 で実測して確定) ----
    /// <summary>高精度。実測で三角形数 2800(28mm角の部品)。</summary>
    public const int ResolutionHigh = 0;
    /// <summary>中精度。実測 1584。</summary>
    public const int ResolutionMedium = 1;
    /// <summary>低精度。実測 1020。</summary>
    public const int ResolutionLow = 2;
    /// <summary>カスタム。SurfaceDeviation などを別途指定する。</summary>
    public const int ResolutionCustom = 3;
    /// <summary>Brep。モデルの表示メッシュをそのまま使う(Inventor の既定)。</summary>
    public const int ResolutionBrep = 4;

    /// <summary>出力単位。既定はミリメートル。</summary>
    public int Units { get; set; } = UnitMillimeter;

    /// <summary>メッシュの細かさ。既定は高精度。</summary>
    public int Resolution { get; set; } = ResolutionHigh;

    /// <summary>true=バイナリ STL / false=ASCII STL。バイナリの方がファイルサイズが 1/10 以下になる。</summary>
    public bool Binary { get; set; } = true;

    /// <summary>
    /// STL に色情報を埋めるか。バイナリ STL の属性バイトに色を入れる方式は標準ではなく、
    /// スライサによっては誤解釈するため既定は false。
    /// </summary>
    public bool ExportColor { get; set; }

    // Resolution が ResolutionCustom のときだけ使われる。0 以下なら Inventor の既定値のまま触らない。
    public double SurfaceDeviation { get; set; }
    public double NormalDeviation { get; set; }
    public double MaxEdgeLength { get; set; }
    public double AspectRatio { get; set; }

    public StlSettings Clone() => (StlSettings)MemberwiseClone();
}
