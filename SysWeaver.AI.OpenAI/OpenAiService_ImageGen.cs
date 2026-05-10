using CommunityToolkit.HighPerformance;
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
using SysWeaver.Media.Png;
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
            var model = p.Model ?? DefaultImageModel;
            var client = CreateImageClient(model);
            ImageGenerationOptions options = null;
            if (ModelOptions.TryGetValue(model, out var fn))
                options = fn(p);
            else
                options = new()
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
            var pngMem = bytes.ToMemory();
            List<PngChunk> chunks;
            using (var s = pngMem.AsStream())
                chunks = PngTools.ReadChunks(s).ToList();
            List<PngChunk> add = new List<PngChunk>(10)
            {
                PngTools.SetCreationTimeInfo(DateTime.UtcNow),
                PngTools.CreateInformationChunk(PngKeywords.Software, EnvInfo.AppDisplayName),
                PngTools.CreateInformationChunk(PngKeywords.Source, String.Concat(model, ' ', options.Quality, ' ', options.Style).Trim().Replace("  ", " ").Replace("  ", " ")),
                PngTools.CreateInformationChunk(PngKeywords.Description, p.Prompt),
            };
            var user = request?.Session?.Auth?.NickName;
            if (user != null)
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Author, user));
            if (!String.IsNullOrEmpty(p.Title))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Title, p.Title));
            chunks.InsertRange(1, add);
            pngMem = PngTools.MakePng(chunks);
            return pngMem;
        }




        static readonly IReadOnlyDictionary<String, Func<OpenAiImagePrompt, ImageGenerationOptions>> ModelOptions = new Dictionary<String, Func<OpenAiImagePrompt, ImageGenerationOptions>>(StringComparer.Ordinal)
        {
            { 
                "gpt-image-1", 
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                    }
            },
            { 
                "gpt-image-1.5",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                    }
            },
            {   
                "gpt-image-2",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2[(int)p.Size],
                    }
            },
            {
                "dall-e-2",
                    p => new ImageGenerationOptions
                    {
                        Size = GeneratedImageSize.W1024xH1024,
                        ResponseFormat = GeneratedImageFormat.Bytes,
                    }
            },
            {
                "dall-e-3",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                        Size = ImageSizes[(int)p.Size],
                        Style = p.Vivid ? GeneratedImageStyle.Vivid : GeneratedImageStyle.Natural,
                        ResponseFormat = GeneratedImageFormat.Bytes,
                    }
            },
        }.Freeze();

#pragma warning disable OPENAI001

        static readonly GeneratedImageSize[] ImageSizes = [
            GeneratedImageSize.Auto,
            GeneratedImageSize.W1024xH1024,
            GeneratedImageSize.W1024xH1792,
            GeneratedImageSize.W1792xH1024,
            GeneratedImageSize.W1024xH1792,
            GeneratedImageSize.W1792xH1024,
            ];


        static readonly GeneratedImageSize[] ImageSizes1 = [
            GeneratedImageSize.Auto,
            GeneratedImageSize.W1024xH1024,
            GeneratedImageSize.W1024xH1536,
            GeneratedImageSize.W1536xH1024,
            GeneratedImageSize.W1024xH1536,
            GeneratedImageSize.W1536xH1024,
            ];


        static readonly GeneratedImageSize[] ImageSizes2 = [
            GeneratedImageSize.Auto,
            new GeneratedImageSize(2048, 2048),
            new GeneratedImageSize(2480, 3508),
            new GeneratedImageSize(3508, 2480),
            new GeneratedImageSize(2160, 3840),
            new GeneratedImageSize(3840, 2160),
            ];


#pragma warning restore OPENAI001

    }
}
