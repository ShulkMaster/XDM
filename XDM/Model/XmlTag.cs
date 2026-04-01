namespace Shulkmaster.XDM.Model;

public sealed class XmlTag: XmlContent
{
    public string Name { get; init; }
    
    public List<XmlAttrib> Attributes { get; init; }
    public List<XmlContent> Children { get; init; }
}
