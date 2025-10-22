using SysWeaver.Net;
using System;
using System.Collections.Generic;
using SysWeaver.Security;
using SysWeaver.Auth;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{

    [IsMicroService]
    [OptionalDep<AuthManager, IFirewallHandler>]
    public sealed class AspHttpServerService : HttpServerServiceBase<AspHttpServer, AspHttpServerServiceParams>, IDisposable
    {

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
