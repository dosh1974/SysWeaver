using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Any instance registered to the service manager, that implements this interface will have the template variables registered
    /// </summary>
    public interface IHaveTemplateVariables
    {
        IReadOnlyDictionary<String, ITemplateVariableGroup> TemplateVariableGroups { get; }

    }



}
