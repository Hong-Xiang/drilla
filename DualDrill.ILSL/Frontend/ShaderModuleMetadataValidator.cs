using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Frontend;

internal static class ShaderModuleMetadataValidator
{
    private const string Boundary = "Shader module metadata validation";

    public static bool IsResourceMetadata(IShaderAttribute attribute) =>
        attribute is IAddressSpaceAttribute or GroupAttribute or BindingAttribute
            or ReadAttribute or ReadWriteAttribute;

    public static IAddressSpace ValidateResourceAttributes(
        string declaration,
        IReadOnlyCollection<IShaderAttribute> attributes)
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
        IReadOnlyCollection<IShaderAttribute> attributes)
    {
        if (attributes.Count > 0)
            throw Invalid(
                declaration,
                $"attribute(s) {AttributeNames(attributes)} are not supported.");
    }

    public static void ValidateOrdinaryModuleField(
        string declaration,
        IReadOnlyCollection<IShaderAttribute> attributes)
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
        IReadOnlyCollection<IShaderAttribute> attributes)
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
        IReadOnlyCollection<IShaderAttribute> attributes)
    {
        var unsupported = attributes
            .Where(attribute => attribute is not IShaderStageAttribute and
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
