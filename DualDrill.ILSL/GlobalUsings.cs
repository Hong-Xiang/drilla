global using ShaderExpr =
    DualDrill.CLSL.Language.Expression.IExpressionTree<DualDrill.CLSL.Language.Symbol.IShaderValue>;
global using ShaderStmt =
    DualDrill.CLSL.Language.Statement.IStatement<DualDrill.CLSL.Language.Symbol.IShaderValue,
        DualDrill.CLSL.Language.Expression.IExpressionTree<DualDrill.CLSL.Language.Symbol.IShaderValue>,
        DualDrill.CLSL.Language.Symbol.IShaderValue,
        DualDrill.CLSL.Language.Declaration.FunctionDeclaration>;