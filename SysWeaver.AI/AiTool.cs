using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An API agnostic function definition, as described to the AI model.
    /// </summary>
    public sealed class AiToolFunction
    {
        public override string ToString() => FunctionName;

        public AiToolFunction(String functionName, String functionDescription, BinaryData functionParameters, bool? functionSchemaIsStrict = null)
        {
            FunctionName = functionName;
            FunctionDescription = functionDescription;
            FunctionParameters = functionParameters;
            FunctionSchemaIsStrict = functionSchemaIsStrict;
        }

        /// <summary>
        /// The name of the function
        /// </summary>
        public readonly String FunctionName;

        /// <summary>
        /// The description of the function (as sent to the AI model)
        /// </summary>
        public readonly String FunctionDescription;

        /// <summary>
        /// The JSON schema of the function parameters (an object), or null if the function takes no parameters
        /// </summary>
        public readonly BinaryData FunctionParameters;

        /// <summary>
        /// If the JSON schema should be strictly followed, null for the API default
        /// </summary>
        public readonly bool? FunctionSchemaIsStrict;

        /// <summary>
        /// Get (or create and cache) an API specific representation of this function.
        /// </summary>
        /// <typeparam name="T">The API specific type</typeparam>
        /// <param name="create">Function used to create the API specific representation</param>
        /// <returns>The API specific representation</returns>
        public T GetApiTool<T>(Func<AiToolFunction, T> create) where T : class
            => (T)ApiTools.GetOrAdd(typeof(T), (_, c) => c(this), create);

        readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Object> ApiTools = new();
    }

    public sealed class AiTool
    {
        public override string ToString() => Name;
        public readonly AiToolFunction Tool;

        public readonly String Name;
        public readonly String Desc;

        public readonly Type Arg;
        public readonly String ArgName;
        public readonly String ArgDesc;

        public readonly Type Ret;
        public readonly String RetDesc;

        public readonly IReadOnlyList<String> Auth;

        public readonly String Icon;

        public readonly String Api;


        public static AiTool Create(String name, IApiHttpServerEndPoint endPoint, AiToolFunction tool, bool exposeApi = false)
            => new AiTool(name, endPoint, tool, exposeApi);

        /// <summary>
        /// Check if a method should be used as a tool (have an AiToolAttribute or an OpenAiUseAttribute)
        /// </summary>
        /// <param name="method">The method</param>
        /// <returns>True if the method is a tool</returns>
        public static bool IsTool(MethodInfo method)
            => (method.GetCustomAttribute<AiToolAttribute>() != null) || (method.GetCustomAttribute<AiUseAttribute>()?.Use ?? false);

        /// <summary>
        /// Get all tool methods (have an AiToolAttribute or an OpenAiUseAttribute) of a type, including private methods declared in base types
        /// </summary>
        /// <param name="type">The type</param>
        /// <returns>The tool methods</returns>
        public static IEnumerable<MethodInfo> GetToolMethods(Type type)
        {
            HashSet<MethodInfo> seen = new();
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var mm in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    //  Overridden methods are only returned once (the most derived)
                    if (!seen.Add(mm.GetBaseDefinition()))
                        continue;
                    if (IsTool(mm))
                        yield return mm;
                }
            }
        }

        /// <summary>
        /// Get the name of a tool (as seen by the AI), uses the AiToolPrefixAttribute and the AiToolNameAttribute
        /// </summary>
        /// <param name="method">The method</param>
        /// <returns>The name of the tool</returns>
        public static String GetToolName(MethodInfo method)
        {
            var type = method.DeclaringType;
            var prefix = type.GetCustomAttribute<AiToolPrefixAttribute>(true)?.ToolPrefix ?? type.Name;
            var name = method.GetCustomAttribute<AiToolNameAttribute>(true)?.ToolName;
            if (String.IsNullOrEmpty(name))
                name = "{0}{1}";
            name = String.Format(name, prefix, method.Name);
            return name;
        }

        /// <summary>
        /// Create a tool from an end point, the function description and parameter schema is created from the end point (and the XML documentation)
        /// </summary>
        /// <param name="fn">The name of the tool</param>
        /// <param name="a">The end point</param>
        /// <param name="exposeApi">If true, the REST API url is added to the description</param>
        /// <returns>A tool</returns>
        public static AiTool FromEndPoint(String fn, IApiHttpServerEndPoint a, bool exposeApi = true)
        {
            a.GetDesc(out var argType, out var retType, out var methodDesc, out var argDesc, out var retDesc, out var argName);
            BinaryData p = null;
            if (argType != null)
                p = JsonSchemaCache.GetBinaryDataParam(argType, argName, false, argDesc);
            if (retType != null)
            {
                bool isArray = retType.IsArray;
                String prefix = "Return type is a";
                if (isArray)
                {
                    retType = retType.GetElementType();
                    prefix = "Return type is an array of";
                }
                if (JsonSchema.TryGetPrim(retType, out var jtype))
                {
                    if (String.IsNullOrEmpty(retDesc))
                        retDesc = String.Concat(prefix, ' ', jtype.ToQuoted());
                    else
                        retDesc = String.Concat(retDesc, ".\n", prefix, ' ', jtype.ToQuoted());
                }
                else
                {
                    retDesc = String.Concat(prefix, isArray ? " objects" : "n object", " with JSON schema:  \n\n```json\n", JsonSchema.ToString(JsonSchema.Get(retType, true, retDesc), true), "\n```\n");
                }
                retDesc = String.Concat("## Tool returns  \n", retDesc);
                methodDesc = String.IsNullOrEmpty(methodDesc) ? retDesc : String.Join(".\n", methodDesc, retDesc);
            }
            if (exposeApi)
            {
                var api = a.Uri;
                if (api != null)
                {
                    methodDesc = methodDesc.Trim().LimitLength(900, "");
                    methodDesc = String.Concat(methodDesc, "\n\n### REST API  \n" + api + "  \n");
                }
            }
            var ct = new AiToolFunction(fn, methodDesc.Trim().LimitLength(1024, ""), p, false);
            return Create(fn, a, ct, exposeApi);
        }

        /// <summary>
        /// Create a tool from a method
        /// </summary>
        /// <param name="instance">Object instance (null for static methods)</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional function name, default is computed using GetToolName</param>
        /// <param name="perfMonitor">An optional performance monitor</param>
        /// <param name="defaultAuth">The default auth to use if there is no WebApiAuthAttribute on the method</param>
        /// <param name="defaultCachedCompression">The default compression when there is no WebApiCompressionAttribute on the method</param>
        /// <param name="defaultCompression">The default compression for uncached cached methods when there is no WebApiCompressionAttribute on the method</param>
        /// <param name="locationPrefix">The location prefix</param>
        /// <returns>A tool</returns>
        public static AiTool FromMethod(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix)
        {
            fn = String.IsNullOrEmpty(fn) ? GetToolName(method) : fn;
            var endPoint = ApiHttpEntry.Create(AiServiceBase.IoParams, instance, method, fn, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression, locationPrefix);
            return FromEndPoint(fn, endPoint, false);
        }

        /// <summary>
        /// Create a tool that is defined (and executed) by a caller, used to describe the tool to the model only (it can't be invoked)
        /// </summary>
        /// <param name="tool">The function definition</param>
        /// <returns>A tool</returns>
        public static AiTool CreateExternal(AiToolFunction tool)
            => new AiTool(tool);

        AiTool(AiToolFunction tool)
        {
            Name = tool.FunctionName;
            Tool = tool;
            Desc = tool.FunctionDescription;
            Icon = "";
            Invoke = (p, request) => throw new NotSupportedException("The tool " + Name.ToQuoted() + " is defined by the caller and can't be invoked");
        }

        AiTool(String name, IApiHttpServerEndPoint endPoint, AiToolFunction tool, bool exposeApi)
        {
            Auth = endPoint.Auth;
            Name = name;
            Tool = tool;
            endPoint.GetDesc(out var arg, out var ret, out var md, out var ad, out var rd, out var an);
            if (exposeApi)
                Api = endPoint.Uri;
            Desc = md;
            Arg = arg;
            ArgName = an;
            ArgDesc = ad;
            Ret = ret;
            RetDesc = rd;
            var attr = endPoint.MethodInfo.GetCustomAttribute<AiToolAttribute>(true);
            Icon = attr?.Icon ?? "";
            if (arg != null)
            {
                Invoke = async (p, request) =>
                {
                    Byte[] mem = null;
                    if (p != null)
                    {
                        var src = p.ToString();
                        var i = src.IndexOf(':');
                        if (i > 0)
                        {
                            src = src.Substring(i + 1, src.Length - 2 - i);
                            mem = Encoding.UTF8.GetBytes(src);
                            if (mem.Length <= 0)
                                mem = null;
                        }
                    }
                    var res = await endPoint.InvokeAsync(request, mem).ConfigureAwait(false);
                    var text = Encoding.UTF8.GetString(res.Span);
                    return text;
                };
            }else
            {
                Invoke = async (p, request) =>
                {
                    var res = await endPoint.InvokeAsync(request, null).ConfigureAwait(false);
                    var text = Encoding.UTF8.GetString(res.Span);
                    return text;
                };

            }
            AsCommand = new AiCommand(Name, Auth, async (args, s, r) =>
            {
                var m = await Invoke(String.IsNullOrEmpty(args) ? null : BinaryData.FromString(String.Join(args, "x:", ';')), r).ConfigureAwait(false);
                m = AiTools.BeautifyJson(m);
                return new Chat.ChatMessage
                {
                    Text = String.Join(m, "```json\n", "\n```"),
                    Format = Chat.ChatMessageFormats.MarkDown
                };
            }, arg == null ? null : "<json data>", Desc);
        }

        /// <summary>
        /// Invoke the tool, the first argument is the function arguments as supplied by the AI (a json object with a single property)
        /// </summary>
        public readonly Func<BinaryData, HttpServerRequest, Task<String>> Invoke;

        public readonly AiCommand AsCommand;

    }

}
