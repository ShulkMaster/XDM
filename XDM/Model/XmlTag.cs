namespace Shulkmaster.XDM.Model;

public sealed class XmlTag: XmlContent
{
    public string Name { get; set; } = string.Empty;
    
    public List<XmlAttrib> Attributes { get; init; } = new();
    public List<XmlContent> Children { get; init; } = new();
}
