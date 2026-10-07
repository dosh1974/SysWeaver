using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SysWeaver.Docs;

namespace SysWeaver.Translation
{

    /// <summary>
    /// Non-generic access to the generated translator of a type, see <see cref="TypeTranslator.TryGetTranslator(Type, out ITypeTranslator)"/>.
    /// </summary>
    public interface ITypeTranslator
    {
        /// <summary>
        /// Translates an object (boxed, must be of the translator type) in place, null if the type have nothing to translate.
        /// Arguments: translator, target language ISO code, object, effort, cache retention.
        /// </summary>
        Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> ObjTranslator { get; }
        /// <summary>
        /// The expression tree of the strongly typed translator, null if the type have nothing to translate.
        /// </summary>
        LambdaExpression TransExp { get; }

        /// <summary>
        /// The strongly typed translator as a <see cref="Delegate"/>
        /// (a Func&lt;ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task&gt;), null if the type have nothing to translate.
        /// </summary>
        Delegate DelTranslator { get; }

        /// <summary>
        /// True if the source language of any translated member (including members of element types) is determined at runtime
        /// using <see cref="AutoTranslateDynLanguageAttribute"/>.
        /// </summary>
        bool HaveDynamicSourceLanguage { get; }

    }


    /// <summary>
    /// Determines if a type (recursively) contains any members that should be translated.
    /// </summary>
    static class TypeTranslationTest
    {

        static bool AddMember(HashSet<Type> seenTypes, MemberInfo mi, Type t, bool haveTrans = false)
        {
            if (t == typeof(String))
                return mi.GetCustomAttribute<AutoTranslateAttribute>(true) != null;
            return HaveTranslations(seenTypes, t, haveTrans);
        }

        /// <summary>
        /// Check if a type have any public instance <see cref="String"/> fields / properties marked with <see cref="AutoTranslateAttribute"/>,
        /// directly or through members of other types, array / <see cref="IEnumerable{T}"/> element types or dictionary value types.
        /// </summary>
        /// <param name="seenTypes">Types already visited (used to break cycles), visited types are added</param>
        /// <param name="t">The type to test</param>
        /// <param name="haveTrans">The result so far, returned as is for already seen, primitive, abstract, interface and enum types</param>
        /// <returns>True if any translatable member was found (or <paramref name="haveTrans"/> was true)</returns>
        public static bool HaveTranslations(HashSet<Type> seenTypes, Type t, bool haveTrans = false)
        {
            if (!seenTypes.Add(t))
                return haveTrans;
            if (t.IsPrimitive)
                return haveTrans;
            if (TypeTranslator.PrimTypes.Contains(t))
                return haveTrans;
            if (t.IsInterface || t.IsAbstract || t.IsEnum)
                return haveTrans;
            if (t.IsArray)
                return HaveTranslations(seenTypes, t.GetElementType(), haveTrans);
            if (t.IsGenericType)
            {
                var ga = t.GetGenericArguments();
                switch (ga.Length)
                {
                    case 1:
                        var et = ga[0];
                        if (typeof(IEnumerable<>).MakeGenericType(et).IsAssignableFrom(t))
                            return HaveTranslations(seenTypes, et, haveTrans);
                        break;
                    case 2:
                        var enumType = typeof(KeyValuePair<,>).MakeGenericType(ga);
                        if (typeof(IEnumerable<>).MakeGenericType(enumType).IsAssignableFrom(t))
                            return HaveTranslations(seenTypes, ga[1], haveTrans);
                        break;
                }
            }
            foreach (var m in t.GetMembers(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m is FieldInfo)
                {
                    haveTrans |= AddMember(seenTypes, m, (m as FieldInfo).FieldType, haveTrans);
                    continue;
                }
                if (m is PropertyInfo)
                {
                    haveTrans |= AddMember(seenTypes, m, (m as PropertyInfo).PropertyType, haveTrans);
                    continue;
                }
            }
            return haveTrans;
        }

    }


