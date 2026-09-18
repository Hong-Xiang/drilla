using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Frontend;

public sealed class RuntimeReflectionParser
{
    private static readonly BindingFlags VariableBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private readonly HashSet<MethodBase> completedMethods = [];
    private readonly HashSet<Type> collectedTypes = [];
    private readonly Dictionary<MethodBase, CollectedMethod> collectedMethods = [];
    private readonly HashSet<MethodBase> inProgressMethods = [];
    private MethodBase? failedMethod;

    public RuntimeReflectionParser()
        : this(CompilationContext.Create())
    {
    }

    public RuntimeReflectionParser(CompilationContext context)
    {
        Context = context;
    }

    public CompilationContext Context { get; }

    public ShaderModuleDeclaration<RawCilFunctionBody> ParseShaderModule(ISharpShader module)
    {
        EnsureUsable();
        var moduleType = module.GetType();
        foreach (var variable in ParseAllModuleVariableDeclarations(moduleType))
            _ = variable;

        var entryMethods = moduleType
                           .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                       BindingFlags.Instance)
                           .Where(method => method.GetCustomAttributes().Any(attribute =>
                               attribute is IShaderStageAttribute))
                           .OrderBy(method => method.Name)
                           .ToImmutableArray();
        return ParseMethods(entryMethods);
    }

    public ShaderModuleDeclaration<RawCilFunctionBody> ParseMethod(MethodBase method) =>
        ParseMethods([method]);

    public IShaderType ParseType(Type type)
    {
        if (Context[type] is { } found)
            return found;

        if (!type.IsValueType)
        {
            var opaque = new OpaqueType(type);
            Context.AddType(type, opaque);
            return opaque;
        }

        var structure = ParseStructDeclaration(type);
        Context.AddStructure(type, structure);
        return structure;
    }

    public ParameterDeclaration ParseParameter(ParameterInfo parameter)
    {
        var symbol = Symbol.Parameter(parameter);
        if (Context[symbol] is { } found)
            return found;

        var declaration = new ParameterDeclaration(
            parameter.Name ?? throw new NotSupportedException("Cannot parse a parameter without a name."),
            ParseType(parameter.ParameterType),
            ParseAttribute(parameter));
        Context.AddParameter(symbol, declaration);
        return declaration;
    }

    public VariableDeclaration ParseStaticField(FieldInfo field)
    {
        var symbol = Symbol.Variable(field);
        if (Context[symbol] is { } found)
            return found;

        var addressSpace = field.GetCustomAttributes().OfType<IAddressSpaceAttribute>().SingleOrDefault()?.AddressSpace
                           ?? throw new NotSupportedException(
                               $"Static field {field} is not a declared shader module variable.");
        var declaration = new VariableDeclaration(
            addressSpace,
            field.Name,
            ParseType(field.FieldType),
            [.. field.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, declaration);
        return declaration;
    }

    public MemberDeclaration ParseField(FieldInfo field)
    {
        if (Context[field] is { } found)
            return found;

        var declaration = new MemberDeclaration(
            field.Name,
            ParseType(field.FieldType),
            [.. field.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddStructureMember(field, declaration);
        return declaration;
    }

    private ShaderModuleDeclaration<RawCilFunctionBody> ParseMethods(IEnumerable<MethodBase> methods)
    {
        EnsureUsable();
        var roots = methods.ToImmutableArray();
        try
        {
            foreach (var method in roots)
                CollectMethod(method);
            return BuildModule();
        }
        catch
        {
            failedMethod ??= inProgressMethods.LastOrDefault() ?? roots.FirstOrDefault();
            throw;
        }
    }

    private void CollectMethod(MethodBase method)
    {
        var declaration = ParseMethodDeclaration(method);
        CollectMethodSignature(method);
        if (IsMethodBoundary(method) || completedMethods.Contains(method) || inProgressMethods.Contains(method))
            return;

        if (method.GetMethodBody() is null)
        {
            failedMethod ??= method;
            throw new NotSupportedException(
                $"Referenced method {method} has no decodable CIL body and is not a registered builtin or intrinsic.");
        }

        inProgressMethods.Add(method);
        try
        {
            var rawCode = CilMethodDecoder.Decode(method);
            var locals = rawCode.Environment.LocalVariables.Select(ParseLocalVariable).ToImmutableArray();
            collectedMethods.Add(method, new CollectedMethod(declaration, rawCode, locals));

            foreach (var clause in rawCode.Environment.Body?.ExceptionHandlingClauses ?? [])
                if (clause.Flags == ExceptionHandlingClauseOptions.Clause &&
                    clause.CatchType is { } catchType)
                    CollectTypeReferences(catchType);

            foreach (var instruction in rawCode.Instructions)
                CollectOperand(method, instruction);

            completedMethods.Add(method);
        }
        catch
        {
            collectedMethods.Remove(method);
            failedMethod ??= method;
            throw;
        }
        finally
        {
            inProgressMethods.Remove(method);
        }
    }

    private void CollectOperand(MethodBase source, CilInstructionInfo instruction)
    {
        try
        {
            switch (instruction.Instruction.Operand)
            {
                case null:
                case sbyte:
                case byte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                case ulong:
                case float:
                case double:
                case char:
                case string:
                case int[]:
                    return;
                case ParameterInfo parameter:
                    CollectTypeReferences(parameter.ParameterType);
                    return;
                case LocalVariableInfo local:
                    CollectTypeReferences(local.LocalType);
                    return;
                case FieldInfo field:
                    CollectField(field);
                    return;
                case MethodBase method:
                    CollectMethod(method);
                    return;
                case Type type:
                    CollectTypeReferences(type);
                    return;
                case byte[]:
                    throw new NotSupportedException("Inline signature metadata is not supported.");
                case var operand:
                    throw new NotSupportedException(
                        $"Metadata operand type {operand.GetType().FullName} is not supported.");
            }
        }
        catch (Exception exception) when (exception is not ValidationException)
        {
            throw new NotSupportedException(
                $"Failed to collect metadata operand at IL_{instruction.ByteOffset:X4} " +
                $"({instruction.Instruction.OpCode}) in {source}.",
                exception);
        }
    }

    private void CollectField(FieldInfo field)
    {
        if (field.DeclaringType is { } declaringType)
            CollectTypeReferences(declaringType);
        CollectTypeReferences(field.FieldType);

        if (field.IsStatic)
        {
            if (field.GetCustomAttributes().OfType<IAddressSpaceAttribute>().Any())
                _ = ParseStaticField(field);
            return;
        }

        _ = ParseField(field);
    }

    private void CollectMethodSignature(MethodBase method)
    {
        if (method.DeclaringType is { } declaringType)
        {
            if (method.IsStatic)
                _ = ParseType(declaringType);
            else
                CollectTypeReferences(declaringType);
        }
        if (method is MethodInfo methodInfo)
            CollectTypeReferences(methodInfo.ReturnType);
        foreach (var parameter in method.GetParameters())
            CollectTypeReferences(parameter.ParameterType);
        if (method is MethodInfo genericMethod)
            foreach (var argument in genericMethod.GetGenericArguments())
                CollectTypeReferences(argument);
    }

    private void CollectTypeReferences(Type type)
    {
        if (!collectedTypes.Add(type))
            return;
        if (type.HasElementType && type.GetElementType() is { } element)
            CollectTypeReferences(element);
        foreach (var argument in type.GetGenericArguments())
            CollectTypeReferences(argument);
        _ = ParseType(type);

        if (SharedBuiltinSymbolTable.Instance.RuntimeTypes.ContainsKey(type) ||
            type.IsPointer ||
            type.IsByRef ||
            type.IsArray ||
            type.IsGenericParameter)
            return;

        foreach (var field in type.GetFields(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                     BindingFlags.DeclaredOnly))
        {
            _ = ParseField(field);
            CollectTypeReferences(field.FieldType);
        }
    }

    private ShaderModuleDeclaration<RawCilFunctionBody> BuildModule()
    {
        var moduleSymbols = Context.Freeze();
        var definitions = collectedMethods
                          .OrderBy(pair => pair.Key.DeclaringType?.FullName, StringComparer.Ordinal)
                          .ThenBy(pair => pair.Key.Name, StringComparer.Ordinal)
                          .Select(pair =>
                          {
                              var collected = pair.Value;
                              var methodSymbols = new CompilationContext(moduleSymbols);
                              foreach (var (index, parameter) in collected.Declaration.Parameters.Index())
                                  methodSymbols.AddParameter(Symbol.Parameter(index), parameter);
                              foreach (var parameter in collected.Code.Environment.Parameters)
                              {
                                  var declarationIndex = parameter.Position +
                                                         (collected.Code.Environment.IsStatic ? 0 : 1);
                                  methodSymbols.AddParameter(
                                      Symbol.Parameter(parameter),
                                      collected.Declaration.Parameters[declarationIndex]);
                              }

                              foreach (var (local, declaration) in
                                       collected.Code.Environment.LocalVariables.Zip(collected.LocalVariables))
                                  methodSymbols.AddVariable(Symbol.Variable(local), declaration);

                              var body = new RawCilFunctionBody(
                                  collected.Declaration,
                                  collected.Code,
                                  collected.LocalVariables,
                                  methodSymbols.Freeze());
                              return KeyValuePair.Create(collected.Declaration, body);
                          })
                          .ToImmutableDictionary();

        var declarations = Context.StructureDeclarations.Cast<IDeclaration>()
                                  .Concat(Context.VariableDeclarations.Where(variable =>
                                      variable.AddressSpace is not FunctionAddressSpace))
                                  .Concat(Context.FunctionDeclarations)
                                  .Distinct()
                                  .OrderBy(declaration => declaration switch
                                  {
                                      StructureDeclaration => 0,
                                      VariableDeclaration => 1,
                                      FunctionDeclaration => 2,
                                      _ => 3
                                  })
                                  .ThenBy(declaration => declaration.Name, StringComparer.Ordinal)
                                  .ToImmutableArray();
        return new ShaderModuleDeclaration<RawCilFunctionBody>(declarations, definitions);
    }

    private FunctionDeclaration ParseMethodDeclaration(MethodBase method)
    {
        var symbol = Symbol.Function(method);
        if (Context[symbol] is { } found)
            return found;

        var metadataAttributes = method.GetCustomAttributes().OfType<IShaderMetadataAttribute>().ToFrozenSet();
        if (metadataAttributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } operationAttribute)
        {
            var function = operationAttribute.Operation.Function;
            Context.AddFunctionDeclaration(symbol, function);
            return function;
        }

        if (method.GetCustomAttributes().OfType<IShaderOperationMethodAttribute>().SingleOrDefault()
            is { } shaderOperationAttribute)
        {
            var result = ParseMethodReturn(method);
            var parameters = method.GetParameters().Select(ParseParameter);
            var operation = shaderOperationAttribute.GetOperation(result.Type, parameters.Select(p => p.Type));
            Context.AddFunctionDeclaration(symbol, operation.Function);
            return operation.Function;
        }

        var declaration = new FunctionDeclaration(
            method.Name,
            method.IsStatic
                ? [.. method.GetParameters().Select(ParseParameter)]
                :
                [
                    new ParameterDeclaration(
                        "this",
                        ParseType(method.DeclaringType ??
                                  throw new NotSupportedException($"Method {method} has no declaring type.")),
                        []),
                    .. method.GetParameters().Select(ParseParameter)
                ],
            ParseMethodReturn(method),
            ParseAttribute(method));
        Context.AddFunctionDeclaration(symbol, declaration);
        return declaration;
    }

    private FunctionReturn ParseMethodReturn(MethodBase method)
    {
        var returnType = method switch
        {
            MethodInfo methodInfo => ParseType(methodInfo.ReturnType),
            ConstructorInfo constructor => ParseType(constructor.DeclaringType ??
                                                      throw new NotSupportedException(
                                                          $"Constructor {constructor} has no declaring type.")),
            _ => throw new NotSupportedException($"Unsupported method {method}.")
        };
        var attributes = method is MethodInfo returnMethod ? ParseAttribute(returnMethod.ReturnParameter) : [];
        return new FunctionReturn(returnType, attributes);
    }

    private StructureType ParseStructDeclaration(Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(field => !field.Name.EndsWith("k__BackingField", StringComparison.Ordinal));
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var declaration = new StructureDeclaration
        {
            Name = type.Name,
            Attributes = [.. type.GetCustomAttributes().OfType<IShaderAttribute>()],
            Members =
            [
                .. fields.Select(ParseField),
                .. properties.Select(property => new MemberDeclaration(
                    property.Name,
                    ParseType(property.PropertyType),
                    [.. property.GetCustomAttributes().OfType<IShaderAttribute>()]))
            ]
        };
        return new StructureType(declaration);
    }

    private VariableDeclaration ParseModuleVariableDeclaration(FieldInfo field)
    {
        var symbol = Symbol.Variable(field);
        if (Context[symbol] is { } found)
            return found;
        return ParseStaticField(field);
    }

    private VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo property)
    {
        _ = property.GetGetMethod() ??
            throw new NotSupportedException("Properties without a getter are not supported.");
        var symbol = Symbol.Variable(property);
        if (Context[symbol] is { } found)
            return found;

        var addressSpace =
            property.GetCustomAttributes().OfType<IAddressSpaceAttribute>().Single().AddressSpace;
        var declaration = new VariableDeclaration(
            addressSpace,
            property.Name,
            ParseType(property.PropertyType),
            [.. property.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, declaration);
        return declaration;
    }

    private ImmutableArray<VariableDeclaration> ParseAllModuleVariableDeclarations(Type moduleType)
    {
        var fields = moduleType.GetFields(VariableBindingFlags)
                               .Where(field => field.GetCustomAttributes().Any(attribute =>
                                   attribute is IAddressSpaceAttribute))
                               .Select(ParseModuleVariableDeclaration);
        var properties = moduleType.GetProperties(VariableBindingFlags)
                                   .Where(property => property.GetCustomAttributes().Any(attribute =>
                                       attribute is IAddressSpaceAttribute))
                                   .Select(ParseModuleVariableDeclaration);
        return [.. fields, .. properties];
    }

    private VariableDeclaration ParseLocalVariable(LocalVariableInfo info) =>
        new(
            FunctionAddressSpace.Instance,
            $"loc_{info.LocalIndex}",
            ParseType(info.LocalType),
            []);

    private bool IsMethodBoundary(MethodBase method)
    {
        if (SharedBuiltinSymbolTable.Instance.RuntimeMethods.ContainsKey(method) ||
            method.GetCustomAttributes().Any(attribute =>
                attribute is IOperationMethodAttribute or IShaderOperationMethodAttribute))
            return true;

        return method.DeclaringType is { } declaringType &&
               Context[declaringType] is IVecType &&
               method.IsSpecialName &&
               method.GetCustomAttributes().Any(attribute => attribute is CompilerGeneratedAttribute);
    }

    private static ImmutableHashSet<IShaderAttribute> ParseAttribute(ParameterInfo parameter) =>
    [
        .. parameter.GetCustomAttributes<BuiltinAttribute>(),
        .. parameter.GetCustomAttributes<LocationAttribute>()
    ];

    private static ImmutableHashSet<IShaderAttribute> ParseAttribute(MethodBase method) =>
        [.. method.GetCustomAttributes().OfType<IShaderAttribute>()];

    private void EnsureUsable()
    {
        if (failedMethod is not null)
            throw new InvalidOperationException(
                $"This runtime-reflection parser failed while collecting {failedMethod}; " +
                "create a new parser with a fresh compilation context.");
    }

    private sealed record CollectedMethod(
        FunctionDeclaration Declaration,
        LinearCode<CilInstructionInfo> Code,
        ImmutableArray<VariableDeclaration> LocalVariables);
}
