using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// A parsed condition on a field, see <see cref="ApiAuditMatcher"/>.
    /// </summary>
    public sealed class ApiAuditCondition
    {
        /// <summary>
        /// The field
        /// </summary>
        public readonly ApiAuditField Field;

        /// <summary>
        /// The operation, never <see cref="TableDataFilterOps.NotEqual"/> or <see cref="TableDataFilterOps.NoneOf"/> (they are inverted instead).
        /// </summary>
        public readonly TableDataFilterOps Op;

        /// <summary>
        /// Invert the result of the operation
        /// </summary>
        public readonly bool Invert;

        /// <summary>
        /// Text operations are case sensitive
        /// </summary>
        public readonly bool CaseSensitive;

        /// <summary>
        /// True if the operation compares texts (using <see cref="Texts"/>), else the <see cref="Numbers"/> or <see cref="Times"/> are used (depending on the type of the field).
        /// </summary>
        public readonly bool IsText;

        /// <summary>
        /// The texts to compare with, one for all operations except any of (any number) and the range operations (two).
        /// </summary>
        public readonly String[] Texts;

        /// <summary>
        /// The numbers to compare with (number and state fields), see <see cref="Texts"/>
        /// </summary>
        public readonly double[] Numbers;

        /// <summary>
        /// The times to compare with (time fields), see <see cref="Texts"/>
        /// </summary>
        public readonly DateTime[] Times;

        /// <summary>
        /// True if the value is invalid for the field (ex: a text can't be compared to a number), the condition never match anything (even if inverted).
        /// </summary>
        public readonly bool Never;

        ApiAuditCondition(ApiAuditField field, TableDataFilter f)
        {
            Field = field;
            var op = f.Op;
            var invert = f.Invert;
            if (op == TableDataFilterOps.NotEqual)
            {
                op = TableDataFilterOps.Equals;
                invert = !invert;
            }
            if (op == TableDataFilterOps.NoneOf)
            {
                op = TableDataFilterOps.AnyOf;
                invert = !invert;
            }
            Op = op;
            Invert = invert;
            CaseSensitive = f.CaseSensitive;
            var value = f.Value;
            var many = (op == TableDataFilterOps.AnyOf) || (op == TableDataFilterOps.InRange) || (op == TableDataFilterOps.OutsideRange);
            var texts = many ? value.Split(',', StringSplitOptions.TrimEntries) : [value];
            if ((op == TableDataFilterOps.InRange) || (op == TableDataFilterOps.OutsideRange))
            {
                if (texts.Length < 2)
                {
                    Never = true;
                    return;
                }
                texts = [texts[0], texts[1]];
            }
            Texts = texts;
            IsText = (field.Type == ApiAuditFieldTypes.Text) || (op == TableDataFilterOps.Contains) || (op == TableDataFilterOps.StartsWith) || (op == TableDataFilterOps.EndsWith);
            if (IsText)
                return;
            if (field.Type == ApiAuditFieldTypes.Time)
            {
                Times = texts.Select(x => (Ok: TryParseTime(x, out var t), Value: t)).Where(x => x.Ok).Select(x => x.Value).ToArray();
                Never = Times.Length < (op == TableDataFilterOps.AnyOf ? 1 : texts.Length);
                return;
            }
            var isState = field.Type == ApiAuditFieldTypes.State;
            Numbers = texts.Select(x => (Ok: TryParseNumber(x, isState, out var t), Value: t)).Where(x => x.Ok).Select(x => x.Value).ToArray();
            Never = Numbers.Length < (op == TableDataFilterOps.AnyOf ? 1 : texts.Length);
        }

        /// <summary>
        /// Parse a filter
        /// </summary>
        /// <param name="f">The filter, can be null</param>
        /// <returns>The condition, null if the filter should be ignored (no value, an unknown column or an unknown operation, same as for tables)</returns>
        public static ApiAuditCondition Create(TableDataFilter f)
        {
            if (f?.Value == null)
                return null;
            var op = (int)f.Op;
            if ((op < 0) || (op >= TableDataFilterOpsProps.Count))
                return null;
            var field = ApiAuditField.Get(f.ColName);
            return field == null ? null : new ApiAuditCondition(field, f);
        }

        /// <summary>
        /// Parse a time (invariant culture, UTC unless an offset is specified)
        /// </summary>
        /// <param name="s">The text</param>
        /// <param name="time">The time (UTC)</param>
        /// <returns>True if the text is a time</returns>
        public static bool TryParseTime(String s, out DateTime time)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out time);

        /// <summary>
        /// Parse a number (invariant culture)
        /// </summary>
        /// <param name="s">The text</param>
        /// <param name="isState">The text can be the name of an <see cref="ApiAuditStates"/> value</param>
        /// <param name="number">The number</param>
        /// <returns>True if the text is a number</returns>
        public static bool TryParseNumber(String s, bool isState, out double number)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return double.IsFinite(number);
            if (!isState || !Enum.TryParse<ApiAuditStates>(s, true, out var state))
                return false;
            number = (int)state;
            return true;
        }

        /// <summary>
        /// The time after a time that is equal to it: a time without any fractions is equal to all times within that second.
        /// </summary>
        /// <param name="time">The time</param>
        /// <returns>The first time that is after (and not equal to) the time</returns>
        public static DateTime EqualEnd(DateTime time)
            => time.AddTicks((time.Ticks % TimeSpan.TicksPerSecond) == 0 ? TimeSpan.TicksPerSecond : 1);

        /// <summary>
        /// Check if an entry matches the condition
        /// </summary>
        /// <param name="e">The entry</param>
        /// <returns>True if it matches</returns>
        public bool Match(ApiAuditEntry e)
            => !Never && (MatchOp(e) != Invert);

        bool MatchOp(ApiAuditEntry e)
        {
            var field = Field;
            if (IsText)
            {
                var v = field.GetText(e);
                var t = Texts;
                var c = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                switch (Op)
                {
                    case TableDataFilterOps.Equals:
                        return String.Equals(v, t[0], c);
                    case TableDataFilterOps.LessThan:
                        return String.Compare(v, t[0], c) < 0;
                    case TableDataFilterOps.GreaterThan:
                        return String.Compare(v, t[0], c) > 0;
                    case TableDataFilterOps.LessEqual:
                        return String.Compare(v, t[0], c) <= 0;
                    case TableDataFilterOps.GreaterEqual:
                        return String.Compare(v, t[0], c) >= 0;
                    case TableDataFilterOps.Contains:
                        return v.Contains(t[0], c);
                    case TableDataFilterOps.StartsWith:
                        return v.StartsWith(t[0], c);
                    case TableDataFilterOps.EndsWith:
                        return v.EndsWith(t[0], c);
                    case TableDataFilterOps.AnyOf:
                        return t.Any(x => String.Equals(v, x, c));
                    case TableDataFilterOps.InRange:
                        return (String.Compare(v, t[0], c) >= 0) && (String.Compare(v, t[1], c) < 0);
                    default:
                        return (String.Compare(v, t[0], c) < 0) || (String.Compare(v, t[1], c) > 0);
                }
            }
            if (field.Type == ApiAuditFieldTypes.Time)
            {
                var v = field.GetTime(e);
                if (v == null)
                    return false;
                var d = v.Value;
                var t = Times;
                switch (Op)
                {
                    case TableDataFilterOps.Equals:
                        return (d >= t[0]) && (d < EqualEnd(t[0]));
                    case TableDataFilterOps.LessThan:
                        return d < t[0];
                    case TableDataFilterOps.GreaterThan:
                        return d >= EqualEnd(t[0]);
                    case TableDataFilterOps.LessEqual:
                        return d < EqualEnd(t[0]);
                    case TableDataFilterOps.GreaterEqual:
                        return d >= t[0];
                    case TableDataFilterOps.AnyOf:
                        return t.Any(x => (d >= x) && (d < EqualEnd(x)));
                    case TableDataFilterOps.InRange:
                        return (d >= t[0]) && (d < t[1]);
                    default:
                        return (d < t[0]) || (d >= EqualEnd(t[1]));
                }
            }
            {
                var v = field.GetNumber(e);
                if (v == null)
                    return false;
                var d = v.Value;
                var t = Numbers;
                switch (Op)
                {
                    case TableDataFilterOps.Equals:
                        return d == t[0];
                    case TableDataFilterOps.LessThan:
                        return d < t[0];
                    case TableDataFilterOps.GreaterThan:
                        return d > t[0];
                    case TableDataFilterOps.LessEqual:
                        return d <= t[0];
                    case TableDataFilterOps.GreaterEqual:
                        return d >= t[0];
                    case TableDataFilterOps.AnyOf:
                        return t.Contains(d);
                    case TableDataFilterOps.InRange:
                        return (d >= t[0]) && (d < t[1]);
                    default:
                        return (d < t[0]) || (d > t[1]);
                }
            }
        }
    }

    /// <summary>
    /// A parsed search term, see <see cref="ApiAuditQuery.Text"/>
    /// </summary>
    public sealed class ApiAuditTerm
    {
        /// <summary>
        /// The field that must contain the text, null if any of the <see cref="ApiAuditField.SearchFields"/> may contain it.
        /// </summary>
        public readonly ApiAuditField Field;

        /// <summary>
        /// The text (case insensitive)
        /// </summary>
        public readonly String Text;

        /// <summary>
        /// True if the term may not match
        /// </summary>
        public readonly bool Exclude;

        internal ApiAuditTerm(ApiAuditField field, String text, bool exclude)
        {
            Field = field;
            Text = text;
            Exclude = exclude;
        }

        /// <summary>
        /// Check if an entry matches the term
        /// </summary>
        /// <param name="e">The entry</param>
        /// <returns>True if it matches</returns>
        public bool Match(ApiAuditEntry e)
        {
            var f = Field;
            var t = Text;
            if (f != null)
                return f.GetText(e).Contains(t, StringComparison.OrdinalIgnoreCase) != Exclude;
            foreach (var x in ApiAuditField.SearchFields)
                if (x.GetText(e).Contains(t, StringComparison.OrdinalIgnoreCase))
                    return !Exclude;
            return Exclude;
        }
    }

    /// <summary>
    /// Parsed selected values of a field, see <see cref="ApiAuditQuery.Values"/>
    /// </summary>
    public sealed class ApiAuditSelection
    {
        /// <summary>
        /// The field
        /// </summary>
        public readonly ApiAuditField Field;

        /// <summary>
        /// The values (as text), not empty
        /// </summary>
        public readonly IReadOnlySet<String> Values;

        /// <summary>
        /// True if the field may not have any of the values
        /// </summary>
        public readonly bool Exclude;

        internal ApiAuditSelection(ApiAuditField field, IEnumerable<String> values, bool exclude)
        {
            Field = field;
            Values = new HashSet<String>(values.Select(x => x ?? ""), field.Type == ApiAuditFieldTypes.Text ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            Exclude = exclude;
        }

        /// <summary>
        /// Check if an entry matches the selection
        /// </summary>
        /// <param name="e">The entry</param>
        /// <returns>True if it matches</returns>
        public bool Match(ApiAuditEntry e)
            => Values.Contains(Field.GetText(e)) != Exclude;
    }

    /// <summary>
    /// The interpretation of an <see cref="ApiAuditQuery"/>, can be used to match entries in memory or to build a database query.
    /// </summary>
    public sealed class ApiAuditMatcher
    {
        /// <summary>
        /// Maximum number of search terms (the rest are ignored)
        /// </summary>
        public const int MaxTerms = 16;

        /// <summary>
        /// Parse a query
        /// </summary>
        /// <param name="q">The query, can be null</param>
        /// <param name="ignoreSelection">Optional name of a field, the selected values of that field are ignored</param>
        public ApiAuditMatcher(ApiAuditQuery q, String ignoreSelection = null)
        {
            From = ToUtc(q?.From);
            To = ToUtc(q?.To);
            Terms = GetTerms(q?.Text);
            var ignore = ApiAuditField.Get(ignoreSelection);
            List<ApiAuditSelection> sel = new();
            foreach (var x in q?.Values ?? [])
            {
                var f = ApiAuditField.Get(x?.Field);
                if ((f == null) || (f == ignore) || ((x.Values?.Length ?? 0) <= 0))
                    continue;
                sel.Add(new ApiAuditSelection(f, x.Values, x.Exclude));
            }
            Selections = sel;
            All = (q?.All ?? []).Select(ApiAuditCondition.Create).Where(x => x != null).ToArray();
            Any = (q?.Any ?? []).Select(ApiAuditCondition.Create).Where(x => x != null).ToArray();
        }

        /// <summary>
        /// Must begin at or after this time (UTC), null = no limit
        /// </summary>
        public readonly DateTime? From;

        /// <summary>
        /// Must begin before this time (UTC), null = no limit
        /// </summary>
        public readonly DateTime? To;

        /// <summary>
        /// Search terms, all must match
        /// </summary>
        public readonly IReadOnlyList<ApiAuditTerm> Terms;

        /// <summary>
        /// Selected field values, all must match
        /// </summary>
        public readonly IReadOnlyList<ApiAuditSelection> Selections;

        /// <summary>
        /// Conditions that all must match
        /// </summary>
        public readonly IReadOnlyList<ApiAuditCondition> All;

        /// <summary>
        /// If there are any conditions, at least one must match
        /// </summary>
        public readonly IReadOnlyList<ApiAuditCondition> Any;

        static DateTime? ToUtc(DateTime? t)
        {
            if (t == null)
                return null;
            var v = t.Value;
            return v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc);
        }

        static ApiAuditTerm[] GetTerms(String text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return [];
            List<ApiAuditTerm> terms = new();
            var l = text.Length;
            for (int i = 0; (i < l) && (terms.Count < MaxTerms);)
            {
                if (Char.IsWhiteSpace(text[i]))
                {
                    ++i;
                    continue;
                }
                var exclude = (text[i] == '-') && (i + 1 < l) && !Char.IsWhiteSpace(text[i + 1]);
                if (exclude)
                    ++i;
                //  An optional field name (unknown names are a part of the text)
                ApiAuditField field = null;
                var e = i;
                while ((e < l) && Char.IsAsciiLetter(text[e]))
                    ++e;
                if ((e > i) && (e + 1 < l) && (text[e] == ':') && !Char.IsWhiteSpace(text[e + 1]))
                {
                    field = ApiAuditField.GetBySearchName(text.Substring(i, e - i));
                    if (field != null)
                        i = e + 1;
                }
                String t;
                var q = text[i] == '"' ? text.IndexOf('"', i + 1) : -1;
                if (q > 0)
                {
                    t = text.Substring(i + 1, q - i - 1);
                    i = q + 1;
                }
                else
                {
                    e = i;
                    while ((e < l) && !Char.IsWhiteSpace(text[e]))
                        ++e;
                    t = text.Substring(i, e - i);
                    i = e;
                }
                if (t.Length > 0)
                    terms.Add(new ApiAuditTerm(field, t, exclude));
            }
            return terms.ToArray();
        }

        /// <summary>
        /// Check if an entry matches
        /// </summary>
        /// <param name="e">The entry</param>
        /// <returns>True if it matches</returns>
        public bool Match(ApiAuditEntry e)
        {
            if ((From != null) && (e.Begin < From.Value))
                return false;
            if ((To != null) && (e.Begin >= To.Value))
                return false;
            foreach (var x in Selections)
                if (!x.Match(e))
                    return false;
            foreach (var x in All)
                if (!x.Match(e))
                    return false;
            foreach (var x in Terms)
                if (!x.Match(e))
                    return false;
            if (Any.Count <= 0)
                return true;
            foreach (var x in Any)
                if (x.Match(e))
                    return true;
            return false;
        }

    }

}
