using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnaryLogicalOp
{
    Not
}