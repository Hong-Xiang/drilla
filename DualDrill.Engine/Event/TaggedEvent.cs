using System.Collections.Concurrent;
using System.Text;

namespace DualDrill.Engine.Event;

public static class TaggedEvent
{
    public static TaggedEvent<T> Create<T>(T e)
        where T : notnull
    {
        return new TaggedEvent<T>(e);
    }

    public static string GetNamespaceQualifiedName(this Type type)
    {
        if (type.IsGenericType)
        {
            var builder = new StringBuilder();
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            builder.Append(genericTypeDefinition.Namespace);
            builder.Append('.');
            builder.Append(genericTypeDefinition.Name);

            // Remove the ` to handle the arity  
            var backtickIndex = builder.ToString().IndexOf('`');
            if (backtickIndex > 0)
            {
                builder.Length = backtickIndex;
            }

            builder.Append('<');
            var genericArguments = type.GetGenericArguments();
            for (int i = 0; i < genericArguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }
                builder.Append(genericArguments[i].GetNamespaceQualifiedName());
            }
            builder.Append('>');

            return builder.ToString();
        }
        else
        {
            return type.FullName ?? type.Name;
        }
    }

    readonly static ConcurrentDictionary<Type, string> TypeTages = [];
    readonly static Lock Lock = new();

    public static string GetTypeTag<T>()
    {
        if (TypeTages.TryGetValue(typeof(T), out var existedTag))
        {
            return existedTag;
        }
        lock (Lock)
        {
            if (TypeTages.TryGetValue(typeof(T), out var existedTagSafe))
            {
                return existedTagSafe;
            }
            var result = GetNamespaceQualifiedName(typeof(T));
            _ = TypeTages.TryAdd(typeof(T), result);
            return result;
        }
    }

}

public interface ITaggedEvent
{
    public string Type { get; }
}

public readonly record struct TaggedEvent<T>(
    T Data
) : ITaggedEvent
    where T : notnull
{
    public string Type { get; } = TypeName;

    public static string TypeName => TaggedEvent.GetTypeTag<T>();
}
