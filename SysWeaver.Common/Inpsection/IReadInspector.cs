namespace SysWeaver.Inspection
{
    public interface IReadInspector : IInspector
    {
        T Read<T>();
    }

}

