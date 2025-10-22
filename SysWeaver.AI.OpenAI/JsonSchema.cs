using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using SysWeaver.Docs;

namespace SysWeaver.AI
{
    public static class JsonSchema
    {


        static readonly ConcurrentDictionary<Tuple<Type, String, bool>, JsonSchemaParam> Cache = new();
        static readonly ConcurrentDictionary<Tuple<Type, String, bool>, String> StringCache = new();

        const String OptPrefix = "[Optional] ";

        public static JsonSchemaParam Get(Type type, bool isOutput, String description = null, bool isOptional = false)
        {
            var cache = Cache;
            if (isOptional)
                description = String.IsNullOrEmpty(description) ? OptPrefix : String.Join(" \n", OptPrefix, description);
            var key = Tuple.Create(type, description, isOutput);
            if (cache.TryGetValue(key, out var s))
                return s;
            s = BuildRec(type, isOutput, description);
            if (!cache.TryAdd(key, s))
                s = cache[key];
            return s;
        }


        static readonly JsonSerializerOptions SerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };


        public static String GetString(Type type, bool isOutput, String description = null)
        {
            var cache = StringCache;
            var key = Tuple.Create(type, description, isOutput);
            if (cache.TryGetValue(key, out var s))
                return s;
            var v = Get(type, isOutput, description);
            if (v == null)
                s = "null";
            else
                s = JsonSerializer.Serialize(v, SerOptions);
            if (!cache.TryAdd(key, s))
                s = cache[key];
            return s;
        }



        public static String ToString(JsonSchemaParam p)
            => p == null ? "null" : JsonSerializer.Serialize(p, SerOptions);

        const String TypeName_String = "string";
        const String TypeName_Integer = "integer";
        const String TypeName_Number = "number";
        const String TypeName_Boolean = "boolean";
        const String TypeName_Array = "array";
        const String TypeName_Object = "object";

        public static bool TryGetPrim(Type type, out String typeName) =>
            Prims.TryGetValue(type, out typeName);

        static readonly IReadOnlyDictionary<Type, String> Prims = new Dictionary<Type, String>
        {
            { typeof(Byte), TypeName_Integer },
            { typeof(UInt16), TypeName_Integer },
            { typeof(UInt32), TypeName_Integer },
            { typeof(UInt64), TypeName_Integer },

            { typeof(SByte), TypeName_Integer },
            { typeof(Int16), TypeName_Integer },
            { typeof(Int32), TypeName_Integer },
            { typeof(Int64), TypeName_Integer },

            { typeof(Single), TypeName_Number },
            { typeof(Double), TypeName_Number },
            { typeof(Decimal), TypeName_Number },

            { typeof(String), TypeName_String },
            { typeof(Boolean), TypeName_Boolean },

            { typeof(DateTime), TypeName_String + "|date-time" },
            { typeof(TimeOnly), TypeName_String + "|time" },
            { typeof(DateOnly), TypeName_String + "|date" },
            { typeof(Guid), TypeName_String + "|uuid" },
            { typeof(Uri), TypeName_String + "|uri" },

        }.Freeze();

        static String CleanDesc(String s)
        {
            if (String.IsNullOrEmpty(s))
                return null;
            s = s.Trim();
            if (s.Length <= 0)
                return null;
            if (!s.FastEndsWith("."))
                s += '.';
            return s;
        }

        static String MergeDesc(String desc, Type type)
        {
            desc = CleanDesc(desc);
            var td = CleanDesc(type.XmlDoc()?.Summary);
            if (type.IsEnum)
            {
                var tab = td == null ? "  " : "    ";
                var vals = type.GetEnumNames();
                var valvs = type.GetEnumValues();
                var vl = vals.Length;
                for (int i = 0; i < vl; ++ i)
                {
                    var name = vals[i];
                    var vd = CleanDesc(type.XmlDocEnum(name)?.Summary);
                    if (vd == null)
                        vals[i] = String.Concat(tab, name, " [Value: ", vals[i], ']');
                    else
                        vals[i] = String.Concat(tab, name, " [Value: ", vals[i], "] description: ", vd);
                }
                if (td == null)
                    td = "Enum values:\n" + String.Join('\n', vals);
                else
                    td = String.Join("  Enum values:\n", td, String.Join('\n', vals));
            }
            if (desc == null)
            {
                if (td == null)
                    return null;
                return "The type description:\n" + td;
            }
            if (td == null)
                return desc;
            return String.Join("\nThe type description:\n", desc, td);
        }
            
