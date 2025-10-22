using System.Security.Cryptography.X509Certificates;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Security.Cryptography;

namespace SysWeaver.Security
{

    /// <summary>
    /// Creates certificates
    /// </summary>
    public sealed class SignedCertificateCreator
    {
        public override string ToString() => CommonName;

        /// <summary>
        /// Create a new certificate creator
        /// </summary>
        /// <param name="p">The paramaters to use when generating the certificate</param>
        public SignedCertificateCreator(CertificateParams p)
        {
            p = p ?? new SelfSignedCertificateProviderParams();
            ValidDays = Math.Max(5, p.ValidDays);

            CommonName = GetValidatedCommonName(p.CommonName, nameof(p.CommonName));
            Locality = ValidateString(EnvInfo.ResolveText(p.Locality), nameof(p.Locality));
            Organization = ValidateString(EnvInfo.ResolveText(p.Organization), nameof(p.Organization));
            Unit = ValidateString(EnvInfo.ResolveText(p.Unit), nameof(p.Unit));
            Country = GetValidatedCountry(p.Country, nameof(p.Country));
            State = ValidateString(EnvInfo.ResolveText(p.State), nameof(p.State));

            DistinguishedNameQualifier = ValidateString(EnvInfo.ResolveText(p.DistinguishedNameQualifier), nameof(p.DistinguishedNameQualifier));
            SerialNumber = ValidateString(EnvInfo.ResolveText(p.SerialNumber), nameof(p.SerialNumber));
            Title = ValidateString(EnvInfo.ResolveText(p.Title), nameof(p.Title));
            SurName = ValidateString(EnvInfo.ResolveText(p.SurName), nameof(p.SurName));
            GivenName = ValidateString(EnvInfo.ResolveText(p.GivenName), nameof(p.GivenName));
            Initials = ValidateString(EnvInfo.ResolveText(p.Initials), nameof(p.Initials));
            Pseudonym = ValidateString(EnvInfo.ResolveText(p.Pseudonym), nameof(p.Pseudonym));
            GenerationQualifier = ValidateString(EnvInfo.ResolveText(p.GenerationQualifier), nameof(p.GenerationQualifier));
            Email = ValidateString(EnvInfo.ResolveText(p.Email), nameof(p.Email));

            Names = p.Names ?? [];
            var b = p.RsaBits;
            if (b <= 0)
                b = 2048;
            RsaBits = b.EnsurePow2();
            IncludeLanIPs = p.IncludeLanIPs;
            IncludeLocalHost = p.IncludeLocalHost;
            IncludeMachineName = p.IncludeMachineName;
        }

        public static String GetValidatedCommonName(String cn, String propertyName)
        {
            if (String.IsNullOrEmpty(cn))
                cn = "SysWeaver.App.$(AppName)";
            return ValidateString(EnvInfo.ResolveText(cn), propertyName);
        }

        public static String GetValidatedCountry(String country, String propertyName)
        {
            if (String.IsNullOrEmpty(country))
                country = EnvInfo.GetCurrentRegion();
            return ValidateString(IsoData.IsoCountry.TryGet(EnvInfo.ResolveText(country))?.Iso3166a2, propertyName);
        }

        public static String ValidateString(String s, String pname)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            foreach (var c in s)
            {
                if ((c == '=') || (c == ','))
                    throw new Exception("Invalid character! '" + c + "' is not allowed for paramater " + pname.ToQuoted());
            }
            return s;
        }

        public readonly int ValidDays;
        public readonly string CommonName;
        public readonly String Locality;
        public readonly String Organization;
        public readonly String Unit;
        public readonly String Country;
        public readonly String State;

        public readonly String DistinguishedNameQualifier;
        public readonly String SerialNumber;
        public readonly String Title;
        public readonly String SurName;
        public readonly String GivenName;
        public readonly String Initials;
        public readonly String Pseudonym;
        public readonly String GenerationQualifier;
        public readonly String Email;


        public readonly String[] Names;
        public readonly int RsaBits;
        public readonly bool IncludeLanIPs;
        public readonly bool IncludeLocalHost;
        public readonly bool IncludeMachineName;



