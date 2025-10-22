using System;

namespace SysWeaver.Compression
{
    public interface ICompType : ICompEncoder, ICompDecoder
    {
        static virtual void Register() => throw new NotImplementedException();
        static virtual ICompType Instance { get => throw new NotImplementedException(); } 
    }

}
