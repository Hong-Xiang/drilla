using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

internal static class ShaderModuleMetadataValidator
{
    private const string Boundary = "Shader module metadata validation";

    public static bool IsResourceMetadata(IShaderAttribute attribute) =>
        attribute is IAddressSpaceAttribute or GroupAttribute or BindingAttribute
            or ReadAttribute or ReadWriteAttribute;

    public static IAddressSpace ValidateResourceAttributes(
        string declaration,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        var addressSpaces = attributes.OfType<IAddressSpaceAttribute>().ToArray();
        var groups = attributes.OfType<GroupAttribute>().ToArray();
        var bindings = attributes.OfType<BindingAttribute>().ToArray();
        if (addressSpaces.Length != 1 || groups.Length != 1 || bindings.Length != 1)
            throw Invalid(
                declaration,
                "a resource requires exactly one address-space, [Group], and [Binding] attribute " +
                $"(found {addressSpaces.Length}, {groups.Length}, and {bindings.Length}).");

        if (addressSpaces[0] is not UniformAttribute)
            throw Invalid(declaration, "only [Uniform] resources are supported by this compiler slice.");

        var unsupported = attributes
            .Where(attribute => attribute is not UniformAttribute and
                                not GroupAttribute and
                                not BindingAttribute &&
                                !IsUniformVisibility(attribute))
            .Select(AttributeName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unsupported.Length > 0)
            throw Invalid(
                declaration,
                $"resource attribute(s) {string.Join(", ", unsupported)} are not supported.");

        if (groups[0].Binding < 0)
            throw Invalid(declaration, $"group must be nonnegative; found {groups[0].Binding}.");
        if (bindings[0].Binding < 0)
            throw Invalid(declaration, $"binding must be nonnegative; found {bindings[0].Binding}.");

        return addressSpaces[0].AddressSpace;
    }

    public static void Validate(IShaderModuleDeclaration module)
    {
        var resources = new List<ResourceBinding>();
        foreach (var variable in module.Declarations.OfType<VariableDeclaration>())
            if (IsResourceDeclaration(variable))
                resources.Add(ValidateResource(variable));
            else
                ValidateOrdinaryModuleVariable(variable);

        var duplicate = resources
            .GroupBy(resource => (resource.Group, resource.Binding))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            var names = duplicate.Select(resource => $"'{resource.Name}'").Order(StringComparer.Ordinal);
            throw Invalid(
                "module",
                $"resource binding ({duplicate.Key.Group}, {duplicate.Key.Binding}) is duplicated by " +
                $"{string.Join(" and ", names)}.");
        }

        foreach (var function in module.Declarations.OfType<FunctionDeclaration>())
        {
            ValidateFunctionAttributes(function);
            foreach (var parameter in function.Parameters)
                ValidateInterfaceAttributes($"parameter '{function.Name}.{parameter.Name}'", parameter.Attributes);
            ValidateInterfaceAttributes($"return of function '{function.Name}'", function.Return.Attributes);
            ValidateComputeFunction(function);
        }

