using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SysWeaver.AI
{
    /// <summary>
    /// A ComfyUI workflow (API format) that has been analyzed for use with the task methods (TextToImage etc) of the <see cref="ComfyUiService"/>.
    /// The analysis works on the node graph, so the workflow doesn't need any annotations:
    ///   The output is the save / preview node (image, video or audio) that the workflow produces, nodes annotated with "#tag" are preferred.
    ///   The task type is determined from the output and the load nodes (images to image / video), audio is classified as speech, music or sound using the names of the file, nodes, titles and inputs.
    ///   Each parameter is mapped to the node inputs that holds the value (following links through primitive, constant, math, switch and reroute nodes), ex: the prompt is the text that ends up in the positive conditioning.
    /// Parameters can be mapped explicitly by annotating a node with a parameter name, ex: "$Prompt", "$Seed", "$Width", "$Height", "$Duration", "$Lyrics", "$Voice", "$EnhancePrompt" or "$Transparent".
    /// When a node is annotated, the heuristics are not used for that parameter.
    /// The workflow is uploaded to ComfyUI-Connect with annotations added for the mapped inputs (as "sw_[name]", and "sw_[name]_sized" if changing the size requires replacing links).
    /// </summary>
    public sealed partial class ComfyTaskWorkflow
    {
        /// <summary>
        /// Ex: "image_t2i" TextToImage: Prompt, Seed, Width, Height
        /// </summary>
        public override string ToString() => String.Concat(Name.ToQuoted(), " ", Type.ToString(), ": ", Parameters.Count == 0 ? "no parameters" : String.Join(", ", Parameters));

        /// <summary>
        /// The workflow name (the file name without path and extension), use as <see cref="ComfyParams.Workflow"/> to select the workflow
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The full file name, null if created from json
        /// </summary>
        public readonly String FileName;

        /// <summary>
        /// The task that this workflow performs
        /// </summary>
        public readonly ComfyTaskTypes Type;

        /// <summary>
        /// The parameters that this workflow supports (respects), ex: "Prompt", "Seed", "Width", "Height", "Duration", "EnhancePrompt", "Transparent", "Lyrics", "Voice" and "Files".
        /// Parameters not in this list are ignored.
        /// </summary>
        public readonly IReadOnlyList<String> Parameters;

        /// <summary>
        /// The number of reference files that the workflow uses (the number of load nodes)
        /// </summary>
        public readonly int FileCount;

        /// <summary>
        /// A description of how the parameters are mapped to the workflow (one line per mapping)
        /// </summary>
        public readonly IReadOnlyList<String> Mappings;

        /// <summary>
        /// The id of the output node
        /// </summary>
        public readonly String OutputNode;

        /// <summary>
        /// The model files used by the workflow (the values of the loader nodes, ex: "flux-2-klein-9b-nvfp4.safetensors"), used to identify the model
        /// </summary>
        public readonly IReadOnlyList<String> ModelFiles;

        /// <summary>
        /// The LoRA files used by the workflow
        /// </summary>
        public readonly IReadOnlyList<String> Loras;

        /// <summary>
        /// The node classes used by the workflow (that the output depends on)
        /// </summary>
        public readonly IReadOnlyList<String> NodeClasses;

        /// <summary>
        /// The default (positive) prompt of the workflow, null if the prompt isn't supported or is empty
        /// </summary>
        public readonly String DefaultPrompt;

        /// <summary>
        /// The default negative prompt of the workflow, null if the negative prompt isn't supported or is empty
        /// </summary>
        public readonly String DefaultNegativePrompt;

        /// <summary>
        /// Text that is added before the prompt (ex: a LoRA trigger), null if none
        /// </summary>
        public readonly String PromptPrefix;

        /// <summary>
        /// Text that is added after the prompt, null if none
        /// </summary>
        public readonly String PromptSuffix;

        /// <summary>
        /// Check if a parameter is supported (case insensitive)
        /// </summary>
        /// <param name="name">The parameter name, ex: "Width"</param>
        /// <returns>True if the parameter is respected by the workflow</returns>
        public bool Supports(String name) => Parameters.Contains(name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A multi line description of the workflow and the mappings
        /// </summary>
        /// <returns>The description</returns>
        public String Describe()
        {
            var sb = new StringBuilder();
            sb.Append(ToString());
            if (FileName != null)
                sb.Append(" (").Append(FileName).Append(')');
            foreach (var m in Mappings)
                sb.AppendLine().Append("  ").Append(m);
            return sb.ToString();
        }

        #region Internal

        /// <summary>
        /// A node input that a parameter is written to
        /// </summary>
        internal sealed class Target
        {
            public String NodeId;
            public String Input;
            /// <summary>
            /// The tag that is added to the node title (unique per target)
            /// </summary>
            public String Tag;
            /// <summary>
            /// True if the input is a link that is replaced by a value (only in the sized workflow)
            /// </summary>
            public bool Cut;
            /// <summary>
            /// True if the value should be rounded to an integer
            /// </summary>
            public bool IsInt;
            /// <summary>
            /// Sizes are rounded to this multiple
            /// </summary>
            public int Multiple;
            /// <summary>
            /// If greater than zero, the input is a number of frames (the duration is converted using this frame rate)
            /// </summary>
            public double Fps;
            /// <summary>
            /// Text added before the prompt (ex: a LoRA trigger), null if none
            /// </summary>
            public String Prefix;
            /// <summary>
            /// Text added after the prompt, null if none
            /// </summary>
            public String Suffix;
            /// <summary>
            /// The original (workflow) value of a steps target, or of a start / end step of a split sampler
            /// </summary>
            public double Orig;
            /// <summary>
            /// The original steps of the node of a start / end step target (a split sampler, ex: KSamplerAdvanced)
            /// </summary>
            public double OrigSteps;
            public String Description;

            /// <summary>
            /// Add the prefix and suffix to a prompt (unless already present)
            /// </summary>
            public String Affix(String prompt)
            {
                if (!String.IsNullOrEmpty(Prefix) && !prompt.TrimStart().StartsWith(Prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                    prompt = Prefix + prompt;
                if (!String.IsNullOrEmpty(Suffix) && !prompt.TrimEnd().EndsWith(Suffix.Trim(), StringComparison.OrdinalIgnoreCase))
                    prompt += Suffix;
                return prompt;
            }
        }

        internal sealed record Flip(String NodeId, String Input, bool Value);

        /// <summary>
        /// The parameters, key is the parameter name (ex: "Prompt"), "Files" are the references
        /// </summary>
        internal readonly IReadOnlyDictionary<String, IReadOnlyList<Target>> Targets;

        /// <summary>
        /// The tag of the output node
        /// </summary>
        internal readonly String OutputTag;

        /// <summary>
        /// The annotated workflow
        /// </summary>
        internal readonly ComfyUiWorkflow Workflow;

        /// <summary>
        /// The annotated workflow to use if a size is specified (with links replaced and switches flipped), null if not needed (the Workflow is used)
        /// </summary>
        internal readonly ComfyUiWorkflow SizedWorkflow;

        #endregion//Internal

        #region Load

        /// <summary>
        /// Load and analyze a workflow file
        /// </summary>
        /// <param name="fileName">The API format workflow file</param>
        /// <param name="type">Force a task type, null to determine it from the workflow</param>
        /// <returns>The analyzed workflow</returns>
        /// <exception cref="Exception">The workflow can't be used for any task (ex: an UI format workflow, or no image, video or audio output)</exception>
        public static ComfyTaskWorkflow Load(String fileName, ComfyTaskTypes? type = null)
        {
            fileName = Path.GetFullPath(fileName);
            return Analyze(Path.GetFileNameWithoutExtension(fileName), fileName, File.ReadAllBytes(fileName), type);
        }

        /// <summary>
        /// Analyze a workflow
        /// </summary>
        /// <param name="name">The name of the workflow</param>
        /// <param name="json">The API format workflow (utf8 json)</param>
        /// <param name="type">Force a task type, null to determine it from the workflow</param>
        /// <returns>The analyzed workflow</returns>
        /// <exception cref="Exception">The workflow can't be used for any task (ex: an UI format workflow, or no image, video or audio output)</exception>
        public static ComfyTaskWorkflow FromJson(String name, byte[] json, ComfyTaskTypes? type = null)
        {
            ArgumentNullException.ThrowIfNull(json);
            return Analyze(name, null, json, type);
        }

        /// <summary>
        /// Information about the models and prompts of a workflow (used to build the prompt enhancement instructions)
        /// </summary>
        sealed record ModelInfo(IReadOnlyList<String> ModelFiles, IReadOnlyList<String> Loras, IReadOnlyList<String> NodeClasses, String DefaultPrompt, String DefaultNegativePrompt);

        ComfyTaskWorkflow(String name, String fileName, ComfyTaskTypes type, String outputNode, String outputTag, Dictionary<String, IReadOnlyList<Target>> targets, ComfyUiWorkflow wf, ComfyUiWorkflow sized, List<String> mappings, ModelInfo info)
        {
            ModelFiles = info.ModelFiles;
            Loras = info.Loras;
            NodeClasses = info.NodeClasses;
            DefaultPrompt = info.DefaultPrompt;
            DefaultNegativePrompt = info.DefaultNegativePrompt;
            if (targets.TryGetValue(PPrompt, out var pt) && (pt.Count > 0))
            {
                PromptPrefix = String.IsNullOrEmpty(pt[0].Prefix) ? null : pt[0].Prefix;
                PromptSuffix = String.IsNullOrEmpty(pt[0].Suffix) ? null : pt[0].Suffix;
            }
            Name = name;
            FileName = fileName;
            Type = type;
            OutputNode = outputNode;
            OutputTag = outputTag;
            Targets = targets;
            Parameters = ParamOrder.Where(x => (x != PStepsSplit) && targets.ContainsKey(x)).ToArray();
            FileCount = targets.TryGetValue(PFiles, out var f) ? f.Count : 0;
            Workflow = wf;
            SizedWorkflow = sized;
            Mappings = mappings;
        }

        #endregion//Load

        #region Analysis

        internal const String PPrompt = "Prompt";
        internal const String PNegative = "NegativePrompt";
        internal const String PSeed = "Seed";
        internal const String PWidth = "Width";
        internal const String PHeight = "Height";
        internal const String PDuration = "Duration";
        internal const String PEnhance = "EnhancePrompt";
        internal const String PTransparent = "Transparent";
        internal const String PLyrics = "Lyrics";
        internal const String PVoice = "Voice";
        internal const String PFiles = "Files";
        internal const String PSteps = "Steps";
        /// <summary>
        /// The start / end steps of split samplers (scaled with the steps, not a parameter of its own)
        /// </summary>
        internal const String PStepsSplit = "StepsSplit";

        static readonly String[] ParamOrder = [PPrompt, PNegative, PFiles, PWidth, PHeight, PDuration, PLyrics, PVoice, PSeed, PSteps, PStepsSplit, PEnhance, PTransparent];

        /// <summary>
        /// Normalized tag names (lower case, letters and digits only) that explicitly maps a node to a parameter, ex: "$Prompt" or "$Positive_Prompt"
        /// </summary>
        static readonly IReadOnlyDictionary<String, String[]> ParamTags = new Dictionary<String, String[]>(StringComparer.Ordinal)
        {
            [PPrompt] = ["prompt", "positiveprompt", "positive"],
            [PNegative] = ["negativeprompt", "negative", "negprompt", "neg"],
            [PSeed] = ["seed", "noiseseed"],
            [PSteps] = ["steps", "numsteps", "samplingsteps", "inferencesteps"],
            [PWidth] = ["width"],
            [PHeight] = ["height"],
            [PDuration] = ["duration", "seconds", "maxduration"],
            [PEnhance] = ["enhanceprompt", "promptenhance", "enhance"],
            [PTransparent] = ["transparent", "transparency"],
            [PLyrics] = ["lyrics"],
            [PVoice] = ["voice", "voiceinstruct", "voicedescription"],
        };

        static readonly String[] MusicPromptTags = ["style", "musicstyle", "tags", "genre"];

        //  Input names that consumes a parameter value
        static readonly String[] PromptSinks = ["text", "prompt", "instruction", "positive_prompt", "positive", "caption", "text_g", "text_l", "t5xxl", "clip_l", "user_prompt", "positive_text", "text_positive"];
        static readonly String[] NegativeSinks = ["negative_prompt", "negative", "neg_prompt", "negative_text", "text_negative", "negative_caption"];
        static readonly String[] MusicPromptSinks = ["style", "tags", "genre", "description"];
        static readonly String[] LyricsSinks = ["lyrics"];
        static readonly String[] VoiceSinks = ["voice_instruct", "voice_description", "voice_instructions", "instruct", "speaker_description"];
        static readonly String[] SeedSinks = ["seed", "noise_seed"];
        static readonly String[] StepsSinks = ["steps", "num_steps", "num_inference_steps", "inference_steps", "sample_steps", "sampling_steps", "diffusion_steps", "denoise_steps"];
        static readonly String[] StepsSplitInputs = ["start_at_step", "end_at_step"];
        static readonly String[] SecondsSinks = ["duration", "seconds", "max_duration", "duration_seconds", "audio_duration", "length_seconds", "audio_length", "total_seconds"];
        static readonly String[] FrameSinks = ["length", "frames_number", "num_frames", "frame_count", "video_length", "video_frames", "frames"];
        static readonly String[] FpsSinks = ["fps", "frame_rate", "framerate"];
        static readonly String[] SizeMultipleInputs = ["multiple", "divisible_by", "round_to", "multiple_of"];

        //  Keywords used to classify audio workflows (matched against the file name, node classes, titles and input names)
        static readonly String[] SpeechWords = ["speech", "tts", "voice", "speak", "talk", "narrat", "kokoro", "vibevoice", "chatterbox", "f5"];
        static readonly String[] MusicWords = ["music", "song", "lyrics", "yue", "acestep", "ace_step", "melody", "diffrhythm", "suno", "singing"];
        static readonly String[] SoundWords = ["sfx", "sound", "foley", "effect", "mmaudio", "ambien"];

        [GeneratedRegex(@"([\$#!])([a-zA-Z0-9_-]+)(?:\(([^)]*)\))?")]
        private static partial Regex TagRegex();

        sealed class GNode
        {
            public String Id;
            public String Class;
            public String Title;
            public JsonObject Inputs;
            /// <summary>
            /// "$tag" annotations (tag name, input filter or null)
            /// </summary>
            public readonly List<(String Tag, String[] Filter)> InTags = new();
            /// <summary>
            /// "#tag" annotations
            /// </summary>
            public readonly List<String> OutTags = new();

            public override string ToString() => String.Concat(Id, " (", Class, String.IsNullOrEmpty(Title) ? "" : (" " + Title.ToQuoted()), ")");
        }

        /// <summary>
        /// A value found by tracing an input
        /// </summary>
        sealed record Found(String NodeId, String Input, bool Cut, String CutSource);

        enum Kind
        {
            String,
            Number,
            Bool,
        }

        sealed class Graph
        {
            public readonly Dictionary<String, GNode> Nodes = new(StringComparer.Ordinal);
            /// <summary>
            /// Key is the source node, value is the consumers (node, input)
            /// </summary>
            public readonly Dictionary<String, List<(String Node, String Input)>> Consumers = new(StringComparer.Ordinal);
            public readonly HashSet<String> Tags = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<String> Ancestors;
            public String Output;
        }

        static String Norm(String s) => WorkflowMapper.Norm(s ?? "");

        static bool IsLink(JsonNode v, out String src)
        {
            src = null;
            if ((v is not JsonArray a) || (a.Count != 2))
                return false;
            if ((a[0] is not JsonValue s) || !s.TryGetValue<String>(out src))
                return false;
            return a[1] is JsonValue;
        }

        static bool IsKind(JsonNode v, Kind k)
        {
            if (v is not JsonValue jv)
                return false;
            var vk = jv.GetValueKind();
            return k switch
            {
                Kind.String => vk == JsonValueKind.String,
                Kind.Number => vk == JsonValueKind.Number,
                _ => (vk == JsonValueKind.True) || (vk == JsonValueKind.False),
            };
        }

        static double GetNumber(JsonNode v)
            => (v is JsonValue jv) && (jv.GetValueKind() == JsonValueKind.Number) ? double.Parse(jv.ToJsonString(), CultureInfo.InvariantCulture) : 0;

        /// <summary>
        /// Nodes that holds a value (primitives, constants, literals)
        /// </summary>
        static bool IsValueNode(GNode n)
        {
            var c = n.Class;
            return c.StartsWith("Primitive", StringComparison.Ordinal)
                || c.Contains("Constant", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Literal", StringComparison.OrdinalIgnoreCase)
                || (c == "Reroute");
        }

        /// <summary>
        /// Nodes that passes on or computes a value from the inputs (math, switches)
        /// </summary>
        static bool IsTransformNode(GNode n)
        {
            var c = n.Class;
            return c.Contains("Math", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Expression", StringComparison.OrdinalIgnoreCase)
                || IsSwitch(n);
        }

        static bool IsSwitch(GNode n) => n.Class.Contains("Switch", StringComparison.OrdinalIgnoreCase);

        static bool IsLoader(GNode n) => n.Class.Contains("Load", StringComparison.Ordinal);

        static bool IsNegative(String s)
        {
            var n = Norm(s);
            return n.Contains("negative", StringComparison.Ordinal) || n.StartsWith("neg", StringComparison.Ordinal);
        }

        /// <summary>
        /// Compare node id's naturally ("2" before "10", "459:451" before "459:1000")
        /// </summary>
        static int CompareIds(String a, String b)
        {
            var pa = a.Split(':');
            var pb = b.Split(':');
            var l = Math.Min(pa.Length, pb.Length);
            for (int i = 0; i < l; ++i)
            {
                int c;
                if (long.TryParse(pa[i], out var na) && long.TryParse(pb[i], out var nb))
                    c = na.CompareTo(nb);
                else
                    c = String.CompareOrdinal(pa[i], pb[i]);
                if (c != 0)
                    return c;
            }
            return pa.Length.CompareTo(pb.Length);
        }

        static readonly Comparer<String> IdComparer = Comparer<String>.Create(CompareIds);

        static Graph Parse(JsonObject root, String source)
        {
            var g = new Graph();
            var tagRegex = TagRegex();
            foreach (var kv in root)
            {
                if ((kv.Value is not JsonObject n) || (n["class_type"] is not JsonValue ct) || !ct.TryGetValue<String>(out var cls))
                    throw new Exception("Workflow " + source.ToQuoted() + " node " + kv.Key.ToQuoted() + " is not a valid API format node!");
                var title = (n["_meta"] is JsonObject meta) && (meta["title"] is JsonValue tv) && tv.TryGetValue<String>(out var t) ? t : null;
                var node = new GNode
                {
                    Id = kv.Key,
                    Class = cls,
                    Title = title,
                    Inputs = n["inputs"] as JsonObject ?? new JsonObject(),
                };
                if (title != null)
                {
                    foreach (Match m in tagRegex.Matches(title))
                    {
                        var tag = m.Groups[2].Value;
                        g.Tags.Add(tag);
                        switch (m.Groups[1].Value[0])
                        {
                            case '$':
                                node.InTags.Add((tag, m.Groups[3].Success ? m.Groups[3].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null));
                                break;
                            case '#':
                                if (!m.Groups[3].Success)
                                    node.OutTags.Add(tag);
                                break;
                        }
                    }
                }
                g.Nodes.Add(kv.Key, node);
            }
            foreach (var n in g.Nodes.Values)
            {
                foreach (var i in n.Inputs)
                {
                    if (!IsLink(i.Value, out var src))
                        continue;
                    if (!g.Consumers.TryGetValue(src, out var l))
                        g.Consumers.Add(src, l = new List<(String, String)>());
                    l.Add((n.Id, i.Key));
                }
            }
            return g;
        }

        enum Media
        {
            Video = 0,
            Audio = 1,
            Image = 2,
            None = 3,
        }

        static Media GetOutputMedia(GNode n)
        {
            var c = n.Class;
            var isOutput = c.StartsWith("Save", StringComparison.Ordinal) || c.StartsWith("Preview", StringComparison.Ordinal) || c.Contains("VideoCombine", StringComparison.Ordinal);
            if (!isOutput && (n.OutTags.Count == 0))
                return Media.None;
            if (c.Contains("Video", StringComparison.OrdinalIgnoreCase) || c.Contains("WEBM", StringComparison.OrdinalIgnoreCase) || c.Contains("Animated", StringComparison.Ordinal) || c.Contains("Gif", StringComparison.Ordinal))
                return Media.Video;
            if (c.Contains("Audio", StringComparison.OrdinalIgnoreCase) || c.Contains("Mp3", StringComparison.OrdinalIgnoreCase) || c.Contains("Opus", StringComparison.OrdinalIgnoreCase) || c.Contains("Flac", StringComparison.OrdinalIgnoreCase))
                return Media.Audio;
            if (c.Contains("Image", StringComparison.OrdinalIgnoreCase))
                return Media.Image;
            return Media.None;
        }

        static HashSet<String> GetAncestors(Graph g, String id)
        {
            var res = new HashSet<String>(StringComparer.Ordinal);
            var stack = new Stack<String>();
            stack.Push(id);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (!res.Add(c) || !g.Nodes.TryGetValue(c, out var n))
                    continue;
                foreach (var i in n.Inputs)
                    if (IsLink(i.Value, out var src) && g.Nodes.ContainsKey(src))
                        stack.Push(src);
            }
            return res;
        }

        const int MaxDepth = 64;

        /// <summary>
        /// Find the value(s) of an input, following links through value and transform nodes.
        /// A link to any other node is returned as a cut (the link can be replaced by a value).
        /// </summary>
        static void Trace(Graph g, String nodeId, String input, Kind kind, List<Found> res, HashSet<String> visited, int depth = 0)
        {
            if (!g.Nodes.TryGetValue(nodeId, out var node) || !node.Inputs.TryGetPropertyValue(input, out var v))
                return;
            if (!IsLink(v, out var src))
            {
                if (IsKind(v, kind))
                    res.Add(new Found(nodeId, input, false, null));
                return;
            }
            if ((depth > MaxDepth) || !visited.Add(String.Concat(nodeId, ".", input)))
                return;
            if (!g.Nodes.TryGetValue(src, out var s))
                return;
            if (IsValueNode(s))
            {
                foreach (var i in s.Inputs)
                {
                    if (IsLink(i.Value, out _))
                        Trace(g, src, i.Key, kind, res, visited, depth + 1);
                    else if (IsKind(i.Value, kind))
                        res.Add(new Found(src, i.Key, false, null));
                }
                return;
            }
            if (IsTransformNode(s))
            {
                var isSwitch = IsSwitch(s);
                foreach (var i in s.Inputs)
                    if (IsLink(i.Value, out _) && !(isSwitch && (i.Key == "switch")))
                        Trace(g, src, i.Key, kind, res, visited, depth + 1);
                return;
            }
            if ((kind == Kind.String) && IsStringCombiner(s))
            {
                //  Text built from parts (ex: a fixed prefix concatenated with the prompt, or a template with the prompt inserted).
                //  The part that comes from a value node is the one to set, the other parts are kept (as a prefix / suffix etc).
                var sub = new List<Found>();
                var anyLinked = false;
                foreach (var i in s.Inputs)
                {
                    if (!IsLink(i.Value, out _) || IsCombinerOption(i.Key))
                        continue;
                    anyLinked = true;
                    Trace(g, src, i.Key, kind, sub, visited, depth + 1);
                }
                var good = sub.Where(x => !x.Cut).ToList();
                if (good.Count > 0)
                {
                    res.AddRange(good);
                    return;
                }
                //  The text comes from another node (ex: an LLM), can't be set
                if (anyLinked)
                {
                    res.Add(new Found(nodeId, input, true, src));
                    return;
                }
                //  Only literal parts: the inserted text of a replace node, else the longest part
                var lits = s.Inputs.Where(x => !IsCombinerOption(x.Key) && IsKind(x.Value, Kind.String)).ToList();
                var pick = s.Inputs.ContainsKey("find") ? lits.FirstOrDefault(x => x.Key.StartsWith("replace", StringComparison.Ordinal)) : default;
                if (pick.Key == null)
                    pick = lits.OrderByDescending(x => ((JsonValue)x.Value).GetValue<String>().Length).FirstOrDefault();
                if (pick.Key != null)
                    res.Add(new Found(src, pick.Key, false, null));
                return;
            }
            res.Add(new Found(nodeId, input, true, src));
        }

        /// <summary>
        /// Nodes that combines text (concatenate, join, replace, format etc)
        /// </summary>
        static bool IsStringCombiner(GNode n)
        {
            var c = n.Class;
            if (c.Contains("Conditioning", StringComparison.OrdinalIgnoreCase))
                return false;
            return c.Contains("Concat", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Join", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Combine", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Append", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Replace", StringComparison.OrdinalIgnoreCase)
                || c.Contains("Format", StringComparison.OrdinalIgnoreCase)
                || c.Contains("StringFunction", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Inputs of a text combiner that are options rather than text parts
        /// </summary>
        static bool IsCombinerOption(String input)
        {
            var n = Norm(input);
            return n.Contains("delim", StringComparison.Ordinal)
                || n.Contains("separat", StringComparison.Ordinal)
                || (n == "sep")
                || n.StartsWith("find", StringComparison.Ordinal)
                || (n == "action")
                || (n == "mode")
                || n.Contains("regex", StringComparison.Ordinal)
                || n.Contains("whitespace", StringComparison.Ordinal)
                || n.Contains("case", StringComparison.Ordinal);
        }

        static List<Found> TraceAll(Graph g, IEnumerable<(String Node, String Input)> sinks, Kind kind)
        {
            var res = new List<Found>();
            var visited = new HashSet<String>(StringComparer.Ordinal);
            foreach (var (n, i) in sinks)
                Trace(g, n, i, kind, res, visited);
            return res;
        }

        /// <summary>
        /// All inputs of the nodes that the output depends on (in node id order) with one of the names, excluding value and transform nodes
        /// </summary>
        static IEnumerable<(String Node, String Input)> Sinks(Graph g, String[] names, Func<GNode, String, bool> filter = null)
        {
            foreach (var id in g.Ancestors.Order(IdComparer))
            {
                var n = g.Nodes[id];
                if (IsValueNode(n) || IsTransformNode(n))
                    continue;
                foreach (var i in n.Inputs)
                    if (names.Contains(i.Key, StringComparer.Ordinal) && ((filter == null) || filter(n, i.Key)))
                        yield return (id, i.Key);
            }
        }

        /// <summary>
        /// Find the inputs of nodes annotated with a parameter tag, null if no node is annotated
        /// </summary>
        static List<Found> Explicit(Graph g, String[] tagNames, Kind kind, String[] preferredInputs)
        {
            List<Found> res = null;
            foreach (var id in g.Ancestors.Order(IdComparer))
            {
                var n = g.Nodes[id];
                if (IsLoader(n))
                    continue;
                foreach (var (tag, filter) in n.InTags)
                {
                    if (!tagNames.Contains(Norm(tag), StringComparer.Ordinal))
                        continue;
                    res ??= new List<Found>();
                    var names = (filter ?? n.Inputs.Select(x => x.Key)).Where(x => n.Inputs.TryGetPropertyValue(x, out var v) && (IsLink(v, out _) || IsKind(v, kind))).ToList();
                    var pref = names.Where(x => preferredInputs.Contains(x, StringComparer.Ordinal)).ToList();
                    if (pref.Count > 0)
                        names = pref;
                    else if (names.Count > 1)
                        names = names.Where(x => !IsLink(n.Inputs[x], out _)).ToList();
                    if (names.Count != 1)
                        continue;
                    Trace(g, id, names[0], kind, res, new HashSet<String>(StringComparer.Ordinal));
                }
            }
            return res;
        }

        /// <summary>
        /// Value nodes with a literal of some kind and a title that matches some words
        /// </summary>
        static IEnumerable<Found> TitledValues(Graph g, Kind kind, params String[] words)
        {
            foreach (var id in g.Ancestors.Order(IdComparer))
            {
                var n = g.Nodes[id];
                if (!IsValueNode(n))
                    continue;
                var t = Norm(n.Title);
                if (!words.Any(x => t.Contains(x, StringComparison.Ordinal)))
                    continue;
                foreach (var i in n.Inputs)
                    if (IsKind(i.Value, kind))
                        yield return new Found(id, i.Key, false, null);
            }
        }

        /// <summary>
        /// True if all consumers of a node uses it as a negative input (ex: a CLIPTextEncode for the negative prompt)
        /// </summary>
        static bool IsNegativeOnly(Graph g, String id)
        {
            if (!g.Consumers.TryGetValue(id, out var c) || (c.Count == 0))
                return false;
            return c.All(x => IsNegative(x.Input));
        }

        static List<Found> Distinct(IEnumerable<Found> f)
            => f.GroupBy(x => String.Concat(x.NodeId, ".", x.Input), StringComparer.Ordinal).Select(x => x.First()).ToList();

        /// <summary>
        /// Find the switches that must be flipped for the value of a node to reach the output
        /// </summary>
        static bool FindSwitches(Graph g, String id, List<(String Switch, bool Value)> req, HashSet<String> visited)
        {
            if (id == g.Output)
                return true;
            if (!visited.Add(id) || !g.Consumers.TryGetValue(id, out var cons))
                return false;
            //  Prefer paths without switches
            foreach (var (cid, input) in cons.OrderBy(x => IsSwitch(g.Nodes[x.Node]) ? 1 : 0).ThenBy(x => x.Node, IdComparer))
            {
                if (!g.Ancestors.Contains(cid))
                    continue;
                var cn = g.Nodes[cid];
                var sw = IsSwitch(cn) && ((input == "on_true") || (input == "on_false"));
                if (sw)
                    req.Add((cid, input == "on_true"));
                if (FindSwitches(g, cid, req, visited))
                    return true;
                if (sw)
                    req.RemoveAt(req.Count - 1);
            }
            return false;
        }

        static ComfyTaskWorkflow Analyze(String name, String fileName, byte[] json, ComfyTaskTypes? forceType)
        {
            var source = fileName ?? name;
            if (String.IsNullOrWhiteSpace(name) || (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("Invalid workflow name " + name.ToQuoted() + "!", nameof(name));
            if (json.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xef, 0xbb, 0xbf]))
                json = json[3..];
            var root = JsonNode.Parse(json, null, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
            if (root == null)
                throw new Exception("Workflow " + source.ToQuoted() + " must be a json object!");
            if (root["nodes"] is JsonArray)
                throw new Exception("Workflow " + source.ToQuoted() + " is in the UI format, save it using \"Workflow > Export (API)\"!");
            var g = Parse(root, source);

            #region Output

            var outputs = g.Nodes.Values
                .Select(x => (Node: x, Media: GetOutputMedia(x)))
                .Where(x => x.Media != Media.None)
                .OrderBy(x => x.Node.OutTags.Count > 0 ? 0 : 1)
                .ThenBy(x => (int)x.Media)
                .ThenBy(x => x.Node.Class.StartsWith("Preview", StringComparison.Ordinal) ? 1 : 0)
                .ThenBy(x => x.Node.Id, IdComparer)
                .ToList();
            if (outputs.Count == 0)
                throw new Exception("Workflow " + source.ToQuoted() + " has no image, video or audio output node!");
            var output = outputs[0].Node;
            var media = outputs[0].Media;
            g.Output = output.Id;
            g.Ancestors = GetAncestors(g, output.Id);

            #endregion//Output

            #region Type

            var imageLoads = g.Ancestors.Order(IdComparer).Select(x => g.Nodes[x]).Where(x => x.Class.Contains("LoadImage", StringComparison.Ordinal) && IsKind(x.Inputs["image"], Kind.String)).ToList();
            var audioLoads = g.Ancestors.Order(IdComparer).Select(x => g.Nodes[x]).Where(x => x.Class.Contains("LoadAudio", StringComparison.Ordinal) && IsKind(x.Inputs["audio"], Kind.String)).ToList();
            ComfyTaskTypes type;
            if (forceType != null)
                type = forceType.Value;
            else
            {
                switch (media)
                {
                    case Media.Image:
                        type = imageLoads.Count > 0 ? ComfyTaskTypes.ImagesToImage : ComfyTaskTypes.TextToImage;
                        break;
                    case Media.Video:
                        type = imageLoads.Count > 0 ? ComfyTaskTypes.ImagesToVideo : ComfyTaskTypes.TextToVideo;
                        break;
                    default:
                        type = ClassifyAudio(g, name);
                        break;
                }
            }
            var isVisual = (type <= ComfyTaskTypes.ImagesToVideo);
            var isVideo = (type == ComfyTaskTypes.TextToVideo) || (type == ComfyTaskTypes.ImagesToVideo);

            #endregion//Type

            #region Parameters

            var found = new Dictionary<String, List<Found>>(StringComparer.Ordinal);
            var notes = new Dictionary<String, String>(StringComparer.Ordinal);

            void Add(String param, IEnumerable<Found> f)
            {
                var l = Distinct(f);
                if (l.Count > 0)
                    found[param] = l;
            }

            //  Prompt
            {
                String[] tags = type == ComfyTaskTypes.TextToMusic ? [.. ParamTags[PPrompt], .. MusicPromptTags] : ParamTags[PPrompt];
                String[] sinks = type == ComfyTaskTypes.TextToMusic ? [.. PromptSinks, .. MusicPromptSinks] : PromptSinks;
                var f = Explicit(g, tags, Kind.String, sinks);
                if (f == null)
                {
                    f = TraceAll(g, Sinks(g, sinks, (n, i) => !IsNegativeOnly(g, n.Id) && !IsNegative(n.Title)), Kind.String);
                    f = f.Where(x => !x.Cut && !IsNegative(g.Nodes[x.NodeId].Title) && !IsNegative(x.Input)).ToList();
                }
                Add(PPrompt, f.Where(x => !x.Cut));
                //  Negative prompt: inputs named as a negative prompt, and the text inputs of nodes that are only used as negative conditioning (or titled negative)
                f = Explicit(g, ParamTags[PNegative], Kind.String, [.. NegativeSinks, .. sinks]);
                if (f == null)
                {
                    f = TraceAll(g, Sinks(g, NegativeSinks), Kind.String);
                    f.AddRange(TraceAll(g, Sinks(g, sinks, (n, i) => IsNegativeOnly(g, n.Id) || IsNegative(n.Title)), Kind.String));
                }
                Add(PNegative, f.Where(x => !x.Cut));
            }
            //  Seed
            {
                var f = Explicit(g, ParamTags[PSeed], Kind.Number, SeedSinks) ?? TraceAll(g, Sinks(g, SeedSinks), Kind.Number);
                Add(PSeed, f.Where(x => !x.Cut));
            }
            //  Steps, and the start / end steps of split samplers (ex: the two KSamplerAdvanced of Wan 2.2 high / low noise) that are scaled with them
            var splitSteps = new Dictionary<String, double>(StringComparer.Ordinal);
            {
                var sinks = Sinks(g, StepsSinks).ToList();
                var f = Explicit(g, ParamTags[PSteps], Kind.Number, StepsSinks) ?? TraceAll(g, sinks, Kind.Number);
                f = f.Where(x => !x.Cut && (GetNumber(g.Nodes[x.NodeId].Inputs[x.Input]) >= 1)).ToList();
                Add(PSteps, f);
                if (f.Count > 0)
                {
                    var split = new List<Found>();
                    foreach (var (sn, si) in sinks)
                    {
                        var steps = TraceAll(g, [(sn, si)], Kind.Number).FirstOrDefault(x => !x.Cut);
                        if (steps == null)
                            continue;
                        var stepsValue = GetNumber(g.Nodes[steps.NodeId].Inputs[steps.Input]);
                        if (stepsValue < 1)
                            continue;
                        var n = g.Nodes[sn];
                        foreach (var i in StepsSplitInputs)
                        {
                            if (!n.Inputs.TryGetPropertyValue(i, out var v) || !IsKind(v, Kind.Number))
                                continue;
                            split.Add(new Found(sn, i, false, null));
                            splitSteps[String.Concat(sn, ".", i)] = stepsValue;
                        }
                    }
                    Add(PStepsSplit, split);
                }
            }
            //  Enhance prompt and transparent
            {
                var f = Explicit(g, ParamTags[PEnhance], Kind.Bool, []);
                f ??= TraceAll(g, g.Ancestors.Order(IdComparer).SelectMany(x => g.Nodes[x].Inputs.Where(y => Norm(y.Key).Contains("enhance", StringComparison.Ordinal)).Select(y => (x, y.Key))), Kind.Bool)
                    .Concat(TitledValues(g, Kind.Bool, "enhance")).ToList();
                Add(PEnhance, f.Where(x => !x.Cut));
                if (isVisual)
                {
                    f = Explicit(g, ParamTags[PTransparent], Kind.Bool, []);
                    f ??= TraceAll(g, g.Ancestors.Order(IdComparer).SelectMany(x => g.Nodes[x].Inputs.Where(y => Norm(y.Key).Contains("transparen", StringComparison.Ordinal)).Select(y => (x, y.Key))), Kind.Bool)
                        .Concat(TitledValues(g, Kind.Bool, "transparen")).ToList();
                    Add(PTransparent, f.Where(x => !x.Cut));
                }
            }
            //  Width and height (links can be cut)
            if (isVisual)
            {
                foreach (var (p, input) in new[] { (PWidth, "width"), (PHeight, "height") })
                {
                    var f = Explicit(g, ParamTags[p], Kind.Number, [input]) ?? TraceAll(g, Sinks(g, [input]), Kind.Number);
                    Add(p, f);
                }
            }
            //  Duration
            var fps = new Dictionary<String, double>(StringComparer.Ordinal);
            if (type >= ComfyTaskTypes.TextToVideo)
            {
                var f = Explicit(g, ParamTags[PDuration], Kind.Number, SecondsSinks);
                if (f == null)
                {
                    f = TraceAll(g, Sinks(g, SecondsSinks), Kind.Number).Where(x => !x.Cut).ToList();
                    if (isVideo)
                    {
                        //  Frame counts, use the source that is titled as a duration (ex: a "Duration" primitive multiplied by the frame rate), else a literal frame count
                        double? frameRate = null;
                        foreach (var (sn, si) in Sinks(g, FrameSinks))
                        {
                            var ff = TraceAll(g, [(sn, si)], Kind.Number).Where(x => !x.Cut).ToList();
                            var secs = ff.Where(x => Norm(g.Nodes[x.NodeId].Title).Contains("duration", StringComparison.Ordinal) || Norm(g.Nodes[x.NodeId].Title).Contains("seconds", StringComparison.Ordinal)).ToList();
                            if (secs.Count > 0)
                            {
                                f.AddRange(secs);
                                continue;
                            }
                            foreach (var x in ff)
                            {
                                var t = Norm(g.Nodes[x.NodeId].Title);
                                if ((x.NodeId != sn) && !t.Contains("length", StringComparison.Ordinal) && !t.Contains("frames", StringComparison.Ordinal))
                                    continue;
                                frameRate ??= GetFrameRate(g);
                                fps[String.Concat(x.NodeId, ".", x.Input)] = frameRate.Value;
                                f.Add(x);
                            }
                        }
                    }
                }
                Add(PDuration, f.Where(x => !x.Cut));
            }
            //  Lyrics
            if (type == ComfyTaskTypes.TextToMusic)
            {
                var f = Explicit(g, ParamTags[PLyrics], Kind.String, LyricsSinks) ?? TraceAll(g, Sinks(g, LyricsSinks), Kind.String);
                Add(PLyrics, f.Where(x => !x.Cut));
            }
            //  Voice
            if (type == ComfyTaskTypes.TextToSpeech)
            {
                var f = Explicit(g, ParamTags[PVoice], Kind.String, VoiceSinks) ?? TraceAll(g, Sinks(g, VoiceSinks), Kind.String);
                Add(PVoice, f.Where(x => !x.Cut));
            }
            //  Files
            var loads = type switch
            {
                ComfyTaskTypes.ImagesToImage or ComfyTaskTypes.ImagesToVideo => imageLoads,
                ComfyTaskTypes.TextToSpeech => audioLoads,
                _ => [],
            };
            if (loads.Count > 0)
                found[PFiles] = loads.Select(x => new Found(x.Id, x.Class.Contains("Audio", StringComparison.Ordinal) ? "audio" : "image", false, null)).ToList();

            //  A node input can only be used by one parameter (the first in this order)
            var usedInputs = new HashSet<String>(StringComparer.Ordinal);
            foreach (var p in ParamOrder)
            {
                if (!found.TryGetValue(p, out var l))
                    continue;
                l = l.Where(x => usedInputs.Add(String.Concat(x.NodeId, ".", x.Input))).ToList();
                if (l.Count == 0)
                    found.Remove(p);
                else
                    found[p] = l;
            }

            //  Width and height must both be supported
            if (found.ContainsKey(PWidth) != found.ContainsKey(PHeight))
            {
                found.Remove(PWidth);
                found.Remove(PHeight);
            }

            #endregion//Parameters

            #region Targets

            var usedTags = new HashSet<String>(g.Tags, StringComparer.OrdinalIgnoreCase);
            String NewTag(String baseName)
            {
                var t = baseName;
                for (int i = 2; !usedTags.Add(t); ++i)
                    t = String.Concat(baseName, "_", i.ToString());
                return t;
            }
            var targets = new Dictionary<String, IReadOnlyList<Target>>(StringComparer.Ordinal);
            var mappings = new List<String>
            {
                String.Concat("Output => ", output.ToString()),
            };
            var defaultMultiple = isVideo ? 16 : 8;
            foreach (var p in ParamOrder)
            {
                if (!found.TryGetValue(p, out var l))
                    continue;
                var tl = new List<Target>();
                int index = 0;
                foreach (var x in l)
                {
                    ++index;
                    var n = g.Nodes[x.NodeId];
                    var t = new Target
                    {
                        NodeId = x.NodeId,
                        Input = x.Input,
                        Tag = NewTag(String.Concat("sw", p, index.ToString())),
                        Cut = x.Cut,
                        //  ComfyUI converts a float to an int for INT inputs (truncating), so only round when the node is known to take an int
                        IsInt = (p == PWidth) || (p == PHeight) || (p == PSeed) || (p == PSteps) || (p == PStepsSplit) || ((p == PDuration) && n.Class.Contains("Int", StringComparison.OrdinalIgnoreCase)),
                    };
                    var desc = String.Concat(p, (l.Count > 1) && (p == PFiles) ? ("[" + (index - 1) + "]") : "", " => ", x.Input.ToQuoted(), " of node ", n.ToString());
                    if ((p == PSteps) || (p == PStepsSplit))
                    {
                        t.Orig = GetNumber(n.Inputs[x.Input]);
                        desc = String.Concat(desc, ", workflow value ", t.Orig.ToString(CultureInfo.InvariantCulture));
                        if (p == PStepsSplit)
                        {
                            t.OrigSteps = splitSteps[String.Concat(x.NodeId, ".", x.Input)];
                            desc = String.Concat(desc, " of ", t.OrigSteps.ToString(CultureInfo.InvariantCulture), " steps (scaled with the steps)");
                        }
                    }
                    if ((p == PWidth) || (p == PHeight))
                    {
                        t.Multiple = defaultMultiple;
                        if (x.Cut)
                        {
                            var src = g.Nodes[x.CutSource];
                            foreach (var mi in SizeMultipleInputs)
                                if (src.Inputs.TryGetPropertyValue(mi, out var mv) && IsKind(mv, Kind.Number) && (GetNumber(mv) >= 1))
                                {
                                    t.Multiple = (int)GetNumber(mv);
                                    break;
                                }
                            desc = String.Concat(desc, ", replacing the link from ", src.ToString());
                        }
                        desc = String.Concat(desc, ", rounded to a multiple of ", t.Multiple.ToString());
                    }
                    if (p == PPrompt)
                    {
                        var v = n.Inputs[x.Input];
                        var (prefix, suffix, reason) = DetectAffixes(g, IsKind(v, Kind.String) ? v.GetValue<String>() : null);
                        t.Prefix = prefix;
                        t.Suffix = suffix;
                        if (!String.IsNullOrEmpty(prefix))
                            desc = String.Concat(desc, ", prefixed with ", prefix.ToQuoted());
                        if (!String.IsNullOrEmpty(suffix))
                            desc = String.Concat(desc, ", suffixed with ", suffix.ToQuoted());
                        if (reason != null)
                            desc = String.Concat(desc, " (", reason, ")");
                    }
                    if ((p == PDuration) && fps.TryGetValue(String.Concat(x.NodeId, ".", x.Input), out var rate))
                    {
                        t.Fps = rate;
                        desc = String.Concat(desc, ", as frames at ", rate.ToString(CultureInfo.InvariantCulture), " fps");
                    }
                    t.Description = desc;
                    mappings.Add(desc);
                    tl.Add(t);
                }
                targets[p] = tl;
            }

            //  Switches that must be flipped for the size to be used
            var flips = new List<Flip>();
            if (targets.TryGetValue(PWidth, out var wt))
            {
                foreach (var t in wt.Concat(targets[PHeight]))
                {
                    var req = new List<(String Switch, bool Value)>();
                    if (!FindSwitches(g, t.NodeId, req, new HashSet<String>(StringComparer.Ordinal)))
                        continue;
                    foreach (var (sw, value) in req)
                    {
                        var ff = new List<Found>();
                        Trace(g, sw, "switch", Kind.Bool, ff, new HashSet<String>(StringComparer.Ordinal));
                        foreach (var x in ff.Where(x => !x.Cut))
                        {
                            var v = g.Nodes[x.NodeId].Inputs[x.Input];
                            if ((v.GetValueKind() == JsonValueKind.True) == value)
                                continue;
                            var flip = new Flip(x.NodeId, x.Input, value);
                            if (flips.Contains(flip))
                                continue;
                            flips.Add(flip);
                            mappings.Add(String.Concat("Width / Height sets ", x.Input.ToQuoted(), " of node ", g.Nodes[x.NodeId].ToString(), " to ", value ? "true" : "false"));
                        }
                    }
                }
            }

            #endregion//Targets

            #region Annotate

            var outputTag = output.OutTags.FirstOrDefault() ?? NewTag("swOutput");
            byte[] Annotate(bool sized)
            {
                var r = (JsonObject)root.DeepClone();
                void Title(String id, String add)
                {
                    var n = (JsonObject)r[id];
                    if (n["_meta"] is not JsonObject meta)
                        n["_meta"] = meta = new JsonObject();
                    var title = (meta["title"] is JsonValue tv) && tv.TryGetValue<String>(out var t) ? t : "";
                    meta["title"] = String.Concat(title, title.Length > 0 ? " " : "", add);
                }
                if (output.OutTags.Count == 0)
                    Title(output.Id, "#" + outputTag);
                foreach (var t in targets.Values.SelectMany(x => x))
                {
                    if (t.Cut)
                    {
                        if (!sized)
                            continue;
                        ((JsonObject)r[t.NodeId]["inputs"])[t.Input] = JsonValue.Create(t.Multiple * 64);
                    }
                    Title(t.NodeId, String.Concat("$", t.Tag, "(", t.Input, ")"));
                }
                if (sized)
                    foreach (var f in flips)
                        ((JsonObject)r[f.NodeId]["inputs"])[f.Input] = JsonValue.Create(f.Value);
                return JsonSerializer.SerializeToUtf8Bytes(r);
            }
            var serverName = "sw_" + name;
            var wf = ComfyUiWorkflow.FromJson(serverName, Annotate(false));
            ComfyUiWorkflow sizedWf = null;
            var needSized = (flips.Count > 0) || targets.Values.SelectMany(x => x).Any(x => x.Cut);
            if (needSized)
                sizedWf = ComfyUiWorkflow.FromJson(serverName + "_sized", Annotate(true));

            #endregion//Annotate

            return new ComfyTaskWorkflow(name, fileName, type, output.Id, outputTag, targets, wf, sizedWf, mappings, GetModelInfo(g, targets));
        }

        [GeneratedRegex(@"(^|_)name\d*$|^ckpt|^model$|^model_path$|^repo_id$|^model_id$|^checkpoint$")]
        private static partial Regex ModelInputRegex();

        /// <summary>
        /// Get the models, LoRA's, node classes and default prompts used by a workflow
        /// </summary>
        static ModelInfo GetModelInfo(Graph g, Dictionary<String, IReadOnlyList<Target>> targets)
        {
            var models = new List<String>();
            var loras = new List<String>();
            var classes = new SortedSet<String>(StringComparer.Ordinal);
            var modelInput = ModelInputRegex();
            foreach (var id in g.Ancestors.Order(IdComparer))
            {
                var n = g.Nodes[id];
                classes.Add(n.Class);
                var isLora = n.Class.Contains("Lora", StringComparison.OrdinalIgnoreCase);
                foreach (var i in n.Inputs)
                {
                    if (!IsKind(i.Value, Kind.String) || !modelInput.IsMatch(i.Key))
                        continue;
                    var v = i.Value.GetValue<String>();
                    //  Ex: "filename_prefix", "sampler_name" and "scheduler" aren't models
                    if (String.IsNullOrWhiteSpace(v) || i.Key.StartsWith("sampler", StringComparison.Ordinal) || i.Key.StartsWith("filename", StringComparison.Ordinal) || i.Key.StartsWith("scheduler", StringComparison.Ordinal))
                        continue;
                    if (isLora || i.Key.StartsWith("lora", StringComparison.Ordinal))
                    {
                        if (!loras.Contains(v))
                            loras.Add(v);
                    }
                    else if (!models.Contains(v))
                        models.Add(v);
                }
            }
            String Default(String p)
            {
                if (!targets.TryGetValue(p, out var l) || (l.Count == 0))
                    return null;
                var t = l[0];
                var v = g.Nodes[t.NodeId].Inputs[t.Input];
                var s = IsKind(v, Kind.String) ? v.GetValue<String>().Trim() : null;
                return String.IsNullOrEmpty(s) ? null : s;
            }
            return new ModelInfo(models, loras, classes.ToArray(), Default(PPrompt), Default(PNegative));
        }

        /// <summary>
        /// The placeholder for the prompt in the default prompt text of a workflow, ex: "photo of {prompt}, 35mm"
        /// </summary>
        public const String PromptPlaceholder = "{prompt}";

        [GeneratedRegex(@"^\s*([\p{L}\p{N}][\p{L}\p{N} _'\-\.]{0,39}?)\s*:[ \t]*(?=\S)")]
        private static partial Regex TriggerRegex();

        /// <summary>
        /// Detect text that must be kept before / after the prompt, from the default prompt of the workflow:
        ///   1. A "{prompt}" placeholder, the text before is the prefix and the text after is the suffix.
        ///   2. If the workflow uses a LoRA: a short trigger phrase followed by a ':' at the start (ex: "ohwx style: a cat"), it's the prefix.
        /// Parts of the prompt that are separate text nodes combined with the prompt (concatenate, replace etc) are kept by the workflow itself.
        /// </summary>
        static (String Prefix, String Suffix, String Reason) DetectAffixes(Graph g, String text)
        {
            if (String.IsNullOrEmpty(text))
                return default;
            var i = text.IndexOf(PromptPlaceholder, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                var suffix = text.Substring(i + PromptPlaceholder.Length);
                if (suffix.Contains(PromptPlaceholder, StringComparison.OrdinalIgnoreCase))
                    return default;
                return (text.Substring(0, i), suffix, "placeholder in the default prompt");
            }
            var lora = g.Ancestors.Select(x => g.Nodes[x]).FirstOrDefault(x => x.Class.Contains("Lora", StringComparison.OrdinalIgnoreCase));
            if (lora == null)
                return default;
            var m = TriggerRegex().Match(text);
            if (!m.Success)
                return default;
            var trigger = m.Groups[1].Value;
            //  A trigger is a short phrase (not a sentence or a markdown header / label like "Task")
            var words = trigger.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if ((words.Length > 5) || (trigger.Length < 2))
                return default;
            if (CommonLabels.Contains(Norm(trigger)))
                return default;
            return (m.Value, null, "trigger of " + lora.ToString());
        }

        /// <summary>
        /// Labels that are commonly used at the start of a prompt but aren't LoRA triggers
        /// </summary>
        static readonly HashSet<String> CommonLabels = new(StringComparer.Ordinal)
        {
            "task", "prompt", "description", "style", "scene", "subject", "note", "notes", "instruction", "instructions", "image", "video", "caption", "camera", "audio", "sound", "music", "lyrics", "text", "positive", "negative", "input", "output", "example", "context", "shot", "setting", "mood", "lighting", "action", "character", "characters", "background", "foreground",
        };

        /// <summary>
        /// Get the frame rate used by a video workflow (24 if not found)
        /// </summary>
        static double GetFrameRate(Graph g)
        {
            foreach (var x in TraceAll(g, Sinks(g, FpsSinks), Kind.Number))
            {
                if (x.Cut)
                    continue;
                var v = GetNumber(g.Nodes[x.NodeId].Inputs[x.Input]);
                if (v > 0)
                    return v;
            }
            return 24;
        }

        /// <summary>
        /// Classify an audio workflow as speech, music or sound (the default)
        /// </summary>
        static ComfyTaskTypes ClassifyAudio(Graph g, String name)
        {
            var words = new List<String> { name.ToLowerInvariant() };
            foreach (var id in g.Ancestors)
            {
                var n = g.Nodes[id];
                words.Add(n.Class.ToLowerInvariant());
                if (n.Title != null)
                    words.Add(n.Title.ToLowerInvariant());
                foreach (var i in n.Inputs)
                {
                    words.Add(i.Key.ToLowerInvariant());
                    //  Model files (ex: "ace_step_v1_3.5b.safetensors")
                    if (IsKind(i.Value, Kind.String) && ModelInputRegex().IsMatch(i.Key))
                        words.Add(i.Value.GetValue<String>().ToLowerInvariant());
                }
            }
            int Score(String[] keys) => words.Sum(w => keys.Count(k => w.Contains(k, StringComparison.Ordinal)));
            var speech = Score(SpeechWords);
            var music = Score(MusicWords);
            var sound = Score(SoundWords);
            if ((speech > music) && (speech >= sound) && (speech > 0))
                return ComfyTaskTypes.TextToSpeech;
            if ((music >= sound) && (music > 0))
                return ComfyTaskTypes.TextToMusic;
            return ComfyTaskTypes.TextToSound;
        }

        #endregion//Analysis
    }
}
