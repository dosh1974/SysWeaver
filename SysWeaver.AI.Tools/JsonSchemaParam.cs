using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SysWeaver.AI
{
    /// <summary>
    /// A JSON schema node (a subset of JSON schema, as used for AI tool parameters)
    /// </summary>
    public sealed class JsonSchemaParam
    {
        /// <inheritdoc />
        public override string ToString() => String.Concat(Type, " [", Description, ']');

        /// <summary>
        /// The JSON schema type (ex: "string", "integer", "number", "boolean", "array", "object")
        /// </summary>
        [JsonPropertyName("type")]
        [JsonInclude]
        public String Type;

        /// <summary>
        /// Optional format of the value (ex: "date-time")
        /// </summary>
        [JsonPropertyName("format")]
        [JsonInclude]
        public String Format;

        /// <summary>
        /// The properties of an object, by name
        /// </summary>
        [JsonPropertyName("properties")]
        [JsonInclude]
        public Dictionary<String, JsonSchemaParam> Properties;

        /// <summary>
        /// Description of the value
        /// </summary>
        [JsonPropertyName("description")]
        [JsonInclude]
        public String Description;

        /// <summary>
        /// The allowed values (for enums)
        /// </summary>
        [JsonPropertyName("enum")]
        [JsonInclude]
        public String[] Enum;

        /// <summary>
        /// The names of the required properties of an object
        /// </summary>
        [JsonPropertyName("required")]
        [JsonInclude]
        public String[] Required;

/*        [JsonPropertyName("additionalProperties")]
        [JsonInclude]
        public bool AdditionalProperties = true;
*/
        /// <summary>
        /// The schema of the items of an array
        /// </summary>
        [JsonPropertyName("items")]
        [JsonInclude]
        public JsonSchemaParam Items;

    }


}
