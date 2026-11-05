/**
 * @file Type handler for floating point members (Single, Double, Decimal and their nullable variants)
 * (`Sw.Edit.Handlers.Number`, legacy global `EditTypeNumber`).
 *
 * Loaded by Edit.js. The editor, range handling and validation live in `Sw.Edit.NumberHandler` (Edit.js): a text box
 * accepting digits, "-" and one "." (metadata may use "," as decimal separator) and, for Slider members with both limits, a
 * slider with a step from EditParams or a power of ten giving 10 to 100 steps; the text then shows the decimals the
 * step / limits need (at most 5).
 */
(function () {
    "use strict";

    const E = Sw.Edit;

    /**
     * Floating point handler.
     */
    class EditTypeNumber extends E.NumberHandler {
        /**
         * @param {number} typeMin - Smallest value.
         * @param {number} typeMax - Largest value.
         * @param {number} maxLen - Maximum text length.
         * @param {boolean} [nullable] - Nullable variant.
         */
        constructor(typeMin, typeMax, maxLen, nullable) {
            super(false, typeMin, typeMax, maxLen, nullable);
        }
    }

    E.Handlers.Number = EditTypeNumber;
    if (!window.EditTypeNumber)
        window.EditTypeNumber = EditTypeNumber;
})();
