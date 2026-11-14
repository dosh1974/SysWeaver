using System;
using System.Text;

namespace SysWeaver.AI
{
    /// <summary>
    /// The kind of task a workflow performs (determined when a workflow is analyzed, see <see cref="ComfyUiService.AddWorkflows(String)"/>)
    /// </summary>
    public enum ComfyTaskTypes
    {
        /// <summary>
        /// Create an image from a text prompt
        /// </summary>
        TextToImage,
        /// <summary>
        /// Create an image from one or more reference images and a text prompt (ex: image edit)
        /// </summary>
        ImagesToImage,
        /// <summary>
        /// Create a video from a text prompt
        /// </summary>
        TextToVideo,
        /// <summary>
        /// Create a video from one or more reference images (ex: the first frame) and a text prompt
        /// </summary>
        ImagesToVideo,
        /// <summary>
        /// Create music from a text prompt (the style) and optional lyrics
        /// </summary>
        TextToMusic,
        /// <summary>
        /// Create speech from a text (the text to speak), optionally a voice description and / or a reference voice
        /// </summary>
        TextToSpeech,
        /// <summary>
        /// Create a sound (effect) from a text prompt
        /// </summary>
        TextToSound,
    }

    /// <summary>
    /// The result of a task, the file created by the workflow (Name, Mime and Data) or an error.
    /// </summary>
    public sealed class ComfyResult : MemoryFile
    {
        /// <summary>
        /// The file, ex: "image_t2i_42.png" [123456] as image/png (workflow "image_t2i", seed 42).
        /// Or the error, ex: Error: No TextToImage workflow available!
        /// </summary>
        public override string ToString()
        {
            if (Error != null)
                return String.Concat("Error: ", Error);
            return String.Concat(base.ToString(), " (workflow ", Workflow.ToQuoted(), ", seed ", Seed.ToString(), ")");
        }

        /// <summary>
        /// Null if successful, else a description of the error (and Data is null)
        /// </summary>
        public String Error;

        /// <summary>
        /// The name of the workflow that was used, null if no workflow was found
        /// </summary>
        public String Workflow;

        /// <summary>
        /// The seed that was used (useful if a random seed was requested)
        /// </summary>
        public long Seed;

        /// <summary>
        /// True if successful (Error is null)
        /// </summary>
        public bool IsSuccess => Error == null;

        internal ComfyResult(String name, String mime, byte[] data, String workflow, long seed) : base(name, mime, data)
        {
            Workflow = workflow;
            Seed = seed;
        }

        internal ComfyResult(String error, String workflow, long seed) : base(null, null, (byte[])null)
        {
            Error = error ?? "Unknown error";
            Workflow = workflow;
            Seed = seed;
        }
    }

    /// <summary>
    /// Parameters common to all tasks.
    /// A workflow maps the parameters it supports, unsupported parameters are ignored (see <see cref="ComfyTaskWorkflow.Parameters"/>).
    /// </summary>
    public abstract class ComfyParams
    {

        public const String DefNegativeImagePrompt = "ugly, tiling, poorly drawn hands, poorly drawn feet, poorly drawn face, out of frame, extra limbs, disfigured, deformed, body out of frame, bad anatomy, watermark, signature, cut off, low contrast, underexposed, overexposed, bad art, beginner, amateur, distorted face, blurry, draft, grainy";

        /// <summary>
        /// A short description, ex: TextToImage "A cat on a roof", 1024x768, seed 42, workflow "image_t2i"
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(TaskType.ToString()).Append(' ');
            sb.Append(Prompt == null ? "(default prompt)" : Shorten(Prompt).ToQuoted());
            if (NegativePrompt != null)
                sb.Append(", negative ").Append(Shorten(NegativePrompt, 32).ToQuoted());
            Append(sb);
            if (this is IVisualComfyParams v)
            {
                if ((v.Width > 0) || (v.Height > 0))
                    sb.Append(", ").Append(v.Width).Append('x').Append(v.Height);
                if (v.Transparent)
                    sb.Append(", transparent");
            }
            if ((this is IDurationComfyParams d) && (d.Duration > 0))
                sb.Append(", ").Append(d.Duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(" s");
            if (this is IReferenceComfyParams r)
            {
                var c = r.Files?.Length ?? 0;
                if (c > 0)
                    sb.Append(", ").Append(c).Append(c == 1 ? " file" : " files");
            }
            sb.Append(Seed < 0 ? ", random seed" : (", seed " + Seed));
            if (EnhancePrompt)
                sb.Append(", enhance prompt");
            if (Workflow != null)
                sb.Append(", workflow ").Append(Workflow.ToQuoted());
            return sb.ToString();
        }

