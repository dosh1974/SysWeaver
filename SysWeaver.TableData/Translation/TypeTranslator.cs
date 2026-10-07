using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{

    /// <summary>
    /// Translates (in place) all <see cref="String"/> members marked with <see cref="AutoTranslateAttribute"/> of an object graph
    /// (including nested objects, arrays, collections and dictionary values) using an <see cref="ITranslator"/>.
    /// </summary>
    /// <remarks>
    /// The translation code for each type is generated once using expression trees (see <see cref="TypeTranslatorT{T}"/>) and cached.
    /// Translation context is built from the member XML doc summary, <see cref="AutoTranslateContextAttribute"/> and <see cref="AutoTranslateTypeAttribute"/>,
    /// the source language from <see cref="AutoTranslateAttribute.FromLanguage"/> (defaults to "en") or <see cref="AutoTranslateDynLanguageAttribute"/>.
    /// Used by the HTTP server to translate API responses and by the table data system to translate table rows.
    /// Individual translation failures are silently ignored (the original text is kept).
    /// </remarks>
    public static class TypeTranslator
    {
        /// <summary>
        /// Translate the content of an object in place (all members marked with <see cref="AutoTranslateAttribute"/>, recursively)
        /// </summary>
        /// <typeparam name="T">The type of the object to translate</typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="value">The object to translate, null is ignored</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>A task that completes when all members have been translated, a completed task if <typeparamref name="T"/> have nothing to translate</returns>
        /// <remarks>Value types are passed by value, so translations of a struct <paramref name="value"/> are lost,
        /// use <see cref="TranslateValue{T}(ITranslator, string, T, TranslationEffort, TranslationCacheRetention)"/> or
        /// <see cref="TranslateBoxed{T}(ITranslator, string, StrongBox{T}, TranslationEffort, TranslationCacheRetention)"/> for structs.
        /// Struct members (and struct array elements) of a class are written back to the member.</remarks>
        public static Task Translate<T>(ITranslator tr, String to, T value, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
            => TypeTranslatorT<T>.Translate?.Invoke(tr, to, value, effort, retention) ?? Task.CompletedTask;

        /// <summary>
        /// Translate the content of a value (all members marked with <see cref="AutoTranslateAttribute"/>, recursively) and return the translated value.
        /// For reference types the object is translated in place and returned, for value types (structs) a translated copy is returned.
        /// </summary>
        /// <typeparam name="T">The type of the value to translate</typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="value">The value to translate, null is ignored</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>The translated value (<paramref name="value"/> as is if <typeparamref name="T"/> have nothing to translate)</returns>
        /// <remarks>
        /// This is the by reference write back variant of <see cref="Translate{T}(ITranslator, string, T, TranslationEffort, TranslationCacheRetention)"/>:
        /// the translation is asynchronous so a ref parameter can't be used, assign the result instead (ex: "s = await TypeTranslator.TranslateValue(tr, to, s);").
        /// </remarks>
        public static async Task<T> TranslateValue<T>(ITranslator tr, String to, T value, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
        {
            var t = TypeTranslatorT<T>.TranslateBox;
            if (t == null)
                return value;
            if (value == null)
                return value;
            var box = new StrongBox<T>(value);
            await t(tr, to, box, effort, retention).ConfigureAwait(false);
            return box.Value;
        }

        /// <summary>
        /// Translate the content of a boxed value in place (all members marked with <see cref="AutoTranslateAttribute"/>, recursively),
        /// when the task completes <see cref="StrongBox{T}.Value"/> holds the translated value (also for value types / structs).
        /// </summary>
        /// <typeparam name="T">The type of the value to translate</typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="value">The box holding the value to translate, null (or a null value) is ignored</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>A task that completes when all members have been translated, a completed task if <typeparamref name="T"/> have nothing to translate</returns>
        public static Task TranslateBoxed<T>(ITranslator tr, String to, StrongBox<T> value, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
            => TypeTranslatorT<T>.TranslateBox?.Invoke(tr, to, value, effort, retention) ?? Task.CompletedTask;

        /// <summary>
        /// Get a value and translate it's content in place, the value is only retrieved if <typeparamref name="T"/> have something to translate
        /// </summary>
        /// <typeparam name="T">The type of the object to translate</typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>default (null) if <typeparamref name="T"/> have nothing to translate or if <paramref name="getValue"/> returned null, else the (translated) value returned by <paramref name="getValue"/> (for value types a translated copy)</returns>
        public static async Task<T> Translate<T>(ITranslator tr, String to, Func<T> getValue, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
        {
            var t = TypeTranslatorT<T>.TranslateBox;
            if (t == null)
                return default;
            var value = getValue();
            if (value == null)
                return default;
            var box = new StrongBox<T>(value);
            await t(tr, to, box, effort, retention).ConfigureAwait(false);
            return box.Value;
        }

        /// <summary>
        /// Get a value and translate it's content in place, the value is only retrieved if <typeparamref name="T"/> have something to translate
        /// </summary>
        /// <typeparam name="T">The type of the object to translate</typeparam>
        /// <typeparam name="A0">The type of the argument passed to <paramref name="getValue"/></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="a0">Argument of the getValue function</param>
        /// <returns>default (null) if <typeparamref name="T"/> have nothing to translate or if <paramref name="getValue"/> returned null, else the (translated) value returned by <paramref name="getValue"/> (for value types a translated copy)</returns>
        public static async Task<T> Translate<T, A0>(ITranslator tr, String to, TranslationEffort effort, TranslationCacheRetention retention, Func<A0, T> getValue, A0 a0)
        {
            var t = TypeTranslatorT<T>.TranslateBox;
            if (t == null)
                return default;
            var value = getValue(a0);
            if (value == null)
                return default;
            var box = new StrongBox<T>(value);
            await t(tr, to, box, effort, retention).ConfigureAwait(false);
            return box.Value;
        }

        /// <summary>
        /// Get a value and translate it's content in place, the value is only retrieved if <typeparamref name="T"/> have something to translate
        /// </summary>
        /// <typeparam name="T">The type of the object to translate</typeparam>
        /// <typeparam name="A0">The type of the first argument passed to <paramref name="getValue"/></typeparam>
        /// <typeparam name="A1">The type of the second argument passed to <paramref name="getValue"/></typeparam>
        /// <param name="tr">The translator to use</param>
        /// <param name="to">The target language ISO code</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <param name="getValue">A function that returns the value, only called if translation in needed</param>
        /// <param name="a0">First argument of the getValue function</param>
        /// <param name="a1">Second argument of the getValue function</param>
        /// <returns>default (null) if <typeparamref name="T"/> have nothing to translate or if <paramref name="getValue"/> returned null, else the (translated) value returned by <paramref name="getValue"/> (for value types a translated copy)</returns>
        public static async Task<T> Translate<T, A0, A1>(ITranslator tr, String to, TranslationEffort effort, TranslationCacheRetention retention, Func<A0, A1, T> getValue, A0 a0, A1 a1)
        {
            var t = TypeTranslatorT<T>.TranslateBox;
            if (t == null)
                return default;
            var value = getValue(a0, a1);
            if (value == null)
                return default;
            var box = new StrongBox<T>(value);
            await t(tr, to, box, effort, retention).ConfigureAwait(false);
            return box.Value;
        }

        /// <summary>
        /// Get a translator interface for a given type (if the type have any fields that require translation)
        /// </summary>
        /// <param name="t">The type to get the translator for</param>
        /// <param name="tr">The translator for the type, null if the type have nothing to translate</param>
        /// <returns>True if the type have something to translate, else false</returns>
        /// <remarks>
        /// Results (including negative ones) are cached per type, thread safe.
        /// Exceptions thrown while generating the translator (ex: invalid attribute usage) are propagated (wrapped in a <see cref="TargetInvocationException"/>).
        /// </remarks>
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
        /// <typeparam name="T">The type to get the translator for</typeparam>
        /// <param name="tr">The method used to translate the type (in place), null if the type have nothing to translate</param>
        /// <returns>True if the type have something to translate, else false</returns>
        public static bool TryGetTranslator<T>(out Func<ITranslator, String, T, TranslationEffort, TranslationCacheRetention, Task> tr)
        {
            tr = TypeTranslatorT<T>.Translate;
            return tr != null;
        }


        internal static readonly ParameterExpression TempVal = Expression.Parameter(typeof(String), "value");
        
        /// <summary>
        /// Check if a method can be used to produce a context argument (for <see cref="AutoTranslateContextAttribute"/>), i.e. it exists and isn't void or async.
        /// </summary>
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


#if DEBUG

#pragma warning disable CS0168

        static String Fmt(String x, Object[] p)
        {
            try
            {
                return String.Format(x, p);
            }
            catch (Exception ex)
            {
                return x;
            }
        }

#pragma warning restore CS0168

        internal static readonly MethodInfo StringFmt = typeof(TypeTranslator).GetMethod(nameof(TypeTranslator.Fmt), BindingFlags.Static | BindingFlags.NonPublic, [typeof(String), typeof(Object[])]);

#else//DEBUG

        internal static readonly MethodInfo StringFmt = typeof(String).GetMethod(nameof(String.Format), BindingFlags.Static | BindingFlags.Public, [typeof(String), typeof(Object[])]);

#endif//DEBUG

        internal static readonly Expression NullString = Expression.Constant(null, typeof(String));

        /// <summary>
        /// Merge multiple translation context texts into a single text, separated by new lines ('\n').
        /// </summary>
        /// <param name="contexts">The contexts to merge, null and empty strings are skipped</param>
        /// <returns>The merged context, null if there are no non-empty contexts</returns>
        public static String MergeContexts(params String[] contexts)
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

        internal static readonly MethodInfo MergeContextsMethod = typeof(TypeTranslator).GetMethod(nameof(MergeContexts), BindingFlags.Static | BindingFlags.Public);
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

        /// <summary>
        /// Translate a single text and store the result using <paramref name="save"/>, all exceptions are swallowed (the text is left untranslated).
        /// </summary>
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


        /// <summary>
        /// Non-primitive types that are never inspected for translatable members.
        /// </summary>
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

        static Func<ITranslator, String, StrongBox<T>, TranslationEffort, TranslationCacheRetention, Task> GetBoxFunc<T>()
            => TypeTranslatorT<T>.TranslateBox;

        internal static readonly MethodInfo GetBoxFuncMethod = typeof(TypeTranslator).GetMethod(nameof(GetBoxFunc), BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Translate a (struct) value in a box and write the translated value back using <paramref name="save"/> when all translations completed.
        /// </summary>
        static async Task TranslateStruct<T>(Func<ITranslator, String, StrongBox<T>, TranslationEffort, TranslationCacheRetention, Task> fn, ITranslator tr, String to, T value, TranslationEffort effort, TranslationCacheRetention retention, Action<T> save)
        {
            if (fn == null)
                return;
            var box = new StrongBox<T>(value);
            await fn(tr, to, box, effort, retention).ConfigureAwait(false);
            save(box.Value);
        }

        internal static readonly MethodInfo TranslateStructMethod = typeof(TypeTranslator).GetMethod(nameof(TranslateStruct), BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Translate a (struct) array element in a box and write the translated value back to the array element when all translations completed.
        /// </summary>
        internal static async Task TranslateArrayElement<T>(Func<ITranslator, String, StrongBox<T>, TranslationEffort, TranslationCacheRetention, Task> fn, ITranslator tr, String to, T[] array, int index, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (fn == null)
                return;
            var box = new StrongBox<T>(array[index]);
            await fn(tr, to, box, effort, retention).ConfigureAwait(false);
            array[index] = box.Value;
        }

        internal static readonly MethodInfo TranslateArrayElementMethod = typeof(TypeTranslator).GetMethod(nameof(TranslateArrayElement), BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Translation context added for members marked with <see cref="AutoTranslateTypeAttribute"/> using <see cref="TranslatorTypes.MD"/>.
        /// </summary>
        public const String MdContext = "The text is Mark Down, urls/links are located within a '(' and ')' and should never be translated.";
        /// <summary>
        /// Translation context added for members marked with <see cref="AutoTranslateTypeAttribute"/> using <see cref="TranslatorTypes.Html"/>.
        /// </summary>
        public const String HtmlContext = "The text is HTML code";

        /// <summary>
        /// The translation context to add for each <see cref="TranslatorTypes"/> value (indexed by the enum value), null for none.
        /// </summary>
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
