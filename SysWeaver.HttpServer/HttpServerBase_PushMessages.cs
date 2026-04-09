using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Security;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {


        #region Push messages

        /// <summary>
        /// Push a message to all sessions that have a logged in user
        /// </summary>
        /// <param name="message"></param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messaged will be sent</param>
        /// <param name="validateAuth">If true, auth must be the same when sending response as when it was pushed</param>
        public void PushMessageAllUsers(PushMessage message, bool onlyLatest = true, bool validateAuth = true)
        {
            foreach (var x in UserSessions)
            {
                foreach (var y in x.Value.Sessions)
                    y.Key.PushMessage(message, onlyLatest, validateAuth);
            }
        }

        /// <summary>
        /// Push a message to all sessions that has the specified user logeed in
        /// </summary>
        /// <param name="userGuid">The auth guid for the user</param>
        /// <param name="message"></param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messaged will be sent</param>
        /// <param name="validateAuth">If true, auth must be the same when sending response as when it was pushed</param>
        public void PushMessageUser(String userGuid, PushMessage message, bool onlyLatest = true, bool validateAuth = true)
        {
            if (userGuid == null)
                return;
            if (!UserSessions.TryGetValue(userGuid, out var x))
                return;
            foreach (var y in x.Sessions)
                y.Key.PushMessage(message, onlyLatest, validateAuth);
        }

        /// <summary>
        /// Push a message to all sessions that has the "current" user logeed in
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messaged will be sent</param>
        /// <param name="validateAuth">If true, auth must be the same when sending response as when it was pushed</param>
        public void PushMessageUser(HttpSession session, PushMessage message, bool onlyLatest = true, bool validateAuth = true) => PushMessageUser(session?.Auth?.Guid, message, onlyLatest, validateAuth);

        /// <summary>
        /// Push a message to all sessions
        /// </summary>
        /// <param name="message"></param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messaged will be sent</param>
        /// <param name="validateAuth">If true, auth must be the same when sending response as when it was pushed</param>
        public void PushMessageAllSessions(PushMessage message, bool onlyLatest = true, bool validateAuth = true)
        {
            foreach (var x in Sessions)
                x.Value.PushMessage(message, onlyLatest, validateAuth);
        }



        /// <summary>
        /// Call this when the client for this session should invalidate some files
        /// </summary>
        /// <param name="session">The session to force reload from</param>
        /// <param name="urls">The urls to refresh, this must be from the root.
        /// Ex: "Auth/userImage".
        /// </param>
        public void OnSessionFilesChanged(HttpSession session, params String[] urls)
            => session.PushMessage(new PushMessageStringArrayValue("FileReload", urls), false);

        /// <summary>
        /// Call this when some file associated with the currently logged in user is changed.
        /// Note that the refresh will happen on all clients that the user is logged on into.
        /// </summary>
        /// <param name="session">The session to force reload from</param>
        /// <param name="urls">The urls to refresh, this must be from the root.</param>
        public void OnUserFilesChanged(HttpSession session, params String[] urls)
        {
            var a = session.Auth;
            var c = new PushMessageStringArrayValue("FileReload", urls);
            if (a == null)
                session.PushMessage(c, false);
            else
                PushMessageUser(session, c, false);
        }


        /// <summary>
        /// Call this when some file associated with the currently logged in user is changed.
        /// Note that the refresh will happen on all clients that the user is logged on into.
        /// </summary>
        /// <param name="userGuid">Guid of the user</param>
        /// <param name="urls">The urls to refresh, this must be from the root.</param>
        public void OnUserFilesChanged(String userGuid, params String[] urls)
            => PushMessageUser(userGuid, new PushMessageStringArrayValue("FileReload", urls), false);

        /// <summary>
        /// Call this when some file that all session are using.
        /// </summary>
        /// <param name="urls">The urls to refresh, this must be from the root.</param>
        public void OnGlobalFilesChanged(params String[] urls)
            => PushMessageAllSessions(new PushMessageStringArrayValue("FileReload", urls), false);


        /// <summary>
        /// Messages that are always sent to clients, must be lowercased
        /// </summary>
        public static readonly String[] ForcedMessages =
        [
            "user.logout",
            "user.login",
            "server.shutdown",
            "server.pause",
            "server.continue",
            "server.restart",
            "reload",
            "refresh",
            "filereload",
#if DEBUG
            "test",
            "testa",
            "testb",
            "testc",
#endif//DEBUG
        ];

        /// <summary>
        /// Send this message to reload windows
        /// </summary>
        public static readonly PushMessage MessageReload = new PushMessage("reload");

        /// <summary>
        /// Refresh.. sent on lanmguage change
        /// </summary>
        public static readonly PushMessage MessageRefresh = new PushMessage("refresh");

        static readonly PushMessage MessageUserLogIn = new PushMessage("user.login");


        static readonly PushMessage MessageServerShutDown = new PushMessage("server.shutdown");
        static readonly PushMessage MessageServerPause = new PushMessage("server.pause");
        static readonly PushMessage MessageServerContinue = new PushMessage("server.continue");
        public static readonly PushMessage MessageServerRestart = new PushMessage("server.restart");

        public static readonly PushMessage[] MessageServerReconnects = [new PushMessageStringValue("server.reconnect", EnvInfo.AppInstance)];
        public static readonly PushMessage[] MessageServerConnects = [new PushMessageStringValue("server.connect", EnvInfo.AppInstance)];

        #endregion//Push messages

    }
}
