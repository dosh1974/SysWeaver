using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed class OpenAiCommand
    {
        public override string ToString() => Name;
        public readonly String Name;
        public readonly IReadOnlyList<String> Auth;
        public Func<String, OpenAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> Fn;
        public readonly String Args;
        public readonly String Desc;

        public OpenAiCommand(string name, IReadOnlyList<string> auth, Func<string, OpenAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> fn, string args, string desc)
        {
            Name = name;
            Auth = auth;
            Fn = fn;
            Args = args;
            Desc = desc;
        }
    }

}