        /// <summary>
        /// Append the task specific values to the description (starting with ", ")
        /// </summary>
        protected virtual void Append(StringBuilder sb)
        {
        }

        internal static String Shorten(String s, int max = 64)
            => s.Length <= max ? s : String.Concat(s.AsSpan(0, max - 2), "..");

        /// <summary>
        /// The task that these parameters are for
        /// </summary>
        public abstract ComfyTaskTypes TaskType { get; }

        /// <summary>
        /// The name of the workflow to use (the file name without path and extension), null to use the default workflow for the task.
        /// </summary>
        [EditDefault(null)]
        public virtual String Workflow { get; set; }

        /// <summary>
        /// The prompt, what to create.
        /// For text to speech it's the text to speak, for music it's the style / description of the music.
        /// Null to use the workflow default.
        /// If the workflow requires a prefix or suffix (ex: a LoRA trigger, or a "{prompt}" placeholder in the default prompt) it's added, unless the prompt already has it.
        /// </summary>
        [EditMin(1)]
        [EditMultiline]
        public virtual String Prompt { get; set; }

        /// <summary>
        /// What to avoid, ex: "blurry, misspelled text, watermark".
        /// Null to use the workflow default, an empty string clears it.
        /// Only respected if the workflow has a negative prompt (a text that ends up as negative conditioning, a "negative_prompt" input or a node annotated "$NegativePrompt").
        /// </summary>
        [EditMultiline]
        [EditAllowNull]
        [EditDefault(null)]
        public virtual String NegativePrompt { get; set; } 

        /// <summary>
        /// If true, the prompt is enhanced (ex: expanded by an LLM) before it's used.
        /// Only respected if the workflow has a prompt enhance option (a boolean named or titled "enhance").
        /// Note that this value is always sent, so false disables prompt enhancement even if the workflow has it enabled by default.
        /// </summary>
        public bool EnhancePrompt { get; set; }

        /// <summary>
        /// The seed to use, the same seed and parameters gives the same result (ComfyUI returns a cached result).
        /// A negative value (the default) uses a random seed, the used seed is returned in <see cref="ComfyResult.Seed"/>.
        /// Only the lower 31 bits are used (so that the seed is valid for all nodes).
        /// Note: some nodes use a seed of 0 for a random seed.
        /// </summary>
        [EditDefault(-1)]
        [EditRange(-1, 2147483647)]
        [EditRandom]
        public long Seed { get; set; } = -1;

        /// <summary>
        /// Number of steps to use, this is very model dependent.
        /// 0 means use model default.
        /// </summary>
        [EditDefault(0)]
        [EditRange(0, 200)]
        public int Steps { get; set; }
    }

    /// <summary>
    /// Parameters for tasks that create an image or a video
    /// </summary>
    public interface IVisualComfyParams
    {
        /// <summary>
        /// The width in pixels, 0 to use the workflow default.
        /// Both Width and Height must be set to change the size.
        /// The size is rounded to a multiple that the model accepts (ex: 8 for images and 16 or 32 for videos).
        /// For workflows where the size follows the reference image (ex: image edit), setting the size may switch to an empty latent of that size (if the workflow has such an option).
        /// </summary>
        [EditRange(1, 8192)]
        [EditDefault(1280)]
        int Width { get; set; }

