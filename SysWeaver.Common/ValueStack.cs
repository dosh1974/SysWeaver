namespace SysWeaver
{
    /// <summary>
    /// If possible use, FastStack instead, only use this if you don't have control over the type that is pushed / popped
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ValueStack<T>
    {
        internal sealed class Node : FastStackNode
        {
            public T Value;
        }

        public void Push(T data)
        {
            if (!Free.TryPop(out Node node))
                node = new();
            node.Value = data;
            Stack.Push(node);
        }

        public bool TryPop(out T value)
        {
            if ((!Stack.TryPop(out var node)))
            {
                value = default;
                return false;
            }
            value = node.Value;
            node.Value = default;
            Free.Push(node);
            return true;
        }

        readonly FastStack<Node> Free = new ();
        readonly FastStack<Node> Stack = new();


    }


}