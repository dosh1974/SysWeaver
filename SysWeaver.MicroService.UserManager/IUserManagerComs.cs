using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{

    public interface IUserManagerComs
    {
        bool AllowSignUp { get; }

        String Name { get; }

        Task Send(UserManagerComOps ops, ManagedLanguageMessages lang, String target, Dictionary<String, String> vars, String system);

        bool CleanAndValidate(ref String target);

    }

    public class ComsParams
    {
        /// <summary>
        /// Set to true if you allow users to sign up using this coms method
        /// </summary>
        public bool AllowSignUp = true;
    }


}
