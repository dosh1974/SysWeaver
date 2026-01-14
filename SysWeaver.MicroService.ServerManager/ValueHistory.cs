using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver.MicroService
{

    public sealed class ValueHistory<T> : IEnumerable<ValueTuple<DateTime, T>>
    {
        public ValueHistory(TimeSpan maxAge)
        {
            MaxAge = maxAge;
        }

        public readonly TimeSpan MaxAge;

        readonly ConcurrentQueue<ValueTuple<DateTime, T>> History = new();

        public void Add(T data, DateTime? now = null)
        {
            var h = History;
            lock (h)
            {
                var n = now ?? DateTime.UtcNow;
                var maxAge = n - MaxAge;
                h.Enqueue((n, data));
                while (h.TryPeek(out var o))
                {
                    if (o.Item1 >= maxAge)
                        break;
                    if (!h.TryDequeue(out o))
                        break;
                }
            }
        }

        public IEnumerator<(DateTime, T)> GetEnumerator()
        {
            var maxAge = DateTime.UtcNow - MaxAge;
            foreach (var x in History)
            {
                if (x.Item1 >= maxAge)
                    yield return x;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    
    }



    public sealed class BucketValueHistory<T> : IEnumerable<ValueTuple<DateTime, DateTime, DateTime, long, T>>
    {
        public BucketValueHistory(TimeSpan bucketSize, TimeSpan maxAge, Func<T, T, T> add)
        {
            BucketSize = bucketSize;
            BucketTicks = bucketSize.Ticks;
            MaxAge = maxAge;
            FnAdd = add;
        }

        readonly long BucketTicks;
        readonly Func<T, T, T> FnAdd;

        public readonly TimeSpan BucketSize;
        public readonly TimeSpan MaxAge;

        sealed class BucketVal
        {
            public readonly DateTime Last;
            public readonly long Count;
            public readonly T Value;

            public BucketVal(DateTime last, long count, T value)
            {
                Last = last;
                Count = count;
                Value = value;
            }
        }

        sealed class Bucket
        {

            public readonly DateTime Start;
            public readonly DateTime First;
            public readonly long Id;
            public BucketVal Val;

            public Bucket(DateTime start, DateTime first, long id, BucketVal val)
            {
                Start = start;
                First = first;
                Id = id;
                Val = val;
            }
        }



        readonly ConcurrentQueue<Bucket> History = new();


        Bucket Last;

        public void Add(T data, DateTime? now = null)
        {
            var h = History;
            lock (h)
            {
                var n = now ?? DateTime.UtcNow;
                var bucketId = n.Ticks / BucketTicks;
                var l = Last;
                if ((l != null) && (l.Id == bucketId))
                {
                    var oldVal = l.Val;
                    var newVal = new BucketVal(n, oldVal.Count + 1, FnAdd(oldVal.Value, data));
                    Interlocked.Exchange(ref l.Val, newVal);
                }else
                {
                    l = new Bucket(new DateTime(bucketId * BucketTicks, DateTimeKind.Utc), n, bucketId, new BucketVal(n, 1, data));
                    Last = l;
                    h.Enqueue(l);
                }
                var maxAge = n - MaxAge;
                while (h.TryPeek(out var o))
                {
                    if (o.Val.Last >= maxAge)
                        break;
                    if (!h.TryDequeue(out o))
                        break;
                }
            }
        }


        /// <summary>
        /// (BucketStartTime, FirstBucketSample, LastBucketSample, NumberOfSamples, AccumulatedData)
        /// </summary>
        /// <returns></returns>
        public IEnumerator<(DateTime, DateTime, DateTime, long, T)> GetEnumerator()
        {
            var maxAge = DateTime.UtcNow - MaxAge;
            foreach (var x in History)
            {
                var val = x.Val;
                var l = val.Last;
                if (l >= maxAge)
                    yield return (x.Start, x.First, l, val.Count, val.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }


}
