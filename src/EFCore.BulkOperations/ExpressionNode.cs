using System.Linq.Expressions;

namespace EFCore.BulkOperations;

internal sealed class ExpressionNode
{
    public ExpressionNode(Expression expression, ExpressionNode? parent)
    {
        Expression = expression;
        Parent = parent;
    }

    public Expression Expression { get; }

    public ExpressionNode? Parent { get; }
}