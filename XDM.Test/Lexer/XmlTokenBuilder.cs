using Shulkmaster.XDM.Lexer;

namespace XDM.Test.Lexer;

public class XmlTokenBuilder
{
    private readonly List<XmlToken> _tokens = [];
    private readonly Stack<string> _openTags = new();

    public IReadOnlyList<XmlToken> Build()
    {
        var eof = new XmlToken
        {
            Kind = XmlTokenKind.Eof,
            NumberValue = 0,
            Span = default,
        };
        _tokens.Add(eof);

        return _tokens;
    }

    public XmlTokenBuilder OpenTag(string tagName)
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.OpenTag,
            NumberValue = 0,
            Span = default,
        };

        _openTags.Push(tagName);
        _tokens.Add(token);
        return Identifier(tagName);
    }

    public XmlTokenBuilder CloseTag()
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.CloseTag,
            NumberValue = 0,
            Span = default,
        };

        _tokens.Add(token);

        return this;
    }

    public XmlTokenBuilder EndTag()
    {
        var end = new XmlToken
        {
            Kind = XmlTokenKind.CloseTagStart,
            NumberValue = 0,
            Span = default,
        };
        _tokens.Add(end);

        var currentTag = _openTags.Pop();

        return Identifier(currentTag).CloseTag();
    }

    public XmlTokenBuilder Identifier(string identifierName)
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.Identifier,
            Span = identifierName.AsMemory(),
        };

        _tokens.Add(token);

        return this;
    }

    public XmlTokenBuilder Equals()
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.Equals,
        };

        _tokens.Add(token);

        return this;
    }

    public XmlTokenBuilder Text(string text)
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.Text,
            Span = text.AsMemory(),
        };

        _tokens.Add(token);

        return this;
    }

    public XmlTokenBuilder Attribute(string attributeName, string attributeValue)
    {
        return Identifier(attributeName)
            .Equals()
            .Text(attributeValue);
    }

    public XmlTokenBuilder ExpressionStart()
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.OpenBrace,
        };

        _tokens.Add(token);

        return this;
    }

    public XmlTokenBuilder ExpressionEnd()
    {
        var token = new XmlToken
        {
            Kind = XmlTokenKind.CloseBrace,
        };

        _tokens.Add(token);

        return this;
    }
}