using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{

    public sealed class TypeHandler<T>
    {
        public delegate void HandleField(IInspectorImplementation i, ref T value);
        public delegate void HandleProp(IInspectorImplementation i, ref T value, SetProp<T> onSet);

        public delegate void DescribeDelegate(IInspectorImplementation i, ref T value, int version);
        public delegate T CreateDelegate(IInspectorImplementation i, int version, bool isLatestVersion);

        public readonly bool Nullable;
        public readonly int LatestVersion;
        public readonly HandleField Field;
        public readonly HandleProp Prop;
        public readonly int TypeIndex;


        public readonly DescribeDelegate Describe;
        public readonly CreateDelegate Create;


        bool ParamatersMatch(ParameterInfo[] par, Type[] types)
        {
            if (par.Length != types.Length)
                return false;
            for (int i = 0; i < par.Length; ++ i)
            {
                if (par[i].ParameterType != types[i])
                    return false;
                if (par[i].IsOut)
                    return false;
            }
            return true;
        }

        static void EmptyDescribe(IInspectorImplementation i, ref T value, int version)
        {
        }

        internal TypeHandler(Type type, bool nullable, int typeIndex)
        {
            try
            {

                var typeInfo = type.GetTypeInfo();
                var callType = typeof(T);
                var callTypeInfo = callType.GetTypeInfo();
                var refCallType = callType.MakeByRefType();
                bool isIncomplete = type.IsInterface || type.IsAbstract;
                bool sameType = callType == type;

                int version = 0;
                TypeIndex = typeIndex;
                if (HelpersTypeHandler.DescribableTypeInfo.IsAssignableFrom(typeInfo))
                {
                    version = 1;
                    var versionAttribute = typeInfo.GetCustomAttributes(typeof(DescVersionAttribute), true).FirstOrDefault() as DescVersionAttribute;
                    if (versionAttribute != null)
                        version = versionAttribute.Version;
                }
                HandleField field = null;
                HandleProp prop = null;
                DescribeDelegate describe = null;
                CreateDelegate create = null;

                var inspectorHandlerType = HelpersTypeHandler.inspectorHandlerType;
                var inspectorParameter = HelpersTypeHandler.inspectorParameter;
                var thisConst = Expression.Constant(this);
                var falseConstant = HelpersTypeHandler.falseConstant;
                var valueParameter = Expression.Parameter(refCallType, "value");
                var valueTypeParameter = Expression.Convert(valueParameter, type);
                var setActionParameter = Expression.Parameter(typeof(SetProp<>).MakeGenericType(callType), "set");
                var versionParameter = HelpersTypeHandler.versionParameter;
                var isLatestVersionParameter = HelpersTypeHandler.isLatestVersionParameter;
                var currentVersionConstant = Expression.Constant(version);
                var isCurrentVersion = Expression.Equal(currentVersionConstant, versionParameter);
                for (; ; )
                {
                    MethodInfo fieldMethod = InspectorInfo<IInspector>.GetRegFieldMethod(callType, false);
                    if (fieldMethod != null)
                    {
                        field = Expression.Lambda<HandleField>(Expression.Call(inspectorParameter, fieldMethod, valueParameter), inspectorParameter, valueParameter).Compile();
                        prop = Expression.Lambda<HandleProp>(Expression.Call(inspectorParameter, InspectorInfo<IInspector>.GetRegPropMethod(callType), valueParameter, setActionParameter), inspectorParameter, valueParameter, setActionParameter).Compile();
                        break;
                    }
                    //  Inspector method
                    if (sameType && (!isIncomplete))
                    {
                        field = Expression.Lambda<HandleField>(Expression.Call(inspectorParameter, (callTypeInfo.IsValueType ? (nullable ? InspectorImplementationInfo<IInspectorImplementation>.Field_NullableValue : InspectorImplementationInfo<IInspectorImplementation>.Field_Value) : InspectorImplementationInfo<IInspectorImplementation>.Field_Object).MakeGenericMethod(callType), thisConst, valueParameter), inspectorParameter, valueParameter).Compile();
                        prop = Expression.Lambda<HandleProp>(Expression.Call(inspectorParameter, (callTypeInfo.IsValueType ? (nullable ? InspectorImplementationInfo<IInspectorImplementation>.Prop_NullableValue : InspectorImplementationInfo<IInspectorImplementation>.Prop_Value) : InspectorImplementationInfo<IInspectorImplementation>.Prop_Object).MakeGenericMethod(callType), thisConst, valueParameter, setActionParameter), inspectorParameter, valueParameter, setActionParameter).Compile();
                    }
                    else
                    {
                        field = Expression.Lambda<HandleField>(Expression.Call(inspectorParameter, InspectorImplementationInfo<IInspectorImplementation>.Field_TypedObject.MakeGenericMethod(callType, type), thisConst, valueParameter), inspectorParameter, valueParameter).Compile();
                        prop = Expression.Lambda<HandleProp>(Expression.Call(inspectorParameter, InspectorImplementationInfo<IInspectorImplementation>.Prop_TypedObject.MakeGenericMethod(callType, type), thisConst, valueParameter, setActionParameter), inspectorParameter, valueParameter, setActionParameter).Compile();

                        var thct = typeof(TypeHandlerCache<>).MakeGenericType(type);
                        var met = thct.GetMethod(nameof(TypeHandlerCache<int>.GetTypeHandler), BindingFlags.Static | BindingFlags.Public);
                        var desc = met.Invoke(null, []);
                        if (desc != null)
                        {
                            var tht = typeof(TypeHandler<>).MakeGenericType(type);
                            var cv = (int)tht.GetField(nameof(LatestVersion), BindingFlags.Instance | BindingFlags.Public).GetValue(desc);
                            var descDel = tht.GetField(nameof(Describe), BindingFlags.Instance | BindingFlags.Public).GetValue(desc);
                            var fieldDel = tht.GetField(nameof(Field), BindingFlags.Instance | BindingFlags.Public).GetValue(desc);

                            var temp = Expression.Parameter(type, "temp");
                            Expression descProg = Expression.Block(new ParameterExpression[]
                                {   temp, },
                                    Expression.Assign(temp, Expression.Convert(valueParameter, type)),
                                    descDel != null ? Expression.Invoke(Expression.Constant(descDel), inspectorParameter, temp, Expression.Constant(cv)) : Expression.Invoke(Expression.Constant(fieldDel), inspectorParameter, temp),
                                    Expression.Assign(valueParameter, Expression.Convert(temp, callType))
                                );
                            var pdel = Expression.Lambda<DescribeDelegate>(descProg, inspectorParameter, valueParameter, versionParameter);
                            describe = pdel.Compile();

                            var createDel = tht.GetField(nameof(Create), BindingFlags.Instance | BindingFlags.Public).GetValue(desc);

                            Expression createProg;
                            if (createDel != null)
                            {
                                createProg = Expression.Convert(Expression.Invoke(Expression.Constant(createDel), inspectorParameter, versionParameter, isLatestVersionParameter), callType);
                            }
                            else
                            {
                                createProg = Expression.Block(new ParameterExpression[]
                                {   temp, },
                                    Expression.Assign(temp, Expression.Default(temp.Type)),
                                    Expression.Invoke(Expression.Constant(fieldDel), inspectorParameter, temp),
                                    Expression.Convert(temp, callType)
                                );
                            }
                            var cdel = Expression.Lambda<CreateDelegate>(createProg, inspectorParameter, versionParameter, isLatestVersionParameter);
                            create = cdel.Compile();
                        }
                        break;
                    }
//                    if (typeInfo.IsInterface)
//                        break;
                    //  Describe method
                    for (; ; )
                    {
                        if (HelpersTypeHandler.DescribableTypeInfo.IsAssignableFrom(typeInfo))
                        {
                            describe = Expression.Lambda<DescribeDelegate>(
                                Expression.Condition(isCurrentVersion,
                                    Expression.Call(sameType ? (Expression)valueParameter : valueTypeParameter, HelpersTypeHandler.GetDescribe(typeInfo), inspectorParameter),
                                    Expression.Call(sameType ? (Expression)valueParameter : valueTypeParameter, HelpersTypeHandler.GetLegacyDescribe(typeInfo), inspectorParameter, versionParameter)), inspectorParameter, valueParameter, versionParameter).Compile();
                            break;
                        }
                        if (type.IsArray)
                        {
                            //                        var arrayDesc = DescribeArrayMethod.MakeGenericMethod(type.GetElementType());
                            if (sameType)
                                describe = ArrayTypeHandlerCreator<T>.MakeDescriptor(type, inspectorParameter, valueParameter, versionParameter);
                            //                            describe = Expression.Lambda<DescribeDelegate>(Expression.Call(arrayDesc, inspectorParameter, valueParameter, versionParameter), inspectorParameter, valueParameter, versionParameter).Compile();
                            else
                            {
                                describe = ArrayTypeHandlerCreator<T>.MakeWrappedDescriptor(callType, type, inspectorParameter, valueParameter, versionParameter);
                                /*                            var tempA = Expression.Variable(callType);
                                                            describe = Expression.Lambda<DescribeDelegate>(
                                                                Expression.Block(
                                                                    Expression.Assign(tempA, valueTypeParameter),
                                                                    Expression.Call(arrayDesc, inspectorParameter, tempA, versionParameter),
                                                                    Expression.Assign(valueParameter, Expression.Convert(tempA, callType))
                                                                    ), inspectorParameter, valueParameter, versionParameter).Compile();
                                */
                            }

                            version = 1;
                            currentVersionConstant = Expression.Constant(version);
                            isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                            break;
                        }
                        if (typeInfo.IsEnum)
                        {
                            if (sameType)
                                describe = EnumTypeHandlerCreator<T>.MakeDescriptor(type, inspectorParameter, valueParameter, versionParameter);
                            else
                            {
                                describe = EnumTypeHandlerCreator<T>.MakeWrappedDescriptor(callType, type, inspectorParameter, valueParameter, versionParameter);
                            }
                            version = 1;
                            currentVersionConstant = Expression.Constant(version);
                            isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                            break;
                        }
                        //  KeyValuePair<K, V>
                        var interfaces = typeInfo.ImplementedInterfaces;
                        //  IList<T>
                        var ilistType = interfaces.FirstOrDefault(x => x.GetTypeInfo().IsGenericType && (x.GetGenericTypeDefinition() == typeof(IList<>)));
                        if (ilistType != null)
                        {
                            var elementType = ilistType.GetTypeInfo().GenericTypeArguments[0];
                            var elementDesc = StaticTypeHandler.SimpleRegField.MakeGenericMethod(elementType).Invoke(null, null);
                            var eConst = Expression.Constant(elementDesc);
                            var handler = typeof(GenericCollectionTypeHandlers<>).MakeGenericType(callType).GetTypeInfo().GetDeclaredMethod("Describe_IList").MakeGenericMethod(elementType);
                            describe = Expression.Lambda<DescribeDelegate>(Expression.Call(handler, inspectorParameter, valueParameter, eConst), inspectorParameter, valueParameter, versionParameter).Compile();
                            version = 1;
                            currentVersionConstant = Expression.Constant(version);
                            isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                            break;
                        }
                        //  ICollection<T>
                        ilistType = interfaces.FirstOrDefault(x => x.GetTypeInfo().IsGenericType && (x.GetGenericTypeDefinition() == typeof(ICollection<>)));
                        if (ilistType != null)
                        {
                            var elementType = ilistType.GetTypeInfo().GenericTypeArguments[0];
                            var elementDesc = StaticTypeHandler.SimpleRegField.MakeGenericMethod(elementType).Invoke(null, null);
                            var eConst = Expression.Constant(elementDesc);
                            var handler = typeof(GenericCollectionTypeHandlers<>).MakeGenericType(callType).GetTypeInfo().GetDeclaredMethod("Describe_ICollection").MakeGenericMethod(elementType);
                            describe = Expression.Lambda<DescribeDelegate>(Expression.Call(handler, inspectorParameter, valueParameter, eConst), inspectorParameter, valueParameter, versionParameter).Compile();
                            version = 1;
                            currentVersionConstant = Expression.Constant(version);
                            isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                            break;
                        }
                        //  IList
                        if (typeof(IList).GetTypeInfo().IsAssignableFrom(typeInfo))
                        {
                            var handler = typeof(CollectionTypeHandlers).GetTypeInfo().GetDeclaredMethod("Describe_List");
                            describe = Expression.Lambda<DescribeDelegate>(Expression.Call(handler, inspectorParameter, valueParameter), inspectorParameter, valueParameter, versionParameter).Compile();
                            version = 1;
                            currentVersionConstant = Expression.Constant(version);
                            isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                            break;
                        }
#if SupportSerializable
                    //  ISerializable
                    if (typeof(System.Runtime.Serialization.ISerializable).IsAssignableFrom(type))
                    {
                        throw new NotImplementedException();
                    }
                    //  Serializable
                    var ser = type.GetCustomAttributes(typeof(SerializableAttribute), false);
                    if ((ser != null) && (ser.Length > 0))
                    {
#endif//SupportSerializable
                        //  Base
                        var fields = typeInfo.AllFields().Where(fi => !fi.IsStatic);
                        List<Expression> regs = new List<Expression>();
                        /*
                        var typeBase = typeInfo.BaseType;
                        if ((typeBase != null) && (typeBase != typeof(Object)) && (typeBase != typeof(ValueType)))
                        {
                            var th = typeof(TypeHandlerCache<>).MakeGenericType(typeBase).GetTypeInfo().GetDeclaredMethod(nameof(TypeHandlerCache<int>.GetTypeHandler)).Invoke(null, null);
                            var baseTh = Expression.Constant(th);
                            var fi = Expression.Field(baseTh, typeof(TypeHandler<>).MakeGenericType(typeBase).GetTypeInfo().GetDeclaredField(nameof(TypeHandler<int>.Field)));
                            regs.Add(Expression.Invoke(fi, inspectorParameter, valueParameter));
                        }
                        */
                        foreach (var f in fields)
                        {
#if SupportSerializable
                            if (f.IsNotSerialized)
                                continue;
#endif//SupportSerializable
                            if (f.Attributes.HasFlag(FieldAttributes.InitOnly))
                                continue;
                            regs.Add(StaticTypeHandler.GetRegFieldExpression(f.FieldType, inspectorParameter, Expression.Field(valueTypeParameter, f)));
                        }
                        if (regs.Count == 0)
                            describe = EmptyDescribe;
                        else
                            describe = Expression.Lambda<DescribeDelegate>(Expression.Block(regs), inspectorParameter, valueParameter, versionParameter).Compile();
                        version = 1;
                        currentVersionConstant = Expression.Constant(version);
                        isCurrentVersion = Expression.Equal(falseConstant, falseConstant);
                        break;
#if SupportSerializable
                    }
                    if (typeInfo.IsInterface)
                        break;
                    throw new Exception("Don't know how to describe the type \"" + type.FullName + "\" as \"" + callType.FullName + "\"");
#endif//SupportSerializable
                    }
                    //  Create method
                    if (isIncomplete)
                        break;
                    for (; ; )
                    {
                        if (type.IsArray)
                        {
                            create = ArrayTypeHandlerCreator<T>.MakeCreator(type, inspectorParameter, versionParameter, isLatestVersionParameter);
                            break;
                        }
                        if (typeInfo.IsEnum)
                        {
                            create = EnumTypeHandlerCreator<T>.MakeCreator(type, inspectorParameter, versionParameter, isLatestVersionParameter);
                            break;
                        }
                        var constructors = typeInfo.DeclaredConstructors.Where(ci => !ci.IsStatic);
                        ConstructorInfo empty = null;
                        foreach (var ci in constructors)
                        {
                            var cip = ci.GetParameters();
                            if (ParamatersMatch(cip, StaticTypeHandler.ConstructDescriptor))
                            {
                                if (sameType)
                                    create = Expression.Lambda<CreateDelegate>(Expression.New(ci, inspectorParameter, versionParameter), inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                                else
                                    create = Expression.Lambda<CreateDelegate>(Expression.Convert(Expression.New(ci, inspectorParameter, versionParameter), callType), inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                            }
                            if (ParamatersMatch(cip, StaticTypeHandler.ConstructDescriptor2))
                            {
                                if (sameType)
                                    create = Expression.Lambda<CreateDelegate>(Expression.New(ci, inspectorParameter, versionParameter, isLatestVersionParameter), inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                                else
                                    create = Expression.Lambda<CreateDelegate>(Expression.Convert(Expression.New(ci, inspectorParameter, versionParameter, isLatestVersionParameter), callType), inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                                break;
                            }



                            if (ParamatersMatch(cip, StaticTypeHandler.Empty))
                                empty = ci;
                        }
                        if (create != null)
                            break;
                        if ((empty != null) || (typeInfo.IsValueType && sameType))
                        {
                            var newO = Expression.Variable(type);
                            Expression callCurrent = null;
                            Expression callVersioned = null;
                            //  Describe method
                            if (HelpersTypeHandler.DescribableTypeInfo.IsAssignableFrom(typeInfo))
                            {
                                callCurrent = Expression.Call(newO, HelpersTypeHandler.GetDescribe(typeInfo), inspectorParameter);
                                callVersioned = Expression.Call(newO, HelpersTypeHandler.GetLegacyDescribe(typeInfo), inspectorParameter, versionParameter);
                            }
                            else
                            {
                                callCurrent = Expression.Call(Expression.Constant(describe), describe.GetType().GetTypeInfo().GetDeclaredMethod("Invoke"), inspectorParameter, newO, versionParameter);
                            }
                            bool assignable = sameType || callType.GetTypeInfo().IsAssignableFrom(typeInfo);
                            if (typeInfo.IsValueType)
                            {
                                create = Expression.Lambda<CreateDelegate>(
                                    Expression.Block(new ParameterExpression[] { newO },
                                        Expression.Assign(newO, Expression.New(type)),
                                        callVersioned == null ? callCurrent : Expression.Condition(isLatestVersionParameter, callCurrent, callVersioned),
                                        assignable ? (Expression)newO : Expression.Convert(newO, callType)), inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                                break;
                            }
                            var createExp = Expression.Block(new ParameterExpression[] { newO },
                                    Expression.Assign(newO, Expression.New(empty)),
                                    Expression.Call(inspectorParameter, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(callType), newO, falseConstant),
                                    callVersioned == null ? callCurrent : Expression.Condition(isLatestVersionParameter, callCurrent, callVersioned),
                                    assignable ? (Expression)newO : Expression.Convert(newO, callType));
                            create = Expression.Lambda<CreateDelegate>(createExp, inspectorParameter, versionParameter, isLatestVersionParameter).Compile();
                            break;
                        }
#if SupportSerializable
                    //  ISerializable
                    if (typeof(System.Runtime.Serialization.ISerializable).IsAssignableFrom(type))
                    {
                        throw new NotImplementedException();
                    }
                    //  Serializable
                    var ser = type.GetCustomAttributes(typeof(SerializableAttribute), false);
                    if ((ser != null) && (ser.Length > 0))
                    {
                        create = (insp, ver, isLat) => (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                        break;
                    }
                    throw new Exception("Don't know how to create the type \"" + type.FullName + "\" as \"" + callType.FullName + "\"");
#else//SupportSerializable
                        create = (insp, ver, isLat) => (T)Activator.CreateInstance(type);
                        break;
#endif//SupportSerializable
                    }
                    //  All done
                    break;
                }
                Nullable = nullable;
                LatestVersion = version;
                Field = field;
                Prop = prop;

                Describe = describe;
                Create = create;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create type handler for type \"" + typeof(T).FullName + "\"", ex);
            }
        }


    }

}