    /// <summary>
    /// Generates (once, in the static constructor) code that translates all members marked with <see cref="AutoTranslateAttribute"/> of <typeparamref name="T"/> in place.
    /// Use <see cref="TypeTranslator"/> for the public API.
    /// </summary>
    /// <typeparam name="T">The type to translate</typeparam>
    /// <remarks>
    /// Supports classes / structs (public instance fields and properties), arrays, <see cref="IEnumerable{T}"/> and dictionaries (values only).
    /// Value types are translated in place inside a <see cref="StrongBox{T}"/> (see <see cref="TranslateBox"/>), struct members (writable fields and properties,
    /// recursively for nested structs) and struct array elements are written back to their containing member / element once their translations completed.
    /// Struct values that can't be written back (read only fields, get only properties, nullable structs, elements of other collections and dictionary values,
    /// a struct passed by value to <see cref="Translate"/> or <see cref="TranslateObj"/>) are translated in a copy and the translations are lost.
    /// All translations of an object are started concurrently and awaited using <see cref="Task.WhenAll(IEnumerable{Task})"/>.
    /// Invalid attribute usage (ex: unknown context member names) throws while generating the code, i.e. a <see cref="TypeInitializationException"/>.
    /// Instances are stateless, they only expose the static members through <see cref="ITypeTranslator"/>.
    /// </remarks>
    public sealed class TypeTranslatorT<T> : ITypeTranslator
    {
        /// <summary>
        /// Translates an instance of <typeparamref name="T"/> in place, null if <typeparamref name="T"/> have nothing to translate.
        /// Arguments: translator, target language ISO code, instance (null is ignored), effort, cache retention.
        /// </summary>
        public static readonly Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> Translate;
        /// <summary>
        /// Same as <see cref="Translate"/> but takes a boxed instance, null if <typeparamref name="T"/> have nothing to translate.
        /// </summary>
        public static readonly Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> TranslateObj;
        /// <summary>
        /// Translates the instance of <typeparamref name="T"/> stored in a <see cref="StrongBox{T}"/> in place (i.e. <see cref="StrongBox{T}.Value"/> holds the
        /// translated value when the task completes, also for value types), null if <typeparamref name="T"/> have nothing to translate.
        /// Arguments: translator, target language ISO code, box (null is ignored), effort, cache retention.
        /// </summary>
        public static readonly Func<ITranslator, String, StrongBox<T>, TranslationEffort, TranslationCacheRetention, Task> TranslateBox;
        /// <summary>
        /// The expression tree that <see cref="Translate"/> was compiled from, null if <typeparamref name="T"/> have nothing to translate.
        /// </summary>
        public static readonly LambdaExpression Exp;
        static readonly bool InternalHaveDynamicSourceLanguage;
        static readonly Type[] ElementTypes;

        static bool? InternalHaveDynamicSourceLanguageSolved;


        static bool GetInternalHaveDynamicSourceLanguage()
        {
            if (InternalHaveDynamicSourceLanguage)
                return true;
            var et = ElementTypes;
            if (et == null)
                return false;
            var l = et.Length;
            for (int i = 0; i < l; ++ i)
            {
                if (TypeTranslator.TryGetTranslator(et[i], out var tr))
                    if (tr.HaveDynamicSourceLanguage)
                        return true;
            }
            return false;
        }

        //public static readonly IReadOnlyDictionary<String, Func<ITranslator, String, T, Task<String>>> GetMember;



        /// <summary>
        /// Returns <see cref="Translate"/>.
        /// </summary>
        public Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> Translator => Translate;
        /// <inheritdoc/>
        public Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> ObjTranslator => TranslateObj;
        /// <inheritdoc/>
        public LambdaExpression TransExp => Exp;

        /// <inheritdoc/>
        public Delegate DelTranslator => Translate;

        /// <inheritdoc/>
        /// <remarks>Lazily computed and cached (benign race, may be computed more than once).</remarks>
        public bool HaveDynamicSourceLanguage
        {
            get
            {
                var s = InternalHaveDynamicSourceLanguageSolved;
                if (s != null)
                    return s ?? false;
                var n = GetInternalHaveDynamicSourceLanguage();
                InternalHaveDynamicSourceLanguageSolved = n;
                return n;
            }
        }


