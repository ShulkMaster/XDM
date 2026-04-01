namespace Shulkmaster.XDM.Parser;

public enum ParserState
{
    Init,
    TagStatement,
    AttribStatement,
    Expression,
    Eof
}