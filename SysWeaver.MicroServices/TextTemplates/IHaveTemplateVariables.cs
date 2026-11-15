using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Any instance registered to the service manager, that implements this interface will have the template variables registered
    /// </summary>
    public interface IHaveTemplateVariables
    {
        /// <summary>
        /// The template variable groups, key = group name (a variable is referenced as "groupName.variableKey")
        /// </summary>
        IReadOnlyDictionary<String, ITemplateVariableGroup> TemplateVariableGroups { get; }

    }



}