        static JsonSchemaParam BuildRec(Type type, bool isOutput, String description = null)
        {
            description = MergeDesc(description, type);
            if (Prims.TryGetValue(type, out var prim))
            {
                var pp = prim.Split('|');
                return new JsonSchemaParam
                {
                    Type = pp[0],
                    Description = description,
                    Format = pp.Length > 1 ? pp[1] : null,
                };
            }
            if (type.IsEnum)
            {
                return new JsonSchemaParam
                {
                    Type = TypeName_String,
                    Description = description,
                    Enum = type.GetEnumNames(),
                };
            }
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    throw new Exception("Only one dimensional arrays are supported!");
                var et = type.GetElementType();
                return new JsonSchemaParam
                {
                    Type = TypeName_Array,
                    Description = description,
                    Items = Get(et, isOutput, et.XmlDoc()?.Summary)
                };
            }
            if (type.IsPrimitive)
                throw new Exception("Unhandled primitve type " + type.FullName.ToQuoted());
            Dictionary<String, JsonSchemaParam> props = new Dictionary<string, JsonSchemaParam>(StringComparer.Ordinal);
            List<String> required = new List<string>();
            foreach (var x in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                bool isOpt;
                switch (x.MemberType)
                {
                    case MemberTypes.Field:
                        var fi = x as FieldInfo;
                        if (fi.GetCustomAttribute<OpenAiIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        if (!isOutput)
                        {
                            if (fi.IsInitOnly)
                                continue;
                        }
                        isOpt = fi.GetCustomAttribute<OpenAiOptionalAttribute>()?.Optional ?? false;
                        props.Add(x.Name, Get(fi.FieldType, isOutput, fi.XmlDoc()?.Summary, isOpt));
                        if (!isOpt)
                            required.Add(x.Name);
                        break;
                    case MemberTypes.Property:
                        var pi = x as PropertyInfo;
                        if (!pi.CanRead)
                            continue;
                        if (!(pi.GetGetMethod()?.IsPublic ?? false))
                            continue;
                        if (!isOutput)
                        {
                            if (!pi.CanWrite)
                                continue;
                            if (!(pi.GetSetMethod()?.IsPublic ?? false))
                                continue;
                        }
                        if (pi.GetCustomAttribute<OpenAiIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        isOpt = pi.GetCustomAttribute<OpenAiOptionalAttribute>()?.Optional ?? false;
                        props.Add(x.Name, Get(pi.PropertyType, isOutput, pi.XmlDoc()?.Summary, isOpt));
                        required.Add(x.Name);
                        break;
                }
            }
            return new JsonSchemaParam
            {
                Type = TypeName_Object,
                Description = description,
                Properties = props,
                Required = required.ToArray(),
            };
        }


    }

    public static class JsonSchemaCache
    {
        public static BinaryData GetBinaryDataParam(Type type, String paramName, bool isOutput, String description = null)
        {
            var cache = BinaryDataCache;
            var key = Tuple.Create(type, description);
            if (cache.TryGetValue(key, out var s))
                return s;
            var v = JsonSchema.Get(type, isOutput);
            if (v != null)
            {
                v = new JsonSchemaParam
                {
                    Type = "object",
                    Properties = new Dictionary<string, JsonSchemaParam>
                    {
                        { paramName, v }
                    },
                    Description = description,
                    Required = [paramName],
                };
                var ss = JsonSchema.ToString(v);
                s = BinaryData.FromString(ss);
            }
            if (!cache.TryAdd(key, s))
                s = cache[key];
            return s;
        }

        static readonly ConcurrentDictionary<Tuple<Type, String>, BinaryData> BinaryDataCache = new();

    }
}
