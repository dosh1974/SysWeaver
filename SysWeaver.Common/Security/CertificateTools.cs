
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text;

namespace SysWeaver.Security
{
    public static class CertificateTools
    {

        /// <summary>
        /// Get the expiration time of the certificate
        /// </summary>
        /// <param name="cert">Certificate to get expiration time</param>
        /// <returns>The time when the certificate expires</returns>
        public static DateTime GetExpiration(this X509Certificate2 cert)
        {
            var time = cert.GetExpirationDateString();
            return DateTime.Parse(time);
        }

        /// <summary>
        /// Test if a certificate is expired or will expire within a day
        /// </summary>
        /// <param name="cert">Cert to test</param>
        /// <param name="expires">When the cert expires</param>
        /// <param name="hoursBeforeExpiration">The number of hours that this certificate must be valid</param>
        /// <returns>True if the cert is expoired or will expire soon</returns>
        public static bool IsSoonExpired(this X509Certificate2 cert, out DateTime expires, int hoursBeforeExpiration)
        {
            expires = GetExpiration(cert);
            var hoursLeft = (expires - DateTime.Now).TotalHours;
            return hoursLeft < hoursBeforeExpiration;
        }


        const string SAN_OID = "2.5.29.17";


        static int ReadLength(ref Span<byte> span)
        {
            var length = (int)span[0];
            span = span.Slice(1);
            if ((length & 0x80) > 0)
            {
                var lengthBytes = length & 0x7F;
                length = 0;
                for (var i = 0; i < lengthBytes; i++)
                {
                    length = length * 0x100 + span[0];
                    span = span.Slice(1);
                }
            }
            return length;
        }

        static IList<string> ParseSubjectAlternativeNames(byte[] rawData)
        {
            var result = new List<string>(); // cannot yield results when using Span yet
            if (rawData.Length < 1 || rawData[0] != '0')
            {
                throw new InvalidDataException("They told me it will start with zero :(");
            }

            var data = rawData.AsSpan(1);
            var length = ReadLength(ref data);
            if (length != data.Length)
            {
                throw new InvalidDataException("I don't know who I am anymore");
            }

            while (!data.IsEmpty)
            {
                var type = data[0];
                data = data.Slice(1);

                var partLength = ReadLength(ref data);
                if (type == 135) // ip
                {
                    result.Add(new IPAddress(data.Slice(0, partLength)).ToString());
                }
                else if (type == 160) // upn
                {
                    // not sure how to parse the part before \f
                    var index = data.IndexOf((byte)'\f') + 1;
                    var upnData = data.Slice(index);
                    var upnLength = ReadLength(ref upnData);
                    result.Add(Encoding.UTF8.GetString(upnData.Slice(0, upnLength)));
                }
                else // all other
                {
                    result.Add(Encoding.UTF8.GetString(data.Slice(0, partLength)));
                }
                data = data.Slice(partLength);
            }
            return result;
        }

        public static IEnumerable<string> GetSubjectAlternativeNames(this X509Certificate2 cert)
        {
            return cert.Extensions
                .Cast<X509Extension>()
                .Where(ext => ext.Oid.Value.Equals(SAN_OID))
                .SelectMany(x => ParseSubjectAlternativeNames(x.RawData));
        }

        public static IEnumerable<string> GetSubjectAlternativeNames(this X509Extension cert) => ParseSubjectAlternativeNames(cert.RawData);


        /// <summary>
        /// Load a certificate from disc
        /// </summary>
        /// <param name="filename">.pfx file containing the cert</param>
        /// <param name="password">Password or password file</param>
        /// <param name="passwordCanBeFile">True if password may be a file</param>
        /// <returns></returns>
        public static async Task<X509Certificate2> Load(String filename, String password = null, bool passwordCanBeFile = true)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException("File " + filename.ToFilename() + ", not found!");
            string pfile = null;
            if (passwordCanBeFile && !string.IsNullOrEmpty(password))
            {
                if (File.Exists(password))
                {
                    pfile = password;
                    password = EnvInfo.ResolveText((await File.ReadAllTextAsync(password).ConfigureAwait(false)).Trim());
                    if (password.Length <= 0)
                        password = null;
                }
            }
            try
            {
                return new X509Certificate2(filename, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load certificate from " + filename.ToFilename() + (pfile != null ? " with password from file " + pfile.ToFilename() : ""), ex);
            }
        }

        /// <summary>
        /// Load a certificate from disc
        /// </summary>
        /// <param name="data">Contents of a .pfx file containing the cert</param>
        /// <param name="password">Password or password file</param>
        /// <param name="passwordCanBeFile">True if password may be a file</param>
        /// <returns></returns>
        public static async Task<X509Certificate2> Create(Byte[] data, String password = null, bool passwordCanBeFile = true)
        {
            string pfile = null;
            if (passwordCanBeFile && !string.IsNullOrEmpty(password))
            {
                if (File.Exists(password))
                {
                    pfile = password;
                    password = EnvInfo.ResolveText((await File.ReadAllTextAsync(password).ConfigureAwait(false)).Trim());
                    if (password.Length <= 0)
                        password = null;
                }
            }
            try
            {
                return new X509Certificate2(data, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create certificate " + (pfile != null ? " with password from file " + pfile.ToFilename() : ""), ex);
            }
        }


        public static bool Install(this X509Certificate2 cert)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                if (store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count <= 0)
                {
                    store.Add(cert);
                }
                store.Close();
            }
            return true;
        }




        public static Byte[] GetCertBytes(ReadOnlySpan<Byte> data) => GetCertBytes(Encoding.UTF8.GetString(data));

        public static Byte[] GetCertBytes(String cert)
        {
            const String header = "-----BEGIN CERTIFICATE-----";
            const String footer = "-----END CERTIFICATE-----";
            var start = cert.IndexOf(header, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                throw new Exception("Not a valid certificate (no header)!");
            start += header.Length;

            var end = cert.LastIndexOf(footer, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                throw new Exception("Not a valid certificate (no footer)!");
            var sb = new StringBuilder(cert.Length);
            for (int i = start; i < end; ++ i)
            {
                var c = cert[i];
                if (c > 32)
                    sb.Append(c);
            }
            cert = sb.ToString();
            var certBytes = Convert.FromBase64String(cert);
            return certBytes;
        }


    }



}
