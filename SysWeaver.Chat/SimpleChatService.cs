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




    public sealed class SimpleChatRoom : ChatSessionParams
    {
        /// <summary>
        /// Name of the room
        /// </summary>
        public String Name;

        /// <summary>
        /// If non-null speech input will be enabled, listening to this keyword.
        /// </summary>
        public String SpeechName = "All";

        /// <summary>
        /// If true, enable speech by default
        /// </summary>
        public bool EnableSpeechByDefault;

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        public bool AllowUserMarkDown = true;

        /// <summary>
        /// Allow storing files and links on the server (requires a UserStore).
        /// </summary>
        public bool AllowStore = true;

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        public bool CanTranslate = true;

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        public bool CanShowProfile;


    }

    public sealed class SimpleChatParams
    {
        public bool AllowUserCreatedChat;
        /// <summary>
        /// Array of chat "rooms" (or sessions).
        /// Each room is defined like: Name | Tokens | Clear | RemoveMessage
        /// [Name] the name of the char room.
        /// [Tokens] the security token required to join, "-" means no login required, "*" means any logged in user.
        /// [ClearUse | and the required security tokens to join, no '|' at all mean that anyone can write even without logging in (anonymously).
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


    public sealed class SimpleChatService : IChatProvider
    {
        public override string ToString() => "Rooms: " + Rooms.Count;

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
                        msg?.AddMessage("Added chat room " + rn.ToQuoted() + " open to " + rm.AuthString, MessageLevels.Debug);
                    else
                        msg?.AddMessage("Failed to add chat room " + rn.ToQuoted() + ", already registered?", MessageLevels.Warning);
                }
            }
        }
        
        IChatController Controller;
        
        public void OnInit(IChatController controller) 
        {
            Controller = controller;
        }

        readonly ConcurrentDictionary<String, Room> Rooms = new ConcurrentDictionary<string, Room>(StringComparer.Ordinal);

        public string Name => "Simple";

        sealed class Room 
        {
            public Room(SimpleChatRoom room)
            {
                R = room;
                var a = Authorization.GetRequiredTokens(room.Auth);
                Auth = a;
                AuthString = a == null ? "everyone" : (a.Count <= 0 ? "any logged in user" : ("any user with any token of: " + String.Join(", ", a)));
            }
            public readonly SimpleChatRoom R;

            public readonly String AuthString;
                
            public readonly IReadOnlyList<String> Auth;
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
                if (room.Auth != null)
                    throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            if (!auth.IsValid(room.Auth))
                throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            return room;
        }

        public Task<string> CreateNewChat(string type, HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

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
                        }
                        else
                        {
                            var i = BinarySearch.Lower(0, ml, pivotId, i => m[i].Id);
                            if (i >= 0)
                            {
                                while (i < ml)
                                {
                                    var msg = m[ml];
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

        public Task<Chat.ChatMessage[]> GetMessages(string providerChatId, HttpServerRequest request, long pivotId, int maxCount)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            return Task.FromResult(InternalGetMessages(room, request.Session.Auth?.Guid, pivotId, maxCount));
        }

        public Task<ChatJoinResponse> Join(string providerChatId, HttpServerRequest request, long pivotId = 0, int maxCount = 50)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            var session = request.Session;
            var a = session.Auth;
            var msgs = InternalGetMessages(room, a?.Guid, pivotId, maxCount);
            var r = room.R;
            return Task.FromResult(new ChatJoinResponse
            {
                UserName = ChatTools.GetUsername(session),
                Lang = session.Language,
                Messages = msgs,
                CanClear = r.CanClear(a),
                CanRemove = r.CanRemove(a),
                SpeechName = String.IsNullOrEmpty(r.SpeechName) ? null : [r.SpeechName],
                EnableSpeechByDefault = r.EnableSpeechByDefault,
                AllowMarkDown = r.AllowUserMarkDown,
                CanStore = r.AllowStore,
                CanTranslate = r.CanTranslate,
                CanShowProfile = r.CanShowProfile,
            });
        }

        public Task<ChatMessage> GetChatMessage(String providerChatId, long messageId, HttpServerRequest request)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            lock (room)
            {
                room.MessageLookup.TryGetValue(messageId, out var m);
                return Task.FromResult(m);
            }
        }


        public Task<bool> UserMessage(string providerChatId, HttpServerRequest request, ChatMessageBody message)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            if ((message.Format == ChatMessageFormats.MarkDown) && (!room.R.AllowUserMarkDown))
                throw new ArgumentException("Users may not send MarkDown messages!", nameof(message.Format));
            var session = request.Session;
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

        public Task<bool> SetValue(String providerChatId, HttpServerRequest request, long messageId, String key, String value)
        {
            var room = GetAuthenticatedRoom(out var c, providerChatId, request);
            throw new Exception("Variable " + key.ToQuoted() + " is unknown!");
        }


    }
}
