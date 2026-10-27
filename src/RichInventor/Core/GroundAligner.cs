using System.Text;
using Inventor;

namespace RichInventor.Core;

/// <summary>接地させる 1 オカレンスぶんの解析結果。基準部品もこの形で持つ。</summary>
public sealed class GroundItem
{
    public required ComponentOccurrence Occurrence { get; init; }
    public required string Name { get; init; }

    /// <summary>そろえる面(アセンブリ文脈の FaceProxy)。スキップなら null。</summary>
    public Face? Face { get; init; }

    public double AreaCm2 { get; init; }
    public Vec3 Normal { get; init; }
    public bool WasGrounded { get; init; }

    /// <summary>前回このコマンドがこの部品を動かす側として付けた拘束。付け直すときに消す。</summary>
    public IReadOnlyList<AssemblyConstraint> OwnConstraints { get; init; } = [];

    /// <summary>スキップする理由。null なら拘束の対象。</summary>
    public string? SkipReason { get; init; }

    public bool IsTarget => SkipReason is null;
}

/// <summary>実行前に確認をとるための解析結果。</summary>
public sealed class GroundPlan
{
    public required AssemblyDocument Assembly { get; init; }

    /// <summary>動かさずに基準にする部品。その最大平面に他の部品をそろえる。候補が無ければ null。</summary>
    public GroundItem? Base { get; set; }

    /// <summary>基準部品以外の、範囲内のオカレンス(拘束対象とスキップ)。</summary>
    public List<GroundItem> Items { get; } = [];

    public bool BaseNeedsGround => Base is { WasGrounded: false };
    public IEnumerable<GroundItem> Targets => Items.Where(i => i.IsTarget);
    public int TargetCount => Targets.Count();
    public int UngroundCount => Targets.Count(i => i.WasGrounded);
    public int ReplaceCount => Targets.Count(i => i.OwnConstraints.Count > 0);
    public List<string> SkipLines => Items.Where(i => !i.IsTarget).Select(i => $"{i.Name}: {i.SkipReason}").ToList();
}

/// <summary>接地の結果。呼び出し側はこれを整形してダイアログに出すだけでよい。</summary>
public sealed class GroundResult(string baseName)
{
    public int Added { get; set; }
    public int Ungrounded { get; set; }
    public int Replaced { get; set; }

    /// <summary>基準部品を今回新たに固定したか。</summary>
    public bool BaseGrounded { get; set; }

    public List<string> Lines { get; } = [];
    public List<string> Skipped { get; } = [];
    public List<string> Errors { get; } = [];
    public int Failed => Errors.Count;

    public string Format(string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine();
        sb.AppendLine($"  拘束 {Added} 件 / スキップ {Skipped.Count} 件 / 失敗 {Failed} 件");
        sb.AppendLine($"  基準: {baseName} の最大平面{(BaseGrounded ? "(固定しました)" : "")}");
        if (Ungrounded > 0 || Replaced > 0)
        {
            sb.AppendLine($"  固定を解除 {Ungrounded} 件 / 接地拘束を付け直し {Replaced} 件");
        }

        GroundAligner.AppendList(sb, null, Lines);
        GroundAligner.AppendList(sb, "スキップした項目:", Skipped);
        GroundAligner.AppendList(sb, "エラーが発生した項目:", Errors);
        return sb.ToString();
    }
}

/// <summary>
/// アセンブリ直下のオカレンス(全部、または選択したもの)から基準部品を 1 つ選び、
/// ほかの部品の面積が最大の平面を、基準部品の最大平面にフラッシュ拘束する。
/// サブアセンブリは 1 つの剛体として扱い、内部の葉パーツの面から最大のものを選ぶ。
/// </summary>
public static class GroundAligner
{
    /// <summary>このコマンドが付けた拘束の目印。再実行時に付け直す対象を見分ける。</summary>
    public const string AttributeSetName = "RichInventorGround";

    public const string TransactionName = "最大平面を接地";

    /// <summary>この比率以上の面積の面は「同じ大きさ」とみなし、向きで選び直す(立方体など)。</summary>
    private const double SameAreaRatio = 0.9999;

