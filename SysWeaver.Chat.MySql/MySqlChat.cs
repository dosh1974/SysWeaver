using SysWeaver;
using SysWeaver.Db;


using System.Threading.Tasks;
using SysWeaver.Chat.MySql;
using System;
using SysWeaver.Net;
using SysWeaver.Auth;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using SimpleStack.Orm;
using SysWeaver.MicroService;

namespace SysWeaver.Chat
{


    [IsMicroService]
    [OptionalDep<IUserStorageService>]
    public class MySqlChat : IChatProvider
    {
        public MySqlChat(ServiceManager manager, MySqlChatParams p)
        {
            p = p ?? new MySqlChatParams();
            Storage = manager.TryGet<IUserStorageService>();
            Name = p.ProviderId ?? "MySql";
            Db = new MySqlDbSimpleStack(p);
            InitDb(Db, p.Rooms).RunAsync();
        }

        readonly IUserStorageService Storage;

        public void AddRoomProvider(IMySqlRoomProvider provider)
        {
            lock (RoomProviders)
            {
                RoomProviders.Add(provider);
            }
        }

        public void RemoveRoomProvider(IMySqlRoomProvider provider)
        {
            lock (RoomProviders)
            {
                RoomProviders.Remove(provider);
            }
        }


        readonly MySqlDbSimpleStack Db;

        async Task InitDb(MySqlDbSimpleStack db, MySqlChatRoom[] rooms)
        {
            await db.Init().ConfigureAwait(false);
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbChatRoom>(c).ConfigureAwait(false);
            }
            foreach (var r in rooms.Nullable())
                await InitRoom(r, true).ConfigureAwait(false);
        }

        readonly AsyncLock RoomsLock = new AsyncLock();

