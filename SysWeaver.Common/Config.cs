using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SysWeaver
{

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ConfigIgnoreAttribute : Attribute
    {
        public ConfigIgnoreAttribute(bool ignore = true)
        {
            Ignore = ignore;
        }
        public readonly bool Ignore;
    }


    public sealed class DefaultConfig
    {
        /// <summary>
        /// One or more paths to use for data (common to all users).
        /// Separate multiple path with semi colon (;).
        /// </summary>
        public String AllFolders { get; set; } = null;

        /// <summary>
        /// One or more paths to use for user specific data.
        /// Separate multiple path with semi colon (;).
        /// </summary>
        public String UserFolders { get; set; } = null;

    }

    /// <summary>
    /// Get configuration settings from the ApplicationName.Config.json file.
    /// Folders can be overridden using the key "FileHashFolders" in the ApplicationName.Config.json file.
    /// </summary>
    public static class Config
    {
        static Config()
        {
            var docType = TypeFinder.Get("SysWeaver.Docs.XmlDocExt, SysWeaver.Docs");
            if (docType != null)
            {
                var m = docType.GetMethod("XmlSummary", BindingFlags.Static | BindingFlags.Public, [typeof(FieldInfo)]);
                var p = Expression.Parameter(typeof(FieldInfo));
                GetField = Expression.Lambda<Func<FieldInfo, String>>(Expression.Call(m, p), p).Compile();
                m = docType.GetMethod("XmlSummary", BindingFlags.Static | BindingFlags.Public, [typeof(PropertyInfo)]);
                p = Expression.Parameter(typeof(PropertyInfo));
                GetProperty = Expression.Lambda<Func<PropertyInfo, String>>(Expression.Call(m, p), p).Compile();
            }else
            {
                GetField = x => null;
                GetProperty = x => null;
            }

            var keys = new Dictionary<string, Tuple<JsonValueKind, Object>>(StringComparer.Ordinal);
            //  System wide default config
            ReadConfig(keys, "DefaultSystemConfig.json", "This configuration is the defaults for all SysWeaver applications running on this system.\nThese settings can be overriden by the application in their '{exe}.Config.json' file.", typeof(DefaultConfig));
            //  Application config
            ReadConfig(keys, EnvInfo.ExecutableBase + ".Config.json");
            //  System wide forced config
            ReadConfig(keys, "ForcedSystemConfig.json", "This configuration is forced for all SysWeaver applications running on this system.\nThese settings can NOT be overriden by the application.\n\nMake sure to only include fields that you really want to enforce", typeof(DefaultConfig), true);

            Keys = keys.Freeze();
        }

        static readonly Func<FieldInfo, String> GetField;
        static readonly Func<PropertyInfo, String> GetProperty;


        static readonly ConcurrentDictionary<Type, String> TypeComments = new ConcurrentDictionary<Type, string>();


        public static void ApplyConfig(Type ct, object config, String filename, String comment = null)
        {
            var keys = new Dictionary<string, Tuple<JsonValueKind, Object>>(StringComparer.Ordinal);
            ReadConfig(keys, filename, comment, ct, true, true);
            foreach (var x in keys)
            {
                {
                    var d = ct.GetProperty(x.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (d != null)
                    {
                        if (!d.CanWrite)
                            continue;
                        if (d.GetCustomAttribute<ConfigIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        SetData(x.Value.Item1, x.Value.Item2, d.PropertyType, a =>
                            {
                                try
                                {
                                    d.SetValue(config, a);
                                    return true;
                                }
                                catch
                                {
                                    return false;
                                }
                            });
                        continue;
                    }
                }
                {
                    var d = ct.GetField(x.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (d != null)
                    {
                        if (d.IsInitOnly)
                            continue;
                        if (d.GetCustomAttribute<ConfigIgnoreAttribute>()?.Ignore ?? false)
                            continue;
                        SetData(x.Value.Item1, x.Value.Item2, d.FieldType, a =>
                            {
                                try
                                {
                                    d.SetValue(config, a);
                                    return true;
                                }
                                catch
                                {
                                    return false;
                                }
                            });
                        continue;
                    }
                }
            }
        }

        public static void ApplyConfig<T>(T config, String filename, String comment = null)
            => ApplyConfig(typeof(T), config, filename, comment);

        static void ReadConfig(Dictionary<string, Tuple<JsonValueKind, Object>> keys, String name, String comment = null, Type dataType = null, bool createEmpty = false, bool caseSensitive = false)
        {
            try
            {
                if (!Path.IsPathRooted(name))
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    name = Path.Combine(appData, "SysWeaver", name);
                }
                if (!File.Exists(name))
                {
                    if (comment == null)
                        return;
                    comment = String.Concat("/*\n * ", String.Join("\n * ", comment.Split('\n', StringSplitOptions.TrimEntries)), "\n*/\n\n");
                    String s;
                    if (createEmpty)
                    {
                        s = comment + "{\n\n\n}\n";
                    }else
                    {
                        var def = new DefaultConfig();
                        s = comment + JsonSerializer.Serialize(def, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                        });
                    }

                    if (dataType != null)
                    {
                        var cache = TypeComments;
                        var setters = Setters;
                        var arraySetters = ArraySetters;
                        if (!cache.TryGetValue(dataType, out var typeComment))
                        {
                 

                            StringBuilder b = new StringBuilder();
                            var gf = GetField;
                            var gp = GetProperty;

                            void Add(String name, Type type, String com, bool haveDefault, Object def)
                            {
                                if (!(type.IsArray ? arraySetters : setters).ContainsKey(type))
                                    return;
                                b.Append(" * \"").Append(name).Append("\" : ").Append(type.Name);
                                if (haveDefault && (def != null))
                                {
                                    try
                                    {
                                        var x = JsonSerializer.Serialize(def);
                                        b.Append(" = ").Append(x);
                                    }
                                    catch
                                    {
                                    }
                                }
                                b.AppendLine();
                                if (com != null)
                                {
                                    var l = " *" + new string(' ', name.Length + 6);
                                    var cl = com.Split('\n', StringSplitOptions.TrimEntries);
                                    foreach (var x in cl)
                                        b.Append(l).AppendLine(x);
                                }
                                b.AppendLine(" *");
                            }

                            Object def = null;
                            try
                            {
                                def = Activator.CreateInstance(dataType);
                            }
                            catch
                            {
                            }
                            foreach (var m in dataType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                            {
                                {
                                    var d = m as FieldInfo;
                                    if (d != null)
                                    {
                                        if (d.IsInitOnly)
                                            continue;
                                        if (d.GetCustomAttribute<ConfigIgnoreAttribute>()?.Ignore ?? false)
                                            continue;
                                        bool have = def != null;
                                        Object val = null;
                                        try
                                        {
                                            if (have)
                                                val = d.GetValue(def);
                                        }
                                        catch
                                        {
                                            have = false;
                                        }
                                        Add(d.Name, d.FieldType, gf(d), have, val);
                                        continue;
                                    }
                                }
                                {
                                    var d = m as PropertyInfo;
                                    if (d != null)
                                    {
                                        if (!d.CanWrite)
                                            continue;
                                        if (d.GetCustomAttribute<ConfigIgnoreAttribute>()?.Ignore ?? false)
                                            continue;
                                        bool have = def != null;
                                        Object val = null;
                                        try
                                        {
                                            if (have)
                                                val = d.GetValue(def);
                                        }
                                        catch
                                        {
                                            have = false;
                                        }
                                        Add(d.Name, d.PropertyType, gp(d), have, val);
                                        continue;
                                    }
                                }
                            }
                            typeComment = b.ToString();
                            cache[dataType] = typeComment;
                        }
                        if (typeComment.Length > 0)
                            s = String.Concat(s, "\n\n/* Members that can be overridden\n * ==============================\n\n", typeComment, "*/");
                    }
                    try
                    {
                        var dir = Path.GetDirectoryName(name);
                        PathExt.EnsureFolderExist(dir);
                        PathExt.AllowAllAccess(dir);
                        File.WriteAllText(name, s);
                    }
                    catch
                    {
                    }
                    return;
                }
                var data = File.ReadAllText(name);
                var options = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };
                using (JsonDocument document = JsonDocument.Parse(data, options))
                {
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                    {
                        try
                        {
                            var key = property.Name;
                            if (!caseSensitive)
                                key = key.FastToLower();
                            var val = property.Value;
                            var prop = ParseValue(val);
                            if (prop == null)
                            {
                                if (val.ValueKind == JsonValueKind.Array)
                                {
                                    var len = val.GetArrayLength();
                                    var ar = new Tuple<JsonValueKind, Object>[len];
                                    for (int i = 0; i < len; ++i)
                                    {
                                        var t = ParseValue(val[i]);
                                        if (t == null)
                                        {
                                            ar = null;
                                            break;
                                        }
                                        ar[i] = t;
                                    }
                                    if (ar != null)
                                        prop = Tuple.Create(JsonValueKind.Array, (Object)ar);
                                }
                            }
                            if (prop != null)
                                keys[key] = prop;
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("Config - Warning: Failed parsing key " + property.Name.ToQuoted() + " in " + name.ToFilename() + ", excpetion:");
                            Console.WriteLine(ex2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Config - Warning: Failed to read config file " + name.ToFilename() + ", excpetion:");
                Console.WriteLine(ex);
            }
            finally
            {
            }
        }

        static Tuple<JsonValueKind, Object> ParseValue(JsonElement val)
        {
            var k = val.ValueKind;
            switch (k)
            {
                case JsonValueKind.String:
                    return Tuple.Create(k, (Object)val.GetString());
                case JsonValueKind.False:
                    return Tuple.Create(k, (Object)false);
                case JsonValueKind.True:
                    return Tuple.Create(k, (Object)true);
                case JsonValueKind.Null:
                    return Tuple.Create(k, (Object)null);
                case JsonValueKind.Number:
                    var text = val.GetRawText().Trim();
                    var signed = text.StartsWith("-");
                    var isDec = text.Contains('.') || text.Contains('e');
                    if (text.Contains('.') || text.Contains('e'))
                        return Tuple.Create(k, (Object)val.GetDecimal());
                    if (text.StartsWith("-"))
                        return Tuple.Create(k, (Object)val.GetInt64());
                    return Tuple.Create(k, (Object)val.GetUInt64());
            }
            return null;
        }

        static readonly IReadOnlyDictionary<String, Tuple<JsonValueKind, Object>> Keys;

        static bool SetData(JsonValueKind kind, Object data, Type type, Func<Object, bool> var)
            => (type.IsArray ? ArraySetters : Setters).TryGetValue(type, out var s) ? s(kind, data, var) : false;

        static readonly IReadOnlyDictionary<Type, Func<JsonValueKind, Object, Func<Object, bool>, bool>> Setters = new Dictionary<Type, Func<JsonValueKind, Object, Func<Object, bool>, bool>>
        {
            { typeof(Byte), (k, d, s) => TryParseUInt32(k, d, out var v) && s((Byte)v) },
            { typeof(SByte), (k, d, s) => TryParseInt32(k, d, out var v) && s((SByte)v) },
            { typeof(UInt16), (k, d, s) => TryParseUInt32(k, d, out var v) && s((UInt16)v) },
            { typeof(Int16), (k, d, s) => TryParseInt32(k, d, out var v) && s((Int16)v) },
            { typeof(UInt32), (k, d, s) => TryParseUInt32(k, d, out var v) && s(v) },
            { typeof(Int32), (k, d, s) => TryParseInt32(k, d, out var v) && s(v) },
            { typeof(UInt64), (k, d, s) => TryParseUInt64(k, d, out var v) && s(v) },
            { typeof(Int64), (k, d, s) => TryParseInt64(k, d, out var v) && s(v) },
            { typeof(Single), (k, d, s) => TryParseSingle(k, d, out var v) && s(v) },
            { typeof(Double), (k, d, s) => TryParseDouble(k, d, out var v) && s(v) },
            { typeof(Decimal), (k, d, s) => TryParseDecimal(k, d, out var v) && s(v) },
            { typeof(String), (k, d, s) => TryParseString(k, d, out var v) && s(v) },
            { typeof(Boolean), (k, d, s) => TryParseBoolean(k, d, out var v) && s(v) },
        }.Freeze();


        static readonly IReadOnlyDictionary<Type, Func<JsonValueKind, Object, Func<Object, bool>, bool>> ArraySetters = new Dictionary<Type, Func<JsonValueKind, Object, Func<Object, bool>, bool>>
        {
            { typeof(Byte[]), (k, d, s) => TryParseArray<Byte>(k, d, out var v) && s(v) },
            { typeof(SByte[]), (k, d, s) => TryParseArray<SByte>(k, d, out var v) && s(v) },
            { typeof(UInt16[]), (k, d, s) => TryParseArray<UInt16>(k, d, out var v) && s(v) },
            { typeof(Int16[]), (k, d, s) => TryParseArray<Int16>(k, d, out var v) && s(v) },
            { typeof(UInt32[]), (k, d, s) => TryParseArray<UInt32>(k, d, out var v) && s(v) },
            { typeof(Int32[]), (k, d, s) => TryParseArray<Int32>(k, d, out var v) && s(v) },
            { typeof(UInt64[]), (k, d, s) => TryParseArray<UInt64>(k, d, out var v) && s(v) },
            { typeof(Int64[]), (k, d, s) => TryParseArray<Int64>(k, d, out var v) && s(v) },
            { typeof(Single[]), (k, d, s) => TryParseArray<Single>(k, d, out var v) && s(v) },
            { typeof(Double[]), (k, d, s) => TryParseArray<Double>(k, d, out var v) && s(v) },
            { typeof(Decimal[]), (k, d, s) => TryParseArray<Decimal>(k, d, out var v) && s(v) },
            { typeof(String[]), (k, d, s) => TryParseArray<String>(k, d, out var v) && s(v) },
            { typeof(Boolean[]), (k, d, s) => TryParseArray<Boolean>(k, d, out var v) && s(v) },
        }.Freeze();
        static bool TryParseString(JsonValueKind kind, Object data, out String value)
        {
            value = kind == JsonValueKind.Null ? null : data.ToString();
            return true;
        }
        static bool TryParseBoolean(JsonValueKind kind, Object data, out Boolean value)
        {
            if (kind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (kind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
            value = default;
            return false;
        }
        static bool TryParseInt32(JsonValueKind kind, Object data, out Int32 value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? Int32.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                if (v != Math.Round(v))
                    return false;
                if (v > Int32.MaxValue)
                    return false;
                if (v < Int32.MinValue)
                    return false;
                value = (Int32)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                if (v > Int32.MaxValue)
                    return false;
                if (v < Int32.MinValue)
                    return false;
                value = (Int32)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                if (v > Int32.MaxValue)
                    return false;
                value = (Int32)v;
                return true;
            }
            return false;
        }
        static bool TryParseUInt32(JsonValueKind kind, Object data, out UInt32 value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? UInt32.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                if (v != Math.Round(v))
                    return false;
                if (v > UInt32.MaxValue)
                    return false;
                if (v < UInt32.MinValue)
                    return false;
                value = (UInt32)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                if (v > UInt32.MaxValue)
                    return false;
                if (v < UInt32.MinValue)
                    return false;
                value = (UInt32)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                if (v > UInt32.MaxValue)
                    return false;
                value = (UInt32)v;
                return true;
            }
            return false;
        }
        static bool TryParseInt64(JsonValueKind kind, Object data, out Int64 value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? Int64.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                if (v != Math.Round(v))
                    return false;
                if (v > Int64.MaxValue)
                    return false;
                if (v < Int64.MinValue)
                    return false;
                value = (Int64)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                value = (Int64)data;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                if (v > Int64.MaxValue)
                    return false;
                value = (Int64)v;
                return true;
            }
            return false;

        }
        static bool TryParseUInt64(JsonValueKind kind, Object data, out UInt64 value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? UInt64.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                if (v != Math.Round(v))
                    return false;
                if (v > UInt64.MaxValue)
                    return false;
                if (v < UInt64.MinValue)
                    return false;
                value = (UInt64)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                if (v < 0)
                    return false;
                value = (UInt64)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                value = (UInt64)data;
                return true;
            }
            return false;
        }
        static bool TryParseDecimal(JsonValueKind kind, Object data, out Decimal value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? Decimal.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                value = (Decimal)data;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                value = (Decimal)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                value = (Decimal)data;
                return true;
            }
            return false;

        }
        static bool TryParseDouble(JsonValueKind kind, Object data, out Double value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? Double.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                value = (Double)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                value = (Double)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                value = (Double)v;
                return true;
            }
            return false;

        }
        static bool TryParseSingle(JsonValueKind kind, Object data, out Single value)
        {
            value = default;
            if (kind != JsonValueKind.Number)
                return kind == JsonValueKind.String ? Single.TryParse((data as String)?.Trim() ?? "", out value) : false;
            var valType = data.GetType();
            if (valType == typeof(Decimal))
            {
                var v = (Decimal)data;
                value = (Single)v;
                return true;
            }
            if (valType == typeof(Int64))
            {
                var v = (Int64)data;
                value = (Single)v;
                return true;
            }
            if (valType == typeof(UInt64))
            {
                var v = (UInt64)data;
                value = (Single)v;
                return true;
            }
            return false;
        }

        static bool TryParseArray<T>(JsonValueKind kind, Object data, out T[] value)
        {
            value = default;
            var et = typeof(T);
            if (!Setters.ContainsKey(et))
                return false;
            if (kind == JsonValueKind.Null)
                return true;
            if (kind != JsonValueKind.Array)
                return false;
            var vars = (Tuple<JsonValueKind, Object>[])data;
            var l = vars.Length;
            var dest = new T[l];
            for (int i = 0; i < l; ++ i)
            {
                var e = vars[i];
                SetData(e.Item1, e.Item2, et, val => {
                    dest[i] = (T)val;
                    return true;
                });
            }
            value = dest;
            return true;
        }


        public static bool TryGetString(String key, out String value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseString(x.Item1, x.Item2, out value);
        }

        public static String GetString(String key, String onFail = default) => TryGetString(key, out var v) ? v : onFail;

        public static bool TryGetBoolean(String key, out Boolean value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseBoolean(x.Item1, x.Item2, out value);
        }

        public static Boolean GetBoolean(String key, Boolean onFail = default) => TryGetBoolean(key, out var v) ? v : onFail;

        public static bool TryGetInt32(String key, out Int32 value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseInt32(x.Item1, x.Item2, out value);
        }

        public static Int32 GetInt32(String key, Int32 onFail = default) => TryGetInt32(key, out var v) ? v : onFail;

        public static bool TryGetUInt32(String key, out UInt32 value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseUInt32(x.Item1, x.Item2, out value);
        }

        public static UInt32 GetUInt32(String key, UInt32 onFail = default) => TryGetUInt32(key, out var v) ? v : onFail;

        public static bool TryGetInt64(String key, out Int64 value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseInt64(x.Item1, x.Item2, out value);
        }

        public static Int64 GetInt64(String key, Int64 onFail = default) => TryGetInt64(key, out var v) ? v : onFail;

        public static bool TryGetUInt64(String key, out UInt64 value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseUInt64(x.Item1, x.Item2, out value);
        }

        public static UInt64 GetUInt64(String key, UInt64 onFail = default) => TryGetUInt64(key, out var v) ? v : onFail;

        public static bool TryGetDecimal(String key, out Decimal value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseDecimal(x.Item1, x.Item2, out value);
        }

        public static Decimal GetDecimal(String key, Decimal onFail = default) => TryGetDecimal(key, out var v) ? v : onFail;

        public static bool TryGetDouble(String key, out Double value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseDouble(x.Item1, x.Item2, out value);
        }

        public static Double GetDouble(String key, Double onFail = default) => TryGetDouble(key, out var v) ? v : onFail;

        public static bool TryGetSingle(String key, out Single value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseSingle(x.Item1, x.Item2, out value);
        }

        public static Single GetSingle(String key, Single onFail = default) => TryGetSingle(key, out var v) ? v : onFail;

        public static bool TryGetArray<T>(String key, out T[] value)
        {
            value = default;
            if (!Keys.TryGetValue(key.FastToLower(), out var x))
                return false;
            return TryParseArray<T>(x.Item1, x.Item2, out value);
        }

        public static T[] GetArray<T>(String key, T[] onFail = default) => TryGetArray<T>(key, out var v) ? v : onFail;



    }

}



