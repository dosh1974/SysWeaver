using SysWeaver.Net;
using System;
using System.Collections.Generic;
using SysWeaver.Security;
using SysWeaver.Auth;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Micro service that runs an HttpListener based web server (<see cref="NetHttpServer"/>) wired to the service manager (see <see cref="HttpServerServiceBase{S, P}"/>).
    /// </summary>
    [IsMicroService]
    [OptionalDep<AuthManager, IFirewallHandler>]
    public sealed class NetHttpServerService : HttpServerServiceBase<NetHttpServer, NetHttpServerServiceParams>
    {
        /// <summary>
        /// Create the server and start it unless <see cref="NetHttpServerServiceParams.Start"/> is false.
        /// </summary>
        /// <param name="manager">The service manager</param>
        /// <param name="p">The parameters, null for the defaults</param>
        public NetHttpServerService(ServiceManager manager, NetHttpServerServiceParams p = null) 
            : base(manager, p)
        {
        }

        protected override NetHttpServer Create(out Type paramType, out String instanceName, out Func<bool> start, Dictionary<String, ICertificateProvider> certs, AuthManager auth, IFirewallHandler firewallHandler)
        {
            var p = Params;
            paramType = typeof(NetHttpServerParams);
            var m = Manager;
            var server = new NetHttpServer(m, m.TryGet<ITranslator>(p.TranslatorInstance), m.TryGet<IApiAuditService>(p.AuditInstance), certs, auth, firewallHandler, p);
            instanceName = p.InstanceName;
            start = p.Start ? server.Start : null;
            return server;
        }
    }

}
