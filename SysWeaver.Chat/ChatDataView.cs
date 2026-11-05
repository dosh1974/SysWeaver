using System;
using System.Collections.Generic;
using SysWeaver.MicroService;

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
        /// (images, files to download).
        /// </summary>
        public String Viewer;

        /// <summary>
        /// Optional: a view of the source of the asset (ex: a read only text view of an html or svg file), the chat shows a
        /// "source" toggle for it.
        /// </summary>
        public String Source;
    }


    /// <summary>
    /// Build <see cref="ChatDataView"/>'s from the data urls, using the extension viewers / editors of the <see cref="ServiceManager"/>
    /// (<see cref="ServiceManager.GetExtensionViewer(string, string)"/>).
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
            var parts = data.Split(';');
            var res = new ChatDataView[parts.Length];
            bool any = false;
            for (int i = 0; i < parts.Length; ++i)
            {
                var v = GetOne(manager, parts[i].Trim());
                res[i] = v;
                any |= (v.Viewer != null) || (v.Source != null);
            }
            return any ? res : null;
        }

        /// <summary>
        /// Files that the browser shows as they are (in a frame or as an image), they get a source view instead of a viewer.
        /// </summary>
        static readonly IReadOnlySet<String> WebFiles = ReadOnlyData.Set("html", "htm", "svg");

        /// <summary>
        /// Get the view of one data url.
        /// - Other origins, site absolute and data urls: the raw url only (chat page relative urls without "../" are files of the chat).
        /// - Images (not svg): the raw url only (shown as an image).
        /// - html, htm, svg: shown as they are, with a source view (the read only extension editor, ex: the text editor); not for a page with a query (an application page).
        /// - Others: the extension viewer (or the read only extension editor) if one exists.
        /// </summary>
        /// <param name="manager">The service manager.</param>
        /// <param name="raw">The raw (chat page relative) url.</param>
        /// <returns>The view.</returns>
        public static ChatDataView GetOne(ServiceManager manager, String raw)
        {
            var v = new ChatDataView { Raw = raw };
            if (String.IsNullOrEmpty(raw) || (manager == null) || raw.Contains("://", StringComparison.Ordinal)
                || raw.StartsWith("/", StringComparison.Ordinal) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return v;
            //  The read url for a viewer page (one folder in from the site root, as the chat page): "../x" stays, a chat page
            //  relative url (ex: the message files of the AI, "file/AI/...", served from "chat/") gets "../chat/"
            var read = raw.StartsWith("../", StringComparison.Ordinal) ? raw : "../chat/" + raw;
            var q = raw.IndexOf('?');
            var path = q < 0 ? raw : raw.Substring(0, q);
            var dot = path.LastIndexOf('.');
            if ((dot < 0) || (dot < path.LastIndexOf('/')))
                return v;
            var ext = path.Substring(dot + 1).ToLowerInvariant();
            //  The viewer / editor formats are site root relative, with the read url relative to the viewer page (one folder in), the
            //  same as the data urls are to the chat page: prefix "../" to get the chat page relative url
            if (WebFiles.Contains(ext))
            {
                //  A page with a query (ex: "../explore/table.html?q=...") is an application page showing data, its source (the page
                //  template) means nothing to the reader: no source view
                if (q >= 0)
                    return v;
                var src = manager.GetExtensionEditor(ext, read);
                if (src != null)
                    v.Source = "../" + src;
                return v;
            }
            var mime = MimeTypeMap.GetMimeFromUrl(path, false)?.Item1;
            if ((mime != null) && mime.StartsWith("image/", StringComparison.Ordinal))
                return v;
            var view = manager.GetExtensionViewer(ext, read) ?? manager.GetExtensionEditor(ext, read);
            if (view != null)
                v.Viewer = "../" + view;
            return v;
        }
    }
}
