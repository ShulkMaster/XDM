namespace Shulkmaster.XDM.Expressions;

public abstract class Expression
{
    public abstract ConstantExpression Resolve();
}

public abstract class ConstantExpression : Expression
{
}

public sealed class NumberExpression : ConstantExpression
{
    public double Value { get; init; }

    public override ConstantExpression Resolve()
    {
        return this;
    }
}

public sealed class StringExpression : ConstantExpression
{
    public string Value { get; init; }

    public override ConstantExpression Resolve()
    {
        return this;
    }
}

public sealed class BooleanExpression : ConstantExpression
{
    public bool Value { get; init; }

    public override ConstantExpression Resolve()
    {
        return this;
    }
}