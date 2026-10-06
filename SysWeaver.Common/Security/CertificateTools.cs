
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
    /// <summary>
    /// Helpers for X.509 certificates (loading, installing, expiration and subject alternative names).
    /// </summary>
    public static class CertificateTools
    {

        /// <summary>
        /// Get the expiration time of the certificate
        /// </summary>
        /// <param name="cert">Certificate to get expiration time</param>
        /// <returns>The time when the certificate expires (local time)</returns>
        /// <remarks>Parses the culture specific string from <see cref="X509Certificate.GetExpirationDateString"/>, <see cref="X509Certificate2.NotAfter"/> gives the same value without parsing.</remarks>
        public static DateTime GetExpiration(this X509Certificate2 cert)
        {
            var time = cert.GetExpirationDateString();
            return DateTime.Parse(time);
        }

        /// <summary>
        /// Test if a certificate is expired or will expire within a number of hours
        /// </summary>
        /// <param name="cert">Cert to test</param>
        /// <param name="expires">When the cert expires (local time)</param>
        /// <param name="hoursBeforeExpiration">The number of hours that this certificate must be valid</param>
        /// <returns>True if the cert is expired or will expire within <paramref name="hoursBeforeExpiration"/> hours</returns>
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
                    // not sure how to parse the part before \f (only search within this entry, skip the entry if there is no UTF8String)
                    var upnData = data.Slice(0, partLength);
                    var index = upnData.IndexOf((byte)'\f');
                    if (index >= 0)
                    {
                        upnData = upnData.Slice(index + 1);
                        var upnLength = ReadLength(ref upnData);
                        result.Add(Encoding.UTF8.GetString(upnData.Slice(0, upnLength)));
                    }
                }
                else // all other
                {
                    result.Add(Encoding.UTF8.GetString(data.Slice(0, partLength)));
                }
                data = data.Slice(partLength);
            }
            return result;
        }

        /// <summary>
        /// Get the subject alternative names (SAN) of a certificate, ex: the DNS names and IP addresses that the certificate is valid for.
        /// </summary>
        /// <param name="cert">The certificate</param>
        /// <returns>The names as strings (IP addresses are formatted, other types are decoded as UTF-8 text). Evaluated lazily.</returns>
        /// <exception cref="InvalidDataException">The extension data is malformed (thrown on enumeration)</exception>
        public static IEnumerable<string> GetSubjectAlternativeNames(this X509Certificate2 cert)
        {
            return cert.Extensions
                .Cast<X509Extension>()
                .Where(ext => ext.Oid.Value.Equals(SAN_OID))
                .SelectMany(x => ParseSubjectAlternativeNames(x.RawData));
        }

        /// <summary>
        /// Get the subject alternative names (SAN) from a subject alternative name extension (OID 2.5.29.17).
        /// </summary>
        /// <param name="cert">The extension (the OID is not checked)</param>
        /// <returns>The names as strings (IP addresses are formatted, other types are decoded as UTF-8 text)</returns>
        /// <exception cref="InvalidDataException">The extension data is malformed</exception>
        public static IEnumerable<string> GetSubjectAlternativeNames(this X509Extension cert) => ParseSubjectAlternativeNames(cert.RawData);

        /// <summary>
        /// Check if a certificate is self signed, i.e. the subject and issuer names are equal (the signature isn't verified).
        /// </summary>
        /// <param name="cert">The certificate</param>
        /// <returns>True if the subject and issuer names are equal</returns>
        public static bool IsSelfSigned(X509Certificate2 cert)
        {
            return cert.SubjectName.RawData.SequenceEqual(cert.IssuerName.RawData);
        }


        /// <summary>
        /// Load a certificate from disc
        /// </summary>
        /// <param name="filename">.pfx file containing the cert</param>
        /// <param name="password">Password or password file</param>
        /// <param name="passwordCanBeFile">True if password may be a file, if the password is the name of an existing file, the (trimmed) content of that file is used as the password (environment variables are resolved using EnvInfo.ResolveText)</param>
        /// <returns>The certificate (with an exportable private key, stored in the machine key set)</returns>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="Exception">The certificate couldn't be loaded (the inner exception contains the reason)</exception>
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
                    password = EnvInfo.ResolveText((await FileExt.ReadTextAsync(password).ConfigureAwait(false)).Trim());
                    if (password.Length <= 0)
                        password = null;
                }
            }
            try
            {
                return X509CertificateLoader.LoadPkcs12FromFile(filename, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                //return new X509Certificate2(filename, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load certificate from " + filename.ToFilename() + (pfile != null ? " with password from file " + pfile.ToFilename() : ""), ex);
            }
        }

        /// <summary>
        /// Load a certificate from memory
        /// </summary>
        /// <param name="data">Contents of a .pfx file containing the cert</param>
        /// <param name="password">Password or password file</param>
        /// <param name="passwordCanBeFile">True if password may be a file, if the password is the name of an existing file, the (trimmed) content of that file is used as the password (environment variables are resolved using EnvInfo.ResolveText)</param>
        /// <returns>The certificate (with an exportable private key, stored in the machine key set)</returns>
        /// <exception cref="Exception">The certificate couldn't be loaded (the inner exception contains the reason)</exception>
        public static async Task<X509Certificate2> Create(ReadOnlyMemory<Byte> data, String password = null, bool passwordCanBeFile = true)
        {
            string pfile = null;
            if (passwordCanBeFile && !string.IsNullOrEmpty(password))
            {
                if (File.Exists(password))
                {
                    pfile = password;
                    password = EnvInfo.ResolveText((await FileExt.ReadTextAsync(password).ConfigureAwait(false)).Trim());
                    if (password.Length <= 0)
                        password = null;
                }
            }
            try
            {
                return X509CertificateLoader.LoadPkcs12(data.Span, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                //return new X509Certificate2(data, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create certificate " + (pfile != null ? " with password from file " + pfile.ToFilename() : ""), ex);
            }
        }


        /// <summary>
        /// Install a certificate in the local machine personal ("My") store (if it's not already installed)
        /// </summary>
        /// <param name="cert">The certificate to check/install</param>
        /// <returns>True if the cert was installed (was new)</returns>
        /// <remarks>Requires write access to the local machine store (typically administrator rights).</remarks>
        public static bool Install(this X509Certificate2 cert)
        {
            bool isNew = false;
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                var certs = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).ToList();
                int count = certs.Count;
                foreach (var c in certs)
                {
                    if ((c.NotBefore != cert.NotBefore) || (c.NotAfter != cert.NotAfter))
                    {
                        store.Remove(c);
                        --count;
                    }
                }
                if (count <= 0)
                {
                    isNew = true;
                    store.Add(cert);
                }
                store.Close();
            }
            return isNew;
        }




        /// <summary>
        /// Get the DER encoded bytes of a PEM encoded certificate.
        /// </summary>
        /// <param name="data">The PEM text (UTF-8 encoded)</param>
        /// <returns>The DER encoded certificate</returns>
        /// <exception cref="Exception">The text doesn't contain a certificate header or footer</exception>
        /// <exception cref="FormatException">The text between the header and footer isn't valid base64</exception>
        public static Byte[] GetCertBytes(ReadOnlySpan<Byte> data) => GetCertBytes(Encoding.UTF8.GetString(data));

        /// <summary>
        /// Get the DER encoded bytes of a PEM encoded certificate.
        /// </summary>
        /// <param name="cert">The PEM text, the first certificate (everything between the first "-----BEGIN CERTIFICATE-----" and the following "-----END CERTIFICATE-----") is decoded,
        /// any following certificates (ex: in a full chain file) are ignored</param>
        /// <returns>The DER encoded certificate</returns>
        /// <exception cref="Exception">The text doesn't contain a certificate header or a footer after the header</exception>
        /// <exception cref="FormatException">The text between the header and footer isn't valid base64</exception>
        public static Byte[] GetCertBytes(String cert)
        {
            const String header = "-----BEGIN CERTIFICATE-----";
            const String footer = "-----END CERTIFICATE-----";
            var start = cert.IndexOf(header, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                throw new Exception("Not a valid certificate (no header)!");
            start += header.Length;

            var end = cert.IndexOf(footer, start, StringComparison.OrdinalIgnoreCase);
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
