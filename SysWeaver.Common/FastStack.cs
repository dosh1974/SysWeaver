using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver
{


    /*
    /// <summary>
    /// A fast thread safe stack.
    /// The data to push / pop from the stack must inherit from FastStackNode
    /// </summary>
    public sealed class FastStack<T> where T : FastStackNode
    {
        public void Push(T node)
        {
#if DEBUG
            if (node == null)
                throw new ArgumentNullException(nameof(node));
#endif//DEBUG

            SpinWait spinner = new();
            while (true)
            {
                var currentBits = Interlocked.CompareExchange(ref Head, 0, 0);
                var current = Unsafe.As<Int128, StackState>(ref currentBits);
                node.Next = current.Node;
                StackState next = new()
                {
                    Node = node,
                    Version = current.Version + 1
                };
                var nextBits = Unsafe.As<StackState, Int128>(ref next);
                if (Interlocked.CompareExchange(ref Head, nextBits, currentBits) == currentBits)
                    return;
                spinner.SpinOnce();
            }
        }

        public bool TryPop(out T poppedNode)
        {
            SpinWait spinner = new();
            while (true)
            {
                var currentBits = Interlocked.CompareExchange(ref Head, 0, 0);
                var current = Unsafe.As<Int128, StackState>(ref currentBits);
                var cnode = current.Node;
                if (cnode == null)
                {
                    poppedNode = default;
                    return false;
                }
                StackState next = new() 
                { 
                    Node = cnode.Next, 
                    Version = current.Version + 1 
                };
                var nextBits = Unsafe.As<StackState, Int128>(ref next);
                if (Interlocked.CompareExchange(ref Head, nextBits, currentBits) == currentBits)
                {
                    poppedNode = (T)current.Node;
                    current.Node.Next = null;
                    return true;
                }
                spinner.SpinOnce();
            }
        }

        Int128 Head;

        struct StackState
        {
            public FastStackNode Node;
            public long Version;
        }

    }

    public class FastStackNode
    {
        internal volatile FastStackNode Next;
    }
*/
}