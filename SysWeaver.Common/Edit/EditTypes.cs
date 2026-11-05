using System;

namespace SysWeaver
{
    /// <summary>
    /// Some predefined editor types (for use with <see cref="EditTypeAttribute"/>)
    /// </summary>
    public static class EditTypes
    {
        /// <summary>
        /// Editor for language select
        /// </summary>
        public const String Language = "Language";

        /// <summary>
        /// Editor for country select
        /// </summary>
        public const String Country = "Country";

        /// <summary>
        /// Editor for currency select
        /// </summary>
        public const String Currency = "Currency";


        /// <summary>
        /// Editor for email addresses (validated, e-mail keyboard on phones)
        /// </summary>
        public const String Email = "Email";

        /// <summary>
        /// Editor for urls (absolute urls are validated, relative paths are allowed), with the url keyboard on phones
        /// </summary>
        public const String Url = "Url";

        /// <summary>
        /// Editor for phone numbers (digits, spaces, "-", "(", ")" and a leading "+"), with the phone keyboard on phones
        /// </summary>
        public const String Phone = "Phone";

        /// <summary>
        /// Color editor, use <see cref="EditColorAttribute"/> (it writes "Color:&lt;Format&gt;:&lt;Alpha&gt;:&lt;MaxIntensity&gt;")
        /// </summary>
        public const String Color = "Color";

        /// <summary>
        /// A number with a unit (durations, byte sizes, percent / ratio), use <see cref="EditUnitAttribute"/> (it writes "Unit:&lt;Unit&gt;")
        /// </summary>
        public const String Unit = "Unit";

        /// <summary>
        /// Valid on objects, makes the object show inlined
        /// </summary>
        public const String Inline = "Inline";

    }

}
