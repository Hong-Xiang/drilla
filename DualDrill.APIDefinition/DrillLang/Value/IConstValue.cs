using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.DrillLang.Value;

[JsonDerivedType(typeof(IntegerValue), typeDiscriminator: "integer")]
[JsonDerivedType(typeof(StringValue), typeDiscriminator: "string")]
[JsonDerivedType(typeof(BooleanValue), typeDiscriminator: "boolean")]
[JsonDerivedType(typeof(NumberValue), typeDiscriminator: "number")]
[JsonDerivedType(typeof(EmptySequenceValue), typeDiscriminator: "sequence")]
[JsonDerivedType(typeof(EmptyDictionaryValue), typeDiscriminator: "dictionary")]
public interface IConstValue { }

public readonly record struct BooleanValue(bool Value) : IConstValue;
public readonly record struct NumberValue(string Value) : IConstValue;
public readonly record struct EmptySequenceValue : IConstValue;
public readonly record struct EmptyDictionaryValue : IConstValue;
