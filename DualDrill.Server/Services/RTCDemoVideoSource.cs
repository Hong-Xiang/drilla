using FFmpeg.AutoGen;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.Encoders.Codecs;
using SIPSorceryMedia.FFmpeg;
using System.Collections.Concurrent;
using System.Net;

namespace DualDrill.Server.Services;

public sealed class RTCDemoVideoSource
{
    object encoderLock = new();
    public RTCDemoVideoSource(ILogger<RTCDemoVideoSource> logger)
    {
        var ffmpegLibFullPath = "C:\\Users\\Xiang\\AppData\\Local\\Microsoft\\WinGet\\Packages\\Gyan.FFmpeg.Shared_Microsoft.Winget.Source_8wekyb3d8bbwe\\ffmpeg-6.1.1-full_build-shared\\bin";
        //string? ffmpegLibFullPath = null;
        SIPSorceryMedia.FFmpeg.FFmpegInit.Initialise(SIPSorceryMedia.FFmpeg.FfmpegLogLevelEnum.AV_LOG_VERBOSE, ffmpegLibFullPath, logger);
        Console.WriteLine(ffmpeg.RootPath);
        VideoEncoder = new();
        long bitrate = 1024L * 1024 * 1024;
        VideoEncoder.SetBitrate(bitrate, 512 * 1024 * 1024, bitrate, bitrate);
        //VideoTrack = new(VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
    }
    public DrillFFmpegVideoEncoder VideoEncoder { get; }
    //public MediaStreamTrack VideoTrack { get; }

    public event EncodedFrameBufferSampleDelegate OnVideoSourceEncodedSample;
    public event RawVideoSampleDelegate OnVideoSourceRawSample;
    public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
    public event SourceErrorDelegate OnVideoSourceError;

    public delegate void EncodedFrameBufferSampleDelegate(uint durationRtpUnits, VideoFrameBuffer sample);
    public async Task CloseVideo()
    {
        Console.WriteLine("Close Video Called");
    }

    public VideoFrameBuffer EncodeVideo(int width, int height, ReadOnlySpan<byte> data)
    {
        lock (encoderLock)
        {
            var result = VideoEncoder.EncodeVideo(width, height, data, SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.Bgra, SIPSorceryMedia.Abstractions.VideoCodecsEnum.VP8);
            if (result is not null)
            {
                OnVideoSourceEncodedSample?.Invoke(90000 / 60, result);
            }
            return result;
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
        throw new NotImplementedException();
    }

    public Task PauseVideo()
    {
        throw new NotImplementedException();
    }

    public void RestrictFormats(Func<VideoFormat, bool> filter)
    {
        throw new NotImplementedException();
    }

    public Task ResumeVideo()
    {
        throw new NotImplementedException();
    }

    public void SetVideoSourceFormat(VideoFormat videoFormat)
    {

    }

    public async Task StartVideo()
    {
        Console.WriteLine("Start Video Called");
    }
}

public class Vp8NetVideoEncoderEndPoint : IVideoSource, IVideoSink, IDisposable
{
    private const int VIDEO_SAMPLING_RATE = 90000;
    private const int DEFAULT_FRAMES_PER_SECOND = 30;
    private const int VP8_FORMAT_ID = 96;

    private ILogger logger = SIPSorcery.LogFactory.CreateLogger<Vp8NetVideoEncoderEndPoint>();

    public readonly List<VideoFormat> SupportedFormats = new List<VideoFormat>
        {
            new VideoFormat(VideoCodecsEnum.VP8, VP8_FORMAT_ID, VIDEO_SAMPLING_RATE)
    };

    private MediaFormatManager<VideoFormat> _formatManager;
    private VpxVideoEncoder _vp8Codec;
    private bool _isClosed;

    /// <summary>
    /// This video source DOES NOT generate raw samples. Subscribe to the encoded samples event
    /// to get samples ready for passing to the RTP transport layer.
    /// </summary>
    [Obsolete("This video source only generates encoded samples. No raw video samples will be supplied to this event.")]
    public event RawVideoSampleDelegate OnVideoSourceRawSample { add { } remove { } }