    /// <summary>基準部品の面が同じ大きさで複数あるとき(板の表裏など)は、底面にあたる -Z 向きを選ぶ。</summary>
    private static readonly Vec3 Down = new(0, 0, -1);

    private sealed class Entry
    {
        public required ComponentOccurrence Occurrence { get; init; }
        public required string Name { get; init; }
        public Relation? Relation { get; init; }
        public List<PlanarFace> Faces { get; } = [];
        public string? SkipReason { get; set; }
        public double MaxArea => Faces.Count == 0 ? 0 : Faces.Max(f => f.Area);
        public IReadOnlyList<AssemblyConstraint> Own => Relation?.Own ?? [];
    }

    /// <param name="only">指定すると、この名前の直下オカレンスだけを対象にする(選択部品モード)。</param>
    public static GroundPlan Plan(AssemblyDocument assembly, IReadOnlySet<string>? only = null)
    {
        var definition = assembly.ComponentDefinition;
        var plan = new GroundPlan { Assembly = assembly };
        // 対象を絞っても、ほかの拘束の有無はアセンブリ全体を見ないと判定できない
        var relations = CollectRelations(definition);

        var entries = new List<Entry>();
        foreach (ComponentOccurrence occurrence in definition.Occurrences)
        {
            var name = occurrence.Name;
            if (only is not null && !only.Contains(name)) continue;
            relations.TryGetValue(name, out var relation);

            var entry = new Entry { Occurrence = occurrence, Name = name, Relation = relation };
            if (occurrence.Suppressed) entry.SkipReason = "抑制されている";
            else if (occurrence.IsPatternElement) entry.SkipReason = "パターン要素(パターンの元を拘束してください)";
            else
            {
                CollectPlanarFaces(occurrence, entry.Faces);
                if (entry.Faces.Count == 0) entry.SkipReason = "平面が見つからない";
            }
            entries.Add(entry);
        }

        // 基準部品: 固定されている部品を優先し、その中(無ければ全体)で最大平面がいちばん大きいもの。
        // 基準は動かさないので、ほかの拘束が付いていても候補にしてよい
        var candidates = entries.Where(e => e.SkipReason is null).ToList();
        var grounded = candidates.Where(e => e.Occurrence.Grounded).ToList();
        var basis = (grounded.Count > 0 ? grounded : candidates).MaxBy(e => e.MaxArea);
        if (basis is null)
        {
            plan.Items.AddRange(entries.Select(e => ToItem(e, null)));
            return plan;
        }

        var baseFace = PickFace(basis.Faces, Down);
        plan.Base = ToItem(basis, baseFace);

        foreach (var entry in entries)
        {
            if (ReferenceEquals(entry, basis)) continue;
            if (entry.SkipReason is null && entry.Relation is { Others: > 0 })
            {
                entry.SkipReason = $"ほかの拘束・ジョイントがある({entry.Relation.Others} 件)";
            }
            // 同じ大きさの面が複数ある場合は、基準面の向きに近い方を選んで回転を最小にする
            plan.Items.Add(ToItem(entry, entry.SkipReason is null ? PickFace(entry.Faces, baseFace.Normal) : null));
        }

        return plan;
    }

    private static GroundItem ToItem(Entry entry, PlanarFace? face) => new()
    {
        Occurrence = entry.Occurrence,
        Name = entry.Name,
        Face = face?.Face,
        AreaCm2 = face?.Area ?? 0,
        Normal = face?.Normal ?? default,
        WasGrounded = face is not null && entry.Occurrence.Grounded,
        OwnConstraints = entry.Own,
        SkipReason = face is null ? entry.SkipReason ?? "平面が見つからない" : null
    };

    /// <summary>
    /// 選択セットから、対象にする直下オカレンスの名前を集める。
    /// 面やエッジ、サブアセンブリ内の部品を選んでいても、それが属する直下のオカレンスに寄せる。
    /// </summary>
    public static HashSet<string> SelectedTopLevelNames(AssemblyDocument assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (object selected in assembly.SelectSet)
        {
            // 作業平面など、オカレンスに属さない選択は無視する
            var occurrence = selected as ComponentOccurrence ?? DocumentScanner.ContainingOccurrenceOf(selected);
            if (occurrence is null) continue;
            try
            {
                var path = occurrence.OccurrencePath;
                if (path.Count > 0) names.Add(path[1].Name);
            }
            catch
            {
                // 経路が取れないものは対象にしない
            }
        }
        return names;
    }