        /// <summary>
        /// Adds code to <paramref name="prog"/> that starts the translation of a single string member (if not null) and adds the task to the task list.
        /// The context is built from the XML doc summary, <see cref="AutoTranslateContextAttribute"/> (formatted using member values) and the translator type,
        /// the source language from the attribute or the member named by <paramref name="fromLanguageMember"/>.
        /// </summary>
        /// <returns>The variable that holds the original value (must be added to the block variables)</returns>
        static ParameterExpression TranslateString(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, List<Expression> prog, Expression p, Expression src, IXmlDocInfo context, AutoTranslateAttribute attr, IEnumerable<AutoTranslateContextAttribute> contextAttributes, String memberName, TranslatorTypes trTypes, String fromLanguageMember)
        {
            var strParams = TypeTranslator.ParamString;
            var value = Expression.Variable(src.Type);
            prog.Add(Expression.Assign(value, src));
            var save = Expression.Lambda<Action<String>>(Expression.Assign(src, strParams), strParams);


            //  Get the context expression
            List<Expression> contexts = new List<Expression>();
            String prevConstant = null;
            void AddConstant(String s)
            {
                if (String.IsNullOrEmpty(s))
                    return;
                if (prevConstant == null)
                {
                    contexts.Add(Expression.Constant(s));
                    prevConstant = s;
                }
                else
                {
                    prevConstant = TypeTranslator.MergeContexts(prevConstant, s);
                    contexts[contexts.Count - 1] = Expression.Constant(prevConstant);
                }
            }

            if (context != null)
            {
                var sum = context.Summary;
                if (!String.IsNullOrEmpty(sum))
                {
                    if (!attr.NoContext)
                        AddConstant(String.Concat("The description is \"", sum, "\"."));
                }
            }

            var tempVal = TypeTranslator.TempVal;
            var fmt = TypeTranslator.StringFmt;
            var ns = TypeTranslator.NullString;
            var type = typeof(T);
            foreach (var c in contextAttributes)
            {
                var x = c.ContextText.Trim().TrimEnd('.');
                if (String.IsNullOrEmpty(x))
                    continue;
                x += '.';
                var argStart = x.IndexOf('{');
                if (argStart < 0)
                {
                    AddConstant(x);
                    continue;
                }
                var t = c.MemberNames;
                if (t == null)
                {
                    AddConstant(x);
                    continue;
                }
                var tl = t.Length;
                if (tl <= 0)
                {
                    AddConstant(x);
                    continue;
                }
#if DEBUG
                while (argStart >= 0)
                {
                    ++argStart;
                    var argEnd = x.IndexOf('}', argStart);
                    if (argEnd < 0)
                        throw new Exception(String.Concat("Mismatched '{' found in \"", x, "\", on member \"", memberName, "\" in type \"", type.FullName, '"'));
                    if (!int.TryParse(x.Substring(argStart, argEnd - argStart), out var ix))
                        throw new Exception(String.Concat("Invalid argument index found in \"", x, "\", on member \"", memberName, "\" in type \"", type.FullName, '"'));
                    if (ix < 0)
                        throw new Exception(String.Concat("Negative argument index found in \"", x, "\", on member \"", memberName, "\" in type \"", type.FullName, '"'));
                    if (ix >= tl)
                        throw new Exception(String  .Concat("Invalid argument index ", ix, ", found in \"", x, "\", on member \"", memberName, "\" in type \"", type.FullName, '"'));
                    argStart = x.IndexOf('{', argEnd + 1);
                }
#endif//DEBUG


                Expression[] reads = new Expression[tl];
                for (int i = 0; i < tl; ++i)
                {
                    var name = t[i];
                    Expression read = null;
                    var tt = type;
                    while (tt != typeof(Object))
                    {

                        var fi = tt.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                        if (fi != null)
                        {
                            read = Expression.Field(fi.IsStatic ? null : p, fi);
                        }
                        else
                        {
                            var pi = tt.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                            if (pi != null)
                            {
                                read = Expression.Property((pi.GetMethod?.IsStatic ?? false) ? null : p, pi);
                            }
                            else
                            {
                                var mi = tt.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy, Array.Empty<Type>());
                                if (TypeTranslator.IsValidContextMethod(mi))
                                {
                                    read = Expression.Call(mi.IsStatic ? null : p, mi);
                                }
                                else
                                {
                                    mi = tt.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy, [type]);
                                    if (TypeTranslator.IsValidContextMethod(mi))
                                        read = Expression.Call(mi, p);
                                }
                            }
                        }
                        if (read != null)
                            break;
                        tt = tt.BaseType;
                    }
                    if (read == null)
                        throw new Exception(String.Concat(
                        "The member named \"", name, "\" was not found in the type \"", type.FullName, "\" detected when processing a \"", nameof(AutoTranslateContextAttribute), "\" attribute"));
                    reads[i] = read;
                }
                List<ParameterExpression> dynP = new List<ParameterExpression>(tl);
                List<Expression> dynProg = new List<Expression>(tl + 1);
                for (int i = 0; i < tl; ++i)
                {
                    var r = reads[i];
                    var rt = r.Type;
                    if (!rt.IsClass)
                        continue;
                    var tp = Expression.Variable(rt);
                    dynP.Add(tp);
                    dynProg.Add(Expression.Assign(tp, r));
                    reads[i] = tp;
                }
                Expression dynE = Expression.Call(fmt, Expression.Constant(x), Expression.NewArrayInit(typeof(Object), reads));
                var dynPC = dynP.Count;
                while (dynPC > 0)
                {
                    --dynPC;
                    var dynV = dynP[dynPC];
                    dynE = Expression.Condition(Expression.Equal(dynV, Expression.Constant(null, dynV.Type)), ns, dynE);
                }
                if (dynP.Count > 0)
                {
                    dynProg.Add(dynE);
                    dynE = Expression.Block(dynP, dynProg);
                }
                contexts.Add(dynE);
                prevConstant = null;
            }


