using System.ServiceProcess;
using System.Threading;
using System.Linq;
using System;


#pragma warning disable CA1416

namespace SysWeaver.OsServices
{


    sealed class ServiceInstance : ServiceBase
    {
        public ServiceInstance(ServiceParams p, Action<SysWeaver.MicroService.ServiceManager> onStart)
        {
            AutoLog = true;
            CanPauseAndContinue = true;
            CanStop = true;
            ServiceName = p.Name;
            OnStartFn = onStart;
        }

        Action<SysWeaver.MicroService.ServiceManager> OnStartFn;


        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            Interlocked.Exchange(ref Manager, null)?.Dispose();
            var manager = new MicroService.ServiceManager(true, null, ServiceHost.RestartService);
            var fn = Interlocked.Exchange(ref OnStartFn, null);
            fn?.Invoke(manager);
            Interlocked.Exchange(ref Manager, manager)?.Dispose();
        }


        protected override void OnPause()
        {
            base.OnPause();
            Manager?.Pause();
        }

        protected override void OnContinue()
        {
            base.OnContinue();
            Manager?.Resume();
        }

        MicroService.ServiceManager Manager;

        protected override void OnStop()
        {
            base.OnStop();
            Interlocked.Exchange(ref Manager, null)?.Dispose();
        }



    }
}
