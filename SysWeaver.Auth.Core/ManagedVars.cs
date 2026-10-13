using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// The standard variables available in managed message templates (in addition to any custom variables).
    /// </summary>
    public class ManagedVars
    {
        /// <summary>
        /// The variables available in html (mail) templates
        /// </summary>
        public static readonly IReadOnlySet<String> HtmlVars = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase,
            "[Site]",
            "[User]",
            "[UserName]",
            "[Email]",
            "[Password]",
            "[Link]",
            "[ShortCode]",
            "[Root]",
            "[BackgroundDark]",
            "[Background]",
            "[Color]",
            "[Acc1]",
            "[Acc2]",
            "[Header]",
            "[Footer]",
            "[Origin]",
            "[Logo]",
            "[LogoGradient]",
            "[0]",
            "[1]",
            "[2]",
            "[3]",
            "[4]"
        );

        /// <summary>
        /// The variables available in text (and plain text mail) templates
        /// </summary>
        public static readonly IReadOnlySet<String> TextVars = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase,
            "[Site]",
            "[User]",
            "[UserName]",
            "[Email]",
            "[Password]",
            "[Link]",
            "[ShortCode]",
            "[Root]",
            "[Header]",
            "[Footer]",
            "[0]",
            "[1]",
            "[2]",
            "[3]",
            "[4]"
        );

    }


}
