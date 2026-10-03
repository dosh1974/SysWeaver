using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Flash attention mode
    /// </summary>
    public enum LlamaSharpFlashAttention
    {
        /// <summary>
        /// Let llama.cpp decide (enabled if supported by the backend)
        /// </summary>
        Auto,
        /// <summary>
        /// Disable flash attention
        /// </summary>
        Disabled,
        /// <summary>
        /// Enable flash attention
        /// </summary>
        Enabled,
    }

    /// <summary>
    /// The data type used for the KV cache (lower precision uses less memory)
    /// </summary>
    public enum LlamaSharpKvCacheType
    {
        /// <summary>
        /// Use the llama.cpp default (F16)
        /// </summary>
        Default,
        /// <summary>
        /// 16 bit floats
        /// </summary>
        F16,
        /// <summary>
        /// 8 bit quantized (requires flash attention for the V cache)
        /// </summary>
        Q8_0,
        /// <summary>
        /// 4 bit quantized (requires flash attention for the V cache)
        /// </summary>
        Q4_0,
    }

    /// <summary>
    /// A local LLM (GGUF file) to load, all of these parameters are used when the model is loaded (on service creation)
    /// </summary>
    public sealed class LlamaSharpLlm
    {
        public override string ToString() => String.Concat(Name, " : ", FilePath);

        #region Identity

        /// <summary>
        /// The model name (the "model code" to use for this model), must be unique.
        /// This is the value to use as the model of a session, ex: "qwen3-8b"
        /// </summary>
        public String Name;

        /// <summary>
        /// The file path of the model data (a GGUF file), can contain path templates, ex: "$(CommonApplicationData)\Models\Qwen3-8B-Q4_K_M.gguf".
        /// Relative paths are relative to the application.
        /// </summary>
        public String FilePath;

        /// <summary>
        /// An optional human readable name of the model
        /// </summary>
        public String DisplayName;

        /// <summary>
        /// An optional description of the model
        /// </summary>
        public String Description;

        #endregion//Identity

        #region Load

        /// <summary>
        /// The context size (number of tokens) of each inference context, this is the maximum number of tokens (input + output) of a request.
        /// 0 = Use the context size the model was trained with (can use a lot of memory).
        /// </summary>
        public uint ContextSize = 8192;

        /// <summary>
        /// Number of layers to offload to the GPU (if a GPU backend is used), a large number (default) offloads all layers. 0 = CPU only.
        /// </summary>
        public int GpuLayerCount = 999;

        /// <summary>
        /// The GPU to use (if more than one is available)
        /// </summary>
        public int MainGpu;

        /// <summary>
        /// Number of threads to use for generation, 0 = Use the llama.cpp default
        /// </summary>
        public int Threads;

        /// <summary>
        /// Number of threads to use for batch (prompt) processing, 0 = Use the llama.cpp default
        /// </summary>
        public int BatchThreads;

        /// <summary>
        /// The logical maximum batch size (number of tokens) submitted to llama.cpp
        /// </summary>
        public uint BatchSize = 512;

        /// <summary>
        /// The physical maximum batch size (number of tokens)
        /// </summary>
        public uint UBatchSize = 512;

        /// <summary>
        /// Flash attention mode
        /// </summary>
        public LlamaSharpFlashAttention FlashAttention = LlamaSharpFlashAttention.Auto;

        /// <summary>
        /// The data type of the K cache
        /// </summary>
        public LlamaSharpKvCacheType TypeK = LlamaSharpKvCacheType.Default;

        /// <summary>
        /// The data type of the V cache (quantized types requires flash attention)
        /// </summary>
        public LlamaSharpKvCacheType TypeV = LlamaSharpKvCacheType.Default;

        /// <summary>
        /// RoPE base frequency, 0 = Use the model default
        /// </summary>
        public float RopeFrequencyBase;

        /// <summary>
        /// RoPE frequency scaling factor, 0 = Use the model default
        /// </summary>
        public float RopeFrequencyScale;

        /// <summary>
        /// Use memory mapping to load the model (faster load, less memory)
        /// </summary>
        public bool UseMemoryMap = true;

        /// <summary>
        /// Force the system to keep the model in RAM
        /// </summary>
        public bool UseMemoryLock;

        /// <summary>
        /// An optional chat template to use instead of the template in the model file.
        /// Can be the name of a template known by llama.cpp (ex: "chatml", "llama3"), "gemma4" (built in, since llama.cpp doesn't support it) or a template string.
        /// If the template isn't supported by llama.cpp and Gemma 4 tags are found in it, the built in "gemma4" template is used.
        /// </summary>
        public String ChatTemplate;

        #endregion//Load

        #region Inference

        /// <summary>
        /// The maximum number of tokens to generate per request (the context size may limit it further)
        /// </summary>
        public int MaxTokens = 4096;

        /// <summary>
        /// Maximum number of concurrent requests to this model (each request allocates a context of ContextSize tokens)
        /// </summary>
        public int MaxConcurrent = 1;

        /// <summary>
        /// True if the model supports the system role (else the system prompt is sent as the first user message)
        /// </summary>
        public bool SupportSystemRole = true;

        /// <summary>
        /// True if the model can use tools (tools are described in the system prompt and the model is asked to respond with &lt;tool_call&gt; tags (Hermes / Qwen style), the built in "gemma4" template uses the native Gemma 4 tool format)
        /// </summary>
        public bool SupportTools = true;

        /// <summary>
        /// True if the model outputs thinking within &lt;think&gt; tags (these are removed from the response)
        /// </summary>
        public bool? CanReason;

        /// <summary>
        /// Decode special tokens in the output, required for models where tags like &lt;tool_call&gt; or &lt;think&gt; are special tokens
        /// </summary>
        public bool DecodeSpecialTokens = true;

        /// <summary>
        /// Top K sampling, 0 = disabled
        /// </summary>
        public int TopK = 40;

        /// <summary>
        /// Top P sampling, 1 = disabled
        /// </summary>
        public float TopP = 0.95f;

        /// <summary>
        /// Min P sampling, 0 = disabled
        /// </summary>
        public float MinP = 0.05f;

        /// <summary>
        /// Repetition penalty, 1 = disabled
        /// </summary>
        public float RepeatPenalty = 1.0f;

        #endregion//Inference
    }


    /// <summary>
    /// Parameters for the LLamaSharp (local LLM) AI service
    /// </summary>
    public sealed class LlamaSharpAiParams : AiServiceParams
    {
        public LlamaSharpAiParams()
        {
            MaxConcurrentImages = 0;
        }

        /// <summary>
        /// The LLM's to load (when the service is created).
        /// If DefaultChatModel isn't set, the first model is the default.
        /// </summary>
        public LlamaSharpLlm[] Llms;

        /// <summary>
        /// Use the Vulkan (GPU) backend if available, else the CPU backend is used.
        /// Note: The backend is selected when the first model is loaded (process wide).
        /// </summary>
        public bool UseVulkan = true;

        /// <summary>
        /// If true, llama.cpp log messages are sent to the message host (errors as errors, warnings as debug)
        /// </summary>
        public bool LogNative = true;
    }


    /// <summary>
    /// Parameters for a LLamaSharp session.
    /// The model is one of the names (model codes) of the loaded LLM's.
    /// </summary>
    public class LlamaSharpSessionParams : AiSessionParams
    {
        /// <summary>
        /// Get LLamaSharp session parameters from some (possibly API agnostic) session parameters
        /// </summary>
        /// <param name="p">The parameters, can be null</param>
        /// <returns>The same instance if it's already a LlamaSharpSessionParams, else a new instance with the common values copied</returns>
        public static LlamaSharpSessionParams From(AiSessionParams p)
        {
            if (p == null)
                return null;
            if (p is LlamaSharpSessionParams lp)
                return lp;
            return new LlamaSharpSessionParams
            {
                Model = p.Model,
                Reasoning = p.Reasoning,
            };
        }
    }

}
