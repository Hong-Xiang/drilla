namespace DualDrill.Client;

internal sealed record class InteractiveServerHandle(string Handle)
{
    public static InteractiveServerHandle? Instance { get; private set; }

    private static TaskCompletionSource<InteractiveServerHandle> InteractiveServerHandleTaskCompletionSource = new TaskCompletionSource<InteractiveServerHandle>();

    internal static Task<InteractiveServerHandle> GetInteractiveServerHandleAsync()
    {
        return InteractiveServerHandleTaskCompletionSource.Task;
    }

    internal static void SetHandle(string handle)
    {
        var server = new InteractiveServerHandle(handle);
        Console.WriteLine("Set Handle Called");
        if (Instance is null)
        {
            Instance = server;
            InteractiveServerHandleTaskCompletionSource.SetResult(server);
        }
        else
        {
            Console.Error.WriteLine("Set can only called once");
        }
    }
}
