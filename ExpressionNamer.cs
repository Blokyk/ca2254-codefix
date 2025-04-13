using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Blokyk.CA2254CodeFix;

internal class ExpressionNamer : CSharpSyntaxVisitor<string>
{
    private static string WithUppercaseFirstLetter(string s) => char.ToUpper(s[0]) + s[1..];

    private static readonly ExpressionNamer _instance = new();
    public static string NameFor(CSharpSyntaxNode node) => WithUppercaseFirstLetter(node.Accept(_instance)!);

    private ExpressionNamer() { }

    public override string DefaultVisit(SyntaxNode node) =>
        string.Join("", node.ChildNodes().Select(n => NameFor((CSharpSyntaxNode)n)));

    public override string VisitMemberAccessExpression(MemberAccessExpressionSyntax node) =>
        /*NameFor(node.Expression) +*/ NameFor(node.Name);

    public override string VisitIdentifierName(IdentifierNameSyntax node) =>
        node.Identifier.ValueText;

    public override string VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) =>
        NameFor(node.Type);

    // fixme: make exception for nameof() expression
    public override string VisitInvocationExpression(InvocationExpressionSyntax node)
        => node.ArgumentList.Arguments.Count == 0
        ? NameFor(node.Expression)
        : NameFor(node.Expression) + "Of" + string.Join("And", node.ArgumentList.Arguments.Select(NameFor));

    public override string VisitIsPatternExpression(IsPatternExpressionSyntax node) =>
        NameFor(node.Expression) + "Is" + NameFor(node.Pattern);

    public override string VisitUnaryPattern(UnaryPatternSyntax node) =>
        "Not" + NameFor(node.Pattern);

    public override string VisitLiteralExpression(LiteralExpressionSyntax node) =>
        node.Kind() switch {
            SyntaxKind.NumericLiteralExpression => "num", // maybe it could be more helpful to put the number's text directly (esp. for patterns)
            SyntaxKind.StringLiteralExpression => "text",
            SyntaxKind.CharacterLiteralExpression => "character",
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.NullLiteralExpression => "null",
            SyntaxKind.DefaultLiteralExpression => "default",
            _ => throw new NotImplementedException("Unexpected literal kind: " + node.Kind())
        };
}