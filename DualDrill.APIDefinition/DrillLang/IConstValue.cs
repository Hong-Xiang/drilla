using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.DrillLang;

[JsonDerivedType(typeof(IntegerValue), typeDiscriminator: "integer")]
[JsonDerivedType(typeof(StringValue), typeDiscriminator: "string")]
public interface IConstValue { }
