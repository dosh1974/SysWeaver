using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// What a table row action does to its row's item, declared by the server (see <see cref="TableDataActionsFullAttribute"/>).
    /// The web client never guesses this from the words of the action's text or tool tip, so rewording or translating them never
    /// changes the behaviour (G47).
    /// </summary>
    [Flags]
    public enum TableDataActionFlags
    {
        /// <summary>
        /// The row's item stays: after the action succeeded the row is re-read and offers its actions again (ex: Force password reset, Disable, Enable, Reset).
        /// </summary>
        None = 0,
        /// <summary>
        /// The action ends or removes the row's item (ex: Delete, Kill, End session, Withdraw invitation): after it succeeded the web table keeps the row
        /// in its place, muted and without actions, until the user refreshes, and does not offer the action again.
        /// </summary>
        Final = 1,
    }


}
