using SysWeaver.Auth;
using SysWeaver.Net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

using Fido2NetLib;
using Fido2NetLib.Objects;

namespace SysWeaver.MicroService
{



    [IsMicroService]
    [RequiredDep<UserManagerService>]
    [OptionalDep<IQrCodeService>]
    [WebApiUrl("auth/passkey")]
    [WebMenuEmbedded("User", "User/AddPassKey", "Add passkey", "auth/AddPassKey.html", "Click to add a passkey for this device", "IconAddPasskey", 31, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanCreateLocalPassKey))]
    [WebMenuEmbedded("User", "User/AddPassKeyOther", "Add passkey (other device)", "auth/AddPassKeyOnOtherDevice.html", "Click to show a QR code that can be used to add a passkey on some other device", "IconAddPasskeyOther", 32, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanCreateRemotePassKey))]
    [WebMenuEmbedded("User", "User/UsePassKey", "Sign in with passkey", "auth/UsePassKey.html", "Click to sign in using your passkey for this device", "IconUsePasskey", 2, null, true, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanSignInWithPassKey))]
    public sealed class PassKeyService
    {

        #region Dynamic menu

        async Task<bool> CanSignInWithPassKey(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            return await Um.HaveAnyPasskeysForDevice(s.DeviceId).ConfigureAwait(false);
        }

        async Task<bool> CanCreateLocalPassKey(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var um = Um;
            var uid = um.GetUid(s.Auth);
            if (uid == 0)
                return false;
            return !(await um.HaveAssignedPassKey(s.DeviceId, uid).ConfigureAwait(false));
        }

        Task<bool> CanCreateRemotePassKey(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return TaskExt.FalseTask;
            if (QR == null)
                return TaskExt.FalseTask;
            var uid = Um.GetUid(s.Auth);
            if (uid == 0)
                return TaskExt.FalseTask;
            return TaskExt.TrueTask;
        }

        #endregion//Dynamic menu


        public PassKeyService(ServiceManager manager, PassKeyParams p)
        {
            p = p ?? new PassKeyParams();
            M = manager;
            Um = manager.Get<UserManagerService>();
            Server = manager.TryGet<HttpServerBase>();
            manager.OnServiceAdded += Manager_OnServiceAdded;
            Params = p;
            RpName = p.RpName ?? EnvInfo.AppName;
            RpId = p.RpId;
            QR = manager.TryGet<IQrCodeService>();


        }

        readonly IQrCodeService QR;

        /// <summary>
        /// Show a QR code with a link that add's a passkey on some other device
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        [WebApi("GetQR.svg")]
        [WebApiRaw("svg")]
        [WebApiClientCache(10)]
        [WebApiRequestCache(9)]
        public async Task<ReadOnlyMemory<Byte>> GetQR(HttpServerRequest context)
        {
            var qr = QR;
            if (qr == null)
                throw new Exception("QR code generation support isn't added to the server");
            var token = await Um.GetShareDeviceToken(context).ConfigureAwait(false);
            if (token == null)
                throw new NoUserLoggedInException();
            String link = String.Concat(context.Prefix ?? "", "auth/AddPassKey.html?token=" + token);
            var svg = qr.CreateQrCode(link);
            return Encoding.UTF8.GetBytes(svg);
        }

        readonly String RpName;
        readonly String RpId;

        void Manager_OnServiceAdded(object service, ServiceInfo info)
        {
            var t = service as HttpServerBase;
            if (t == null)
                return;
            Server = t;
        }

        HttpServerBase Server;
        readonly ServiceManager M;

        Fido2 GetLib()
        {
            var l = Lib;
            if (l != null)
                return l;
            lock (this)
            {
                l = Lib;
                if (l != null)
                    return l;
                var p = Params;
                var lt = Math.Max(1, p.ChallengeLifeTime);
                HashSet<String> prefixes = new HashSet<String>();
                List<String> aliases = new List<string>();
                aliases.Add("localhost");
                aliases.Add("127.0.0.1");
                foreach (var x in NetworkTools.GetAllLanIps())
                    aliases.Add(x.ToString());
                var s = Server;
                if (s != null)
                {
                    foreach (var pr in s.AllPrefixes)
                    {
                        if (!pr.StartsWith("https://"))
                            continue;
                        AddPrefix(prefixes, pr, aliases);
                    }
                }
                var pp = p.Prefixes;
                if (pp != null)
                {
                    foreach (var pr in pp)
                    {
                        if (!pr.StartsWith("https://"))
                            continue;
                        AddPrefix(prefixes, pr, aliases);
                    }
                }
                l = new Fido2(new Fido2Configuration
                {
                    ServerName = RpId,
                    ChallengeSize = 32,
                    Timeout = (uint)(lt * 1000),
                    Origins = prefixes,
                });
                Lib = l;
                return l;
            }
        }

        static void AddPrefix(HashSet<String> prefixes, String pr, List<String> aliases)
        {
            var li = pr.IndexOf('/', 8);
            var name = li < 0 ? pr : pr.Substring(0, li);
            if (name.Contains("*"))
            {
                foreach (var a in aliases)
                    prefixes.Add(name.Replace("*", a));
            }
            else
            {
                prefixes.Add(name);
            }
        }

        static readonly Char[] End = ":/".ToCharArray();

        static String FixRp(String pr)
        {
            var p = pr.IndexOf("://");
            p += 3;
            var li = pr.IndexOfAny(End, p);
            return li < 0 ? pr.Substring(p) : pr.Substring(p, li - p);
        }

        Fido2 Lib;
        readonly PassKeyParams Params;
           
        public override string ToString() => "Provides an API for logging in securely using FIDO2 passkeys";

        readonly UserManagerService Um;

        static readonly AuthInfo Failed = new AuthInfo { };

        static readonly Dictionary<String, PublicKeyCredentialType> Pkt = new Dictionary<string, PublicKeyCredentialType>(StringComparer.Ordinal)
        {
            {  PublicKeyCredentialType.PublicKey.ToString().RemoveCamelCase('-').FastToLower(), PublicKeyCredentialType.PublicKey },
        };

        #region API

        #region Auth

        /// <summary>
        /// Get a passkey challenge for userless and passwordless login.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public async Task<PassKeyGetOptions> GetAuthChallenge(HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth != null)
                throw new UserAlreadyLoggedInException();
            var lib = GetLib();
            var options = lib.GetAssertionOptions(null, null);
            options.RpId = FixRp(context.Prefix);
            var userCredIds = await Um.GetPublicKeyIdsForDevice(session.DeviceId).ConfigureAwait(false);
            if ((userCredIds != null) && (userCredIds.Count > 0))
                options.AllowCredentials = userCredIds.Select(x => new PublicKeyCredentialDescriptor(Convert.FromBase64String(x))).ToList();
            context.Session.Set("FidoAuthOptions", options);
            var res = new PassKeyGetOptions
            {
                publicKey = new PassKeyGetCredential(options),
            };
            return res;
        }

        /// <summary>
        /// Get a passkey challenge for a specific user for passwordless login.
        /// </summary>
        /// <param name="userIdentifier">User identifier (typically the email)</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public async Task<PassKeyGetOptions> GetUserAuthChallenge(String userIdentifier, HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth != null)
                throw new UserAlreadyLoggedInException();
            var lib = GetLib();
            var options = lib.GetAssertionOptions(null, null);
            options.RpId = FixRp(context.Prefix);
            var userCredIds = await Um.GetPublicKeyIdsForUser(userIdentifier).ConfigureAwait(false);
            if ((userCredIds == null) || (userCredIds.Count <= 0))
                return null;
            options.AllowCredentials = userCredIds.Select(x => new PublicKeyCredentialDescriptor(Convert.FromBase64String(x))).ToList();
            context.Session.Set("FidoAuthOptions", options);
            var res = new PassKeyGetOptions
            {
                publicKey = new PassKeyGetCredential(options),
            };
            return res;
        }


        /// <summary>
        /// Authenticate / login using a pass key request
        /// </summary>
        /// <param name="response">Passkey auth data</param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        public async Task<AuthInfo> Auth(PassKeyGetResponse response, HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth != null)
                throw new UserAlreadyLoggedInException();
            if (!session.TryRemove("FidoAuthOptions", out AssertionOptions options))
                return null;
            var credId = Convert.ToBase64String(response.rawId);
            var pks = await Um.GetPassKeyPublicKey(credId).ConfigureAwait(false);
            if (pks == null)
                return null;
            var uid = UserManagerService.MakeGuid(pks.Item2, "UM");
            IsUserHandleOwnerOfCredentialIdAsync callback =  (args, ct) =>
            {
                var guid = Encoding.UTF8.GetString(args.UserHandle);
                return Task.FromResult(guid == uid);
            };
            var rr = new AuthenticatorAssertionRawResponse
            {
                Id = Convert.FromBase64String(credId),
                RawId = response.rawId,
                Response = response.response == null ? null : new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = response.response.authenticatorData,
                    ClientDataJson = response.response.clientDataJSON,
                    Signature =  response.response.signature,
                    UserHandle = response.response.userHandle,
                },
                Type = Pkt[response.type],
            };

            var lib = GetLib();
            var res = await lib.MakeAssertionAsync(rr, options, pks.Item1, null, 0, callback);
            if (res == null)
                throw new Exception("Failed to validate!");

            auth = await Um.AuthUser(credId).ConfigureAwait(false);
            if (auth == null)
                return Failed;
            session.SetAuth(auth);
            await context.Server.RunOnLogin(session).ConfigureAwait(false);
            session.InvalidateCache();
            return new AuthInfo
            {
                Succeeded = true,
                Username = auth.Username,
                Domain = auth.Domain,
                Tokens = auth.Tokens?.ToArray(),
                Guid = auth.Guid.ToHex(),
                Language = session.Language,
                NickName = auth.NickName ?? auth.Username
            };
        }


        #endregion//Auth


        #region Create

        /// <summary>
        /// Get a server challenge for attaching a passkey to the current user
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        public async Task<PassKeyCreateOptions> GetCreateChallenge(HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth == null)
                throw new NoUserLoggedInException();
            var user = new Fido2User
            {
                DisplayName = auth.Username,
                Id = Encoding.UTF8.GetBytes(auth.Guid),
                Name = auth.Email ?? auth.Username,
            };
            var lib = GetLib();
            var options = lib.RequestNewCredential(user, null, AuthenticatorSelection.Default, AttestationConveyancePreference.None);
            options.Rp.Id = FixRp(context.Prefix);
            options.Rp.Name = RpName;
            context.Session.Set("FidoOptions", options);
            context.Session.TryRemove("FidoNewAuth");

            var userCredIds = await Um.GetPublicKeyIdsForUser(context).ConfigureAwait(false);
            if ((userCredIds != null) && (userCredIds.Count > 0))
                options.ExcludeCredentials = userCredIds.Select(x => new PublicKeyCredentialDescriptor(Convert.FromBase64String(x))).ToList();
            var res = new PassKeyCreateOptions
            {
                publicKey = new PassKeyCreateCredential(options),
            };
            return res;
        }


        /// <summary>
        /// Get a server challenge for adding a new passkey to a user (typically on some other device)
        /// </summary>
        /// <returns></returns>
        [WebApi]
        public async Task<PassKeyCreateOptions> GetResetChallenge(String token, HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth != null)
                throw new UserAlreadyLoggedInException();
            auth = await Um.GetUser<InternalNewPasswordData>(token, UserManagerService.ActionTokenGetResetChallenge, x => x.UserId, context).ConfigureAwait(false);
            var user = new Fido2User
            {
                DisplayName = auth.Username,
                Id = Encoding.UTF8.GetBytes(auth.Guid),
                Name = auth.Email ?? auth.Username,
            };
            var lib = GetLib();
            var options = lib.RequestNewCredential(user, null, AuthenticatorSelection.Default, AttestationConveyancePreference.None);
            options.Rp.Id = FixRp(context.Prefix);
            options.Rp.Name = RpName;
            context.Session.Set("FidoOptions", options);
            context.Session.Set("FidoNewAuth", auth);
            var userCredIds = await Um.GetPublicKeyIdsForUser(auth).ConfigureAwait(false);
            if ((userCredIds != null) && (userCredIds.Count > 0))
                options.ExcludeCredentials = userCredIds.Select(x => new PublicKeyCredentialDescriptor(Convert.FromBase64String(x))).ToList();
            var res = new PassKeyCreateOptions
            {
                publicKey = new PassKeyCreateCredential(options),
            };
            return res;
        }

        /// <summary>
        /// Get a server challenge for adding a new passkey to a user (on some other device)
        /// </summary>
        /// <returns></returns>
        [WebApi]
        public async Task<PassKeyCreateOptions> GetNewChallenge(String token, HttpServerRequest context)
        {
            var session = context.Session;
            var auth = session.Auth;
            if (auth != null)
                throw new UserAlreadyLoggedInException();
            auth = await Um.CreateUser(new AddUserRequest
            {
                Token = token,
            }, context, false).ConfigureAwait(false);
            var user = new Fido2User
            {
                DisplayName = auth.Username,
                Id = Encoding.UTF8.GetBytes(auth.Guid),
                Name = auth.Email ?? auth.Username,
            };
            var lib = GetLib();
            var options = lib.RequestNewCredential(user, null, AuthenticatorSelection.Default, AttestationConveyancePreference.None);
            options.Rp.Id = FixRp(context.Prefix);
            options.Rp.Name = RpName;
            context.Session.Set("FidoOptions", options);
            context.Session.Set("FidoNewAuth", auth);
            var userCredIds = await Um.GetPublicKeyIdsForUser(context).ConfigureAwait(false);
            if ((userCredIds != null) && (userCredIds.Count > 0))
                options.ExcludeCredentials = userCredIds.Select(x => new PublicKeyCredentialDescriptor(Convert.FromBase64String(x))).ToList();
            var res = new PassKeyCreateOptions
            {
                publicKey = new PassKeyCreateCredential(options),
            };
            return res;
        }


        /// <summary>
        /// Attach a new passkey to the currently logged in account
        /// </summary>
        /// <param name="response"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        public async Task<bool> Create(PassKeyCreateResponse response, HttpServerRequest context)
        {
            var auth = await InternalCreate(response, context).ConfigureAwait(false);
            return auth != null;
        }

        /// <summary>
        /// Attach a new passkey using previous auth token
        /// </summary>
        /// <param name="response"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public async Task<UserLoginResponse> New(PassKeyCreateResponse response, HttpServerRequest context)
        {
            var auth = await InternalCreate(response, context).ConfigureAwait(false);
            if (auth == null)
                return new UserLoginResponse
                {
                    Error = UserErrors.TokenExpired
                };
            return new UserLoginResponse
            {
                Username = auth.Username,
                Tokens = auth.Tokens?.ToArray(),
            };
        }

        async Task<Authorization> InternalCreate(PassKeyCreateResponse response, HttpServerRequest context)
        {
            var session = context.Session;
            session.TryRemove("FidoNewAuth", out Authorization newAuth);
            bool doLogin = newAuth != null;
            var auth = session.Auth;
            if (doLogin)
            {
                if (auth != null)
                    throw new UserAlreadyLoggedInException();
            }
            else
            {
                if (auth == null)
                    throw new NoUserLoggedInException();
            }
            if (!session.TryRemove("FidoOptions", out CredentialCreateOptions options))
                return null;
            var credId = Convert.ToBase64String(response.rawId);
            IsCredentialIdUniqueToUserAsyncDelegate callback = async (args, ct) =>
            {
                var cmpId = Convert.ToBase64String(args.CredentialId);
                var publicKey = await Um.GetPassKeyPublicKey(cmpId).ConfigureAwait(false);
                return publicKey == null;
            };
            var rr = new AuthenticatorAttestationRawResponse
            {
                Id = Convert.FromBase64String(credId),
                RawId = response.rawId,
                Response = response.response == null ? null : new AuthenticatorAttestationRawResponse.AttestationResponse
                {
                    AttestationObject = response.response.attestationObject,
                    ClientDataJson = response.response.clientDataJSON,
                },
                Type = Pkt[response.type],
            };
            var lib = GetLib();
            RegisteredPublicKeyCredential res = await lib.MakeNewCredentialAsync(rr, options, callback).ConfigureAwait(false);
            if (res == null)
                throw new Exception("Failed to validate!");
            if (doLogin)
            {
                session.SetAuth(newAuth);
                await context.Server.RunOnLogin(session).ConfigureAwait(false);
                auth = newAuth;
            }
            var deviceId = session.DeviceId;
            var r2 = await Um.AttachPublicKeyAuth(credId, deviceId, deviceId, res.PublicKey, context).ConfigureAwait(false);
            if (!r2)
                return null;
            return auth;
        }




        #endregion//Create


        #endregion//API

    }


}