            var typeContext = TypeTranslator.TypeContexts[(int)trTypes];
            if (!String.IsNullOrEmpty(typeContext))
                AddConstant(typeContext);

            Expression con = ns;
            var count = contexts.Count;
            switch (count)
            {
                case 0:
                    break;
                case 1:
                    con = contexts[0];
                    break;
                default:
                    con = Expression.Call(TypeTranslator.MergeContextsMethod, Expression.NewArrayInit(typeof(String), contexts));
                    break;
            }
            //  
            var from = attr.FromLanguage;
            if (String.IsNullOrEmpty(from))
                from = "en";
            var fromExpC = Expression.Constant(from);
            Expression fromExp = fromExpC;
            if (fromLanguageMember != null)
            {
                var lmf = type.GetField(fromLanguageMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if ((lmf != null) && (lmf.FieldType == typeof(String)))
                {
                    fromExp = Expression.Field(lmf.IsStatic ? null : p, lmf);
                }
                else
                {
                    var lmp = type.GetProperty(fromLanguageMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    if ((lmp != null) && (lmp.PropertyType == typeof(String)) && (lmp.GetMethod != null) && lmp.CanRead)
                    {
                        fromExp = Expression.Property(lmp.GetMethod.IsStatic ? null : p, lmp);
                    }
                    else
                    {
                        var lmm = type.GetMethod(fromLanguageMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, Array.Empty<Type>());
                        if ((lmm != null) && (lmm.ReturnType == typeof(String)))
                        {
                            fromExp = Expression.Call(lmm.IsStatic ? null : p, lmm);
                        }
                        else
                        {
                            var lmm2 = type.GetMethod(fromLanguageMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, [typeof(String)]);
                            if ((lmm2 != null) && (lmm2.ReturnType == typeof(String)))
                            {
                                fromExp = Expression.Call(lmm2.IsStatic ? null : p, lmm2, Expression.Constant(memberName));
                            }
                            else
                            {
                                throw new Exception(String.Concat('"', type.FullName, "\" doesn't contain a member named \"", fromLanguageMember, "\" as declared using the \"", nameof(AutoTranslateDynLanguageAttribute), "\" on member \"", memberName, '"'));
                            }
                        }
                    }
                }
                fromExp = Expression.Coalesce(fromExp, fromExpC);
                haveDynamicSourceLanguage |= true;
            }
            var fnc = Expression.Call(
                TypeTranslator.TranslateOneMethod,
                TypeTranslator.ParamTranslator,
                fromExp,
                TypeTranslator.ParamTo,
                value,
                con,
                save,
                TypeTranslator.ParamEffort, TypeTranslator.ParamRetention
                );
            prog.Add(Expression.IfThen(Expression.NotEqual(value, ns), Expression.Call(TypeTranslator.VarTaskList, TypeTranslator.ListAddMethod, fnc)));
/*            var mc = Expression.Call(
                TypeTranslator.TranslateMemberMethod,
                transExp,
                fromExp,
                toExp,
                src,
                con);
            var mcl = Expression.Lambda<Func<ITranslator, String, T, Task<String>>>(mc, transExp, toExp, p);
            members.Add(memberName, mcl.Compile());
*/            return value;
        }


        /// <summary>
        /// Builds code that translates all public instance fields and properties of an object, null if there is nothing to translate.
        /// </summary>
        /// <param name="haveDynamicSourceLanguage">Set to true if any member uses a dynamic source language</param>
        /// <param name="members">Receives the translation functions of the string members</param>
        /// <param name="t">The type of the instance</param>
        /// <param name="p">The instance, for value types this is the <see cref="StrongBox{T}.Value"/> field of a box so that members are written in place.</param>
        static Expression BuildObject(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, Type t, Expression p)
        {
            var taskList = TypeTranslator.VarTaskList;
            List<Expression> prog = new()
            {
                Expression.Assign(taskList, Expression.New(typeof(List<Task>)))
            };
            List<ParameterExpression> progP = new()
            {
                taskList
            };
            foreach (var m in t.GetMembers(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m is FieldInfo)
                {
                    var mi = m as FieldInfo;
                    var et = mi.FieldType;
                    var src = Expression.Field(p, mi);
                    AddMember(ref haveDynamicSourceLanguage, members, et, m, progP, prog, p, src);
                    continue;
                }
                if (m is PropertyInfo)
                {
                    var mi = m as PropertyInfo;
                    var et = mi.PropertyType;
                    try
                    {
                        var src = Expression.Property(p, mi);
                        AddMember(ref haveDynamicSourceLanguage, members, et, m, progP, prog, p, src);
                    }
                    catch// (Exception ex)
                    {
                        var src = Expression.Property(p, mi);
                        AddMember(ref haveDynamicSourceLanguage, members, et, m, progP, prog, p, src);
                    }
                    continue;
                }
            }

            if (prog.Count <= 1)
                return null;

            prog.Add(Expression.Call(TypeTranslator.TaskWhenAllMethod, taskList));
            Expression pr = Expression.Block(progP, prog);
            return pr;
        }

        /// <summary>
        /// Check if a member can be assigned (used to write back translated struct values).
        /// </summary>
        static bool IsWritable(MemberInfo mi)
        {
            if (mi is FieldInfo fi)
                return !(fi.IsInitOnly || fi.IsLiteral);
            if (mi is PropertyInfo pi)
                return pi.CanWrite && (pi.GetIndexParameters().Length == 0);
            return false;
        }

        static void AddMember(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, Type et, MemberInfo mi, List<ParameterExpression> progP, List<Expression> prog, Expression p, Expression src)
        {
            if (et == typeof(String))
            {
                var attr = mi.GetCustomAttribute<AutoTranslateAttribute>(true);
                if (attr == null)
                    return;
                progP.Add(TranslateString(ref haveDynamicSourceLanguage, members, prog, p, src, attr.NoContext ? null : mi.XmlDoc(), attr, mi.GetCustomAttributes<AutoTranslateContextAttribute>(true).OrderBy(x => x.Order), mi.Name, mi.GetCustomAttribute<AutoTranslateTypeAttribute>(true)?.Type ?? TranslatorTypes.Text, mi.GetCustomAttribute<AutoTranslateDynLanguageAttribute>(true)?.MemberName));
                return;
            }
            var seen = new HashSet<Type>();
            if (!TypeTranslationTest.HaveTranslations(seen, et))
                return;
            if (et.IsValueType && (Nullable.GetUnderlyingType(et) == null) && IsWritable(mi))
            {
                //  Struct member: translate a boxed copy and write it back to the member when done
                if (!seen.Contains(mi.DeclaringType))
                {
                    //  Generate the struct translator now (as for other members), so that invalid attribute usage is detected here
                    if (!TypeTranslator.TryGetTranslator(et, out var _))
                        throw new Exception("Internal error!");
                }
                var v = Expression.Parameter(et, "v");
                var save = Expression.Lambda(typeof(Action<>).MakeGenericType(et), Expression.Assign(src, v), v);
                var transStruct = Expression.Call(
                    TypeTranslator.TranslateStructMethod.MakeGenericMethod(et),
                    Expression.Call(TypeTranslator.GetBoxFuncMethod.MakeGenericMethod(et)),
                    TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, src, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention,
                    save);
                prog.Add(Expression.Call(TypeTranslator.VarTaskList, TypeTranslator.ListAddMethod, transStruct));
                return;
            }
            Expression trElement;
            if (seen.Contains(mi.DeclaringType))
            {
                trElement = Expression.Call(TypeTranslator.GetFuncMethod.MakeGenericMethod(et));
            }
            else
            {
                if (!TypeTranslator.TryGetTranslator(et, out var vt))
                    throw new Exception("Internal error!");
                trElement = Expression.Constant(vt.DelTranslator, typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), et, typeof(TranslationEffort), typeof(TranslationCacheRetention), typeof(Task)));
            }

