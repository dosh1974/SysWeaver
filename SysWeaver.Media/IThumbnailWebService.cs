using System;
using System.Threading.Tasks;
using SysWeaver.Media;

namespace SysWeaver.MicroService
{
    public interface IThumbnailWebService : IDisposable
    {
        /// <summary>
        /// Get a png (screenshot) from an url
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>Response</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
        [WebApiCompression("")]
        Task<GetPngResponse> GetPng(GetPngRequest r);
    }

    public abstract class GetDataRequestBase
    {
        public override string ToString() => String.Concat(Width, 'x', Height, " from ", Url);

        /// <summary>
        /// The url to take a snap shot for
        /// </summary>
        public String Url;

        /// <summary>
        /// The width (can be modified if the page is "aware" and control is true)
        /// </summary>
        public int Width = 1920;

        /// <summary>
        /// The height (can be modified if the page is "aware" and control is true)
        /// </summary>
        public int Height = 1080;

    }



    public class GetPngRequest : GetDataRequestBase
    {
        public override string ToString() => String.Concat(Width, 'x', Height, " from ", Url);
        /// <summary>
        /// If true, a js object is registered so that the page can control dimensions and when to take the screenshot
        /// </summary>
        public bool Control;
    }


    public sealed class GetPngResponse
    {
        public Byte[] Png;
        public MediaInfo Info;
    }




}
