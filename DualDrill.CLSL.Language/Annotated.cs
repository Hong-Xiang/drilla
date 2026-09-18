using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language;

public sealed record Annotated<TNode, TAnnotation>(TNode Node, TAnnotation Annotation)
{
    public Annotated<TNodeResult, TAnnotationResult> Select<TNodeResult, TAnnotationResult>(
        Func<TNode, TNodeResult> selectNode,
        Func<TAnnotation, TAnnotationResult> selectAnnotation) =>
        new(selectNode(Node), selectAnnotation(Annotation));

    public Annotated<TNodeResult, TAnnotation> SelectNode<TNodeResult>(
        Func<TNode, TNodeResult> selectNode) =>
        new(selectNode(Node), Annotation);

    public Annotated<TNode, TAnnotationResult> SelectAnnotation<TAnnotationResult>(
        Func<TAnnotation, TAnnotationResult> selectAnnotation) =>
        new(Node, selectAnnotation(Annotation));

    public void PrettyPrint(
        IndentedTextWriter writer,
        PrettyPrintOption option,
        Action<TNode, IndentedTextWriter, PrettyPrintOption> printNode,
        Action<TAnnotation, IndentedTextWriter, PrettyPrintOption> printAnnotation)
    {
        printNode(Node, writer, option);
        printAnnotation(Annotation, writer, option);
    }
}
