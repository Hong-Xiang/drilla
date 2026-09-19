using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

// Executes the emitter's real lexical control flow, never its original CFG successors.
// ponytail: deliberately only the scalar emitter grammar used here; reject additions until tested.
internal sealed class EmittedScalarProgram
{
    private abstract record Flow
    {
        private Flow() { }
        internal sealed record Next : Flow;
        internal sealed record Break : Flow;
        internal sealed record Continue : Flow;
        internal sealed record Return(Value Value) : Flow;
    }

    private sealed record Slot(IShaderType Type, bool ReadOnly, Value? Value);

    private sealed class State(string name, int limit)
    {
        internal Budget Budget { get; } = new($"Emitted {name}", limit);
        internal List<Label> Trace { get; } = [];
        internal Stack<Dictionary<string, Slot>> Scopes { get; } = new([new()]);

        internal Value Read(string name) =>
            Find(name)[name].Value ?? throw new InvalidOperationException($"Uninitialized emitted scalar {name}.");

        private Dictionary<string, Slot> Find(string name) =>
            Scopes.FirstOrDefault(scope => scope.ContainsKey(name)) ??
            throw new InvalidOperationException($"Undefined emitted scalar {name}.");

        internal void Declare(string name, Slot slot)
        {
            if (slot.Value is { } value && !HasType(value, slot.Type))
                throw new NotSupportedException($"Emitted {name}: expected {slot.Type.Name}, got {value}.");
            if (!Scopes.Peek().TryAdd(name, slot))
                throw new InvalidOperationException($"Duplicate emitted declaration {name} in one lexical scope.");
        }

        internal void Assign(string name, Value value)
        {
            var scope = Find(name);
            var slot = scope[name];
            if (slot.ReadOnly || !HasType(value, slot.Type))
                throw new NotSupportedException($"Invalid emitted assignment to {name}: {value}.");
            scope[name] = slot with { Value = value };
        }
    }

    private readonly FunctionBody4 body;
    private readonly Func<State, Flow> execute;

    internal EmittedScalarProgram(FunctionBody4 body, string source)
    {
        this.body = body;
        var lines = source.Replace("{", "\n{\n", StringComparison.Ordinal)
            .Replace("}", "\n}\n", StringComparison.Ordinal)
            .Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        var signature = $"{body.Declaration.ReturnType.Name} {body.Declaration.Name}(" +
            string.Concat(body.Declaration.Parameters.Select(p => $"{p.Type.Name} {p.Name}, ")) + ")";
        var start = Array.IndexOf(lines, signature);
        if (start < 0 || lines.Count(line => line == signature) != 1)
            throw new NotSupportedException($"Expected exactly one scalar function signature: {signature}");
        foreach (var prefix in lines[..start])
            if (prefix is not ("typealias f32 = float;" or "typealias u32 = uint;" or "typealias i32 = int;"
                or "typealias vec4<t> = vector<t, 4>;" or "typealias vec3<t> = vector<t, 3>;"
                or "typealias vec2<t> = vector<t, 2>;" or "[shader(\"vertex\")]" or "[shader(\"fragment\")]"))
                throw new NotSupportedException($"Unsupported emitted prefix: {prefix}");
        var position = start + 1;

        string Take() => position < lines.Length ? lines[position++] :
            throw new NotSupportedException($"Emitted {body.Declaration.Name}: unexpected end of source.");
        void Expect(string expected)
        {
            var actual = Take();
            if (actual != expected)
                throw new NotSupportedException($"Expected '{expected}', got '{actual}' in {body.Declaration.Name}.");
        }

        Func<State, Flow> Block()
        {
            Expect("{");
            var statements = new List<Func<State, Flow>>();
            while (position < lines.Length && lines[position] != "}")
            {
                var line = Take();
                if (line.StartsWith("//", StringComparison.Ordinal) &&
                    !line.StartsWith("// block", StringComparison.Ordinal) &&
                    !line.StartsWith("// loop", StringComparison.Ordinal))
                    continue;
                var statement = Statement(line);
                statements.Add(state =>
                {
                    state.Budget.Step(line);
                    return statement(state);
                });
            }
            Expect("}");
            return state =>
            {
                state.Scopes.Push([]);
                try
                {
                    foreach (var statement in statements)
                    {
                        var flow = statement(state);
                        if (flow is not Flow.Next)
                            return flow;
                    }
                    return new Flow.Next();
                }
                finally
                {
                    state.Scopes.Pop();
                }
            };
        }

        Func<State, Flow> Statement(string line)
        {
            if (line == "{")
            {
                position--;
                return Block();
            }
            if (line == "while(true)")
            {
                var loop = Block();
                return state =>
                {
                    while (true)
                    {
                        state.Budget.Step("while(true)");
                        switch (loop(state))
                        {
                            case Flow.Return returned: return returned;
                            case Flow.Break: return new Flow.Next();
                            case Flow.Next or Flow.Continue: break;
                            default: throw new NotSupportedException("Unsupported loop flow.");
                        }
                    }
                };
            }
            if (Regex.Match(line, @"^if\((.+)\)$") is { Success: true } conditional)
            {
                var condition = Expression(conditional.Groups[1].Value);
                var whenTrue = Block();
                Expect("else");
                var whenFalse = Block();
                return state => condition(state).Bool ? whenTrue(state) : whenFalse(state);
            }
            if (Regex.Match(line, @"^// (?:block|loop) \^\d+Label\(([^()]*)\)$") is { Success: true } marker)
            {
                var name = marker.Groups[1].Value;
                var labels = body.Labels.Where(label => label.Name == name).ToArray();
                if (labels.Length != 1)
                    throw new NotSupportedException($"Unresolved/ambiguous original block marker: {line}");
                return state => { state.Trace.Add(labels[0]); return new Flow.Next(); };
            }
            if (line == "break;") return _ => new Flow.Break();
            if (line == "continue;") return _ => new Flow.Continue();
            if (Regex.Match(line, @"^return (.+);$") is { Success: true } returned)
            {
                var value = Expression(returned.Groups[1].Value);
                return state => new Flow.Return(value(state));
            }
            if (Regex.Match(line, @"^var (\w+) : (i32|bool);$") is { Success: true } variable)
                return state =>
                {
                    state.Declare(variable.Groups[1].Value, new Slot(Type(variable.Groups[2].Value), false, null));
                    return new Flow.Next();
                };
            if (Regex.Match(line, @"^let (\w+) : (i32|bool) = (.+);$") is { Success: true } local)
            {
                var value = Expression(local.Groups[3].Value);
                return state =>
                {
                    state.Declare(local.Groups[1].Value, new Slot(Type(local.Groups[2].Value), true, value(state)));
                    return new Flow.Next();
                };
            }
            if (Regex.Match(line, @"^(\w+) = (.+);$") is { Success: true } assignment)
            {
                var value = Expression(assignment.Groups[2].Value);
                return state => { state.Assign(assignment.Groups[1].Value, value(state)); return new Flow.Next(); };
            }
            throw new NotSupportedException($"Emitted {body.Declaration.Name}: unsupported statement '{line}'.");
        }

        execute = Block();
        if (position != lines.Length)
            throw new NotSupportedException($"Unexpected source after {body.Declaration.Name}: {lines[position]}");
    }

