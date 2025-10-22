
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
using BitFaster.Caching;

namespace SysWeaver.Remote.Connection
{

    public class ApiMeta
    {
        public ApiMeta(String name, int cacheDuration, int maxCachedItems, RemoteConnection con)
        {
            Name = name;
        }
        public readonly String Name;
    }

    public sealed class ApiMeta<T> : ApiMeta
    {
        public ApiMeta(String name, int cacheDuration, int maxCachedItems, RemoteConnection con) : base(name, cacheDuration, maxCachedItems, con)
        {
            if (cacheDuration == RemoteCacheAttribute.UseConnection)
                cacheDuration = con.CacheDuration;
            if (maxCachedItems == RemoteCacheAttribute.UseConnection)
                maxCachedItems = con.MaxCachedItems;

            if (cacheDuration > 0)
            {
                if (maxCachedItems > 0)
                {
                    Cache = new ConcurrentLruBuilder<String, T>()
                            .WithExpireAfterWrite(TimeSpan.FromSeconds(cacheDuration))
                            .WithCapacity(maxCachedItems)
                            .Build();
                }else
                {
                    Cache = new ConcurrentLruBuilder<String, T>()
                            .WithExpireAfterWrite(TimeSpan.FromSeconds(cacheDuration))
                            .Build();
                }
            }
            else
            {
                if (maxCachedItems > 0)
                {
                    Cache = new ConcurrentLruBuilder<String, T>()
                            .WithCapacity(maxCachedItems)
                            .Build();
                }
            }
        }

        public readonly ICache<String, T> Cache;
    }


    static class InterfaceTypeConsts
    {
        public static readonly Type BaseType = typeof(RemoteConnectionBase);
        public static readonly Type[] BaseConstructorTypes = [typeof(RemoteConnection), typeof(Type)];
        public static readonly ConstructorInfo BaseConstructor = BaseType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, BaseConstructorTypes, null);
        
        
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        
        public static readonly MethodInfo[] RestTypeMethods = 
        [
                BaseType.GetMethod(nameof(HttpEndPointTypes.Get), bf),
                BaseType.GetMethod(nameof(HttpEndPointTypes.Post), bf),
                BaseType.GetMethod(nameof(HttpEndPointTypes.Put), bf),
                BaseType.GetMethod(nameof(HttpEndPointTypes.Delete), bf),
        ];