        public String GetSubject()
        {
            var sb = new StringBuilder();
            String t;
            sb.Append("CN=").Append(CommonName);
            t = Country;
            if (!String.IsNullOrEmpty(t))
                sb.Append(",C=").Append(t);
            t = State;
            if (!String.IsNullOrEmpty(t))
                sb.Append(",ST=").Append(t);
            t = Locality;
            if (!String.IsNullOrEmpty(t))
                sb.Append(",L=").Append(t);
            t = Organization;
            if (!String.IsNullOrEmpty(t))
                sb.Append(",O=").Append(t);
            t = Unit;
            if (!String.IsNullOrEmpty(t))
                sb.Append(",OU=").Append(t);
            t = DistinguishedNameQualifier;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", dnQualifier=").Append(t);
            t = SerialNumber;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", serialNumber=").Append(t);
            t = Title;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", title=").Append(t);
            t = SurName;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", SN=").Append(t);
            t = GivenName;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", GN=").Append(t);
            t = Initials;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", initials=").Append(t);
            t = Pseudonym;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", pseudonym=").Append(t);
            t = GenerationQualifier;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", generationQualifier=").Append(t);
            t = Email;
            if (!String.IsNullOrEmpty(t))
                sb.Append(", E=").Append(t);
            return sb.ToString();
        }


        /// <summary>
        /// Test if a certificate uses the same paramaters as this
        /// </summary>
        /// <param name="cert">Certificate to test</param>
        /// <returns>True if it's the same, else false</returns>
        public bool IsSame(X509Certificate2 cert)
        {
            var expectedNames = cert.GetSubjectAlternativeNames().ToList();
            var configNames = GetSans().Build().GetSubjectAlternativeNames().ToList();
            var l = expectedNames.Count;
            if (l != configNames.Count)
                return false;
            for (int i = 0; i < l; i++)
            {
                if (!String.Equals(expectedNames[i], configNames[i], StringComparison.Ordinal))
                    return false;
            }
            Dictionary<String, String> vals = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var s in cert.Subject.Split(','))
            {
                var t = s.IndexOf('=');
                var key = s.Substring(0, t).Trim().FastToLower();
                vals.Add(key, s.Substring(t + 1).Trim());
            }
            bool Test(String value, params String[] keys)
            {
                if (String.IsNullOrEmpty(value))
                    return false;
                foreach (var key in keys)
                {
                    if (vals.TryGetValue(key, out var val))
                        return !String.Equals(value, val, StringComparison.Ordinal);
                }
                return true;
            }
            if (Test(CommonName, "cn", "commonname"))
                return false;
            if (Test(Country, "c", "countryname"))
                return false;
            if (Test(State, "st", "stateorprovincename"))
                return false;
            if (Test(Locality, "l", "locality"))
                return false;
            if (Test(Organization, "o", "organizationname"))
                return false;
            if (Test(Unit, "ou", "organizationalunitname"))
                return false;
            if (Test(DistinguishedNameQualifier, "dnqualifier"))
                return false;
            if (Test(SerialNumber, "serialnumber"))
                return false;
            if (Test(Title, "title"))
                return false;
            if (Test(SurName, "sn", "surname"))
                return false;
            if (Test(GivenName, "gn", "givenname"))
                return false;
            if (Test(Initials, "initials"))
                return false;
            if (Test(Pseudonym, "pseudonym"))
                return false;
            if (Test(GenerationQualifier, "generationqualifier"))
                return false;
            if (Test(Email, "e"))
                return false;
            return true;
        }

        SubjectAlternativeNameBuilder GetSans()
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
            void AddDns(String s)
            {
                s = s.Trim();
                if (s.Length <= 0)
                    return;
                if (seen.Add(s.FastToLower()))
                    sanBuilder.AddDnsName(s);
            }
            void AddIP(IPAddress s)
            {
                if (seen.Add(s.ToString().FastToLower()))
                    sanBuilder.AddIpAddress(s);
            }
            if (IncludeLocalHost)
            {
                AddDns("localhost");
                AddIP(IPAddress.Loopback);
                AddIP(IPAddress.IPv6Loopback);
            }
            if (IncludeMachineName)
                AddDns(Environment.MachineName);
            foreach (var x in Names.OrderBy(x => x))
                AddDns(x);
            if (IncludeLanIPs)
            {
                foreach (var x in NetworkTools.GetLocalIps())
                    AddIP(x);
            }
            return sanBuilder;
        }

        /// <summary>
        /// Creates a self signed certificate with the given params
        /// </summary>
        /// <returns>A new certificate</returns>
        public X509Certificate2 CreateSelfSigned()
        {
            var subject = GetSubject();
            using (RSA rsa = RSA.Create(RsaBits))
            {
                CertificateRequest req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));
                var sb = GetSans().Build();
                req.CertificateExtensions.Add(sb);
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection 
                    {   
                        new Oid("1.3.6.1.5.5.7.3.1"),  // TLS Server auth
                    },true));
                req.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
                var now = DateTime.Now;
                var from = now.AddDays(-4);
                var to = now.AddDays(ValidDays);
                using (var temp = req.CreateSelfSigned(from, to))
                {
                    using (var tc = temp.HasPrivateKey ? null : temp.CopyWithPrivateKey(rsa))
                    {
                        var pp = tc ?? temp;
                        var wk = new X509Certificate2(pp.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                        return wk;
                    }
                }
            }
        }


        /// <summary>
        /// Create a certificated with the given paramaters, signed by the parent cert (must have private keys)
        /// </summary>
        /// <param name="parentCert">The parent certificate to use for signing (must have private keys)</param>
        /// <returns>A new certificate</returns>
        public X509Certificate2 Create(X509Certificate2 parentCert)
        {
            var subject = GetSubject();
            using (RSA rsa = RSA.Create(RsaBits))
            {
                CertificateRequest req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));
                var sb = GetSans().Build();
                req.CertificateExtensions.Add(sb);
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"),  // TLS Server auth
                    }, true));
                req.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
                var now = DateTime.Now;
                var from = now.AddDays(-4);
                var to = now.AddDays(ValidDays);
                if (from < parentCert.NotBefore)
                    from = parentCert.NotBefore;
                if (to > parentCert.NotAfter)
                    to = parentCert.NotAfter;
                using (var temp = req.Create(parentCert, from, to, [1, 2, 3, 4]))
                using (var tc = temp.CopyWithPrivateKey(rsa))
                {
                    var wk = new X509Certificate2(tc.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                    return wk;
                }
            }
        }


    }



}
