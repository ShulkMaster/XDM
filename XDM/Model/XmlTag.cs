namespace Shulkmaster.XDM.Model;

public class XmlTag
{
    public string Name { get; init; }
    
    public List<XmlAttrib> Attributes { get; init; }
    public List<XmlTag> Children { get; init; }
}
