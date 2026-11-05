/**
 * @file Mobile layout strategy of the object editor (`Sw.Edit.Layouts.Mobile`, legacy global `EditMobile`).
 *
 * Loaded by Edit.js after EditDesktop.js has been evaluated or not (no load order dependency: it uses the desktop row
 * builder at call time). The rows are the same as the desktop rows (see EditDesktop.js); EditMobile.css puts the title and
 * the icon cell on one line and the value below it in full width, with touch sized controls.
 */
(function () {
    "use strict";

    /**
     * Mobile layout strategy (stateless; same interface as EditDesktop).
     */
    class EditMobile {
        /** @type {string} The style name. */
        get Name() {
            return "Mobile";
        }

        /**
         * Fill the title row (delegates to editor.SetTypeInfo).
         * @param {Sw.Edit} editor - The editor.
         * @param {HTMLElement} head - The title row.
         * @param {object} type - The type.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         */
        AddTypeInfo(editor, head, type, title, options) {
            editor.SetTypeInfo(head, editor.Element, type, title, options);
        }

        /**
         * Add the row of one member (same rows as the desktop layout, see EditDesktop.AddMember).
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {HTMLElement} body - The rows container.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @param {boolean} isLast - Last visible member.
         * @returns {Promise<?object>} The item.
         */
        AddMember(obj, editor, body, member, title, options, isLast) {
            return Sw.Edit.LayoutDesktop.prototype.AddMember.call(this, obj, editor, body, member, title, options, isLast);
        }
    }

    Sw.Edit.Layouts.Mobile = new EditMobile();
    Sw.Edit.LayoutMobile = EditMobile;
    if (!window.EditMobile)
        window.EditMobile = EditMobile;
})();
