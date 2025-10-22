using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.AI
{
    public sealed class OpenAiTool
    {
        public override string ToString() => Name;
        public readonly ChatTool Tool;

        public readonly String Name;
        public readonly String Desc;

        public readonly Type Arg;
        public readonly String ArgName;
        public readonly String ArgDesc;

        public readonly Type Ret;
        public readonly String RetDesc;

        public readonly IReadOnlyList<String> Auth;

        public readonly String Icon;

        public static OpenAiTool Create(String name, IApiHttpServerEndPoint endPoint, ChatTool tool)
            => new OpenAiTool(name, endPoint, tool);

        OpenAiTool(String name, IApiHttpServerEndPoint endPoint, ChatTool tool)
        {
            Auth = endPoint?.Auth;
            Name = name;
            Tool = tool;
            endPoint.GetDesc(out var arg, out var ret, out var md, out var ad, out var rd, out var an);
            Desc = md;
            Arg = arg;
            ArgName = an;
            ArgDesc = ad;
            Ret = ret;
            RetDesc = rd;
            var attr = endPoint.MethodInfo.GetCustomAttribute<OpenAiToolAttribute>(true);
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
            AsCommand = new OpenAiCommand(Name, Auth, async (args, s, r) =>
            {
                var m = await Invoke(String.IsNullOrEmpty(args) ? null : BinaryData.FromString(String.Join(args, "x:", ';')), r).ConfigureAwait(false);
                m = OpenAiTools.BeautifyJson(m);
                return new Chat.ChatMessage
                {
                    Text = String.Join(m, "```json\n", "\n```"),
                    Format = Chat.ChatMessageFormats.MarkDown
                };
            }, arg == null ? null : "<json data>", Desc);
        }


        public static OpenAiTool Create(Object instance, MethodInfo method, ChatTool tool, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix)
        {
            var name = String.Join('_', instance.GetType().Name, method.Name);
            var endPoint = ApiHttpEntry.Create(OpenAiService.IoParams, instance, method, name, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression, locationPrefix);
            return new OpenAiTool(name, endPoint, tool);
        }

   
        public readonly Func<BinaryData, HttpServerRequest, Task<String>> Invoke;

        public readonly OpenAiCommand AsCommand;





    }



}
