using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Blokyk.CA2254CodeFix;

internal static class Utils
{
    [return: NotNullIfNotNull(nameof(operation))]
    public static IOperation WalkUpParentheses(this IOperation operation) {
        while (operation.Parent is IParenthesizedOperation parenthesizedOperation) {
            operation = parenthesizedOperation;
        }

        return operation;
    }
}