    /// <summary>
    /// This event will be fired whenever a video sample is encoded and is ready to transmit to the remote party.
    /// </summary>
    public event EncodedSampleDelegate OnVideoSourceEncodedSample;

    /// <summary>
    /// This event is fired after the sink decodes a video frame from the remote party.
    /// </summary>
    public event VideoSinkSampleDecodedDelegate OnVideoSinkDecodedSample;

#pragma warning disable CS0067
    public event SourceErrorDelegate OnVideoSourceError;
    public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
    public event VideoSinkSampleDecodedFasterDelegate OnVideoSinkDecodedSampleFaster;
#pragma warning restore CS0067

    /// <summary>
    /// Creates a new video source that can encode and decode samples.
    /// </summary>
    public Vp8NetVideoEncoderEndPoint()
    {
        _formatManager = new MediaFormatManager<VideoFormat>(SupportedFormats);
        _vp8Codec = new VpxVideoEncoder();
    }

    public void RestrictFormats(Func<VideoFormat, bool> filter) => _formatManager.RestrictFormats(filter);
    public List<VideoFormat> GetVideoSourceFormats() => _formatManager.GetSourceFormats();
    public void SetVideoSourceFormat(VideoFormat videoFormat) => _formatManager.SetSelectedFormat(videoFormat);
    public List<VideoFormat> GetVideoSinkFormats() => _formatManager.GetSourceFormats();
    public void SetVideoSinkFormat(VideoFormat videoFormat) => _formatManager.SetSelectedFormat(videoFormat);

    public void ForceKeyFrame() => throw new NotSupportedException();
    public void GotVideoRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload) =>
        throw new ApplicationException("The Windows Video End Point requires full video frames rather than individual RTP packets.");
    public bool HasEncodedVideoSubscribers() => OnVideoSourceEncodedSample != null;
    public bool IsVideoSourcePaused() => false;
    public Task PauseVideo() => Task.CompletedTask;
    public Task ResumeVideo() => Task.CompletedTask;
    public Task StartVideo() => Task.CompletedTask;
    public Task CloseVideoSink() => Task.CompletedTask;
    public Task PauseVideoSink() => Task.CompletedTask;
    public Task ResumeVideoSink() => Task.CompletedTask;
    public Task StartVideoSink() => Task.CompletedTask;

    public MediaEndPoints ToMediaEndPoints()
    {
        return new MediaEndPoints
        {
            VideoSource = this,
            VideoSink = this
        };
    }

    public void EncodeVideo()
    {
    }

    public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
    {
        if (!_isClosed)
        {
            if (OnVideoSourceEncodedSample != null)
            {
                var encodedBuffer = _vp8Codec.EncodeVideo(width, height, sample, pixelFormat, VideoCodecsEnum.VP8);

                if (encodedBuffer != null)
                {
                    uint fps = (durationMilliseconds > 0) ? 1000 / durationMilliseconds : DEFAULT_FRAMES_PER_SECOND;
                    uint durationRtpTS = VIDEO_SAMPLING_RATE / fps;
                    OnVideoSourceEncodedSample.Invoke(durationRtpTS, encodedBuffer);
                }
            }
        }
    }

    public void GotVideoFrame(IPEndPoint remoteEndPoint, uint timestamp, byte[] frame, VideoFormat format)
    {
        if (!_isClosed)
        {
            foreach (var decoded in _vp8Codec.DecodeVideo(frame, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8))
            {
                OnVideoSinkDecodedSample(decoded.Sample, decoded.Width, decoded.Height, (int)(decoded.Width * 3), VideoPixelFormatsEnum.Bgr);
            }
        }
    }

    public Task CloseVideo()
    {
        if (!_isClosed)
        {
            _isClosed = true;
            Dispose();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _vp8Codec?.Dispose();
    }

    public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
    {
        throw new NotImplementedException();
    }
}




