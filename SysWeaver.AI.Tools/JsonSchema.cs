using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using SysWeaver.Docs;

namespace SysWeaver.AI
{
    /// <summary>
    /// Builds JSON schemas (as <see cref="JsonSchemaParam"/>) from .NET types, used to describe tool parameters to AI models
    /// </summary>
    public static class JsonSchema
    {


        //  Value tuple keys, no allocation per lookup (same equality as the Tuple keys)
        static readonly ConcurrentDictionary<(Type, String, bool), JsonSchemaParam> Cache = new();
        static readonly ConcurrentDictionary<(Type, String, bool), String> StringCache = new();

        const String OptPrefix = "[Optional] ";

        /// <summary>
        /// Get the (cached) schema of a type
        /// </summary>
        /// <param name="type">The type</param>
        /// <param name="isOutput">True if the schema describes a return value, false for an argument</param>
        /// <param name="description">Optional description of the value</param>
        /// <param name="isOptional">If true, the description is prefixed with "[Optional]"</param>
        /// <returns>The schema</returns>
        public static JsonSchemaParam Get(Type type, bool isOutput, String description = null, bool isOptional = false)
        {
            //  A nullable value type (ex: DateTime?) has the schema of the value type (else it's described as an object with HasValue and Value)
            type = Nullable.GetUnderlyingType(type) ?? type;
            var cache = Cache;
            if (isOptional)
                description = String.IsNullOrEmpty(description) ? OptPrefix : String.Join(" \n", OptPrefix, description);
            var key = (type, description, isOutput);
            if (cache.TryGetValue(key, out var s))
                return s;
            //  A schema built inside another one may have unexpanded parts (a recursive type), only complete (top level) schemas are cached
            var nested = (Building?.Count ?? 0) > 0;
            s = BuildRec(type, isOutput, description);
            if (nested)
                return s;
            if (!cache.TryAdd(key, s))
                s = cache[key];
            return s;
        }


        static readonly JsonSerializerOptions SerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };


        static readonly JsonSerializerOptions SerVerboseOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
        };

        /// <summary>
        /// Get the (cached) schema of a type as indented JSON
        /// </summary>
        /// <param name="type">The type</param>
        /// <param name="isOutput">True if the schema describes a return value, false for an argument</param>
        /// <param name="description">Optional description of the value</param>
        /// <returns>The JSON schema, or "null" if the type has no schema</returns>
        public static String GetString(Type type, bool isOutput, String description = null)
        {
            var cache = StringCache;
            var key = (type, description, isOutput);
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



        /// <summary>
        /// Serialize a schema to JSON
        /// </summary>
        /// <param name="p">The schema</param>
        /// <param name="verbose">True to use the verbose serializer options</param>
        /// <returns>The JSON, or "null" if the schema is null</returns>
        public static String ToString(JsonSchemaParam p, bool verbose = false)
            => p == null ? "null" : JsonSerializer.Serialize(p, verbose ? SerVerboseOptions : SerOptions);

        const String TypeName_String = "string";
        const String TypeName_Integer = "integer";
        const String TypeName_Number = "number";
        const String TypeName_Boolean = "boolean";
        const String TypeName_Array = "array";
        const String TypeName_Object = "object";

        /// <summary>
        /// Try to get the JSON schema type name of a primitive type
        /// </summary>
        /// <param name="type">The type</param>
        /// <param name="typeName">The schema type name (ex: "string", "integer"), if found</param>
        /// <returns>True if the type is a primitive type</returns>
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
            
        /// <summary>
        /// The object types being built on this thread (a type that contains itself, directly or through other types, would recurse forever)
        /// </summary>
        [ThreadStatic]
        static HashSet<Type> Building;

        /// <summary>
        /// The nesting depth of the object being built on this thread
        /// </summary>
        [ThreadStatic]
        static int Depth;

        /// <summary>
        /// The maximum nesting depth of a schema, deeper objects are described as plain objects (ex: generic types that nest themselves in new types)
        /// </summary>
        const int MaxDepth = 24;

        /// <summary>
        /// The schema of an object that isn't expanded (a recursive reference, or too deep): a plain object, the description names the type
        /// </summary>
        static JsonSchemaParam Unexpanded(Type type, String description, String why)
        {
            var note = String.Concat("An object of the type ", type.CleanTypename().ToQuoted(), " (", why, ").");
            return new JsonSchemaParam
            {
                Type = TypeName_Object,
                Description = description == null ? note : String.Concat(description, "\n", note),
            };
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
                throw new Exception("Unhandled primitve type " + type.CleanTypename().ToQuoted());
            var building = Building ??= new HashSet<Type>();
            if (building.Contains(type))
                return Unexpanded(type, description, "the same structure as the enclosing object of that type, it's recursive");
            if (Depth >= MaxDepth)
                return Unexpanded(type, description, "nested too deep to describe");
            building.Add(type);
            ++Depth;
            try
            {
                return BuildObject(type, isOutput, description);
            }
            finally
            {
                --Depth;
                building.Remove(type);
            }
        }

        /// <summary>
        /// Check if a member may be left out (it's not in the required list, and its description starts with "[Optional]"):
        /// an AiOptionalAttribute decides if present, else a nullable value type (ex: DateTime?) or a member with an EditAllowNullAttribute is optional
        /// </summary>
        /// <param name="m">The field or property</param>
        /// <param name="type">The type of the member</param>
        /// <returns>True if the member is optional</returns>
        static bool IsOptional(MemberInfo m, Type type)
        {
            var o = m.GetCustomAttribute<AiOptionalAttribute>();
            if (o != null)
                return o.Optional;
            if (Nullable.GetUnderlyingType(type) != null)
                return true;
            return m.GetCustomAttribute<EditAllowNullAttribute>()?.AllowNull ?? false;
        }

        static JsonSchemaParam BuildObject(Type type, bool isOutput, String description)
        {
            Dictionary<String, JsonSchemaParam> props = new Dictionary<string, JsonSchemaParam>(StringComparer.Ordinal);
            List<String> required = new List<string>();
            foreach (var x in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                bool isOpt;
                switch (x.MemberType)
                {
                    case MemberTypes.Field:
                        var fi = x as FieldInfo;
                        if (fi.GetCustomAttribute<AiIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        if (!isOutput)
                        {
                            if (fi.IsInitOnly)
                                continue;
                        }
                        isOpt = IsOptional(fi, fi.FieldType);
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
                        if (pi.GetCustomAttribute<AiIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        isOpt = IsOptional(pi, pi.PropertyType);
                        props.Add(x.Name, Get(pi.PropertyType, isOutput, pi.XmlDoc()?.Summary, isOpt));
                        if (!isOpt)
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

    /// <summary>
    /// A cache of tool parameter schemas
    /// </summary>
    public static class JsonSchemaCache
    {
        /// <summary>
        /// Get the (cached) schema of a tool parameter, as an object schema with a single (required) property
        /// </summary>
        /// <param name="type">The type of the parameter</param>
        /// <param name="paramName">The name of the property holding the parameter</param>
        /// <param name="isOutput">True if the schema describes a return value, false for an argument</param>
        /// <param name="description">Optional description of the parameter</param>
        /// <returns>The JSON schema, or null if the type has no schema</returns>
        public static BinaryData GetBinaryDataParam(Type type, String paramName, bool isOutput, String description = null)
        {
            var cache = BinaryDataCache;
            //  The parameter name and the direction are part of the schema
            var key = (type, paramName, isOutput, description);
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

        static readonly ConcurrentDictionary<(Type, String, bool, String), BinaryData> BinaryDataCache = new();

    }
}
