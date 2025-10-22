using System;
using System.Collections.Generic;
using SimpleStack.Orm;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    public static class DbSimpleStackCache<T>
    {
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

        public static readonly IReadOnlyDictionary<String, int> Ordinal = GetOrdinal();
    }


}
