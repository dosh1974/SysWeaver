using System;
using System.Collections;
using System.Collections.Generic;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    /// <summary>
    /// The built-in template variable groups that every <see cref="HttpServerBase"/> registers:
    /// "Env" (process environment variables, ex: "${Env.PATH}") and "EnvInfo" (<see cref="EnvInfo.TextVars"/>, ex: "${EnvInfo.AppName}").
    /// </summary>
    /// <remarks>
    /// Any template file can read any environment variable through the "Env" group, so secrets stored in environment variables
    /// are exposed to whoever can author (or get served) a template.
    /// </remarks>
    sealed class StaticVars : IHaveTemplateVariables
    {
        StaticVars()
        {
        }

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly StaticVars Inst = new StaticVars();

        /// <summary>
        /// The variable groups, keyed by group name ("Env" and "EnvInfo"), both are static (non-dynamic).
        /// </summary>
        public IReadOnlyDictionary<String, ITemplateVariableGroup> TemplateVariableGroups { get; private set; } = new Dictionary<String, ITemplateVariableGroup>
        {
            { "Env",  new TemplateVariableGroup(Environment.GetEnvironmentVariable, GetEnv, false) },
            { "EnvInfo",  new TemplateVariableGroup(key => EnvInfo.TextVars.TryGetValue(key, out var v) ? v : null, GetEnvInfo, false) },
        }.Freeze();

        static IEnumerable<KeyValuePair<String, String>> GetEnv()
        {
            var ed = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry x in ed)
                yield return new KeyValuePair<String, String>(x.Key as String, x.Value as String);
        }

        static IEnumerable<KeyValuePair<String, String>> GetEnvInfo() => EnvInfo.TextVars;

    }



}
