using Inventor;

namespace RichInventor.Core;

public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 From(Inventor.Point p) => new(p.X, p.Y, p.Z);
    public static Vec3 From(Vector v) => new(v.X, v.Y, v.Z);
    public static Vec3 From(UnitVector v) => new(v.X, v.Y, v.Z);
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double n) => new(a.X * n, a.Y * n, a.Z * n);
    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3 Unit => Length > 1e-9 ? this * (1 / Length) : this;
}

public sealed class MeasuredHole
{
    public required Vec3 Center { get; init; }
    public required Vec3 Axis { get; init; }
    public double DiameterMm { get; set; }
    public string Kind { get; set; } = "穴";
    public string Thread { get; set; } = "不明";
    public string Depth { get; set; } = "不明";
    public string Detail { get; set; } = "";
    public bool FromFeature { get; init; }
}

public sealed class PartMeasurement
{
    public required PdfPartTarget Target { get; init; }
    public required Vec3 Origin { get; init; }
    public required Vec3 XAxis { get; init; }
    public required Vec3 YAxis { get; init; }
    public required Vec3 ZAxis { get; init; }
    public required double LengthMm { get; init; }
    public required double WidthMm { get; init; }
    public required double HeightMm { get; init; }
    public string WallThickness { get; set; } = "未判定";
    public List<MeasuredHole> Holes { get; } = [];
    public List<string> Warnings { get; } = [];

    public Vec3 LocalMm(Vec3 point)
    {
        var relative = point - Origin;
        return new(relative.Dot(XAxis) * 10, relative.Dot(YAxis) * 10, relative.Dot(ZAxis) * 10);
    }
}

/// <summary>Inventor内部のcmをmmに直し、加工情報は確定できた範囲だけ記録する。</summary>
public static class PartMeasurer
{
    public static PartMeasurement Measure(PdfPartTarget target)
    {
        var part = target.Document;
        var box = part.ComponentDefinition.OrientedMinimumRangeBox;
        var directions = new[] { Vec3.From(box.DirectionOne), Vec3.From(box.DirectionTwo), Vec3.From(box.DirectionThree) }
            .OrderByDescending(v => v.Length).ToArray();
        if (directions[2].Length < 1e-9) throw new InvalidOperationException($"外形寸法を取得できません: {target.Name}");
        var x = directions[0].Unit;
        var y = directions[1].Unit;
        var z = x.Cross(y).Unit;
        var corner = Vec3.From(box.CornerPoint);
        var corners = new List<Vec3>();
        foreach (var a in new[] { 0, 1 })
            foreach (var b in new[] { 0, 1 })
                foreach (var c in new[] { 0, 1 })
                    corners.Add(corner + Vec3.From(box.DirectionOne) * a + Vec3.From(box.DirectionTwo) * b + Vec3.From(box.DirectionThree) * c);
        var minX = corners.Min(p => p.Dot(x));
        var minY = corners.Min(p => p.Dot(y));
        var minZ = corners.Min(p => p.Dot(z));
        var measurement = new PartMeasurement
        {
            Target = target,
            Origin = x * minX + y * minY + z * minZ,
            XAxis = x,
            YAxis = y,
            ZAxis = z,
            LengthMm = (corners.Max(p => p.Dot(x)) - minX) * 10,
            WidthMm = (corners.Max(p => p.Dot(y)) - minY) * 10,
            HeightMm = (corners.Max(p => p.Dot(z)) - minZ) * 10
        };

        ReadFeatureHoles(part, measurement);
        ReadCylindricalHoles(part, measurement);
        ReadThreadFeatures(part, measurement);
        measurement.Holes.Sort((a, b) =>
        {
            var pa = measurement.LocalMm(a.Center);
            var pb = measurement.LocalMm(b.Center);
            var c = pa.X.CompareTo(pb.X);
            return c != 0 ? c : pa.Y.CompareTo(pb.Y);
        });
        measurement.WallThickness = FindWallThickness(part, measurement);
        return measurement;
    }

