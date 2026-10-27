using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RichInventor.UI;

/// <summary>
/// リボンボタンのアイコンをコードで描いて IPictureDisp に変換する。
///
/// 画像ファイルを同梱しないのは、アドインの配布物を DLL 1 つに保つため。
/// Inventor の ControlDefinitions.AddButtonDefinition はアイコンを
/// IPictureDisp(COM の型)で要求するので、Bitmap から変換する必要がある。
/// </summary>
public static class IconFactory
{
    private static readonly Color Ink = Color.FromArgb(60, 60, 60);
    private static readonly Color Accent = Color.FromArgb(0, 122, 204);
    private static readonly Color Warm = Color.FromArgb(214, 118, 20);

    /// <summary>機能①: 部品を STL に。立方体 + 下向き矢印。</summary>
    public static Bitmap ExportActive(int size) => Draw(size, (g, s) =>
    {
        var unit = s / 32f;
        DrawCube(g, unit * 4, unit * 3, unit * 16, Accent);
        DrawDownArrow(g, unit * 23, unit * 15, unit * 8, Ink);
    });

    /// <summary>機能②: 3DP 部品を一括で。積層 + 下向き矢印。</summary>
    public static Bitmap Export3Dp(int size) => Draw(size, (g, s) =>
    {
        var unit = s / 32f;
        // 積層(3Dプリントの層)を3枚
        for (var i = 0; i < 3; i++)
        {
            DrawLayer(g, unit * 3, unit * (5 + i * 6), unit * 17, unit * 5, i == 0 ? Warm : Accent);
        }
        DrawDownArrow(g, unit * 23, unit * 15, unit * 8, Ink);
    });

    /// <summary>選択部品: 立方体 + 選択枠 + 下向き矢印。</summary>
    public static Bitmap ExportSelected(int size) => Draw(size, (g, s) =>
    {
        var unit = s / 32f;
        DrawCube(g, unit * 5, unit * 5, unit * 14, Accent);

        using var pen = new Pen(Warm, MathF.Max(1f, unit * 1.5f));
        DrawSelectionCorners(g, unit * 2, unit * 2, unit * 22, unit * 23, unit * 5, pen);

        DrawDownArrow(g, unit * 26, unit * 16, unit * 9, Ink);
    });

