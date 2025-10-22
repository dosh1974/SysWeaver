using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.Docs;

namespace SysWeaver.Translation
{

    public interface ITypeTranslator
    {
        Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> ObjTranslator { get; }
        LambdaExpression TransExp { get; }

        Delegate DelTranslator { get; }

        bool HaveDynamicSourceLanguage { get; }

    }


    static class TypeTranslationTest
    {

        static bool AddMember(HashSet<Type> seenTypes, MemberInfo mi, Type t, bool haveTrans = false)
        {
            if (t == typeof(String))
                return mi.GetCustomAttribute<AutoTranslateAttribute>(true) != null;
            return HaveTranslations(seenTypes, t, haveTrans);
        }

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


    public sealed class TypeTranslatorT<T> : ITypeTranslator
    {
        public static readonly Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> Translate;
        public static readonly Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> TranslateObj;
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



        public Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> Translator => Translate;
        public Func<ITranslator, String, Object, TranslationEffort, TranslationCacheRetention, Task> ObjTranslator => TranslateObj;
        public LambdaExpression TransExp => Exp;

        public Delegate DelTranslator => Translate;

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


        static ParameterExpression TranslateString(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, List<Expression> prog, ParameterExpression p, Expression src, IXmlDocInfo context, AutoTranslateAttribute attr, IEnumerable<AutoTranslateContextAttribute> contexts, String memberName, TranslatorTypes trTypes, String fromLanguageMember)
        {
            var strParams = TypeTranslator.ParamString;
            var value = Expression.Variable(src.Type);
            prog.Add(Expression.Assign(value, src));
            var save = Expression.Lambda<Action<String>>(Expression.Assign(src, strParams), strParams);


            //  Get the context expression
            List<String> staticContexts = new List<string>(10);
            if (context != null)
            {
                var sum = context.Summary;
                if (!String.IsNullOrEmpty(sum))
                {
                    if (!attr.NoContext)
                        staticContexts.Add(String.Concat("The description is \"", sum, "\"."));
                }
            }

            var typeContext = TypeTranslator.TypeContexts[(int)trTypes];
            if (!String.IsNullOrEmpty(typeContext))
                staticContexts.Add(typeContext);

            List<Expression> dynContexts = new List<Expression>();
            var tempVal = TypeTranslator.TempVal;
            var fmt = TypeTranslator.StringFmt;
            var ns = TypeTranslator.NullString;
            var type = typeof(T);
            foreach (var c in contexts)
            {
                var x = c.ContextText.Trim().TrimEnd('.');
                if (String.IsNullOrEmpty(x))
                    continue;
                x += '.';
                if (x.IndexOf('{') < 0)
                {
                    staticContexts.Add(x);
                    continue;
                }
                var t = c.MemberNames;
                if (t == null)
                {
                    staticContexts.Add(x);
                    continue;
                }
                var tl = t.Length;
                if (tl <= 0)
                {
                    staticContexts.Add(x);
                    continue;
                }

                Expression[] reads = new Expression[tl];
                for (int i = 0; i < tl; ++i)
                {
                    var name = t[i];
                    Expression read = null;
                    var fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi != null)
                    {
                        read = Expression.Field(p, fi);
                    }
                    else
                    {
                        var pi = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi != null)
                        {
                            read = Expression.Property(p, pi);
                        }
                        else
                        {
                            var mi = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, Array.Empty<Type>());
                            if (TypeTranslator.IsValidContextMethod(mi))
                            {
                                read = Expression.Call(p, mi);
                            }
                            else
                            {
                                mi = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [type]);
                                if (TypeTranslator.IsValidContextMethod(mi))
                                    read = Expression.Call(mi, p);
                            }
                        }
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
                dynContexts.Add(dynE);
            }
            Expression con = ns;
            if (dynContexts.Count > 0)
            {
                if (staticContexts.Count > 0)
                    dynContexts.Add(Expression.Constant(String.Join('\n', staticContexts)));
                con = Expression.Call(TypeTranslator.MergeContextsMethod, Expression.NewArrayInit(typeof(String), dynContexts));
            }
            else
            {
                if (staticContexts.Count > 0)
                    con = Expression.Constant(String.Join('\n', staticContexts));
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


        static Expression BuildObject(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, Type t, ParameterExpression p)
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

        static void AddMember(ref bool haveDynamicSourceLanguage, Dictionary<String, Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task<String>>> members, Type et, MemberInfo mi, List<ParameterExpression> progP, List<Expression> prog, ParameterExpression p, Expression src)
        {
            if (et == typeof(String))
            {
                var attr = mi.GetCustomAttribute<AutoTranslateAttribute>(true);
                if (attr == null)
                    return;
                progP.Add(TranslateString(ref haveDynamicSourceLanguage, members, prog, p, src, attr.NoContext ? null : mi.XmlDoc(), attr, mi.GetCustomAttributes<AutoTranslateContextAttribute>(true), mi.Name, mi.GetCustomAttribute<AutoTranslateTypeAttribute>(true)?.Type ?? TranslatorTypes.Text, mi.GetCustomAttribute<AutoTranslateDynLanguageAttribute>(true)?.MemberName));
                return;
            }
            var seen = new HashSet<Type>();
            if (!TypeTranslationTest.HaveTranslations(seen, et))
                return;
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

        static Expression BuildArray(out Type et, Type t, ParameterExpression p)
        {
            et = t.GetElementType();
            if (!TypeTranslationTest.HaveTranslations(new HashSet<Type>(), et))
                return null;
            var elFunc = Expression.Variable(typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), et, typeof(TranslationEffort), typeof(TranslationCacheRetention), typeof(Task)), "fn");
            var len = TypeTranslator.VarLen;
            var taskList = TypeTranslator.VarTaskArray;
            List<Expression> prog = new()
            {
                Expression.Assign(taskList, Expression.NewArrayBounds(typeof(Task), len)),
                Expression.Assign(elFunc, Expression.Call(TypeTranslator.GetFuncMethod.MakeGenericMethod(et))),
            };
            var subOne = Expression.PreDecrementAssign(len);
            var getOne = Expression.ArrayIndex(p, len);
            var transOne = Expression.Invoke(elFunc, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, getOne, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
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

        static Expression BuildEnumerable(Type t, Type enumerableType, Type et, ParameterExpression p, Func<Expression, Expression> getVal)
        {
            if (!TypeTranslator.TryGetTranslator(et, out var vt))
                return null;
            var elFunc = Expression.Variable(typeof(Func<,,,,,>).MakeGenericType(typeof(ITranslator), typeof(String), et, typeof(Task)), "fn");
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
            Expression program = null;
            if (t.IsArray)
            {
                program = BuildArray(out var et, t, p);
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
                                program = BuildEnumerable(t, et, et, p, e => e);
                                if (program == null)
                                    return;
                                ElementTypes = [et];
                            }
                            break;
                        case 2:
                            var enumType = typeof(KeyValuePair<,>).MakeGenericType(ga);
                            if (typeof(IEnumerable<>).MakeGenericType(enumType).IsAssignableFrom(t))
                            {
                                program = BuildEnumerable(t, enumType, ga[1], p, e => Expression.Property(e, nameof(KeyValuePair<int,int>.Value)));
                                if (program == null)
                                    return;
                                ElementTypes = ga;
                            }
                            break;
                    }


                }
                if (program == null)
                    program = BuildObject(ref haveDynamicSourceLanguage, members, t, p);
            }
            if (program == null)
                return;
            if (!t.IsValueType)
                program = Expression.Condition(Expression.Equal(p, Expression.Constant(null, t)), TypeTranslator.ConstCompletedTask, program);
            var transExp = Expression.Lambda<Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task>>(program, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, p, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
            var translate = transExp.Compile();
            Translate = translate;
            Exp = transExp;

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
