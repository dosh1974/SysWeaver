using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// Helpers for web paths (urls like "https://example.com/a/b.html" or local paths like "/a/b.html" and "a/../b.html")
    /// </summary>
    public static class WebPath
    {

        /// <summary>
        /// Check if a web path is rooted, i.e it contains a scheme and a server (contains "://"), example: "https://example.com/a/b.html".
        /// </summary>
        /// <param name="webPath">The web path to check</param>
        /// <returns>True if the web path contains "://", else false</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="webPath"/> is null</exception>
        public static bool IsRoot(String webPath)
        {
            ArgumentNullException.ThrowIfNull(webPath);
            var t = webPath.FastIndexOf("://");
            return t >= 0;
        }

        /// <summary>
        /// Split a web path into the server part (scheme, server, port and the first '/') and the local part (the rest).
        /// Example: "https://example.com/a/b.html" => "https://example.com/" and "a/b.html".
        /// </summary>
        /// <param name="server">The server part (including the trailing '/' if present), an empty string if the web path isn't rooted</param>
        /// <param name="local">The local part (without the leading '/' that ends the server part), the whole web path if it isn't rooted</param>
        /// <param name="webPath">The web path to split</param>
        /// <returns>True if the web path is rooted (contains "://"), else false</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="webPath"/> is null</exception>
        /// <remarks>server + local is always equal to the web path</remarks>
        public static bool SplitServerLocal(out String server, out String local, String webPath)
        {
            ArgumentNullException.ThrowIfNull(webPath);
            var t = webPath.FastIndexOf("://");
            if (t < 0)
            {
                server = "";
                local = webPath;
                return false;
            }
            var e = webPath.IndexOf('/', t + 3);
            if (e < 0)
            {
                server = webPath;
                local = "";
                return true;
            }
            ++e;
            server = webPath.Substring(0, e);
            local = webPath.Substring(e);
            return true;
        }

        /// <summary>
        /// Remove all "." segments and resolve all ".." segments in the local part of a web path.
        /// Examples: "a/./b/../c.html" => "a/c.html", "https://example.com/a/../b/" => "https://example.com/b/".
        /// </summary>
        /// <param name="webPath">The web path to collapse</param>
        /// <returns>The collapsed web path (the same instance if nothing could be collapsed)</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="webPath"/> is null</exception>
        /// <remarks>
        /// The server part (if any) is never changed.
        /// A ".." that can't be resolved is kept in a relative path ("a/../../b" => "../b"), and removed in an absolute local path ("/../b" => "/b").
        /// A trailing "." or ".." is a directory, so the result ends with a '/' ("a/b/.." => "a/", "a/." => "a/", ".." => "../").
        /// Empty segments are kept ("a//b" => "a//b").
        /// No allocations are made if nothing can be collapsed, else only the resulting string is allocated.
        /// </remarks>
        [SkipLocalsInit]
        public static String Collapse(String webPath)
        {
            ArgumentNullException.ThrowIfNull(webPath);
            int start = 0;
            var t = webPath.FastIndexOf("://");
            if (t >= 0)
            {
                var e = webPath.IndexOf('/', t + 3);
                if (e < 0)
                    return webPath;
                start = e + 1;
            }
            var local = webPath.AsSpan(start);
            if (!HasDotSegment(local))
                return webPath;
            //  The result is never longer than the input + 1 (a trailing ".." that is kept gets a '/')
            var len = local.Length + 1;
            Char[] rented = null;
            Span<Char> buffer = len <= 2048 ? stackalloc Char[len] : (rented = ArrayPool<Char>.Shared.Rent(len));
            try
            {
                var res = buffer.Slice(0, CollapseLocal(buffer, local));
                if (res.SequenceEqual(local))
                    return webPath;
                return start == 0 ? new String(res) : String.Concat(webPath.AsSpan(0, start), res);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Check if any segment is "." or ".."
        /// </summary>
        static bool HasDotSegment(ReadOnlySpan<Char> s)
        {
            int p = 0;
            var l = s.Length;
            for (; ; )
            {
                var i = s.Slice(p).IndexOf('.');
                if (i < 0)
                    return false;
                i += p;
                if ((i == 0) || (s[i - 1] == '/'))
                {
                    var n = i + 1;
                    if ((n < l) && (s[n] == '.'))
                        ++n;
                    if ((n == l) || (s[n] == '/'))
                        return true;
                }
                p = i + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsDotDot(ReadOnlySpan<Char> s) => (s.Length == 2) && (s[0] == '.') && (s[1] == '.');

        /// <summary>
        /// Collapse the segments of a local path into a buffer
        /// </summary>
        /// <param name="o">The output buffer, must be at least one char longer than the local path</param>
        /// <param name="s">The local path</param>
        /// <returns>The number of chars written to the buffer</returns>
        static int CollapseLocal(Span<Char> o, ReadOnlySpan<Char> s)
        {
            int ol = 0; // Output length
            int oc = 0; // Number of output segments
            var l = s.Length;
            // An absolute local path has an empty root segment that must never be removed
            int min = (l > 0 && s[0] == '/') ? 1 : 0;
            int p = 0;
            for (; ; )
            {
                var e = s.Slice(p).IndexOf('/');
                var last = e < 0;
                e = last ? l : e + p;
                var seg = s.Slice(p, e - p);
                bool isDot = (seg.Length == 1) && (seg[0] == '.');
                bool isDotDot = IsDotDot(seg);
                bool add = !isDot;
                if (isDotDot)
                {
                    if (oc > min)
                    {
                        // Remove the last output segment (unless it's a "..")
                        var ls = o.Slice(0, ol).LastIndexOf('/');
                        if (!IsDotDot(o.Slice(ls + 1, ol - ls - 1)))
                        {
                            --oc;
                            ol = ls < 0 ? 0 : ls;
                            add = false;
                        }
                    }
                    if (add && (min == 1))  // "/.." == "/"
                        add = false;
                }
                if (add)
                {
                    if (oc > 0)
                        o[ol++] = '/';
                    seg.CopyTo(o.Slice(ol));
                    ol += seg.Length;
                    ++oc;
                }
                if (last)
                {
                    //  A trailing "." or ".." is a directory, so end with an (empty) file name
                    if ((isDot || isDotDot) && (oc > 0))
                        o[ol++] = '/';
                    return ol;
                }
                p = e + 1;
            }
        }
    }

}
