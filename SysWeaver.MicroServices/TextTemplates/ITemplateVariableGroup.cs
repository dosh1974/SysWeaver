using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    public interface ITemplateVariableGroup
    {

        bool IsDynamic { get; }

        String GetTemplateVariableValue(String key);

        IEnumerable<KeyValuePair<String, String>> EnumTemplateVariableValues();

    }



}