        /// <summary>
        /// The height in pixels, 0 to use the workflow default.
        /// Both Width and Height must be set to change the size.
        /// The size is rounded to a multiple that the model accepts (ex: 8 for images and 16 or 32 for videos).
        /// </summary>
        [EditRange(1, 8192)]
        [EditDefault(720)]
        int Height { get; set; }

        /// <summary>
        /// If true, the result should have a transparent background.
        /// Only respected if the workflow has a transparency option (a boolean named or titled "transparent").
        /// </summary>
        bool Transparent { get; set; }
    }

    /// <summary>
    /// Parameters for tasks that use reference files (images or audio)
    /// </summary>
    public interface IReferenceComfyParams
    {
        /// <summary>
        /// The reference files, in the order of the load nodes of the workflow (ordered by node id).
        /// Files with data are uploaded (named from a hash of the content), files without data (links) are downloaded by the server from the url in Name.
        /// If fewer files than load nodes are supplied, the remaining load nodes use the workflow default, extra files are ignored.
        /// </summary>
        MemoryFile[] Files { get; set; }
    }

    /// <summary>
    /// Parameters for tasks that create something with a duration (video, music, speech and sound)
    /// </summary>
    public interface IDurationComfyParams
    {
        /// <summary>
        /// The duration in seconds, 0 to use the workflow default.
        /// Depending on the workflow it's an exact duration, a maximum duration (ex: music) or rounded (ex: to whole seconds or a valid number of video frames).
        /// </summary>
        [EditRange(0.01, 60 * 60 * 24)]
        [EditDefault(5)]
        double Duration { get; set; }
    }

    /// <summary>
    /// Parameters for creating an image from a text prompt
    /// </summary>
    public sealed class TextToImageParams : ComfyParams, IVisualComfyParams
    {

        public TextToImageParams()
        {
            NegativePrompt = DefNegativeImagePrompt;
        }

        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.TextToImage;

        /// <inheritdoc/>
        public int Width { get; set; } = 1280;

        /// <inheritdoc/>
        public int Height { get; set; } = 720;

        /// <inheritdoc/>
        public bool Transparent { get; set; }



        [EditDefault(DefNegativeImagePrompt)]
        public override string NegativePrompt { get => base.NegativePrompt; set => base.NegativePrompt = value; }
    }

    /// <summary>
    /// Parameters for creating an image from one or more reference images and a text prompt (ex: image edit).
    /// At least one file is required.
    /// </summary>
    public sealed class ImagesToImageParams : ComfyParams, IVisualComfyParams, IReferenceComfyParams
    {
        public ImagesToImageParams()
        {
            NegativePrompt = DefNegativeImagePrompt;
        }

        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.ImagesToImage;

        /// <inheritdoc/>
        public int Width { get; set; } = 1280;

        /// <inheritdoc/>
        public int Height { get; set; } = 720;

        /// <inheritdoc/>
        public bool Transparent { get; set; }

        /// <summary>
        /// The reference images (at least one), in the order of the "Load Image" nodes of the workflow (ordered by node id).
        /// Files with data are uploaded, files without data (links) are downloaded by the server from the url in Name.
        /// </summary>
        public MemoryFile[] Files { get; set; }

        [EditDefault(DefNegativeImagePrompt)]
        public override string NegativePrompt { get => base.NegativePrompt; set => base.NegativePrompt = value; }

    }

    /// <summary>
    /// Parameters for creating a video from a text prompt
    /// </summary>
    public sealed class TextToVideoParams : ComfyParams, IVisualComfyParams, IDurationComfyParams
    {
        public TextToVideoParams()
        {
            NegativePrompt = DefNegativeImagePrompt;
        }


        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.TextToVideo;

        /// <inheritdoc/>
        public int Width { get; set; } = 1280;

        /// <inheritdoc/>
        public int Height { get; set; } = 720;

        /// <inheritdoc/>
        public bool Transparent { get; set; }

        /// <inheritdoc/>
        [EditDefault(15)]
        public double Duration { get; set; } = 15;

