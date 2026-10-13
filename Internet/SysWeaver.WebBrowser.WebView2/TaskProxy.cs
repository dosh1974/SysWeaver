using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    public abstract class TaskProxy : IDisposable
    {
        interface IDoOne
        {
            Task Do();
        }

        sealed class DoOne<T> : IDoOne
        {

            public DoOne(Func<Task<T>> f, TaskCompletionSource<T> s)
            {
                F = f;
                S = s;
            }

            readonly Func<Task<T>> F;
            readonly TaskCompletionSource<T> S;


            public async Task Do()
            {
                try
                {
                    var res = await F();
                    S.SetResult(res);
                }
                catch (Exception ex)
                {
                    S.SetException(ex);
                }
            }
        }

        sealed class DoOneVoid : IDoOne
        {

            public DoOneVoid(Func<Task> f, TaskCompletionSource s)
            {
                F = f;
                S = s;
            }

            readonly Func<Task> F;
            readonly TaskCompletionSource S;


            public async Task Do()
            {
                try
                {
                    await F();
                    S.SetResult();
                }
                catch (Exception ex)
                {
                    S.SetException(ex);
                }
            }
        }

        sealed class DoOneAction : IDoOne
        {

            public DoOneAction(Action f, Action<Exception> ex)
            {
                F = f;
                Ex = ex;
            }

            readonly Action F;
            readonly Action<Exception> Ex;


            public Task Do()
            {
                try
                {
                    F();
                }
                catch (Exception ex)
                {
                    try
                    {
                        Ex?.Invoke(ex);
                    }
                    catch
                    {
                    }
                }
                return Task.CompletedTask;
            }
        }

        sealed class DoOneFunc<T> : IDoOne
        {

            public DoOneFunc(Func<T> f, Action<T> onResult, Action<Exception> ex)
            {
                F = f;
                R = onResult;
                Ex = ex;
            }

            readonly Func<T> F;
            readonly Action<T> R;
            readonly Action<Exception> Ex;


            public Task Do()
            {
                try
                {
                    var res = F();
                    R?.Invoke(res);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Ex?.Invoke(ex);
                    }
                    catch
                    {
                    }
                }
                return Task.CompletedTask;
            }
        }

        public Task<T> Run<T>(Func<Task<T>> func)
        {
            var comp = new TaskCompletionSource<T>();
            Tasks.Enqueue(new DoOne<T>(func, comp));
            W.Set();
            return comp.Task;
        }

        public Task Run(Func<Task> func)
        {
            var comp = new TaskCompletionSource();
            Tasks.Enqueue(new DoOneVoid(func, comp));
            W.Set();
            return comp.Task;
        }

        public void Run(Action a, Action<Exception> onException = null)
        {
            Tasks.Enqueue(new DoOneAction(a, onException));
            W.Set();
        }

        public void Run<T>(Func<T> f, Action<T> onResult, Action<Exception> onException = null)
        {
            Tasks.Enqueue(new DoOneFunc<T>(f, onResult, onException));
            W.Set();
        }


        readonly ConcurrentQueue<IDoOne> Tasks = new ConcurrentQueue<IDoOne>();

        ManualResetEventSlim W = new ManualResetEventSlim();


        /// <summary>
        /// Return true if disposed
        /// </summary>
        /// <returns></returns>
        protected async Task<bool> Wait()
        {
            if (IsDisposed)
                return true;
            var w = W;
            await w.WaitHandle.WaitOneAsync();
            w.Reset();
            return IsDisposed;
        }

        protected bool SpinWait(Action onSpin)
        {
            if (IsDisposed)
                return true;
            var s = new SpinWait();
            var w = W;
            while (!w.IsSet)
            {
                s.SpinOnce();
                onSpin();
            }
            if (IsDisposed)
                return true;
            w.Reset();
            return false;
        }

        protected Task NextTask()
            => Tasks.TryDequeue(out var d) ? d.Do() : null;

        protected async Task Consume()
        {
            var t = Tasks;
            while (t.TryDequeue(out var d))
                await d.Do();
        }

        public virtual void Dispose()
        {
            IsDisposed = true;
            W.Set();
            W.Dispose();
        }

        bool IsDisposed;

    }





}