    private static void ReadFeatureHoles(PartDocument part, PartMeasurement result)
    {
        foreach (HoleFeature hole in part.ComponentDefinition.Features.HoleFeatures)
        {
            if (hole.Suppressed) continue;
            foreach (object item in hole.HoleCenterPoints)
            {
                var center = item switch
                {
                    Inventor.Point p => Vec3.From(p),
                    SketchPoint p => Vec3.From(p.Geometry3d),
                    WorkPoint p => Vec3.From(p.Point),
                    _ => (Vec3?)null
                };
                if (center is null) continue;
                var diameter = 0d;
                try { diameter = Convert.ToDouble(hole.HoleDiameter?.Value) * 10; } catch { }
                var axis = AxisOfHole(hole);
                var detail = hole.HoleType switch
                {
                    HoleTypeEnum.kCounterBoreHole => $"座ぐり φ{ParamMm(hole.CBoreDiameter)} 深さ{ParamMm(hole.CBoreDepth)} mm",
                    HoleTypeEnum.kCounterSinkHole => $"皿穴 φ{ParamMm(hole.CSinkDiameter)}",
                    HoleTypeEnum.kSpotFaceHole => $"スポットフェース φ{ParamMm(hole.SpotFaceDiameter)}",
                    _ => ""
                };
                var thread = "なし";
                if (hole.Tapped)
                {
                    try
                    {
                        if (hole.TapInfo is HoleTapInfo tap)
                        {
                            thread = tap.ThreadDesignation;
                            if (diameter <= 0) diameter = Convert.ToDouble(tap.TapDrillDiameter) * 10;
                        }
                        else thread = "タップ (規格不明)";
                    }
                    catch { thread = "タップ (規格不明)"; }
                }
                var depth = hole.ExtentType == PartFeatureExtentEnum.kThroughAllExtent
                    ? "貫通" : hole.ExtentType == PartFeatureExtentEnum.kDistanceExtent
                        ? $"{hole.Depth * 10:0.##} mm" : "不明";
                result.Holes.Add(new MeasuredHole
                {
                    Center = center.Value,
                    Axis = axis,
                    DiameterMm = diameter,
                    Kind = "穴フィーチャー",
                    Thread = thread,
                    Depth = depth,
                    Detail = detail,
                    FromFeature = true
                });
            }
        }
    }

    private static Vec3 AxisOfHole(HoleFeature hole)
    {
        try
        {
            foreach (Face face in hole.SideFaces)
                if (face.Geometry is Cylinder cylinder) return Vec3.From(cylinder.AxisVector).Unit;
        }
        catch { }
        return new Vec3(0, 0, 0);
    }

    private static string ParamMm(Parameter parameter)
    {
        try { return (Convert.ToDouble(parameter.Value) * 10).ToString("0.##"); }
        catch { return "?"; }
    }

