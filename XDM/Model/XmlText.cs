using Shulkmaster.XDM.Expressions;

namespace Shulkmaster.XDM.Model;

public sealed class XmlText: XmlContent
{
    public string Value { get; set; } = string.Empty;
    public List<Expression> Bindings { get; set; } = [];
}