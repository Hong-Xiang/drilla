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
