using System;
using System.Collections.Generic;
using SimpleStack.Orm;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    /// <summary>
    /// Cached SimpleStack model information for a type
    /// </summary>
    /// <typeparam name="T">The model type</typeparam>
    public static class DbSimpleStackCache<T>
    {
        /// <summary>
        /// The SimpleStack model definition of T
        /// </summary>
        public static readonly ModelDefinition Model = ModelDefinition<T>.Definition;

        static IReadOnlyDictionary<String, int> GetOrdinal()
        {
            Dictionary<String, int> t = new Dictionary<string, int>(StringComparer.Ordinal);
            var p = Model.FieldDefinitions;
            var pl = p.Count;
            for (int i = 0; i < pl; ++ i)
            {
                var fi = p[i];
                t[fi.FieldName] = i;
            }
            return t.Freeze();
        }

        /// <summary>
        /// Maps field name to the index of the field in the model definition's field list
        /// </summary>
        public static readonly IReadOnlyDictionary<String, int> Ordinal = GetOrdinal();
    }


}
