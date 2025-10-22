using System.Linq;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{
    public static class InspectorImplementationInfo<I> where I : IInspectorImplementation
    {
        public static readonly TypeInfo InspectorTypeInfo = typeof(I).GetTypeInfo();

        public static readonly MethodInfo ArrayLenghts = InspectorTypeInfo.GetDeclaredMethod("Array_Begin");
        public static readonly MethodInfo ArrayLevelUp = InspectorTypeInfo.GetDeclaredMethod("Array_LevelUp");
        public static readonly MethodInfo ArrayLevelDown = InspectorTypeInfo.GetDeclaredMethod("Array_LevelDown");
        public static readonly MethodInfo ArrayByteArray = InspectorTypeInfo.GetDeclaredMethod("Array_ByteArray");


        public static readonly MethodInfo Field_Object = InspectorTypeInfo.GetDeclaredMethods("Field_Object").FirstOrDefault();
        public static readonly MethodInfo Prop_Object = InspectorTypeInfo.GetDeclaredMethods("Prop_Object").FirstOrDefault();
        public static readonly MethodInfo Field_TypedObject = InspectorTypeInfo.GetDeclaredMethods("Field_TypedObject").FirstOrDefault();
        public static readonly MethodInfo Prop_TypedObject = InspectorTypeInfo.GetDeclaredMethods("Prop_TypedObject").FirstOrDefault();
        public static readonly MethodInfo Field_Value = InspectorTypeInfo.GetDeclaredMethods("Field_Value").FirstOrDefault();
        public static readonly MethodInfo Prop_Value = InspectorTypeInfo.GetDeclaredMethods("Prop_Value").FirstOrDefault();
        public static readonly MethodInfo Field_NullableValue = InspectorTypeInfo.GetDeclaredMethods("Field_NullableValue").FirstOrDefault();
        public static readonly MethodInfo Prop_NullableValue = InspectorTypeInfo.GetDeclaredMethods("Prop_NullableValue").FirstOrDefault();
        public static readonly MethodInfo Array_Begin = InspectorTypeInfo.GetDeclaredMethods("Array_Begin").FirstOrDefault();
        public static readonly MethodInfo Array_ByteArray = InspectorTypeInfo.GetDeclaredMethods("Array_ByteArray").FirstOrDefault();
        public static readonly MethodInfo Array_LevelUp = InspectorTypeInfo.GetDeclaredMethods("Array_LevelUp").FirstOrDefault();
        public static readonly MethodInfo Array_LevelDown = InspectorTypeInfo.GetDeclaredMethods("Array_LevelDown").FirstOrDefault();

    }

}

