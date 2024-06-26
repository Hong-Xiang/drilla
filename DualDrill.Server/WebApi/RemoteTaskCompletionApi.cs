using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace DualDrill.Server.WebApi;

public static class RemoteTaskCompletionApi
{
    public static Task<IResult> SetResult(nint handle)
    {
        throw new NotImplementedException();
    }

    public static Task<IResult> CreateTaskCompletionSource()
    {
        var tcs = new TaskCompletionSource();
        var handle = GCHandle.Alloc(tcs);
        throw new NotImplementedException();
    }
    public static async Task<IResult> WaitTaskCompletionDone(nint handle)
    {
        var tcs = (TaskCompletionSource)GCHandle.FromIntPtr(handle).Target;
        await tcs.Task.ConfigureAwait(false);
        Console.WriteLine("Finished");
        return Results.Ok("Finished");
    }


    public static void MapRemoteTaskCompletionApi(this WebApplication app)
    {
        app.MapGet("/api/remote-task-completion/{handle}", WaitTaskCompletionDone);
        app.MapPost("/api/remote-task-completion/{handle}", SetResult);
        app.MapPost("/api/remote-task-completion/", CreateTaskCompletionSource);
    }
}