    /// <summary>設定。歯車。</summary>
    public static Bitmap Settings(int size) => Draw(size, (g, s) =>
    {
        var unit = s / 32f;
        var center = new PointF(s / 2f, s / 2f);
        using var pen = new Pen(Ink, MathF.Max(1f, unit * 2.5f));
        g.DrawEllipse(pen, center.X - unit * 9, center.Y - unit * 9, unit * 18, unit * 18);
        g.DrawEllipse(pen, center.X - unit * 3.5f, center.Y - unit * 3.5f, unit * 7, unit * 7);
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.PI / 4f;
            g.DrawLine(pen,
                center.X + MathF.Cos(a) * unit * 9, center.Y + MathF.Sin(a) * unit * 9,
                center.X + MathF.Cos(a) * unit * 13, center.Y + MathF.Sin(a) * unit * 13);
        }
    });

    public static Bitmap DimensionPdf(int size) => Draw(size, (g, s) =>
    {
        var u = s / 32f;
        using var outline = new Pen(Accent, MathF.Max(1f, u * 1.7f));
        using var detail = new Pen(Warm, MathF.Max(1f, u * 1.2f));
        g.DrawRectangle(outline, 5 * u, 3 * u, 19 * u, 25 * u);
        g.DrawLine(detail, 9 * u, 9 * u, 20 * u, 9 * u);
        g.DrawRectangle(detail, 9 * u, 13 * u, 10 * u, 8 * u);
        g.DrawEllipse(detail, 12 * u, 15 * u, 4 * u, 4 * u);
        DrawDownArrow(g, 26 * u, 16 * u, 9 * u, Ink);
    });

    /// <summary>最大平面を接地: 下向き矢印 + 平たい板 + 地面。接地する面を強調する。</summary>
    public static Bitmap GroundLargestFace(int size) => Draw(size, (g, s) => DrawGroundScene(g, s / 32f));

    /// <summary>選択部品を接地: 接地の絵 + 選択枠。</summary>
    public static Bitmap GroundSelected(int size) => Draw(size, (g, s) =>
    {
        var u = s / 32f;
        DrawGroundScene(g, u);
        using var pen = new Pen(Warm, MathF.Max(1f, u * 1.5f));
        DrawSelectionCorners(g, 1.5f * u, 9 * u, 30.5f * u, 29.5f * u, 4 * u, pen);
    });

    private static void DrawGroundScene(Graphics g, float u)
    {
        DrawDownArrow(g, 14 * u, 2 * u, 10 * u, Ink);
        DrawSlab(g, 4 * u, 14 * u, 22 * u, 6 * u, 5 * u, Accent);

        // 接地する面
        using var face = new Pen(Warm, MathF.Max(1.5f, u * 2.5f));
        g.DrawLine(face, 4 * u, 25 * u, 21 * u, 25 * u);

        // 地面とハッチ
        using var ground = new Pen(Ink, MathF.Max(1f, u * 1.5f));
        g.DrawLine(ground, 1 * u, 27 * u, 31 * u, 27 * u);
        using var hatch = new Pen(Ink, MathF.Max(1f, u));
        for (var x = 6f; x <= 30f; x += 6f)
        {
            g.DrawLine(hatch, x * u, 27 * u, (x - 3) * u, 31 * u);
        }
    }

    /// <summary>選択枠の四隅だけを描く。</summary>
    private static void DrawSelectionCorners(Graphics g, float left, float top, float right, float bottom, float corner, Pen pen)
    {
        g.DrawLine(pen, left, top, left + corner, top);
        g.DrawLine(pen, left, top, left, top + corner);
        g.DrawLine(pen, right, top, right - corner, top);
        g.DrawLine(pen, right, top, right, top + corner);
        g.DrawLine(pen, left, bottom, left + corner, bottom);
        g.DrawLine(pen, left, bottom, left, bottom - corner);
        g.DrawLine(pen, right, bottom, right - corner, bottom);
        g.DrawLine(pen, right, bottom, right, bottom - corner);
    }

    private static Bitmap Draw(int size, Action<Graphics, float> body)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        body(g, size);
        return bmp;
    }

    private static void DrawCube(Graphics g, float x, float y, float side, Color color)
    {
        var d = side * 0.32f;
        using var pen = new Pen(color, MathF.Max(1f, side / 12f)) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(Color.FromArgb(40, color));

        var front = new RectangleF(x, y + d, side - d, side - d);
        g.FillRectangle(fill, front);
        g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);

        // 上面と右面
        g.DrawLine(pen, front.Left, front.Top, front.Left + d, front.Top - d);
        g.DrawLine(pen, front.Right, front.Top, front.Right + d, front.Top - d);
        g.DrawLine(pen, front.Left + d, front.Top - d, front.Right + d, front.Top - d);
        g.DrawLine(pen, front.Right, front.Bottom, front.Right + d, front.Bottom - d);
        g.DrawLine(pen, front.Right + d, front.Top - d, front.Right + d, front.Bottom - d);
    }

    /// <summary>奥行きのある平たい直方体。(x, y) は奥側上面の左上、w は手前面と奥行きを合わせた幅。</summary>
    private static void DrawSlab(Graphics g, float x, float y, float w, float h, float depth, Color color)
    {
        using var pen = new Pen(color, MathF.Max(1f, h / 4f)) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(Color.FromArgb(40, color));

        var front = new RectangleF(x, y + depth, w - depth, h);
        g.FillRectangle(fill, front);
        g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);

        // 上面と右面
        g.DrawLine(pen, front.Left, front.Top, front.Left + depth, front.Top - depth);
        g.DrawLine(pen, front.Right, front.Top, front.Right + depth, front.Top - depth);
        g.DrawLine(pen, front.Left + depth, front.Top - depth, front.Right + depth, front.Top - depth);
        g.DrawLine(pen, front.Right, front.Bottom, front.Right + depth, front.Bottom - depth);
        g.DrawLine(pen, front.Right + depth, front.Top - depth, front.Right + depth, front.Bottom - depth);
    }

    private static void DrawLayer(Graphics g, float x, float y, float w, float h, Color color)
    {
        using var pen = new Pen(color, MathF.Max(1f, h / 4f)) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(Color.FromArgb(50, color));
        g.FillRectangle(fill, x, y, w, h);
        g.DrawRectangle(pen, x, y, w, h);
    }

    private static void DrawDownArrow(Graphics g, float cx, float top, float length, Color color)
    {
        using var pen = new Pen(color, MathF.Max(1.2f, length / 5f)) { EndCap = LineCap.Flat };
        var bottom = top + length;
        g.DrawLine(pen, cx, top, cx, bottom - length * 0.42f);
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush,
        [
            new PointF(cx - length * 0.42f, bottom - length * 0.5f),
            new PointF(cx + length * 0.42f, bottom - length * 0.5f),
            new PointF(cx, bottom)
        ]);
    }

    // =====================================================================
    // Bitmap -> IPictureDisp
    // =====================================================================

    /// <summary>
    /// Inventor に渡せる IPictureDisp に変換する。
    /// 変換できなかった場合は null を返す(アイコン無しでもボタン自体は機能する)。
    /// </summary>
    public static object? ToPictureDisp(Bitmap bitmap)
    {
        // まず WinForms の変換を使う。アルファチャンネルが保たれるのでこちらが望ましい。
        try
        {
            return PictureDispConverter.Convert(bitmap);
        }
        catch
        {
            // .NET 8 の WinForms で AxHost が使えない環境向けのフォールバック
        }

        try
        {
            return FromHBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>AxHost の protected static メソッドを使うための派生クラス。</summary>
    private sealed class PictureDispConverter : AxHost
    {
        private PictureDispConverter() : base(string.Empty) { }

        public static object Convert(Image image) => GetIPictureDispFromPicture(image)!;
    }

    private static object? FromHBitmap(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap(Color.White);
        try
        {
            var desc = new PictDesc
            {
                cbSizeOfStruct = Marshal.SizeOf<PictDesc>(),
                picType = PictureTypeBitmap,
                hbitmap = hBitmap,
                hpal = IntPtr.Zero
            };
            var iid = IidIPictureDisp;
            OleCreatePictureIndirect(ref desc, ref iid, true, out var picture);
            return picture;
        }
        catch
        {
            DeleteObject(hBitmap);
            throw;
        }
    }

    private const int PictureTypeBitmap = 1;
    private static readonly Guid IidIPictureDisp = new("7BF80981-BF32-101A-8BBB-00AA00300CAB");

    [StructLayout(LayoutKind.Sequential)]
    private struct PictDesc
    {
        public int cbSizeOfStruct;
        public int picType;
        public IntPtr hbitmap;
        public IntPtr hpal;
    }

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void OleCreatePictureIndirect(
        ref PictDesc pictdesc,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Bool)] bool fOwn,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppvObj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
