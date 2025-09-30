﻿using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LiteralOperation : IOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => "literal";


    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.Literal(inst, this, inst.Result,
            inst.Payload as ILiteral ?? throw new ArgumentException("invalid payload for instruction"));
}