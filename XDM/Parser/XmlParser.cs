using System.Text;
using Shulkmaster.XDM.Expressions;
using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Model;

namespace Shulkmaster.XDM.Parser;

public class XmlParser
{
    private readonly IXmlLexer _lexer;
    private readonly Stack<XmlTag> _stack = new();
    private readonly StringBuilder _stringBuilder = new();
    private ParserState _state = ParserState.Init;
    private XmlTag? _currentTag;
    private string? _currentAttrName;
    private bool _isClosingTag;
    private bool _isSelfClosing;
    private char _quoteChar;

    public XmlParser(IXmlLexer lexer)
    {
        _lexer = lexer;
    }


    public async Task<XmlDocument> ParseAsync(CancellationToken token = default)
    {
        var root = new XmlTag();
        _stack.Push(root);

        try
        {
            while (_state != ParserState.Eof)
            {
                await _lexer.FetchNextChunkAsync(token);
                if (token.IsCancellationRequested)
                {
                    break;
                }

                ConsumeTokens();
            }

            return new XmlDocument { Root = root, Finished = true };
        }
        catch (TaskCanceledException)
        {
            // no need to do anything we can return the partial parsed document
        }

        return new XmlDocument { Root = root, Finished = false };
    }

    private void ConsumeTokens()
    {
        foreach (var token in _lexer.GetTokens())
        {
            switch (_state)
            {
                case ParserState.Init:
                    HandleInit(token);
                    break;
                case ParserState.TagStatement:
                    HandleTagStatement(token);
                    break;
                case ParserState.AttribStatement:
                    HandleAttribStatement(token);
                    break;
                case ParserState.Expression:
                    HandleExpression(token);
                    break;
                case ParserState.Eof:
                    break;
            }
        }
    }
    
    private void HandleInit(XmlToken token)
    {
        if (token.Kind == XmlTokenKind.OpenTag)
        {
            _currentTag = new XmlTag();
            _isClosingTag = false;
            _isSelfClosing = false;
            _state = ParserState.TagStatement;
        }
        else if (token.Kind == XmlTokenKind.CloseTagStart)
        {
            _isClosingTag = true;
            _isSelfClosing = false;
            _state = ParserState.TagStatement;
        }
        else if (token.Kind is XmlTokenKind.Text or XmlTokenKind.Identifier)
        {
            if (_stack.Count > 0)
            {
                var text = token.Span.ToString();
                var parent = _stack.Peek();
                if (parent.Children.Count > 0 && parent.Children[^1] is XmlText lastText)
                {
                    lastText.Value += text;
                }
                else
                {
                    parent.Children.Add(new XmlText { Value = text });
                }
            }
        }
        else if (token.Kind == XmlTokenKind.Eof)
        {
            _state = ParserState.Eof;
        }
    }

    private void HandleTagStatement(XmlToken token)
    {
        if (token.Kind == XmlTokenKind.Identifier)
        {
            if (!_isClosingTag && string.IsNullOrEmpty(_currentTag?.Name))
            {
                _currentTag!.Name = token.Span.ToString();
            }
            else if (!_isClosingTag)
            {
                _currentAttrName = token.Span.ToString();
                _state = ParserState.AttribStatement;
            }
            // Ignore identifier in closing tag (just consume name)
        }
        else if (token.Kind == XmlTokenKind.Slash)
        {
            if (!_isClosingTag)
            {
                _isSelfClosing = true;
            }
        }
        else if (token.Kind == XmlTokenKind.CloseTag)
        {
            if (_isClosingTag)
            {
                if (_stack.Count > 1)
                {
                    _stack.Pop();
                }
            }
            else
            {
                if (_stack.Count > 0)
                {
                    _stack.Peek().Children.Add(_currentTag!);
                }

                if (!_isSelfClosing)
                {
                    _stack.Push(_currentTag!);
                }
            }
            _currentTag = null;
            _state = ParserState.Init;
        }
        else if (token.Kind == XmlTokenKind.Eof)
        {
            _state = ParserState.Eof;
        }
    }

    private void HandleAttribStatement(XmlToken token)
    {
        if (token.Kind == XmlTokenKind.Equals)
        {
            _state = ParserState.Expression;
            _stringBuilder.Clear();
            _quoteChar = '\0';
        }
        else if (token.Kind == XmlTokenKind.Eof)
        {
            _state = ParserState.Eof;
        }
    }

    private void HandleExpression(XmlToken token)
    {
        if (_quoteChar == '\0')
        {
            if (token.Kind == XmlTokenKind.Text && token.Span.Length > 0)
            {
                char c = token.Span.Span[0];
                if (c == '"' || c == '\'')
                {
                    _quoteChar = c;
                }
            }
        }
        else
        {
            if (token.Kind == XmlTokenKind.Text && token.Span.Length == 1 && token.Span.Span[0] == _quoteChar)
            {
                _currentTag!.Attributes.Add(new XmlAttrib
                {
                    Name = _currentAttrName!,
                    Value = new StringExpression { Value = _stringBuilder.ToString() }
                });
                _currentAttrName = null;
                _state = ParserState.TagStatement;
            }
            else if (token.Kind == XmlTokenKind.Eof)
            {
                _state = ParserState.Eof;
            }
            else
            {
                _stringBuilder.Append(token.Span.ToString());
            }
        }
    }
}