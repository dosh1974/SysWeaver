using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
                     
    public interface IThumbnailRemoteService : IDisposable
    {
        Task<ScreenshotImageResponse> GetMediaImage(GetMediaImageRequest r);
        Task<Byte[]> MediaImage(GetMediaImageRequest r);

        Task<ScreenshotImageResponse> GetMediaVideo(GetMediaVideoRequest r);
        Task<Byte[]> MediaVideo(GetMediaVideoRequest r);


        Task<ScreenshotImageResponse> GetMediaEffect(GetMediaEffectRequest r);
        Task<Byte[]> MediaEffect(GetMediaEffectRequest r);

        Task<ScreenshotImageResponse> GetMediaYouTube(GetMediaYouTubeRequest r);
        Task<Byte[]> MediaYouTube(GetMediaYouTubeRequest r);
        Task<Byte[]> GetGoogleMapPng(GetGoogleMapRequest r);
        Task<Byte[]> GetGoogleMapJpg(GetGoogleMapJpegRequest r);

        Task<ScreenshotImageResponse> GetImage(ScreenshotImageRequest r);
        Task<Byte[]> WebScreenshotPng(ScreenshotPngRequest r);
        Task<Byte[]> WebScreenshotJpg(ScreenshotJpegRequest r);
    }


}
