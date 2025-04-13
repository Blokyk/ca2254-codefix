using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Blokyk.CA2254CodeFix;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = "CA2254 CodeFix provider"), Shared]
public partial class CSharpLoggerMessageFixer : LoggerMessageFixer
{
    protected override SyntaxNode GetCalleeSyntax(IInvocationOperation call) =>
        // for CA2254, the operation with the diagnostic should always be a normal method call
        ((InvocationExpressionSyntax)call.Syntax).Expression;

    protected override SyntaxNode AsInterpolatedTextSyntax(string text) =>
        SyntaxFactory.InterpolatedStringText(
            SyntaxFactory.Token(
                SyntaxTriviaList.Empty,
                SyntaxKind.InterpolatedStringTextToken,
                text,
                text,
                SyntaxTriviaList.Empty));

    protected override SyntaxNode AsRegularString(SyntaxNode node)
    {
        var interpolatedNode = (InterpolatedStringExpressionSyntax)node;

        var hey = Task.FromResult(0) is not default(Task<int>);

        // todo: handle *raw* interpolated strings (i.e. $@"" or @$"")
        // todo: handle multiline raw interpolated strings

        var newNode = interpolatedNode.Contents.ToString();

        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(newNode));
    }

    protected override string GetFormatValueName(IOperation expr, int index)
    {
        if (expr.Type?.SpecialType is SpecialType.System_DateTime)
            return "DateTime";

        return ExpressionNamer.NameFor((CSharpSyntaxNode)expr.Syntax);
    }
}