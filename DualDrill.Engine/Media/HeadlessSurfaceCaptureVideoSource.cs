using DualDrill.Common.Abstraction.Signal;
using DualDrill.Engine.Headless;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using MessagePipe;

namespace DualDrill.Engine.Media;

public sealed class HeadlessSurfaceCaptureVideoSource
{
    object encoderLock = new();
    public HeadlessSurfaceCaptureVideoSource(ILogger<HeadlessSurfaceCaptureVideoSource> logger, HeadlessSurface surface)
    {
        var ffmpegLibFullPath = Environment.GetEnvironmentVariable("DUALDRILLFFMPEGPATH");
        SIPSorceryMedia.FFmpeg.FFmpegInit.Initialise(SIPSorceryMedia.FFmpeg.FfmpegLogLevelEnum.AV_LOG_VERBOSE, ffmpegLibFullPath, logger);
        Console.WriteLine(ffmpeg.RootPath);
        VideoEncoder = new();
        long bitrate = 1024L * 1024 * 1024;
        VideoEncoder.SetBitrate(bitrate, 512 * 1024 * 1024, bitrate, bitrate);
        Logger = logger;
        Surface = surface;
        //VideoTrack = new(VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
    }
    public DrillFFmpegVideoEncoder VideoEncoder { get; }
    public ILogger<HeadlessSurfaceCaptureVideoSource> Logger { get; }
    private HeadlessSurface Surface { get; }

    //public MediaStreamTrack VideoTrack { get; }

    public event EncodedFrameBufferSampleDelegate OnVideoSourceEncodedSample;
    public event RawVideoSampleDelegate OnVideoSourceRawSample;
    public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
    public event SourceErrorDelegate OnVideoSourceError;
    private SerialDisposable SurfaceFrameSubscriptions { get; } = new();

    public delegate void EncodedFrameBufferSampleDelegate(uint durationRtpUnits, VideoFrameBuffer sample);
    public async Task CloseVideo()
    {
        if (Enabled)
        {
            SurfaceFrameSubscriptions.Disposable = Disposable.Empty;
            Enabled = false;
        }
    }

    private bool Enabled = false;

    public VideoFrameBuffer EncodeVideo(int width, int height, ReadOnlySpan<byte> data)
    {
        lock (encoderLock)
        {
            var result = VideoEncoder.EncodeVideo(width, height, data, VideoPixelFormatsEnum.Bgra, VideoCodecsEnum.VP8);
            if (result is not null)
            {
                OnVideoSourceEncodedSample?.Invoke((uint)Frequency.TimeUnit<Frequency.RealtimeFrame>(), result);
            }
            return result;
        }
    }

    private void EncodeVideoFromFrame(HeadlessSurfaceFrame frame)
    {
        lock (encoderLock)
        {
            var result = VideoEncoder.EncodeVideo(frame.Size.Width, frame.Size.Height, frame.Data.ToArray(), VideoPixelFormatsEnum.Bgra, VideoCodecsEnum.VP8);
            if (result is not null)
            {
                OnVideoSourceEncodedSample?.Invoke(90000 / 60, result);
            }
        }
    }

    public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
    {
        throw new NotImplementedException();
    }

    public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
    {
        throw new NotImplementedException();
    }

    public void ForceKeyFrame()
    {
        throw new NotImplementedException();
    }

    public List<VideoFormat> GetVideoSourceFormats()
    {
        throw new NotImplementedException();
    }

    public bool HasEncodedVideoSubscribers()
    {
        throw new NotImplementedException();
    }

    public bool IsVideoSourcePaused()
    {
        return !Enabled;
    }

    public async Task PauseVideo()
    {
        if (Enabled)
        {
            SurfaceFrameSubscriptions.Disposable = Disposable.Empty;
            Enabled = false;
        }
    }

    public void RestrictFormats(Func<VideoFormat, bool> filter)
    {
        throw new NotImplementedException();
    }

    public async Task ResumeVideo()
    {
        if (!Enabled)
        {
            SurfaceFrameSubscriptions.Disposable = Surface.OnFrame.Subscribe(async (frame, cancellation) =>
            {
                EncodeVideoFromFrame(frame);
            });
            Enabled = true;
        }
    }

    public void SetVideoSourceFormat(VideoFormat videoFormat)
    {

    }

    public async Task StartVideo()
    {
        if (!Enabled)
        {
            SurfaceFrameSubscriptions.Disposable = Surface.OnFrame.Subscribe(async (frame, cancellation) =>
            {
                EncodeVideoFromFrame(frame);
            });
            Enabled = true;
        }
    }
}
