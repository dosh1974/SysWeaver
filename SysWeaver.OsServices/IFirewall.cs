using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Lists and edits the rules of the OS firewall (Windows Defender Firewall, ufw or iptables).
    /// Get an instance using <see cref="OsSystem.Firewall"/> (created by <see cref="IServiceHostFactory.CreateFirewall"/>).
    /// Changes requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface IFirewall
    {
        /// <summary>
        /// The display name of the firewall, ex: "Windows Defender Firewall", "ufw" or "iptables".
        /// </summary>
        String Name { get; }

        /// <summary>
        /// True if rules can be disabled (without being removed).
        /// </summary>
        bool CanDisableRules { get; }

        /// <summary>
        /// Get the state of the firewall profiles.
        /// </summary>
        /// <returns>The profiles</returns>
        IReadOnlyList<FirewallProfile> GetProfiles();

        /// <summary>
        /// Get all rules.
        /// </summary>
        /// <returns>The rules</returns>
        IReadOnlyList<FirewallRule> GetRules();

        /// <summary>
        /// Get a rule.
        /// </summary>
        /// <param name="id">The id of the rule</param>
        /// <returns>The rule or null if not found</returns>
        FirewallRule GetRule(String id);

        /// <summary>
        /// Add a new rule.
        /// </summary>
        /// <param name="rule">The rule to add (the id is ignored)</param>
        /// <returns>The id of the new rule</returns>
        String AddRule(FirewallRule rule);

        /// <summary>
        /// Update (replace) a rule.
        /// </summary>
        /// <param name="id">The id of the rule to update</param>
        /// <param name="rule">The new values</param>
        /// <returns>The (possibly new) id of the rule</returns>
        String UpdateRule(String id, FirewallRule rule);

        /// <summary>
        /// Remove a rule.
        /// </summary>
        /// <param name="id">The id of the rule to remove</param>
        void RemoveRule(String id);

        /// <summary>
        /// Enable or disable a rule (see <see cref="CanDisableRules"/>).
        /// </summary>
        /// <param name="id">The id of the rule</param>
        /// <param name="enabled">True to enable, false to disable</param>
        /// <returns>The (possibly new) id of the rule</returns>
        String SetRuleEnabled(String id, bool enabled);
    }
}
