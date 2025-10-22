using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    public sealed class TemplateVariableGroup : ITemplateVariableGroup
    {

        public TemplateVariableGroup(Func<String, String> get, Func<IEnumerable<KeyValuePair<String, String>>> enumValues, bool isDynamic = false)
        {
            Get = get;
            EnumValues = enumValues;
            IsDynamic = isDynamic;
        }

        readonly Func<String, String> Get;
        readonly Func<IEnumerable<KeyValuePair<String, String>>> EnumValues;


        public bool IsDynamic { get; private set; }

        public IEnumerable<KeyValuePair<String, String>> EnumTemplateVariableValues() => EnumValues();

        public string GetTemplateVariableValue(string key) => Get(key);
    }



}
