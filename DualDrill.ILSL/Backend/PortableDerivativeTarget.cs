using System.Collections.Frozen;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Backend;

internal static class PortableDerivativeTarget
{
    private static readonly FrozenSet<FunctionDeclaration> Derivatives =
        ShaderFunction.Instance.Functions
            .Where(static function =>
                Enum.TryParse<NumericBuiltinFunctionName>(function.Name, out var name) &&
                name is >= NumericBuiltinFunctionName.dpdx and <= NumericBuiltinFunctionName.fwidthFine)
            .ToFrozenSet();

    private static readonly FrozenDictionary<FunctionDeclaration, FunctionDeclaration> Supported =
        ShaderFunction.Instance.Functions
            .Where(IsPortableShape)
            .Select(static function => KeyValuePair.Create(function, Target(function)))
            .ToFrozenDictionary();

    internal static bool IsDerivative(FunctionDeclaration function) =>
        Derivatives.Contains(function);

    internal static bool TryLower(
        FunctionDeclaration function,
        out FunctionDeclaration target) =>
        Supported.TryGetValue(function, out target!);

    internal static string UnsupportedReason(FunctionDeclaration function)
    {
        if (!IsDerivative(function))
            return "the call is not a registered derivative builtin declaration";
        if (!IsF32ScalarOrVector(function.ReturnType) ||
            function.Parameters.Length != 1 ||
            !Equals(function.Parameters[0].Type, function.ReturnType))
            return $"PortableWgsl supports only matching f32 scalar/vector derivative signatures, got {function.Type.Name}";
        return $"pinned Slang WGSL target has no verified spelling for '{function.Name}'";
    }

    private static bool IsPortableShape(FunctionDeclaration function) =>
        IsDerivative(function) &&
        function.Name is "dpdx" or "dpdy" or "fwidth" &&
        function.Parameters.Length == 1 &&
        Equals(function.Parameters[0].Type, function.ReturnType) &&
        IsF32ScalarOrVector(function.ReturnType);

    private static bool IsF32ScalarOrVector(IShaderType type) =>
        type is FloatType<DualDrill.Common.Nat.N32> ||
        type is IVecType { ElementType: FloatType<DualDrill.Common.Nat.N32> };

    private static FunctionDeclaration Target(FunctionDeclaration source) =>
        new(
            source.Name switch
            {
                "dpdx" => "ddx",
                "dpdy" => "ddy",
                "fwidth" => "fwidth",
                _ => throw new InvalidOperationException("Unsupported portable derivative mapping.")
            },
            source.Parameters,
            source.Return,
            []);
}
