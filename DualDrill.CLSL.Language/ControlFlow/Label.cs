namespace DualDrill.CLSL.Language.ControlFlow;

public interface ILabel<TSelf> : IEquatable<TSelf>
    where TSelf : ILabel<TSelf>
{
}



public interface ILabeledEntity
{
    public Label Label { get; }
}

[Obsolete]
public interface ILabelScopeInstructionGroup
{
    ILabeledEntity GetLabelTarget(Label label);
}

/// <summary>
/// A scope of label targeting to T,
/// i.e. if there is any usage of label in children elements of this scope,
/// that label might be resolved to a T instance
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILabelScope<T>
    where T : ILabeledEntity
{
    /// <summary>
    /// /// if resolution result is null, then given label is out of this scope
    /// * for unstructrued constrol flow, it means label is not defined
    /// * for structrued control flow, the label is either not defined, or defined in inner scope of this scope
    /// </summary>
    /// <param name="label"></param>
    /// <returns></returns>
    T? GetLabelTarget(Label label);
}

sealed class LabelNotFoundException(Label Label) : Exception($"Label {Label.Name} not found")
{
}

public sealed class Label : ILabel<Label>
{
    Label(string? name)
    {
        Name = name;
    }
    public string? Name { get; }

    public static Label Create(string name) => new(name);
    public static Label Create(int byteOffset) => new($"0x{byteOffset:X}");
    public static Label FromIndex(int index) => new($"#{index}");
    public static Label Create() => new(null);

    public bool Equals(Label? other)
    {
        return ReferenceEquals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return obj is Label l && Equals(l);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}


