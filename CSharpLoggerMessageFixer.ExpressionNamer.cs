using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Blokyk.CA2254CodeFix;

public partial class CSharpLoggerMessageFixer
{
    protected override string GetFormatValueName(IOperation expr, int index)
    {
        if (expr.Type?.SpecialType is SpecialType.System_DateTime)
            return "DateTime";

        // if (expr.Syntax is LiteralExpressionSyntax)
        //     return GetFormatValueNameFromType(expr.Type) + index;

        return SyntaxNamer.NameFor((CSharpSyntaxNode)expr.Syntax);
    }

    // private static string GetFormatValueNameFromType(ITypeSymbol? type)
    // {
    //     if (type is null)
    //         return $"Expr";

    //     if (type.SpecialType is not SpecialType.None)
    //         return fromSpecialType(type.SpecialType);

    //     // literals can have a non-special type in case they're a `default` or `default(T)`
    //     // in that case, we just return the type name
    //     return type.ToDisplayString();

    //     static string fromSpecialType(SpecialType type) => type switch
    //     {
    //         SpecialType.System_Boolean => "Flag",
    //         SpecialType.System_Byte => "Byte",
    //         SpecialType.System_String => "Text",
    //         SpecialType.System_Char => "Character",
    //         SpecialType.System_Int16 or
    //         SpecialType.System_Int32 or
    //         SpecialType.System_Int64 or
    //         SpecialType.System_UInt16 or
    //         SpecialType.System_UInt32 or
    //         SpecialType.System_UInt64 => "Number",
    //         _ => "Expr",
    //     };
    // }

    // private class OperationNamer : OperationVisitor<object?, string>
    // {
    //     public override string? DefaultVisit(IOperation operation, object? _) =>
    //         throw new NotImplementedException();

    //     public override string? VisitLiteral(ILiteralOperation operation, object? _) =>
    //         GetFormatValueNameFromType(operation.Type);

    //     public override string? VisitAwait(IAwaitOperation operation, object? _) =>
    //         operation.Operation.Accept(this, null);

    //     public override string? VisitInvocation(IInvocationOperation operation, object? _)
    //     {
    //         // if this isn't a static call
    //         if (operation.Instance is not null) {
    //             operation.
    //         }
    //     }
    // }

    private static string WithUppercaseFirstLetter(string s) => char.ToUpper(s[0]) + s[1..];

    private class SyntaxNamer : CSharpSyntaxVisitor<string>
    {
        private static readonly SyntaxNamer _instance = new();
        public static string NameFor(CSharpSyntaxNode node) => WithUppercaseFirstLetter(node.Accept(_instance)!);

        private SyntaxNamer() { }

        public override string DefaultVisit(SyntaxNode node) =>
            string.Join("", node.ChildNodes().Select(n => NameFor((CSharpSyntaxNode)n)));

        public override string VisitMemberAccessExpression(MemberAccessExpressionSyntax node) =>
            /*NameFor(node.Expression) +*/ NameFor(node.Name);

        public override string VisitIdentifierName(IdentifierNameSyntax node) =>
            node.Identifier.ValueText;

        public override string VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) =>
            NameFor(node.Type);

        public override string VisitInvocationExpression(InvocationExpressionSyntax node)
            => node.ArgumentList.Arguments.Count == 0
            ? NameFor(node.Expression)
            : NameFor(node.Expression) + "Of" + string.Join("And", node.ArgumentList.Arguments.Select(NameFor));

        public override string VisitIsPatternExpression(IsPatternExpressionSyntax node) =>
            NameFor(node.Expression) + "Is" + NameFor(node.Pattern);

        public override string VisitUnaryPattern(UnaryPatternSyntax node) =>
            "Not" + NameFor(node.Pattern);

        public override string VisitLiteralExpression(LiteralExpressionSyntax node) =>
            node.Kind() switch
            {
                SyntaxKind.NumericLiteralExpression => "num",
                SyntaxKind.StringLiteralExpression => "text",
                SyntaxKind.CharacterLiteralExpression => "character",
                SyntaxKind.TrueLiteralExpression => "true",
                SyntaxKind.FalseLiteralExpression => "false",
                SyntaxKind.NullLiteralExpression => "null",
                SyntaxKind.DefaultLiteralExpression => "default",
                _ => throw new NotImplementedException("Unexpected literal kind: " + node.Kind())
            };
    }
}