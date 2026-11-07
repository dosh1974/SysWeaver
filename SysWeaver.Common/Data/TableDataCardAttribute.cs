using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a row type to show its data tables using a card template, see <see cref="CommonTableData.Card"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [TableDataCard("drive@ServerManager/cards")]    // Cards by default, the viewer can switch to the table
    /// [TableDataCard("+user@auth/cards")]             // Rows by default, the viewer can switch to the cards
    /// sealed class DriveRow { ... }
    /// </code>
    /// </example>
    /// <remarks>
    /// Only used by tables made from the row type (<c>TableDataTools.Get</c>), other tables can use <c>TableDataTools.WithCard</c>.
    /// The html and css of a card goes in a "srcWeb" folder of the same project as the api.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
    public sealed class TableDataCardAttribute : Attribute
    {
        /// <summary>
        /// Set the card template.
        /// </summary>
        /// <param name="card">The card template, see <see cref="CommonTableData.Card"/> for the format</param>
        public TableDataCardAttribute(String card)
        {
            Card = card;
        }

        /// <summary>
        /// The card template, see <see cref="CommonTableData.Card"/> for the format
        /// </summary>
        public readonly String Card;
    }
}
