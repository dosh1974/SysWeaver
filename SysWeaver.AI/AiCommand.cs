using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A chat command (ex: "/tools") that a user can run in a chat session
    /// </summary>
    public sealed class AiCommand
    {
        /// <inheritdoc />
        public override string ToString() => Name;

        /// <summary>
        /// The name of the command
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The auth required to run the command
        /// </summary>
        public readonly IReadOnlyList<String> Auth;

        /// <summary>
        /// The function that runs the command (arguments, session, request), returns the message to show
        /// </summary>
        public Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> Fn;

        /// <summary>
        /// A description of the arguments (ex: "&lt;tool name&gt;"), null if none
        /// </summary>
        public readonly String Args;

        /// <summary>
        /// A description of what the command does
        /// </summary>
        public readonly String Desc;

        /// <summary>
        /// Create a command
        /// </summary>
        /// <param name="name">The name of the command</param>
        /// <param name="auth">The auth required to run the command</param>
        /// <param name="fn">The function that runs the command</param>
        /// <param name="args">A description of the arguments, null if none</param>
        /// <param name="desc">A description of what the command does</param>
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
