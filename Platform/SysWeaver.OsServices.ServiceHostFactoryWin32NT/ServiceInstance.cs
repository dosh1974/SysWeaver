using System.ServiceProcess;
using System.Threading;
using System.Linq;
using System;


#pragma warning disable CA1416

namespace SysWeaver.OsServices
{


    /// <summary>
    /// The <see cref="ServiceBase"/> run by the SCM in the "daemon" process, owns the <see cref="SysWeaver.MicroService.ServiceManager"/> while the service runs.
    /// </summary>
    sealed class ServiceInstance : ServiceBase
    {
        /// <summary>
        /// Creates the instance (pause/continue and stop are supported).
        /// </summary>
        /// <param name="p">The service parameters (the name is used as the service name).</param>
        /// <param name="onStart">Optional callback invoked (once) after the service manager has been created.</param>
        public ServiceInstance(ServiceParams p, Action<SysWeaver.MicroService.ServiceManager> onStart)
        {
            AutoLog = true;
            CanPauseAndContinue = true;
            CanStop = true;
            ServiceName = p.Name;
            OnStartFn = onStart;
        }

        Action<SysWeaver.MicroService.ServiceManager> OnStartFn;


        /// <summary>
        /// Creates the service manager (loading the manifest) synchronously and invokes the start callback.
        /// </summary>
        /// <param name="args">Ignored.</param>
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            Interlocked.Exchange(ref Manager, null)?.Dispose();
            var manager = new MicroService.ServiceManager(true, null, ServiceHost.RestartService);
            var fn = Interlocked.Exchange(ref OnStartFn, null);
            fn?.Invoke(manager);
            Interlocked.Exchange(ref Manager, manager)?.Dispose();
        }


        /// <summary>
        /// Pauses all services.
        /// </summary>
        protected override void OnPause()
        {
            base.OnPause();
            Manager?.Pause();
        }

        /// <summary>
        /// Resumes all services.
        /// </summary>
        protected override void OnContinue()
        {
            base.OnContinue();
            Manager?.Resume();
        }

        MicroService.ServiceManager Manager;

        /// <summary>
        /// Disposes the service manager.
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();
            Interlocked.Exchange(ref Manager, null)?.Dispose();
        }

        /// <summary>
        /// Disposes the service manager when the system shuts down.
        /// </summary>
        protected override void OnShutdown()
        {
            base.OnShutdown();
            Interlocked.Exchange(ref Manager, null)?.Dispose();
        }



    }
}
