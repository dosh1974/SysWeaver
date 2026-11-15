using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization;

namespace SysWeaver.ExchangeRate
{
    /// <summary>
    /// Stores the rates of a source on disc (the previous version is kept as a backup)
    /// </summary>
    public sealed class RateDiscCache
    {
        /// <summary>
        /// Create the cache (the folder is created if it doesn't exist)
        /// </summary>
        /// <param name="cacheFolder">The folder to store rates in (path variables are resolved), null or empty to use a folder in the common application data folder</param>
        /// <param name="sourceName">The name of the source (used to make the file name)</param>
        /// <param name="errTracker">Optional tracker for errors that occur when reading and writing</param>
        public RateDiscCache(String cacheFolder, String sourceName, ExceptionTracker errTracker = null)
        {
            cacheFolder = PathTemplate.Resolve(cacheFolder);
            if (String.IsNullOrEmpty(cacheFolder))
                cacheFolder = Path.Combine(PathTemplate.CommonApplicationData, "SysWeaver_ExchangeRateCache_" + EnvInfo.AppGuid);
            cacheFolder = Path.GetFullPath(cacheFolder);
            var ex = PathExt.EnsureFolderExist(cacheFolder);
            if (ex != null)
                throw ex;
            Fails = errTracker;
            var safeName = String.Join('_', PathExt.SafeFilename(sourceName), PathExt.SafeFilename(Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceName))));
            var cb = Path.Combine(cacheFolder, safeName);
            CacheBase = cb;
            Ser = SerManager.Get("json");
            ex = PathExt.TryDeleteFile(cb + ".tmp");
            if (ex != null)
                errTracker?.OnException(ex);
        }
        readonly ExceptionTracker Fails;
        readonly ISerializerType Ser;
        readonly String CacheBase;


        /// <summary>
        /// Save the rates, the previously saved rates are kept as a backup
        /// </summary>
        /// <param name="rates">The rates</param>
        /// <returns>True if the rates were saved</returns>
        public bool TrySaveRates(Rates rates)
        {
            var data = Ser.Serialize(rates);
            var tempName = CacheBase + ".tmp";
            var oldName = String.Join("_old", CacheBase, ".json");
            var name = CacheBase + ".json";
            try
            {
                FileExt.WriteMemory(tempName, data);
                if (File.Exists(name))
                    File.Move(name, oldName, true);
                File.Move(tempName, name, true);
                return true;
            }
            catch (Exception ex)
            {
                Fails?.OnException(ex);
            }
            return false;
        }

        /// <summary>
        /// Read the saved rates, the backup is used if the rates can't be read
        /// </summary>
        /// <returns>The rates, null if there are none (or they can't be read)</returns>
        public Rates TryReadRates()
        {
            var name = CacheBase + ".json";
            if (File.Exists(name))
            {
                try
                {
                    using var mem = FileReadOnlyMemory.Read(name);
                    return Ser.Create<Rates>(mem.Memory);
                }
                catch (Exception ex)
                {
                    Fails?.OnException(ex);
                }
            }
            name = String.Join("_old", CacheBase, ".json");
            if (File.Exists(name))
            {
                try
                {
                    using var mem = FileReadOnlyMemory.Read(name);
                    return Ser.Create<Rates>(mem.Memory);
                }
                catch (Exception ex)
                {
                    Fails?.OnException(ex);
                }
            }
            return null;
        }

    }

}
