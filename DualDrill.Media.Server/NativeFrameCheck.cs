using Gst;
using Gst.App;

internal static class NativeFrameCheck
{
    internal static int Run()
    {
        using Pipeline pipeline = Pipeline.New("cpu-frame-check");
        using AppSrc source = ElementFactory.Make("appsrc", "source") as AppSrc
            ?? throw new InvalidOperationException("appsrc is unavailable.");
        using AppSink sink = ElementFactory.Make("appsink", "sink") as AppSink
            ?? throw new InvalidOperationException("appsink is unavailable.");
        var input = new CpuBgraInput(source);
        sink.SetProperty("sync", false);
        if (!pipeline.AddMany(source, sink) || !source.Link(sink))
            throw new InvalidOperationException("Could not link the CPU frame check.");

        try
        {
            if (pipeline.SetState(State.Playing) == StateChangeReturn.Failure)
                throw new InvalidOperationException("The CPU frame check could not start.");
            var pixels = new byte[CpuFrames.FrameBytes];
            var first = PushAndRead(input, sink, pixels);
            Thread.Sleep(250);
            var second = PushAndRead(input, sink, pixels);
            var elapsed = second.Nanoseconds - first.Nanoseconds;
            if (second <= first || elapsed < 200 * ClockTime.NanosecondsPerMillisecond)
                throw new InvalidOperationException("A paused source did not resume at live running time.");
            Console.WriteLine($"Native CPU copy and live timestamp recovery passed ({elapsed / 1_000_000d:F1} ms).");
            return 0;
        }
        finally
        {
            pipeline.SetState(State.Null);
        }
    }

    private static ClockTime PushAndRead(CpuBgraInput input, AppSink sink, byte[] pixels)
    {
        CpuFrames.Fill(pixels, 0);
        if (input.Push(pixels) != FlowReturn.Ok)
            throw new InvalidOperationException("appsrc rejected the test frame.");
        Array.Clear(pixels);
        using Sample sample = sink.TryPullSample(ClockTime.FromSeconds(5))
            ?? throw new InvalidOperationException("appsink did not receive the CPU frame.");
        using Gst.Buffer buffer = sample.GetBuffer()
            ?? throw new InvalidOperationException("The sample had no buffer.");
        using var map = buffer.Map(MapFlags.Read);
        if (map.Span.Length != CpuFrames.FrameBytes || map.Span[(60 * CpuFrames.Width + 80) * 4 + 2] != 255)
            throw new InvalidOperationException("Reusing caller memory corrupted the submitted CPU frame.");
        if (buffer.Pts.IsNone)
            throw new InvalidOperationException("The CPU frame has no live presentation timestamp.");
        return buffer.Pts;
    }
}
