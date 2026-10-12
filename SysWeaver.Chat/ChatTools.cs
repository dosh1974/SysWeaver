using System;
using System.Globalization;
using System.IO.Hashing;
using System.Text;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.Chat
{
    public static class ChatTools
    {
        /// <summary>
        /// Get an anonymous user name for the given session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static String GetAnonymousUsername(HttpSession session)
        {
            if (session == null)
                return "Anonymous";
            //  A stable hash (String.GetHashCode is randomized per process and the name is persisted)
            var id = session.DeviceId.AsSpan();
            uint t;
            if (id.Length <= MaxStackChars)
            {
                Span<Byte> b = stackalloc Byte[MaxStackChars * 3];
                t = XxHash32.HashToUInt32(b.Slice(0, Encoding.UTF8.GetBytes(id, b)));
            }
            else
            {
                t = XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(session.DeviceId));
            }
            //  "Anonymous" + t, one allocation
            Span<char> c = stackalloc char[AnonymousPrefix.Length + 10];
            AnonymousPrefix.CopyTo(c);
            t.TryFormat(c.Slice(AnonymousPrefix.Length), out var n, default, CultureInfo.InvariantCulture);
            return new String(c.Slice(0, AnonymousPrefix.Length + n));
        }

        const String AnonymousPrefix = "Anonymous";
        const int MaxStackChars = 128;

        /// <summary>
        /// Get the user name from a session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static String GetUsername(HttpSession session)
        {
            var nick = session?.Auth?.NickName;
            if (!String.IsNullOrEmpty(nick))
                return nick; 
            return GetAnonymousUsername(session);
        }

        /// <summary>
        /// Remove data from a table that an AI doesn't care about
        /// </summary>
        /// <param name="d">The table to trim</param>
        /// <returns>The same table, but trimmed</returns>
        public static TableData TrimDataTable(TableData d)
        {
            var cols = d.Cols;
            if (cols == null)
                return d;
            foreach (var c in cols)
            {
                c.Format = null;
                c.Props = 0;
                c.Type = c.Type.Replace("System.", "");
                c.Title = null;
            }
            ;
            return d;
        }

        /// <summary>
        /// Remove data from a table that an AI doesn't care about
        /// </summary>
        /// <param name="d">The table to trim</param>
        /// <returns>The same table, but trimmed</returns>
        public static TypedTableData<T> TrimDataTable<T>(TypedTableData<T> d)
        {
            var cols = d.Cols;
            if (cols == null)
                return d;
            foreach (var c in cols)
            {
                c.Format = null;
                c.Props = 0;
                c.Type = c.Type.Replace("System.", "");
                c.Title = null;
            }
            ;
            return d;
        }


    }

}
