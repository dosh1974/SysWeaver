/**
 * @file Type handler for integer members (SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, UInt64 and their nullable
 * variants) (`Sw.Edit.Handlers.Integer`, legacy global `EditTypeInteger`).
 *
 * Loaded by Edit.js. The editor, range handling and validation live in `Sw.Edit.NumberHandler` (Edit.js); this file sets
 * the natural range of each integer size (64 bit types are limited to the JS safe integer range).
 */
(function () {
    "use strict";

    const E = Sw.Edit;

    /**
     * Integer handler.
     */
    class EditTypeInteger extends E.NumberHandler {
        /**
         * @param {boolean} signed - Signed type.
         * @param {number} byteSize - Size in bytes (1, 2, 4, 8).
         * @param {boolean} [nullable] - Nullable variant.
         * @param {number} [literalMin] - Explicit type minimum (64 bit types).
         * @param {number} [literalMax] - Explicit type maximum (64 bit types).
         */
        constructor(signed, byteSize, nullable, literalMin, literalMax) {
            let min, max;
            if (literalMax !== undefined) {
                min = literalMin;
                max = literalMax;
            }
            else {
                max = Math.pow(2, byteSize * 8);
                min = 0;
                if (signed) {
                    max /= 2;
                    min = -max;
                    --max;
                }
            }
            super(true, min, max, Math.max(("" + min).length, ("" + max).length), nullable);
            /** @type {boolean} Signed type. */
            this.Signed = !!signed;
            /** @type {number} Size in bytes. */
            this.ByteSize = byteSize;
        }
    }

    E.Handlers.Integer = EditTypeInteger;
    if (!window.EditTypeInteger)
        window.EditTypeInteger = EditTypeInteger;
})();
