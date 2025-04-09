﻿namespace DualDrill.CLSL.Language.ControlFlow;

public interface ILabel<TSelf> : IEquatable<TSelf>
    where TSelf : ILabel<TSelf>
{
}

public interface ILabeledEntity
{
    public Label Label { get; }
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

    public override string ToString()
    {
        return $"Label({Name})";
    }
}


