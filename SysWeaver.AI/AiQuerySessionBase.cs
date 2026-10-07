using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A function call requested by the AI model
    /// </summary>
    /// <param name="Name">The name of the function (tool)</param>
    /// <param name="Arguments">The arguments as a json object (as supplied by the AI)</param>
    public readonly record struct AiFunctionCall(String Name, BinaryData Arguments);


    /// <summary>
    /// API agnostic base class for query (and chat) sessions.
    /// Handles tool registration, tool invocation, the memory tools and some common model properties.
    /// </summary>
    public abstract class AiQuerySessionBase : IAiQuerySession
    {
        protected AiQuerySessionBase(String model, bool haveTemperature, bool supportSystemRole, bool? supportParallelToolCalls, IAiToolCache toolCache = null, PerfMonitor monitor = null, IAiMemory memory = null)
        {
            Model = model;
            HaveTemperature = haveTemperature;
            SupportSystemRole = supportSystemRole;
            SupportParallelToolCalls = supportParallelToolCalls;
            Monitor = monitor;
            ToolCache = toolCache;
            Memory = memory;
            if (memory != null)
            {
                AddTool(this, Method_AddMemory, nameof(AddMemory), Monitor, "");
                AddTool(this, Method_GetMemory, nameof(GetMemory), Monitor, "");
                AddTool(this, Method_RemoveMemory, nameof(RemoveMemory), Monitor, "");
                AddTool(this, Method_SetMemory, nameof(SetMemory), Monitor, "");
            }
        }

        #region Memory

        protected IAiMemory Memory;

        /// <summary>
        /// Add a memory entry for the current user.
        /// A maximum of 64 entries can be added.
        /// If the memory alredy exist, it's overwritten with this memory.
        /// The memory stored for the key "Global" is availabe in the system prompt at all times.
        /// </summary>
        /// <param name="data">Data to add</param>
        /// <param name="context"></param>
        /// <returns>True if successful</returns>
        [AiTool("🧠➕")]
        Task<bool> AddMemory(AiMemoryAdd data, HttpServerRequest context)
           => Memory.AddMemory(context.Session, data.Key, data.Desc, data.Value);


        /// <summary>
        /// Get some memory associated with the current user
        /// </summary>
        /// <param name="key">The memory key</param>
        /// <param name="context"></param>
        /// <returns>The saved memory</returns>
        [AiTool("🧠📥")]
        Task<string> GetMemory(String key, HttpServerRequest context)
            => Memory.GetMemory(context.Session, key);


        /// <summary>
        /// Remove some stored memory associated with the current user
        /// </summary>
        /// <param name="key"></param>
        /// <param name="context"></param>
        /// <returns>True if successful</returns>
        [AiTool("🧠❌")]
        Task<bool> RemoveMemory(String key, HttpServerRequest context)
            => Memory.RemoveMemory(context.Session, key);

        /// <summary>
        /// Update / set some memoru associated with the current user
        /// </summary>
        /// <param name="data">Data to update / set</param>
        /// <param name="context"></param>
        /// <returns>True if successful</returns>
        [AiTool("🧠💾")]
        Task<bool> SetMemory(AiMemorySet data, HttpServerRequest context)
           => Memory.SetMemory(context.Session, data.Key, data.Value);

        static readonly MethodInfo Method_AddMemory = typeof(AiQuerySessionBase).GetMethod(nameof(AddMemory), BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo Method_GetMemory = typeof(AiQuerySessionBase).GetMethod(nameof(GetMemory), BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo Method_RemoveMemory = typeof(AiQuerySessionBase).GetMethod(nameof(RemoveMemory), BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo Method_SetMemory = typeof(AiQuerySessionBase).GetMethod(nameof(SetMemory), BindingFlags.NonPublic | BindingFlags.Instance);

        #endregion//Memory

        #region Model

        protected readonly PerfMonitor Monitor;
        protected readonly IAiToolCache ToolCache;
        public String Model { get; }
        public bool HaveTemperature { get; }
        public bool SupportSystemRole { get; }
        public bool? SupportParallelToolCalls { get; }

        /// <summary>
        /// Temperature of the AI model, lower is more cosistent and less random [0, 2] (1 is default).
        /// </summary>
        public float Temperature { get; set; } = 0.2f;

        /// <summary>
        /// The system prompt
        /// </summary>
        public abstract String SystemPrompt { get; set; }

        #endregion//Model

        #region Tools

        internal readonly Dictionary<String, AiTool> Tools = new Dictionary<string, AiTool>(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<String, AiTool>> ALlTools => Tools;

        volatile AiTool[] ToolArray;

        /// <summary>
        /// Get all tools in this session (cached until a tool is added)
        /// </summary>
        /// <returns>All tools</returns>
        protected AiTool[] GetTools()
        {
            var t = ToolArray;
            if (t != null)
                return t;
            t = Tools.Values.ToArray();
            ToolArray = t;
            return t;
        }

        /// <summary>
        /// Called whenever a tool is added (to invalidate cached API options etc)
        /// </summary>
        protected virtual void OnToolsChanged()
        {
        }

        bool InternalAddTool(AiTool tool)
        {
            if (tool == null)
                return false;
            Tools.TryAdd(tool.Name, tool);
            ToolArray = null;
            OnToolsChanged();
            return true;
        }

        /// <summary>
        /// Add an API endpoint that the AI can use in this session
        /// </summary>
        /// <param name="apiName">Name of tha api local url: ex: "Api/auth/GetUser"</param>
        /// <param name="fn">An optional function name (as shown to the AI).
        /// null - will use the underlaying method name (or as specified by the OpenAiToolName attribute) in combination with the declaring type name.
        /// "" - will user the full apiName, but with '/' replaced by '_'.
        /// </param>
        /// <returns>True if the tool was added</returns>
        public bool AddTool(String apiName, String fn = null)
            => InternalAddTool(ToolCache.GetTool(apiName, fn));

        /// <summary>
        /// Add a tool from some registered tool
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <returns>True if the tool was added</returns>
        public bool AddRegistredTool(String name)
            => InternalAddTool(ToolCache.GetRegisteredTool(name));

        /// <summary>
        /// Add a method that the AI can use in this session.
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional function name, default will be instance type name _ method name</param>
        /// <param name="perfMonitor">An optional performance monitor</param>
        /// <param name="defaultAuth">The default auth to use if there is no WebApiAuthAttribute on the method</param>
        /// <param name="defaultCachedCompression">The default compression when there is no WebApiCompressionAttribute on the method</param>
        /// <param name="defaultCompression">The default compression for uncached cached methods when there is no WebApiCompressionAttribute on the method</param>
        /// <returns></returns>
        public bool AddTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression)
            => InternalAddTool(ToolCache.GetTool(instance, method, fn, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression));

        async Task<AiCallInstance> AiCall(String name, BinaryData b, HttpServerRequest request, ConcurrentDictionary<String, int> callIcons)
        {
            var start = DateTime.UtcNow;
            if (!Tools.TryGetValue(name, out var tool))
                return new AiCallInstance(null, b, start, DateTime.UtcNow, new Exception("The tool " + name.ToQuoted() + " is unknown!"), name);
            if (!(request?.Session.IsValid(tool.Auth) ?? false))
                return new AiCallInstance(tool, b, start, DateTime.UtcNow, new Exception("The requesting user is not allowed to use the tool " + name.ToQuoted()));

            var icon = tool.Icon;
            callIcons.TryGetValue(icon, out var c);
            callIcons[icon] = c + 1;
            try
            {
                using var xx = Monitor?.Track("AiCall." + name);
                var res = await tool.Invoke(b, request).ConfigureAwait(false);
                return new AiCallInstance(tool, b, start, DateTime.UtcNow, res);
            }
            catch (Exception e)
            {
                return new AiCallInstance(tool, b, start, DateTime.UtcNow, e);
            }
        }

        /// <summary>
        /// Execute a batch of function calls (in parallel)
        /// </summary>
        /// <param name="calls">The calls to perform</param>
        /// <param name="request">The request</param>
        /// <param name="debugMsg">Optional debug tracking</param>
        /// <param name="onToolCalls">Optional callback with a summary (icons) of the tools being called</param>
        /// <returns>The output for each call (in the same order as the input), to be sent back to the AI</returns>
        protected async Task<String[]> AiCalls(IReadOnlyList<AiFunctionCall> calls, HttpServerRequest request, AiDebugMessage debugMsg, Action<String> onToolCalls)
        {
            var tcl = calls.Count;
            if (tcl <= 0)
                return [];
            debugMsg?.StartBatch();
            ConcurrentDictionary<String, int> icons = new (StringComparer.Ordinal);
            Task<AiCallInstance>[] tasks = new Task<AiCallInstance>[tcl];
            for (int i = 0; i < tcl; ++i)
            {
                var tc = calls[i];
                tasks[i] = AiCall(tc.Name, tc.Arguments, request, icons);
            }
            StringBuilder sb = new StringBuilder();
            bool prevIsDigit = false;
            foreach (var x in icons.OrderByDescending(x => x.Value))
            {
                if (sb.Length > 0)
                    sb.Append('-');
                if (prevIsDigit)
                    sb.Append(' ');
                var c = x.Key;
                var v = x.Value;
                if (c == "")
                {
                    prevIsDigit = true;
                    sb.Append('x').Append(v);
                }
                else
                {
                    sb.Append(x.Key);
                    prevIsDigit = v > 1;
                    if (prevIsDigit)
                        sb.Append('x').Append(v);
                }
            }
            onToolCalls?.Invoke(sb.ToString());
            await Task.WhenAll(tasks).ConfigureAwait(false);
            var ret = new String[tcl];
            for (int i = 0; i < tcl; ++i)
            {
                var res = tasks[i].GetAwaiter().GetResult();
                debugMsg?.AddCall(res);
                var ex = res.Ex;
                //  The model may echo the error to the user, so sensitive information is removed
                ret[i] = ex != null ? "Execution failed: " + ex.SafeMessage() : (res.Ret ?? "");
            }
            debugMsg?.EndBatch();
            return ret;
        }

        #endregion//Tools

        /// <summary>
        /// Perform a query (the query is not added to any conversation history)
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <param name="extraData">optional attachements</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
        public abstract Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null);

    }

}
