using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface ISequenceIdContext<T>
{
    int GetId(T entity, out bool isNewId);
}

internal sealed class SingleSequenceIdContext<T> : ISequenceIdContext<T>
    where T : notnull
{
    private readonly Dictionary<T, int> indices = [];

    public int GetId(T entity, out bool isNew)
    {
        lock (indices)
        {
            if (indices.TryGetValue(entity, out var index))
            {
                isNew = false;
                return index;
            }

            isNew = true;
            indices.Add(entity, indices.Count);
            return indices[entity];
        }
    }
}

public sealed class ShaderObjectSequenceIdContext
    : ISequenceIdContext<IShaderType>
    , ISequenceIdContext<IShaderValue>
    , ISequenceIdContext<Label>
{
    private readonly SingleSequenceIdContext<Label> labels = new();
    private readonly SingleSequenceIdContext<IShaderType> types = new();
    private readonly SingleSequenceIdContext<IShaderValue> values = new();

    public int GetId(IShaderType entity, out bool isNewId) => types.GetId(entity, out isNewId);

    public int GetId(IShaderValue entity, out bool isNewId) => values.GetId(entity, out isNewId);

    public int GetId(Label entity, out bool isNewId) => labels.GetId(entity, out isNewId);
}