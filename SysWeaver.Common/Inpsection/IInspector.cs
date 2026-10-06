using System;
using System.Collections.Generic;


namespace SysWeaver.Inspection
{

    /// <summary>
    /// A setter used to assign a new value to a property (or any other value that can't be passed by reference).
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    /// <param name="newValue">The new value</param>
    public delegate void SetProp<T>(T newValue);

    /// <summary>
    /// An inspector visits the data of an object, the same interface is used for reading (the inspector assigns values), writing (the inspector reads values) and other operations (copy, compare etc).
    /// <see cref="IDescribable"/> objects describe themselves by calling the methods of an inspector for each member.
    /// </summary>
    /// <remarks>
    /// The Field methods take a reference that is read and/or assigned, the Prop methods take the current value and an optional setter that is called when the inspector assigns a new value
    /// (a null setter means that the value is only written, not read back).
    /// Implementations are typically not thread safe.
    /// </remarks>
    public interface IInspector : IDisposable
    {

        #region Fields

        /// <summary>
        /// Inspect a <see cref="Boolean"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Boolean value);
        /// <summary>
        /// Inspect a <see cref="Byte"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Byte value);
        /// <summary>
        /// Inspect a <see cref="Char"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Char value);
        /// <summary>
        /// Inspect a <see cref="Decimal"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Decimal value);
        /// <summary>
        /// Inspect a <see cref="Double"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Double value);
        /// <summary>
        /// Inspect a <see cref="Int16"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Int16 value);
        /// <summary>
        /// Inspect a <see cref="Int32"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Int32 value);
        /// <summary>
        /// Inspect a <see cref="Int64"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Int64 value);
        /// <summary>
        /// Inspect a <see cref="SByte"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref SByte value);
        /// <summary>
        /// Inspect a <see cref="Single"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Single value);
        /// <summary>
        /// Inspect a <see cref="String"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref String value);
        /// <summary>
        /// Inspect a <see cref="UInt16"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref UInt16 value);
        /// <summary>
        /// Inspect a <see cref="UInt32"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref UInt32 value);
        /// <summary>
        /// Inspect a <see cref="UInt64"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref UInt64 value);
        /// <summary>
        /// Inspect a <see cref="DateTime"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref DateTime value);
        /// <summary>
        /// Inspect a <see cref="TimeSpan"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref TimeSpan value);
        /// <summary>
        /// Inspect a <see cref="TimeOnly"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref TimeOnly value);
        /// <summary>
        /// Inspect a <see cref="DateOnly"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref DateOnly value);
        /// <summary>
        /// Inspect a <see cref="DateTimeOffset"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref DateTimeOffset value);
        /// <summary>
        /// Inspect a <see cref="Guid"/> field.
        /// </summary>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field(ref Guid value);
        /// <summary>
        /// Inspect a field of any type (objects, collections, enums etc are handled by the type handlers).
        /// </summary>
        /// <typeparam name="T">The field type</typeparam>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void Field<T>(ref T value);

        #endregion//Fields

        #region Properties

