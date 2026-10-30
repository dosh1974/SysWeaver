using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Small helpers for WMI queries.
    /// </summary>
    static class Win32Wmi
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Run a WQL query.
        /// </summary>
        /// <param name="scope">The namespace, ex: "root\Microsoft\Windows\Storage"</param>
        /// <param name="wql">The query, ex: "SELECT * FROM MSFT_Disk"</param>
        /// <returns>The objects (the caller should dispose them)</returns>
        public static List<ManagementObject> Query(String scope, String wql)
        {
            var res = new List<ManagementObject>();
            using var s = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql), new EnumerationOptions { Timeout = Timeout, ReturnImmediately = true, Rewindable = false });
            using var c = s.Get();
            foreach (ManagementObject o in c)
                res.Add(o);
            return res;
        }

        /// <summary>
        /// Run a query, an empty list if it fails (ex: the namespace doesn't exist or access is denied).
        /// </summary>
        public static List<ManagementObject> TryQuery(String scope, String wql)
        {
            try
            {
                return Query(scope, wql);
            }
            catch
            {
                return new List<ManagementObject>();
            }
        }

        /// <summary>
        /// Dispose all objects.
        /// </summary>
        public static void Dispose(IEnumerable<ManagementObject> objs)
        {
            foreach (var o in objs)
                o.Dispose();
        }

        /// <summary>
        /// Get a property value, null if it doesn't exist.
        /// </summary>
        public static Object Get(ManagementBaseObject o, String name)
        {
            try
            {
                return o[name];
            }
            catch
            {
                return null;
            }
        }

        public static String Str(ManagementBaseObject o, String name)
        {
            var v = Get(o, name);
            if (v == null)
                return "";
            if (v is char c)
                return c == '\0' ? "" : c.ToString();
            return Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        }

        public static long Long(ManagementBaseObject o, String name)
        {
            var v = Get(o, name);
            if (v == null)
                return 0;
            try
            {
                return Convert.ToInt64(v, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        public static bool Bool(ManagementBaseObject o, String name) => Get(o, name) is bool b && b;

        public static String[] Strs(ManagementBaseObject o, String name) => Get(o, name) as String[] ?? [];
    }
}
