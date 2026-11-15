using System;

namespace SysWeaver.Inspection.Implementation
{

    /// <summary>
    /// Inspectors must implement this interface, the methods are called by the compiled type handlers (<see cref="TypeHandler{T}"/>):
    /// Field_* / Prop_* for objects (reference types, by T or with another runtime type F), values (structs) and nullable values,
    /// Array_* for the lengths (Array_Begin, the inspector may change them when reading), byte array blocks and the levels of multi dimensional arrays.
    /// </summary>
    public interface IInspectorImplementation : IInspector
    {
        #region Object

        void Field_Object<T>(TypeHandler<T> context, ref T value);
        void Prop_Object<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet);

        void Field_TypedObject<T, F>(TypeHandler<T> context, ref T value);
        void Prop_TypedObject<T, F>(TypeHandler<T> context, ref T value, SetProp<T> onSet);

        #endregion // Object

        #region Value

        void Field_Value<T>(TypeHandler<T> context, ref T value);
        void Prop_Value<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet);

        void Field_NullableValue<T>(TypeHandler<T> context, ref T value);
        void Prop_NullableValue<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet);

        #endregion//Value

        #region Array

        void Array_Begin(int[] dimensions);

        /// <summary>
        /// The length of a one dimensional array (the same as <see cref="Array_Begin(int[])"/> with one dimension, without allocating the dimensions array),
        /// a reader sets the length that was read, a writer writes it.
        /// </summary>
        /// <param name="length">The length (-1 for a new array when reading)</param>
        void Array_Length(ref int length)
        {
            var d = new int[] { length };
            Array_Begin(d);
            length = d[0];
        }
        void Array_ByteArray(int length, ref Byte[] value);
        void Array_LevelUp(int rank);
        void Array_LevelDown(int rank);

        #endregion // Array


    }

}

