using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// A way for the user manager to communicate with users (ex: e-mail or text messages)
    /// </summary>
    public interface IUserManagerComs
    {
        /// <summary>
        /// True if users can sign up using this method
        /// </summary>
        bool AllowSignUp { get; }

        /// <summary>
        /// The name of the method (ex: "Email")
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Send one of the standard user manager messages
        /// </summary>
        /// <param name="ops">The message to send</param>
        /// <param name="messages">Localized messages to use, null to use the built in (or configured) messages</param>
        /// <param name="target">The address (ex: e-mail address or phone number) to send it to</param>
        /// <param name="vars">The variables to replace in the message</param>
        /// <param name="system">The name of the system that sends the message</param>
        /// <returns>A task that completes when the message has been sent</returns>
        Task Send(UserManagerComOps ops, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system);

        /// <summary>
        /// Send a named message
        /// </summary>
        /// <param name="message">The name of the message</param>
        /// <param name="messages">Localized messages to look in first, can be null</param>
        /// <param name="target">The address (ex: e-mail address or phone number) to send it to</param>
        /// <param name="vars">The variables to replace in the message</param>
        /// <param name="system">The name of the system that sends the message</param>
        /// <param name="fallbacks">Messages to look in (in order) if the message isn't found in <paramref name="messages"/></param>
        /// <returns>A task that completes when the message has been sent</returns>
        Task Send(String message, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system, params ManagedLanguageMessages[] fallbacks);

        /// <summary>
        /// Validate (and normalize) an address
        /// </summary>
        /// <param name="target">The address, replaced by the normalized form if it is valid</param>
        /// <returns>True if the address is valid for this method</returns>
        bool CleanAndValidate(ref String target);

    }

    /// <summary>
    /// Base class for the parameters of a communication method
    /// </summary>
    public class ComsParams
    {
        /// <summary>
        /// Set to true if you allow users to sign up using this coms method
        /// </summary>
        public bool AllowSignUp = true;
    }


}