        [EditDefault(DefNegativeImagePrompt)]
        public override string NegativePrompt { get => base.NegativePrompt; set => base.NegativePrompt = value; }

    }

    /// <summary>
    /// Parameters for creating a video from one or more reference images (ex: the first frame) and a text prompt.
    /// At least one file is required.
    /// </summary>
    public sealed class ImagesToVideoParams : ComfyParams, IVisualComfyParams, IReferenceComfyParams, IDurationComfyParams
    {
        public ImagesToVideoParams()
        {
            NegativePrompt = DefNegativeImagePrompt;
        }

        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.ImagesToVideo;

        /// <inheritdoc/>
        public int Width { get; set; } = 1280;

        /// <inheritdoc/>
        public int Height { get; set; } = 720;

        /// <inheritdoc/>
        public bool Transparent { get; set; }

        /// <summary>
        /// The reference images (at least one, ex: the first frame), in the order of the "Load Image" nodes of the workflow (ordered by node id).
        /// Files with data are uploaded, files without data (links) are downloaded by the server from the url in Name.
        /// </summary>
        public MemoryFile[] Files { get; set; }

        /// <inheritdoc/>
        [EditDefault(15)]
        public double Duration { get; set; } = 15;

        [EditDefault(DefNegativeImagePrompt)]
        public override string NegativePrompt { get => base.NegativePrompt; set => base.NegativePrompt = value; }

    }

    /// <summary>
    /// Parameters for creating music.
    /// The <see cref="ComfyParams.Prompt"/> is the style / description of the music, ex: "Synthwave, retro 80s, driving bass, female vocals".
    /// </summary>
    public sealed class TextToMusicParams : ComfyParams, IDurationComfyParams
    {
        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.TextToMusic;

        /// <inheritdoc/>
        protected override void Append(StringBuilder sb)
        {
            if (Lyrics != null)
                sb.Append(", lyrics ").Append(Shorten(Lyrics, 32).ToQuoted());
        }

        /// <summary>
        /// The lyrics, null to use the workflow default.
        /// Most models use section markers, ex: "[verse]\nFirst line\n\n[chorus]\nChorus line".
        /// Only respected if the workflow has a lyrics input.
        /// </summary>
        [EditDefault(null)]
        [EditMultiline]
        public String Lyrics;

        /// <inheritdoc/>
        [EditDefault(300)]
        public double Duration { get; set; } = 300;

    }

    /// <summary>
    /// Parameters for creating speech.
    /// The <see cref="ComfyParams.Prompt"/> is the text to speak (for workflows with a single instruction input, it's the complete instruction).
    /// </summary>
    public sealed class TextToSpeechParams : ComfyParams, IReferenceComfyParams, IDurationComfyParams
    {
        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.TextToSpeech;

        /// <inheritdoc/>
        protected override void Append(StringBuilder sb)
        {
            if (Voice != null)
                sb.Append(", voice ").Append(Shorten(Voice, 32).ToQuoted());
        }

        /// <summary>
        /// A description of the voice, ex: "male, low pitch, british accent", null to use the workflow default.
        /// Only respected if the workflow has a voice description / instruction input.
        /// </summary>
        public String Voice;

        /// <summary>
        /// Optional reference voice(s) to clone, in the order of the "Load Audio" nodes of the workflow (ordered by node id).
        /// Only respected if the workflow is a voice clone workflow (has a "Load Audio" node).
        /// </summary>
        public MemoryFile[] Files { get; set; }

        /// <inheritdoc/>
        [EditDefault(20)]
        public double Duration { get; set; } = 20;
    }

    /// <summary>
    /// Parameters for creating a sound (effect) from a text prompt, ex: "A heavy wooden door creaking open".
    /// </summary>
    public sealed class TextToSoundParams : ComfyParams, IDurationComfyParams
    {
        /// <inheritdoc/>
        public override ComfyTaskTypes TaskType => ComfyTaskTypes.TextToSound;

        /// <inheritdoc/>
        [EditDefault(10)]
        public double Duration { get; set; } = 10;
    }

}
