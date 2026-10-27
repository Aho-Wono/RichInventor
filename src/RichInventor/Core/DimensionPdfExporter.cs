using System.Globalization;
using Inventor;

namespace RichInventor.Core;

/// <summary>一時的なInventor図面をPDFに変換する。元モデルと図面ファイルは保存しない。</summary>
public sealed class DimensionPdfExporter(Inventor.Application app)
{
    public const string TranslatorAddInId = "{0AC6FD96-2F4D-42CE-8BE0-8AEA580399E4}";
    private readonly TransientGeometry _geometry = app.TransientGeometry;
    private TextStyle? _japaneseStyle;

    public void Export(IReadOnlyList<PdfPartTarget> targets, string path)
    {
        if (targets.Count == 0) throw new ArgumentException("PDFの対象部品がありません。");
        var measured = targets.Select(PartMeasurer.Measure).ToList();
        DrawingDocument? drawing = null;
        var tempPath = Path.Combine(Path.GetDirectoryName(path)!, $"RichInventor_{Guid.NewGuid():N}.pdf");
        try
        {
            drawing = (DrawingDocument)app.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, "", true);
            var summary = drawing.Sheets[1];
            PrepareSheet(summary);
            DrawSummary(summary, measured, 0);
            for (var offset = 18; offset < measured.Count; offset += 18)
                DrawSummary(NewSheet(drawing, "一覧"), measured, offset);

            foreach (var part in measured) DrawPart(drawing, part);
            drawing.Update2(true);
            SavePdf(drawing, tempPath);
            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                throw new IOException("PDF変換後のファイルが空です。");
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (drawing is not null) drawing.Close(true);
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private void DrawSummary(Sheet sheet, IReadOnlyList<PartMeasurement> parts, int offset)
    {
        var page = offset / 18 + 1;
        AddNote(sheet, 2, 28, $"部品寸法一覧  ({page})");
        AddNote(sheet, 2, 26.8, "単位: mm  /  使用個数: 対象範囲の非抑制オカレンス数  /  未判定・不明は自動判定不可");
        var rows = parts.Skip(offset).Take(18).Select(p => new[]
        {
            p.Target.Name,
            p.Target.Quantity.ToString(CultureInfo.InvariantCulture),
            $"{F(p.LengthMm)} x {F(p.WidthMm)} x {F(p.HeightMm)}",
            p.WallThickness,
            p.Holes.Count.ToString(CultureInfo.InvariantCulture)
        }).ToList();
        AddTable(sheet, "部品一覧", 2, 25.7,
            ["部品名", "個数", "外形 X x Y x Z", "肉厚", "穴数"], [18, 2, 10, 4, 3], rows);
    }

    private void DrawPart(DrawingDocument drawing, PartMeasurement part)
    {
        var sheet = NewSheet(drawing, part.Target.Name);
        AddNote(sheet, 2, 28, $"{part.Target.Name}  (数量 {part.Target.Quantity})");
        AddNote(sheet, 2, 26.8,
            $"外形: X {F(part.LengthMm)} / Y {F(part.WidthMm)} / Z {F(part.HeightMm)} mm    肉厚: {part.WallThickness}");
        AddNote(sheet, 2, 25.8, "穴座標の原点: 外形最小端 (X=長手方向)。投影図中の ±X/±Y/±Z はこの座標軸。");

        var directions = new (string Name, Vec3 Look, Vec3 Up)[]
        {
            ("+X", part.XAxis, part.ZAxis), ("-X", part.XAxis * -1, part.ZAxis),
            ("+Y", part.YAxis, part.ZAxis), ("-Y", part.YAxis * -1, part.ZAxis),
            ("+Z", part.ZAxis, part.YAxis), ("-Z", part.ZAxis * -1, part.YAxis)
        };
        var kept = new List<(DrawingView View, string Signature, string Label)>();
        foreach (var direction in directions)
        {
            var horizontal = direction.Up.Cross(direction.Look).Unit;
            double Extent(Vec3 axis) =>
                (part.LengthMm * Math.Abs(axis.Dot(part.XAxis)) +
                 part.WidthMm * Math.Abs(axis.Dot(part.YAxis)) +
                 part.HeightMm * Math.Abs(axis.Dot(part.ZAxis))) / 10;
            var scale = Math.Min(4, Math.Min(10 / Math.Max(Extent(horizontal), 0.1),
                4.5 / Math.Max(Extent(direction.Up), 0.1)));
            var view = AddView(sheet, part, direction.Look, direction.Up, scale);
            var signature = SignatureOf(view);
            var duplicate = signature is null ? -1 : kept.FindIndex(x => x.Signature == signature);
            if (duplicate >= 0)
            {
                var existing = kept[duplicate];
                kept[duplicate] = (existing.View, existing.Signature, existing.Label + " / " + direction.Name);
                view.Delete();
            }
            else kept.Add((view, signature ?? Guid.NewGuid().ToString("N"),
                $"{direction.Name} ({F(scale)}:1)"));
        }

        for (var i = 0; i < kept.Count; i++)
        {
            var column = i % 3;
            var row = i / 3;
            var x = 8 + 13 * column;
            var y = 21.5 - 7 * row;
            kept[i].View.Center = _geometry.CreatePoint2d(x, y);
            AddNote(sheet, x - 3, y - 3.1, kept[i].Label);
        }
        AddNote(sheet, 2, 10, $"穴: {part.Holes.Count} 件  /  不明な加工情報は推測しない");
        var allRows = HoleRows(part);
        AddTable(sheet, "穴一覧", 2, 8.5,
            ["#", "径 mm", "入口中心 X/Y/Z mm", "軸", "タップ", "深さ", "備考"],
            [1.2, 3, 9.3, 2.8, 6, 4, 10.7], allRows.Take(6).ToList());
        for (var offset = 6; offset < allRows.Count; offset += 22)
        {
            var continuation = NewSheet(drawing, part.Target.Name + " 穴一覧");
            AddNote(continuation, 2, 28, $"{part.Target.Name}  穴一覧 (続き)");
            AddTable(continuation, "穴一覧", 2, 26,
                ["#", "径 mm", "入口中心 X/Y/Z mm", "軸", "タップ", "深さ", "備考"],
                [1.2, 3, 9.3, 2.8, 6, 4, 10.7], allRows.Skip(offset).Take(22).ToList());
        }
    }

    private List<string[]> HoleRows(PartMeasurement part)
    {
        if (part.Holes.Count == 0) return [["-", "-", "-", "-", "-", "-", "穴なし"]];
        return part.Holes.Select((hole, index) =>
        {
            var p = part.LocalMm(hole.Center);
            var axis = Math.Abs(hole.Axis.Dot(part.XAxis)) > 0.9 ? "X"
                : Math.Abs(hole.Axis.Dot(part.YAxis)) > 0.9 ? "Y"
                : Math.Abs(hole.Axis.Dot(part.ZAxis)) > 0.9 ? "Z" : "斜め/不明";
            return new[] { (index + 1).ToString(), hole.DiameterMm > 0 ? F(hole.DiameterMm) : "不明",
                $"{F(p.X)} / {F(p.Y)} / {F(p.Z)}", axis,
                hole.Thread, hole.Depth, string.IsNullOrEmpty(hole.Detail) ? hole.Kind : hole.Detail };
        }).ToList();
    }

    private DrawingView AddView(Sheet sheet, PartMeasurement part, Vec3 look, Vec3 up, double scale)
    {
        var center = part.Origin + part.XAxis * (part.LengthMm / 20)
            + part.YAxis * (part.WidthMm / 20) + part.ZAxis * (part.HeightMm / 20);
        var distance = Math.Max(part.LengthMm, Math.Max(part.WidthMm, part.HeightMm)) / 10 * 4 + 10;
        var camera = app.TransientObjects.CreateCamera();
        camera.Target = _geometry.CreatePoint(center.X, center.Y, center.Z);
        var eye = center + look * distance;
        camera.Eye = _geometry.CreatePoint(eye.X, eye.Y, eye.Z);
        camera.UpVector = _geometry.CreateUnitVector(up.X, up.Y, up.Z);
        return sheet.DrawingViews.AddBaseView((Inventor._Document)(object)part.Target.Document,
            _geometry.CreatePoint2d(21, 18),
            scale, ViewOrientationTypeEnum.kArbitraryViewOrientation,
            DrawingViewStyleEnum.kHiddenLineDrawingViewStyle, "", camera, null!);
    }

    /// <summary>線分・円・円弧だけを厳密に比較。複雑な曲線があれば安全側で省略しない。</summary>
    private static string? SignatureOf(DrawingView view)
    {
        var segments = new List<(int Kind, bool Hidden, double X1, double Y1, double X2, double Y2,
            double Cx, double Cy, double Radius)>();
        foreach (DrawingCurve curve in view.DrawingCurves[null!])
            foreach (DrawingCurveSegment segment in curve.Segments)
            {
                if (!segment.Visible && !segment.HiddenLine) continue;
                var start = segment.StartPoint;
                var end = segment.EndPoint;
                var kind = (int)segment.GeometryType;
                double cx = 0, cy = 0, radius = 0;
                if (segment.Geometry is Circle2d circle)
                {
                    cx = circle.Center.X; cy = circle.Center.Y; radius = circle.Radius;
                }
                else if (segment.Geometry is Arc2d arc)
                {
                    cx = arc.Center.X; cy = arc.Center.Y; radius = arc.Radius;
                }
                else if (segment.Geometry is not LineSegment2d) return null;
                segments.Add((kind, segment.HiddenLine,
                    start?.X ?? cx, start?.Y ?? cy, end?.X ?? cx, end?.Y ?? cy, cx, cy, radius));
            }
        if (segments.Count == 0) return null;
        var signatures = new List<string>();
        for (var rotation = 0; rotation < 4; rotation++)
        {
            (double X, double Y) R(double x, double y) => rotation switch
            {
                1 => (-y, x),
                2 => (-x, -y),
                3 => (y, -x),
                _ => (x, y)
            };
            var points = segments.SelectMany(s => new[] { R(s.X1, s.Y1), R(s.X2, s.Y2) }).ToList();
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            string P(double x, double y)
            {
                var p = R(x, y);
                return $"{Math.Round((p.X - minX) * 1000):0},{Math.Round((p.Y - minY) * 1000):0}";
            }
            signatures.Add(string.Join("|", segments.Select(s =>
            {
                var ends = new[] { P(s.X1, s.Y1), P(s.X2, s.Y2) };
                Array.Sort(ends, StringComparer.Ordinal);
                var center = s.Kind == (int)Curve2dTypeEnum.kLineSegmentCurve2d
                    ? "" : P(s.Cx, s.Cy);
                return $"{s.Kind}:{s.Hidden}:{ends[0]}:{ends[1]}:{center}:{Math.Round(s.Radius * 1000):0}";
            }).OrderBy(x => x, StringComparer.Ordinal)));
        }
        return signatures.Min(StringComparer.Ordinal);
    }

    private Sheet NewSheet(DrawingDocument drawing, string name)
    {
        var sheet = drawing.Sheets.Add(DrawingSheetSizeEnum.kA3DrawingSheetSize,
            PageOrientationTypeEnum.kLandscapePageOrientation, name, null!, null!);
        PrepareSheet(sheet);
        return sheet;
    }

    private static void PrepareSheet(Sheet sheet)
    {
        sheet.Size = DrawingSheetSizeEnum.kA3DrawingSheetSize;
        sheet.Orientation = PageOrientationTypeEnum.kLandscapePageOrientation;
        try { sheet.Border?.Delete(); } catch { }
        try { sheet.TitleBlock?.Delete(); } catch { }
    }

    private void AddNote(Sheet sheet, double x, double y, string text)
    {
        var point = _geometry.CreatePoint2d(x, y);
        if (_japaneseStyle is null)
        {
            var seed = sheet.DrawingNotes.GeneralNotes.AddFitted(point, "X", null!);
            _japaneseStyle = CreateJapaneseStyle(seed.TextStyle);
            seed.Delete();
        }
        var note = sheet.DrawingNotes.GeneralNotes.AddFitted(point, text, _japaneseStyle);
        note.Width = 38;
    }

    private static TextStyle CreateJapaneseStyle(TextStyle source)
    {
        var style = (TextStyle)source.Copy("RichInventor PDF Japanese " + Guid.NewGuid().ToString("N"));
        try { style.Font = "Noto Sans JP"; }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("日本語PDFの出力には Noto Sans JP フォントが必要です。", ex);
        }
        return style;
    }

