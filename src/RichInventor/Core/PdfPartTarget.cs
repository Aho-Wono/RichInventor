using Inventor;

namespace RichInventor.Core;

public sealed class PdfPartTarget(PartDocument document, int quantity)
{
    public PartDocument Document { get; } = document;
    public int Quantity { get; set; } = quantity;
    public string Name => Path.GetFileNameWithoutExtension(
        string.IsNullOrEmpty(Document.FullFileName) ? Document.DisplayName : Document.FullFileName);
}
