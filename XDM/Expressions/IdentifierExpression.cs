namespace Shulkmaster.XDM.Expressions;

public sealed class IdentifierExpression: Expression
{
    public string Name { get; init; }

    public override ConstantExpression Resolve()
    {
        // todo: resolve identifier
        return new StringExpression { Value = Name };
    }
}
