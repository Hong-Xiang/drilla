using System.Reflection;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public static class Symbol
{
    public static IVariableSymbol Variable(FieldInfo field) => new ShaderModuleFieldVariableSymbol(field);
    public static IVariableSymbol Variable(PropertyInfo getter) => new ShaderModulePropertyGetterVariableSymbol(getter);
    public static IVariableSymbol Variable(LocalVariableInfo info) => new LocalVariableIndexSymbol(info.LocalIndex);
    public static IVariableSymbol Variable(int localIndex) => new LocalVariableIndexSymbol(localIndex);
    public static IFunctionSymbol Function(MethodBase method) => new CSharpMethodFunctionSymbol(method);

    public static IParameterSymbol Parameter(ParameterInfo info) => new ParameterInfoSymbol(info);
    public static IParameterSymbol Parameter(int index) => new ParameterIndexSymbol(index);
}