global using ShaderExpr =
    DualDrill.CLSL.Language.Expression.IExpressionTree<DualDrill.CLSL.Language.Symbol.ShaderValue>;
global using ShaderStmt =
    DualDrill.CLSL.Language.Statement.IStatement<DualDrill.CLSL.Language.Symbol.ShaderValue,
        DualDrill.CLSL.Language.Expression.IExpressionTree<DualDrill.CLSL.Language.Symbol.ShaderValue>,
        DualDrill.CLSL.Language.Symbol.ShaderValue,
        DualDrill.CLSL.Language.Declaration.FunctionDeclaration>;