    /// <summary>解析結果どおりに拘束する。全体を 1 つのトランザクションにして Ctrl+Z 1 回で戻せるようにする。</summary>
    public static GroundResult Apply(Inventor.Application app, GroundPlan plan)
    {
        var basis = plan.Base ?? throw new InvalidOperationException("基準にできる部品がありません。");
        var result = new GroundResult(basis.Name);
        result.Skipped.AddRange(plan.SkipLines);

        // interop の COM インターフェースは継承関係を持たないので、object 経由で QueryInterface させる
        var document = (Inventor.Document)(object)plan.Assembly;
        var constraints = plan.Assembly.ComponentDefinition.Constraints;
        var transaction = app.TransactionManager.StartTransaction((_Document)(object)plan.Assembly, TransactionName);
        try
        {
            // 基準部品は動かさない。前回この部品を動かす側で付けた接地拘束(旧版の XY 平面への拘束を含む)を外して固定する
            foreach (var old in basis.OwnConstraints) old.Delete();
            if (!basis.WasGrounded)
            {
                basis.Occurrence.Grounded = true;
                result.BaseGrounded = true;
            }

            foreach (var item in plan.Targets)
            {
                ApplyOne(item, basis.Face!, constraints, document, result);
            }
            transaction.End();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        return result;
    }

    private static void ApplyOne(GroundItem item, Face baseFace, AssemblyConstraints constraints,
        Inventor.Document document, GroundResult result)
    {
        try
        {
            // 同じ面に二重に拘束すると冗長になるので、前回の接地拘束は先に消す
            foreach (var old in item.OwnConstraints) old.Delete();
            if (item.WasGrounded) item.Occurrence.Grounded = false;

            // EntityOne を動かす側にする。OccurrenceOne で自前拘束の持ち主を見分けるため
            var constraint = (AssemblyConstraint)(object)constraints.AddFlushConstraint(item.Face, baseFace, 0.0);
            var health = HealthOf(constraint, document);
            if (!IsHealthy(health))
            {
                try { constraint.Delete(); } catch { }
                throw new InvalidOperationException($"拘束を解決できませんでした ({health})。");
            }

            Mark(constraint, item.Name);
            result.Added++;
            if (item.WasGrounded) result.Ungrounded++;
            if (item.OwnConstraints.Count > 0) result.Replaced++;
            result.Lines.Add($"{item.Name}  ({item.AreaCm2 * 100:0.#} mm²)");
        }
        catch (Exception ex)
        {
            if (item.WasGrounded)
            {
                try { item.Occurrence.Grounded = true; } catch { }
            }
            result.Errors.Add($"{item.Name}: {ex.Message}");
        }
    }

    private static HealthStatusEnum HealthOf(AssemblyConstraint constraint, Inventor.Document document)
    {
        var health = constraint.HealthStatus;
        if (health is HealthStatusEnum.kNewlyAddedHealth or HealthStatusEnum.kOutOfDateHealth)
        {
            // まだ解かれていないだけなので、更新してから見直す
            document.Update();
            health = constraint.HealthStatus;
        }
        return health;
    }

    private static bool IsHealthy(HealthStatusEnum health) =>
        health is HealthStatusEnum.kUpToDateHealth
            or HealthStatusEnum.kNewlyAddedHealth
            or HealthStatusEnum.kOutOfDateHealth
            or HealthStatusEnum.kUnknownHealth;

    /// <summary>目印の属性と名前を付ける。名前が付けられなくても拘束自体は有効なので握りつぶす。</summary>
    private static void Mark(AssemblyConstraint constraint, string occurrenceName)
    {
        try { constraint.AttributeSets.Add(AttributeSetName, false); } catch { }
        try { constraint.Name = $"接地:{occurrenceName}"; } catch { }
    }

    private static bool IsOwn(AttributeSets attributes)
    {
        try { return attributes.NameIsUsed[AttributeSetName]; }
        catch { return false; }
    }

    private sealed class Relation
    {
        public List<AssemblyConstraint> Own { get; } = [];
        public int Others { get; set; }
    }

    /// <summary>
    /// アセンブリの拘束とジョイントを、かかっている直下オカレンスの名前ごとに数える。
    /// サブアセンブリ内部の面に付いた拘束も、OccurrencePath の先頭で直下のオカレンスに寄せる。
    /// </summary>
    private static Dictionary<string, Relation> CollectRelations(AssemblyComponentDefinition definition)
    {
        var map = new Dictionary<string, Relation>(StringComparer.Ordinal);

        foreach (AssemblyConstraint constraint in definition.Constraints)
        {
            if (IsOwn(constraint.AttributeSets))
            {
                // 自前の拘束は動かす側(OccurrenceOne)にだけ数える。基準側(OccurrenceTwo)は
                // 他の拘束にも数えないので、前回の基準部品も今回の対象にでき、基準の付け直しで
                // 範囲外の部品がその基準に付けている拘束を消すこともない
                foreach (var name in TopLevelNames(() => constraint.OccurrenceOne))
                {
                    RelationOf(map, name).Own.Add(constraint);
                }
                continue;
            }

            foreach (var name in TopLevelNames(() => constraint.OccurrenceOne, () => constraint.OccurrenceTwo))
            {
                RelationOf(map, name).Others++;
            }
        }

        foreach (AssemblyJoint joint in definition.Joints)
        {
            foreach (var name in TopLevelNames(() => joint.OccurrenceOne, () => joint.OccurrenceTwo))
            {
                RelationOf(map, name).Others++;
            }
        }

        return map;
    }

    private static Relation RelationOf(Dictionary<string, Relation> map, string name)
    {
        if (!map.TryGetValue(name, out var relation)) map[name] = relation = new Relation();
        return relation;
    }

    private static IEnumerable<string> TopLevelNames(params Func<ComponentOccurrence?>[] getters)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var getter in getters)
        {
            try
            {
                // アセンブリの作業平面など、オカレンスに属さない相手は null になる
                var occurrence = getter();
                if (occurrence is null) continue;
                var path = occurrence.OccurrencePath;
                if (path.Count > 0) names.Add(path[1].Name);
            }
            catch
            {
                // 種類によっては取得できない。拘束の判定から外すだけでよい
            }
        }
        return names;
    }

    private readonly record struct PlanarFace(Face Face, double Area, Vec3 Normal);

    /// <summary>面積が最大の面を選ぶ。同じ大きさの面が複数あれば、法線が preferredNormal に近いものにする。</summary>
    private static PlanarFace PickFace(List<PlanarFace> faces, Vec3 preferredNormal)
    {
        var max = faces.Max(f => f.Area);
        return faces.Where(f => f.Area >= max * SameAreaRatio).MaxBy(f => f.Normal.Dot(preferredNormal));
    }

    private static void CollectPlanarFaces(ComponentOccurrence occurrence, List<PlanarFace> faces)
    {
        if (occurrence.Suppressed) return;

        if (occurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
        {
            foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                CollectPlanarFaces(child, faces);
            return;
        }

        if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kPartDocumentObject) return;

        foreach (SurfaceBody body in occurrence.SurfaceBodies)
        {
            if (!body.IsSolid || !body.Visible) continue;
            foreach (Face face in body.Faces)
            {
                if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface) continue;
                try
                {
                    var point = Vec3.From(face.PointOnFace);
                    var values = new[] { point.X, point.Y, point.Z };
                    var normal = new double[3];
                    face.Evaluator.GetNormalAtPoint(ref values, ref normal);
                    faces.Add(new PlanarFace(face, face.Evaluator.Area, new Vec3(normal[0], normal[1], normal[2]).Unit));
                }
                catch
                {
                    // 評価できない面は候補から外す
                }
            }
        }
    }

    /// <summary>一覧をダイアログ本文に足す。長すぎるとダイアログが画面外に出るので上限を設ける。</summary>
    public static void AppendList(StringBuilder sb, string? title, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;
        sb.AppendLine();
        if (title is not null) sb.AppendLine(title);
        const int max = 30;
        foreach (var line in lines.Take(max))
        {
            sb.AppendLine("    " + line);
        }
        if (lines.Count > max)
        {
            sb.AppendLine($"    ... 他 {lines.Count - max} 件");
        }
    }
}
