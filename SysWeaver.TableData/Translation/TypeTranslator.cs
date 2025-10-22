using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{

    public static class TypeTranslator
    {
        /// <summary>
        /// Translate the content of a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="value">The object to translate</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns></returns>
        public static Task Translate<T>(ITranslator tr, String to, T value, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
            => TypeTranslatorT<T>.Translate?.Invoke(tr, to, value, effort, retention) ?? Task.CompletedTask;

        /// <summary>
        /// Translate the content of a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>null if no translation is needed, else the value returned by getValue</returns>
        public static async Task<T> Translate<T>(ITranslator tr, String to, Func<T> getValue, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
        {
            var t = TypeTranslatorT<T>.Translate;
            if (t == null)
                return default;
            var value = getValue();
            if (value == null)
                return default;
            await t(tr, to, value, effort, retention).ConfigureAwait(false);
            return value;
        }

        /// <summary>
        /// Translate the content of a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="A0"></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="a0">Argument of the getValue function</param>
        /// <returns>null if no translation is needed, else the value returned by getValue</returns>
        public static async Task<T> Translate<T, A0>(ITranslator tr, String to, TranslationEffort effort, TranslationCacheRetention retention, Func<A0, T> getValue, A0 a0)
        {
            var t = TypeTranslatorT<T>.Translate;
            if (t == null)
                return default;
            var value = getValue(a0);
            if (value == null)
                return default;
            await t(tr, to, value, effort, retention).ConfigureAwait(false);
            return value;
        }

        /// <summary>
        /// Translate the content of a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="A0"></typeparam>
        /// <typeparam name="A1"></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="a0">First argument of the getValue function</param>
        /// <param name="a1">Second argument of the getValue function</param>
        /// <returns>null if no translation is needed, else the value returned by getValue</returns>
        public static async Task<T> Translate<T, A0, A1>(ITranslator tr, String to, TranslationEffort effort, TranslationCacheRetention retention, Func<A0, A1, T> getValue, A0 a0, A1 a1)
        {
            var t = TypeTranslatorT<T>.Translate;
            if (t == null)
                return default;
            var value = getValue(a0, a1);
            if (value == null)
                return default;
            await t(tr, to, value, effort, retention).ConfigureAwait(false);
            return value;
        }

        /// <summary>
        /// Get a translator interface for a given type (if the type have any fields that require translation)
        /// </summary>
        /// <param name="t">The type to get the translator for</param>
        /// <param name="tr">The method used to translate the type</param>
        /// <returns>True if the type can be translated, null if it can't</returns>
        public static bool TryGetTranslator(Type t, out ITypeTranslator tr)
        {
            var c = Translators;
            if (c.TryGetValue(t, out tr))
                return tr != null;
            var type = typeof(TypeTranslatorT<>).MakeGenericType(t);
            var tt = Activator.CreateInstance(type);
            var tti = tt as ITypeTranslator;
            tr = (tti?.ObjTranslator == null) ? null : tti;
            c.TryAdd(t, tr);
            return tr != null;
        }

        /// <summary>
        /// Get a method that will translate a type (if the type have any fields that require translation)
        /// </summary>
        /// <param name="tr">The method used to translate the type</param>
        /// <returns>True if the type can be translated, null if it can't</returns>
        public static bool TryGetTranslator<T>(out Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> tr)
        {
            tr = TypeTranslatorT<T>.Translate;
            return tr != null;
        }


        internal static readonly ParameterExpression TempVal = Expression.Parameter(typeof(String), "value");
        
        internal static bool IsValidContextMethod(MethodInfo mi)
        {
            if (mi == null)
                return false;
            var rt = mi.ReturnType;
            if (InvalidContextMethodReturnType.Contains(rt))
                return false;
            if (rt.IsGenericType)
                if (InvalidContextMethodGenericReturnType.Contains(rt.GetGenericTypeDefinition()))
                    return false;
            return true;
        }

        static readonly IReadOnlySet<Type> InvalidContextMethodReturnType = ReadOnlyData.Set(
            [
                typeof(void),
                typeof(Task),
                typeof(ValueTask),
            ]
        );

        static readonly IReadOnlySet<Type> InvalidContextMethodGenericReturnType = ReadOnlyData.Set(
            [
                typeof(Task<>),
                typeof(ValueTask<>),
            ]
        );


        internal static readonly MethodInfo StringFmt = typeof(String).GetMethod(nameof(String.Format), BindingFlags.Static | BindingFlags.Public, [typeof(String), typeof(Object[])]);

        internal static readonly Expression NullString = Expression.Constant(null, typeof(String));

        static String MergeContexts(String[] contexts)
        {
            var c = contexts.Length;
            int l = 0;
            for (int i = 0; i < c; ++i)
            {
                var t = contexts[i];
                if (t == null)
                    continue;
                var tl = t.Length;
                if (tl <= 0)
                    continue;
                if (l > 0)
                    ++l;
                l += tl;
            }
            if (l <= 0)
                return null;
            return String.Create(l, contexts, StringMergerAction);
        }

        static void StringMerger(Span<Char> to, String[] contexts)
        {
            var c = contexts.Length;
            int l = 0;
            for (int i = 0; i < c; ++i)
            {
                var t = contexts[i];
                if (t == null)
                    continue;
                var tl = t.Length;
                if (tl <= 0)
                    continue;
                if (l > 0)
                {
                    to[l] = '\n';
                    ++l;
                }
                t.AsSpan().CopyTo(to.Slice(l));
                l += tl;
            }
        }

        static readonly SpanAction<char, String[]> StringMergerAction = StringMerger;

        internal static readonly MethodInfo MergeContextsMethod = typeof(TypeTranslator).GetMethod(nameof(MergeContexts), BindingFlags.Static | BindingFlags.NonPublic);
        internal static readonly MethodInfo ListAddMethod = typeof(List<Task>).GetMethod(nameof(List<Task>.Add), BindingFlags.Instance| BindingFlags.Public, [typeof(Task)]);
        internal static readonly MethodInfo TaskWhenAllMethod = typeof(Task).GetMethod(nameof(Task.WhenAll), BindingFlags.Static| BindingFlags.Public, [typeof(IEnumerable<Task>)]);
        internal static readonly MethodInfo TaskWhenAllArrayMethod = typeof(Task).GetMethod(nameof(Task.WhenAll), BindingFlags.Static | BindingFlags.Public, [typeof(Task[])]);


        internal static readonly ParameterExpression ParamObj = Expression.Parameter(typeof(Object), "obj");
        internal static readonly ParameterExpression ParamTo = Expression.Parameter(typeof(String), "to");
        internal static readonly ParameterExpression ParamTranslator = Expression.Parameter(typeof(ITranslator), "tr");

        internal static readonly ParameterExpression ParamEffort = Expression.Parameter(typeof(TranslationEffort), "effort");
        internal static readonly ParameterExpression ParamRetention = Expression.Parameter(typeof(TranslationCacheRetention), "retention");

        internal static readonly ParameterExpression VarTaskList = Expression.Variable(typeof(List<Task>), "tasks");
        internal static readonly ParameterExpression VarTaskArray = Expression.Variable(typeof(Task[]), "tasks");

        internal static readonly ParameterExpression VarLen = Expression.Variable(typeof(int), "len");

        internal static readonly ParameterExpression ParamString = Expression.Parameter(typeof(String), "str");

        internal static readonly ConstantExpression ConstIntZero = Expression.Constant(0, typeof(int));
        internal static readonly ConstantExpression ConstIntMinus1 = Expression.Constant(-1, typeof(int));
        internal static readonly ConstantExpression ConstCompletedTask = Expression.Constant(Task.CompletedTask as Task, typeof(Task));


        internal static readonly LabelTarget LabelTarget = Expression.Label();
        internal static readonly LabelExpression LabelExp = Expression.Label(LabelTarget);

        internal static readonly MethodInfo TranslateOneMethod = typeof(TypeTranslator).GetMethod(nameof(TranslateOne), BindingFlags.Static | BindingFlags.NonPublic);

        static async Task TranslateOne(ITranslator tr, String from, String to, String text, String context, Action<String> save, TranslationEffort effort, TranslationCacheRetention retention)
        {
            try
            {
                var res = await tr.TranslateOne(new TranslateRequest
                {
                    From = from,
                    To = to,
                    Text = text,
                    Context = context,
                    Effort = effort,
                    Retention = retention,
                }).ConfigureAwait(false);
                if (res != null)
                    save(res);
            }
            catch
            {
            }
        }

        static readonly Task<String> NullTask = Task.FromResult((String)null);
        static readonly Task<String> EmptyTask = Task.FromResult("");

        static readonly ConcurrentDictionary<Type, ITypeTranslator> Translators = new ();


        static void CondDispose(Object o)
            => (o as IDisposable)?.Dispose();


        internal static readonly MethodInfo CondDisposeMethod = typeof(TypeTranslator).GetMethod(nameof(CondDispose), BindingFlags.Static | BindingFlags.NonPublic);
        internal static readonly MethodInfo MoveNextMethod = typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext), BindingFlags.Public | BindingFlags.Instance, Array.Empty<Type>());


        internal static readonly IReadOnlySet<Type> PrimTypes = ReadOnlyData.Set(
            [
                typeof(String),
                typeof(Object),
                typeof(Guid),
                typeof(DateTime),
                typeof(DateOnly),
                typeof(TimeOnly),
                typeof(DateTimeOffset),
                typeof(TimeSpan),
            ]
            );


        static Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> GetFunc<T>()
            => TypeTranslatorT<T>.Translate;

        internal static readonly MethodInfo GetFuncMethod = typeof(TypeTranslator).GetMethod(nameof(GetFunc), BindingFlags.Static | BindingFlags.NonPublic);

        public const String MdContext = "The text is Mark Down, urls/links are located within a '(' and ')' and should never be translated.";
        public const String HtmlContext = "The text is HTML code";

        public static readonly IReadOnlyList<String> TypeContexts = [
            null,
            MdContext,
            HtmlContext
            ];

        /*
        /// <summary>
        /// Get a dictionary with all string members of a type that is marked as translatable, along with a function to do the translation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>null if the type doesn't have any string members markes as translatable</returns>
        public static IReadOnlyDictionary<String, Func<ITranslator, String, T, Task<String>>> GetMember<T>() => TypeTranslatorT<T>.GetMember;
        */

    }


}
