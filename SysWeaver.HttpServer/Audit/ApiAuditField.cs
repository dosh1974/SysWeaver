using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SysWeaver.Net
{

    /// <summary>
    /// The types of audit fields
    /// </summary>
    public enum ApiAuditFieldTypes
    {
        /// <summary>
        /// Some text
        /// </summary>
        Text,
        /// <summary>
        /// A number
        /// </summary>
        Number,
        /// <summary>
        /// An <see cref="ApiAuditStates"/> value (compared as a number, the text is the name)
        /// </summary>
        State,
        /// <summary>
        /// A time stamp (UTC)
        /// </summary>
        Time,
    }

    /// <summary>
    /// A field of <see cref="ApiAuditEntry"/> that can be used in queries
    /// </summary>
    public sealed class ApiAuditField
    {
        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>
        /// Name of the member in <see cref="ApiAuditEntry"/>
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The type of the values
        /// </summary>
        public readonly ApiAuditFieldTypes Type;

        /// <summary>
        /// Get the text of the field, never null (an empty text if the value is missing).
        /// </summary>
        public readonly Func<ApiAuditEntry, String> GetText;

        /// <summary>
        /// Get the number of a <see cref="ApiAuditFieldTypes.Number"/> or <see cref="ApiAuditFieldTypes.State"/> field, the returned value is null if the value is missing.
        /// </summary>
        public readonly Func<ApiAuditEntry, double?> GetNumber;

        /// <summary>
        /// Get the time of a <see cref="ApiAuditFieldTypes.Time"/> field, the returned value is null if the value is missing.
        /// </summary>
        public readonly Func<ApiAuditEntry, DateTime?> GetTime;

        /// <summary>
        /// The format of the text of a time (invariant culture)
        /// </summary>
        public const String TimeFormat = "yyyy-MM-dd HH:mm:ss";

        ApiAuditField(String name, ApiAuditFieldTypes type, Func<ApiAuditEntry, String> text, Func<ApiAuditEntry, double?> number = null, Func<ApiAuditEntry, DateTime?> time = null)
        {
            Name = name;
            Type = type;
            GetText = text;
            GetNumber = number;
            GetTime = time;
        }

        static ApiAuditField Text(String name, Func<ApiAuditEntry, String> get)
            => new(name, ApiAuditFieldTypes.Text, e => get(e) ?? "");

        static ApiAuditField Number(String name, Func<ApiAuditEntry, double?> get)
            => new(name, ApiAuditFieldTypes.Number, e => get(e)?.ToString(CultureInfo.InvariantCulture) ?? "", get);

        static ApiAuditField Time(String name, Func<ApiAuditEntry, DateTime?> get)
            => new(name, ApiAuditFieldTypes.Time, e => get(e)?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? "", null, get);

        /// <summary>
        /// All fields
        /// </summary>
        public static readonly IReadOnlyList<ApiAuditField> All =
        [
            Number(nameof(ApiAuditEntry.Id), e => e.Id),
            Time(nameof(ApiAuditEntry.Begin), e => e.Begin),
            Time(nameof(ApiAuditEntry.End), e => e.End),
            Number(nameof(ApiAuditEntry.Duration), e => e.Duration),
            new(nameof(ApiAuditEntry.State), ApiAuditFieldTypes.State, e => e.State.ToString(), e => (int)e.State),
            Text(nameof(ApiAuditEntry.Api), e => e.Api),
            Text(nameof(ApiAuditEntry.Group), e => e.Group),
            Text(nameof(ApiAuditEntry.UserName), e => e.UserName),
            Text(nameof(ApiAuditEntry.UserGuid), e => e.UserGuid),
            Text(nameof(ApiAuditEntry.Input), e => e.Input),
            Text(nameof(ApiAuditEntry.Output), e => e.Output),
            Text(nameof(ApiAuditEntry.Session), e => e.Session),
            Number(nameof(ApiAuditEntry.ClientId), e => e.ClientId),
            Text(nameof(ApiAuditEntry.Ip), e => e.Ip),
            Text(nameof(ApiAuditEntry.Language), e => e.Language),
            Text(nameof(ApiAuditEntry.TimeZone), e => e.TimeZone),
            Text(nameof(ApiAuditEntry.DeviceId), e => e.DeviceId),
            Text(nameof(ApiAuditEntry.UserAgent), e => e.UserAgent),
        ];

        /// <summary>
        /// The fields that are searched by a search term without any field name
        /// </summary>
        public static readonly IReadOnlyList<ApiAuditField> SearchFields = All.Where(x => x.Type == ApiAuditFieldTypes.Text).ToArray();

        static readonly Dictionary<String, ApiAuditField> ByName = All.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Short names of fields that can be used in search terms
        /// </summary>
        static readonly Dictionary<String, String> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "user", nameof(ApiAuditEntry.UserName) },
            { "guid", nameof(ApiAuditEntry.UserGuid) },
            { "in", nameof(ApiAuditEntry.Input) },
            { "out", nameof(ApiAuditEntry.Output) },
            { "client", nameof(ApiAuditEntry.ClientId) },
            { "lang", nameof(ApiAuditEntry.Language) },
            { "tz", nameof(ApiAuditEntry.TimeZone) },
            { "device", nameof(ApiAuditEntry.DeviceId) },
            { "agent", nameof(ApiAuditEntry.UserAgent) },
        };

        /// <summary>
        /// Get a field
        /// </summary>
        /// <param name="name">The name (case insensitive), can be null</param>
        /// <returns>The field, null if it doesn't exist</returns>
        public static ApiAuditField Get(String name)
            => (name != null) && ByName.TryGetValue(name, out var f) ? f : null;

        /// <summary>
        /// Get a field by the name used in a search term
        /// </summary>
        /// <param name="name">The name or a short name (case insensitive), can be null</param>
        /// <returns>The field, null if it doesn't exist</returns>
        public static ApiAuditField GetBySearchName(String name)
            => Get((name != null) && Aliases.TryGetValue(name, out var n) ? n : name);
    }

}