        foreach (var structure in module.Declarations.OfType<StructureDeclaration>())
        {
            ValidateTypeAttributes($"structure '{structure.Name}'", structure.Attributes);
            foreach (var member in structure.Members.Where(member => member.Attributes.Count > 0))
                throw Invalid(
                    $"structure member '{structure.Name}.{member.Name}'",
                    $"attribute(s) {AttributeNames(member.Attributes)} are not supported.");
        }
    }

    public static void ValidateTypeAttributes(
        string declaration,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        if (attributes.Count > 0)
            throw Invalid(
                declaration,
                $"attribute(s) {AttributeNames(attributes)} are not supported.");
    }

    public static void ValidateOrdinaryModuleField(
        string declaration,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        if (attributes.Count > 0)
            throw Invalid(
                declaration,
                $"attribute(s) {AttributeNames(attributes)} are not valid on an ordinary module field.");
    }

    private static void ValidateOrdinaryModuleVariable(VariableDeclaration variable)
    {
        if (variable.Attributes.Count > 0)
            throw Invalid(
                $"module variable '{variable.Name}'",
                $"attribute(s) {AttributeNames(variable.Attributes)} are not valid on an ordinary module variable.");
    }

    public static void ValidateInterfaceAttributes(
        string declaration,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        var unsupported = attributes
            .Where(attribute => attribute is not BuiltinAttribute and not LocationAttribute)
            .ToArray();
        if (unsupported.Length > 0)
            throw Invalid(
                declaration,
                $"attribute(s) {AttributeNames(unsupported)} are not supported; " +
                "entry parameters and returns accept only [Builtin] or [Location].");

        if (attributes.Count > 1)
            throw Invalid(
                declaration,
                $"exactly one interface attribute is allowed; found {AttributeNames(attributes)}.");

        if (attributes.OfType<LocationAttribute>().SingleOrDefault() is { Binding: < 0 } location)
            throw Invalid(declaration, $"location must be nonnegative; found {location.Binding}.");
    }

    private static bool IsResourceDeclaration(VariableDeclaration declaration) =>
        declaration.AddressSpace is UniformAddressSpace ||
        declaration.Attributes.Any(IsResourceMetadata);

    private static bool IsUniformVisibility(IShaderAttribute attribute) =>
        attribute is VertexAttribute or FragmentAttribute or ComputeAttribute;

    private static ResourceBinding ValidateResource(VariableDeclaration declaration)
    {
        var addressSpace = ValidateResourceAttributes(
            $"resource '{declaration.Name}'",
            declaration.Attributes);
        if (!Equals(declaration.AddressSpace, addressSpace))
            throw Invalid(
                $"resource '{declaration.Name}'",
                $"declared address space {declaration.AddressSpace.Kind} does not match " +
                $"attribute address space {addressSpace.Kind}.");

        return new ResourceBinding(
            declaration.Name,
            declaration.Attributes.OfType<GroupAttribute>().Single().Binding,
            declaration.Attributes.OfType<BindingAttribute>().Single().Binding);
    }

    public static void ValidateFunctionAttributes(
        string declaration,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        var unsupported = attributes
            .Where(attribute => attribute is not IShaderStageAttribute and
                                not WorkgroupSizeAttribute and
                                not IShaderMetadataAttribute and
                                not IShaderOperationMethodAttribute)
            .ToArray();
        if (unsupported.Length > 0)
            throw Invalid(
                declaration,
                $"attribute(s) {AttributeNames(unsupported)} are not supported.");
    }

    private static void ValidateFunctionAttributes(FunctionDeclaration function) =>
        ValidateFunctionAttributes($"function '{function.Name}'", function.Attributes);

    public static void ValidateReflectedComputeMetadata(MethodInfo method)
    {
        var attributes = method.GetCustomAttributes().OfType<IShaderAttribute>().ToArray();
        var stages = attributes.OfType<IShaderStageAttribute>().ToArray();
        var workgroupSizes = attributes.OfType<WorkgroupSizeAttribute>().ToArray();
        var globalInvocationIds = method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<BuiltinAttribute>())
            .Count(attribute => attribute.Slot is BuiltinBinding.global_invocation_id);
        var globalInvocationIdReturn = method.ReturnParameter
            .GetCustomAttributes<BuiltinAttribute>()
            .Any(attribute => attribute.Slot is BuiltinBinding.global_invocation_id);
        var declaration = $"method '{method.DeclaringType?.FullName}.{method.Name}'";
        var hasComputeStage = ValidateComputeStageAndWorkgroup(declaration, stages, workgroupSizes);

        if (!hasComputeStage)
        {
            if (globalInvocationIds > 0 || globalInvocationIdReturn)
                throw Invalid(
                    declaration,
                    "[Builtin(global_invocation_id)] is valid only on a [Compute] entry input.");
            return;
        }

        if (!method.IsStatic)
            throw Invalid(declaration, "a compute entry point must be static.");
    }

    private static void ValidateComputeFunction(FunctionDeclaration function)
    {
        var stages = function.Attributes.OfType<IShaderStageAttribute>().ToArray();
        var workgroupSizes = function.Attributes.OfType<WorkgroupSizeAttribute>().ToArray();
        var globalInvocationIds = function.Parameters
            .SelectMany(parameter => parameter.Attributes.OfType<BuiltinAttribute>())
            .Count(attribute => attribute.Slot is BuiltinBinding.global_invocation_id);
        var globalInvocationIdReturn = function.Return.Attributes
            .OfType<BuiltinAttribute>()
            .Any(attribute => attribute.Slot is BuiltinBinding.global_invocation_id);
        var declaration = $"function '{function.Name}'";
        var hasComputeStage = ValidateComputeStageAndWorkgroup(declaration, stages, workgroupSizes);

        if (!hasComputeStage)
        {
            if (globalInvocationIds > 0 || globalInvocationIdReturn)
                throw Invalid(
                    declaration,
                    "[Builtin(global_invocation_id)] is valid only on a [Compute] entry input.");
            return;
        }

        if (function.Return.Type is not UnitType)
            throw Invalid(
                declaration,
                $"a compute entry must return void; found {function.Return.Type.Name}.");
        if (function.Return.Attributes.Count > 0)
            throw Invalid(
                declaration,
                $"a compute entry return must not have interface attributes; found " +
                $"{AttributeNames(function.Return.Attributes)}.");

        if (globalInvocationIdReturn)
            throw Invalid(
                declaration,
                "[Builtin(global_invocation_id)] is valid only on a compute input parameter.");
        if (globalInvocationIds > 1)
            throw Invalid(
                declaration,
                $"a compute entry accepts at most one global_invocation_id input; found {globalInvocationIds}.");
        if (function.Parameters.Length > 1)
            throw Invalid(
                declaration,
                $"the supported compute signature accepts zero or one input; found {function.Parameters.Length}.");
        if (function.Parameters.Length == 0)
            return;

        var parameter = function.Parameters[0];
        var builtin = parameter.Attributes.OfType<BuiltinAttribute>().SingleOrDefault();
        if (parameter.Attributes.Count != 1 ||
            builtin is null ||
            builtin.Slot is not BuiltinBinding.global_invocation_id)
            throw Invalid(
                $"parameter '{function.Name}.{parameter.Name}'",
                "the only supported compute input is [Builtin(global_invocation_id)] vec3u32.");

        var globalInvocationIdType = VecType<N3, UIntType<N32>>.Instance;
        if (!parameter.Type.Equals(globalInvocationIdType))
            throw Invalid(
                $"parameter '{function.Name}.{parameter.Name}'",
                $"global_invocation_id must have type {globalInvocationIdType.Name}; found {parameter.Type.Name}.");
    }

    private static bool ValidateComputeStageAndWorkgroup(
        string declaration,
        IReadOnlyCollection<IShaderStageAttribute> stages,
        IReadOnlyList<WorkgroupSizeAttribute> workgroupSizes)
    {
        var computeCount = stages.Count(stage => stage is ComputeAttribute);
        if (computeCount == 0)
        {
            if (workgroupSizes.Count > 0)
                throw Invalid(declaration, "[WorkgroupSize] is valid only on a [Compute] entry point.");
            return false;
        }

        if (stages.Count != 1)
            throw Invalid(
                declaration,
                $"a compute entry requires exactly one shader stage attribute; found {stages.Count}.");
        if (workgroupSizes.Count != 1)
            throw Invalid(
                declaration,
                $"a compute entry requires exactly one [WorkgroupSize] attribute; found {workgroupSizes.Count}.");

        var workgroupSize = workgroupSizes[0];
        if (workgroupSize.X <= 0 || workgroupSize.Y <= 0 || workgroupSize.Z <= 0)
            throw Invalid(
                declaration,
                "workgroup dimensions must be positive Int32 values; " +
                $"found ({workgroupSize.X}, {workgroupSize.Y}, {workgroupSize.Z}).");
        return true;
    }

    private static string AttributeNames(IEnumerable<IShaderAttribute> attributes) =>
        string.Join(
            ", ",
            attributes.Select(AttributeName).Order(StringComparer.Ordinal));

    private static string AttributeName(IShaderAttribute attribute) =>
        $"[{attribute.GetType().Name.Replace("Attribute", string.Empty, StringComparison.Ordinal)}]";

    private static NotSupportedException Invalid(string declaration, string message) =>
        new($"{Boundary} rejected {declaration}: {message}");

    private sealed record ResourceBinding(string Name, int Group, int Binding);
}
