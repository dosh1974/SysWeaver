using System;
using System.Collections.Generic;


namespace SysWeaver.Inspection
{

    public delegate void SetProp<T>(T newValue);

    public interface IInspector : IDisposable
    {

        #region Fields

        void Field(ref Boolean value);
        void Field(ref Byte value);
        void Field(ref Char value);
        void Field(ref Decimal value);
        void Field(ref Double value);
        void Field(ref Int16 value);
        void Field(ref Int32 value);
        void Field(ref Int64 value);
        void Field(ref SByte value);
        void Field(ref Single value);
        void Field(ref String value);
        void Field(ref UInt16 value);
        void Field(ref UInt32 value);
        void Field(ref UInt64 value);
        void Field(ref DateTime value);
        void Field(ref TimeSpan value);
        void Field(ref TimeOnly value);
        void Field(ref DateOnly value);
        void Field(ref DateTimeOffset value);
        void Field(ref Guid value);
        void Field<T>(ref T value);

        #endregion//Fields

        #region Properties

        void Prop(Boolean value, SetProp<Boolean> setValue = null);
        void Prop(Byte value, SetProp<Byte> setValue = null);
        void Prop(Char value, SetProp<Char> setValue = null);
        void Prop(Decimal value, SetProp<Decimal> setValue = null);
        void Prop(Double value, SetProp<Double> setValue = null);
        void Prop(Int16 value, SetProp<Int16> setValue = null);
        void Prop(Int32 value, SetProp<Int32> setValue = null);
        void Prop(Int64 value, SetProp<Int64> setValue = null);
        void Prop(SByte value, SetProp<SByte> setValue = null);
        void Prop(Single value, SetProp<Single> setValue = null);
        void Prop(String value, SetProp<String> setValue = null);
        void Prop(UInt16 value, SetProp<UInt16> setValue = null);
        void Prop(UInt32 value, SetProp<UInt32> setValue = null);
        void Prop(UInt64 value, SetProp<UInt64> setValue = null);
        void Prop(TimeSpan value, SetProp<TimeSpan> setValue = null);
        void Prop(DateTime value, SetProp<DateTime> setValue = null);
        void Prop(TimeOnly value, SetProp<TimeOnly> setValue = null);
        void Prop(DateOnly value, SetProp<DateOnly> setValue = null);
        void Prop(DateTimeOffset value, SetProp<DateTimeOffset> setValue = null);
        void Prop(Guid value, SetProp<Guid> setValue = null);
        void Prop<T>(T value, SetProp<T> setValue = null);

        #endregion//Properties

        #region Soft references

        void LazyRef<T>(T value, SetProp<T> setValue) where T : class;

        void ParentField<T>(ref T value) where T : class;
        void ParentProp<T>(T value, SetProp<T> setValue) where T : class;

        void UnmanagedMemory(ref IntPtr data, ref int length, ref Action disposeAction);

        #endregion//Soft references



        void OnNew<T>(T value, bool replaceLast = true);
        Dictionary<String, Object> Context { get; }
        Stack<Object> Stack { get; }
    }

}//namespace SysWeaver.Inspection

