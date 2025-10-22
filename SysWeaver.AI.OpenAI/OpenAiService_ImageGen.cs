using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using System;
using System.Buffers;
using System.ClientModel;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Serialization;
using TiktokenSharp;

namespace SysWeaver.AI
{

    public sealed partial class OpenAiService
    {


        /// <summary>
        /// The model used when supplying an empty model (can be configured)
        /// </summary>
        public readonly String DefaultImageModel;


        readonly AsyncLock ImageGenLock;


        /// <summary>
        /// Create a simple Image client
        /// </summary>
        /// <param name="model">The gpt model to use, ex:
        /// "dall-e-3"
        /// </param>
        /// <returns></returns>
        public ImageClient CreateImageClient(String model = null)
        {
            if (String.IsNullOrEmpty(model))
                model = DefaultImageModel;
            return new ImageClient(model, ApiKey, Options);
        }

        /// <summary>
        /// Generate an image
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi("debug/" + nameof(GenImage))]
        [WebApiAuth(Roles.Debug)]
        [WebApiRaw("image/png", true)]
        public async Task<ReadOnlyMemory<Byte>> GenImage(OpenAiImagePrompt p, HttpServerRequest request)
        {
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            var client = CreateImageClient();
            ImageGenerationOptions options = null;
            if (DefaultImageModel.FastEquals("gpt-image-1"))
            {
                options = new ImageGenerationOptions
                {
                    Quality = p.HighQuality ? "high" : "medium",
                    Size = ImageSizes[(int)p.Size],
                };
            }
            options = options ?? new()
            {
                Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                Size = ImageSizes[(int)p.Size],
                Style = p.Vivid ? GeneratedImageStyle.Vivid : GeneratedImageStyle.Natural,
                ResponseFormat = GeneratedImageFormat.Bytes,
            };
            GeneratedImage image;
            image = await client.GenerateImageAsync(p.Prompt, options).ConfigureAwait(false);
            /*

            int sleep = 1000;
            DateTime failAt = DateTime.UtcNow.AddMinutes(5);
            for (; ;)
            {
                try
                {
                    image = await client.GenerateImageAsync(p.Prompt, options).ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    var ee = ex as ClientResultException;
                    if (ee != null)
                    {
                        if (ee.Status == 429)
                        {
                            var next = DateTime.UtcNow.AddMilliseconds(sleep);
                            if (next > failAt)
                                throw;
                            await Task.Delay(sleep).ConfigureAwait(false);
                            sleep = (sleep + 1000) + (sleep >> 1);
                            continue;
                        }
                    }
                    throw;
                }
            }
            */
            BinaryData bytes = image.ImageBytes;
            return bytes.ToMemory();
        }

        static readonly GeneratedImageSize[] ImageSizes = [
            GeneratedImageSize.W1024xH1024,
            GeneratedImageSize.W1024xH1792,
            GeneratedImageSize.W1792xH1024,
            ];

    }
}
