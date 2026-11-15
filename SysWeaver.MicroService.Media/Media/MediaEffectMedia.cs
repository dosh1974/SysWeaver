using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// The media line of an effect (Effect Weaver's media effects): <c>//  media: &lt;kind&gt; &lt;refs&gt;</c> in the shader, kind "image" (a picture),
    /// "audio" (a sound: it plays with the effect and is its audio texture) or "collage" (several pictures). The media player reads it when
    /// the effect's settings name no Texture; references that aren't urls (ex: Effect Weaver's "stock:file", "my:file") and a line without
    /// references (the defaults of the kind) are resolved by the server (<see cref="MediaService.ResolveEffectMedia"/>).
    /// </summary>
    public sealed class MediaEffectMediaRequest
    {
        /// <summary>image | audio | collage</summary>
        public String Kind;

        /// <summary>The references of the line (urls are used by the player as they are, only the others need the server), empty = the defaults</summary>
        public String[] Refs;
    }

    /// <summary>
    /// The urls of an effect's media (see <see cref="MediaEffectMediaRequest"/>): absolute, or relative to the mediaView folder
    /// ("../editor-media/stock/a.png"); empty when no resolver knows them
    /// </summary>
    public sealed class MediaEffectMediaResponse
    {
        public String[] Urls = [];
    }

    /// <summary>
    /// A service that knows the media references of effects (registered in the service manager, used by <see cref="MediaService.ResolveEffectMedia"/>,
    /// ex: Effect Weaver's editor service: its stock media and the user's own media)
    /// </summary>
    public interface IEffectMediaResolver
    {
        /// <summary>
        /// The urls of the media of an effect's media line, for the user of the request: absolute, or relative to the mediaView folder.
        /// Null (or empty) when the references aren't this resolver's.
        /// </summary>
        /// <param name="kind">image | audio | collage</param>
        /// <param name="refs">The references, empty = the defaults of the kind</param>
        /// <param name="context">The request (its session: the user's own media)</param>
        Task<String[]> ResolveEffectMedia(String kind, String[] refs, HttpServerRequest context);
    }
}
