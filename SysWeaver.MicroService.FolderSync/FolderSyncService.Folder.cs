using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SysWeaver.Auth;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        internal sealed class PushFolder
        {


            /// <summary>
            /// Optional commands to execute before deactivating (before old folder is renamed to back-up name)
            /// </summary>
            public readonly String[] OnDeactivate;

            /// <summary>
            /// Optional commands to execute to activate (after the folder have been replaced with old content)
            /// </summary>
            public readonly String[] OnActivate;

            /// <summary>
            /// Optional commands to execute when a new folder is uploaded
            /// </summary>
            public readonly String[] OnNewFolder;

            public FolderPushFolder.ActivationHandler OnActivateAsync;
            public FolderPushFolder.ActivationHandler OnDeactivateAsync;
            public FolderPushFolder.ActivationHandler OnNewFolderAsync;

            public readonly String LockName;
            public readonly String Name;
            public readonly String DestPath;
            public readonly IReadOnlyList<String> Auth;
            public TimeSpan RemoveAfter;
            public readonly FileHttpServerModuleFolder ModFolder;

            /// <summary>
            /// If true, folder versions are compressed.
            /// Activating (swapping) is slower but disc usage is reduced a lot (especially for many versions).
            /// </summary>
            public readonly bool Compress;

            static String[] ParseCommands(String s, IReadOnlyDictionary<String, String> extra)
            {
                if (String.IsNullOrEmpty(s))
                    return null;
                var r = s.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var l = r.Length;
                if (l <= 0)
                    return null;
                for (int i = 0; i < l; ++i)
                    r[i] = PathTemplate.Resolve(r[i], extra);
                return r;
            }

            public PushFolder(string name, string path, string auth, TimeSpan removeAfter, FolderPushFolder fs)
            {
                Name = name;
                var tp = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                DestPath = tp + Path.DirectorySeparatorChar;
                Auth = Authorization.GetRequiredTokens(auth);
                RemoveAfter = removeAfter;
                LockName = "FolderSync_" + Encoding.UTF8.GetBytes(name.FastToLower()).ToHex();
                ModFolder = new FileHttpServerModuleFolder
                {
                    AssumePreCompressed = true,
                    Auth = Roles.AdminOps,
                    ClientCacheDuration = 5,
                    RequestCacheDuration = 4,
                    WebFolder = "FolderSync/Folders/" + name,
                    DiscFolder = tp,
                };
                var x = new Dictionary<String, String>(StringComparer.Ordinal)
                {
                    { "name", name },
                    { "target", tp },
                    { "targetname", Path.GetFileName(tp) },
                    { "targetdir", Path.GetDirectoryName(tp) }
                };
                OnActivate = ParseCommands(fs.OnActivate, x);
                OnDeactivate = ParseCommands(fs.OnDeactivate, x);
                OnNewFolder = fs.OnNewFolder?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                OnActivateAsync = fs.OnActivateAsync;
                OnDeactivateAsync = fs.OnDeactivateAsync;
                OnNewFolderAsync = fs.OnNewFolderAsync;
                Compress = fs.Compress;
            }
        }




        internal sealed class PullFolder
        {
            public readonly String LockName;
            public readonly String Name;
            public readonly String DestPath;
            public readonly IReadOnlyList<String> Auth;
            public readonly FileHttpServerModuleFolder ModFolder;

            public PullFolder(string name, string path, string auth)
            {
                Name = name;
                var tp = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                DestPath = tp + Path.DirectorySeparatorChar;
                Auth = Authorization.GetRequiredTokens(auth);
                LockName = "FolderSync_" + Encoding.UTF8.GetBytes(name.FastToLower()).ToHex();
                ModFolder = new FileHttpServerModuleFolder
                {
                    AssumePreCompressed = true,
                    Auth = auth == null ? null : String.Join(',', auth),
                    ClientCacheDuration = 5,
                    RequestCacheDuration = 4,
                    WebFolder = "FolderSync/PullFolders/" + name,
                    DiscFolder = tp,
                };

            }
        }

    }

}
