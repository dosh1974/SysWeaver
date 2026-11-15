using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.AI
{
    public sealed partial class ComfyTaskWorkflow
    {
        #region Prompt enhancement

        /// <summary>
        /// The name of the model (family) that the workflow uses, ex: "FLUX.2" or "LTX-2", null if not recognized.
        /// Determined from the model files, node classes and the workflow name.
        /// </summary>
        public String ModelName => GetGuide()?.Name;

        /// <summary>
        /// Get a system prompt for an LLM that rewrites a user request (and any media details, such as the size or duration) into a prompt that is optimized for this workflow.
        /// The instructions include the model (family) and how to best prompt it, the task, the prefix / suffix to apply, how the negative prompt is handled,
        /// an example prompt (the default prompt of the workflow) and rules to only output the prompt.
        /// Intended flow: prompt = LLM(system: GetPromptEnhancement(), user: request + media details), then run the task with the prompt.
        /// </summary>
        /// <returns>The system prompt</returns>
        public String GetPromptEnhancement()
        {
            var e = PromptEnhancement;
            if (e != null)
                return e;
            e = BuildPromptEnhancement();
            PromptEnhancement = e;
            return e;
        }

        String PromptEnhancement;

        /// <summary>
        /// How negative prompts are handled by a model
        /// </summary>
        enum Negatives
        {
            /// <summary>
            /// Not known
            /// </summary>
            Unknown,
            /// <summary>
            /// Negative prompts are used and effective (real CFG)
            /// </summary>
            Used,
            /// <summary>
            /// Negative prompts have no effect (guidance distilled or CFG 1)
            /// </summary>
            Ignored,
        }

        /// <summary>
        /// Prompting guidance for a model (family)
        /// </summary>
        /// <param name="Name">The name of the model (family)</param>
        /// <param name="Match">Matched against the workflow name, the model files and the node classes (one per line, lower case)</param>
        /// <param name="Types">The task types this applies to, null for all</param>
        /// <param name="Negative">How negative prompts are handled</param>
        /// <param name="Guidance">The guidance (one instruction per line)</param>
        /// <param name="TransparentPrefix">Text added before the prompt when a transparent result is requested (the model makes a transparent image from the prompt), null if none</param>
        /// <param name="TransparentSuffix">Text added after the prompt when a transparent result is requested, null if none</param>
        sealed record Guide(String Name, Regex Match, ComfyTaskTypes[] Types, Negatives Negative, String Guidance, String TransparentPrefix = null, String TransparentSuffix = null)
        {
            /// <summary>
            /// True if the model makes a transparent result from the prompt (it has a transparent prefix and / or suffix)
            /// </summary>
            public bool PromptTransparency => (TransparentPrefix != null) || (TransparentSuffix != null);
        }

        static Guide G(String name, String pattern, Negatives negative, String guidance, params ComfyTaskTypes[] types)
            => new Guide(name, new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant), types.Length == 0 ? null : types, negative, guidance);

        /// <summary>
        /// A model that makes a transparent result when the prompt gets a prefix and / or suffix (see <see cref="Guide.TransparentPrefix"/>)
        /// </summary>
        static Guide GT(String name, String pattern, Negatives negative, String guidance, String transparentPrefix, String transparentSuffix, params ComfyTaskTypes[] types)
            => G(name, pattern, negative, guidance, types) with { TransparentPrefix = transparentPrefix, TransparentSuffix = transparentSuffix };

        const ComfyTaskTypes I2I = ComfyTaskTypes.ImagesToImage;
        const ComfyTaskTypes T2I = ComfyTaskTypes.TextToImage;
        const ComfyTaskTypes I2V = ComfyTaskTypes.ImagesToVideo;
        const ComfyTaskTypes T2V = ComfyTaskTypes.TextToVideo;

        #region Qwen-Image

        /// <summary>
        /// Qwen-Image 2.1 or later: a model file with a dotted version ("qwen_image_2.1_int8_convrot.safetensors", "qwen-image-edit-2_1",
        /// "qwen_image_3"), or a node class with the version ("TextEncodeQwenImage21", "QwenImage21Cache").
        /// A date version ("qwen_image_2512", "qwen_image_edit_2509") is an older release and doesn't match.
        /// </summary>
        const String QwenImage21Pattern = @"qwen[_\-]?image(?:[_\-]?edit)?[_\-]?v?(?:2[._][1-9]|[3-9](?:[._]\d|(?!\d)))|qwenimage(?:edit)?(?:2[1-9]|[3-9]\d?)(?!\d)";

        /// <summary>
        /// The prompt prefix that makes Qwen-Image 2.1 (and later) create an image with transparency
        /// </summary>
        const String QwenImageTransparentPrefix = "This is an RGBA format image with transparency. ";

        /// <summary>
        /// The prompt suffix that makes Qwen-Image 2.1 (and later) create an image with transparency
        /// </summary>
        const String QwenImageTransparentSuffix = " The image has an alpha channel and a transparent background.";

        const String QwenImageEditGuidance = """
                Write a direct edit instruction, ex: "Turn the cat into a dog" or "Replace the background with a beach at sunset".
                Be explicit about what to keep unchanged (identity, face, pose, clothing, lighting, composition) and what exactly to change.
                With several input images, refer to them by number ("the person in image 1", "the product from image 2") and describe positions and relations ("on the left", "facing each other").
                Text in the image: give the exact text in quotes and where it goes; English and Chinese text both render well.
                The model is unstable with vague instructions, so a precise, complete instruction (one to four sentences) works best.
                """;

        const String QwenImageGuidance = """
                Natural language, English or Chinese, detailed and structured (it uses a Qwen VL language model as the text encoder, so long and precise instructions are understood).
                Describe the subject, composition, setting, lighting, color palette and the style or medium.
                Text rendering is a strength: give the exact text in quotes and describe the layout explicitly (where the title goes, colors, sections, exact wording); Chinese text renders very well.
                For posters, infographics, icons or UI describe every element and its position.
                About 50-200 words; a longer, precise prompt is better than a short vague one.
                """;

        #endregion//Qwen-Image

        /// <summary>
        /// The known models, the first match is used (so more specific entries are first).
        /// Sources: the model cards / official READMEs and prompting guides of each model (Hugging Face, GitHub, docs.comfy.org, docs.bfl.ai).
        /// </summary>
        static readonly Guide[] Guides =
        [
            #region Image edit

            G("FLUX.1 Kontext", @"kontext", Negatives.Ignored, """
                Write an edit instruction, not a description of the whole image: say exactly what to change, and explicitly what to keep.
                Use direct verbs ("Change", "Replace", "Add", "Remove", "Make") rather than vague ones like "transform".
                Name subjects by their visible features ("the woman with short black hair"), not by pronouns.
                State what must be preserved, ex: "while keeping the same facial features, hairstyle, pose and expression" or "keep the subject in the exact same position".
                For a style change name the style precisely and keep the composition: "Convert to a watercolor painting while maintaining the composition and the character".
                For text in the image: Replace "old text" with "new text", maintain the same font style and color.
                Keep it to one or a few related edits, written in English, one or two sentences.
                """),
            GT("Qwen-Image-Edit 2.1", QwenImage21Pattern, Negatives.Used, QwenImageEditGuidance, QwenImageTransparentPrefix, QwenImageTransparentSuffix, I2I),
            G("Qwen-Image-Edit", @"qwen[_\-]?image", Negatives.Used, QwenImageEditGuidance, I2I),
            G("OmniGen2", @"omnigen", Negatives.Used, """
                Write the instruction in English, stating both what to change and how.
                Refer to the input images by number, ex: "Add the bird from image 1 onto the desk in image 2".
                For in-context generation describe the new scene and which subjects from which image to use.
                """),
            G("HiDream-E1", @"hidream[_\-]?e1", Negatives.Unknown, """
                Write a short edit instruction in English, ex: "Convert the image into a Ghibli style illustration" or "Change the jacket to red leather, keep everything else".
                """),

            #endregion//Image edit

            #region Image

            G("FLUX.2", @"flux[_\-.]?2", Negatives.Ignored, """
                Natural language prose. Put the most important elements first (earlier words weigh more): Subject + Action + Style + Context.
                Length: about 30-80 words for most images, up to 150 for complex scenes; don't pad with filler or quality tags.
                Describe what you want positively (ex: "sharp focus throughout" instead of "no blur"); the model ignores negative prompts.
                Photorealism: name the camera, lens or film stock and the era (ex: "shot on a 35mm lens, Kodak Portra 400, natural window light").
                Text to render: put the exact text in quotes and describe the placement, font style, size and color.
                Colors can be given as hex values attached to an object (ex: "a sofa in #1B6B6F").
                With reference images, describe the role of each reference ("the jacket from image 1 worn by the person in image 2").
                """),
            G("FLUX.1", @"flux[_\-.]?1|flux[_\-]?(dev|schnell)", Negatives.Ignored, """
                Natural language prose (no tag lists, no weighting syntax like "(word:1.2)"), most important elements first: subject, action, style, context.
                Length: about 40-100 words of concrete visual description (the T5 encoder reads up to 512 tokens, schnell 256).
                Describe what should be there instead of what shouldn't (negative prompts have no effect).
                Text to render goes in quotes, ex: a sign that reads "OPEN".
                Photorealism: describe the camera, lens, lighting and the time of day.
                """),
            G("Chroma", @"chroma", Negatives.Used, """
                Natural language prose or short comma separated phrases (T5 encoder), descriptive and concrete: subject, appearance, setting, composition, lighting, style.
                About 40-120 words.
                """),
            G("HiDream-I1", @"hidream", Negatives.Unknown, """
                Detailed natural language prose (it uses CLIP, T5 and a Llama text encoder): subject, appearance, action, setting, composition, lighting, style and mood.
                About 50-150 words. Text to render goes in quotes.
                """),
            GT("Qwen-Image 2.1", QwenImage21Pattern, Negatives.Used, QwenImageGuidance, QwenImageTransparentPrefix, QwenImageTransparentSuffix),
            G("Qwen-Image", @"qwen[_\-]?image", Negatives.Used, QwenImageGuidance),
            G("Z-Image", @"z[_\-]?image", Negatives.Ignored, """
                Detailed descriptive prose or comma separated descriptions, English or Chinese.
                Describe the subject, appearance, clothing, pose, setting, lighting, camera and style in concrete terms; world knowledge (famous places, styles) is understood.
                Text rendering (English and Chinese) is strong: put the exact text in quotes.
                The turbo model runs without guidance, so negative prompts have no effect: describe what you want instead.
                """),
            G("Neta Lumina", @"neta[_\-]?lumina|netayume", Negatives.Used, """
                Anime images. Start the prompt with exactly: "You are an assistant designed to generate anime images based on textual prompts. <Prompt Start> "
                Then a tag skeleton in this order: character, art style, appearance, clothing, expression and action, camera, lighting and effects, atmosphere, quality tags (include "best quality").
                Then one or two sentences of natural language detail (composition, light and shadow, scene details) that complement the tags without repeating them.
                Danbooru style tags (with spaces instead of underscores) are understood; English, Chinese and Japanese work.
                """),
            G("Lumina-Image 2.0", @"lumina", Negatives.Used, """
                Long, detailed natural language prose (Gemma text encoder): subject, appearance, lighting, color, composition, mood and style.
                About 60-200 words.
                """),
            G("HunyuanImage", @"hunyuan[_\-]?image", Negatives.Unknown, """
                Long, detailed descriptive sentences in English or Chinese: subject, appearance, action, environment, lighting, composition and style.
                Text to render goes in quotes (the model has a glyph aware text encoder), ex: a beret with the word "Tencent" on it.
                """),
            G("Stable Diffusion 3.5", @"sd3|stable[_\-]?diffusion[_\-]?3", Negatives.Used, """
                Natural language prose: subject, details, setting, composition, lighting and style, about 40-100 words.
                The first ~70 words matter most (the CLIP encoders read 77 tokens, the T5 encoder more).
                Text to render goes in quotes.
                """),
            G("Pony Diffusion XL", @"pony", Negatives.Used, """
                Start the prompt with: "score_9, score_8_up, score_7_up, score_6_up, score_5_up, score_4_up, " followed by a source tag (source_anime, source_cartoon, source_pony or source_furry) and a rating tag (rating_safe unless the user explicitly asks otherwise).
                Then comma separated booru style tags: subject and count (ex: 1girl), appearance, clothing, pose, expression, setting, lighting, style; short phrases are tolerated.
                Keep it within about 60 tags/words.
                """),
            G("Illustrious / NoobAI (SDXL anime)", @"illustrious|noob", Negatives.Used, """
                Comma separated Danbooru tags (spaces instead of underscores), optionally followed by one short natural language sentence about the scene.
                Order: character and series, count (1girl, 2boys), appearance, clothing, expression, pose and action, setting, lighting, then quality tags: "masterpiece, best quality, absurdres, very aesthetic, newest".
                Don't use Pony score tags. Keep it within about 60 tags.
                """),
            G("SDXL", @"sd[_\-]?xl|sdxl|juggernaut|realvis|dreamshaper[_\-]?xl", Negatives.Used, """
                Short natural phrases plus a few style keywords, comma separated: subject, details, setting, lighting, style or medium, quality words (ex: "highly detailed, sharp focus").
                Keep the important words in the first ~60 words (CLIP reads 77 tokens per chunk); about 30-60 words.
                Weighting like "(red hair:1.2)" can be used for emphasis, sparingly.
                """),
            G("Stable Diffusion 1.5", @"v1[_\-]?5|sd[_\-]?1[_\-.]?5|sd15", Negatives.Used, """
                Comma separated tags and short phrases: subject, details, style or medium, lighting, then quality words (masterpiece, best quality, highly detailed).
                Keep it within ~60 words (CLIP reads 77 tokens), the first words matter most. Weighting like "(red hair:1.2)" can be used sparingly.
                """),
            G("Gemini image (Nano Banana)", @"gemini|nano[_\-]?banana", Negatives.Unknown, """
                Descriptive narrative prose (full sentences, not tag lists). For photos: shot type, subject and setting, lighting, camera angle and lens.
                Edits are conversational instructions: what to add, remove or change, and what to keep. Text to render goes in quotes.
                """),

            #endregion//Image

            #region Video

            G("LTX-2", @"ltx[_\-.]?2|ltx2|ltxv[_\-]?2|ltx25|ltx23", Negatives.Used, """
                One single flowing paragraph in the present tense, chronological, like a cinematographer describing one continuous shot; within about 200 words.
                Order: the shot type and main action in the first sentence, then movements and gestures, the appearance of the people and objects, the environment, the camera movement, lighting and color.
                The model also generates the sound: describe it (ambient sounds, sound effects, music) and put any spoken lines in quotes, saying who speaks and how ("she says softly: \"Welcome home.\"").
                Be literal and precise; avoid abstract words, emotions without visible expression, and scene cuts unless asked for.
                """),
            G("LTX-Video", @"ltx", Negatives.Used, """
                One single flowing paragraph, chronological, within about 200 words, literal and precise like a shot description.
                Order: the main action in one sentence, then movements and gestures, the appearance of characters and objects, the background and environment, the camera angle and movement, lighting and color, then any changes or events.
                """),
            G("Wan", @"(^|[^a-z])wan[_\-.]?2", Negatives.Used, """
                Natural language, English or Chinese, rich and specific (the official tools expand prompts with an LLM, so longer prompts work better): about 60-150 words.
                Formula: subject (appearance), action (what happens, in order), environment, camera (shot size and movement, ex: "medium shot, slow dolly in"), lighting, style and mood.
                Keep it to one continuous shot without cuts unless asked for.
                """),
            G("HunyuanVideo", @"hunyuan[_\-]?video", Negatives.Unknown, """
                Long, detailed prompts work significantly better, English or Chinese.
                Structure: the scene and the subject, then a timeline of the action ("Initially ... Then ... Finally ..."), then the camera movement, lighting and atmosphere, then a style line (ex: "cinematic photography, realistic style").
                About 80-200 words.
                """),
            G("Mochi", @"mochi", Negatives.Unknown, """
                Descriptive English prose, photorealistic content works best (weak on animation and cartoons): subject, action, setting, camera and lighting, about 50-120 words.
                """),
            G("CogVideoX", @"cogvideo", Negatives.Unknown, """
                English only, long descriptive prose: subject, setting, lighting, color palette, motion and mood.
                Stay under about 150 words (the encoder reads at most 226 tokens).
                """),
            G("Kandinsky 5", @"kandinsky", Negatives.Unknown, """
                Natural language in English (or Russian): subject, action, setting, camera and style, about 30-100 words.
                """),

            #endregion//Video

            #region Music

            G("ACE-Step", @"ace[_\-]?step", Negatives.Unknown, """
                The prompt is the caption / tags of the song: comma separated and specific, combining several dimensions: genre and subgenre, mood and emotion, instruments, timbre, era, production style, vocal type (ex: "female vocal, breathy") or "instrumental".
                Don't put the BPM, key or lyrics in the caption; lyrics are supplied separately.
                About 8-20 tags.
                """, ComfyTaskTypes.TextToMusic),
            G("YuE", @"(^|[^a-z])yue", Negatives.Unknown, """
                The prompt is the genre / style description as comma separated tags: language of the vocals (ex: "English"), genre, mood, vocal timbre and gender (ex: "warm female vocal"), key instruments, ex: "English, jazz-funk, warm lead vocal, Rhodes, bass and drums".
                About 6-15 tags. Lyrics are supplied separately (with [verse] / [chorus] sections).
                """, ComfyTaskTypes.TextToMusic),
            G("DiffRhythm", @"diffrhythm", Negatives.Unknown, """
                The prompt is a short style description, ex: "Jazzy Nightclub Vibe" or "Pop Emotional Piano"; for instrumentals a scene style description. Lyrics are supplied separately.
                """, ComfyTaskTypes.TextToMusic),

            #endregion//Music

            #region Sound

            G("MMAudio", @"mmaudio", Negatives.Unknown, """
                A short, concrete description of the sounds (source and action), ex: "waves crashing on rocks, seagulls in the distance".
                Use common, well known sound concepts; avoid unusual or technical names.
                """),
            G("Stable Audio", @"stable[_\-]?audio", Negatives.Used, """
                Short, concrete English descriptions work best, ex: "The sound of a hammer hitting a wooden surface" or "128 BPM tech house drum loop".
                For sound effects describe the source, the action, the materials, the environment and acoustics (indoor, outdoor, reverb, distance) and the timing.
                For music give the genre, BPM, instruments and mood; realistic vocals are not possible.
                About 10-40 words.
                """),

            #endregion//Sound

            #region Speech

            G("AuK", @"(^|\n)auk", Negatives.Unknown, """
                The prompt is a single instruction that contains both the voice description and the text to speak, in exactly this format:
                Generate speech based on the following description: "<voice description>". The content to speak is: "<text to speak>"
                Use the voice the user asks for, or choose a natural voice that fits the text (ex: "A warm, clear female voice"). The text to speak is the user's text, cleaned up for speaking.
                """, ComfyTaskTypes.TextToSpeech),
            G("OmniVoice", @"omnivoice", Negatives.Unknown, """
                The prompt is the exact text to speak, many languages are supported.
                Non-verbal sounds can be inserted inline in square brackets where they occur, ex: "Hello! [laughter] That was fun."; use them sparingly and only when they fit the request.
                The voice itself is described separately (not in the text).
                """),
            G("Higgs Audio", @"higgs", Negatives.Unknown, """
                The prompt is the text to speak. Delivery tags in the form <|category:value|> are supported: emotion (ex: <|emotion:amusement|>), style (<|style:whispering|>), prosody (<|prosody:speed_slow|>, <|prosody:pause|>) and sound effects (<|sfx:laughter|>, followed by its onomatopoeia).
                Put delivery tags (emotion, style, speed, pitch) at the start, pauses and sound effects inline. Use them only when they fit the request.
                """),
            G("Chatterbox", @"chatterbox", Negatives.Unknown, """
                The prompt is the exact text to speak. Inline paralinguistic tags like [laugh], [chuckle] or [cough] are supported (English), use them sparingly where they fit.
                """),
            G("VibeVoice", @"vibevoice", Negatives.Unknown, """
                The prompt is the script to speak, English or Chinese. For several speakers write one line per turn prefixed with the speaker, ex: "Speaker 1: Hello there." and "Speaker 2: Hi!" (at most 4 speakers).
                """),
            G("IndexTTS2", @"index[_\-]?tts", Negatives.Unknown, """
                The prompt is the exact text to speak (Chinese or English); there are no inline emotion tags.
                """),
            G("Qwen3-TTS", @"qwen3[_\-]?tts", Negatives.Unknown, """
                The prompt is the exact text to speak (Chinese, English and several other languages). The speaking style and voice are described separately.
                """),
            G("F5-TTS", @"f5[_\-]?tts", Negatives.Unknown, """
                The prompt is the plain text to speak (English or Chinese), no tags.
                """),
            G("Kokoro", @"kokoro", Negatives.Unknown, """
                The prompt is the plain text to speak, no emotion tags. A custom pronunciation can be given as [word](/IPA/), only when needed.
                """),

            #endregion//Speech
        ];

        Guide GetGuide()
        {
            var g = _Guide;
            if (g != null)
                return g == NoGuide ? null : g;
            var text = String.Join('\n', new[] { Name }.Concat(ModelFiles).Concat(Loras).Concat(NodeClasses)).ToLowerInvariant();
            g = Guides.FirstOrDefault(x => ((x.Types == null) || x.Types.Contains(Type)) && x.Match.IsMatch(text)) ?? NoGuide;
            _Guide = g;
            return g == NoGuide ? null : g;
        }

        Guide _Guide;
        static readonly Guide NoGuide = new Guide("", null, null, Negatives.Unknown, "");

        static String TaskDescription(ComfyTaskTypes t) => t switch
        {
            ComfyTaskTypes.TextToImage => "creates an image from a text prompt",
            ComfyTaskTypes.ImagesToImage => "creates an image from one or more reference images and a text instruction (ex: an image edit)",
            ComfyTaskTypes.TextToVideo => "creates a video from a text prompt",
            ComfyTaskTypes.ImagesToVideo => "creates a video from one or more reference images (ex: the first frame) and a text prompt",
            ComfyTaskTypes.TextToMusic => "creates music from a text prompt (the style) and lyrics (supplied separately)",
            ComfyTaskTypes.TextToSpeech => "creates speech from a text (the text to speak)",
            ComfyTaskTypes.TextToSound => "creates a sound (effect) from a text prompt",
            _ => "creates media",
        };

        /// <summary>
        /// General guidance per task type (used for all models)
        /// </summary>
        static String TaskGuidance(ComfyTaskTypes t) => t switch
        {
            ComfyTaskTypes.TextToImage => """
                Turn the request into a concrete visual description: the main subject first (who or what, appearance, pose, expression), then the setting and background, the composition and framing (shot size, angle, placement in the frame), lighting, color palette, and the style or medium (ex: photo, oil painting, 3D render, flat vector illustration).
                For photographs describe the camera, lens and lighting; for illustrations the medium and art style.
                Keep every element the user asked for; add plausible details that serve the request, never contradict it.
                Use the aspect ratio (if known) for the composition, ex: a wide landscape for 16:9, a full body portrait for 9:16.
                Any text that should appear in the image goes in double quotes, exactly as it should be written.
                """,
            ComfyTaskTypes.ImagesToImage => """
                The output prompt is an instruction about how to change or combine the reference images, not a full description of a new image.
                Say precisely what to change (objects, colors, style, background, text, pose) and what must stay the same (identity, face, pose, composition, lighting, style, unchanged objects).
                Refer to the reference images by number when there are several ("image 1", "image 2"), and only to images that are supplied.
                Describe the wanted result concretely ("replace the sky with a dramatic orange sunset") rather than abstractly ("make it better").
                """,
            ComfyTaskTypes.TextToVideo => """
                Describe one continuous shot (no cuts unless asked for) in present tense and in chronological order: the subject and its appearance, the action and how it develops over time, the environment, the camera (shot size, angle and movement, ex: "slow dolly in", "static locked-off shot", "handheld tracking shot"), the lighting and the style.
                Fit the amount of action to the duration (if known): about one distinct action or event per 2-3 seconds; slow, smooth motion looks best.
                Describe visible motion and expressions rather than inner feelings, and keep people, objects and the setting consistent.
                Use the aspect ratio (if known) for the framing.
                """,
            ComfyTaskTypes.ImagesToVideo => """
                The reference image defines the first frame (or the appearance of the subjects), so don't redescribe it in detail and don't contradict it: mention the main subject briefly so that it's clear what moves.
                Focus on what happens: the motion and action in chronological order, how the subjects move, the camera movement, and changes in light or atmosphere.
                Describe one continuous shot in present tense, no cuts unless asked for.
                Fit the amount of action to the duration (if known): about one distinct action or event per 2-3 seconds; slow, smooth motion looks best.
                """,
            ComfyTaskTypes.TextToMusic => """
                The output prompt describes the music (it's not lyrics): genre and subgenre, mood, tempo feel, the key instruments, the production style and era, and the vocals (type, gender, timbre) or that it's instrumental.
                Be specific and combine several of these dimensions; comma separated descriptors work best unless the model guidance says otherwise.
                Lyrics are supplied separately, never put lyrics in the prompt.
                """,
            ComfyTaskTypes.TextToSpeech => """
                The output prompt is the text that will be spoken, so it must only contain the words to say (unless the model guidance below describes a different format).
                Keep the user's words and meaning; if the user describes what to say rather than giving the exact words, write natural spoken text for it.
                Make it easy to speak: write numbers, dates, times, currencies, units, abbreviations and symbols out as they are spoken (ex: "$5.50" => "five dollars and fifty cents"), use punctuation for natural pauses and intonation, and split very long sentences.
                No markdown, emojis, URLs, stage directions or speaker labels unless the model supports them.
                """,
            ComfyTaskTypes.TextToSound => """
                Describe the sound itself concretely: the source, the action, the materials, how it develops over time (start, rhythm, end), and the environment and acoustics (indoor or outdoor, room size, reverb, distance, background).
                Use the duration (if known) to describe how much happens.
                No music or speech unless asked for. Keep it short and concrete.
                """,
            _ => "",
        };

        String BuildPromptEnhancement()
        {
            var type = Type;
            var guide = GetGuide();
            var media = type switch
            {
                ComfyTaskTypes.TextToImage or ComfyTaskTypes.ImagesToImage => "image",
                ComfyTaskTypes.TextToVideo or ComfyTaskTypes.ImagesToVideo => "video",
                ComfyTaskTypes.TextToMusic => "music",
                ComfyTaskTypes.TextToSpeech => "speech",
                _ => "sound",
            };
            var sb = new StringBuilder();
            void Line(String s = "") => sb.Append(s).Append('\n');
            void Lines(String s)
            {
                foreach (var l in s.Split('\n'))
                {
                    var t = l.Trim();
                    if (t.Length > 0)
                        Line("- " + t);
                }
            }

            Line($"You write prompts for an AI {media} generator. Rewrite the user's request into one prompt that is optimized for the generation workflow described below. Your output is sent directly to the workflow.");
            Line();

            //  The workflow
            Line("# The workflow");
            Line($"- Workflow: \"{Name}\", it {TaskDescription(type)}.");
            var modelName = guide?.Name;
            var files = ModelFiles.Where(x => !IsAuxiliaryModel(x)).Take(4).ToList();
            if (modelName != null)
                Line($"- Model: {modelName}" + (files.Count > 0 ? $" (model files: {String.Join(", ", files)})." : "."));
            else if (files.Count > 0)
                Line($"- Model files: {String.Join(", ", files)} (the model isn't known, use the model files and the example prompt below to infer how to prompt it).");
            if (Loras.Count > 0)
                Line($"- LoRA's: {String.Join(", ", Loras)}.");
            if (FileCount > 0)
                Line(type == ComfyTaskTypes.TextToSpeech
                    ? "- A reference voice recording is supplied (the voice is cloned from it)."
                    : $"- Reference images: {FileCount} ({(FileCount == 1 ? "\"image 1\"" : String.Join(", ", Enumerable.Range(1, FileCount).Select(x => $"\"image {x}\"")))}). You can't see them, rely on the user's description of them.");
            Line();

            //  How to write the prompt
            Line("# How to write the prompt");
            Lines(TaskGuidance(type));
            if (guide != null)
            {
                Line();
                Line($"## {guide.Name} specific");
                Lines(guide.Guidance);
            }
            Line();

            //  Prefix and suffix
            var prefix = PromptPrefix;
            var suffix = PromptSuffix;
            if ((prefix != null) || (suffix != null))
            {
                Line("# Required prefix / suffix");
                if (prefix != null)
                    Line($"- The prompt must start with exactly this text (ex: a trigger for a LoRA or a fixed style): {Json(prefix)}");
                if (suffix != null)
                    Line($"- The prompt must end with exactly this text: {Json(suffix)}");
                Line("- Write the rest of the prompt so that it reads naturally together with it, and don't repeat it.");
                Line();
            }

            //  Transparency made from the prompt (ex: Qwen-Image 2.1)
            if (PromptTransparency)
            {
                Line("# Transparency");
                Line("- A transparent background is a separate option: when it's used, the workflow adds the text that the model needs for it to the prompt, so never write about transparency, alpha channels or RGBA yourself.");
                Line("- If the request asks for a transparent background (ex: a sticker, an icon, a logo, a cut-out product), describe only the subject(s), cleanly isolated and fully in frame, without a background scene.");
                Line();
            }

            //  Negative prompt
            var negative = Supports(PNegative);
            var neg = guide?.Negative ?? Negatives.Unknown;
            Line("# Negative prompt");
            if (negative && (neg != Negatives.Ignored))
                Line("- What to avoid is given to the workflow as a separate negative prompt, so the prompt should only describe what should be there.");
            else
                Line("- This workflow has no (effective) negative prompt.");
            Line("- Avoid negations like \"no people\" or \"without text\" (generators tend to add what is mentioned): describe the wanted result positively instead (ex: \"an empty street\").");
            Line();

            //  Example
            var example = DefaultPrompt;
            if (example != null)
            {
                const int maxExample = 1500;
                if (example.Length > maxExample)
                    example = String.Concat(example.AsSpan(0, maxExample), " [...]");
                Line("# Example");
                Line("A prompt that this workflow was saved with, it shows a format and a level of detail that works with the workflow. Use it as a reference for the format only, never copy its content into your prompt:");
                Line("<example>");
                Line(example);
                Line("</example>");
                Line();
            }

            //  The user message
            Line("# The user message");
            Line("- The user message contains the request, and may contain media details (ex: the size or aspect ratio, the duration, a description of the reference images or the voice). Use the details to shape the prompt (composition, amount of action, pacing), but don't write technical parameters (pixel sizes, durations in seconds, seeds) into the prompt unless the model guidance says so.");
            Line("- If the request is already a good prompt in the right format, keep it and only make small improvements (and apply the required prefix / suffix).");
            Line("- If the request is short or vague, make reasonable creative choices that serve it; never ask questions, the output is used directly.");
            Line();

            //  Output rules
            Line("# Output rules");
            Line("- Output only the final prompt text: no explanations, reasoning, comments, titles, labels (like \"Prompt:\"), quotes around the whole prompt, markdown or alternatives.");
            Line("- Output one prompt only" + ((type == ComfyTaskTypes.TextToSpeech) || (type == ComfyTaskTypes.TextToMusic) ? "." : ", as a single paragraph (no line breaks) unless the model format requires line breaks."));
            if (type == ComfyTaskTypes.TextToSpeech)
                Line("- Write the text to speak in the language the user wants it spoken in (the language of the request unless asked otherwise).");
            else
                Line("- Write the prompt in English (unless the model guidance says another language works better for the request), but keep any text that should be rendered or spoken in the language the user wants.");
            Line("- Keep the user's intent: don't add, remove or change subjects or important details, and don't add content the user didn't ask for (ex: extra people, text or logos).");
            return sb.ToString();
        }

        /// <summary>
        /// Models that don't identify the generator (vae's, upscalers, text encoders)
        /// </summary>
        static bool IsAuxiliaryModel(String file)
        {
            var f = file.ToLowerInvariant();
            return f.Contains("vae") || f.Contains("upscale") || f.Contains("clip") || f.Contains("t5") || f.Contains("encoder")
                || f.Contains("gemma") || f.Contains("llama") || f.Contains("qwen3vl") || f.Contains("qwen_3") || f.Contains("qwen3.5") || f.Contains("qwen2.5") || f.Contains("qwen_omni");
        }

        static String Json(String s) => System.Text.Json.JsonSerializer.Serialize(s);

        #endregion//Prompt enhancement
    }
}
