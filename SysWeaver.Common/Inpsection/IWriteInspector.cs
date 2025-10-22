namespace SysWeaver.Inspection
{
    public interface IWriteInspector : IInspector
    {
        void Write<T>(T obj, bool saveAsObject = true);
    }

}

