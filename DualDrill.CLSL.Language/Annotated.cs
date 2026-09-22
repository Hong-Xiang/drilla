using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language;

public sealed class Annotated<TNode, TAnnotation> :
    IPrintable,
    IEquatable<Annotated<TNode, TAnnotation>>
{
    private readonly Action<TNode, TAnnotation, IndentedTextWriter, PrettyPrintOption> prettyPrint;

    public Annotated(
        TNode node,
        TAnnotation annotation,
        Action<TNode, TAnnotation, IndentedTextWriter, PrettyPrintOption> prettyPrint)
    {
        ArgumentNullException.ThrowIfNull(prettyPrint);
        Node = node;
        Annotation = annotation;
        this.prettyPrint = prettyPrint;
    }

    public TNode Node { get; }
    public TAnnotation Annotation { get; }

    public Annotated<TNodeResult, TAnnotationResult> Select<TNodeResult, TAnnotationResult>(
        Func<TNode, TNodeResult> selectNode,
        Func<TAnnotation, TAnnotationResult> selectAnnotation,
        Action<TNodeResult, TAnnotationResult, IndentedTextWriter, PrettyPrintOption> resultPrettyPrint)
    {
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(selectAnnotation);
        ArgumentNullException.ThrowIfNull(resultPrettyPrint);
        return new(
            selectNode(Node),
            selectAnnotation(Annotation),
            resultPrettyPrint);
    }

    public Annotated<TNodeResult, TAnnotation> SelectNode<TNodeResult>(
        Func<TNode, TNodeResult> selectNode,
        Action<TNodeResult, TAnnotation, IndentedTextWriter, PrettyPrintOption> resultPrettyPrint)
    {
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(resultPrettyPrint);
        return new(selectNode(Node), Annotation, resultPrettyPrint);
    }

    public Annotated<TNode, TAnnotationResult> SelectAnnotation<TAnnotationResult>(
        Func<TAnnotation, TAnnotationResult> selectAnnotation,
        Action<TNode, TAnnotationResult, IndentedTextWriter, PrettyPrintOption> resultPrettyPrint)
    {
        ArgumentNullException.ThrowIfNull(selectAnnotation);
        ArgumentNullException.ThrowIfNull(resultPrettyPrint);
        return new(Node, selectAnnotation(Annotation), resultPrettyPrint);
    }

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        prettyPrint(Node, Annotation, writer, option);

    public bool Equals(Annotated<TNode, TAnnotation>? other) =>
        other is not null &&
        EqualityComparer<TNode>.Default.Equals(Node, other.Node) &&
        EqualityComparer<TAnnotation>.Default.Equals(Annotation, other.Annotation);

    public override bool Equals(object? obj) =>
        obj is Annotated<TNode, TAnnotation> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Node, Annotation);
}
