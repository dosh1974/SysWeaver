using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SysWeaver.AI
{
    public sealed class JsonSchemaParam
    {
        public override string ToString() => String.Concat(Type, " [", Description, ']');

        [JsonPropertyName("type")]
        [JsonInclude]
        public String Type;

        [JsonPropertyName("format")]
        [JsonInclude]
        public String Format;

        [JsonPropertyName("properties")]
        [JsonInclude]
        public Dictionary<String, JsonSchemaParam> Properties;

        [JsonPropertyName("description")]
        [JsonInclude]
        public String Description;

        [JsonPropertyName("enum")]
        [JsonInclude]
        public String[] Enum;

        [JsonPropertyName("required")]
        [JsonInclude]
        public String[] Required;

/*        [JsonPropertyName("additionalProperties")]
        [JsonInclude]
        public bool AdditionalProperties = true;
*/
        [JsonPropertyName("items")]
        [JsonInclude]
        public JsonSchemaParam Items;

    }


}