    private void AddTable(Sheet sheet, string title, double x, double y,
        string[] headers, double[] widths, List<string[]> rows)
    {
        var flat = rows.SelectMany(row => row).ToArray();
        var table = sheet.CustomTables.Add(title, _geometry.CreatePoint2d(x, y), headers.Length,
            rows.Count, ref headers, flat, widths, null!, null!);
        _japaneseStyle ??= CreateJapaneseStyle(table.DataTextStyle);
        table.DataTextStyle = _japaneseStyle;
        table.ColumnHeaderTextStyle = _japaneseStyle;
        table.TitleTextStyle = _japaneseStyle;
    }

    private void SavePdf(DrawingDocument drawing, string path)
    {
        var translator = app.ApplicationAddIns.ItemById[TranslatorAddInId] as TranslatorAddIn
            ?? throw new InvalidOperationException("InventorのPDFトランスレータが見つかりません。");
        if (!translator.Activated) translator.Activate();
        var context = app.TransientObjects.CreateTranslationContext();
        context.Type = IOMechanismEnum.kFileBrowseIOMechanism;
        var options = app.TransientObjects.CreateNameValueMap();
        if (!translator.HasSaveCopyAsOptions[drawing, context, options])
            throw new InvalidOperationException("図面をPDFに変換できません。");
        options.Value["Sheet_Range"] = PrintRangeEnum.kPrintAllSheets;
        options.Value["Vector_Resolution"] = 400;
        var medium = app.TransientObjects.CreateDataMedium();
        medium.FileName = path;
        translator.SaveCopyAs(drawing, context, options, medium);
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
