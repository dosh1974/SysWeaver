using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.Chat
{




    /// <summary>
    /// A chat room of the <see cref="SimpleChatService"/>, the <see cref="ChatSessionParams.Name"/> is the room name.
    /// </summary>
    public sealed class SimpleChatRoom : ChatSessionParams
    {
    }

    /// <summary>
    /// Parameters for the <see cref="SimpleChatService"/>
    /// </summary>
    public sealed class SimpleChatParams
    {
        /// <summary>
        /// Not used by <see cref="SimpleChatService"/> (it doesn't support user created chats).
        /// </summary>
        public bool AllowUserCreatedChat;
        /// <summary>
        /// The chat rooms (or sessions), rooms with an empty name are ignored.
        /// Access to each room is defined by the <see cref="ChatSessionParams"/> members (ex: <see cref="ChatSessionParams.Auth"/>).
        /// </summary>
        public SimpleChatRoom[] Rooms = [
            
            new SimpleChatRoom
            {
                Name = "Generic",
            },
            new SimpleChatRoom
            {
                Name = "Admins",
                Auth = "Admin",
            },
        ];
    }


    /// <summary>
    /// A simple in memory chat provider with a fixed set of chat rooms (messages are lost on restart).
    /// </summary>
    public sealed class SimpleChatService : IChatProvider
    {
        /// <inheritdoc/>
        public override string ToString() => "Rooms: " + Rooms.Count;

        /// <summary>
        /// Create a simple chat service
        /// </summary>
        /// <param name="msg">Optional message host, used to log the rooms that are added</param>
        /// <param name="p">Parameters, null uses the defaults</param>
        public SimpleChatService(IMessageHost msg = null, SimpleChatParams p = null)
        {
            p = p ?? new SimpleChatParams();
            var r = p.Rooms;
            var rooms = Rooms;
            if (r != null)
            {
                foreach (var room in r)
                {
                    var rn = room.Name?.Trim();
                    if (String.IsNullOrEmpty(rn))
                        continue;
                    var rm = new Room(room);
                    if (rooms.TryAdd(rn, rm))
                        msg?.AddMessage(MessageLevels.Debug, $"Added chat room {rn.ToQuoted()} open to {rm.AuthString}");
                    else
                        msg?.AddMessage(MessageLevels.Warning, $"Failed to add chat room {rn.ToQuoted()}, already registered?");
                }
            }
        }
        
        IChatController Controller;
        
        /// <inheritdoc/>
        public void OnInit(IChatController controller)
        {
            Controller = controller;
        }

        readonly ConcurrentDictionary<String, Room> Rooms = new ConcurrentDictionary<string, Room>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public string Name => "Simple";

        sealed class Room 
        {
            public Room(SimpleChatRoom room)
            {
                R = room;
                var a = Authorization.GetRequiredTokens(room.Auth);
                Auth = a;
                AuthString = a == null ? "everyone" : (a.Count <= 0 ? "any logged in user" : ("any user with any token of: " + String.Join(", ", a)));
                a = Authorization.GetRequiredTokens(room.PostAuth ?? room.Auth);
                PostAuth = a;
                PostAuthString = a == null ? "everyone" : (a.Count <= 0 ? "any logged in user" : ("any user with any token of: " + String.Join(", ", a)));

            }
            public readonly SimpleChatRoom R;

            public readonly String AuthString;
            public readonly IReadOnlyList<String> Auth;

            public readonly String PostAuthString;
            public readonly IReadOnlyList<String> PostAuth;

            public readonly List<ChatMessage> Messages = new List<ChatMessage>();
            public readonly Dictionary<long, ChatMessage> MessageLookup = new Dictionary<long, ChatMessage>();
            public long MsgId;
        }



        Room GetAuthenticatedRoom(out IChatController controller, string providerChatId, HttpServerRequest request)
        {
            controller = Controller;
            if (controller == null)
                throw new Exception(nameof(SimpleChatService).ToQuoted() + " is not registered to any " + nameof(ChatService).ToQuoted());
            if (!Rooms.TryGetValue(providerChatId, out Room room))
                throw new ArgumentException("No chat room named " + providerChatId.ToQuoted(), nameof(providerChatId));
            var auth = request.Session?.Auth;
            if (auth == null)
            {
                if (room.Auth != null)
                    throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            }
            else
            {
                if (!auth.IsValid(room.Auth))
                    throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            }
            return room;
        }

        /// <summary>
        /// Not supported, always throws.
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public Task<string> CreateNewChat(string type, HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<bool> Clear(string providerChatId, HttpServerRequest request)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            var session = request.Session;
            if (!room.R.CanClear(session.Auth))
                throw new Exception("Not allowed to clear the chat session!");
            lock (room)
            {
                if (room.Messages.Count > 0)
                {
                    room.Messages.Clear();
                    room.MessageLookup.Clear();
                    c.ClearAllMessages(providerChatId, session, ChatScopes.Global);
                }
            }
            return TaskExt.TrueTask;
        }
        
        /// <inheritdoc/>
        public Task<bool> RemoveMessage(string providerChatId, long messageId, HttpServerRequest request)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            var session = request.Session;
            var auth = session?.Auth;
            lock (room)
            {
                if (!room.MessageLookup.TryGetValue(messageId, out var m))
                    throw new Exception("Unknown message id #" + messageId);
                if (!m.IsFor(auth?.Guid))
                {
                    if (!room.R.CanClear(auth))
                        throw new Exception("Not allowed to delete the chat message!");
                }
                if (!room.MessageLookup.TryRemove(messageId, out m))
                    throw new Exception("Internal error!");
                room.Messages.Remove(m);
                c.RemoveMessage(providerChatId, messageId, session, ChatScopes.Global);
            }
            return TaskExt.TrueTask;
        }

        /// <inheritdoc/>
        public Task<long> GetCurrentId(string providerChatId, HttpServerRequest request)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            return Task.FromResult(Interlocked.Read(ref room.MsgId));
        }


        static Chat.ChatMessage[] InternalGetMessages(Room s, String guid, long pivotId, int maxCount)
        {
            bool reverse = maxCount < 0;
            if (reverse)
                maxCount = -maxCount;
            List<Chat.ChatMessage> ret = new(maxCount);
            var m = s.Messages;
            var ml = m.Count;
            lock (m)
            {
                if (ml > 0)
                {
                    if (pivotId <= 0)
                    {
                        while (ml > 0)
                        {
                            --ml;
                            var msg = m[ml];
                            if (msg.IsFor(guid))
                            {
                                ret.Add(msg);
                                if (ret.Count >= maxCount)
                                    break;
                            }
                        }
                        ret.Reverse();
                    }
                    else
                    {
                        var max = m[ml - 1].Id;
                        if (reverse)
                        {
                            var i = BinarySearch.Upper(0, ml, pivotId, i => m[i].Id);
                            if (i >= 0)
                            {
                                while (i > 0)
                                {
                                    --i;
                                    var msg = m[i];
                                    if (msg.IsFor(guid))
                                    {
                                        ret.Add(msg);
                                        if (ret.Count >= maxCount)
                                            break;
                                    }
                                }
                                ret.Reverse();
                            }
                        }
                        else
                        {
                            var i = BinarySearch.Lower(0, ml, pivotId, i => m[i].Id);
                            if (i >= 0)
                            {
                                while (i < ml)
                                {
                                    var msg = m[i];
                                    if (msg.IsFor(guid))
                                    {
                                        ret.Add(msg);
                                        if (ret.Count >= maxCount)
                                            break;
                                    }
                                    ++i;
                                }
                            }
                        }
                    }
                }
            }
            return ret.ToArray();
        }

        /// <inheritdoc/>
        public Task<Chat.ChatMessage[]> GetMessages(string providerChatId, HttpServerRequest request, long pivotId, int maxCount)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            return Task.FromResult(InternalGetMessages(room, request.Session.Auth?.Guid, pivotId, maxCount));
        }

        /// <inheritdoc/>
        public Task<ChatJoinResponse> Join(string providerChatId, HttpServerRequest request, long pivotId = 0, int maxCount = 50)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            var session = request.Session;
            var a = session.Auth;
            var msgs = InternalGetMessages(room, a?.Guid, pivotId, maxCount);
            return Task.FromResult(new ChatJoinResponse(room.R, session, msgs));
        }

        /// <inheritdoc/>
        public Task<ChatMessage> GetChatMessage(String providerChatId, long messageId, HttpServerRequest request)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            lock (room)
            {
                room.MessageLookup.TryGetValue(messageId, out var m);
                return Task.FromResult(m);
            }
        }


        /// <inheritdoc/>
        public Task<bool> UserMessage(string providerChatId, HttpServerRequest request, ChatMessageBody message)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            var session = request.Session;
            if (!room.R.CanPost(session.Auth))
                throw new Exception("Not allowed to post messages to the chat session!");
            if ((message.Format == ChatMessageFormats.MarkDown) && (!room.R.AllowUserMarkDown))
                throw new ArgumentException("Users may not send MarkDown messages!", nameof(message.Format));
            ChatMessage m;
            lock (room)
            {
                m = new ChatMessage
                {
                    Id = Interlocked.Increment(ref room.MsgId),
                    From = ChatTools.GetUsername(session),
                    FromImage = session?.Auth?.GetUserImage(),
                    Text = message.Text,
                    Data = message.Data,
                    Format = message.Format,
                    Lang = message.Lang ?? request.Language,
                    Time = DateTime.UtcNow,
                };
                room.Messages.Add(m);
                room.MessageLookup[m.Id] = m;
            }
            c.PostMessage(providerChatId, m, session, ChatScopes.Global);
            return TaskExt.TrueTask;
        }

        /// <summary>
        /// Not supported, always throws (no variables exist).
        /// </summary>
        /// <exception cref="Exception">Always (or if the room is unknown / not accessible)</exception>
        public Task<bool> SetValue(String providerChatId, HttpServerRequest request, long messageId, String key, String value)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            throw new Exception("Variable " + key.ToQuoted() + " is unknown!");
        }


    }
}