            var transOne = Expression.Invoke(trElement, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, src, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var addOne = Expression.Call(
                                    TypeTranslator.VarTaskList,
                                    TypeTranslator.ListAddMethod,
                                    transOne
                                );
            prog.Add(addOne);
        }

        /// <summary>
        /// Builds code that translates all elements of a (single dimensional) array, null if the element type have nothing to translate.
        /// </summary>
        static Expression BuildArray(out Type et, Type t, Expression p)
        {
            et = t.GetElementType();
            if (!TypeTranslationTest.HaveTranslations(new HashSet<Type>(), et))
                return null;
            //  Struct elements are translated in a boxed copy that is written back to the array element when done
            var byElement = et.IsValueType && (Nullable.GetUnderlyingType(et) == null);
            var elFunc = byElement
                ? Expression.Variable(typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), typeof(StrongBox<>).MakeGenericType(et), typeof(TranslationEffort), typeof(TranslationCacheRetention), typeof(Task)), "fn")
                : Expression.Variable(typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), et, typeof(TranslationEffort), typeof(TranslationCacheRetention), typeof(Task)), "fn");
            var len = TypeTranslator.VarLen;
            var taskList = TypeTranslator.VarTaskArray;
            List<Expression> prog = new()
            {
                Expression.Assign(taskList, Expression.NewArrayBounds(typeof(Task), len)),
                Expression.Assign(elFunc, Expression.Call((byElement ? TypeTranslator.GetBoxFuncMethod : TypeTranslator.GetFuncMethod).MakeGenericMethod(et))),
            };
            var subOne = Expression.PreDecrementAssign(len);
            Expression transOne;
            if (byElement)
            {
                transOne = Expression.Call(TypeTranslator.TranslateArrayElementMethod.MakeGenericMethod(et), elFunc, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, p, len, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            }
            else
            {
                var getOne = Expression.ArrayIndex(p, len);
                transOne = Expression.Invoke(elFunc, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, getOne, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            }
            var addOne = Expression.Assign(Expression.ArrayAccess(taskList, len), transOne);
            prog.Add(
                Expression.Loop(
                        Expression.IfThenElse(
                            Expression.GreaterThan(len, TypeTranslator.ConstIntZero),
                                Expression.Block(subOne, addOne),
                                Expression.Break(TypeTranslator.LabelTarget)
                        ),
                        TypeTranslator.LabelTarget
                )
            );
            prog.Add(Expression.Call(TypeTranslator.TaskWhenAllMethod, taskList));
            var pr = Expression.Block([len, elFunc], [
                Expression.Assign(len, Expression.Property(p, nameof(Array.Length))),
                Expression.Condition(
                    Expression.GreaterThan(len, TypeTranslator.ConstIntZero),
                    Expression.Block([taskList], prog),
                    TypeTranslator.ConstCompletedTask
                    )
                ]);
            return pr;
        }

        /// <summary>
        /// Builds code that enumerates a sequence and translates every element (or the value selected by <paramref name="getVal"/>),
        /// null if the element type have nothing to translate.
        /// </summary>
        static Expression BuildEnumerable(Type t, Type enumerableType, Type et, Expression p, Func<Expression, Expression> getVal)
        {
            if (!TypeTranslator.TryGetTranslator(et, out var vt))
                return null;
            var elFunc = Expression.Variable(typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), et, typeof(TranslationEffort), typeof(TranslationCacheRetention), typeof(Task)), "fn");
            var taskList = TypeTranslator.VarTaskList;
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(enumerableType);
            var enumMethod = typeof(IEnumerable<>).MakeGenericType(enumerableType).GetMethod(nameof(IEnumerable<int>.GetEnumerator), BindingFlags.Public | BindingFlags.Instance, Array.Empty<Type>());
            var enumerator = Expression.Variable(enumeratorType, "e");
            List<Expression> prog = new()
            {
                Expression.Assign(taskList, Expression.New(typeof(List<Task>))),
                Expression.Assign(enumerator, Expression.Call(p, enumMethod)),
                Expression.Assign(elFunc, Expression.Call(TypeTranslator.GetFuncMethod.MakeGenericMethod(et))),
            };
            var getOne = getVal(Expression.Property(enumerator, nameof(IEnumerator<int>.Current)));
            var transOne = Expression.Invoke(elFunc, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, getOne, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var addOne = Expression.Call(
                                    taskList,
                                    TypeTranslator.ListAddMethod,
                                    transOne
                                );
            var moveCheck = Expression.Call(enumerator, TypeTranslator.MoveNextMethod);
            prog.Add(
                Expression.Loop(
                        Expression.IfThenElse(
                            moveCheck,
                            addOne,
                            Expression.Break(TypeTranslator.LabelTarget)
                        ),
                        TypeTranslator.LabelTarget
                )
            );
            prog.Add(Expression.Call(TypeTranslator.CondDisposeMethod, enumerator));
            prog.Add(Expression.Call(TypeTranslator.TaskWhenAllMethod, taskList));
            Expression pr = Expression.Block([taskList, enumerator, elFunc], prog);
            return pr;
        }

        static TypeTranslatorT()
        {
            var t = typeof(T);
            if (t.IsPrimitive)
                return;
            if (TypeTranslator.PrimTypes.Contains(t))
                return;
            if (t.IsInterface || t.IsAbstract || t.IsEnum)
                return;
            HashSet<Type> seen = new();
            if (!TypeTranslationTest.HaveTranslations(seen, t))
                return;

            Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members = new (StringComparer.Ordinal);


            bool haveDynamicSourceLanguage = false;

            var p = Expression.Parameter(t, "p");
            //  Value types are translated inside a box, so that all writes (strings and nested structs) end up in the box
            ParameterExpression box = t.IsValueType ? Expression.Parameter(typeof(StrongBox<T>), "box") : null;
            Expression inst = box == null ? p : Expression.Field(box, nameof(StrongBox<T>.Value));
            Expression program = null;
            if (t.IsArray)
            {
                program = BuildArray(out var et, t, inst);
                ElementTypes = [et];
            }else
            {
                if (t.IsGenericType)
                {
                    var ga = t.GetGenericArguments();
                    switch (ga.Length)
                    {
                        case 1:
                            var et = ga[0];
                            if (typeof(IEnumerable<>).MakeGenericType(et).IsAssignableFrom(t))
                            {
                                program = BuildEnumerable(t, et, et, inst, e => e);
                                if (program == null)
                                    return;
                                ElementTypes = [et];
                            }
                            break;
                        case 2:
                            var enumType = typeof(KeyValuePair<,>).MakeGenericType(ga);
                            if (typeof(IEnumerable<>).MakeGenericType(enumType).IsAssignableFrom(t))
                            {
                                program = BuildEnumerable(t, enumType, ga[1], inst, e => Expression.Property(e, nameof(KeyValuePair<int,int>.Value)));
                                if (program == null)
                                    return;
                                ElementTypes = ga;
                            }
                            break;
                    }


                }
                if (program == null)
                    program = BuildObject(ref haveDynamicSourceLanguage, members, t, inst);
            }
            if (program == null)
                return;
            if (box != null)
            {
                //  Value type: the box translator writes in place, the by value translator translates a boxed copy (translations are lost)
                program = Expression.Condition(Expression.Equal(box, Expression.Constant(null, box.Type)), TypeTranslator.ConstCompletedTask, program);
                var boxExp = Expression.Lambda<Func<ITranslator, String, StrongBox<T>, TranslationEffort, TranslationCacheRetention, Task>>(program, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, box, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
                TranslateBox = boxExp.Compile();
                program = Expression.Invoke(boxExp, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, Expression.New(typeof(StrongBox<T>).GetConstructor([t]), p), TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            }
            else
            {
                program = Expression.Condition(Expression.Equal(p, Expression.Constant(null, t)), TypeTranslator.ConstCompletedTask, program);
            }
            var transExp = Expression.Lambda<Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task>>(program, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, p, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var translate = transExp.Compile();
            Translate = translate;
            Exp = transExp;
            if (box == null)
                TranslateBox = (tr, to, b, effort, retention) => b == null ? Task.CompletedTask : translate(tr, to, b.Value, effort, retention);

            var o = TypeTranslator.ParamObj;
            var prExp = Expression.Invoke(transExp, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, Expression.Convert(o, t), TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var translateExpObj = Expression.Lambda<Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task>>(prExp, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, o, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var translateObj = translateExpObj.Compile();
            TranslateObj = translateObj;
            InternalHaveDynamicSourceLanguage = haveDynamicSourceLanguage;
/*            if (members.Count > 0)
                GetMember = members.Freeze();
*/
        }


    }


}
