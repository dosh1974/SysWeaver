using FFmpeg.AutoGen.Abstractions;
using System;
using System.Runtime.InteropServices;

namespace SysWeaver.Media
{
    sealed unsafe class VideoFrameConverter : IDisposable
    {
        readonly IntPtr _convertedFrameBufferPtr;
        readonly byte_ptr4 _dstData;
        readonly int4 _dstLinesize;
        readonly SwsContext* _pConvertContext;
        public readonly Byte[] DestBuffer;
        public readonly int DestWidth;
        public readonly int DestHeight;
        readonly GCHandle Handle;
        public VideoFrameConverter( AVPixelFormat sourcePixelFormat, int sourceWidth, int sourceHeight,
                                    AVPixelFormat destinationPixelFormat, int destWidth = 0, int destHeight = 0)
        {
            if (destWidth <= 0)
                destWidth = sourceWidth;
            if (destHeight <= 0)
                destHeight = sourceHeight;
            DestWidth = destWidth;
            DestHeight = destHeight;
            _pConvertContext = ffmpeg.sws_getContext(sourceWidth, sourceHeight, sourcePixelFormat, destWidth, destHeight, destinationPixelFormat, ffmpeg.SWS_FAST_BILINEAR, null, null, null);
            if (_pConvertContext == null) 
                throw new ApplicationException("Could not initialize the conversion context.");

            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat, destWidth, destHeight, 1);
            
            DestBuffer = GC.AllocateUninitializedArray<Byte>(convertedFrameBufferSize);
            var h = GCHandle.Alloc(DestBuffer, GCHandleType.Pinned);
            Handle = h;

            _convertedFrameBufferPtr = h.AddrOfPinnedObject();

            _dstData = new byte_ptr4();
            _dstLinesize = new int4();

            ffmpeg.av_image_fill_arrays(ref _dstData, ref _dstLinesize, (byte*)_convertedFrameBufferPtr, destinationPixelFormat, destWidth, destHeight, 1);
        }

        

        public void Dispose()
        {
            Handle.Free();
            ffmpeg.sws_freeContext(_pConvertContext);
        }

        public AVFrame Convert(AVFrame sourceFrame)
        {
            ffmpeg.sws_scale(_pConvertContext, sourceFrame.data, sourceFrame.linesize, 0, sourceFrame.height, _dstData, _dstLinesize);

            var data = new byte_ptr8();
            data.UpdateFrom(_dstData);
            var linesize = new int8();
            linesize.UpdateFrom(_dstLinesize);

            return new AVFrame
            {
                data = data,
                linesize = linesize,
                width = DestWidth,
                height = DestHeight
            };
        }
    }
}
