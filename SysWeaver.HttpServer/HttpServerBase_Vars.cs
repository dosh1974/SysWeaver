using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {

        #region Variables


        /// <summary>
        /// Static template variables: the "Key=Value" pairs from <see cref="HttpServerBaseParams.Variables"/> and the application colors ("Color.Background", "Color.Color", "Color.Acc1", "Color.Acc2").
        /// </summary>
        readonly IReadOnlyDictionary<String, String> TempVars;


        /// <summary>
        /// Template variable groups, keyed by group name, a variable "Group.Name" is resolved by the group "Group" (ex: "${EnvInfo.AppName}").
        /// Services can add their own groups, the built-in groups are "Env" and "EnvInfo".
        /// </summary>
        public readonly ConcurrentDictionary<String, ITemplateVariableGroup> TempVarGroups = new(StringComparer.Ordinal);


        static readonly IReadOnlySet<String> DynamnicPrefix = ReadOnlyData.Set(StringComparer.Ordinal,
            "Session", "Server", "Request"
        );

        /// <summary>
        /// Check if a variable is dynamic (prefix "Session.", "Server.", "Request." or a dynamic variable group).
        /// </summary>
        /// <summary>
        /// The names of the per request (dynamic) variables, see <see cref="GetVars"/>
        /// </summary>
        static readonly IReadOnlySet<String> DynamicVarNames = ReadOnlyData.Set(StringComparer.Ordinal,
            "Server.UTC", "Request.Prefix", "Request.IP", "Session.Lang", "Session.User", "Session.UserName", "Session.Email", "Session.Domain", "Session.NickName"
        );

        /// <summary>
        /// Check if a name is a server defined template variable: a static variable, a per request variable (see <see cref="GetVars"/>) or a member of a variable group (ex: "EnvInfo.AppName").
        /// Templates only replace these, any other "${...}" token is kept as is.
        /// </summary>
        /// <param name="name">The variable name (without any transform prefix)</param>
        /// <returns>True if the name is a template variable</returns>
        public bool IsTemplateVariable(String name)
        {
            if (TempVars.ContainsKey(name) || DynamicVarNames.Contains(name))
                return true;
            var k = name.IndexOf('.');
            return (k > 0) && TempVarGroups.ContainsKey(name.Substring(0, k));
        }

        bool IsDynamic(String s)
        {
            var k = s.IndexOf('.');
            if (k < 0)
                return false;
            var key = s.Substring(0, k);
            if (DynamnicPrefix.Contains(key))
                return true;
            if (TempVarGroups.TryGetValue(key, out var tt))
                return tt.IsDynamic;
            return false;
        }

        /// <summary>
        /// Check if a template uses any dynamic variables (responses using it can't be cached globally).
        /// </summary>
        /// <param name="temp">The template</param>
        /// <returns>True if any variable is dynamic</returns>
        public bool IsDynamic(TextTemplate temp)
        {
            foreach (var x in temp.Vars)
            {
                if (IsDynamic(x))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get the per request template variables.
        /// If <paramref name="isDynamic"/> is true, "Server.UTC", "Request.Prefix", "Request.IP", "Session.Lang" and (if a user is logged in) "Session.User", "Session.UserName", "Session.Email", "Session.Domain" and "Session.NickName" are added.
        /// Query string parameters are never used as template variables.
        /// </summary>
        /// <param name="isDynamic">True to include the dynamic variables</param>
        /// <param name="request">The request</param>
        /// <returns>A new dictionary</returns>
        /// <remarks>These variables take precedence over static variables and variable groups when a template is applied.
        /// Values are inserted as is unless the template uses an encoding modifier.
        /// "Server.UTC" is formatted as "yyyy-MM-dd HH:mm:ss" (24 hour clock, invariant culture).</remarks>
        public static Dictionary<String, String> GetVars(bool isDynamic, HttpServerRequest request)
        {
            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal);
            if (isDynamic)
            {
                vars["Server.UTC"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                vars["Request.Prefix"] = request.Prefix;
                vars["Request.IP"] = request.GetIpAddress();
                var s = request.Session;
                vars["Session.Lang"] = s.Language ?? s.ClientLanguage ?? "en";
                var a = s.Auth;
                if (a != null)
                {
                    var un = a.Username;
                    vars["Session.User"] = un;
                    vars["Session.UserName"] = un;
                    vars["Session.Email"] = a.Email;
                    vars["Session.Domain"] = a.Domain;
                    vars["Session.NickName"] = a.NickName;
                }
            }
            return vars;
        }




        /// <summary>
        /// Apply a template, variables are resolved from (in order): <paramref name="vars"/>, <paramref name="extra"/>, the static variables and the variable groups.
        /// Unknown variables resolve to null.
        /// </summary>
        /// <param name="template">The template</param>
        /// <param name="vars">The per request variables (see <see cref="GetVars"/>)</param>
        /// <param name="extra">Optional extra variables (translations), may be null</param>
        /// <returns>The UTF-8 encoded result</returns>
        public ReadOnlyMemory<Byte> ApplyTemplate(TextTemplate template, IReadOnlyDictionary<String, String> vars, IReadOnlyDictionary<String, String> extra)
        {
            using (PerfMon.Track(nameof(ApplyTemplate)))
            {
                String getVar(String key)
                {
                    if (vars.TryGetValue(key, out var v))
                        return v;
                    if (extra?.TryGetValue(key, out v) ?? false)
                        return v;
                    if (TempVars.TryGetValue(key, out v))
                        return v;
                    var i = key.IndexOf('.');
                    if (i < 0)
                        return null;
                    if (!TempVarGroups.TryGetValue(key.Substring(0, i), out var fn))
                        return null;
                    return fn.GetTemplateVariableValue(key.Substring(i + 1));
                }
                var text = template.Get(getVar);
                return Encoding.UTF8.GetBytes(text);
            }
        }

        IEnumerable<TemplateVariableValue> GetTemplateVars(HttpServerRequest request)
        {
            var seen = new HashSet<String>();
            var s = GetVars(false, request);
            foreach (var x in GetVars(true, request))
            {
                var k = x.Key;
                bool isDyn = !s.ContainsKey(k);
                if (seen.Add(k))
                    yield return new TemplateVariableValue(k, x.Value, isDyn);
            }
            foreach (var x in TempVars)
            {
                var k = x.Key;
                if (seen.Add(k))
                    yield return new TemplateVariableValue(k, x.Value);
            }
            foreach (var x in TempVarGroups)
            {
                var pre = x.Key + ".";
                var d = x.Value.IsDynamic;
                foreach (var y in x.Value.EnumTemplateVariableValues())
                {
                    var k = pre + y.Key;
                    if (seen.Add(k))
                        yield return new TemplateVariableValue(k, y.Value, d);
                }
            }
        }

        /// <summary>
        /// Get all template variables (with their values for the current request).
        /// </summary>
        /// <param name="r">Table parameters</param>
        /// <param name="request">The request</param>
        /// <returns>The table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuPath, null, null, "IconTableTemplates")]
        public TableData TemplateVariables(TableDataRequest r, HttpServerRequest request) => TableDataTools.Get(r, 1000, GetTemplateVars(request));


        #endregion//Variables


        /// <summary>
        /// Get the translations of a language template as variables (untranslated if there is no translator or the language is English).
        /// The result only depends on the language and is cached in <see cref="LanguageTemplate.LangVars"/>, so variables inside of the translations are NOT substituted here,
        /// that is done per request by <see cref="SubstituteTranslationVars"/> (else the values of the first request would be cached and shown to everyone).
        /// If a translation variable whose name starts with 'V' is only inserted html encoded in the template (ex: "${#Vr1}"), html encoding transforms of its inner variables are removed
        /// (ex: "Welcome ${#Session.NickName}" becomes "Welcome ${Session.NickName}"), so that the values aren't html encoded twice.
        /// </summary>
        /// <param name="language">The language to translate to</param>
        /// <param name="temp">The language template</param>
        /// <returns>The translation variables, null if the template has no texts</returns>
        async Task<IReadOnlyDictionary<String, String>> GetTranslationVars(String language, LanguageTemplate temp)
        {
            var v = temp.Vars;
            if (v == null)
                return null;
            var l = v.Count;
            if (l <= 0)
                return null;
            var r = new Dictionary<String, String>(l, StringComparer.Ordinal);
            var tr = Translator;
            var noTrans = (tr == null) || String.IsNullOrEmpty(language) || language.FastEquals("en");
            if (noTrans)
            {
                for (int i = 0; i < l; ++i)
                {
                    var d = v[i];
                    r.Add(d.VarName, d.Text);
                }
            }
            else
            {
                using var _ = PerfMon.Track(nameof(GetTranslationVars));
                using var __ = PerfMon.Track(String.Concat(nameof(GetTranslationVars), '.', language));
                var trs = await v.ConvertAsync(d => tr.TranslateSafe(d.Text, language, "en", d.Context)).ConfigureAwait(false);
                for (int i = 0; i < l; ++i)
                    r.Add(v[i].VarName, trs[i]);
            }
            var vv = r.ToList();
            for (int i = 0; i < l; ++ i)
            {
                var kv = vv[i];
                var key = kv.Key;
                if (key[0] != 'V')
                    continue;
                var nv = kv.Value;
                //  The variable is html encoded when it's inserted, so html encoding the inner variables would encode them twice
                if (HaveInnerHtmlEncodedVars(nv) && IsOnlyUsedHtmlEncoded(temp.Text, key))
                    r[key] = RemoveInnerHtmlEncoding(nv);
            }
            if (!noTrans)
            {
                r["EnvInfo.AppDescription"] = await tr.TranslateSafe(EnvInfo.AppDescription, language, "en", String.Concat("This is a description of the application named \"", EnvInfo.AppDisplayName, "\" and is displayed to the user"));
            }
            return r.Freeze();
        }

        /// <summary>
        /// Substitute the variables inside of the translation variables whose name starts with 'V' (ex: "Welcome ${Session.NickName}") using the per request variables.
        /// </summary>
        /// <param name="translations">The (cached) translation variables, see <see cref="GetTranslationVars"/>, may be null</param>
        /// <param name="vars">The per request variables (see <see cref="GetVars"/>), may be null</param>
        /// <returns><paramref name="translations"/> if nothing was substituted, else a new dictionary</returns>
        static IReadOnlyDictionary<String, String> SubstituteTranslationVars(IReadOnlyDictionary<String, String> translations, IReadOnlyDictionary<String, String> vars)
        {
            if ((translations == null) || (vars == null))
                return translations;
            Dictionary<String, String> r = null;
            foreach (var kv in translations)
            {
                var key = kv.Key;
                if ((key.Length <= 0) || (key[0] != 'V'))
                    continue;
                var v = kv.Value;
                if ((v == null) || (v.IndexOf("${", StringComparison.Ordinal) < 0))
                    continue;
                var nv = TextTemplate.SearchAndReplaceVars(v, vars, "${", "}", false, true);
                if (String.Equals(nv, v, StringComparison.Ordinal))
                    continue;
                r ??= new Dictionary<String, String>(translations, StringComparer.Ordinal);
                r[key] = nv;
            }
            return r ?? translations;
        }

        /// <summary>
        /// Check if any translation variable whose name starts with 'V' (a translated text that contains other variables) uses a dynamic variable (ex: "Welcome ${#Session.NickName}").
        /// A template using such a translation variable must be treated as dynamic (it's output depends on the request), even if the template text itself have no dynamic variables.
        /// </summary>
        /// <param name="temp">The language template, may be null</param>
        /// <returns>True if any translation variable uses a dynamic variable</returns>
        bool HaveDynamicTranslationVars(LanguageTemplate temp)
        {
            var v = temp?.Vars;
            if (v == null)
                return false;
            foreach (var d in v)
            {
                var name = d.VarName;
                if (String.IsNullOrEmpty(name) || (name[0] != 'V'))
                    continue;
                var text = d.Text;
                if ((text == null) || (text.IndexOf("${", StringComparison.Ordinal) < 0))
                    continue;
                if (IsDynamic(new TextTemplate(text, "${", "}", IsTemplateVariable)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get the length of an html encoding transform prefix ("#" or "@", optionally preceded by one of "_", "^" or "~") at a position, 0 if none
        /// </summary>
        static int GetHtmlEncodingTransformLength(String text, int pos, out int encPos)
        {
            encPos = pos;
            var tl = text.Length;
            if (pos >= tl)
                return 0;
            var c = text[pos];
            if ((c == '_') || (c == '^') || (c == '~'))
            {
                ++encPos;
                if (encPos >= tl)
                    return 0;
                c = text[encPos];
            }
            if ((c != '#') && (c != '@'))
                return 0;
            return encPos - pos + 1;
        }

        /// <summary>
        /// Check if a text have any variables using an html encoding transform, ex: "${#Session.NickName}"
        /// </summary>
        static bool HaveInnerHtmlEncodedVars(String text)
        {
            if (text == null)
                return false;
            for (int p = text.IndexOf("${", StringComparison.Ordinal); p >= 0; p = text.IndexOf("${", p + 2, StringComparison.Ordinal))
            {
                if (GetHtmlEncodingTransformLength(text, p + 2, out _) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Remove the html encoding transforms of all variables in a text (case transforms are kept), ex: "Hi ${_#Name}" => "Hi ${_Name}"
        /// </summary>
        static String RemoveInnerHtmlEncoding(String text)
        {
            var sb = new StringBuilder(text.Length);
            int s = 0;
            for (int p = text.IndexOf("${", StringComparison.Ordinal); p >= 0; p = text.IndexOf("${", p + 2, StringComparison.Ordinal))
            {
                if (GetHtmlEncodingTransformLength(text, p + 2, out var encPos) <= 0)
                    continue;
                sb.Append(text, s, encPos - s);
                s = encPos + 1;
            }
            sb.Append(text, s, text.Length - s);
            return sb.ToString();
        }

        /// <summary>
        /// Check if all uses of a variable in a template text are html encoded ("${#Var}" or "${¤Var}", optionally with a case transform), and that it's used at least once
        /// </summary>
        static bool IsOnlyUsedHtmlEncoded(String templateText, String varName)
        {
            if (templateText == null)
                return false;
            var find = String.Concat(varName, "}");
            bool found = false;
            for (int p = templateText.IndexOf(find, StringComparison.Ordinal); p >= 0; p = templateText.IndexOf(find, p + 1, StringComparison.Ordinal))
            {
                var e = p - 1;
                if (e < 2)
                    return false;
                var c = templateText[e];
                if ((c != '#') && (c != '¤'))
                    return false;
                --e;
                c = templateText[e];
                if ((c == '_') || (c == '^') || (c == '~'))
                {
                    --e;
                    if (e < 1)
                        return false;
                }
                if ((templateText[e] != '{') || (templateText[e - 1] != '$'))
                    return false;
                found = true;
            }
            return found;
        }



    }


}
