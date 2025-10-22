using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.Remote;
using SysWeaver.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// The service managers act as an object registry, where you can register object instances that can be used by other services.
    /// </summary>
    public class ServiceManager : MessageHost, IDisposable, IPerfMonitored
    {
        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(ServiceManager));

        public override string ToString() => nameof(ServiceManager);

        Action<ServiceManager> HostRestart;

        public static String GetManifestFileName()
        {
            var b = EnvInfo.ExecutableBase;
            var fn = String.Join('.', b, "Services", Environment.OSVersion.Platform, "json");
            if (File.Exists(fn))
                return fn;
            fn = String.Join('.', b, "Services", "json");
            if (File.Exists(fn))
                return fn;
            return null;
        }

            /// <summary>
            /// Restart the process
            /// </summary>
            /// <returns>True if successful</returns>
            public bool RestartProcess()
        {
            var h = Interlocked.Exchange(ref HostRestart, null);
            if (h == null)
                return false;
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(200);
                h(this);
            });
            return true;
        }

        public ServiceManager(bool registerFromManifestFile = true, Action<ServiceManager> runBeforeRegister = null, Action<ServiceManager> restartFn = null) : base()
        {
            using var perfMon = PerfMon.Track("Constructor");
            HostRestart = restartFn;
            if (Debugger.IsAttached)
            {
#if DEBUG
                var mh = DebugMessageHandler.GetSync(Message.TextStyles.Debug);
#else////DEBUG
                var mh = DebugMessageHandler.GetAsync(Message.TextStyles.Verbose);
#endif//DEBUG
                //DisposeOnExit.Push(mh);
                //AddHandler(mh);
                Register(mh);
            }
            if (EnvInfo.HaveConsole)
            {
#if DEBUG
                var mh = ConsoleMessageHandler.GetSync(ConsoleMessageHandler.Styles.Debug);
#else//DEBUG
                var mh = ConsoleMessageHandler.GetAsync(ConsoleMessageHandler.Styles.Verbose);
#endif//DEBUG
                //DisposeOnExit.Push(mh);
                //AddHandler(mh);
                Register(mh);
            }
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
            PruneTask = new PeriodicTask(Prune, 2000);
            runBeforeRegister?.Invoke(this);
            if (registerFromManifestFile)
            {
                var fn = GetManifestFileName();
                if (fn != null)
                {
                    ManifestFileName = fn;
                    RegisterManifestFile(fn);
                }
            }
        }

        public ConfigEntry[] ReadManifest(String file = null)
        {
            var text = File.ReadAllText(file ?? ManifestFileName);
            ServiceManifest[] mf = JsonSerializer.Deserialize<ServiceManifest[]>(text, DeSerOpt);
            var l = mf.Length;
            var config = new ConfigEntry[l];
            var optType = typeof(ConfigEntryOP<>);
            var pType = typeof(ConfigEntryP<>);
            for (int i = 0; i < l; ++ i)
            {
                var m = mf[i];
                var p = GetManifestParam(out bool isOptional, m);
                Type t;
                if (p != null)
                {
                    t = p.GetType();
                }else
                {
                    isOptional = true;
                    t = GetManifestParamType(m);
                }
                if (t == null)
                {
                    config[i] = new ConfigEntry
                    {
                        Name = m.Name,
                        Type = m.Type,
                    };
                    continue;
                }
                var ct = (isOptional ? optType : pType).MakeGenericType(t);
                var pe = Activator.CreateInstance(ct, p) as ConfigEntry;
                pe.Type = m.Type;
                pe.Name = m.Name;
                config[i] = pe;
            }
            return config;
        }

        internal static readonly JsonSerializerOptions SerOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            IgnoreReadOnlyFields = true,
            IncludeFields = true,
        };

        public String BuildManifest(ConfigEntry[] configEntries)
        {
            var l = configEntries.Length;
            ServiceManifest[] me = new ServiceManifest[l];
            for (int i = 0; i < l; ++i)
            {
                var d = new ServiceManifest();
                me[i] = d;
                var s = configEntries[i];
                d.Type = s.Type;
                d.Name = s.Name;
                d.Params = s.GetParams();
            }
            return JsonSerializer.Serialize<ServiceManifest[]>(me, SerOpt);
        }


        public String ManifestFileName { get; init; }

        void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
                AddMessage("Terminating CLR due to a Fatal Unhandled Exception: " + e.ToString(), e.ExceptionObject as Exception);
            else
                AddMessage("Fatal Unhandled Exception: " + e.ToString(), e.ExceptionObject as Exception);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            AddMessage(Tag + " Unregistering and disposing owned services:");
            using (Tab())
            {
                var k = Instances.OrderByDescending(x => x.Value.Order).Select(x => x.Key).ToList();
                foreach (var i in k)
                {
                    if (!Instances.ContainsKey(i))
                        continue;
                    ServiceInfo info = null;
                    try
                    {
                        info = Unregister(i);
                        if (info == null)
                        {
                            AddMessage(String.Join("Can't find service of type ", Tag, ServiceName(i, info)), MessageLevels.Warning);
                            continue;
                        }
                        if (!info.Owned)
                            continue;
                        var d = i as IDisposable;
                        if (d == null)
                            continue;
                        d.Dispose();
                        AddMessage(String.Join(" Disposed service of type ", Tag, ServiceName(i, info)), MessageLevels.Debug);
                    }
                    catch (Exception e)
                    {
                        AddMessage(String.Join("Failed to unregister or dispose service of type ", Tag, ServiceName(i, info)), e, MessageLevels.Warning);
                    }
                }
            }
            AddMessage(Tag + " Disposing internals");
            using (Tab())
            {
                var d = DisposeOnExit;
                while (d.TryPop(out var ds))
                {
                    RemoveHaveStats(ds as IHaveStats);
                    RemoveHavePerfMonitor(ds as IPerfMonitored);
                    RemoveMessageHandler(ds as MessageHandler);
                    (ds as IDisposable)?.Dispose();
                }
            }
            AppDomain.CurrentDomain.UnhandledException -= UnhandledException;
        }


        readonly ConcurrentStack<Object> DisposeOnExit = new ConcurrentStack<Object>();


        public const String Tag = "[ServiceManager]";


        #region Add / remove

        /// <summary>
        /// Invoked whenever a new service instance is added
        /// </summary>
        public event Action<Object, ServiceInfo> OnServiceAdded;

        /// <summary>
        /// Invoked whenever a service instance is removed
        /// </summary>
        public event Action<Object, ServiceInfo> OnServiceRemoved;



        /// <summary>
        /// Register an instance (add it to the service manager)
        /// </summary>
        /// <param name="instance">The service to register</param>
        /// <param name="instanceName">An optional instance name, could be useful when registering multiple instances of the same type (when registering API's etc)</param>
        /// <param name="giveOwnershipToTheServiceManager">If true, the service manager will call Dispose on the instance when it's disposed, else it's upp to the application to perform a dispose</param>
        /// <param name="paramType">Optional type of the parameters (from manifest)</param>
        /// <exception cref="Exception"></exception>
        public ServiceInfo Register(Object instance, String instanceName = null, bool giveOwnershipToTheServiceManager = true, Type paramType = null)
        {
            var info = new ServiceInfo(instance, instanceName ?? "", giveOwnershipToTheServiceManager, paramType);
            if (!Instances.TryAdd(instance, info))
                throw new Exception("An instance may only be registered once!");
            AddMessage(String.Join(" Registering instance of type ", Tag, ServiceName(instance, info, true)), MessageLevels.Debug);
            using (Tab())
            {
                var type = instance.GetType();
                AddType(type, instance, info);
                var t = instance as IServiceMessageListener;
                if (t != null)
                {
                    if (!MessageListeners.TryAdd(t, info))
                        throw new Exception("Internal error!");
                }
                AddMessageHandler(instance as MessageHandler);
                AddHavePerfMonitor(instance as IPerfMonitored);
                AddHaveStats(instance as IHaveStats);
            }
            AddMessage(String.Join(" Registered instance of type ", Tag, ServiceName(instance, info)));
            OnServiceAdded?.Invoke(instance, info);
            return info;
        }

        static readonly IReadOnlySet<Type> ThisTypes = ReadOnlyData.Set(
            typeof(ServiceManager),
            typeof(IMessageHost),
            typeof(MessageHandler)
        );

        static readonly IReadOnlySet<Type> AllowedGenericTypes = ReadOnlyData.Set(
            typeof(IList<>),
            typeof(IReadOnlyList<>),
            typeof(IEnumerable<>),
            typeof(ICollection<>),
            typeof(IReadOnlyCollection<>),
            typeof(IReadOnlySet<>),
            typeof(List<>),
            typeof(HashSet<>)
        );


        /// <summary>
        /// Try to find an object instance given a type, can be a single object T, array T[], collections of T and so on
        /// </summary>
        /// <param name="t">The type to get an instance for</param>
        /// <param name="type">Filter instances</param>
        /// <returns>An object instance if found or null if no matching instance can be resolved</returns>
        public Object TryGetObject(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any)
        {
            if (ThisTypes.Contains(t))
                return this;
            if (t.IsArray)
            {
                var et = t.GetElementType();
                var v = GetAll(et, type, ServiceInstanceOrders.Oldest).ToList();
                var vl = v.Count;
                var a = Array.CreateInstance(et, vl);
                for (int i = 0; i < vl; ++i)
                    a.SetValue(v[i], i);
                return a;
            }
            if (t.IsGenericType)
            {
                var c = t.GetGenericArguments();
                if (c.Length == 1)
                {
                    var et = c[0];
                    var gd = t.GetGenericTypeDefinition();
                    if (AllowedGenericTypes.Contains(gd))
                    {
                        var v = GetAll(et, type, ServiceInstanceOrders.Oldest).ToList();
                        var vl = v.Count;
                        var a = Array.CreateInstance(et, vl);
                        for (int i = 0; i < vl; ++i)
                            a.SetValue(v[i], i);
                        if (gd.IsInterface)
                            return a;
                        return Activator.CreateInstance(t, a);
                    }
                }
            }
            return TryGet(t, type);
        }



        Object GetManifestParam(out bool isOptional, ServiceManifest m)
        {
            isOptional = false;
            var op = m.Params;
            if (op == null)
                return null;
            var type = TypeFinder.Get(m.Type);
            if (type == null)
                return null;
            if (type.IsInterface)
            {
                if (m.TryGetParamAs(out var remoteConnection, typeof(RemoteConnection)))
                    return remoteConnection;
            }
            if (typeof(ISerializerType).IsAssignableFrom(type))
                return null;
            if (typeof(ICompType).IsAssignableFrom(type))
                return null;
            var cis = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(cis, (a, b) => b.GetParameters().Length - a.GetParameters().Length);
            foreach (var c in cis)
            {
                var p = c.GetParameters();
                var pl = p.Length;
                Object opt = null;
                bool fail = false;
                for (int i = 0; i < pl; ++i)
                {
                    var pa = p[i];
                    var pt = pa.ParameterType;
                    var a = TryGetObject(pt);
                    if (a != null)
                        continue;
                    if (m.TryGetParamAs(out opt, pt))
                    {
                        isOptional = pa.HasDefaultValue;
                        continue;
                    }
                    if (pa.HasDefaultValue)
                    {
                        continue;
                    }
                    else
                    {
                        if (!pt.IsValueType)
                            continue;
                    }
                    fail = true;
                    break;
                }
                if (!fail)
                    return opt;
            }
            return null;
        }

        Type GetManifestParamType(ServiceManifest m)
        {
            var type = TypeFinder.Get(m.Type);
            if (type == null)
                return null;
            if (type.IsInterface)
            {
                if (typeof(IRemoteApi).IsAssignableFrom(type))
                    return typeof(RemoteConnection);
            }
            if (typeof(ISerializerType).IsAssignableFrom(type))
                return null;
            if (typeof(ICompType).IsAssignableFrom(type))
                return null;
            var cis = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(cis, (a, b) => b.GetParameters().Length - a.GetParameters().Length);
            foreach (var c in cis)
            {
                var p = c.GetParameters();
                var pl = p.Length;
                Type outType = null;
                bool fail = false;
                for (int i = 0; i < pl; ++i)
                {
                    var pa = p[i];
                    var pt = pa.ParameterType;
                    var a = TryGetObject(pt);
                    if (a != null)
                        continue;
                    if (pa.HasDefaultValue)
                    {
                        if (!pt.IsValueType)
                            outType = pt;
                        continue;
                    }
                    else
                    {
                        if (!pt.IsValueType)
                            continue;
                    }
                    fail = true;
                    break;
                }
                if (!fail)
                    return outType;
            }
            return null;
        }


        /// <summary>
        /// Register a service manifest entry
        /// </summary>
        /// <param name="m">The entry to register</param>
        public void RegisterManifest(ServiceManifest m)
        {
            var start = PerfMonitor.GetTimestamp();
            var type = TypeFinder.Get(m.Type);
            if (type == null)
            {
                var ex = new TypeInitializationException(m.Type, null);
                AddMessage(String.Concat("Type \"", m.Type, "\" not found!"), ex);
                Flush();
                throw ex;
            }
            if (type.IsInterface)
            {
                if (m.TryGetParamAs(out var remoteConnection, typeof(RemoteConnection)))
                {
                    Object inst;
                    try
                    {
                        using (Tab())
                        {
                            inst = ((RemoteConnection)remoteConnection).Create(type);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddMessage(String.Concat(Tag, " Failed to create remote connetion of type ", m.Type.ToQuoted()), ex);
                        throw;
                    }
                    Register(inst, m.Name, true, typeof(RemoteConnection)).StartDuration = PerfMonitor.GetEllapsed(start);
                    return;
                }
            }
            if (typeof(ISerializerType).IsAssignableFrom(type))
            {
                var regT = type.GetMethod(nameof(ISerializerType.Register), BindingFlags.Static | BindingFlags.Public, null, [], null);
                if (regT != null)
                {
                    regT.Invoke(null, null);
                    AddMessage(String.Concat(Tag, " Registered serializer of type \"", type.FullName, "\""));
                    return;
                }
            }
            if (typeof(ICompType).IsAssignableFrom(type))
            {
                var regT = type.GetMethod(nameof(ICompType.Register), BindingFlags.Static | BindingFlags.Public, null, [], null);
                if (regT != null)
                {
                    regT.Invoke(null, null);
                    AddMessage(String.Concat(Tag, " Registered compressor of type \"", type.FullName, "\""));
                    return;
                }
            }
            var cis = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(cis, (a, b) => b.GetParameters().Length - a.GetParameters().Length);
            HashSet<Type> missingTypes = new HashSet<Type>();
            Type paramType = null;
            foreach (var c in cis)
            {
                var p = c.GetParameters();
                var pl = p.Length;
                var args = GC.AllocateUninitializedArray<Object>(pl);
                bool fail = false;
                for (int i = 0; i < pl; ++ i)
                {
                    var pa = p[i];
                    var pt = pa.ParameterType;
                    var a = TryGetObject(pt);
                    if (a != null)
                    {
                        args[i] = a;
                        continue;
                    }
                    var op = m.Params;
                    if (op != null)
                    {
                        if (m.TryGetParamAs(out var opt, pt))
                        {
                            args[i] = opt;
                            paramType = pt;
                            continue;
                        }
                    }else
                    {
                        paramType = paramType ?? pt;
                    }
                    if (pa.HasDefaultValue)
                    {
                        args[i] = pa.DefaultValue;
                        continue;
                    }else
                    {
                        if (!pt.IsValueType)
                        {
                            args[i] = null;
                            continue;
                        }
                    }
                    missingTypes.Add(pt);
                    fail = true;
                    break;
                }
                if (fail)
                    continue;
                if (args.Length > 0)
                    AddMessage(String.Concat(Tag, " Creating instance of type \"", type.Name, "\", with arguments: ", String.Join(", ", args.Select(x => GetToString(x)))));
                else
                    AddMessage(String.Concat(Tag, " Creating instance of type \"", type.Name, '"'));
                Object inst;
                try
                {
                    using (Tab())
                    {
                        inst = c.Invoke(args);
                    }
                }
                catch (Exception ex)
                {
                    AddMessage(String.Concat(Tag, " Failed to create service of type ", m.Type.ToQuoted()), ex);
                    throw;
                }
                Register(inst, m.Name, true, paramType).StartDuration = PerfMonitor.GetEllapsed(start);
                return;
            }
            var ex2 = new Exception("Can't find a constructor in \"" + m.Type + "\" that matches the available instances, can't find an instance for the following types:\n" + String.Join('\n', missingTypes.Select(x => x.FullName.ToQuoted())));
            AddMessage(String.Concat("Can't create an instance of type \"", m.Type, "\""), ex2);
            Flush();
            throw ex2;
        }

        static String GetToString(Object o)
        {
            if (o == null)
                return "null";
            var t = o.GetType();
            if (QuotedTypes.Contains(t))
                return o.ToString().ToQuoted();
            if (t.IsEnum || t.IsPrimitive)
                return o.ToString();
            var v = o as Array;
            if (v != null)
                return String.Join(String.Join(", ", v), '[', ']');
            var n = o.ToString();
            if (n == t.FullName)
                n = t.Name;
            return String.Join(n, '{', '}');
        }

        static readonly IReadOnlySet<Type> QuotedTypes = ReadOnlyData.Set(
            typeof(String), typeof(DateTime), typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly), typeof(DateTimeOffset), typeof(Guid), typeof(Char)
        );


        internal static readonly JsonSerializerOptions DeSerOpt = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IgnoreReadOnlyFields = true,
            IncludeFields = true,
        };

        /// <summary>
        /// Register a service manifest (array of service manifest entries as json)
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="filename"></param>
        /// <param name="trackPerf"></param>
        public void RegisterManifest(String manifest, String filename = null, bool trackPerf = true)
        {
            using var perfMon = (trackPerf && !String.IsNullOrEmpty(filename)) ? PerfMon.Track("Register." + Path.GetFileName(filename)) : null;
            ServiceManifest[] mf;
            try
            {
                mf = JsonSerializer.Deserialize<ServiceManifest[]>(manifest, DeSerOpt);
            }
            catch (Exception ex)
            {
                if (String.IsNullOrEmpty(filename))
                    AddMessage(String.Concat(Tag, " Failed to parse manifest"), ex);
                else
                    AddMessage(String.Concat(Tag, " Failed to parse manifest file ", filename.ToFilename()), ex);
                throw;
            }
            foreach (var m in mf)
            {
                RegisterManifest(m);
            }
        }




        /// <summary>
        /// Register a file containing a serivce manifest (array of service manifest entries as json)
        /// </summary>
        /// <param name="file">The name of the file</param>
        /// <param name="trackPerf"></param>
        public void RegisterManifestFile(String file, bool trackPerf = true)
        {
            using var perfMon = trackPerf ? PerfMon.Track("Register." + Path.GetFileName(file)) : null;
            AddMessage(Tag + " Registering services from file \"" + file + "\":");
            var i = Instances;
            var c = i.Count;
            using (Tab())
            {
                String text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    AddMessage(String.Concat(Tag, " Failed to read manifest file ", file.ToFilename()), ex);
                    throw;
                }
                RegisterManifest(text, file, false);
            }
            c = i.Count - c;
            AddMessage(Tag + " Added " + c + (c == 1 ? " service from the file." : " services from the file."));
        }

        static String ServiceName(Object i, ServiceInfo info, bool isDebug = false)
        {
            var type = i.GetType();
            var n = isDebug ? type.FullName : type.Name;
            var x = info?.Name;
            var val = i.ToString();
            if (val == type.FullName)
                val = "";
            else
                val = String.Join(val, " {", '}');
            if (String.IsNullOrEmpty(x))
                return String.Concat('"', n, '"', val);
            return String.Concat('"', n, "\" [", x, ']', val);
        }


        /// <summary>
        /// Unregister an instance (remove it from the service manager)
        /// </summary>
        /// <param name="instance"></param>
        /// <exception cref="Exception"></exception>
        public ServiceInfo Unregister(Object instance)
        {
            if (!Instances.TryRemove(instance, out var info))
                throw new Exception("The supplied instance isn't registered!");

            RemoveHaveStats(instance as IHaveStats);
            RemoveHavePerfMonitor(instance as IPerfMonitored);
            RemoveMessageHandler(instance as MessageHandler);

            var t = instance as IServiceMessageListener;
            if (t != null)
            {
                if (!MessageListeners.TryRemove(t, out var _))
                    throw new Exception("Internal error!");
            }
            var type = instance.GetType();
            RemoveType(type, instance);
            AddMessage(String.Join(" Unregistered instance of type ", Tag, ServiceName(instance, info)));
            OnServiceRemoved?.Invoke(instance, info);
            return info;
        }





        





        #endregion// Add / remove

        #region Filters

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> FilterAny(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c;

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> FilterLocalOnly(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.Where(x => !x.Value.Remote);

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> FilterRemoteOnly(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.Where(x => x.Value.Remote);

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> FilterLocalOrRemote(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.Where(x => !x.Value.Remote).Concat(c.Where(x => x.Value.Remote));
        static IEnumerable<KeyValuePair<Object, ServiceInfo>> FilterRemoteAllLocal(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.Where(x => x.Value.Remote).Concat(c.Where(x => !x.Value.Remote));



        static readonly Func<IEnumerable<KeyValuePair<Object, ServiceInfo>>, IEnumerable<KeyValuePair<Object, ServiceInfo>>>[] Filters = new Func<IEnumerable<KeyValuePair<object, ServiceInfo>>, IEnumerable<KeyValuePair<object, ServiceInfo>>>[]
        {
            FilterAny,
            FilterLocalOnly,
            FilterRemoteOnly,
            FilterLocalOrRemote,
            FilterRemoteAllLocal,
        };

        #endregion//Filters

        #region Orders

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> OrderAny(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c;

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> OrderOldest(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.OrderBy(x => x.Value.Order);

        static IEnumerable<KeyValuePair<Object, ServiceInfo>> OrderNewest(IEnumerable<KeyValuePair<Object, ServiceInfo>> c) => c.OrderByDescending(x => x.Value.Order);



        static readonly Func<IEnumerable<KeyValuePair<Object, ServiceInfo>>, IEnumerable<KeyValuePair<Object, ServiceInfo>>>[] Orders = new Func<IEnumerable<KeyValuePair<object, ServiceInfo>>, IEnumerable<KeyValuePair<object, ServiceInfo>>>[]
        {
            OrderAny,
            OrderOldest,
            OrderNewest,
        };

        #endregion//Orders


        #region Single instance

        /// <summary>
        /// Get the instance registered as the specified type
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="type">Filter instances</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public T Get<T>(ServiceInstanceTypes type = ServiceInstanceTypes.Any, String instanceName = null, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => Get<T>(out var _, type, instanceName, order);


        /// <summary>
        /// Get the instance registered as the specified type
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public T Get<T>(String instanceName, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => Get<T>(out var _, type, instanceName, order);

        /// <summary>
        /// Get the instance registered as the specified type
        /// </summary>
        /// <param name="t">The type of the object to get</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public Object Get(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => Get(out var _, t, type, null, order);

        /// <summary>
        /// Get the instance registered as the specified type
        /// </summary>
        /// <param name="t">The type of the object to get</param>
        /// <param name="type">Filter instances</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public Object Get(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, String instanceName = null, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => Get(out var _, t, type, instanceName, order);

        /// <summary>
        /// Get the instance registered as the specified type
        /// </summary>
        /// <param name="t">The type of the object to get</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public Object Get(Type t, String instanceName, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => Get(out var _, t, type, instanceName, order);

        /// <summary>
        /// Get the instance registered as the specified type and the associated information
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="info">Information about the instance</param>
        /// <param name="type">Filter instances</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public T Get<T>(out ServiceInfo info, ServiceInstanceTypes type = ServiceInstanceTypes.Any, String instanceName = null, ServiceInstanceOrders order = ServiceInstanceOrders.Newest)
        {
            var t = typeof(T);
            if (!TypeInstances.TryGetValue(t, out var x))
                throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            if (String.IsNullOrEmpty(instanceName))
            {
                using var e = d.GetEnumerator();
                if (!e.MoveNext())
                    throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
                var v = e.Current;
                info = v.Value;
                info.OnUse();
                return (T)v.Key;
            }
            foreach (var v in d)
            {
                info = v.Value;
                if (String.Equals(info.Name, instanceName, StringComparison.OrdinalIgnoreCase))
                {
                    info.OnUse();
                    return (T)v.Key;
                }
            }
            throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
        }

        /// <summary>
        /// Get the instance registered as the specified type and the associated information
        /// </summary>
        /// <param name="info">Information about the instance</param>
        /// <param name="t">The type of the object to get</param>
        /// <param name="type">Filter instances</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public Object Get(out ServiceInfo info, Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, String instanceName = null, ServiceInstanceOrders order = ServiceInstanceOrders.Newest)
        {
            if (!TypeInstances.TryGetValue(t, out var x))
                throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            if (String.IsNullOrEmpty(instanceName))
            {
                using var e = d.GetEnumerator();
                if (!e.MoveNext())
                    throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
                var v = e.Current;
                info = v.Value;
                info.OnUse();
                return v.Key;
            }
            foreach (var v in d)
            {
                info = v.Value;
                if (String.Equals(info.Name, instanceName, StringComparison.OrdinalIgnoreCase))
                {
                    info.OnUse();
                    return v.Key;
                }
            }
            throw new Exception("No instance of type \"" + t.FullName + "\" is registered!");
        }


        /// <summary>
        /// Try to get the instance registered as the specified type 
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type or null if it doesn't exist</returns>
        public T TryGet<T>(ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) where T : class => TryGet<T>(out var _, type, order);

        /// <summary>
        /// Try to get the instance registered as the specified type 
        /// </summary>
        /// <param name="t">The type of the object to get</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type or null if it doesn't exist</returns>
        public Object TryGet(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) => TryGet(out var _, t, type, order);


        /// <summary>
        /// Try to get the instance registered as the specified type and the associated information
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="info">Information about the instance</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type or null if it doesn't exist</returns>
        public T TryGet<T>(out ServiceInfo info, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) where T : class
        {
            var t = typeof(T);
            info = null;
            if (!TypeInstances.TryGetValue(t, out var x))
                return null;
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            using (var e = d.GetEnumerator())
            {
                if (!e.MoveNext())
                    return null;
                var v = e.Current;
//                if (e.MoveNext())
//                    return null;
                info = v.Value;
                info.OnUse();
                return (T)v.Key;
            }
        }

        /// <summary>
        /// Try to get the instance registered as the specified type and the associated information
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns></returns>
        public T TryGet<T>(String instanceName, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) where T : class
        {
            var all = GetAllInfo<T>(type, order);
            if (instanceName == null)
                return all.FirstOrDefault().Key;
            foreach (var x in all)
                if (x.Value.Name.FastStartsWith(instanceName))
                    return x.Key;
            return null;
        }

        /// <summary>
        /// Try to get the instance registered as the specified type and the associated information
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="info">Information about the instance</param>
        /// <param name="instanceName">An optional instance name</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns></returns>
        public T TryGet<T>(out ServiceInfo info, String instanceName, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest) where T : class
        {
            var all = GetAllInfo<T>(type, order);
            if (instanceName == null)
            {
                var f = all.FirstOrDefault();
                info = f.Value;
                return f.Key;
            }
            foreach (var x in all)
            {
                info = x.Value;
                if (info.Name.FastStartsWith(instanceName))
                    return x.Key;
            }
            info = null;
            return null;
        }


        /// <summary>
        /// Try to get the instance registered as the specified type and the associated information
        /// </summary>
        /// <param name="info">Information about the instance</param>
        /// <param name="t">The type of the object to get</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type or null if it doesn't exist</returns>
        public Object TryGet(out ServiceInfo info, Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Newest)
        {
            info = null;
            if (!TypeInstances.TryGetValue(t, out var x))
                return null;
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            using (var e = d.GetEnumerator())
            {
                if (!e.MoveNext())
                    return null;
                var v = e.Current;
//                if (e.MoveNext())
//                    return null;
                info = v.Value;
                info.OnUse();
                return v.Key;
            }
        }

        #endregion//Single instance

        #region Multiple instances

        /// <summary>
        /// Get the instances registered as the specified type
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public IEnumerable<T> GetAll<T>(ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Any)
        {
            var t = typeof(T);
            if (!TypeInstances.TryGetValue(t, out var x))
                return [];
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            return d.Select(x =>
            {
                x.Value.OnUse();
                return (T)x.Key;
            });
        }


        /// <summary>
        /// Get the instances registered as the specified type
        /// </summary>
        /// <param name="t">The type to get instances for</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public IEnumerable<Object> GetAll(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Any)
        {
            if (!TypeInstances.TryGetValue(t, out var x))
                return [];
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            return d.Select(x =>
            {
                x.Value.OnUse();
                return x.Key;
            });
        }

        /// <summary>
        /// Get the instances registered
        /// </summary>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instances</returns>
        public IEnumerable<Object> GetAll(ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Any)
        {
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = Instances;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            return d.Select(x =>
            {
                x.Value.OnUse();
                return x.Key;
            });
        }


        


        /// <summary>
        /// Get the instances registered as the specified type and the associated information
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public IEnumerable<KeyValuePair<T, ServiceInfo>> GetAllInfo<T>(ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Any)
        {
            var t = typeof(T);
            if (!TypeInstances.TryGetValue(t, out var x))
                return [];
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            return d.Select(x =>
            {
                x.Value.OnUse();
                return new KeyValuePair<T, ServiceInfo>((T)x.Key, x.Value);
            });
        }

        /// <summary>
        /// Get the instances registered as the specified type and the associated information
        /// </summary>
        /// <param name="t">The type to get instances for</param>
        /// <param name="type">Filter instances</param>
        /// <param name="order">Order of the instanes</param>
        /// <returns>The instance that matches the type</returns>
        public IEnumerable<KeyValuePair<Object, ServiceInfo>> GetAllInfo(Type t, ServiceInstanceTypes type = ServiceInstanceTypes.Any, ServiceInstanceOrders order = ServiceInstanceOrders.Any)
        {
            if (!TypeInstances.TryGetValue(t, out var x))
                return [];
            IEnumerable<KeyValuePair<Object, ServiceInfo>> d = x;
            d = Filters[(int)type](d);
            d = Orders[(int)order](d);
            return d.Select(x =>
            {
                x.Value.OnUse();
                return new KeyValuePair<Object, ServiceInfo>(x.Key, x.Value);
            });
        }


        #endregion//Multiple instances

        #region Info

        /// <summary>
        /// Get information about an instance
        /// </summary>
        /// <param name="o">The instance to get information about</param>
        /// <returns>Information about the instance or null if it isn't registered</returns>
        public ServiceInfo GetInfo(Object o)
        {
            Instances.TryGetValue(o, out var x);
            return x;
        }

        /// <summary>
        /// Get all unique registered instances
        /// </summary>
        public IEnumerable<Object> UniqueInstances => Instances.Keys;

        /// <summary>
        /// Get all unique registered instances, and the associated information about them
        /// </summary>
        public IEnumerable<KeyValuePair<Object, ServiceInfo>> UniqueInfoInstances => Instances;

        /// <summary>
        /// Get all unique registered instances in the order that they where registered
        /// </summary>
        public IEnumerable<Object> OrderedUniqueInstances => Instances.OrderBy(x => x.Value.Order).Select(x => x.Key);

        /// <summary>
        /// Get all unique registered instances in the order that they where registered, and the associated information about them
        /// </summary>
        public IEnumerable<KeyValuePair<Object, ServiceInfo>> OrderedUniqueInfoInstances => Instances.OrderBy(x => x.Value.Order);

        #endregion//Info

        #region Internals

        void AddType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type t, Object service, ServiceInfo info)
        {
            bool first = true;
            while (t != null)
            {
                var m = GetTypeInstance(t);
                if (!m.TryAdd(service, info))
                    return;
                if (first)
                {
                    first = false;
                    foreach (var x in t.GetInterfaces())
                    {
                        m = GetTypeInstance(x);
                        m.TryAdd(service, info);
                    }
                }
                if (t == typeof(Object))
                    return;
                t = t.BaseType;
            }
        }

        void RemoveType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type t, Object service)
        {
            bool first = true;
            while (t != null)
            {
                var m = GetTypeInstance(t);
                if (!m.TryRemove(service, out var info))
                    return;
                if (first)
                {
                    first = false;
                    foreach (var x in t.GetInterfaces())
                    {
                        m = GetTypeInstance(x);
                        m.TryRemove(service, out info);
                    }
                }
                if (t == typeof(Object))
                    return;
                t = t.BaseType;
            }
        }

        ConcurrentDictionary<Object, ServiceInfo> GetTypeInstance(Type t)
        {
            var xx = TypeInstances;
            ConcurrentDictionary<object, ServiceInfo> n = null;
            for (; ; )
            {
                if (xx.TryGetValue(t, out var v))
                    return v;
                n = n ?? new ConcurrentDictionary<object, ServiceInfo>();
                if (xx.TryAdd(t, n))
                    return n;
            }
        }


        readonly ConcurrentDictionary<Type, ConcurrentDictionary<Object, ServiceInfo>> TypeInstances = new ConcurrentDictionary<Type, ConcurrentDictionary<object, ServiceInfo>>();
        readonly ConcurrentDictionary<Object, ServiceInfo> Instances = new ConcurrentDictionary<object, ServiceInfo>();
        readonly ConcurrentDictionary<IServiceMessageListener, ServiceInfo> MessageListeners = new ConcurrentDictionary<IServiceMessageListener, ServiceInfo>();



        #endregion//Internals

        #region Messaging

        /// <summary>
        /// Post a message to all listeners without blocking (messages will be delivered using a new task chain)
        /// </summary>
        /// <param name="key">Message key</param>
        /// <param name="data">Message data</param>
        public void PostMessage(String key, Object data) => TaskExt.StartNewAsyncChain(() => PostMessageAsync(key, data).ConfigureAwait(false));

        /// <summary>
        /// Post a message to all listeners an return a task that can be awaitable (all message will be delivered before it completes)
        /// </summary>
        /// <param name="key">Message key</param>
        /// <param name="data">Message data</param>
        /// <returns></returns>
        public Task PostMessageAsync(String key, Object data)
        {
            var k = MessageListeners.Keys.ToList();
            var kl = k.Count;
            if (kl <= 0)
                return Task.CompletedTask;
            if (kl == 1)
                return DeliverMessage(k[0], key, data);
            Task[] tasks = GC.AllocateUninitializedArray<Task>(kl);
            var du = tasks.AsSpan();
            for (int i = 0; i < kl; ++i)
                du[i] = DeliverMessage(k[i], key, data);
            return Task.WhenAll(tasks);
        }


        async Task DeliverMessage(IServiceMessageListener m, String key, Object data)
        {
            try
            {
                await m.OnServiceMessage(key, data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServiceMessageExceptions.OnException(ex);
            }
        }

        readonly ExceptionTracker ServiceMessageExceptions = new ExceptionTracker();

        #endregion//Messaging


        /// <summary>
        /// Check if a service is available
        /// </summary>
        /// <param name="serviceTypeName">The type name of the service</param>
        /// <returns>True if the service exist</returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public bool HaveService(String serviceTypeName)
        {
            var t = TypeFinder.Get(serviceTypeName);
            if (t == null)
                return false;
            return TryGet(t) != null;
        }

        #region Debug


        /// <summary>
        /// Get information about all registered services as table data
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(29)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/{0}", "Loaded services", null, "IconTableServices")]
        public TableData ServicesTable(TableDataRequest r) => TableDataTools.Get(r, 30000, Instances.Values);

        #endregion//Debug



        public bool IsPaused { get; private set; }

        public bool Pause()
        {
            lock (this)
            {
                if (IsPaused)
                    return false;
                AddMessage("Service pausing");
                foreach (var o in Instances.OrderByDescending(x => x.Value.Order))
                { 
                    var p = o.Key as IServicePausable;
                    p?.Pause();
                }
                IsPaused = true;
            }
            AddMessage("Service paused");
            return true;
        }

        public bool Resume()
        {
            lock (this)
            {
                if (!IsPaused)
                    return false;
                AddMessage("Service is resuming");
                foreach (var o in Instances.OrderBy(x => x.Value.Order))
                {
                    var p = o.Key as IServicePausable;
                    p?.Continue();
                }
                IsPaused = false;
            }
            AddMessage("Service has resumed");
            return true;
        }



        #region Stats


        bool AddHaveStats(IHaveStats h) => h != null && HaveStats.TryAdd(h, 0);

        bool RemoveHaveStats(IHaveStats h) => h != null && HaveStats.TryRemove(h, out var _);

        readonly ConcurrentDictionary<IHaveStats, int> HaveStats = new();


        bool AddHavePerfMonitor(IPerfMonitored h) => h != null && HavePerfMonitor.TryAdd(h, 0);

        bool RemoveHavePerfMonitor(IPerfMonitored h) => h != null && HavePerfMonitor.TryRemove(h, out var _);

        readonly ConcurrentDictionary<IPerfMonitored, int> HavePerfMonitor = new();


        /// <summary>
        /// Return all stats
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Stats> GetStats()
        {
            const String sys = nameof(ServiceManager);
            yield return new Stats(sys, nameof(Monitor.LockContentionCount), Monitor.LockContentionCount, "The number of times there was contention when trying to take the monitor's lock");
            foreach (var x in EnvInfo.GetStats())
                yield return x;
            foreach (var s in HaveStats.Keys)
            {
                foreach (var x in s.GetStats())
                    yield return x;
            }
        }
        /// <summary>
        /// Return all perfmonitors
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPerfEntry> GetPerformanceEntries(bool onlyApis = false)
        {
            if (!onlyApis)
            {
                foreach (var x in PerfMon)
                    yield return x;
                foreach (var s in HavePerfMonitor.Keys)
                {
                    foreach (var x in s.PerfMon)
                        yield return x;
                }
            }
            else
            {
                foreach (var s in HavePerfMonitor.Keys)
                {
                    foreach (var x in s.PerfMon)
                        if (x.System == "API")
                            yield return x;
                }
            }
        }

        /// <summary>
        /// Get statistics for all registered services as table data
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>Table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1, WebApiCaches.Globally)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Debug/{0}", "Statistics", null, "IconTableStats")]
        public Task<TableData> StatsTable(TableDataRequest r, HttpServerRequest context) => TableDataTools.Get(context, r, 1000, GetStats());


        /// <summary>
        /// Get performance information
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>Table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Debug/{0}", "Performance", null, "IconTablePerformance")]
        public TableData PerfTable(TableDataRequest r, HttpServerRequest context)
        {
            PerfTableApis.Add(context.LocalUrl);
            bool onlyApi = !context.Session.Auth.IsValid("debug,ops");
            var data = TableDataTools.Get(r, 1000, GetPerformanceEntries(onlyApi));
            if (onlyApi)
                data = TableDataTools.HideColumn(data, nameof(IPerfEntry.System));
            return data;
        }


        /// <summary>
        /// Get performance monitor information
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>Table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Debug/{0}", "Performance systems", null, "IconTablePerfSystem")]
        public TableData PerfSystemsTable(TableDataRequest r, HttpServerRequest context)
        {
            PerfTableApis.Add(context.LocalUrl);
            return TableDataTools.Get(r, 30000, HavePerfMonitor.Keys.Select(x => x.PerfMon));
        }

        readonly HashSet<String> PerfTableApis = new HashSet<string>();


        /// <summary>
        /// Toggle performance monitor on/off
        /// </summary>
        /// <param name="systemName">The name of the system</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        public bool TogglePerformanceMonitor(String systemName)
        {
            foreach (var x in HavePerfMonitor.Keys)
            {
                var i = x.PerfMon;
                if (i.System == systemName)
                {
                    i.Enabled ^= true;
                    var s = Server;
                    if (s == null)
                    {
                        s = TryGet<HttpServerBase>();
                        Server = s;
                    }
                    var tab = PerfTableApis;
                    s?.InvalidateCache(localUrl => tab.Contains(localUrl));
                    return true;
                }
            }
            return false;
        }

        HttpServerBase Server;

        /// <summary>
        /// Reset a performance monitor
        /// </summary>
        /// <param name="systemName">The name of the system</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        public bool ResetPerformanceMonitor(String systemName)
        {
            foreach (var x in HavePerfMonitor.Keys)
            {
                var i = x.PerfMon;
                if (i.System == systemName)
                {
                    i.Reset();
                    if (!i.Enabled)
                        i.Enabled = true;
                    var s = Server;
                    if (s == null)
                    {
                        s = TryGet<HttpServerBase>();
                        Server = s;
                    }
                    var tab = PerfTableApis;
                    s?.InvalidateCache(localUrl => tab.Contains(localUrl));
                    return true;
                }
            }
            return false;
        }

        #endregion//Stats

        /// <summary>
        /// Determine if a type is a micro service
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsMicroService(Type t)
        {
            var cache = CacheIsMicroService;
            if (cache.TryGetValue(t, out var res))
                return res;
            foreach (var x in ServiceTypes)
            {
                if (x.IsAssignableFrom(t))
                {
                    cache[t] = true;
                    return true;
                }
            }
            foreach (var x in ServiceAttributes)
            {
                if (t.GetCustomAttribute(x) != null)
                {
                    cache[t] = true;
                    return true;
                }
            }
            cache[t] = false;
            return false;
        }

        static readonly ConcurrentDictionary<Type, bool> CacheIsMicroService = new ConcurrentDictionary<Type, bool>();


        public static readonly IReadOnlyList<Type> ServiceTypes = [
            typeof(IRemoteApi),
            typeof(ISerializerType),
            typeof(ICompType),
            typeof(Security.ICertificateProvider),
        ];

        public static readonly IReadOnlyList<Type> ServiceAttributes = [
            typeof(IsMicroServiceAttribute),
            typeof(RequiredDepAttribute),
            typeof(OptionalDepAttribute),
        ];

        #region One time pad

        PeriodicTask PruneTask;


        readonly long OneTimePadLifeTime = TimeSpan.FromSeconds(10).Ticks;

        readonly long OneTimePadRemoveTime = TimeSpan.FromSeconds(11).Ticks;

        bool Prune()
        {
            var tokens = OneTimePads;
            var queue = OneTimePadQueue;
            var now = DateTime.UtcNow.Ticks;
            while (queue.TryPeek(out var i))
            {
                if (i.Item1 > now)
                    break;
                tokens.TryRemove(i.Item2, out var _);
                queue.TryDequeue(out i);
            }
            return true;
        }

        readonly ConcurrentDictionary<String, String> OneTimePads = new ConcurrentDictionary<string, String>(StringComparer.Ordinal);
        readonly ConcurrentQueue<Tuple<long, String>> OneTimePadQueue = new ConcurrentQueue<Tuple<long, string>>();

        /// <summary>
        /// Create a new one time pad to use for login
        /// </summary>
        /// <returns>A one time pad string</returns>
        public String CreateOneTimePad(String payload)
        {
            var tokens = OneTimePads;
            String token;
            using (var r = new SecureRng())
            {
                for (; ; )
                {
                    token = r.GetTimeStampGuid24();
                    if (tokens.TryAdd(token, payload))
                        break;
                }
            }
            OneTimePadQueue.Enqueue(Tuple.Create(DateTime.UtcNow.Ticks + OneTimePadRemoveTime, token));
            return token;
        }

        /// <summary>
        /// Consume a one time pad (test if it's valid and remove it)
        /// </summary>
        /// <param name="oneTimePad">The one time pad to consume</param>
        /// <param name="payload">The payload if valid</param>
        /// <returns>True if the one time pad was valid, else false</returns>
        public bool TryConsumeOneTimePad(String oneTimePad, out String payload)
        {
            if (!OneTimePads.TryRemove(oneTimePad, out payload))
                return false;
            var expTime = SecureRng.GetTimeStampFromGuid(oneTimePad) + OneTimePadLifeTime;
            var now = DateTime.UtcNow.Ticks;
            return expTime > now;
        }

        /// <summary>
        /// Look at the data for a one time pad (do NOT use since in most cases it should be consumed as well) 
        /// </summary>
        /// <param name="oneTimePad">The one time pad to consume</param>
        /// <param name="payload">The payload if valid</param>
        /// <returns>True if the one time pad was valid, else false</returns>
        public bool InspectOneTimePad(String oneTimePad, out String payload)
            => OneTimePads.TryGetValue(oneTimePad, out payload);

        #endregion//One time pad


    }


}
