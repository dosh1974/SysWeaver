using SysWeaver.Auth;
using SysWeaver.Net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

using Fido2NetLib;
using Fido2NetLib.Exceptions;
using Fido2NetLib.Objects;

namespace SysWeaver.MicroService
{



    [IsMicroService]
    [RequiredDep<UserManagerService>]
    [OptionalDep<IQrCodeService>]
    [WebApiUrl("auth/passkey")]
    [WebMenuEmbedded("User", "User/AddPassKey", "Add passkey", "auth/AddPassKey.html", "Click to add a passkey for this device", "IconAddPasskey", 31, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanCreateLocalPassKey))]
    [WebMenuEmbedded("User", "User/AddPassKeyOther", "Add passkey (other device)", "auth/AddPassKeyOnOtherDevice.html", "Click to show a QR code that can be used to add a passkey on some other device", "IconAddPasskeyOther", 32, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanCreateRemotePassKey))]
    [WebMenuEmbedded("User", "User/MyPassKeys", "My passkeys", "auth/MyPassKeys.html", "Click to see and manage your passkeys", "IconUsePasskey", 33, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanManagePassKeys))]
    [WebMenuEmbedded("User", "User/UsePassKey", "Sign in with passkey", "auth/UsePassKey.html", "Click to sign in using a passkey", "IconUsePasskey", 2, null, true, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(PassKeyService) + "." + nameof(PassKeyService.CanSignInWithPassKey))]
    public sealed class PassKeyService
    {

        #region Dynamic menu

        Task<bool> CanSignInWithPassKey(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if ((s == null) || (s.Auth != null))
                return TaskExt.FalseTask;
            return PassKeyTools.CanUse(PassKeyTools.GetOrigin(context.Prefix)) ? TaskExt.TrueTask : TaskExt.FalseTask;
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
            if (!PassKeyTools.CanUse(PassKeyTools.GetOrigin(context.Prefix)))
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
            return PassKeyTools.CanUse(PassKeyTools.GetOrigin(context.Prefix)) ? TaskExt.TrueTask : TaskExt.FalseTask;
        }

        async Task<bool> CanManagePassKeys(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var um = Um;
            return await um.HaveAnyPassKeys(um.GetUid(s.Auth)).ConfigureAwait(false);
        }

        #endregion//Dynamic menu


        public PassKeyService(ServiceManager manager, PassKeyParams p)
        {
            p = p ?? new PassKeyParams();
            M = manager;
            Um = manager.Get<UserManagerService>();
            Params = p;
            RpName = p.RpName ?? EnvInfo.AppName;
            RpId = String.IsNullOrWhiteSpace(p.RpId) ? null : p.RpId.Trim();
            QR = manager.TryGet<IQrCodeService>();
            LifeTime = TimeSpan.FromSeconds(Math.Max(10, p.ChallengeLifeTime));
            UserVerification = PassKeyTools.FromWebAuthn<UserVerificationRequirement>(p.UserVerification) ?? UserVerificationRequirement.Required;
            ResidentKey = PassKeyTools.FromWebAuthn<ResidentKeyRequirement>(p.ResidentKey) ?? ResidentKeyRequirement.Required;
            List<String> origins = new List<String>();
            var pp = p.Prefixes;
            if (pp != null)
            {
                foreach (var x in pp)
                {
                    var o = PassKeyTools.GetOrigin(x?.Trim());
                    if (o == null)
                    {
                        manager.AddMessage("Ignoring invalid passkey origin " + x.ToQuoted() + ", expected \"scheme://host[:port]\"", MessageLevels.Warning);
                        continue;
                    }
                    origins.Add(o);
                }
            }
            AdditionalOrigins = origins.ToArray();
        }

        readonly IQrCodeService QR;
        readonly ServiceManager M;
        readonly PassKeyParams Params;
        readonly UserManagerService Um;
        readonly String RpName;
        readonly String RpId;
        readonly TimeSpan LifeTime;
        readonly UserVerificationRequirement UserVerification;
        readonly ResidentKeyRequirement ResidentKey;
        readonly String[] AdditionalOrigins;

        public override string ToString() => "Provides an API for logging in securely using FIDO2 passkeys";


        #region Ceremonies (challenges)

        enum CeremonyKinds
        {
            /// <summary>
            /// Sign in
            /// </summary>
            Auth,
            /// <summary>
            /// Add a passkey to the signed in user
            /// </summary>
            Attach,
            /// <summary>
            /// Add a passkey to the user of a token (and sign in)
            /// </summary>
            Token,
            /// <summary>
            /// Create a new user using a token, with a passkey (and sign in)
            /// </summary>
            NewUser,
        }

        sealed class Ceremony
        {
            public CeremonyKinds Kind;
            public DateTime Expires;
            public String Origin;
            public String RpId;
            public AssertionOptions AssertionOptions;
            public CredentialCreateOptions CreateOptions;
            public String Token;
            public long UserId;
            public Byte[] UserHandle;
        }

        /// <summary>
        /// The pending ceremonies of a session (all tabs of a browser share the session)
        /// </summary>
        sealed class Ceremonies
        {
            public readonly Dictionary<String, Ceremony> Pending = new Dictionary<String, Ceremony>(StringComparer.Ordinal);
        }

        const String SessionKey = "SysWeaver.PassKey.Ceremonies";

        /// <summary>
        /// The maximum number of pending ceremonies per session, the oldest is removed when exceeded
        /// </summary>
        const int MaxCeremonies = 8;

        String AddCeremony(HttpSession session, Ceremony ceremony)
        {
            var now = DateTime.UtcNow;
            ceremony.Expires = now + LifeTime + TimeSpan.FromSeconds(30);
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var cs = session.GetOrCreate(SessionKey, () => new Ceremonies());
            var p = cs.Pending;
            lock (p)
            {
                foreach (var x in p.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToList())
                    p.Remove(x);
                while (p.Count >= MaxCeremonies)
                    p.Remove(p.MinBy(x => x.Value.Expires).Key);
                p[id] = ceremony;
            }
            return id;
        }

        /// <summary>
        /// Get and remove a pending ceremony
        /// </summary>
        /// <returns>The ceremony or null if it doesn't exist, has expired or is of some other kind</returns>
        static Ceremony TakeCeremony(HttpSession session, String id, params CeremonyKinds[] kinds)
        {
            if (id == null)
                return null;
            if (!session.TryGet<Ceremonies>(SessionKey, out var cs))
                return null;
            var p = cs.Pending;
            Ceremony c;
            lock (p)
            {
                if (!p.Remove(id, out c))
                    return null;
            }
            if (c.Expires <= DateTime.UtcNow)
                return null;
            return kinds.Contains(c.Kind) ? c : null;
        }

        /// <summary>
        /// Get the origin and relying party id to use for a request
        /// </summary>
        /// <returns>The origin and relying party id, null if passkeys can't be used on the origin of the request</returns>
        Tuple<String, String> GetRp(HttpServerRequest context)
        {
            var origin = PassKeyTools.GetOrigin(context.Prefix);
            if (!PassKeyTools.CanUse(origin))
                return null;
            return Tuple.Create(origin, PassKeyTools.GetRpId(PassKeyTools.GetHost(origin), RpId));
        }

        Fido2 GetLib(String origin, String rpId)
        {
            var origins = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
            {
                origin
            };
            foreach (var x in AdditionalOrigins)
                origins.Add(x);
            return new Fido2(new Fido2Configuration
            {
                RPID = rpId,
                RPName = RpName,
                ChallengeSize = 32,
                Timeout = (uint)LifeTime.TotalMilliseconds,
                Origins = origins,
            });
        }

        Fido2 GetLib(Ceremony c) => GetLib(c.Origin, c.RpId);

        AuthenticatorSelection Selection => new AuthenticatorSelection
        {
            ResidentKey = ResidentKey,
            UserVerification = UserVerification,
        };

        #endregion//Ceremonies (challenges)


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
            String link = String.Concat(context.Prefix ?? "", "auth/AddPassKey.html?token=", Uri.EscapeDataString(token));
            var svg = qr.CreateQrCode(link);
            return Encoding.UTF8.GetBytes(svg);
        }

        /// <summary>
        /// The number of minutes that a QR code (from GetQR.svg) is valid
        /// </summary>
        /// <returns>The number of minutes</returns>
        [WebApi]
        [WebApiClientCache(60)]
        public int GetQRLifeTime() => (int)Um.ShareDeviceTokenLifeTime.TotalMinutes;


        #region API

        #region Auth

        /// <summary>
        /// Get a passkey challenge for user-less (discoverable credentials) and passwordless sign in.
        /// </summary>
        /// <param name="context"></param>
        /// <returns>The options to pass to navigator.credentials.get</returns>
        [WebApi]
        public Task<PassKeyGetOptions> GetAuthChallenge(HttpServerRequest context)
            => Task.FromResult(InternalGetAuthChallenge(context, []));

        /// <summary>
        /// Get a passkey challenge for a specific user for passwordless sign in (works with non-discoverable credentials).
        /// </summary>
        /// <param name="userIdentifier">User identifier (typically the email)</param>
        /// <param name="context"></param>
        /// <returns>The options to pass to navigator.credentials.get</returns>
        [WebApi]
        public async Task<PassKeyGetOptions> GetUserAuthChallenge(String userIdentifier, HttpServerRequest context)
        {
            if (context.Session.Auth != null)
                return PassKeyGetOptions.Fail(PassKeyErrors.AlreadySignedIn);
            userIdentifier = userIdentifier?.Trim();
            if (String.IsNullOrEmpty(userIdentifier))
                return PassKeyGetOptions.Fail(PassKeyErrors.NoPassKeys);
            var keys = await Um.GetPassKeys(userIdentifier).ConfigureAwait(false);
            if ((keys == null) || (keys.Count <= 0))
                return PassKeyGetOptions.Fail(PassKeyErrors.NoPassKeys);
            return InternalGetAuthChallenge(context, keys.Select(x => PassKeyTools.ToDescriptor(x.CredentialId, x.Transports)).ToList());
        }

        PassKeyGetOptions InternalGetAuthChallenge(HttpServerRequest context, IReadOnlyList<PublicKeyCredentialDescriptor> allow)
        {
            var session = context.Session;
            if (session.Auth != null)
                return PassKeyGetOptions.Fail(PassKeyErrors.AlreadySignedIn);
            var rp = GetRp(context);
            if (rp == null)
                return PassKeyGetOptions.Fail(PassKeyErrors.NotSupported);
            var options = GetLib(rp.Item1, rp.Item2).GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = allow,
                UserVerification = UserVerification,
            });
            var id = AddCeremony(session, new Ceremony
            {
                Kind = CeremonyKinds.Auth,
                Origin = rp.Item1,
                RpId = rp.Item2,
                AssertionOptions = options,
            });
            return new PassKeyGetOptions
            {
                challengeId = id,
                publicKey = new PassKeyGetCredential(options),
            };
        }


        /// <summary>
        /// Sign in using a passkey
        /// </summary>
        /// <param name="response">The response of navigator.credentials.get</param>
        /// <param name="context"></param>
        /// <returns>The result, with the signed in user on success</returns>
        [WebApi]
        public async Task<PassKeyResult> Auth(PassKeyGetResponse response, HttpServerRequest context)
        {
            var session = context.Session;
            if (session.Auth != null)
                return PassKeyResult.Fail(PassKeyErrors.AlreadySignedIn);
            var c = TakeCeremony(session, response?.challengeId, CeremonyKinds.Auth);
            if (c == null)
                return PassKeyResult.Fail(PassKeyErrors.ChallengeExpired);
            if ((response.rawId == null) || (response.response == null))
                return PassKeyResult.Fail(PassKeyErrors.VerificationFailed);
            var credId = Convert.ToBase64String(response.rawId);
            var key = await Um.GetPassKey(credId).ConfigureAwait(false);
            if (key == null)
                return PassKeyResult.Fail(PassKeyErrors.UnknownCredential);
            var expectedHandle = key.UserHandle ?? Encoding.UTF8.GetBytes(Um.MakeGuid(key.UserId));
            IsUserHandleOwnerOfCredentialIdAsync callback = (args, ct) => Task.FromResult(args.UserHandle.AsSpan().SequenceEqual(expectedHandle));
            VerifyAssertionResult res;
            try
            {
                res = await GetLib(c).MakeAssertionAsync(new MakeAssertionParams
                {
                    AssertionResponse = PassKeyTools.ToRaw(response),
                    OriginalOptions = c.AssertionOptions,
                    StoredPublicKey = key.PublicKey,
                    StoredSignatureCounter = (uint)key.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = callback,
                }).ConfigureAwait(false);
            }
            catch (Fido2VerificationException e)
            {
                M.AddMessage("Passkey sign in failed: " + e.Message, MessageLevels.Warning);
                return PassKeyResult.Fail(PassKeyErrors.VerificationFailed, e.Message);
            }
            var auth = await Um.UsePassKey(credId, res.SignCount, res.IsBackedUp).ConfigureAwait(false);
            if (auth == null)
                return PassKeyResult.Fail(PassKeyErrors.UnknownCredential);
            return await SignIn(auth, context).ConfigureAwait(false);
        }

        static async Task<PassKeyResult> SignIn(Authorization auth, HttpServerRequest context)
        {
            var session = context.Session;
            session.SetAuth(auth);
            await context.Server.RunOnLogin(session, context).ConfigureAwait(false);
            session.InvalidateCache();
            return new PassKeyResult
            {
                User = new AuthInfo
                {
                    Succeeded = true,
                    Username = auth.Username,
                    Domain = auth.Domain,
                    Tokens = auth.Tokens?.ToArray(),
                    Guid = auth.Guid,
                    Language = session.Language,
                    NickName = auth.NickName ?? auth.Username
                },
            };
        }


        #endregion//Auth


        #region Create

        PassKeyCreateOptions InternalGetCreateChallenge(HttpServerRequest context, Ceremony c, Fido2User user, IEnumerable<PassKeyCredential> exclude)
        {
            var rp = GetRp(context);
            if (rp == null)
                return PassKeyCreateOptions.Fail(PassKeyErrors.NotSupported);
            c.Origin = rp.Item1;
            c.RpId = rp.Item2;
            c.UserHandle = user.Id;
            var options = GetLib(c).RequestNewCredential(new RequestNewCredentialParams
            {
                User = user,
                ExcludeCredentials = exclude?.Select(x => PassKeyTools.ToDescriptor(x.CredentialId, x.Transports)).ToList() ?? [],
                AuthenticatorSelection = Selection,
                AttestationPreference = AttestationConveyancePreference.None,
            });
            c.CreateOptions = options;
            var id = AddCeremony(context.Session, c);
            return new PassKeyCreateOptions
            {
                challengeId = id,
                publicKey = new PassKeyCreateCredential(options),
            };
        }

        static Fido2User GetUser(Authorization auth) => new Fido2User
        {
            DisplayName = auth.NickName ?? auth.Username,
            Id = Encoding.UTF8.GetBytes(auth.Guid),
            Name = auth.Email ?? auth.Username,
        };

        /// <summary>
        /// Get a server challenge for adding a passkey to the signed in user
        /// </summary>
        /// <returns>The options to pass to navigator.credentials.create</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<PassKeyCreateOptions> GetCreateChallenge(HttpServerRequest context)
        {
            var auth = context.Session.Auth;
            var uid = Um.GetUid(auth);
            if (uid == 0)
                return PassKeyCreateOptions.Fail(PassKeyErrors.NotSignedIn);
            var keys = await Um.GetPassKeys(uid).ConfigureAwait(false);
            return InternalGetCreateChallenge(context, new Ceremony
            {
                Kind = CeremonyKinds.Attach,
                UserId = uid,
            }, GetUser(auth), keys);
        }


        /// <summary>
        /// Get a server challenge for adding a new passkey to a user using a token (from a QR code, a reset password or an add password link)
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="context"></param>
        /// <returns>The options to pass to navigator.credentials.create</returns>
        [WebApi]
        public async Task<PassKeyCreateOptions> GetResetChallenge(String token, HttpServerRequest context)
        {
            if (context.Session.Auth != null)
                return PassKeyCreateOptions.Fail(PassKeyErrors.AlreadySignedIn);
            Authorization auth;
            try
            {
                auth = await Um.GetPassKeyTokenUser(token, context).ConfigureAwait(false);
            }
            catch (TokenExpiredException)
            {
                return PassKeyCreateOptions.Fail(PassKeyErrors.TokenExpired);
            }
            catch (UserNoLongerExistException)
            {
                return PassKeyCreateOptions.Fail(PassKeyErrors.TokenExpired);
            }
            var keys = await Um.GetPassKeys(auth).ConfigureAwait(false);
            return InternalGetCreateChallenge(context, new Ceremony
            {
                Kind = CeremonyKinds.Token,
                Token = token,
            }, GetUser(auth), keys);
        }

        /// <summary>
        /// Get a server challenge for creating a new user (using a sign up token) with a passkey
        /// </summary>
        /// <param name="token">The sign up token</param>
        /// <param name="context"></param>
        /// <returns>The options to pass to navigator.credentials.create</returns>
        [WebApi]
        public async Task<PassKeyCreateOptions> GetNewChallenge(String token, HttpServerRequest context)
        {
            if (context.Session.Auth != null)
                return PassKeyCreateOptions.Fail(PassKeyErrors.AlreadySignedIn);
            var data = await Um.GetNewUserData(token, context).ConfigureAwait(false);
            if (data == null)
                return PassKeyCreateOptions.Fail(PassKeyErrors.TokenExpired);
            var name = data.Email ?? data.Phone ?? data.UserName;
            //  The user id isn't known until the user is created, so a random user handle is used (stored with the credential)
            var user = new Fido2User
            {
                DisplayName = data.NickName ?? data.UserName ?? name,
                Id = RandomNumberGenerator.GetBytes(32),
                Name = name ?? data.UserName,
            };
            return InternalGetCreateChallenge(context, new Ceremony
            {
                Kind = CeremonyKinds.NewUser,
                Token = token,
            }, user, null);
        }

        /// <summary>
        /// Add a new passkey to the signed in user
        /// </summary>
        /// <param name="response">The response of navigator.credentials.create</param>
        /// <param name="context"></param>
        /// <returns>The result</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<PassKeyResult> Create(PassKeyCreateResponse response, HttpServerRequest context)
        {
            var session = context.Session;
            var uid = Um.GetUid(session.Auth);
            if (uid == 0)
                return PassKeyResult.Fail(PassKeyErrors.NotSignedIn);
            var c = TakeCeremony(session, response?.challengeId, CeremonyKinds.Attach);
            if ((c == null) || (c.UserId != uid))
                return PassKeyResult.Fail(PassKeyErrors.ChallengeExpired);
            var v = await Verify(c, response, context).ConfigureAwait(false);
            if (v.Item2 != null)
                return v.Item2;
            await Um.AttachPassKey(v.Item1, context).ConfigureAwait(false);
            return PassKeyResult.Ok;
        }

        /// <summary>
        /// Add a new passkey using a token or create a new user with a passkey (see <see cref="GetResetChallenge(string, HttpServerRequest)"/> and <see cref="GetNewChallenge(string, HttpServerRequest)"/>), the user is signed in on success
        /// </summary>
        /// <param name="response">The response of navigator.credentials.create</param>
        /// <param name="context"></param>
        /// <returns>The result, with the signed in user on success</returns>
        [WebApi]
        public async Task<PassKeyResult> New(PassKeyCreateResponse response, HttpServerRequest context)
        {
            var session = context.Session;
            if (session.Auth != null)
                return PassKeyResult.Fail(PassKeyErrors.AlreadySignedIn);
            var c = TakeCeremony(session, response?.challengeId, CeremonyKinds.Token, CeremonyKinds.NewUser);
            if (c == null)
                return PassKeyResult.Fail(PassKeyErrors.ChallengeExpired);
            var v = await Verify(c, response, context).ConfigureAwait(false);
            if (v.Item2 != null)
                return v.Item2;
            Authorization auth;
            try
            {
                auth = c.Kind == CeremonyKinds.NewUser
                    ? await Um.CreateUserWithPassKey(c.Token, v.Item1, context).ConfigureAwait(false)
                    : await Um.AttachPassKeyUsingToken(c.Token, v.Item1, context).ConfigureAwait(false);
            }
            catch (TokenExpiredException)
            {
                return PassKeyResult.Fail(PassKeyErrors.TokenExpired);
            }
            catch (UserNoLongerExistException)
            {
                return PassKeyResult.Fail(PassKeyErrors.TokenExpired);
            }
            catch (UserAlreadyExistException)
            {
                return PassKeyResult.Fail(PassKeyErrors.AccountExists);
            }
            return await SignIn(auth, context).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a new credential
        /// </summary>
        /// <returns>The credential to store, or an error result</returns>
        async Task<Tuple<PassKeyCredential, PassKeyResult>> Verify(Ceremony c, PassKeyCreateResponse response, HttpServerRequest context)
        {
            if ((response.rawId == null) || (response.response == null))
                return Tuple.Create<PassKeyCredential, PassKeyResult>(null, PassKeyResult.Fail(PassKeyErrors.VerificationFailed));
            IsCredentialIdUniqueToUserAsyncDelegate callback = async (args, ct) =>
                (await Um.GetPassKey(Convert.ToBase64String(args.CredentialId)).ConfigureAwait(false)) == null;
            RegisteredPublicKeyCredential res;
            try
            {
                res = await GetLib(c).MakeNewCredentialAsync(new MakeNewCredentialParams
                {
                    AttestationResponse = PassKeyTools.ToRaw(response),
                    OriginalOptions = c.CreateOptions,
                    IsCredentialIdUniqueToUserCallback = callback,
                }).ConfigureAwait(false);
            }
            catch (Fido2VerificationException e)
            {
                if (e.Code == Fido2ErrorCode.NonUniqueCredentialId)
                    return Tuple.Create<PassKeyCredential, PassKeyResult>(null, PassKeyResult.Fail(PassKeyErrors.AlreadyRegistered));
                M.AddMessage("Passkey creation failed: " + e.Message, MessageLevels.Warning);
                return Tuple.Create<PassKeyCredential, PassKeyResult>(null, PassKeyResult.Fail(PassKeyErrors.VerificationFailed, e.Message));
            }
            var session = context.Session;
            return Tuple.Create<PassKeyCredential, PassKeyResult>(new PassKeyCredential
            {
                CredentialId = Convert.ToBase64String(res.Id),
                PublicKey = res.PublicKey,
                UserHandle = c.UserHandle,
                RpId = c.RpId,
                SignCount = res.SignCount,
                Transports = PassKeyTools.ToString(res.Transports),
                BackupEligible = res.IsBackupEligible,
                BackedUp = res.IsBackedUp,
                AaGuid = res.AaGuid == Guid.Empty ? null : res.AaGuid.ToString(),
                DeviceId = session.DeviceId,
                DeviceName = PassKeyTools.GetName(res.AaGuid, session.UserAgent),
            }, null);
        }

        #endregion//Create

        #region Manage

        /// <summary>
        /// Get the passkeys of the signed in user
        /// </summary>
        /// <param name="context"></param>
        /// <returns>The passkeys, most recently used first</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<PassKeyInfo[]> GetPassKeys(HttpServerRequest context)
        {
            var session = context.Session;
            var deviceId = session.DeviceId;
            var keys = await Um.GetPassKeys(session.Auth).ConfigureAwait(false);
            return keys.OrderByDescending(x => x.LastUsed).Select(x => new PassKeyInfo
            {
                Id = x.CredentialId,
                Name = x.DeviceName,
                Created = DateTime.SpecifyKind(x.Created, DateTimeKind.Utc),
                LastUsed = DateTime.SpecifyKind(x.LastUsed, DateTimeKind.Utc),
                Synced = x.BackedUp,
                ThisDevice = (deviceId != null) && deviceId.LimitLength(64, "").Equals(x.DeviceId, StringComparison.Ordinal),
            }).ToArray();
        }

        /// <summary>
        /// Rename a passkey of the signed in user
        /// </summary>
        /// <param name="request">The passkey and the new name</param>
        /// <param name="context"></param>
        /// <returns>True if the passkey was renamed</returns>
        [WebApi]
        [WebApiAuth]
        public Task<bool> RenamePassKey(PassKeyRenameRequest request, HttpServerRequest context)
            => Um.RenamePassKey(request.Id, request.Name, context);

        /// <summary>
        /// Remove a passkey from the signed in user, the last sign in method (passkey or password) of a user can't be removed
        /// </summary>
        /// <param name="id">The credential id (from <see cref="PassKeyInfo.Id"/>)</param>
        /// <param name="context"></param>
        /// <returns>The result, error is <see cref="PassKeyErrors.UnknownCredential"/> if it doesn't exist, <see cref="PassKeyErrors.LastSignInMethod"/> if it's the last way the user can sign in</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<PassKeyResult> DeletePassKey(String id, HttpServerRequest context)
        {
            try
            {
                return (await Um.DeletePassKey(id, context).ConfigureAwait(false)) ? PassKeyResult.Ok : PassKeyResult.Fail(PassKeyErrors.UnknownCredential);
            }
            catch (LastSignInMethodException)
            {
                return PassKeyResult.Fail(PassKeyErrors.LastSignInMethod);
            }
        }

        #endregion//Manage

        #endregion//API

    }


}
