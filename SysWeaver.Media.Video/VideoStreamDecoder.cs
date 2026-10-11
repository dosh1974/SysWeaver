using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SysWeaver.Media
{
    sealed unsafe class VideoStreamDecoder : IDisposable
    {
        //  Not readonly: FreeNative frees them and sets them to null (Dispose can be called more than once)
        AVCodecContext* _pCodecContext;
        AVFormatContext* _pFormatContext;
        readonly int _streamIndex;
        AVFrame* _pFrame;
        AVFrame* _receivedFrame;
        AVPacket* _pPacket;

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
            try
            {
                var pFormatContext = ffmpeg.avformat_alloc_context();
                if (pFormatContext == null)
                    throw new OutOfMemoryException("Failed to allocate a format context");
                //  On failure avformat_open_input frees the context and sets pFormatContext to null, so only an opened context is stored (freed with avformat_close_input)
                var openError = ffmpeg.avformat_open_input(&pFormatContext, url, null, null);
                _pFormatContext = pFormatContext;
                openError.ThrowExceptionIfError();
                _receivedFrame = AllocFrame();
                _pFrame = AllocFrame();
                _pPacket = ffmpeg.av_packet_alloc();
                if (_pPacket == null)
                    throw new OutOfMemoryException("Failed to allocate a packet");
                ffmpeg.avformat_find_stream_info(pFormatContext, null).ThrowExceptionIfError();
                AVCodec* codec = null;
                _streamIndex = ffmpeg.av_find_best_stream(pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0).ThrowExceptionIfError();
                _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);
                if (_pCodecContext == null)
                    throw new OutOfMemoryException("Failed to allocate a codec context");
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
                var stream = pFormatContext->streams[_streamIndex];
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
            }
            catch
            {
                //  Free everything allocated so far (the caller never gets an instance to dispose)
                FreeNative();
                throw;
            }
        }

        static AVFrame* AllocFrame()
        {
            var f = ffmpeg.av_frame_alloc();
            if (f == null)
                throw new OutOfMemoryException("Failed to allocate a frame");
            return f;
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
            FreeNative();
        }

        /// <summary>
        /// Free all native objects (null ones are skipped) and set them to null, safe to call more than once.
        /// av_frame_free / av_packet_free also unreference the buffers (av_free only freed the struct and leaked them).
        /// </summary>
        void FreeNative()
        {
            _lastFrame = null;
            var pFrame = _pFrame;
            _pFrame = null;
            ffmpeg.av_frame_free(&pFrame);

            var receivedFrame = _receivedFrame;
            _receivedFrame = null;
            ffmpeg.av_frame_free(&receivedFrame);

            var pPacket = _pPacket;
            _pPacket = null;
            ffmpeg.av_packet_free(&pPacket);

            //  Also unreferences hw_device_ctx
            var pCodecContext = _pCodecContext;
            _pCodecContext = null;
            ffmpeg.avcodec_free_context(&pCodecContext);

            var pFormatContext = _pFormatContext;
            _pFormatContext = null;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        /// <summary>
        /// The native frame returned by the last successful TryDecodeNextFrame (_pFrame or _receivedFrame), null if the last call failed.
        /// Its buffers are released by the next TryDecodeNextFrame call.
        /// </summary>
        AVFrame* _lastFrame;

        /// <summary>
        /// Make dest a new reference to the frame returned by the last successful TryDecodeNextFrame, so that it stays valid after the next decode.
        /// dest is unreferenced first, the caller must unreference / free it (av_frame_free).
        /// </summary>
        /// <param name="dest">A frame allocated with av_frame_alloc</param>
        public void RefLastFrame(AVFrame* dest)
        {
            if (_lastFrame == null)
                throw new InvalidOperationException("No frame was decoded");
            ffmpeg.av_frame_unref(dest);
            ffmpeg.av_frame_ref(dest, _lastFrame).ThrowExceptionIfError();
        }

        public bool TryDecodeNextFrame(out AVFrame frame, out bool hardware)
        {
            _lastFrame = null;
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
                _lastFrame = _receivedFrame;
            }
            else
            {
                frame = *_pFrame;
                hardware = false;
                _lastFrame = _pFrame;
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
