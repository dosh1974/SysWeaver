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
        /// All query string parameters are included (decoded) as variables with the parameter name as key.
        /// If <paramref name="isDynamic"/> is true, "Server.UTC", "Request.Prefix", "Request.IP", "Session.Lang" and (if a user is logged in) "Session.User", "Session.UserName", "Session.Email", "Session.Domain" and "Session.NickName" are added (overriding query parameters with the same name).
        /// </summary>
        /// <param name="isDynamic">True to include the dynamic variables</param>
        /// <param name="request">The request</param>
        /// <returns>A new dictionary</returns>
        /// <remarks>These variables take precedence over static variables and variable groups when a template is applied, so a query parameter can override for example "EnvInfo.AppDisplayName".
        /// Values are inserted as is unless the template uses an encoding modifier.
        /// "Server.UTC" is formatted using a 12 hour clock without AM/PM.</remarks>
        public static Dictionary<String, String> GetVars(bool isDynamic, HttpServerRequest request)
        {
            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal);
            var q = request.QueryParameters;
            foreach (String key in q)
                if (key != null)
                    vars[key] = q.Get(key);
            if (isDynamic)
            {
                vars["Server.UTC"] = DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss");
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
        /// Variables in translations whose name starts with 'V' are substituted using <paramref name="vars"/>.
        /// </summary>
        /// <param name="language">The language to translate to</param>
        /// <param name="temp">The language template</param>
        /// <param name="vars">Variables to substitute, may be null</param>
        /// <returns>The translation variables, null if the template has no texts</returns>
        async Task<IReadOnlyDictionary<String, String>> GetTranslationVars(String language, LanguageTemplate temp, IReadOnlyDictionary<String, String> vars)
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
            if (vars != null)
            {
                var vv = r.ToList();
                for (int i = 0; i < l; ++ i)
                {
                    var kv = vv[i];
                    var key = kv.Key;
                    if (key[0] != 'V')
                        continue;
                    var nv = TextTemplate.SearchAndReplaceVars(kv.Value, vars, "${", "}", false, true);
                    r[key] = nv;
                }
            }
            if (!noTrans)
            {
                r["EnvInfo.AppDescription"] = await tr.TranslateSafe(EnvInfo.AppDescription, language, "en", String.Concat("This is a description of the application named \"", EnvInfo.AppDisplayName, "\" and is displayed to the user"));
            }
            return r.Freeze();
        }



    }


}
