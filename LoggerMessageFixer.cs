using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Blokyk.CA2254CodeFix;

/// <summary>
/// CA2254: Template should be a static expression
/// </summary>
public abstract class LoggerMessageFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CA2254"];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null)
            return;

        var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
        if (node is null)
            return;

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null)
            return;

        // get the message format argument
        var formatOperation = model.GetOperation(node, context.CancellationToken);
        if (formatOperation is not IInterpolatedStringOperation interpolatedString)
            return; // this code fix only works on interpolated strings

        // get the argument for the format string
        var formatArg = (IArgumentOperation)formatOperation.WalkUpParentheses().Parent!;

        // get the .Log*() call
        var callOperation = (IInvocationOperation)formatArg.Parent!;

        var paramPosition = formatArg.Parameter?.Ordinal ?? -2; // not -1 cause that can be used
        if (paramPosition < 0)
            return; // this shouldn't happen in most cases but might if the call can't be bound for some reason

        // if there's already arguments after the format string, don't offer a fix
        // for example:
        //     logger.LogDebug($"User {user.Name} has {{DocCount}} documents in {path}", fileCount + folderCount)
        // in this case, it's a little harder to fix, since we'd first have to process the "correct" args so that
        // we can insert the interpolated segments correctly before, after, or even in-between them. for now, we
        // just won't offer a fix in that case
        if (callOperation.Arguments.Length - 2 > paramPosition + 1) // -2 to remove implicit 'this' and 'params' arguments
            return;

        // if (interpolatedString.Parts is [IInterpolationOperation interpolationExpr])
        // {
        //     context.RegisterCodeFix(CodeAction.Create(
        //         title: "fixme",
        //         createChangedDocument: token => SpreadSingleFormatStringAsync(context.Document, callOperation, interpolationExpr, paramPosition, token),
        //         equivalenceKey: "fixme"
        //     ), context.Diagnostics.First());
        // }
        // else
        // {
        //     context.RegisterCodeFix(CodeAction.Create(
        //         title: "fixme", // fixme: title
        //         createChangedDocument: token => SpreadFormatStringAsync(context.Document, callOperation, interpolatedString, paramPosition, token),
        //         equivalenceKey: "fixme" // fixme: equivalenceKey
        //     ), context.Diagnostics.First());
        // }

        context.RegisterCodeFix(CodeAction.Create(
            title: "fixme", // fixme: title
            createChangedDocument: token => SpreadFormatStringAsync(context.Document, callOperation, interpolatedString, paramPosition, token),
            equivalenceKey: "fixme" // fixme: equivalenceKey
        ), context.Diagnostics.First());
    }

    private async Task<Document> SpreadFormatStringAsync(
        Document document, IInvocationOperation callOperation, IInterpolatedStringOperation interpolatedString,
        int paramPos, CancellationToken token)
    {
        var editor = await DocumentEditor.CreateAsync(document, token);

        var calleeSyntax = GetCalleeSyntax(callOperation);

        // take all arguments before the format one
        IEnumerable<SyntaxNode> preFormatArgs =[]; // callOperation.Arguments.Take(paramPosition).Select(a => a.Syntax);

        var (uninterpolatedString, extractedParts) = Uninterpolate(editor.Generator, interpolatedString);

        SyntaxNode[] allArgsSyntax =
            [.. preFormatArgs, uninterpolatedString, .. extractedParts];

        // todo: check how to rewrite argument lists
        var newCall = editor.Generator.InvocationExpression(calleeSyntax, allArgsSyntax);

        editor.ReplaceNode(callOperation.Syntax, newCall);

        return editor.GetChangedDocument();
    }

    // private static async Task<Document> SpreadSingleFormatStringAsync(
    //     Document document, IInvocationOperation callOperation, IInterpolationOperation interpolationExpr,
    //     int paramPosition, CancellationToken token)
    // {
    //     var editor = await DocumentEditor.CreateAsync(document, token);

    //     var calleeSyntax = GetCalleeSyntax(callOperation);

    //     // take all arguments before the format one
    //     IEnumerable<SyntaxNode> preFormatArgs = callOperation.Arguments.Take(paramPosition).Select(a => a.Syntax);

    //     SyntaxNode uninterpolatedString = editor.Generator.LiteralExpression(
    //         "{" + GetFormatValueName(interpolationExpr.Expression) + "}"
    //     );

    //     SyntaxNode[] allArgsSyntax = [.. preFormatArgs, uninterpolatedString, interpolationExpr.Expression.Syntax];

    //     var newCall = editor.Generator.InvocationExpression(calleeSyntax, allArgsSyntax);

    //     editor.ReplaceNode(callOperation.Syntax, newCall);

    //     return editor.GetChangedDocument();
    // }

    private (SyntaxNode, ImmutableArray<SyntaxNode>) Uninterpolate(
        SyntaxGenerator _, IInterpolatedStringOperation interpolated)
    {
        var uninterpolatedNode = interpolated.Syntax;
        var extractedParts = ImmutableArray.CreateBuilder<SyntaxNode>(interpolated.Parts.Length);

        int exprPartIndex = 0;
        foreach (var part in interpolated.Parts)
        {
            // // if this part is the 'text' part, just append it directly
            // if (part is IInterpolatedStringTextOperation { Text.Syntax: var partText })
            // {
            //     // we can't just use the .ConstantValue directly since we want to keep
            //     // the fragment intact, including e.g. escape sequences
            //     var oldTextToken = partText.GetFirstToken();

            //     uninterpolatedNode = uninterpolatedNode.ReplaceToken(oldTextToken, )
            //     continue;
            // }

            // if this part is just text, don't do anything
            if (part is IInterpolatedStringTextOperation)
                continue;

            var interpolationPart = (IInterpolationOperation)part;
            var expr = interpolationPart.Expression;

            // add the original expression to the list of arguments to give later
            extractedParts.Add(expr.Syntax);

            // then replace it in the message with the format specifier
            var name = GetFormatValueName(expr, exprPartIndex);
            var formatText = new StringBuilder();
            formatText
                .Append('{').Append(name);

            // append the original format specifier if there was one
            if (interpolationPart.FormatString is not null)
            {
                formatText
                    .Append(':').Append(interpolationPart.FormatString);
            }

            formatText.Append('}');

            // we don't the same for .Alignement since the documentation doesn't mention any support for it

            var formatTextNode = AsInterpolatedTextSyntax(formatText.ToString());
            // uninterpolatedNode = generator.ReplaceNode(uninterpolatedNode, interpolationPart.Syntax, formatTextNode);
            uninterpolatedNode = uninterpolatedNode.ReplaceNode(interpolationPart.Syntax, formatTextNode);
        }

        uninterpolatedNode = AsRegularString(uninterpolatedNode);

        return (uninterpolatedNode, extractedParts.ToImmutable());
    }

    protected abstract SyntaxNode GetCalleeSyntax(IInvocationOperation call);
    protected abstract string GetFormatValueName(IOperation interpolationExpr, int index);
    protected abstract SyntaxNode AsInterpolatedTextSyntax(string text);
    protected abstract SyntaxNode AsRegularString(SyntaxNode node);
}