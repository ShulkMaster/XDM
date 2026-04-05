using Document.ViewNodes;
using Shulkmaster.XDM.Model;

namespace Document.Template;

public class TemplateBuilder
{
    public ViewNode FromXmlDoc(XmlDocument doc)
    {
        var root = new ViewNode();

        // Stack holds (XmlTag source, ViewNode target) pairs
        var stack = new Stack<(XmlTag tag, ViewNode node)>();
        MapTag(doc.Root, root);
        stack.Push((doc.Root, root));

        while (stack.Count > 0)
        {
            var (tag, parentNode) = stack.Pop();

            for (var i = 0; i < tag.Children.Count; i++)
            {
                var child = tag.Children[i];

                switch (child)
                {
                    case XmlText text:
                    {
                        var textView = new TextViewNode
                        {
                            Text = [new TextNode { Text = text.Value }]
                        };
                        parentNode.Children.Add(textView);
                        break;
                    }
                    case XmlTag childTag:
                    {
                        var childNode = new ViewNode();
                        MapTag(childTag, childNode);
                        parentNode.Children.Add(childNode);
                        stack.Push((childTag, childNode));
                        break;
                    }
                }
            }
        }

        return root;
    }

    private static void MapTag(XmlTag tag, ViewNode node)
    {
        // Map known attributes to styles
        // For now, divs and unknown tags all become regular ViewNodes
        // Future: map specific attributes to ViewStyles properties
    }
}