        public async ValueTask<bool> InitRoom(MySqlChatRoom room, bool createTable = true)
        {
            var rooms = Rooms;
            var name = room.Name;
            if (name.Length <= 0)
                throw new Exception("Invalid room name!");
            if (name.Length > 64)
                throw new Exception("Room name too long!");
            if (rooms.TryGetValue(name, out var r))
                return true;
            using var lck = await RoomsLock.Lock().ConfigureAwait(false);
            if (rooms.TryGetValue(name, out r))
                return true;
            using (var con = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await con.BeginTransactionAsync().ConfigureAwait(false);
                var t = await con.FirstOrDefaultAsync<DbChatRoom>(x => x.Name == name).ConfigureAwait(false);
                var now = DateTime.UtcNow;
                if (t == null)
                {
                    await con.InsertAsync(new DbChatRoom
                    {
                        Name = name,
                        Created = now,
                        Used = now,
                    }).ConfigureAwait(false);
                }else
                {
                    t.Used = now;
                    await con.UpdateAsync(t, x => new { x.Used }).ConfigureAwait(false);
                }
                if (createTable)
                    await con.CreateTableAsync<DbChatMessage>(false, default, ToTableName(name)).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            r = new Room(room);
            rooms.TryAdd(name, r);
            return true;
        }


        static String ToTableName(String roomName)
            => "Room_" + roomName;


        sealed class Room
        {
            public Room(MySqlChatRoom room)
            {
                R = room;
                var a = Authorization.GetRequiredTokens(room.Auth);
                Auth = a;
                AuthString = a == null ? "everyone" : (a.Count <= 0 ? "any logged in user" : ("any user with any token of: " + String.Join(", ", a)));
                TableName = ToTableName(room.Name);
                var l = room.ServiceLimiter;
                ServerLimiter = l == null ? null : new HttpRateLimiter(l);
                room.SessionLimiter?.Validate();
            }

            public ValueTask<IDisposable> Lock() => IntLock.Lock();

            public AsyncLock IntLock = new AsyncLock();

            public volatile bool HaveTable;

            public readonly HttpRateLimiter ServerLimiter;


            public HttpRateLimiter SessionRateLimiter(HttpSession session)
            {
                var l = R.SessionLimiter;
                if (l == null)
                    return null;
                var t = session.GetOrCreate("ChatRateLimits", () => new ConcurrentDictionary<String, HttpRateLimiter>(StringComparer.Ordinal));
                var k = R.Name;
                if (t.TryGetValue(k, out var limiter))
                    return limiter;
                limiter = new HttpRateLimiter(l);
                if (!t.TryAdd(k, limiter))
                    limiter = t[k];
                return limiter;
            }

            public async ValueTask<bool> CheckTable(DbSimpleStack db, OrmConnection c)
            {
                if (HaveTable)
                    return true;
                var name = TableName;
                if (!await c.TableExistsAsync(name).ConfigureAwait(false))
                    return false;
                await db.ValidateTable<DbChatMessage>(c, name).ConfigureAwait(false);
                HaveTable = true;
                return true;
            }

            public readonly String TableName;
            public readonly MySqlChatRoom R;
            public readonly String AuthString;
            public readonly IReadOnlyList<String> Auth;
        }

        async ValueTask EnsureRoom(Room r)
        {
            if (r.HaveTable)
                return;
            using var lck = await RoomsLock.Lock().ConfigureAwait(false);
            if (r.HaveTable)
                return;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                await c.CreateTableAsync<DbChatMessage>(false, default, r.TableName).ConfigureAwait(false);
            r.HaveTable = true;
        }

        readonly List<IMySqlRoomProvider> RoomProviders = new();


        async ValueTask<MySqlChatRoom> GetRoomDef(string providerChatId)
        {
            IMySqlRoomProvider[] provs;
            lock (RoomProviders)
                provs = RoomProviders.ToArray();
            foreach (var x in provs)
            {
                var def = await x.GetRoom(providerChatId).ConfigureAwait(false);
                if (def != null)
                    return def;
            }
            return null;
        }

        async ValueTask<Room> GetAuthenticatedRoom(string providerChatId, HttpServerRequest request)
        {
            if (Controller == null)
                throw new Exception(nameof(SimpleChatService).ToQuoted() + " is not registered to any " + nameof(ChatService).ToQuoted());
            var rooms = Rooms;
            if (!rooms.TryGetValue(providerChatId, out var room))
            {
                using var lck = await RoomsLock.Lock().ConfigureAwait(false);
                if (!rooms.TryGetValue(providerChatId, out room))
                {
                    var def = await GetRoomDef(providerChatId).ConfigureAwait(false);
                    if (def == null)
                        throw new ArgumentException("No chat room named " + providerChatId.ToQuoted(), nameof(providerChatId));

                    DbChatRoom d;
                    using (var c = await Db.GetAsync().ConfigureAwait(false))
                    {
                        d = await c.FirstOrDefaultAsync<DbChatRoom>(x => x.Name == providerChatId).ConfigureAwait(false);
                        var now = DateTime.UtcNow;
                        if (d == null)
                        {
                            d = new DbChatRoom
                            {
                                Name = providerChatId,
                                Created = now,
                                Used = now,
                            };
                            await c.InsertAsync(d).ConfigureAwait(false);
                            room = new Room(def);
                            rooms.TryAdd(providerChatId, room);
                        }
                        else
                        {
                            d.Used = now;
                            await c.UpdateAsync(d, x => new { x.Used }).ConfigureAwait(false);
                            room = new Room(def);
                            rooms.TryAdd(providerChatId, room);
                        }
                    }
                }
            }
            var auth = request.Session?.Auth;
            if (auth == null)
                if (room.Auth != null)
                    throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            if (!auth.IsValid(room.Auth))
                throw new Exception("Session is not authorized to acccess room " + providerChatId.ToQuoted());
            return room;
        }

        readonly ConcurrentDictionary<String, Room> Rooms = new ConcurrentDictionary<string, Room>(StringComparer.Ordinal);

        IChatController Controller;

        #region IChatProvider


        /// <summary>
        /// Called once when the chat service registers the instance.
        /// The supplied controller is used to push actions back to clients.
        /// </summary>
        /// <param name="controller">The controller to use for pushing actions to clients</param>
        public void OnInit(IChatController controller)
        {
            Controller = controller;
        }




        public string Name { get; init; }

        public Task<String> CreateNewChat(String type, HttpServerRequest request) => TaskExt.NullStringTask;

        public async Task<bool> Clear(string providerChatId, HttpServerRequest request)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var session = request.Session;
            if (!room.R.CanClear(session.Auth))
                throw new Exception("Not allowed to clear the chat session!");
            var table = room.TableName;
            var db = Db;
            using var lck = await room.Lock().ConfigureAwait(false);
            using var con = await db.GetAsync().ConfigureAwait(false);
            if (!await room.CheckTable(db, con).ConfigureAwait(false))
                return true;
            await con.DeleteAllAsync<DbChatMessage>(x =>
            {
                x.From(table);
            }).ConfigureAwait(false);
            Controller.ClearAllMessages(providerChatId, session, ChatScopes.Global);
            return true;
        }

