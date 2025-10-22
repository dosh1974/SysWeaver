using System;
using System.Collections;
using System.Collections.Generic;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    sealed class StaticVars : IHaveTemplateVariables
    {
        StaticVars()
        {
        }

        public static readonly StaticVars Inst = new StaticVars();

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
