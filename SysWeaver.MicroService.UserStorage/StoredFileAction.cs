using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    sealed class StoredFileAction : StoredFile
    {
        public StoredFileAction(StoredFile f, int cut)
        {
            var url = f.Url.Substring(cut);
            Url = url;
            var l = url.LastIndexOf('.');
            Ext = l < 0 ? null : url.Substring(l + 1);
            Size = f.Size;
            Comp = f.Comp;
            Private = f.Private;
            Auth = f.Auth;
            Saved = f.Saved;
            LastViewed = f.LastViewed;
            Expires = f.Expires;
            Shard = f.Shard;

            var parts = url.Split('/');
            var randomGuid = parts[1];
            var flags = randomGuid[randomGuid.Length - 1] - 'a';
            if (randomGuid.FastEquals("u"))
                flags = 1;
            Actions = (flags & 1) != 0 ? f.Url : null;
        }

        /// <summary>
        /// File extension
        /// </summary>
        [TableDataFileExtensionImage]
        [TableDataOrder(-1)]
        public String Ext;

        /// <summary>
        /// Actions that can be performed, some files are maintained by other mechanics and can't be deleted manually
        /// </summary>
        [TableDataActions("Delete", "Click to delete this file.\nWarning!\nThis operation can NOT be undone.", "../Api/UserStorage/" + nameof(UserStorageService.DeleteStoredFile) + "?\"{0}\"", "IconDbRemove")]
        [TableDataOrder(1)]
        public String Actions;
    }

}
