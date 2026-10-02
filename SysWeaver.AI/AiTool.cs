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