        public async Task<bool> RemoveMessage(string providerChatId, long messageId, HttpServerRequest request)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var session = request.Session;
            var auth = session?.Auth;
            var table = room.TableName;
            var db = Db;
            using var lck = await room.Lock().ConfigureAwait(false);
            using var con = await db.GetAsync().ConfigureAwait(false);
            if (!await room.CheckTable(db, con).ConfigureAwait(false))
                return false;
            var m = await con.FirstOrDefaultAsync<DbChatMessage>(x =>
            {
                x.From(table);
                x.Where(y => y.Id == messageId);
            }).ConfigureAwait(false);
            if (m == null)
                throw new Exception("Unknown message id #" + messageId);
            if (!room.R.CanClear(auth))
                throw new Exception("Not allowed to delete the chat message!");
            await con.DeleteAllAsync<DbChatMessage>(x =>
            {
                x.From(table);
                x.Where(y => y.Id == messageId);
            }).ConfigureAwait(false);
            Controller.RemoveMessage(providerChatId, messageId, session, ChatScopes.Global);
            return true;
        }

        public async Task<long> GetCurrentId(string providerChatId, HttpServerRequest request)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var table = room.TableName;
            var db = Db;
            using var con = await db.GetAsync().ConfigureAwait(false);
            if (!await room.CheckTable(db, con).ConfigureAwait(false))
                return 0;
            var m = await con.FirstOrDefaultAsync<DbChatMessage>(x =>
            {
                x.From(table);
                x.OrderByDescending(y => y.Id);
                x.Select(y => y.Id);
            }).ConfigureAwait(false);
            return m.Id;
        }

        static ChatMessage ToMessage(DbChatMessage m) 
            =>
            m == null ? null : new ChatMessage
            {
                Id = m.Id,
                Data = m.Data,
                Format = (ChatMessageFormats)m.Format,
                From = m.From,
                FromImage = m.FromImage,
                Text = m.Text,
                Time = m.Time,
                Lang = m.Lang,
            };

        async ValueTask<Chat.ChatMessage[]> InternalGetMessages(Room room, String guid, long pivotId, int maxCount)
        {
            bool reverse = maxCount < 0;
            if (reverse)
                maxCount = -maxCount;
            var table = room.TableName;
            var db = Db;
            using (var con = await db.GetAsync().ConfigureAwait(false))
            {
                if (await room.CheckTable(db, con).ConfigureAwait(false))
                {
                    List<DbChatMessage> msg;
                    if (pivotId <= 0)
                    {
                        msg = (await con.SelectAsync<DbChatMessage>(x =>
                        {
                            x.From(table);
                            x.OrderByDescending(y => y.Id);
                            x.Limit(maxCount);
                        }).ConfigureAwait(false)).ToList();
                        msg.Reverse();
                    }
                    else
                    {
                        if (reverse)
                        {
                            msg = (await con.SelectAsync<DbChatMessage>(x =>
                            {
                                x.From(table);
                                x.Where(y => y.Id < pivotId);
                                x.OrderByDescending(y => y.Id);
                                x.Limit(maxCount);
                            }).ConfigureAwait(false)).ToList();
                            msg.Reverse();
                        }
                        else
                        {
                            msg = (await con.SelectAsync<DbChatMessage>(x =>
                            {
                                x.From(table);
                                x.Where(y => y.Id >= pivotId);
                                x.OrderBy(y => y.Id);
                                x.Limit(maxCount);
                            }).ConfigureAwait(false)).ToList();
                        }
                    }
                    return msg.Convert(ToMessage);
                }
            }
            return Array.Empty<Chat.ChatMessage>();
        }

        public async Task<Chat.ChatMessage[]> GetMessages(string providerChatId, HttpServerRequest request, long pivotId, int maxCount)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            return await InternalGetMessages(room, request.Session.Auth?.Guid, pivotId, maxCount).ConfigureAwait(false);
        }

        public async Task<ChatJoinResponse> Join(string providerChatId, HttpServerRequest request, long pivotId = 0, int maxCount = 50)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var session = request.Session;
            var a = session.Auth;
            var msgs = await InternalGetMessages(room, a?.Guid, pivotId, maxCount).ConfigureAwait(false);
            var r = room.R;
            var repo = r.UploadRepo;
            return new ChatJoinResponse
            {
                UserName = ChatTools.GetUsername(session),
                MaxTextLength = 4096,
                MaxDataLength = 2048,
                Lang = session.Language,
                Messages = msgs,
                CanClear = r.CanClear(a),
                CanRemove = r.CanRemove(a),
                SpeechName = String.IsNullOrEmpty(r.SpeechName) ? null : [r.SpeechName],
                EnableSpeechByDefault = r.EnableSpeechByDefault,
                MaxDataCount = Math.Max(0, r.MaxDataCount),
                AllowMarkDown = r.AllowUserMarkDown,
                CanStore = r.AllowStore,
                CanTranslate = r.CanTranslate,
                CanShowProfile = r.CanShowProfile,
                UploadRepo = (!String.IsNullOrEmpty(repo)) && (Storage != null) && (a != null) && (r.MaxDataCount > 0) ? repo : null,
            };
        }

        public async Task<ChatMessage> GetChatMessage(String providerChatId, long messageId, HttpServerRequest request)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var table = room.TableName;
            var db = Db;
            DbChatMessage m;
            using (var con = await db.GetAsync().ConfigureAwait(false))
            {
                if (!await room.CheckTable(db, con).ConfigureAwait(false))
                    return null;
                m = await con.FirstOrDefaultAsync<DbChatMessage>(x =>
                {
                    x.From(table);
                    x.Where(y => y.Id == messageId);
                }).ConfigureAwait(false);
            }
            return ToMessage(m);
        }


        public async Task<bool> UserMessage(string providerChatId, HttpServerRequest request, ChatMessageBody message)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            var l = room.ServerLimiter;
            if ((l != null) && (await l.IsOverTheLimit().ConfigureAwait(false)))
                throw new HttpResponseException(429);
            var session = request.Session;
            l = room.SessionRateLimiter(session);
            if ((l != null) && (await l.IsOverTheLimit().ConfigureAwait(false)))
                throw new HttpResponseException(429);

            var table = room.TableName;
            if ((message.Format == ChatMessageFormats.MarkDown) && (!room.R.AllowUserMarkDown))
                throw new ArgumentException("Users may not send MarkDown messages!", nameof(message.Format));
            DbChatMessage m = new DbChatMessage
            {
                From = ChatTools.GetUsername(session),
                FromImage = session?.Auth?.GetUserImage(),
                Text = message.Text.LimitLength(4096, ""),
                Data = message.Data.LimitLength(2048, ""),
                Format = (Byte)message.Format,
                Time = DateTime.UtcNow,
                Lang = message.Lang ?? request.Language,
            };
            await EnsureRoom(room).ConfigureAwait(false);
            using (var con = await Db.GetAsync().ConfigureAwait(false))
                m.Id = await con.InsertAsync<long, DbChatMessage>(m, table).ConfigureAwait(false);
            var mm = ToMessage(m);
            Controller.PostMessage(providerChatId, mm, session, ChatScopes.Global);
            return true;
        }

        public async Task<bool> SetValue(String providerChatId, HttpServerRequest request, long messageId, String key, String value)
        {
            var room = await GetAuthenticatedRoom(providerChatId, request).ConfigureAwait(false);
            throw new Exception("Variable " + key.ToQuoted() + " is unknown!");
        }



        #endregion//IChatProvider

    }


}
