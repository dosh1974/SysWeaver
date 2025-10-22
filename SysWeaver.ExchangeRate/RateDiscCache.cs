using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization;

namespace SysWeaver.ExchangeRate
{
    public sealed class RateDiscCache
    {
        public RateDiscCache(String cacheFolder, String sourceName, ExceptionTracker errTracker = null)
        {
            cacheFolder = PathTemplate.Resolve(cacheFolder);
            if (String.IsNullOrEmpty(cacheFolder))
                cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SysWeaver_ExchangeRateCache_" + EnvInfo.AppGuid);
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

        public Rates TryReadRates()
        {
            var name = CacheBase + ".json";
            if (File.Exists(name))
            {
                try
                {
                    var data = File.ReadAllBytes(name);
                    return Ser.Create<Rates>(data.AsSpan());
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
                    var data = File.ReadAllBytes(name);
                    return Ser.Create<Rates>(data.AsSpan());
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
