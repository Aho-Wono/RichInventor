using System.Runtime.InteropServices;
using Inventor;
using RichInventor.Core;
using RichInventor.UI;

namespace RichInventor.Probe;

/// <summary>
/// 起動中の Inventor に COM でつないで、アドインのコアを直接動かす検証ツール。
///
/// アドインはリボンから使う都合上 Inventor の再起動が要るが、こちらは
/// 起動中の Inventor にそのままつながるので、ロジックの確認が速く回せる。
///
///   RichInventorProbe scan            何が書き出されるか調べるだけ(書き出さない)
///   RichInventorProbe export &lt;dir&gt;    実際に 3DP 部品を書き出す
///   RichInventorProbe icons &lt;dir&gt;     リボンアイコンを PNG に出す(見た目の確認用)
///   RichInventorProbe pdfpart &lt;ipt&gt; &lt;pdf&gt;  部品1件の寸法PDFを検証する
///   RichInventorProbe groundscan [selected]  最大平面を接地で、どの面が選ばれるか調べるだけ(拘束しない)
///   RichInventorProbe ground [selected]      実際に最大平面を接地する(Ctrl+Z 1 回で戻せる)
///                                            selected を付けると選択部品だけを対象にする
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "scan";

        try
        {
            return command switch
            {
                "scan" => Scan(),
                "export" => Export(args.Length > 1 ? args[1] : throw new ArgumentException("出力先フォルダを指定してください")),
                "icons" => Icons(args.Length > 1 ? args[1] : throw new ArgumentException("出力先フォルダを指定してください")),
                "pdfpart" => PdfPart(args),
                "pdfpartnew" => PdfPart(args, true),
                "pdfscan" => PdfScan(args),
                "pdfassembly" => PdfAssembly(args),
                "ribboncheck" => RibbonCheck(),
                "groundscan" => Ground(args, apply: false),
                "ground" => Ground(args, apply: true),
                _ => Usage()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR [{ex.GetType().Name}]: {ex}");
            if (ex.InnerException is not null)
            {
                Console.WriteLine($"  inner: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static int Usage()
    {
        Console.WriteLine("usage: RichInventorProbe <scan|export <dir>|icons <dir>|pdfpart <ipt> <pdf>|pdfscan <ipt|iam>|groundscan [selected]|ground [selected]>");
        return 2;
    }

    private static int Scan()
    {
        var app = ConnectToInventor();
        Console.WriteLine($"Inventor      : {app.SoftwareVersion.DisplayName}");

        var options = AddInOptions.Load();
        Console.WriteLine($"設定ファイル  : {AddInOptions.FilePath}");
        Console.WriteLine($"  目印        : \"{options.PrintMarker}\" (IgnoreCase={options.MarkerIgnoreCase})");
        Console.WriteLine($"  STL         : Units={options.StlUnits} Resolution={options.StlResolution} " +
                          $"Binary={options.StlBinary} Color={options.StlExportColor}");
        Console.WriteLine();

        // STL トランスレータが取れるか
        var translator = app.ApplicationAddIns.ItemById[StlExporter.TranslatorAddInId] as TranslatorAddIn;
        Console.WriteLine($"STLトランスレータ: {(translator is null ? "見つからない" : translator.DisplayName)} " +
                          $"(Activated={translator?.Activated})");
        Console.WriteLine();

        var doc = app.ActiveDocument;
        if (doc is null)
        {
            Console.WriteLine("ドキュメントが開かれていません。");
            return 0;
        }

        Console.WriteLine($"アクティブ    : {DocumentScanner.BaseNameOf(doc)}");
        Console.WriteLine($"  種類        : {(DocumentScanner.IsAssembly(doc) ? "アセンブリ" : DocumentScanner.IsPart(doc) ? "パーツ" : "その他")}");
        Console.WriteLine($"  フォルダ    : {DocumentScanner.FolderOf(doc)}");
        Console.WriteLine();

        var opened = DocumentScanner.OpenedParts(app);
        Console.WriteLine($"開いているパーツ (Views.Count>0): {opened.Count} 件 / 全 {app.Documents.Count} ドキュメント");
        foreach (var d in opened)
        {
            Console.WriteLine($"    {DocumentScanner.BaseNameOf(d)}");
        }
        Console.WriteLine();

        if (DocumentScanner.IsAssembly(doc))
        {
            var selected = DocumentScanner.FromSelection(doc);
            Console.WriteLine($"選択中の部品  : {selected.Count} 件");
            foreach (var t in selected)
            {
                Console.WriteLine($"    {t}");
            }
            Console.WriteLine();

            var hits = DocumentScanner.CollectParts(
                doc, options.PrintMarker, options.MarkerIgnoreCase, options.ExcludeOldVersions);
            Console.WriteLine($"\"{options.PrintMarker}\" 部品   : {hits.Count} 件 " +
                              $"(参照ドキュメント総数 {doc.AllReferencedDocuments.Count})");
            foreach (var t in hits)
            {
                Console.WriteLine($"    {t.BaseName}");
            }
        }

        return 0;
    }

    private static int Export(string outputFolder)
    {
        var app = ConnectToInventor();
        var doc = app.ActiveDocument ?? throw new InvalidOperationException("ドキュメントが開かれていません。");

        if (!DocumentScanner.IsAssembly(doc))
        {
            throw new InvalidOperationException("アセンブリを開いた状態で実行してください。");
        }

        var options = AddInOptions.Load();
        var targets = DocumentScanner.CollectParts(
            doc, options.PrintMarker, options.MarkerIgnoreCase, options.ExcludeOldVersions);

        Console.WriteLine($"{targets.Count} 件を {outputFolder} に書き出します...");

        var exporter = new StlExporter(app);
        var result = exporter.ExportAll(targets, outputFolder, options.ToStlSettings());

        Console.WriteLine(result.Format("書き出し完了"));
        return result.Failed > 0 ? 1 : 0;
    }

    private static int Icons(string outputFolder)
    {
        System.IO.Directory.CreateDirectory(outputFolder);

        foreach (var size in new[] { 16, 32, 64 })
        {
            Save(IconFactory.ExportActive(size), $"ExportActive_{size}.png");
            Save(IconFactory.Export3Dp(size), $"Export3DP_{size}.png");
            Save(IconFactory.ExportSelected(size), $"ExportSelected_{size}.png");
            Save(IconFactory.DimensionPdf(size), $"DimensionPDF_{size}.png");
            Save(IconFactory.Settings(size), $"Settings_{size}.png");
            Save(IconFactory.GroundLargestFace(size), $"GroundLargestFace_{size}.png");
            Save(IconFactory.GroundSelected(size), $"GroundSelected_{size}.png");
        }

        // IPictureDisp への変換が通るかも確かめる(リボン表示の要)
        using var probe = IconFactory.ExportActive(32);
        var disp = IconFactory.ToPictureDisp(probe);
        Console.WriteLine($"IPictureDisp 変換: {(disp is null ? "失敗 (アイコン無しで動作する)" : "成功 " + disp.GetType().Name)}");
        Console.WriteLine($"PNG 出力先: {outputFolder}");
        return disp is null ? 1 : 0;

        void Save(System.Drawing.Bitmap bmp, string name)
        {
            using (bmp)
            {
                bmp.Save(System.IO.Path.Combine(outputFolder, name), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }

    private static int PdfPart(string[] args, bool forceNew = false)
    {
        if (args.Length < 3) throw new ArgumentException("pdfpart <ipt> <pdf> を指定してください。");
        var created = false;
        Inventor.Application app;
        try
        {
            if (forceNew) throw new InvalidOperationException("Start a fresh Inventor instance");
            app = ConnectToInventor();
        }
        catch
        {
            var type = System.Type.GetTypeFromProgID("Inventor.Application")
                ?? throw new InvalidOperationException("Inventor COMを起動できません。");
            app = (Inventor.Application)Activator.CreateInstance(type)!;
            app.Visible = false;
            created = true;
        }
        var wasOpen = app.Documents.Cast<Document>().Any(d =>
            string.Equals(d.FullFileName, args[1], StringComparison.OrdinalIgnoreCase));
        var part = (PartDocument)app.Documents.Open(args[1], false);
        try
        {
            var target = new PdfPartTarget(part, 1);
            var measurement = PartMeasurer.Measure(target);
            Console.WriteLine($"{target.Name}: {measurement.LengthMm:0.##} x {measurement.WidthMm:0.##} x {measurement.HeightMm:0.##} mm, 穴 {measurement.Holes.Count} 件, 肉厚 {measurement.WallThickness}");
            new DimensionPdfExporter(app).Export([target], args[2]);
            Console.WriteLine($"PDF: {args[2]}");
            return 0;
        }
        finally
        {
            if (!wasOpen) part.Close(true);
            if (created) app.Quit();
        }
    }

    private static int PdfScan(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("pdfscan <ipt|iam> を指定してください。");
        var app = ConnectToInventor();
        var document = app.Documents.Open(args[1], false);
        try
        {
            var targets = document is AssemblyDocument assembly
                ? PdfTargetScanner.FromAssembly(assembly, "ASP")
                : document is PartDocument part ? PdfTargetScanner.FromPart(part)
                : throw new InvalidOperationException("パーツかアセンブリを指定してください。");
            Console.WriteLine($"対象: {targets.Count} 部品");
            foreach (var target in targets)
            {
                var m = PartMeasurer.Measure(target);
                Console.WriteLine($"{target.Name}: x{target.Quantity}, {m.LengthMm:0.##} x {m.WidthMm:0.##} x {m.HeightMm:0.##} mm, 穴 {m.Holes.Count}, 肉厚 {m.WallThickness}");
                foreach (var hole in m.Holes)
                {
                    var p = m.LocalMm(hole.Center);
                    Console.WriteLine($"  φ{hole.DiameterMm:0.##} @({p.X:0.##},{p.Y:0.##},{p.Z:0.##}) {hole.Thread} {hole.Depth} {hole.Kind} {hole.Detail}");
                }
            }
            return 0;
        }
        finally { document.Close(true); }
    }

    private static int PdfAssembly(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("pdfassembly <iam> <pdf> を指定してください。");
        var app = ConnectToInventor();
        var wasOpen = app.Documents.Cast<Document>().Any(d =>
            string.Equals(d.FullFileName, args[1], StringComparison.OrdinalIgnoreCase));
        var assembly = (AssemblyDocument)app.Documents.Open(args[1], false);
        try
        {
            var targets = PdfTargetScanner.FromAssembly(assembly, "ASP");
            Console.WriteLine($"対象: {targets.Count} 部品");
            foreach (var target in targets) Console.WriteLine($"{target.Name} x{target.Quantity}");
            new DimensionPdfExporter(app).Export(targets, args[2]);
            Console.WriteLine($"PDF: {args[2]}");
            return 0;
        }
        finally
        {
            if (!wasOpen) assembly.Close(true);
        }
    }

    private static int RibbonCheck()
    {
        var type = System.Type.GetTypeFromProgID("Inventor.Application")
            ?? throw new InvalidOperationException("Inventor COM を起動できません。");
        var app = (Inventor.Application)Activator.CreateInstance(type)!;
        try
        {
            var addIn = app.ApplicationAddIns.ItemById["{0165C313-EE82-48FF-9894-36CAF30D298F}"];
            if (!addIn.Activated) addIn.Activate();
            Console.WriteLine($"RichInventor activated: {addIn.Activated}");
            foreach (var ribbonName in new[] { "Part", "Assembly" })
            {
                var tab = app.UserInterfaceManager.Ribbons[ribbonName].RibbonTabs["RichInventor:Tab"];
                foreach (var panelName in new[] { "RichInventor:Panel:Stl", "RichInventor:Panel:DimensionPdf", "RichInventor:Panel:Placement" })
                {
                    RibbonPanel panel;
                    try { panel = tab.RibbonPanels[panelName]; }
                    catch
                    {
                        Console.WriteLine($"{ribbonName}/{panelName}: (パネル無し)");
                        continue;
                    }
                    Console.WriteLine($"{ribbonName}/{panelName}: {panel.CommandControls.Count} buttons");
                    foreach (CommandControl control in panel.CommandControls)
                        Console.WriteLine($"  {control.InternalName}");
                }
            }
            return 0;
        }
        finally { app.Quit(); }
    }

    private static int Ground(string[] args, bool apply)
    {
        var selectedOnly = args.Length > 1 && string.Equals(args[1], "selected", StringComparison.OrdinalIgnoreCase);
        var app = ConnectToInventor();
        if (app.ActiveDocument is not AssemblyDocument assembly)
            throw new InvalidOperationException("アセンブリを開いた状態で実行してください。");

        // Vec3 は record の ToString が Unit を辿って無限再帰するので、自前で整形する
        static string Format(Vec3 v) => $"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})";

        HashSet<string>? selected = null;
        if (selectedOnly)
        {
            selected = GroundAligner.SelectedTopLevelNames(assembly);
            Console.WriteLine($"選択から寄せた直下オカレンス: {selected.Count} 件 ({string.Join(", ", selected)})");
            if (selected.Count == 0) return 0;
        }

        var plan = GroundAligner.Plan(assembly, selected);
        if (plan.Base is not { } basis)
        {
            Console.WriteLine("基準にできる部品がありません。");
            foreach (var line in plan.SkipLines) Console.WriteLine($"  {line}");
            return 0;
        }
        Console.WriteLine($"基準: {basis.Name} {basis.AreaCm2 * 100:0.##} mm², 法線 {Format(basis.Normal)}" +
                          (plan.BaseNeedsGround ? ", 固定する" : ", 固定済み") +
                          (basis.OwnConstraints.Count > 0 ? $", 自前拘束 {basis.OwnConstraints.Count} 件を外す" : ""));
        Console.WriteLine($"対象 {plan.TargetCount} / 固定解除 {plan.UngroundCount} / 付け直し {plan.ReplaceCount} / スキップ {plan.SkipLines.Count}");
        foreach (var item in plan.Items)
        {
            var decision = item.IsTarget
                ? $"{item.AreaCm2 * 100:0.##} mm², 法線 {Format(item.Normal)}" +
                  (item.WasGrounded ? ", 固定を解除" : "") + (item.OwnConstraints.Count > 0 ? ", 付け直し" : "")
                : $"スキップ: {item.SkipReason}";
            Console.WriteLine($"  {item.Name}: {decision}");
        }

        if (!apply) return 0;

        var result = GroundAligner.Apply(app, plan);
        Console.WriteLine();
        Console.WriteLine(result.Format("接地完了"));
        return result.Failed > 0 ? 1 : 0;
    }

    // =====================================================================
    // 起動中の Inventor への接続
    //
    // .NET 8 では Marshal.GetActiveObject が削除されているため、
    // oleaut32 の GetActiveObject を直接呼ぶ。
    // =====================================================================
    private static Inventor.Application ConnectToInventor()
    {
        CLSIDFromProgID("Inventor.Application", out var clsid);
        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return (Inventor.Application)obj;
    }

    [DllImport("ole32.dll", PreserveSig = false)]
    private static extern void CLSIDFromProgID(
        [MarshalAs(UnmanagedType.LPWStr)] string lpszProgID,
        out Guid lpclsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}