    internal Execution Run(ImmutableArray<Value> arguments, int stepLimit = 10000)
    {
        if (arguments.Length != body.Declaration.Parameters.Length)
            throw new InvalidOperationException("Incorrect emitted scalar argument count.");
        var state = new State(body.Declaration.Name, stepLimit);
        foreach (var (parameter, argument) in body.Declaration.Parameters.Zip(arguments))
            state.Declare(parameter.Name, new Slot(parameter.Type, false, argument));
        if (execute(state) is not Flow.Return returned)
            throw new InvalidOperationException($"Emitted {body.Declaration.Name}: fell through without a return.");
        // Slang permits scalar numeric/bool conversion to the declared return type.
        return new Execution(Convert(returned.Value, body.Declaration.ReturnType), [.. state.Trace]);
    }

    private static IShaderType Type(string type) => type switch
    {
        "i32" => ShaderType.I32,
        "bool" => ShaderType.Bool,
        _ => throw new NotSupportedException($"Unsupported emitted scalar type {type}")
    };

    private static Func<State, Value> Atom(string text)
    {
        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            return _ => new Value.Integer(integer);
        if (text is "True" or "true") return _ => new Value.Boolean(true);
        if (text is "False" or "false") return _ => new Value.Boolean(false);
        if (Regex.IsMatch(text, @"^[A-Za-z_]\w*$")) return state => state.Read(text);
        throw new NotSupportedException($"Unsupported emitted scalar atom '{text}'.");
    }

    private static Func<State, Value> Expression(string text)
    {
        if (Regex.Match(text, @"^(i32|bool)\(([^()]+)\)$") is { Success: true } conversion)
        {
            var atom = Atom(conversion.Groups[2].Value);
            var type = Type(conversion.Groups[1].Value);
            return state => Convert(atom(state), type);
        }
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return Atom(text);
        if (parts.Length != 3)
            throw new NotSupportedException($"Unsupported emitted scalar expression '{text}'.");
        var left = Atom(parts[0]);
        var right = Atom(parts[2]);
        IBinaryOp op = parts[1] switch
        {
            "+" => BinaryArithmetic.Add.Instance,
            "-" => BinaryArithmetic.Sub.Instance,
            "*" => BinaryArithmetic.Mul.Instance,
            "==" => BinaryRelational.Eq.Instance,
            "!=" => BinaryRelational.Ne.Instance,
            "<" => BinaryRelational.Lt.Instance,
            "<=" => BinaryRelational.Le.Instance,
            ">" => BinaryRelational.Gt.Instance,
            ">=" => BinaryRelational.Ge.Instance,
            _ => throw new NotSupportedException($"Unsupported emitted scalar operator '{parts[1]}'.")
        };
        return state => Binary(op, left(state), right(state));
    }
}
