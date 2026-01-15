using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{
    public sealed partial class ServerManagerService
    {
        sealed class BackupFileRepo : IFileRepo
        {
            readonly ServerManagerService Manager;

            public BackupFileRepo(String key, String discFolder, ServerManagerService manager, bool isKey = false)
            {
                Manager = manager;
                IsKey = isKey;
                Key = key;
                DiscFolder = discFolder;
                UploadAuth = isKey ? Roles.Admin : "";
                ValidExt = isKey ? ValidKeyExt : ValidConfigExt;
                InvalidSuffixes = isKey ? ReadOnlySet<String>.Empty : InvalidConfigSuffixes;
            }
            readonly bool IsKey;
            readonly String DiscFolder;

            public string Key { get; init; }

            public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders => null;

            readonly IReadOnlySet<String> InvalidSuffixes;
            readonly IReadOnlySet<String> ValidExt;

            public string UploadAuth { get; init; }

            async ValueTask<FileUploadResult> CheckFile(FileUploadInfo file)
            {
                var dest = Path.Combine(DiscFolder, file.Name);
                if (File.Exists(dest))
                {
                    var h = await FileHash.GetHashAsync(dest).ConfigureAwait(false);
                    h = HashTools.ToHexHash(h);
                    if (h.FastEquals(file.Hash))
                        return FileUploadResult.AlreadyUploaded;
                }
                if (!ValidExt.Contains(file.GetExtension().FastToLower()))
                    return FileUploadResult.RefuseExtension;
                var lfile = file.Name.FastToLower();
                foreach (var x in InvalidSuffixes)
                    if (lfile.FastEndsWith(x))
                        return FileUploadResult.Refuse;
                if (file.Length > (64 << 10))
                    return FileUploadResult.RefuseSize;
                return FileUploadResult.Upload;
            }

            public ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return ValueTask.FromResult(ArrayExt.Create(info.Length, FileUploadResult.NotAuthorized));
                    }
                }
                return info.ConvertAsyncValue(CheckFile);
            }

            public async ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return FileUploadResult.NotAuthorized;
                    }
                }
                var res = await CheckFile(file).ConfigureAwait(false);
                var dest = Path.Combine(DiscFolder, file.Name);
                if (res.Result != FileUploadStatus.Upload)
                    return res;

                var a = Manager.Audit;
                HttpApiAudit ad = null;
                long id = 0;
                if (a != null)
                {
                    id = ApiAudit.GetId();
                    ad = new HttpApiAudit(String.Concat("Upload ", Key, '/', file.Name), AuditGroup);
                    a.OnApiBegin(id, r, ad, file.Hash);
                }
                try
                {
                    if (!ServiceHost.BackupConfig(dest, Manager.Manager))
                    {
                        if (a != null)
                            a.OnApiException(id, r, ad, new Exception("Backup failed"));
                        return FileUploadResult.Refuse;
                    }
                    var data = await s.ReadAllMemoryAsync().ConfigureAwait(false);
                    if (decoder != null)
                        data = decoder.GetDecompressed(data.Span);
                    var text = Encoding.UTF8.GetString(data.Span);
                    await FileExt.WriteMemoryAsync(dest, data, true).ConfigureAwait(false);
                    if (a != null)
                        a.OnApiEnd(id, r, ad, IsKey ? "** PROTECTED **" : text.LimitLength(2048));
                    Manager.Syncer.GetFolderData(Key);
                    r.Session.InvalidateCache();
                    r.Server.InvalidateCache();
                    return FileUploadResult.None;
                }
                catch (Exception ex)
                {
                    if (a != null)
                        a.OnApiException(id, r, ad, ex);
                    throw;
                }
            }
        }

    }

}