    private static void ReadCylindricalHoles(PartDocument part, PartMeasurement result)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (SurfaceBody body in part.ComponentDefinition.SurfaceBodies)
        {
            if (!body.IsSolid || !body.Visible) continue;
            foreach (Face face in body.Faces)
            {
                if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface || face.Geometry is not Cylinder cylinder) continue;
                if (!IsInterior(face, cylinder)) continue;
                var axis = Vec3.From(cylinder.AxisVector).Unit;
                var circles = new List<Circle>();
                foreach (Edge edge in face.Edges)
                {
                    if (edge.Geometry is Circle circle && Math.Abs(circle.Radius - cylinder.Radius) <= 0.001)
                        circles.Add(circle);
                }
                // 同じ円筒の入口と出口は1穴。基準座標で小さい側を代表点にする。
                var entrance = circles.Select(c => Vec3.From(c.Center))
                    .OrderBy(p => result.LocalMm(p).X).ThenBy(p => result.LocalMm(p).Y)
                    .ThenBy(p => result.LocalMm(p).Z).FirstOrDefault();
                if (circles.Count == 0) continue;
                var diameter = cylinder.Radius * 20;
                var local = result.LocalMm(entrance);
                var key = $"{Math.Round(local.X, 2)}:{Math.Round(local.Y, 2)}:{Math.Round(local.Z, 2)}:{Math.Round(diameter, 2)}";
                if (!found.Add(key)) continue;
                var sameAxis = result.Holes.FirstOrDefault(h => h.Axis.Length > 0.5 &&
                    DistanceToAxis(entrance, h.Center, h.Axis) < 0.05 &&
                    (h.Depth == "貫通" || Math.Abs((entrance - h.Center).Dot(h.Axis)) < 0.5));
                if (sameAxis is not null)
                {
                    if (Math.Abs(sameAxis.DiameterMm - diameter) >= 0.2)
                    {
                        var extra = $"追加径 φ{diameter:0.##} (形状)";
                        if (!sameAxis.Detail.Contains($"φ{diameter:0.##}", StringComparison.Ordinal))
                            sameAxis.Detail = string.IsNullOrEmpty(sameAxis.Detail) ? extra : sameAxis.Detail + " / " + extra;
                    }
                    continue;
                }
                result.Holes.Add(new MeasuredHole
                {
                    Center = entrance,
                    Axis = axis,
                    DiameterMm = diameter,
                    Kind = "形状から検出",
                    Thread = "不明",
                    Depth = "不明"
                });
            }
        }
    }

    private static bool IsInterior(Face face, Cylinder cylinder)
    {
        try
        {
            var p = Vec3.From(face.PointOnFace);
            var axis = Vec3.From(cylinder.AxisVector).Unit;
            var radial = p - Vec3.From(cylinder.BasePoint);
            radial -= axis * radial.Dot(axis);
            var point = new[] { p.X, p.Y, p.Z };
            var normal = new double[3];
            face.Evaluator.GetNormalAtPoint(ref point, ref normal);
            return radial.Dot(new Vec3(normal[0], normal[1], normal[2])) < -1e-5;
        }
        catch { return false; }
    }

    private static double DistanceToAxis(Vec3 point, Vec3 basePoint, Vec3 axis)
    {
        if (axis.Length < 0.5) return (point - basePoint).Length;
        var delta = point - basePoint;
        return (delta - axis * delta.Dot(axis)).Length;
    }

    private static void ReadThreadFeatures(PartDocument part, PartMeasurement result)
    {
        foreach (ThreadFeature feature in part.ComponentDefinition.Features.ThreadFeatures)
        {
            if (feature.Suppressed) continue;
            foreach (Face face in feature.ThreadedFace)
            {
                if (face.Geometry is not Cylinder cylinder) continue;
                var axis = Vec3.From(cylinder.AxisVector).Unit;
                var center = Vec3.From(cylinder.BasePoint);
                foreach (var hole in result.Holes.Where(h => DistanceToAxis(h.Center, center, axis) < 0.05))
                {
                    try { hole.Thread = feature.ThreadInfo.ThreadDesignation; }
                    catch { hole.Thread = "タップ (規格不明)"; }
                }
            }
        }
    }

    private static string FindWallThickness(PartDocument part, PartMeasurement result)
    {
        // 平行な4面を明確に認識できる角パイプだけ採用する。
        var y = new List<double>();
        var z = new List<double>();
        foreach (SurfaceBody body in part.ComponentDefinition.SurfaceBodies)
            foreach (Face face in body.Faces)
            {
                if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface) continue;
                var point = Vec3.From(face.PointOnFace);
                var values = new[] { point.X, point.Y, point.Z };
                var normal = new double[3];
                try { face.Evaluator.GetNormalAtPoint(ref values, ref normal); }
                catch { continue; }
                var n = new Vec3(normal[0], normal[1], normal[2]);
                var local = result.LocalMm(point);
                if (Math.Abs(n.Dot(result.YAxis)) > 0.98) y.Add(local.Y);
                if (Math.Abs(n.Dot(result.ZAxis)) > 0.98) z.Add(local.Z);
            }
        var yThickness = PairThickness(y, result.WidthMm);
        var zThickness = PairThickness(z, result.HeightMm);
        if (yThickness is null || zThickness is null || Math.Abs(yThickness.Value - zThickness.Value) > 0.2)
            return "未判定";
        return $"{(yThickness.Value + zThickness.Value) / 2:0.##} mm";
    }

    private static double? PairThickness(List<double> positions, double total)
    {
        var unique = positions.Where(v => v > 0.05 && v < total - 0.05)
            .OrderBy(v => v).DistinctBy(v => Math.Round(v, 1)).ToList();
        if (unique.Count != 2) return null;
        var left = unique[0];
        var right = total - unique[1];
        return Math.Abs(left - right) <= 0.2 ? (left + right) / 2 : null;
    }
}
