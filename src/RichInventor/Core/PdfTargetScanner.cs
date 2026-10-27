using Inventor;

namespace RichInventor.Core;

/// <summary>アセンブリの実オカレンスを数え、同じ部品ファイルを1件にまとめる。</summary>
public static class PdfTargetScanner
{
    public static List<PdfPartTarget> FromAssembly(AssemblyDocument assembly, string marker)
    {
        var result = new Dictionary<string, PdfPartTarget>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ComponentOccurrence occurrence in assembly.ComponentDefinition.Occurrences)
            AddOccurrence(occurrence, result, visited, marker);
        return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<PdfPartTarget> FromSelection(AssemblyDocument assembly)
    {
        var result = new Dictionary<string, PdfPartTarget>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (object selected in assembly.SelectSet)
        {
            var occurrence = selected as ComponentOccurrence ?? ContainingOccurrence(selected);
            if (occurrence is not null) AddOccurrence(occurrence, result, visited, null);
        }
        return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<PdfPartTarget> FromPart(PartDocument part) => [new(part, 1)];

    private static void AddOccurrence(ComponentOccurrence occurrence,
        Dictionary<string, PdfPartTarget> result, HashSet<string> visited, string? marker)
    {
        if (occurrence.Suppressed) return;
        var path = string.Join("/", occurrence.OccurrencePath.Cast<ComponentOccurrence>().Select(x => x.Name));
        if (!visited.Add(path)) return;

        if (occurrence.Definition.Document is PartDocument part)
        {
            var full = part.FullFileName;
            if (marker is not null && !Path.GetFileName(full).Contains(marker, StringComparison.Ordinal)) return;
            var key = string.IsNullOrEmpty(full) ? part.InternalName : full;
            if (!result.TryGetValue(key, out var target)) result[key] = new PdfPartTarget(part, 1);
            else target.Quantity++;
            return;
        }

        if (occurrence.Definition.Document is AssemblyDocument)
        {
            foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                AddOccurrence(child, result, visited, marker);
        }
    }

    private static ComponentOccurrence? ContainingOccurrence(object selected)
    {
        try
        {
            return selected.GetType().InvokeMember("ContainingOccurrence",
                System.Reflection.BindingFlags.GetProperty, null, selected, null) as ComponentOccurrence;
        }
        catch { return null; }
    }
}
