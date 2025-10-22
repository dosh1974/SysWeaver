using SysWeaver.Auth;
using System;
using System.IO;
using System.Linq;
using SysWeaver.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Media;
using System.Text;
using SysWeaver.Compression;
using System.Threading;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.MicroService
{



    [WebApiUrl("auth")]
    [WebMenuPath("User", "User", "User", "User management", "IconUser")]
    [WebMenuEmbedded("User", "User/Logout", "Sign out", "auth/Logout.html", "Click to sign out", "IconLogout", 0, "")]
    [IsMicroService]
    [RequiredDep<AuthorizerBase>]
    [OptionalDep<IUserImageHandler>]
    public sealed class AuthManagerService : IHttpServerModule, IDisposable
    {
        public override string ToString() => "[Service] " + Auth;


const String ReadMeText = @"
This is the root folder for user images, the format is:

auth/UserImages/[Guid]/[Size]

[Guid] is the encoded user guid (as returned by most API's).
[Size] is one of the predefined sizes (depends on the image provider).

There is a special [Guid] named Current that returns the image of the currently signed in user:
auth/UserImages/Current/[Size]

There are two special [Size] values: large and small:
auth/UserImages/Current/small or auth/UserImages/Current/large

This server supports the following sizes:
";
        const String ImageRoot = "auth/UserImages/";

        public AuthManagerService(ServiceManager manager, AuthManagerServiceParams p = null)
        {
            Manager = manager;
            AsyncHandler = GetAsyncHandler;
            p = p ?? new AuthManagerServiceParams();
            var auths = manager.GetAll<AuthorizerBase>(ServiceInstanceTypes.Any, ServiceInstanceOrders.Oldest).ToArray();
            var auth = new AuthManager(p, auths);
            Auth = auth;
            AllowEmailIps = p.AllowEmailIps;

            var siteName = p.SiteName ?? Path.GetFileNameWithoutExtension(EnvInfo.Executable);
            SiteName = siteName;

            manager.Register(auth, p.InstanceName, false, typeof(AuthManagerParams));
            manager.OnServiceAdded += Manager_OnServiceAdded;
            manager.OnServiceRemoved += Manager_OnServiceRemoved;



            SetUserImageHandler(manager.TryGet<IUserImageHandler>());
            DefaultUserImageHandler = manager.TryGet<IDefaultUserImageHandler>();

            //ImageRoot = imageRoot;
            ImageRootLen = ImageRoot.Length;
            OnlyForPrefixes = [ImageRoot];

            AddReadMe(manager.TryGet<StaticDataHttpServerModule>());
        }

        void SetUserImageHandler(IUserImageHandler ui)
        {
            UserImageHandler = ui;
            var d = new Dictionary<String, int>(StringComparer.Ordinal);
            int min = 0;
            int max = 0;
            if (ui != null)
            {
                min = int.MaxValue;
                max = int.MinValue;
                foreach (var x in ui.Sizes)
                {
                    if (x < min)
                        min = x;
                    if (x > max)
                        max = x;
                    d[x.ToString()] = x;
                }
            }
            d["small"] = min;
            d["large"] = max;
            UserImageSizes = d.Freeze();
        }


        int ReadMeAdded;

        StaticDataHttpServerModule StaticMod;

        void AddReadMe(StaticDataHttpServerModule sm)
        {
            if (sm == null)
                return;
            if (Interlocked.CompareExchange(ref ReadMeAdded, 1, 0) != 0)
                return;
            StaticMod = sm;
            var t = ReadMeText;
            foreach (var x in UserImageSizes.OrderBy(x => x.Key))
            {
                var n = x.Key;
                var s = x.Value;
                t +=
                    s <= 0
                    ?
                    String.Concat("- ", n, " ", ImageRoot, "[Guid]/", n, " (Generated SVG)\n")
                    :
                    String.Concat("- ", n, " ", ImageRoot, "[Guid]/", n, " (", s, 'x', s, ")\n");
            }
            sm.AddText(ImageRoot + "ReadMe.txt", "Embedded in " + GetType().Assembly, t);
        }

        void RemoveReadMe()
        {
            if (Interlocked.CompareExchange(ref ReadMeAdded, 0, 1) != 1)
                return;
            StaticMod.Remove(ImageRoot + "ReadMe.txt");
        }

        void Manager_OnServiceRemoved(object service, ServiceInfo info)
        {
            var sa = service as AuthorizerBase;
            if (sa != null)
                Auth.RemoveAuth(sa);
            var sm = service as StaticDataHttpServerModule;
            if (sm != null)
                RemoveReadMe();
            var ui = service as IUserImageHandler;
            if (ui != null)
                SetUserImageHandler(null);
            var dui = service as IDefaultUserImageHandler;
            if (dui != null)
                DefaultUserImageHandler = null;
        }

        void Manager_OnServiceAdded(object service, ServiceInfo info)
        {
            var sa = service as AuthorizerBase;
            if (sa != null)
                Auth.AddAuth(sa);
            var sm = service as StaticDataHttpServerModule;
            if (sm != null)
                AddReadMe(sm);
            var ui = service as IUserImageHandler;
            if (ui != null)
                SetUserImageHandler(ui);
            var dui = service as IDefaultUserImageHandler;
            if (dui != null)
                DefaultUserImageHandler = dui;
        }

        readonly String SiteName;
        readonly ServiceManager Manager;
        IUserImageHandler UserImageHandler;
        IDefaultUserImageHandler DefaultUserImageHandler;
        IReadOnlyDictionary<String, int> UserImageSizes;

        public readonly bool AllowEmailIps;
        public readonly AuthManager Auth;


        /// <summary>
        /// Validate an email address (throws if invalid)
        /// </summary>
        /// <param name="mail">The address to validate</param>
        /// <returns>True if the email address seems to be valid</returns>
        [WebApi]
        public bool ValidateEmailAddress(String mail)
            => ValidateEmailAddress(mail, true);


        /// <summary>
        /// Validate an email address
        /// </summary>
        /// <param name="mail">The address to validate</param>
        /// <param name="exceptions">If true, exceptions are thrown</param>
        /// <returns>True if the email address seems to be valid</returns>
        public bool ValidateEmailAddress(String mail, bool exceptions)
        {
            try
            {
                switch (StringValidate.Email(mail?.Trim()))
                {
                    case DomainTypes.ComputerName:
                        throw new Exception("NetBIOS computer names are not allowed!");
                    case DomainTypes.IPv4:
                    case DomainTypes.IPv6:
                        if (!AllowEmailIps)
                            throw new Exception("IP-addresses are not allowed!");
                        break;
                }
                return true;
            }
            catch
            {
                if (exceptions)
                    throw;
            }
            return false;
        }



        /// <summary>
        /// Validate an international phone number
        /// </summary>
        /// <param name="phoneNumber">The international phone number to validate (all non-digits are ignored)</param>
        /// <returns>True if the email address seems to be valid</returns>
        [WebApi]
        public bool ValidatePhoneNumber(String phoneNumber)
            => ValidatePhoneNumber(ref phoneNumber, true);


        /// <summary>
        /// Validate an international phone number
        /// </summary>
        /// <param name="phoneNumber">The international phone number to validate (all non-digits are ignored)</param>
        /// <returns>True if the email address seems to be valid</returns>
        public bool ValidatePhoneNumber(ref String phoneNumber)
            => ValidatePhoneNumber(ref phoneNumber, true);

        /// <summary>
        /// Validate an international phone number
        /// </summary>
        /// <param name="phoneNumber">The international phone number to validate (all non-digits are ignored)</param>
        /// <param name="exceptions">If true, exceptions are thrown</param>
        /// <returns>True if the email address seems to be valid</returns>
        public bool ValidatePhoneNumber(ref String phoneNumber, bool exceptions)
        {
            try
            {
                PhonePrefix.GetValidatedPhoneNumber(out var n, out var c, out var a, out var b, phoneNumber);
                phoneNumber = String.Concat(a, ' ', b);
                return true;
            }
            catch
            {
                if (exceptions)
                    throw;
            }
            return false;
        }

        /// <summary>
        /// Get details about an international phone number
        /// </summary>
        /// <param name="phoneNumber">The international phone number to get information about</param>
        /// <returns>Information about the phone number</returns>
        [WebApi]
        public PhoneNumberInfo GetPhoneNumberInfo(String phoneNumber)
        {
            var countries = PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var prefix, out var local, phoneNumber);
            return new PhoneNumberInfo
            {
                Prefix = prefix,
                Local = local,
                Name = name,
                IsoCountry = isoCountry,
                Prefixes = countries.Select(x => new PhonePrefixInfo
                {
                    Name = x.Name,
                    IsoCountry = x.IsoCountry,
                    CountryCode = x.CountryCode,
                    RegionPrefixes = x.RegionPrefix,
                }).ToArray(),
            };
        }

        /// <summary>
        /// Get details about the phone number prefixes for a partial international phone number
        /// </summary>
        /// <param name="partialPhoneNumber">The partial international phone number to get phone number prefix information about</param>
        /// <returns>Phone number prefix information for the partial international phone number</returns>
        [WebApi]
        public PhonePrefixInfo[] GetPhonePrefixInfo(String partialPhoneNumber)
        {
            var countries = PhonePrefix.Identify(partialPhoneNumber);
            return countries.Select(x => new PhonePrefixInfo
            {
                Name = x.Name,
                IsoCountry = x.IsoCountry,
                CountryCode = x.CountryCode,
                RegionPrefixes = x.RegionPrefix,
            }).ToArray();
        }


        /// <summary>
        /// Get information about the currently logged in user
        /// </summary>
        /// <param name="context">Context, handled internally</param>
        /// <returns>User information</returns>
        [WebApi]
        [WebApiClientCache(10)]
        [WebApiRequestCache(9)]
        public AuthInfo GetUser(HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            var res = new AuthInfo();
            res.Language = session.Language;
            if (auth == null)
            {
                res.Guid = "none";
                return res;
            }
            res.Guid = auth.Guid.ToHex();
            res.NickName = auth.NickName;
            res.AutoNickName = auth.AutoNickName;
            res.Succeeded = true;
            res.Username = auth.Username;
            res.Domain = auth.Domain;
            res.Tokens = auth.Tokens.ToArray();
            return res;
        }


        public void Dispose()
        {
            var m = Manager;
            m.OnServiceAdded -= Manager_OnServiceAdded;
            m.OnServiceRemoved -= Manager_OnServiceRemoved;
            m.Unregister(Auth);
            RemoveReadMe();
        }


        #region IHttpServerModule

        //readonly String ImageRoot;
        readonly int ImageRootLen;
        
        public String[] OnlyForPrefixes { get; init; }


        /// <summary>
        /// An optional async handler
        /// </summary>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }

        /// <summary>
        /// Remove a user image from the cache
        /// </summary>
        /// <param name="userGuid"></param>
        public void InvalidateUserImageCache(String userGuid)
        {
            if (userGuid != null)
            {
                var c = UserImageCache;
                foreach (var x in UserImageSizes)
                {
                    var key = String.Concat(userGuid, '\n', x.Value);
                    c.Remove(key);
                }
            }
        }

        /// <summary>
        /// Remove a user image from the cache
        /// </summary>
        /// <param name="context"></param>
        public void InvalidateUserImageCache(HttpServerRequest context)
            => InvalidateUserImageCache(context.Session.Auth?.Guid);

        /// <summary>
        /// Determine if the request can be handled by this module
        /// </summary>
        /// <param name="context">The incoming request</param>
        /// <returns>A handler for the request or null if it can't be handled by this module</returns>
        async ValueTask<IHttpRequestHandler> GetAsyncHandler(HttpServerRequest context)
        {
            var local = context.LocalUrl;
            //var root = ImageRoot;
            //if (!local.FastStartsWith(root))
            //return null;
            local = local.Substring(ImageRootLen);
            var next = local.IndexOf('/');
            if (next < 0)
                return null;
            var fname = local.Substring(next + 1);
            if (!UserImageSizes.TryGetValue(fname, out var size))
                return null;
            var cache = UserImageCache;
            var guid = local.Substring(0, next);
            bool isCurent = guid.FastEquals("Current");
            String key = null;
            if (isCurent)
            {
                guid = context.Session?.Auth?.Guid;
                key = String.Concat(guid, '\n', size);
                //context.Etag = guid;
                //cache.Remove(key);
            }
            else
            {
                try
                {
                    guid = guid.ToStringFromHex();
                    key = String.Concat(guid, '\n', size);
                }
                catch
                {
                    guid = null;
                }
            }
            if (guid != null)
            {
                var h = await cache.GetOrUpdateValueAsync(key, async __guid =>
                {
                    var eTag = HttpServerTools.ToEtag(DateTime.UtcNow);
                    var img = UserImageHandler;
                    if (img != null)
                    {
                        var r = await img.Get(guid, size).ConfigureAwait(false);
                        if (r != null)
                            return ValueTuple.Create(eTag, r);
                    }

                    var def = DefaultUserImageHandler;
                    if (def != null)
                    {
                        var r = await def.Get(guid, size).ConfigureAwait(false);
                        if (r != null)
                            return ValueTuple.Create(eTag, r);
                    }
                    return ValueTuple.Create(eTag, await DefaultImages.GetOrUpdateAsync(guid, GetDefaultImage).ConfigureAwait(false));
                }).ConfigureAwait(false);
                var rr = h.Item2;
                if (rr != null)
                {
                    context.ClientCacheDuration = isCurent ? 0 : 30;
                    context.RequestCacheDuration = isCurent ? 0 : 25;
                    context.Etag = h.Item1;
                    return rr;
                }
            }
            return await context.Server.InternalRedirect(this, context, context.Prefix + "auth/icons/user_not_found.svg").ConfigureAwait(false);
        }



        readonly FastMemCache<String, ValueTuple<String, IHttpRequestHandler>> UserImageCache = new FastMemCache<string, ValueTuple<String, IHttpRequestHandler>>(TimeSpan.FromSeconds(30), StringComparer.Ordinal);

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null)
        {
            if (root == null)
            {
                foreach (var x in UserImageSizes)
                    yield return new HttpServerEndPoint(ImageRoot + "Current/" + x.Key, "GET", 0, 0, false, null, null, null, HttpServerEndpointTypes.File, "Dynamic content", null, HttpServerTools.StartedTime, "image/*", null);
            }else
            {
                if (root.FastStartsWith(ImageRoot))
                {
                    var rest = root.Substring(ImageRootLen);
                    if (rest.Length == 0)
                    {
                        yield return new HttpServerEndPoint(ImageRoot + "Current", "Implicit folder", HttpServerTools.StartedTime);
                    }
                    else
                    {
                        if (rest.FastEquals("Current/"))
                        {
                            foreach (var x in UserImageSizes)
                                yield return new HttpServerEndPoint(ImageRoot + "Current/" + x.Key, "GET", 0, 0, false, null, null, null, HttpServerEndpointTypes.File, "Dynamic content", null, HttpServerTools.StartedTime, "image/*", null);
                        }
                    }
                }
            }
        }

        #endregion//IHttpServerModule

        static readonly HttpCompressionPriority SvgCompression = HttpCompressionPriority.GetSupportedEncoders();

        
        public static StaticMemoryHttpRequestHandler GenSvgImage(String name, String guid)
        {
            var svgS = new SvgScene(256, 256);
            var hash = (int)QuickHash.Hash(name + guid);
            svgS.AddFavIcon(name, null, null, hash);
            var svgText = svgS.ToSvg();
            var enc = Encoding.UTF8;
            var svgData = enc.GetBytes(svgText);
            var cmp = CompBrotliNET.Instance;
            var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
            var svgHandler = new StaticMemoryHttpRequestHandler("icon.svg", "Generated", svgMem, MimeTypeMap.Svg, SvgCompression, 30, 15, null, cmp);
            return svgHandler;
        }
            
        async Task<StaticMemoryHttpRequestHandler> GetDefaultImage(String userGuid)
        {
            var ui = await Auth.FindUserFromGuid(userGuid).ConfigureAwait(false);
            if (ui == null)
                return null;
            var name = ui.NickName ?? ui.Username ?? ui.Email ?? ui.Guid;
            return GenSvgImage(name, ui.Guid);
        }

        readonly FastMemCache<String, StaticMemoryHttpRequestHandler> DefaultImages = new (TimeSpan.FromMinutes(5), StringComparer.Ordinal);

        /// <summary>
        /// Try to login a user using a one time use token, typically used when integrating some other page.
        /// </summary>
        /// <param name="oneTimeToken">A one time use token, must have the format "AuthorizerName@onetimerandomtoken"</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>User information</returns>
        [WebApi]
        [WebApiAudit(AuthTools.AuditGroup)]
        [WebApiAuditFilterParams(nameof(AuditInputFilter_TokenAuth))]
        public async Task<AuthInfo> TokenAuth(String oneTimeToken, HttpServerRequest context)
        {
            var s = context?.Session;
            if (s == null)
                return AuthInfo.Failed;
            if (s.Auth != null)
                return AuthInfo.Failed;
            var am = this.Auth;
            var auth = await am.TokenAuth(oneTimeToken).ConfigureAwait(false);
            await TaskExt.RandomDelay().ConfigureAwait(false);
            if (auth == null)
                return AuthInfo.Failed;
            s.SetAuth(auth);
            await context.Server.RunOnLogin(s).ConfigureAwait(false);
            s.InvalidateCache();
            return GetUser(context);
        }

        static Object AuditInputFilter_TokenAuth(long id, HttpServerRequest request, Object obj)
            => (obj as String).SecureStart(8);

    }

}
