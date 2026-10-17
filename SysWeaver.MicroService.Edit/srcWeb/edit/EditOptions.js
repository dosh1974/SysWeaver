/**
 * @file Option bag for the object editor (`Edit`).
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs`; host pages that build editors (e.g. explore/api.html,
 * ServerManager, mediaView/media.js) use the class through Edit.js. Defines the global class `EditOptions`.
 * No dependencies on other files.
 *
 * Conventions: besides the declared fields, the `Edit` constructor also reads the undeclared properties `Name`
 * (path segment / object name used for undo paths) and `UndoStack` (an `UndoStack` to share, e.g. set by
 * mediaView/media.js); handlers set `Name` on cloned options. `Clone()` does NOT copy these two.
 */

/**
 * Options controlling how an `Edit` instance renders and behaves.
 * Created with `new EditOptions()` (all defaults) or by cloning a preset (`EditOptions.Clean.Clone()` etc.);
 * passed to `new Edit(...)` / `Edit.FromServer(...)`. `EditTypeObject` / `EditTypeCollection` clone and modify
 * them for child editors.
 */
class EditOptions {

    /** @type {boolean} Show the title row (expand icon, menu icon, type title) via the layout's `AddTypeInfo`. */
    Title = true;
    /** @type {boolean} Developer mode: tooltips include member/type names and assembly; member menu gets "Copy member name" / "Copy type name". */
    Dev = true;
    /** @type {boolean} Make every member read only (inherited by child editors). */
    ReadOnly = false;
    /** @type {boolean} Show the expand/collapse icon in the title row of non-primitive editors. */
    CanExpand = true;
    /** @type {boolean} Initial state of the title-row expand toggle (false adds `EditorCollapsed` to the table). */
    IsExpanded = true;
    /** @type {?string} Title text override for the title row (collection item key such as `[3]`); null uses `type.DisplayName`. */
    KeyName = null;
    /** @type {boolean} When false the root `SysWeaver-Edit<style>` element gets class `NoBorder`. */
    Border = true;
    /** @type {boolean} Collections expand their items automatically when their editor is created. */
    ExpandAllCollections = false;
    /** @type {boolean} Add a menu `ColorIcon` in each member's icon cell (also makes `ChangeTitle` refresh the copy-on-click tooltip). */
    MenuIcon = false;
    /** @type {string} Icon class of the member menu icon (when `MenuIcon`). */
    MenuIconClass = "SysWeaverEditIconMenu";
    /** @type {string} Colour class of the member menu icon (when `MenuIcon`). */
    MenuColorClass = "IconColorThemeAcc2";
    /** @type {?function(HTMLElement):void} Hook called by `Edit.SetTypeInfo` with the title cell to add extra elements (collections add remove/insert icons). */
    AddToHeader = null;

    /**
     * Creates a copy of this options object. Copies only the declared fields above; the undeclared `Name` and
     * `UndoStack` properties are not copied.
     * @returns {EditOptions} The new instance.
     */
    Clone() {
        const e = new EditOptions();
        e.Title = this.Title;
        e.Dev = this.Dev;
        e.ReadOnly = this.ReadOnly;
        e.CanExpand = this.CanExpand;
        e.IsExpanded = this.IsExpanded;
        e.KeyName = this.KeyName;
        e.Border = this.Border;
        e.ExpandAllCollections = this.ExpandAllCollections;
        e.MenuIcon = this.MenuIcon;
        e.MenuIconClass = this.MenuIconClass;
        e.MenuColorClass = this.MenuColorClass;
        e.AddToHeader = this.AddToHeader;
        return e;
    }


    /**
     * Preset (built by an IIFE) for a minimal embedded editor: `Title`, `CanExpand`, `IsExpanded`, `Border` and
     * `Dev` all false. Despite the name it does not enable `MenuIcon` (still the default false), so it equals
     * `Clean` in practice. Used by `Edit.Boolean` when `showMenu` is set. Share-able instance: clone before modifying.
     * @type {EditOptions}
     */
    static CleanMenu = (() => {
        const o = new EditOptions();
        o.Title = false;
        o.CanExpand = false;
        o.IsExpanded = false;
        o.Border = false;
        o.Dev = false;
        return o;
    })();

    /**
     * Preset: clone of `CleanMenu` with `ReadOnly = true`. Used by `Edit.Boolean` (read only, with menu).
     * @type {EditOptions}
     */
    static CleanReadOnlyMenu = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.ReadOnly = true;
        return o;
    })();


    /**
     * Preset: clone of `CleanMenu` with `MenuIcon = false`. Used by `Edit.Boolean` (editable, no menu).
     * @type {EditOptions}
     */
    static Clean = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.MenuIcon = false;
        return o;
    })();

    /**
     * Preset: clone of `CleanMenu` with `ReadOnly = true` and `MenuIcon = false`. Used by `Edit.Boolean` (read only, no menu).
     * @type {EditOptions}
     */
    static CleanReadOnly = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.ReadOnly = true;
        o.MenuIcon = false;
        return o;
    })();

}
