using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SysWeaver.Media
{
    sealed unsafe class VideoStreamDecoder : IDisposable
    {
        readonly AVCodecContext* _pCodecContext;
        readonly AVFormatContext* _pFormatContext;
        readonly int _streamIndex;
        readonly AVFrame* _pFrame;
        readonly AVFrame* _receivedFrame;
        readonly AVPacket* _pPacket;

        static String ReadString(Byte* data)
        {
            for (int i = 0; ; ++ i)
            {
                if (data[i] == 0)
                    return System.Text.Encoding.UTF8.GetString(data, i);
            }
        }
        static IReadOnlyDictionary<String, String> ReadMetaData(AVDictionary* meta)
        {
            AVDictionaryEntry* d = null;
            if (meta != null)
            {
                Dictionary<String, String> metaD = new Dictionary<string, string>(StringComparer.Ordinal);
                for (; ; )
                {
                    d = ffmpeg.av_dict_get(meta, "", d, ffmpeg.AV_DICT_IGNORE_SUFFIX);
                    if (d == null)
                        break;
                    var key = ReadString(d->key);
                    if (key == null)
                        continue;
                    var value = ReadString(d->value);
                    if (value == null)
                        continue;
                    metaD[key.FastToLower()] = value;
                }
                if (metaD.Count > 0)
                    return metaD.Freeze();
            }
            return null;
        }

        public String GetMetaData(String key)
        {
            var d = ffmpeg.av_dict_get(_pFormatContext->metadata, key, null, ffmpeg.AV_DICT_MATCH_CASE);
            if (d == null)
                return null;
            return ReadString(d->value);
        }

        public String GetVideoMetaData(String key)
        {
            var d = ffmpeg.av_dict_get(Stream->metadata, key, null, ffmpeg.AV_DICT_MATCH_CASE);
            if (d == null)
                return null;
            return ReadString(d->value);
        }

        public VideoStreamDecoder(string url, AVHWDeviceType hardwareCodec = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            _pFormatContext = ffmpeg.avformat_alloc_context();
            _receivedFrame = ffmpeg.av_frame_alloc();
            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_open_input(&pFormatContext, url, null, null).ThrowExceptionIfError();
            ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();
            AVCodec* codec = null;
            _streamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0).ThrowExceptionIfError();
            _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (hardwareCodec != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                var e = ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, hardwareCodec, null, null, 0);
                if (e < 0)
                {
#if DEBUG
                    Console.WriteLine("[Ffmpeg] Hardware decoding disabled for " + url.ToQuoted());
#endif//DEBUG
                    hardwareCodec = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                }
            }
            var stream = _pFormatContext->streams[_streamIndex];
            Stream = stream;
            ffmpeg.avcodec_parameters_to_context(_pCodecContext, stream->codecpar).ThrowExceptionIfError();
            ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();

            CodecName = ffmpeg.avcodec_get_name(codec->id);
            FrameWidth = _pCodecContext->width;
            FrameHeight = _pCodecContext->height;
            HardwareFormat = Ffmpeg.GetHWPixelFormat(hardwareCodec);
            SoftwareFormat = _pCodecContext->pix_fmt;
          

            TimeScaleNum = stream->time_base.num;
            TimeScaleDenom = stream->time_base.den;

            decimal fps = stream->avg_frame_rate.num;
            decimal duration = pFormatContext->duration;



            VideoMetaData = ReadMetaData(stream->metadata);
            MetaData = ReadMetaData(pFormatContext->metadata);
            duration /= ffmpeg.AV_TIME_BASE;
            if (stream->avg_frame_rate.den != 0)
                fps /= stream->avg_frame_rate.den;
            Duration = duration;
            Fps = fps;
            _pPacket = ffmpeg.av_packet_alloc();
            _pFrame = ffmpeg.av_frame_alloc();
        }
        readonly AVStream* Stream;

        readonly int TimeScaleNum;
        readonly int TimeScaleDenom;


        public readonly string CodecName;
        public readonly int FrameWidth;
        public readonly int FrameHeight;
        public readonly Decimal Duration;
        public readonly Decimal Fps;

        public readonly AVPixelFormat HardwareFormat;

        public readonly AVPixelFormat SoftwareFormat;

        public readonly IReadOnlyDictionary<String, String> VideoMetaData;
        public readonly IReadOnlyDictionary<String, String> MetaData;
        public decimal Time { get; private set; }
        public bool IsKeyFrame { get; private set; }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_free(_pFrame);

            ffmpeg.av_packet_unref(_pPacket);
            ffmpeg.av_free(_pPacket);

            var pCodecContext = _pCodecContext;
            ffmpeg.avcodec_free_context(&pCodecContext);
            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        public bool TryDecodeNextFrame(out AVFrame frame, out bool hardware)
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_frame_unref(_receivedFrame);
            int error;
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *_pFrame;
                            hardware = false;
                            return false;
                        }

                        error.ThrowExceptionIfError();
                    } while (_pPacket->stream_index != _streamIndex);

                    ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));
            error.ThrowExceptionIfError();
            Decimal time = _pFrame->pkt_dts;
            time *= TimeScaleNum;
            if (TimeScaleDenom != 0)
                time /= TimeScaleDenom;
            Time = time;
            const int AV_FRAME_FLAG_KEY = (1 << 1);
            IsKeyFrame = ((_pFrame->flags & AV_FRAME_FLAG_KEY) != 0) || (_pFrame->pict_type == AVPictureType.AV_PICTURE_TYPE_I);
            if (_pFrame->hw_frames_ctx != null)
            {
                error = ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0);
                error.ThrowExceptionIfError();
                frame = *_receivedFrame;
                hardware = true;
            }
            else
            {
                frame = *_pFrame;
                hardware = false;
            }
            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }
    }
}
