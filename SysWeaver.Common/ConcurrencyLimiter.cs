using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class ConcurrencyLimiter
    {

        /// <summary>
        /// Use this value (Int.MaxValue) to avoid any limiting
        /// </summary>
        public const int NoLimit = int.MaxValue;




        public static readonly int ProcessorCount = Environment.ProcessorCount;


        /// <summary>
        /// The default maximum concurrency used by default (when maxConcurrency is zero).
        /// </summary>
        public static readonly int DefaultLimit = ProcessorCount <= 2 ? 2 : (ProcessorCount - 1);

        /// <summary>
        /// Applies a concurrency limiter to some task function
        /// </summary>
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// <param name="fn">The function to limit</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
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
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <param name="numberOfItems">The lock is only created if the number of items is greater than the computed concurrency</param>
        /// <returns>A lock or null</returns>
        static AsyncLock CreateLock(int maxConcurrency, int numberOfItems)
        {
            if ((numberOfItems <= 1) || (maxConcurrency >= NoLimit))
                return null;
            if (maxConcurrency == 0)
                maxConcurrency = DefaultLimit;
            if (maxConcurrency < 0)
            {
                var p = ProcessorCount;
                maxConcurrency = -maxConcurrency;
                maxConcurrency *= p;
                maxConcurrency += 50;
                maxConcurrency /= 100;
                if (maxConcurrency < 1)
                    maxConcurrency = 1;
            }
            return numberOfItems <= maxConcurrency ? null : new AsyncLock(maxConcurrency);
        }


    }

}
