using SysWeaver.Net;
using System;
using System.Collections.Generic;
using SysWeaver.Security;
using SysWeaver.Auth;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Micro service that runs a Kestrel based web server (<see cref="AspHttpServer"/>) wired to the service manager (see <see cref="HttpServerServiceBase{S, P}"/>).
    /// </summary>
    [IsMicroService]
    [OptionalDep<AuthManager, IFirewallHandler>]
    public sealed class AspHttpServerService : HttpServerServiceBase<AspHttpServer, AspHttpServerServiceParams>, IDisposable
    {

        /// <summary>
        /// Create the server and start it unless <see cref="AspHttpServerServiceParams.Start"/> is false.
        /// </summary>
        /// <param name="manager">The service manager</param>
        /// <param name="p">The parameters, null for the defaults</param>
        public AspHttpServerService(ServiceManager manager, AspHttpServerServiceParams p = null)
            : base(manager, p)
        {
        }

        protected override AspHttpServer Create(out Type paramType, out String instanceName, out Func<bool> start, Dictionary<String, ICertificateProvider> certs, AuthManager auth, IFirewallHandler firewallHandler)
        {
            var p = Params;
            paramType = typeof(AspHttpServerParams);
            var m = Manager;
            var server = new AspHttpServer(m, m.TryGet<ITranslator>(p.TranslatorInstance), m.TryGet<IApiAuditService>(p.AuditInstance), certs, auth, firewallHandler, p);
            instanceName = p.InstanceName;
            start = p.Start ? server.Start : null;
            return server;
        }

    }

}
