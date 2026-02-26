using System;
using System.Threading;
using SysWeaver.Data;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public sealed partial class LanCertificateManagerService
    {
        sealed class Data
        {
            public Data(Domain d)
            {
                DomainName = d.DomainName;
                LastChecked = new DateTime(Interlocked.Read(ref d.LastChecked), DateTimeKind.Utc);
                var data = d.Data;
                Cc = -1;
                if (data != null)
                {
                    Cc = data.Cc;
                    var cert = data.Cert;
                    Expiration = cert.GetExpiration();
                    From = cert.NotBefore;
                    To = cert.NotAfter;
                    Serial = cert.SerialNumber;
                }
                var e = d.CertErrors;
                ExCount = e.Count;
                ExLast = new DateTime(e.LastTime, DateTimeKind.Utc);
                LastException = e.LastException?.ToString();
                Actions = d.DomainName.FastToLower();
            }

            /// <summary>
            /// Name of the domain
            /// </summary>
            [TableDataUrl("{0}", "https://{0}")]
            public String DomainName;

            /// <summary>
            /// When the cert was last checked / updated
            /// </summary>
            public DateTime LastChecked;

            /// <summary>
            /// Certificate change counter
            /// </summary>
            public long Cc;

            /// <summary>
            /// What time the certificate is valid from
            /// </summary>
            public DateTime From;

            /// <summary>
            /// What time the certificate is valid to
            /// </summary>
            public DateTime To;

            /// <summary>
            /// When the certificate expires
            /// </summary>
            public DateTime Expiration;

            /// <summary>
            /// The serial number of the certificate
            /// </summary>
            public String Serial;

            //  Actions
            [TableDataActions("Update", "Click to manually perform an update check", "../Api/Lcm/" + nameof(UpdateCert) + "?\"{0}\"", "../icons/reload.svg")]
            public String Actions;

            /// <summary>
            /// Number of exceptions encountered when getting the certificate
            /// </summary>
            public long ExCount;

            /// <summary>
            /// When the last exception occured
            /// </summary>
            public DateTime ExLast;

            /// <summary>
            /// The details of the last exception
            /// </summary>
            [TableDataText]
            public String LastException;

        }






    }
}
