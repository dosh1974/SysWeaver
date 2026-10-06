using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Limits the number of concurrent executions of async functions, by replacing a function with a function that takes a slot in an <see cref="AsyncLock"/> before calling the original function.
    /// </summary>
    /// <remarks>
    /// A typical usage is to limit the number of concurrent tasks when processing a list of items in parallel.
    /// The function is only wrapped if the number of items is greater than the computed concurrency (otherwise there is nothing to limit).
    /// No arguments are validated, a null function is wrapped (and the wrapped function throws a <see cref="NullReferenceException"/> when invoked).
    /// </remarks>
    public static class ConcurrencyLimiter
    {

        /// <summary>
        /// Use this value (Int.MaxValue) to avoid any limiting
        /// </summary>
        public const int NoLimit = int.MaxValue;



        /// <summary>
        /// The number of logical processors (Environment.ProcessorCount), used to compute the concurrency when maxConcurrency is zero or negative.
        /// </summary>
        public static readonly int ProcessorCount = Environment.ProcessorCount;


        /// <summary>
        /// The default maximum concurrency used by default (when maxConcurrency is zero), the number of processors minus one (but at least two).
        /// </summary>
        public static readonly int DefaultLimit = ProcessorCount <= 2 ? 2 : (ProcessorCount - 1);

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, T>(ref Func<K, int, Task<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, T>(ref Func<K, int, ValueTask<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, T>(ref Func<K, Task<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, T>(ref Func<K, ValueTask<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k).ConfigureAwait(false);
            };
        }




        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, V, T>(ref Func<K, V, int, Task<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, v, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, V, T>(ref Func<K, V, int, ValueTask<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, v, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, V, T>(ref Func<K, V, Task<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, v).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        /// <typeparam name="T">The type of the result of the function</typeparam>
        public static void LimitConcurrency<K, V, T>(ref Func<K, V, ValueTask<T>> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                return await orgFn(k, v).ConfigureAwait(false);
            };
        }





        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        public static void LimitConcurrency<K>(ref Func<K, int, Task> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        public static void LimitConcurrency<K>(ref Func<K, int, ValueTask> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        public static void LimitConcurrency<K>(ref Func<K, Task> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        public static void LimitConcurrency<K>(ref Func<K, ValueTask> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k).ConfigureAwait(false);
            };
        }




        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        public static void LimitConcurrency<K, V>(ref Func<K, V, int, Task> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, v, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        public static void LimitConcurrency<K, V>(ref Func<K, V, int, ValueTask> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v, i) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, v, i).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        public static void LimitConcurrency<K, V>(ref Func<K, V, Task> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, v).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit, it is replaced with a function that waits for a free slot before calling the original function (it is left unchanged if no limiting is needed)</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <typeparam name="K">The type of the first argument of the function (typically the item)</typeparam>
        /// <typeparam name="V">The type of the second argument of the function</typeparam>
        public static void LimitConcurrency<K, V>(ref Func<K, V, ValueTask> fn, int maxConcurrency, int numberOfItems)
        {
            var l = CreateLock(maxConcurrency, numberOfItems);
            if (l == null)
                return;
            var orgFn = fn;
            fn = async (k, v) =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                await orgFn(k, v).ConfigureAwait(false);
            };
        }





        /// <summary>
        /// Return a lock for the given concurrency constraints
        /// </summary>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors (rounded to the nearest integer, at least one).
        /// If zero the concurrency is <see cref="DefaultLimit"/> (the number of processors minus one, but at least two).
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The number of items that will be processed, the function is only wrapped if it's greater than the computed concurrency (and greater than one)</param>
        /// <returns>A lock or null</returns>
        static AsyncLock CreateLock(int maxConcurrency, int numberOfItems)
        {
            if ((numberOfItems <= 1) || (maxConcurrency >= NoLimit))
                return null;
            if (maxConcurrency == 0)
                maxConcurrency = DefaultLimit;
            if (maxConcurrency < 0)
            {
                // Use 64 bit math, large negative values (percentages) would overflow (and incorrectly result in a concurrency of one)
                var m = ((-(long)maxConcurrency) * ProcessorCount + 50) / 100;
                maxConcurrency = m < 1 ? 1 : (m >= NoLimit ? NoLimit : (int)m);
            }
            return numberOfItems <= maxConcurrency ? null : new AsyncLock(maxConcurrency);
        }


    }

}
