using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SysWeaver.AI
{
    /// <summary>
    /// Built in Gemma 4 chat template (llama.cpp can't run jinja templates and doesn't know the Gemma 4 format).
    /// Tools are declared, called and responded to using the native Gemma 4 format.
    /// </summary>
    static class LlamaSharpGemma4
    {
        /// <summary>
        /// Name of the built in Gemma 4 template (can be used as the ChatTemplate)
        /// </summary>
        public const String Name = "gemma4";

        /// <summary>
        /// Stop sequences, end of a turn or the model expecting a tool response
        /// </summary>
        public static readonly String[] AntiPrompts = [TurnEnd, ToolResponseStart];

        /// <summary>
        /// String delimiter used in tool declarations, calls and responses
        /// </summary>
        const String Q = "<|\"|>";

        const String TurnEnd = "<turn|>";
        public const String ToolCallStart = "<|tool_call>";
        public const String ToolCallEnd = "<tool_call|>";
        public const String ToolResponseStart = "<|tool_response>";
        const String ToolResponseEnd = "<tool_response|>";
        const String CallPrefix = "call:";

        #region Prompt

        /// <summary>
        /// Format a conversation (ending with the start of a model turn, or inside a model turn after tool responses)
        /// </summary>
        /// <param name="systemPrompt">The system prompt, can be null</param>
        /// <param name="tools">The tools available to the model, can be null</param>
        /// <param name="messages">The messages</param>
        /// <param name="first">Index of the first message to include</param>
        /// <returns>The prompt (the BOS token is added by the tokenizer)</returns>
        public static String Format(String systemPrompt, AiTool[] tools, IReadOnlyList<LlamaSharpMessage> messages, int first)
        {
            var sb = new StringBuilder();
            var haveTools = (tools?.Length ?? 0) > 0;
            if (haveTools || (!String.IsNullOrEmpty(systemPrompt)))
            {
                sb.Append("<|turn>system\n");
                if (systemPrompt != null)
                    sb.Append(systemPrompt.Trim());
                if (haveTools)
                    foreach (var x in tools)
                        sb.Append("<|tool>").Append(GetDeclaration(x)).Append("<tool|>");
                sb.Append(TurnEnd).Append('\n');
            }
            //  Tool calls, tool responses and the following model response are all part of the same model turn
            bool inModelTurn = false;
            var ml = messages.Count;
            for (int i = first; i < ml; ++i)
            {
                var m = messages[i];
                var c = m.Content ?? "";
                if (m.IsToolResponse || (m.Role == LlamaSharpModel.RoleAssistant))
                {
                    if (!inModelTurn)
                        sb.Append("<|turn>model\n");
                    inModelTurn = true;
                    if (m.IsToolResponse)
                    {
                        sb.Append(c);
                        continue;
                    }
                    sb.Append(c.Trim());
                    if (m.IsToolCall)
                        continue;
                    sb.Append(TurnEnd).Append('\n');
                    inModelTurn = false;
                    continue;
                }
                if (inModelTurn)
                    sb.Append(TurnEnd).Append('\n');
                inModelTurn = false;
                sb.Append("<|turn>").Append(m.Role).Append('\n').Append(c.Trim()).Append(TurnEnd).Append('\n');
            }
            //  Start a model turn with an empty thought channel (thinking disabled)
            if (!inModelTurn)
                sb.Append("<|turn>model\n<|channel>thought\n<channel|>");
            return sb.ToString();
        }

        /// <summary>
        /// Create the content of a tool response message
        /// </summary>
        public static String CreateToolResponses(IReadOnlyList<AiFunctionCall> calls, String[] res)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < calls.Count; ++i)
                sb.Append(ToolResponseStart).Append("response:").Append(calls[i].Name).Append("{value:").Append(Q).Append(res[i]).Append(Q).Append('}').Append(ToolResponseEnd);
            return sb.ToString();
        }

        #endregion//Prompt

        #region Tool declaration

        sealed class Declaration
        {
            public Declaration(String text) => Text = text;
            public readonly String Text;
        }

        /// <summary>
        /// Get the Gemma 4 declaration of a tool
        /// </summary>
        static String GetDeclaration(AiTool tool)
            => tool.Tool.GetApiTool(f =>
            {
                var sb = new StringBuilder();
                sb.Append("declaration:").Append(f.FunctionName).Append("{description:").Append(Q).Append(f.FunctionDescription).Append(Q);
                var p = f.FunctionParameters;
                if (p != null)
                {
                    using var d = JsonDocument.Parse(p.ToString());
                    var r = d.RootElement;
                    if ((r.ValueKind == JsonValueKind.Object) && r.EnumerateObject().Any())
                    {
                        sb.Append(",parameters:{");
                        if (r.TryGetProperty("properties", out var props) && (props.ValueKind == JsonValueKind.Object) && props.EnumerateObject().Any())
                        {
                            sb.Append("properties:{");
                            FormatParameters(sb, props, false);
                            sb.Append("},");
                        }
                        if (TryGetNonEmptyArray(r, "required", out var req))
                        {
                            sb.Append("required:");
                            FormatArgument(sb, req, true);
                            sb.Append(',');
                        }
                        var type = GetType(r);
                        if (type.Length > 0)
                            sb.Append("type:").Append(Q).Append(type).Append(Q).Append('}');
                    }
                }
                sb.Append('}');
                return new Declaration(sb.ToString().Trim());
            }).Text;

        static readonly HashSet<String> StandardKeys = ["description", "type", "properties", "required", "nullable"];

        static IEnumerable<JsonProperty> Sorted(JsonElement e)
            => e.EnumerateObject().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

        static bool TryGetNonEmptyArray(JsonElement e, String name, out JsonElement value)
            => e.TryGetProperty(name, out value) && (value.ValueKind == JsonValueKind.Array) && (value.GetArrayLength() > 0);

        /// <summary>
        /// Get the (upper case) type of a schema, or an empty string
        /// </summary>
        static String GetType(JsonElement e)
        {
            if (!e.TryGetProperty("type", out var t))
                return "";
            if (t.ValueKind == JsonValueKind.String)
                return t.GetString().ToUpperInvariant();
            if (t.ValueKind == JsonValueKind.Array)
                foreach (var x in t.EnumerateArray())
                    if ((x.ValueKind == JsonValueKind.String) && (x.GetString() != "null"))
                        return x.GetString().ToUpperInvariant();
            return "";
        }

        /// <summary>
        /// Port of the format_parameters macro in the Gemma 4 template
        /// </summary>
        static void FormatParameters(StringBuilder sb, JsonElement properties, bool filterKeys)
        {
            bool foundFirst = false;
            foreach (var prop in Sorted(properties))
            {
                var key = prop.Name;
                var value = prop.Value;
                if (filterKeys && StandardKeys.Contains(key))
                    continue;
                if (value.ValueKind != JsonValueKind.Object)
                    continue;
                if (foundFirst)
                    sb.Append(',');
                foundFirst = true;
                sb.Append(key).Append(":{");
                bool addComma = false;
                void Comma()
                {
                    if (addComma)
                        sb.Append(',');
                    addComma = true;
                }
                if (value.TryGetProperty("description", out var desc) && (desc.ValueKind == JsonValueKind.String) && (desc.GetString().Length > 0))
                {
                    sb.Append("description:").Append(Q).Append(desc.GetString()).Append(Q);
                    addComma = true;
                }
                var type = GetType(value);
                if (type == "STRING")
                {
                    if (TryGetNonEmptyArray(value, "enum", out var en))
                    {
                        Comma();
                        sb.Append("enum:");
                        FormatArgument(sb, en, true);
                    }
                }
                else if (type == "ARRAY")
                {
                    if (value.TryGetProperty("items", out var items) && (items.ValueKind == JsonValueKind.Object) && items.EnumerateObject().Any())
                    {
                        Comma();
                        sb.Append("items:{");
                        bool itemsFirst = false;
                        foreach (var item in Sorted(items))
                        {
                            var iv = item.Value;
                            if (iv.ValueKind == JsonValueKind.Null)
                                continue;
                            if (itemsFirst)
                                sb.Append(',');
                            itemsFirst = true;
                            switch (item.Name)
                            {
                                case "properties":
                                    sb.Append("properties:{");
                                    if (iv.ValueKind == JsonValueKind.Object)
                                        FormatParameters(sb, iv, false);
                                    sb.Append('}');
                                    break;
                                case "required":
                                    sb.Append("required:");
                                    FormatArgument(sb, iv, true);
                                    break;
                                case "type":
                                    sb.Append("type:");
                                    if (iv.ValueKind == JsonValueKind.String)
                                        sb.Append(Q).Append(iv.GetString().ToUpperInvariant()).Append(Q);
                                    else
                                        FormatArgument(sb, iv, true, true);
                                    break;
                                default:
                                    sb.Append(item.Name).Append(':');
                                    FormatArgument(sb, iv, true);
                                    break;
                            }
                        }
                        sb.Append('}');
                    }
                }
                if (value.TryGetProperty("nullable", out var nullable) && (nullable.ValueKind == JsonValueKind.True))
                {
                    Comma();
                    sb.Append("nullable:true");
                }
                if (type == "OBJECT")
                {
                    Comma();
                    sb.Append("properties:{");
                    if (value.TryGetProperty("properties", out var props) && (props.ValueKind == JsonValueKind.Object))
                        FormatParameters(sb, props, false);
                    else
                        FormatParameters(sb, value, true);
                    sb.Append('}');
                    if (TryGetNonEmptyArray(value, "required", out var req))
                    {
                        Comma();
                        sb.Append("required:");
                        FormatArgument(sb, req, true);
                    }
                }
                Comma();
                sb.Append("type:").Append(Q).Append(type).Append(Q).Append('}');
            }
        }

        /// <summary>
        /// Port of the format_argument macro in the Gemma 4 template
        /// </summary>
        static void FormatArgument(StringBuilder sb, JsonElement e, bool escapeKeys, bool upper = false)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    sb.Append("null");
                    return;
                case JsonValueKind.String:
                    var s = e.GetString();
                    sb.Append(Q).Append(upper ? s.ToUpperInvariant() : s).Append(Q);
                    return;
                case JsonValueKind.True:
                    sb.Append("true");
                    return;
                case JsonValueKind.False:
                    sb.Append("false");
                    return;
                case JsonValueKind.Object:
                    sb.Append('{');
                    bool first = true;
                    foreach (var p in Sorted(e))
                    {
                        if (!first)
                            sb.Append(',');
                        first = false;
                        if (escapeKeys)
                            sb.Append(Q).Append(p.Name).Append(Q);
                        else
                            sb.Append(p.Name);
                        sb.Append(':');
                        FormatArgument(sb, p.Value, escapeKeys, upper);
                    }
                    sb.Append('}');
                    return;
                case JsonValueKind.Array:
                    sb.Append('[');
                    bool firstItem = true;
                    foreach (var x in e.EnumerateArray())
                    {
                        if (!firstItem)
                            sb.Append(',');
                        firstItem = false;
                        FormatArgument(sb, x, escapeKeys, upper);
                    }
                    sb.Append(']');
                    return;
                default:
                    sb.Append(e.GetRawText());
                    return;
            }
        }

        /// <summary>
        /// Format a tool call (as the model writes it), ex: "&lt;|tool_call&gt;call:name{key:&lt;|"|&gt;text&lt;|"|&gt;}&lt;tool_call|&gt;"
        /// </summary>
        /// <param name="name">The name of the function</param>
        /// <param name="jsonArgs">The arguments as a json object</param>
        /// <returns>The tool call</returns>
        public static String FormatCall(String name, String jsonArgs)
        {
            var sb = new StringBuilder();
            sb.Append(ToolCallStart).Append(CallPrefix).Append(name);
            try
            {
                using var d = JsonDocument.Parse(jsonArgs);
                var r = d.RootElement;
                if (r.ValueKind == JsonValueKind.Object)
                    FormatArgument(sb, r, false);
                else
                    sb.Append("{}");
            }
            catch
            {
                sb.Append("{}");
            }
            sb.Append(ToolCallEnd);
            return sb.ToString();
        }

        #endregion//Tool declaration

        #region Tool call parsing

        /// <summary>
        /// Parse a tool call, ex: "call:name{key:&lt;|"|&gt;text&lt;|"|&gt;,count:4}"
        /// </summary>
        /// <param name="call">The text between the tool call start and end tags</param>
        /// <param name="invalidName">The name to use if the function name can't be parsed</param>
        /// <param name="error">An error message (for the model) if the call couldn't be parsed, else null</param>
        /// <returns>The call, the arguments are converted to json</returns>
        public static AiFunctionCall ParseCall(String call, String invalidName, out String error)
        {
            error = null;
            call = call.Trim();
            if (call.StartsWith(CallPrefix, StringComparison.Ordinal))
                call = call.Substring(CallPrefix.Length);
            var b = call.IndexOf('{');
            var name = (b < 0 ? call : call.Substring(0, b)).Trim();
            if (name.Length <= 0)
            {
                error = "Error: The tool call doesn't have a function name.";
                return new AiFunctionCall(invalidName, BinaryData.FromString("{}"));
            }
            if (b < 0)
                return new AiFunctionCall(name, BinaryData.FromString("{}"));
            try
            {
                using var ms = new MemoryStream();
                using (var w = new Utf8JsonWriter(ms))
                {
                    int pos = b;
                    ParseValue(call, ref pos, w);
                }
                return new AiFunctionCall(name, BinaryData.FromBytes(ms.ToArray()));
            }
            catch (Exception ex)
            {
                error = String.Concat("Error: The arguments of the tool call couldn't be parsed (", ex.Message, ").");
                return new AiFunctionCall(name, BinaryData.FromString("{}"));
            }
        }

        static void SkipWhiteSpace(String s, ref int pos)
        {
            while ((pos < s.Length) && Char.IsWhiteSpace(s[pos]))
                ++pos;
        }

        static Exception ParseError(int pos) => new FormatException("Invalid tool call arguments at position " + pos.ToString());

        /// <summary>
        /// Read a json string (some models may use json strings instead of the Gemma 4 string delimiter)
        /// </summary>
        static String ReadJsonString(String s, ref int pos)
        {
            var start = pos;
            ++pos;
            while (pos < s.Length)
            {
                var c = s[pos];
                if (c == '\\')
                {
                    pos += 2;
                    continue;
                }
                ++pos;
                if (c == '"')
                    return JsonSerializer.Deserialize<String>(s.AsSpan(start, pos - start));
            }
            throw ParseError(start);
        }

        /// <summary>
        /// Read a string delimited by &lt;|"|&gt;, or a json string
        /// </summary>
        static bool TryReadString(String s, ref int pos, out String value)
        {
            if (String.CompareOrdinal(s, pos, Q, 0, Q.Length) == 0)
            {
                var start = pos + Q.Length;
                var end = s.IndexOf(Q, start, StringComparison.Ordinal);
                if (end < 0)
                    throw ParseError(pos);
                value = s.Substring(start, end - start);
                pos = end + Q.Length;
                return true;
            }
            if ((pos < s.Length) && (s[pos] == '"'))
            {
                value = ReadJsonString(s, ref pos);
                return true;
            }
            value = null;
            return false;
        }

        static void ParseValue(String s, ref int pos, Utf8JsonWriter w)
        {
            SkipWhiteSpace(s, ref pos);
            if (pos >= s.Length)
                throw ParseError(pos);
            if (TryReadString(s, ref pos, out var str))
            {
                w.WriteStringValue(str);
                return;
            }
            var c = s[pos];
            if (c == '{')
            {
                ++pos;
                w.WriteStartObject();
                SkipWhiteSpace(s, ref pos);
                if ((pos < s.Length) && (s[pos] == '}'))
                {
                    ++pos;
                    w.WriteEndObject();
                    return;
                }
                for (; ; )
                {
                    SkipWhiteSpace(s, ref pos);
                    if (!TryReadString(s, ref pos, out var key))
                    {
                        var e = s.IndexOf(':', pos);
                        if (e < 0)
                            throw ParseError(pos);
                        key = s.Substring(pos, e - pos).Trim();
                        pos = e;
                    }
                    SkipWhiteSpace(s, ref pos);
                    if ((pos >= s.Length) || (s[pos] != ':'))
                        throw ParseError(pos);
                    ++pos;
                    w.WritePropertyName(key);
                    ParseValue(s, ref pos, w);
                    SkipWhiteSpace(s, ref pos);
                    if (pos >= s.Length)
                        throw ParseError(pos);
                    c = s[pos++];
                    if (c == ',')
                    {
                        //  Allow a trailing comma
                        SkipWhiteSpace(s, ref pos);
                        if ((pos < s.Length) && (s[pos] == '}'))
                        {
                            ++pos;
                            break;
                        }
                        continue;
                    }
                    if (c == '}')
                        break;
                    throw ParseError(pos - 1);
                }
                w.WriteEndObject();
                return;
            }
            if (c == '[')
            {
                ++pos;
                w.WriteStartArray();
                SkipWhiteSpace(s, ref pos);
                if ((pos < s.Length) && (s[pos] == ']'))
                {
                    ++pos;
                    w.WriteEndArray();
                    return;
                }
                for (; ; )
                {
                    ParseValue(s, ref pos, w);
                    SkipWhiteSpace(s, ref pos);
                    if (pos >= s.Length)
                        throw ParseError(pos);
                    c = s[pos++];
                    if (c == ',')
                    {
                        //  Allow a trailing comma
                        SkipWhiteSpace(s, ref pos);
                        if ((pos < s.Length) && (s[pos] == ']'))
                        {
                            ++pos;
                            break;
                        }
                        continue;
                    }
                    if (c == ']')
                        break;
                    throw ParseError(pos - 1);
                }
                w.WriteEndArray();
                return;
            }
            //  A literal (number, bool or null), read until the next delimiter
            var b = pos;
            while ((pos < s.Length) && (s[pos] != ',') && (s[pos] != '}') && (s[pos] != ']'))
                ++pos;
            var lit = s.Substring(b, pos - b).Trim();
            switch (lit)
            {
                case "true":
                    w.WriteBooleanValue(true);
                    return;
                case "false":
                    w.WriteBooleanValue(false);
                    return;
                case "null":
                    w.WriteNullValue();
                    return;
            }
            if (Decimal.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                w.WriteNumberValue(d);
            else
                w.WriteStringValue(lit);
        }

        #endregion//Tool call parsing
    }
}
