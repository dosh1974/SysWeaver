using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    sealed class StoredUrlAction : StoredUrl
    {
        public StoredUrlAction(StoredUrl f, int cut)
        {
            var url = f.Url.Substring(cut);
            Url = url;
            var l = url.LastIndexOf('.');
            Size = f.Size;
            Comp = f.Comp;
            Private = f.Private;
            Auth = f.Auth;
            Saved = f.Saved;
            LastViewed = f.LastViewed;
            Expires = f.Expires;
            Shard = f.Shard;
            Actions = f.Url;
        }

        /// <summary>
        /// Actions that can be performed
        /// </summary>
        //  Delete is final (G47): the web table keeps the row muted without actions until the user refreshes
        [TableDataActionsFull(new[] { TableDataActionFlags.Final }, "Delete", "Click to delete this url.\nWarning!\nThis operation can NOT be undone.", "../Api/UserStorage/" + nameof(UserStorageService.DeleteStoredUrl) + "?\"{0}\"", "IconDbRemove", "", "", "")]
        [TableDataOrder(1)]
        public String Actions;
    }

}