        /// <summary>
        /// Inspect a <see cref="Boolean"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Boolean value, SetProp<Boolean> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Byte"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Byte value, SetProp<Byte> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Char"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Char value, SetProp<Char> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Decimal"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Decimal value, SetProp<Decimal> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Double"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Double value, SetProp<Double> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Int16"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Int16 value, SetProp<Int16> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Int32"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Int32 value, SetProp<Int32> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Int64"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Int64 value, SetProp<Int64> setValue = null);
        /// <summary>
        /// Inspect a <see cref="SByte"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(SByte value, SetProp<SByte> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Single"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Single value, SetProp<Single> setValue = null);
        /// <summary>
        /// Inspect a <see cref="String"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(String value, SetProp<String> setValue = null);
        /// <summary>
        /// Inspect a <see cref="UInt16"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(UInt16 value, SetProp<UInt16> setValue = null);
        /// <summary>
        /// Inspect a <see cref="UInt32"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(UInt32 value, SetProp<UInt32> setValue = null);
        /// <summary>
        /// Inspect a <see cref="UInt64"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(UInt64 value, SetProp<UInt64> setValue = null);
        /// <summary>
        /// Inspect a <see cref="TimeSpan"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(TimeSpan value, SetProp<TimeSpan> setValue = null);
        /// <summary>
        /// Inspect a <see cref="DateTime"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(DateTime value, SetProp<DateTime> setValue = null);
        /// <summary>
        /// Inspect a <see cref="TimeOnly"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(TimeOnly value, SetProp<TimeOnly> setValue = null);
        /// <summary>
        /// Inspect a <see cref="DateOnly"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(DateOnly value, SetProp<DateOnly> setValue = null);
        /// <summary>
        /// Inspect a <see cref="DateTimeOffset"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(DateTimeOffset value, SetProp<DateTimeOffset> setValue = null);
        /// <summary>
        /// Inspect a <see cref="Guid"/> property.
        /// </summary>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop(Guid value, SetProp<Guid> setValue = null);
        /// <summary>
        /// Inspect a property of any type (objects, collections, enums etc are handled by the type handlers).
        /// </summary>
        /// <typeparam name="T">The property type</typeparam>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value, may be null</param>
        void Prop<T>(T value, SetProp<T> setValue = null);

        #endregion//Properties

        #region Soft references

        /// <summary>
        /// A soft (lazy) reference to another object, inspectors may ignore it (the binary inspectors does).
        /// </summary>
        /// <typeparam name="T">The reference type</typeparam>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value</param>
        void LazyRef<T>(T value, SetProp<T> setValue) where T : class;

        /// <summary>
        /// A reference to a parent object (an object higher up in the object graph), the value isn't stored.
        /// When reading, the closest object on the <see cref="Stack"/> that is assignable to <typeparamref name="T"/> is assigned (or null).
        /// </summary>
        /// <typeparam name="T">The parent type</typeparam>
        /// <param name="value">The field value, may be assigned by the inspector</param>
        void ParentField<T>(ref T value) where T : class;
        /// <summary>
        /// A reference to a parent object (an object higher up in the object graph), the value isn't stored.
        /// When reading, the closest object on the <see cref="Stack"/> that is assignable to <typeparamref name="T"/> is passed to the setter (or null).
        /// </summary>
        /// <typeparam name="T">The parent type</typeparam>
        /// <param name="value">The current value</param>
        /// <param name="setValue">Called if the inspector assigns a new value</param>
        void ParentProp<T>(T value, SetProp<T> setValue) where T : class;

        /// <summary>
        /// Inspect a block of unmanaged memory.
        /// </summary>
        /// <param name="data">Pointer to the memory, may be assigned by the inspector</param>
        /// <param name="length">The number of bytes, may be assigned by the inspector</param>
        /// <param name="disposeAction">The action that frees the memory, may be assigned by the inspector (when it allocates the memory)</param>
        void UnmanagedMemory(ref IntPtr data, ref int length, ref Action disposeAction);

        #endregion//Soft references



        /// <summary>
        /// Used by the type handlers to tell a reading inspector that a new object instance has been created (so that it can be referenced and used as a parent).
        /// Not intended to be called by <see cref="IDescribable"/> implementations.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="value">The new instance</param>
        /// <param name="replaceLast">True to replace the last registered object (a placeholder), false to add it</param>
        void OnNew<T>(T value, bool replaceLast = true);
        /// <summary>
        /// User defined context values (can be used to pass information to the <see cref="IDescribable"/> implementations)
        /// </summary>
        Dictionary<String, Object> Context { get; }
        /// <summary>
        /// The stack of objects currently being inspected (the top is the innermost object), used to resolve parent references
        /// </summary>
        Stack<Object> Stack { get; }
    }

}//namespace SysWeaver.Inspection

