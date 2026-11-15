using System;
using System.Collections.Generic;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.Chat
{
    /// <summary>
    /// How one entry of <see cref="ChatMessageBody.Data"/> is shown (see <see cref="ChatMessage.DataViews"/>, one per entry, same order).
    /// All urls are relative to the chat page (normally "../" relative, like the data urls).
    /// </summary>
    public sealed class ChatDataView
    {
        public override string ToString() => Viewer == null ? Raw : String.Concat(Raw, " => ", Viewer);

        /// <summary>
        /// The original asset (what the data entry is). The menu operations (copy url, store, create link, download) use this.
        /// </summary>
        public String Raw;

        /// <summary>
        /// Optional: the url to display (ex: an extension viewer page for the asset), null to show the raw asset as is
        /// (images, files to download, application pages).
        /// </summary>
        public String Viewer;

        /// <summary>
        /// Optional: a view of the source of the asset (ex: a read only text view of an html, svg or markdown file), the chat shows a
        /// "Source" toggle (and a "View source" menu item) for it.
        /// </summary>
        public String Source;
    }


    /// <summary>
    /// Build <see cref="ChatDataView"/>'s from the data urls, using the extension viewers / editors of the <see cref="ServiceManager"/>
    /// (<see cref="ServiceManager.GetExtensionViewer(string, string)"/>).
    /// <para>
    /// The heuristic (see <see cref="GetOne(ServiceManager, string, IReadOnlyList{string})"/>) separates two kinds of links:
    /// <list type="bullet">
    /// <item><b>Files</b> (a document, a data file, a report the AI made, an upload): shown in the extension viewer of the file type when
    /// one exists, with a source view (the read only extension editor) when that is something else than the viewer.</item>
    /// <item><b>Live links</b> (an application page such as a table, a chart or the api explorer, an api call, anything with a query or a
    /// fragment): shown as they are (the page is the view), never with a source view (the page template or the request means nothing
    /// to the reader).</item>
    /// </list>
    /// An html page is only treated as a file when it is user / chat content (the chat files, the user storage); every other html page of
    /// the site is an application page. Other file types are files unless they are an api call.
    /// </para>
    /// </summary>
    public static class ChatDataViews
    {
        /// <summary>
        /// Fill <see cref="ChatMessage.DataViews"/> from <see cref="ChatMessageBody.Data"/> (again when the data changed).
        /// Views set by the provider itself are kept.
        /// </summary>
        /// <param name="manager">The service manager (extension viewers), may be null (no views).</param>
        /// <param name="message">The message (may be null).</param>
        public static void Fill(ServiceManager manager, ChatMessage message)
        {
            if (message == null)
                return;
            var data = message.Data;
            if ((message.DataViews != null) && (message.DataViewsOf == null))
                return;
            if (String.Equals(message.DataViewsOf, data, StringComparison.Ordinal) && (message.DataViews != null || String.IsNullOrEmpty(data)))
                return;
            message.DataViews = Get(manager, data);
            message.DataViewsOf = data ?? "";
        }

        /// <summary>
        /// Get the views of a data string (";" separated urls).
        /// </summary>
        /// <param name="manager">The service manager (extension viewers), may be null (no views).</param>
        /// <param name="data">The data urls.</param>
        /// <returns>One view per entry, null if there is no data, or no entry has a viewer or a source view.</returns>
        public static ChatDataView[] Get(ServiceManager manager, String data)
        {
            if (String.IsNullOrEmpty(data) || (manager == null))
                return null;
            var content = ContentPrefixes(manager);
            var parts = data.Split(';');
            var res = new ChatDataView[parts.Length];
            bool any = false;
            for (int i = 0; i < parts.Length; ++i)
            {
                var v = GetOne(manager, parts[i].Trim(), content);
                res[i] = v;
                any |= (v.Viewer != null) || (v.Source != null);
            }
            return any ? res : null;
        }

        /// <summary>
        /// The site root relative prefix of the chat files (the files of a chat, ex: what the AI made, "file/AI/..." relative to the chat page).
        /// </summary>
        const String ChatFilePrefix = "chat/file/";

        /// <summary>
        /// Get the site root relative url prefixes of user / chat content (files, not application pages): the chat files and the user storage
        /// (when there is one).
        /// </summary>
        /// <param name="manager">The service manager (may be null).</param>
        /// <returns>The prefixes (lower case, ending with "/").</returns>
        public static IReadOnlyList<String> ContentPrefixes(ServiceManager manager)
        {
            var store = manager?.TryGet<IUserStorageService>();
            if (store == null)
                return [ChatFilePrefix];
            var t = store.GetBaseUrlPrefix();
            var p = t.IsCompletedSuccessfully ? t.Result : null;
            //  An empty prefix (the storage at the site root) says nothing about a url
            if (String.IsNullOrEmpty(p))
                return [ChatFilePrefix];
            p = p.TrimStart('/').ToLowerInvariant();
            if (!p.EndsWith('/'))
                p += '/';
            return [ChatFilePrefix, p];
        }

        /// <summary>
        /// Html pages: their extension viewer is only used for user / chat content (see <see cref="ChatDataViews"/>).
        /// </summary>
        static readonly IReadOnlySet<String> HtmlFiles = ReadOnlyData.Set("html", "htm", "xhtml");

        /// <summary>
        /// Get the view of one data url (see <see cref="GetOne(ServiceManager, string, IReadOnlyList{string})"/>).
        /// </summary>
        /// <param name="manager">The service manager.</param>
        /// <param name="raw">The raw (chat page relative) url.</param>
        /// <returns>The view.</returns>
        public static ChatDataView GetOne(ServiceManager manager, String raw)
            => GetOne(manager, raw, ContentPrefixes(manager));

        /// <summary>
        /// Get the view of one data url:
        /// <list type="number">
        /// <item>Other origins, site absolute, data: and blob: urls: the raw url only (chat page relative urls without "../" are files of the chat).</item>
        /// <item>No file extension (an api call, a folder): the raw url only.</item>
        /// <item>An api call (a path with an "Api" folder, ex: "../chat/Api/GetFile.json?..."): the raw url only.</item>
        /// <item>Html: a live link (a query or a fragment, ex: "../explore/table.html?q=...") or an html page of the site that isn't user / chat
        /// content (an application page, ex: "../chart/Chart.html") is shown as it is, without a source view.
        /// User / chat content (a document) uses the extension viewer of html with "sandbox=0" (the page runs with this site's origin, so the
        /// files and apis it uses work; when there is no viewer it's shown as it is) and gets a source view.</item>
        /// <item>Images (svg and raster): a static image uses its extension viewer (when there is one, else it's shown as an image), a static
        /// svg also gets a source view; a generated image (a query) is shown as an image.</item>
        /// <item>Other files: the extension viewer (or else the read only extension editor) if one exists; a static file (no query or fragment)
        /// also gets the read only extension editor as a source view, when that is something else than the viewer.</item>
        /// </list>
        /// </summary>
        /// <param name="manager">The service manager.</param>
        /// <param name="raw">The raw (chat page relative) url.</param>
        /// <param name="contentPrefixes">The site root relative url prefixes of user / chat content (lower case, see <see cref="ContentPrefixes(ServiceManager)"/>).</param>
        /// <returns>The view.</returns>
        public static ChatDataView GetOne(ServiceManager manager, String raw, IReadOnlyList<String> contentPrefixes)
        {
            var v = new ChatDataView { Raw = raw };
            if (String.IsNullOrEmpty(raw) || (manager == null) || raw.Contains("://", StringComparison.Ordinal)
                || raw.StartsWith("/", StringComparison.Ordinal) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                return v;
            //  The read url for a viewer page (one folder in from the site root, as the chat page): "../x" stays, a chat page
            //  relative url (ex: the message files of the AI, "file/AI/...", served from "chat/") gets "../chat/"
            var read = raw.StartsWith("../", StringComparison.Ordinal) ? raw : "../chat/" + raw;
            //  The site root relative path (without the query and the fragment); a query or a fragment makes it a live link
            var end = read.IndexOfAny(QueryChars);
            var live = end >= 0;
            var path = (live ? read.Substring(0, end) : read).Substring(3);
            //  Above the site root ("../../x") or with a "..", "." segment: not a plain site url, show it as it is
            if (path.Contains("./", StringComparison.Ordinal) || path.EndsWith('.'))
                return v;
            var slash = path.LastIndexOf('/');
            var dot = path.LastIndexOf('.');
            if ((dot < 0) || (dot < slash))
                return v;
            //  An api call (the api of a service is served from its "Api" folder): a result, not a file
            if (IsApi(path))
                return v;
            var ext = path.Substring(dot + 1).ToLowerInvariant();
            var lowerPath = path.ToLowerInvariant();
            bool isContent = false;
            if (contentPrefixes != null)
                foreach (var p in contentPrefixes)
                    if (lowerPath.StartsWith(p, StringComparison.Ordinal))
                    {
                        isContent = true;
                        break;
                    }
            //  The viewer / editor formats are site root relative, with the read url relative to the viewer page (one folder in), the
            //  same as the data urls are to the chat page: prefix "../" to get the chat page relative url
            if (HtmlFiles.Contains(ext))
            {
                //  An application page (a table, a chart, an explorer, ...): the page itself is the view, its source (the page template)
                //  means nothing to the reader
                if (live || !isContent)
                    return v;
                //  A document (an uploaded page, a page the AI made): the html viewer and its source.
                //  The viewer runs the page with this site's origin ("sandbox=0"): a page the AI made uses the files it made and the apis
                //  of the site (with the session). This doesn't give a page more than opening it in a tab does: the viewer only does it for
                //  a plain file url (loaded by the frame itself, so the response headers apply, ex: user storage serves active content
                //  with a CSP sandbox of its own); html viewers that don't know the option ignore it.
                var hv = manager.GetExtensionViewer(ext, read);
                if (hv != null)
                    v.Viewer = "../" + hv + (hv.Contains('?') ? "&" : "?") + "sandbox=0";
                var hs = manager.GetExtensionEditor(ext, read);
                if (hs != null)
                    v.Source = "../" + hs;
                return v;
            }
            var mime = MimeTypeMap.GetMimeFromUrl(path, false)?.Item1;
            if ((mime != null) && mime.StartsWith("image/", StringComparison.Ordinal))
            {
                //  A generated image (a query, ex: a chart or a qr code) is a result: shown as an image (the chat only loads such an
                //  image on a click, a viewer would load it right away)
                if (live)
                    return v;
                //  A static image: its extension viewer if one exists (else shown as an image), a svg also gets its source
                var iv = manager.GetExtensionViewer(ext, read);
                if (iv != null)
                    v.Viewer = "../" + iv;
                if (ext == "svg")
                {
                    var ss = manager.GetExtensionEditor(ext, read);
                    if (ss != null)
                        v.Source = "../" + ss;
                }
                return v;
            }
            var viewer = manager.GetExtensionViewer(ext, read);
            var editor = manager.GetExtensionEditor(ext, read);
            var view = viewer ?? editor;
            if (view == null)
                return v;
            v.Viewer = "../" + view;
            if (!live && (viewer != null) && (editor != null) && !String.Equals(viewer, editor, StringComparison.Ordinal))
                v.Source = "../" + editor;
            return v;
        }

        static readonly char[] QueryChars = ['?', '#'];

        /// <summary>
        /// Test if a site root relative path is an api call (it has a folder named "Api", any case, ex: "chat/Api/GetFile", "Api/Foo.json").
        /// </summary>
        /// <param name="path">The path (no query).</param>
        /// <returns>True for an api call.</returns>
        static bool IsApi(String path)
        {
            int s = 0;
            for (; ; )
            {
                var e = path.IndexOf('/', s);
                if (e < 0)
                    return false;
                if (((e - s) == 3) && (String.Compare(path, s, "api", 0, 3, StringComparison.OrdinalIgnoreCase) == 0))
                    return true;
                s = e + 1;
            }
        }
    }
}
