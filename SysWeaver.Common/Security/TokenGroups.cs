using System;
using System.Collections.Generic;

namespace SysWeaver.Security
{
    /// <summary>
    /// Declares which events revoke which token purposes, by group: a group is revoked by a set of events, a purpose belongs to groups.
    /// When something changes on a subject (ex: its password, its e-mail address), every live token whose meaning depends on the old state must stop working:
    /// raise the event and revoke the purposes of <see cref="RevokedBy"/> (a database, with <see cref="SubjectTokens.Tag"/>) or call <see cref="OneTimeTokens{T}.RevokeOn"/> (memory).
    /// <para>A new token kind joins the rules by declaration (<see cref="Purpose"/>), so it can't be forgotten in one of the places that revoke;
    /// a unit test can generate the whole matrix (<see cref="Purposes"/> x <see cref="Events"/>, <see cref="IsRevokedBy"/>).</para>
    /// <para>Ex:
    /// <code>
    /// static readonly TokenGroups Groups = new TokenGroups()
    ///     .Group("Credential", "PasswordChanged", "EmailChanged")
    ///     .Group("Email", "PasswordChanged", "EmailChanged", "EmailRemoved")
    ///     .Purpose("ResetPassword", "Credential")
    ///     .Purpose("ChangeEmail", "Email")
    ///     .Purpose("Invitation");   // declared, revoked by no event
    /// </code></para>
    /// </summary>
    /// <remarks>Declare everything once (ex: in a static field), then only read it: reading is thread safe.</remarks>
    public sealed class TokenGroups
    {
        readonly Dictionary<String, HashSet<String>> GroupEvents = new(StringComparer.Ordinal);
        readonly Dictionary<String, String[]> PurposeGroups = new(StringComparer.Ordinal);
        readonly List<String> PurposeOrder = new();
        readonly List<String> EventOrder = new();
        readonly Dictionary<String, HashSet<String>> Revokes = new(StringComparer.Ordinal);

        /// <summary>
        /// Declare a group and the events that revoke its tokens (declare it before the purposes that use it).
        /// </summary>
        /// <param name="group">The group name, ex: "Credential"</param>
        /// <param name="revokedBy">The events that revoke every token of the group, ex: "PasswordChanged"</param>
        /// <returns>This (to chain declarations)</returns>
        /// <exception cref="ArgumentException">The group is empty or already declared, or an event is empty</exception>
        public TokenGroups Group(String group, params String[] revokedBy)
        {
            if (String.IsNullOrEmpty(group))
                throw new ArgumentException("A group name is required", nameof(group));
            if (GroupEvents.ContainsKey(group))
                throw new ArgumentException("The group " + group + " is already declared", nameof(group));
            var h = new HashSet<String>(StringComparer.Ordinal);
            foreach (var e in revokedBy ?? [])
            {
                if (String.IsNullOrEmpty(e))
                    throw new ArgumentException("An event name can't be empty", nameof(revokedBy));
                h.Add(e);
                if (!Revokes.ContainsKey(e))
                {
                    Revokes.Add(e, new HashSet<String>(StringComparer.Ordinal));
                    EventOrder.Add(e);
                }
            }
            GroupEvents.Add(group, h);
            return this;
        }

        /// <summary>
        /// Declare a token purpose and the groups it belongs to (none = declared, but no event revokes it, ex: an invitation that belongs to no account yet).
        /// </summary>
        /// <param name="purpose">The purpose, ex: "RstPwd"</param>
        /// <param name="groups">The groups (declared with <see cref="Group"/>)</param>
        /// <returns>This (to chain declarations)</returns>
        /// <exception cref="ArgumentException">The purpose is empty or already declared, or a group isn't declared</exception>
        public TokenGroups Purpose(String purpose, params String[] groups)
        {
            if (String.IsNullOrEmpty(purpose))
                throw new ArgumentException("A purpose is required", nameof(purpose));
            if (PurposeGroups.ContainsKey(purpose))
                throw new ArgumentException("The purpose " + purpose + " is already declared", nameof(purpose));
            groups ??= [];
            foreach (var g in groups)
                if ((g == null) || !GroupEvents.ContainsKey(g))
                    throw new ArgumentException("The group " + (g ?? "null") + " is not declared (declare it with Group first)", nameof(groups));
            foreach (var g in groups)
                foreach (var e in GroupEvents[g])
                    Revokes[e].Add(purpose);
            PurposeGroups.Add(purpose, (String[])groups.Clone());
            PurposeOrder.Add(purpose);
            return this;
        }

        /// <summary>
        /// True if a purpose is declared.
        /// </summary>
        /// <param name="purpose">The purpose</param>
        /// <returns>True if declared with <see cref="Purpose"/></returns>
        public bool IsDeclared(String purpose) => (purpose != null) && PurposeGroups.ContainsKey(purpose);

        /// <summary>
        /// The declared purposes (in declaration order).
        /// </summary>
        public IReadOnlyList<String> Purposes => PurposeOrder.ToArray();

        /// <summary>
        /// Every event that revokes something (in declaration order).
        /// </summary>
        public IReadOnlyList<String> Events => EventOrder.ToArray();

        /// <summary>
        /// The groups a purpose belongs to.
        /// </summary>
        /// <param name="purpose">The purpose</param>
        /// <returns>The groups (empty if none or not declared)</returns>
        public IReadOnlyList<String> GroupsOf(String purpose) => (purpose != null) && PurposeGroups.TryGetValue(purpose, out var g) ? g : [];

        static readonly HashSet<String> None = new();

        /// <summary>
        /// The purposes whose tokens an event revokes.
        /// </summary>
        /// <param name="evt">The event</param>
        /// <returns>The purposes (empty for an unknown event)</returns>
        public IReadOnlySet<String> RevokedBy(String evt) => (evt != null) && Revokes.TryGetValue(evt, out var p) ? p : None;

        /// <summary>
        /// True if an event revokes the tokens of a purpose.
        /// </summary>
        /// <param name="purpose">The purpose</param>
        /// <param name="evt">The event</param>
        /// <returns>True if the event revokes the purpose (through one of its groups)</returns>
        public bool IsRevokedBy(String purpose, String evt) => RevokedBy(evt).Contains(purpose);
    }
}
