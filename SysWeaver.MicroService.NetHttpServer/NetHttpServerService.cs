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
    public sealed class NetHttpServerService : HttpServerServiceBase<NetHttpServer, NetHttpServerServiceParams>
    {
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
