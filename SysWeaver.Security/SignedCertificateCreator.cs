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
    /// Creates RSA TLS server certificates (self-signed or signed by a CA certificate) from validated <see cref="CertificateParams"/>.
    /// </summary>
    /// <remarks>
    /// Generated certificates use SHA256 with PKCS#1 padding, are not CA certificates, have the "TLS server authentication" extended key usage,
    /// and are valid from 4 days ago (to tolerate clock skew).
    /// The subject alternative names are built from localhost, the machine name, <see cref="Names"/> and the local IP addresses (as configured).
    /// The returned certificates are loaded with <see cref="X509KeyStorageFlags.MachineKeySet"/>, <see cref="X509KeyStorageFlags.PersistKeySet"/> and <see cref="X509KeyStorageFlags.Exportable"/>.
    /// Immutable after construction and thread safe.
    /// </remarks>
    public sealed class SignedCertificateCreator
    {
        /// <inheritdoc/>
        public override string ToString() => CommonName;

        /// <summary>
        /// Create a new certificate creator, resolving EnvInfo variables and validating all subject fields.
        /// </summary>
        /// <param name="p">The parameters to use when generating the certificate, null uses the <see cref="SelfSignedCertificateProviderParams"/> defaults.</param>
        /// <exception cref="Exception">A subject field contains '=' or ','.</exception>
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

        /// <summary>
        /// Get a common name, using "SysWeaver.App.$(AppName)" if none is supplied, with EnvInfo variables resolved and the result validated.
        /// </summary>
        /// <param name="cn">The configured common name, may be null or empty.</param>
        /// <param name="propertyName">Name of the property (used in the exception message).</param>
        /// <returns>The resolved common name.</returns>
        /// <exception cref="Exception">The common name contains '=' or ','.</exception>
        public static String GetValidatedCommonName(String cn, String propertyName)
        {
            if (String.IsNullOrEmpty(cn))
                cn = "SysWeaver.App.$(AppName)";
            return ValidateString(EnvInfo.ResolveText(cn), propertyName);
        }

        /// <summary>
        /// Get an ISO 3166 alpha-2 country code from a country value, using the current region if none is supplied.
        /// </summary>
        /// <param name="country">The configured country (any value understood by <c>IsoCountry.TryGet</c>, can use EnvInfo variables), may be null or empty.</param>
        /// <param name="propertyName">Name of the property (used in the exception message).</param>
        /// <returns>The two letter country code, or null if the country is unknown.</returns>
        public static String GetValidatedCountry(String country, String propertyName)
        {
            if (String.IsNullOrEmpty(country))
                country = EnvInfo.GetCurrentRegion();
            return ValidateString(IsoData.IsoCountry.TryGet(EnvInfo.ResolveText(country))?.Iso3166a2, propertyName);
        }

        /// <summary>
        /// Validate that a subject field value doesn't contain characters that would break the distinguished name ('=' and ',').
        /// </summary>
        /// <param name="s">The value to validate, null or empty is allowed.</param>
        /// <param name="pname">Name of the property (used in the exception message).</param>
        /// <returns>The input value.</returns>
        /// <exception cref="Exception"><paramref name="s"/> contains '=' or ','.</exception>
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

        /// <summary>
        /// Number of days that generated certificates are valid (at least 5).
        /// </summary>
        public readonly int ValidDays;
        /// <summary>
        /// The resolved common name (CN).
        /// </summary>
        public readonly string CommonName;
        /// <summary>
        /// The resolved locality (L), may be null.
        /// </summary>
        public readonly String Locality;
        /// <summary>
        /// The resolved organization (O), may be null.
        /// </summary>
        public readonly String Organization;
        /// <summary>
        /// The resolved organizational unit (OU), may be null.
        /// </summary>
        public readonly String Unit;
        /// <summary>
        /// The ISO 3166 alpha-2 country code (C), may be null.
        /// </summary>
        public readonly String Country;
        /// <summary>
        /// The resolved state or province (ST), may be null.
        /// </summary>
        public readonly String State;

        /// <summary>
        /// The resolved distinguished name qualifier (dnQualifier), may be null.
        /// </summary>
        public readonly String DistinguishedNameQualifier;
        /// <summary>
        /// The resolved subject serial number attribute (serialNumber), may be null. This is not the certificate serial number.
        /// </summary>
        public readonly String SerialNumber;
        /// <summary>
        /// The resolved title, may be null.
        /// </summary>
        public readonly String Title;
        /// <summary>
        /// The resolved surname (SN), may be null.
        /// </summary>
        public readonly String SurName;
        /// <summary>
        /// The resolved given name (GN), may be null.
        /// </summary>
        public readonly String GivenName;
        /// <summary>
        /// The resolved initials, may be null.
        /// </summary>
        public readonly String Initials;
        /// <summary>
        /// The resolved pseudonym, may be null.
        /// </summary>
        public readonly String Pseudonym;
        /// <summary>
        /// The resolved generation qualifier, may be null.
        /// </summary>
        public readonly String GenerationQualifier;
        /// <summary>
        /// The resolved email (E), may be null.
        /// </summary>
        public readonly String Email;


        /// <summary>
        /// Additional DNS names to add as SAN's (never null).
        /// </summary>
        public readonly String[] Names;
        /// <summary>
        /// The RSA key size in bits (a power of two).
        /// </summary>
        public readonly int RsaBits;
        /// <summary>
        /// True if the local IP addresses are added as SAN's.
        /// </summary>
        public readonly bool IncludeLanIPs;
        /// <summary>
        /// True if "localhost" and the loopback addresses are added as SAN's.
        /// </summary>
        public readonly bool IncludeLocalHost;
        /// <summary>
        /// True if the machine name is added as a SAN.
        /// </summary>
        public readonly bool IncludeMachineName;



        /// <summary>
        /// Build the subject distinguished name, ex: "CN=SysWeaver.App.MyService,C=SE,O=SysWeaver,OU=Platform".
        /// Only non-empty fields are included.
        /// </summary>
        /// <returns>The subject distinguished name.</returns>
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
        /// Test if a certificate was generated using the same parameters as this creator, i.e. it has the same subject alternative names (in the same order)
        /// and all non-empty subject fields of this creator are present in the certificate subject with the same value.
        /// Used to decide if a cached certificate can be reused.
        /// </summary>
        /// <param name="cert">Certificate to test, may not be null.</param>
        /// <returns>True if it's the same, else false.</returns>
        /// <remarks>
        /// The certificate subject is parsed by splitting on ',' and uses the attribute names as formatted by the platform.
        /// Throws if the subject contains duplicate attribute names.
        /// </remarks>
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
                var key = s.Substring(0, t).FastTrimToLower();
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
        /// Creates a new self-signed certificate (with a new RSA key) using the parameters of this creator.
        /// </summary>
        /// <returns>A new certificate including the private key, the caller owns it and should dispose it.</returns>
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
                        var wk = X509CertificateLoader.LoadPkcs12(pp.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                        //var wk = new X509Certificate2(pp.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                        return wk;
                    }
                }
            }
        }


        /// <summary>
        /// Create a new certificate (with a new RSA key) using the parameters of this creator, signed by a parent (CA) certificate.
        /// The validity period is clamped to the validity period of the parent certificate.
        /// </summary>
        /// <param name="parentCert">The parent certificate to use for signing (must have a private key).</param>
        /// <returns>A new certificate including the private key, the caller owns it and should dispose it.</returns>
        /// <remarks>
        /// Every certificate is issued with a random 128 bit serial number.
        /// </remarks>
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
                //  Random positive serial number (unique per issuer as required by RFC 5280), the leading byte is kept in 0x40-0x7F for a minimal DER encoding
                var serial = RandomNumberGenerator.GetBytes(16);
                serial[0] = (Byte)((serial[0] & 0x7f) | 0x40);
                using (var temp = req.Create(parentCert, from, to, serial))
                using (var tc = temp.CopyWithPrivateKey(rsa))
                {
                    var wk = X509CertificateLoader.LoadPkcs12(tc.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                    //var wk = new X509Certificate2(tc.Export(X509ContentType.Pfx), (String)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                    return wk;
                }
            }
        }


    }



}
