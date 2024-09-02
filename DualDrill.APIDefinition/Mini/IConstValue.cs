using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.Mini;

[JsonDerivedType(typeof(IntegerValue), typeDiscriminator: "integer")]
[JsonDerivedType(typeof(StringValue), typeDiscriminator: "string")]
public interface IConstValue { }
