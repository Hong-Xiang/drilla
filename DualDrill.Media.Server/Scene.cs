using Microsoft.Extensions.Primitives;

internal enum Scene
{
    Triangle,
    Raymarching,
}

internal static class SceneQuery
{
    internal static bool TryParse(IQueryCollection query, out Scene scene)
    {
        scene = Scene.Triangle;
        if (query.Count == 0)
        {
            return true;
        }

        if (query.Count != 1)
        {
            return false;
        }

        KeyValuePair<string, StringValues> parameter = query.First();
        if (parameter.Key != "scene" || parameter.Value.Count != 1)
        {
            return false;
        }

        switch (parameter.Value[0])
        {
            case "triangle":
                return true;
            case "raymarching":
                scene = Scene.Raymarching;
                return true;
            default:
                return false;
        }
    }

    internal static int RunSelfTest()
    {
        static IQueryCollection Query(string key, params string[] values) =>
            new QueryCollection(
                new Dictionary<string, StringValues>
                {
                    [key] = new(values),
                });

        if (!TryParse(QueryCollection.Empty, out Scene defaultScene) ||
            defaultScene != Scene.Triangle ||
            !TryParse(Query("scene", "triangle"), out Scene triangle) ||
            triangle != Scene.Triangle ||
            !TryParse(Query("scene", "raymarching"), out Scene raymarching) ||
            raymarching != Scene.Raymarching ||
            TryParse(Query("scene", ""), out _) ||
            TryParse(Query("scene", "triangle", "raymarching"), out _) ||
            TryParse(Query("scene", "unknown"), out _) ||
            TryParse(Query("extra", "triangle"), out _))
        {
            throw new InvalidOperationException("Scene query validation failed.");
        }

        var gate = new SessionGate(1);
        if (TryParse(Query("scene", "invalid"), out _))
        {
            throw new InvalidOperationException("An invalid scene query was accepted.");
        }
        using SessionAdmission? admission = gate.TryAcquire();
        if (admission is null)
        {
            throw new InvalidOperationException("Invalid scene validation consumed admission.");
        }

        Console.WriteLine(
            "Scene query default, choices, malformed rejection, and pre-admission validation passed.");
        return 0;
    }
}
