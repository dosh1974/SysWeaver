using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed class AiCommand
    {
        public override string ToString() => Name;
        public readonly String Name;
        public readonly IReadOnlyList<String> Auth;
        public Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> Fn;
        public readonly String Args;
        public readonly String Desc;

        public AiCommand(string name, IReadOnlyList<string> auth, Func<string, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> fn, string args, string desc)
        {
            Name = name;
            Auth = auth;
            Fn = fn;
            Args = args;
            Desc = desc;
        }
    }

}