        public static readonly MethodInfo[] RestVoidTypeMethods =
        [
                BaseType.GetMethod("Void" + nameof(HttpEndPointTypes.Get), bf),
                BaseType.GetMethod("Void" + nameof(HttpEndPointTypes.Post), bf),
                BaseType.GetMethod("Void" + nameof(HttpEndPointTypes.Put), bf),
                BaseType.GetMethod("Void" + nameof(HttpEndPointTypes.Delete), bf),
        ];

    }


    /// <summary>
    /// Provides functionality for creating a remote connection instance of a specific type, this uses il emit to build a new type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    static class InterfaceTypeCache<T> where T : class, IDisposable
    {

        static Func<RemoteConnection, Type, T> Build()
        {
            var t = typeof(T);
            if (!t.IsInterface)
                throw new Exception("Only interfaces may be used, type \"" + t.FullName + "\" is NOT an interface!");
            if (!t.IsPublic)
                throw new Exception("The interface must be public, type \"" + t.FullName + "\" is NOT public!");
            if (!typeof(IDisposable).IsAssignableFrom(t))
                throw new Exception("The interface must inherit the IDisposable interface, type \"" + t.FullName + "\" do NOT inherit IDisposable!");

            var guid = Guid.NewGuid().ToString().Replace('-', '_');
            var asmName = new AssemblyName(String.Concat("RestApi.", t.Name, '_', guid));
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var moduleBuilder = asmBuilder.DefineDynamicModule(asmName.Name);
            var baseType = typeof(RemoteConnectionBase);//.MakeGenericType(t);
            var typeBuilder = moduleBuilder.DefineType(String.Join('.', guid, t.Name), TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class, baseType, [t]);

            var staticConstructor = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
            var staticIl = staticConstructor.GetILGenerator();


            var baseConstructor = InterfaceTypeConsts.BaseConstructor;
            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, InterfaceTypeConsts.BaseConstructorTypes);
            var constructorIl = constructor.GetILGenerator();

            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Ldarg_1);
            constructorIl.Emit(OpCodes.Ldarg_2);
            constructorIl.Emit(OpCodes.Call, baseConstructor);





            Dictionary<string, FieldBuilder> options = new Dictionary<string, FieldBuilder>();
            Dictionary<string, Tuple<Type, ConstructorInfo, TypeBuilder>> nestedTypes = new Dictionary<string, Tuple<Type, ConstructorInfo, TypeBuilder>>();

            var voidType = typeof(Task);
            var encoderType = typeof(UriParamsEncoder<>);
            Type[] noTypes = [];
            var metaTypeT = typeof(ApiMeta<>);
            int index = 0;
            var prefix = t.GetCustomAttribute<RemotePathPrefixAttribute>(false)?.PathPrefix ?? "";
            if (prefix.Length > 0)
                if (!prefix.EndsWith('/'))
                    prefix += "/";
            foreach (var m in t.GetMembers())
            {
                if (m.MemberType != MemberTypes.Method)
                    throw new Exception("Only methods are allowed, member \"" + m.Name + "\" in type \"" + t.FullName + "\" is NOT a method!");
                var mi = m as MethodInfo;
                var returnType = mi.ReturnType;
                bool isVoid = mi.ReturnType == voidType;
                if (!isVoid)
                {
                    if (!returnType.IsGenericType)
                        throw new Exception("Only Task or Task<T> may be returned, method \"" + m.Name + "\" in type \"" + t.FullName + "\" returns \"" + returnType.FullName + "\"");
                    returnType = returnType.GenericTypeArguments[0];
                }
                ++index;
                var miParams = mi.GetParameters();
                var parameterTypes = miParams.Select(v => v.ParameterType).ToArray();
                var parameterCount = parameterTypes.Length;
                var xt = isVoid ? parameterTypes : [returnType];
                Type[] returnParameterTypes = [..xt, ..parameterTypes];
                var method = typeBuilder.DefineMethod(mi.Name, MethodAttributes.Public | MethodAttributes.Virtual, mi.ReturnType, parameterTypes);
                var plen = miParams.Length;
                for (int i = 0; i < plen; ++ i)
                {
                    var sp = miParams[i];
                    var pb = method.DefineParameter(i + 1, sp.Attributes, sp.Name);
                    if (sp.HasDefaultValue)
                        pb.SetConstant(sp.DefaultValue);
                }
                var rest = mi.GetCustomAttribute<RemoteEndPointAttribute>();
                if (rest == null)
                {
                    //if (parameterCount > 1)
                    //throw new Exception("Method must have a " + nameof(RestApiAttribute) + " attribute, method \"" + m.Name + "\" in type \"" + t.FullName + "\" does NOT");
                    rest = new RemoteEndPointAttribute(mi.Name, parameterCount == 0 ? HttpEndPointTypes.Get : HttpEndPointTypes.Post);
                }
                var sf = "_T" + index;
                var il = method.GetILGenerator();
                var restMethod = rest.Type;
                var path = rest.Path;
                if (string.IsNullOrEmpty(path))
                    path = mi.Name;
                
                path = prefix + path;

                var metaType = isVoid ? typeof(ApiMeta) : metaTypeT.MakeGenericType(returnType);
                var metaName = "_M" + index;
                var metaField = typeBuilder.DefineField(metaName, metaType, FieldAttributes.Private | FieldAttributes.InitOnly);
                int cacheDuration = -2; // Number of seconds to keep the data
                int maxCachedItems = -2; // Max number of entries
                if (!isVoid)
                {
                    cacheDuration = RemoteCacheAttribute.UseConnection;
                    maxCachedItems = RemoteCacheAttribute.UseConnection;
                    var cacheAttr = mi.GetCustomAttribute<RemoteCacheAttribute>();
                    if (cacheAttr != null)
                    {
                        cacheDuration = Math.Max(RemoteCacheAttribute.UseConnection, cacheAttr.Duration);
                        maxCachedItems = Math.Max(RemoteCacheAttribute.UseConnection, cacheAttr.MaxItems);
                    }
                }
                constructorIl.Emit(OpCodes.Ldarg_0);
                constructorIl.Emit(OpCodes.Ldstr, String.Join(' ', restMethod.ToString().FastToUpper(), path));
                constructorIl.Emit(OpCodes.Ldc_I4, cacheDuration);
                constructorIl.Emit(OpCodes.Ldc_I4, maxCachedItems);
                constructorIl.Emit(OpCodes.Ldarg_1);
                constructorIl.Emit(OpCodes.Newobj, metaType.GetConstructor([typeof(String), typeof(int), typeof(int), typeof(RemoteConnection)]));
                constructorIl.Emit(OpCodes.Stfld, metaField);


                var serAttr = mi.GetCustomAttribute<RemoteSerializerAttribute>();
                var ser = serAttr?.Ser;
                var postSer = serAttr?.PostSer;
                var timeOut = mi.GetCustomAttribute<RemoteTimeoutAttribute>()?.TimeOutInMilliSeconds ?? 0;

                var useObject = (mi.GetCustomAttribute<ParamAsObjectAttribute>() ?? t.GetCustomAttribute<ParamAsObjectAttribute>(false)) != null;
                FieldBuilder optField = null;



                var restTypeMethods = InterfaceTypeConsts.RestTypeMethods;
                var restVoidTypeMethods = InterfaceTypeConsts.RestVoidTypeMethods;

                //  RestOptions builder, must match constructor
                if (ser != null || postSer != null || timeOut > 0)
                {
                    var key = String.Join('|', string.IsNullOrEmpty(ser) ? "" : ser, string.IsNullOrEmpty(postSer) ? "" : postSer, timeOut > 0 ? timeOut : 0);
                    if (!options.TryGetValue(key, out optField))
                    {
                        var optName = "_O" + index;
                        var ft = typeof(EndPointOptions);
                        var ftCon = ft.GetConstructor([typeof(string), typeof(string), typeof(int), typeof(String)]);
                        optField = typeBuilder.DefineField(sf, ft, FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Private);
                        if (string.IsNullOrEmpty(ser))
                            staticIl.Emit(OpCodes.Ldnull);
                        else
                            staticIl.Emit(OpCodes.Ldstr, ser);
                        if (string.IsNullOrEmpty(postSer))
                            staticIl.Emit(OpCodes.Ldnull);
                        else
                            staticIl.Emit(OpCodes.Ldstr, postSer);
                        staticIl.Emit(OpCodes.Ldc_I4, timeOut < 0 ? 0 : timeOut);
                        //  Number of arguments and order must be matched above
                        staticIl.Emit(OpCodes.Newobj, ftCon);
                        staticIl.Emit(OpCodes.Stsfld, optField);
                        options.Add(key, optField);
                    }
                }
                var restIndex = (int)restMethod;
                switch (parameterTypes.Length)
                {
                    case 0:
                        switch (restMethod)
                        {
                            case HttpEndPointTypes.Get:
                            case HttpEndPointTypes.Delete:
                                {
                                    il.Emit(OpCodes.Ldarg_0);
                                    il.Emit(OpCodes.Ldstr, path);
                                    il.Emit(OpCodes.Ldarg_0);
                                    il.Emit(OpCodes.Ldfld, metaField);
                                    if (optField != null)
                                        il.Emit(OpCodes.Ldsfld, optField);
                                    else
                                        il.Emit(OpCodes.Ldnull);
                                    MethodInfo baseMethod = isVoid ? restVoidTypeMethods[restIndex] : restTypeMethods[restIndex].MakeGenericMethod(returnType);
                                    il.Emit(OpCodes.Call, baseMethod);
                                }
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                        break;
                    case 1:
                        {
                            var inputType = parameterTypes[0];
                            switch (restMethod)
                            {
                                case HttpEndPointTypes.Get:
                                case HttpEndPointTypes.Delete:
                                    {
                                        var ft = typeof(Func<,>).MakeGenericType(inputType, typeof(string));
                                        var field = typeBuilder.DefineField(sf, ft, FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Private);
                                        staticIl.Emit(OpCodes.Ldstr, path);
                                        var encMet = encoderType.MakeGenericType(inputType).GetMethod(nameof(UriParamsEncoder<int>.Get), BindingFlags.Static | BindingFlags.Public);
                                        staticIl.Emit(OpCodes.Call, encMet);
                                        staticIl.Emit(OpCodes.Stsfld, field);
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldsfld, field);
                                        il.Emit(OpCodes.Ldarg_1);
                                        il.Emit(OpCodes.Callvirt, ft.GetMethod(nameof(Func<int>.Invoke), [inputType]));
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldfld, metaField);
                                        if (optField != null)
                                            il.Emit(OpCodes.Ldsfld, optField);
                                        else
                                            il.Emit(OpCodes.Ldnull);
                                        MethodInfo baseMethod = isVoid ? restVoidTypeMethods[restIndex] : restTypeMethods[restIndex].MakeGenericMethod(returnType);
                                        il.Emit(OpCodes.Call, baseMethod);
                                    }
                                    break;
                                case HttpEndPointTypes.Post:
                                case HttpEndPointTypes.Put:
                                    {
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldstr, path);
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldfld, metaField);
                                        il.Emit(OpCodes.Ldarg_1);
                                        if (optField != null)
                                            il.Emit(OpCodes.Ldsfld, optField);
                                        else
                                            il.Emit(OpCodes.Ldnull);
                                        MethodInfo baseMethod = isVoid ? restVoidTypeMethods[restIndex].MakeGenericMethod(inputType) : restTypeMethods[restIndex].MakeGenericMethod(returnType, inputType);
                                        il.Emit(OpCodes.Call, baseMethod);
                                    }
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;
                    default:
                        {
                            //  TODO: Support Object[]!?
                            var paramCount = miParams.Length;
                            Action pushData = null;
                            Type inputType = typeof(Object[]);
                            if (useObject)
                            {
                                var typeId = String.Join('|', miParams.Select(x => String.Join('=', x.ParameterType.FullName, x.Name)));
                                if (!nestedTypes.TryGetValue(typeId, out var nestedType))
                                {
                                    //var ntb = typeBuilder.DefineNestedType("_N" + index, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NestedPrivate);
                                    var ntb = moduleBuilder.DefineType(guid + ".P" + index, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic);
                                    var con = ntb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameterTypes);
                                    var conIl = con.GetILGenerator();
                                    int ai = 0;
                                    foreach (var x in miParams)
                                    {
                                        ++ai;
                                        var dfi = ntb.DefineField(x.Name, x.ParameterType, FieldAttributes.Public | FieldAttributes.InitOnly);
                                        conIl.Emit(OpCodes.Ldarg_0);
                                        conIl.Emit(OpCodes.Ldarg, ai);
                                        conIl.Emit(OpCodes.Stfld, dfi);
                                    }
                                    conIl.Emit(OpCodes.Ret);

                                    var ntbt = ntb.CreateType();
                                    nestedType = Tuple.Create(ntbt, ntbt.GetConstructor(parameterTypes), ntb);
                                    //nestedType = Tuple.Create((Type)ntb, (ConstructorInfo)con, ntb);

                                    nestedTypes.Add(typeId, nestedType);
                                }
                                var nestedCon = nestedType.Item2;
                                inputType = nestedType.Item1;
                                pushData = () =>
                                {
                                    for (int i = 1; i <= paramCount; ++i)
                                        il.Emit(OpCodes.Ldarg, i);
                                    il.Emit(OpCodes.Newobj, nestedCon);
                                };
                            }else
                            {
                                pushData = () =>
                                {
                                    il.Emit(OpCodes.Ldc_I4, paramCount);
                                    il.Emit(OpCodes.Newarr, typeof(Object));
                                    for (int i = 0; i < paramCount; ++i)
                                    {
                                        il.Emit(OpCodes.Dup);
                                        il.Emit(OpCodes.Ldc_I4, i);
                                        il.Emit(OpCodes.Ldarg, i + 1);
                                        if (miParams[i].ParameterType.IsValueType)
                                            il.Emit(OpCodes.Box);
                                        il.Emit(OpCodes.Stelem_Ref);
                                    }
                                };
                            }
                            switch (restMethod)
                            {
                                case HttpEndPointTypes.Get:
                                case HttpEndPointTypes.Delete:
                                    {
                                        var ft = typeof(Func<,>).MakeGenericType(inputType, typeof(string));
                                        var field = typeBuilder.DefineField(sf, ft, FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Private);
                                        staticIl.Emit(OpCodes.Ldstr, path);
                                        var tt = encoderType.MakeGenericType(inputType);
                                        var encMet = tt.GetMethod(nameof(UriParamsEncoder<int>.Get), BindingFlags.Static | BindingFlags.Public);
                                        staticIl.Emit(OpCodes.Call, encMet);
                                        staticIl.Emit(OpCodes.Stsfld, field);
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldsfld, field);
                                        pushData();
                                        il.Emit(OpCodes.Callvirt, ft.GetMethod(nameof(Func<int>.Invoke), [inputType]));
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldfld, metaField);
                                        if (optField != null)
                                            il.Emit(OpCodes.Ldsfld, optField);
                                        else
                                            il.Emit(OpCodes.Ldnull);
                                        MethodInfo baseMethod = isVoid ? restVoidTypeMethods[restIndex] : restTypeMethods[restIndex].MakeGenericMethod(returnType);
                                        il.Emit(OpCodes.Call, baseMethod);
                                    }
                                    break;
                                case HttpEndPointTypes.Post:
                                case HttpEndPointTypes.Put:
                                    {
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldstr, path);
                                        il.Emit(OpCodes.Ldarg_0);
                                        il.Emit(OpCodes.Ldfld, metaField);
                                        pushData();
                                        if (optField != null)
                                            il.Emit(OpCodes.Ldsfld, optField);
                                        else
                                            il.Emit(OpCodes.Ldnull);
                                        MethodInfo baseMethod = isVoid ? restVoidTypeMethods[restIndex].MakeGenericMethod(inputType) : restTypeMethods[restIndex].MakeGenericMethod(returnType, inputType);
                                        il.Emit(OpCodes.Call, baseMethod);
                                    }
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }


                        }
                        break;
                }
                il.Emit(OpCodes.Ret);
            }
            constructorIl.Emit(OpCodes.Ret);
            staticIl.Emit(OpCodes.Ret);
            var type = typeBuilder.CreateType();
            /*
            foreach (var x in nestedTypes)
                x.Value.Item3.CreateType();
            */


            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var prest = Expression.Parameter(typeof(RemoteConnection));
            var ptype = Expression.Parameter(typeof(Type));
            var newO = Expression.New(type.GetConstructor([typeof(RemoteConnection), typeof(Type)]), prest, ptype);
            return Expression.Lambda<Func<RemoteConnection, Type, T>>(newO, prest, ptype).Compile();
        }

        /// <summary>
        /// Creates a remote connection instance for a specific remote interface
        /// </summary>
        public static readonly Func<RemoteConnection, Type, T> Create = Build();
    }

}
