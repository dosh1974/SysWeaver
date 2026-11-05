/**
 * @file Tag gallery control: renders a list of tags as chips (optionally removable) plus an "Add" button that opens a
 * searchable selection dialog. Optional library (no first-party page uses it); a page loads it after the Sw core and
 * ui (`../sw/core.js`, `../sw/ui.js`, or the legacy `../common/common.js`) and links `../app/tags.css`.
 *
 * New API: `new Sw.TagsGallery({FindTags, GetTags, RemoveTag, AddTag, MaxTagCount, Options})` with `.E` (the
 * element), `Update()`, `GetTags()`; `await gallery.Ready` resolves after the first render.
 * Legacy globals (unchanged signatures): `TagGalleryParams`, `Tag`, `FilterTags`, `SetTagClassName`,
 * `CreateTagsGallery` (returns `{Element, Update, GetTags}` like before).
 *
 * Dependencies: Sw ui (`Sw.Button`, `Sw.Dialog.Select`, `Sw.Fail`), Sw core (`Sw.Format.StringFormat`).
 * Conventions: tags are identified by their numeric `Code` (names may be localized); tag sources return objects
 * shaped `{Tags: Tag[]}`. Element names (`SysWeaver-TagsGallery`, `SysWeaver-TagEdit`, `SysWeaver-TagText`) are kept
 * as css hooks (tags.css, page styles).
 */

/**
 * Options of a tag gallery: texts (translated when the instance is created), icon classes, extra css classes and
 * layout flags. Create with `new TagGalleryParams()` and override fields as needed.
 */
class TagGalleryParams {
    /**
     * True: the add dialog lists all tags and the input filters them (Filter* texts); false: the input is a search and
     * nothing is listed until something is typed (Search* texts).
     * @type {boolean}
     */
    ShowAllTags = false;

    /** If true the Add button is the first child of the gallery, else the last. @type {boolean} */
    AddAtFront = true;

    /** Optional extra css class of the `<SysWeaver-TagsGallery>` element (ex: "Keep" / "Ignore"). @type {?string} */
    Class = null;

    /** Add button text. @type {string} */
    AddText = _TF("Add", "The text of a button that when pressed will add a tag to a list of tags");
    /** Add button tool tip. @type {string} */
    AddTitle = _TF("Add a tag", "The tool tip description of a button that when pressed will add a tag to a list of tags");
    /** Error message prefix when adding fails. @type {string} */
    AddError = _TF("Failed to add tag", "An error message displayed when a tag couldn't be added to the list of tags");
    /** Add button icon class. @type {string} */
    AddIcon = "IconTagAdd";
    /** Optional extra css class of the Add button. @type {?string} */
    AddClass = null;

    /** Remove button tool tip (format, {0} = tag name). @type {string} */
    RemoveTitle = _TF('Remove the tag "{0}"', "The tool tip description of a button that when pressed will remove a tag from a list of tags. {0} is replaced with the name of the tag");
    /** Error message prefix when removing fails. @type {string} */
    RemoveError = _TF("Failed to remove tag", "An error message displayed when a tag couldn't be removed from the list of tags");
    /** Remove button icon class. @type {string} */
    RemoveIcon = "IconTagRemove";
    /** Optional extra css class of the remove buttons. @type {?string} */
    RemoveClass = null;
    /** Remove icon size in pixels (kept for compatibility; the button follows the Sw touch size). @type {number} */
    RemoveIconSize = 24;

    /** Dialog title in search mode (ShowAllTags false). @type {string} */
    SearchText = _TF("Search for a tag", "The text of a label for a text input field that is used for searching");
    /** Search box placeholder in search mode. @type {string} */
    SearchTitle = _TF("Enter some text to search for a tag", "The tool tip of a text input field that is used for searching");
    /** Error message prefix when searching fails. @type {string} */
    SearchError = _TF("Failed to find tags", "An error message that is displayed when a search for tags yielded no results");

    /** Dialog title in filter mode (ShowAllTags true). @type {string} */
    FilterText = _TF("Filter tags", "The text of a label for a text input field that is used for filtering");
    /** Filter box placeholder in filter mode. @type {string} */
    FilterTitle = _TF("Enter some text to filter the tags", "The tool tip of a text input field that is used for filtering");
    /** Error message prefix when filtering fails. @type {string} */
    FilterError = _TF("Failed to filter tags", "An error message that is displayed when filtering tags yields no results");

    /** If true the gallery gets class `Embedded` (no border, padding or margin). @type {boolean} */
    Embedded = false;
}

/**
 * Shape of a tag (documentation / default values; any object with these members works). `FilterTags` adds `Rank`.
 */
class Tag {
    /** Unique code (names may be localized). @type {number} */
    Code = 0;
    /** Name (shown). @type {?string} */
    Name = null;
    /** Optional description (tool tip). @type {?string} */
    Desc = null;
    /** Optional css class of the chip. @type {?string} */
    Class = null;
}

/**
 * Client side tag search helper (ex: to implement a FindTags callback). Case insensitive: a tag matches if its Name or
 * Desc contains the text. Rank = 0.25 / len(text) * descHit + (100 - namePos) (namePos 100 when the name does not
 * match), so earlier name matches rank higher. Writes `Rank` on each matched tag, sorts by descending Rank and keeps at
 * most maxCount. A missing Desc no longer throws.
 * @param {Tag[]} tags - Tags to search.
 * @param {string} searchFor - Search text; falsy returns `tags` unchanged (same array).
 * @param {number} [maxCount] - Maximum result count (no limit when undefined).
 * @returns {Tag[]} The original array (empty search) or a new array of matching tags.
 */
function FilterTags(tags, searchFor, maxCount) {
    if (!searchFor)
        return tags;
    const sl = searchFor.toLowerCase();
    const isl = 0.25 / sl.length;
    const dest = [];
    for (const s of tags) {
        let namePos = (s.Name || "").toLowerCase().indexOf(sl);
        const dl = (s.Desc || "").toLowerCase();
        const descHit = dl.length - dl.replace(sl, "").length;
        if ((namePos < 0) && (descHit <= 0))
            continue;
        if (namePos < 0)
            namePos = 100;
        s.Rank = isl * descHit + (100 - namePos);
        dest.push(s);
    }
    dest.sort((a, b) => b.Rank - a.Rank);
    if (dest.length > maxCount)
        dest.length = maxCount;
    return dest;
}

/**
 * Set `Class` on every tag of a tag result (in place), so all chips get the same css class.
 * @param {{Tags: Tag[]}} tagRet - Tag result object.
 * @param {?string} className - Css class.
 * @returns {{Tags: Tag[]}} The same object.
 */
function SetTagClassName(tagRet, className) {
    for (const t of tagRet.Tags)
        t.Class = className;
    return tagRet;
}

(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Create an element with an optional class and text.
     * @param {string} tag - Tag name.
     * @param {?string} [cls] - Css class.
     * @param {?string} [text] - Text content.
     * @returns {HTMLElement} The element.
     */
    function make(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls)
            e.classList.add(cls);
        if (text != null)
            e.textContent = text;
        return e;
    }

    /**
     * Text of an error.
     * @param {*} e - Error or value.
     * @returns {string} Text.
     */
    function errText(e) {
        return (e && e.message) ? e.message : String(e);
    }

    /**
     * A tag gallery (see the file header).
     */
    class TagsGallery {
        /**
         * @param {object} o - Options:
         *  FindTags(searchText) -> Promise<{Tags}> (candidates for the add dialog),
         *  GetTags() -> Promise<{Tags}> (the current tags),
         *  RemoveTag(tag) -> Promise<boolean> (optional: remove buttons),
         *  AddTag(tag) -> Promise<boolean> (optional: the Add button),
         *  MaxTagCount {number} (the Add button shows while there are fewer tags; default unlimited),
         *  Options {TagGalleryParams} (texts, icons, classes).
         */
        constructor(o) {
            /** @type {object} The options. */
            this.O = o || {};
            /** @type {TagGalleryParams} Texts, icons and classes. */
            this.P = this.O.Options || new TagGalleryParams();
            const g = make("SysWeaver-TagsGallery");
            if (this.P.Embedded)
                g.classList.add("Embedded");
            if (this.P.Class)
                g.classList.add(this.P.Class);
            g.setAttribute("role", "list");
            /** @type {HTMLElement} The gallery element (expando AllTags = the tags of the last render). */
            this.E = g;
            /** @type {Tag[]|null} The tags of the last render. */
            this.Tags = null;
            g.AllTags = null;
            /** @type {Promise<void>} The first render. */
            this.Ready = this.Update();
        }

        /** @returns {Tag[]|null} The tags of the last render. */
        GetTags() {
            return this.Tags;
        }

        /**
         * Fetch the current tags and render the gallery.
         * @returns {Promise<void>}
         */
        async Update() {
            const o = this.O;
            const res = await o.GetTags();
            const tags = (res && res.Tags) || [];
            this.Tags = tags;
            this.E.AllTags = tags;
            const items = tags.map(t => this.Chip(t));
            const max = typeof o.MaxTagCount === "number" ? o.MaxTagCount : Infinity;
            if (o.AddTag && (tags.length < max)) {
                const add = this.AddButton(new Set(tags.map(t => t.Code)));
                if (this.P.AddAtFront)
                    items.unshift(add);
                else
                    items.push(add);
            }
            this.E.replaceChildren(...items);
        }

        /**
         * Create the chip of a tag (with a remove button when RemoveTag is set).
         * @param {Tag} tag - Tag.
         * @returns {HTMLElement} The `<SysWeaver-TagEdit>` element.
         */
        Chip(tag) {
            const p = this.P;
            const te = make("SysWeaver-TagEdit", tag.Class);
            te.setAttribute("role", "listitem");
            if (tag.Desc)
                te.title = tag.Desc;
            te.append(make("SysWeaver-TagText", null, tag.Name));
            const remove = this.O.RemoveTag;
            if (remove) {
                const b = new Sw.Button({
                    Icon: p.RemoveIcon,
                    Kind: "ghost",
                    Small: true,
                    Class: p.RemoveClass || undefined,
                    Title: Sw.Format.StringFormat(p.RemoveTitle, tag.Name),
                    OnClick: async () => {
                        try {
                            if (!await remove(tag))
                                Sw.Fail(p.RemoveError + "!");
                        }
                        catch (e) {
                            Sw.Fail(p.RemoveError + ": " + errText(e));
                        }
                        await this.Update();
                    },
                });
                te.append(b.E);
            }
            return te;
        }

        /**
         * Create the Add button: opens a searchable selection of FindTags results (existing tags excluded).
         * @param {Set<number>} existing - Codes of the current tags.
         * @returns {HTMLElement} The button element (class TagAdd).
         */
        AddButton(existing) {
            const o = this.O;
            const p = this.P;
            const all = p.ShowAllTags;
            const err = all ? p.FilterError : p.SearchError;
            const b = new Sw.Button({
                Text: p.AddText,
                Title: p.AddTitle,
                Icon: p.AddIcon,
                Class: p.AddClass || undefined,
                OnClick: async () => {
                    const tag = await Sw.Dialog.Select({
                        Title: all ? p.FilterText : p.SearchText,
                        Placeholder: all ? p.FilterTitle : p.SearchTitle,
                        GetAll: all,
                        Items: async search => {
                            let res;
                            try {
                                res = await o.FindTags(search);
                            }
                            catch (e) {
                                Sw.Fail(err + ": " + errText(e));
                                return [];
                            }
                            if (!res) {
                                Sw.Fail(err + "!");
                                return [];
                            }
                            return (res.Tags || []).filter(t => !existing.has(t.Code)).map(t => ({ Text: t.Name, Title: t.Desc || null, Value: t }));
                        },
                    });
                    if (!tag)
                        return;
                    try {
                        if (!await o.AddTag(tag))
                            Sw.Fail(p.AddError + "!");
                    }
                    catch (e) {
                        Sw.Fail(p.AddError + ": " + errText(e));
                    }
                    await this.Update();
                },
            });
            b.E.classList.add("TagAdd");
            return b.E;
        }
    }

    Sw.TagsGallery = TagsGallery;
})();

/**
 * LEGACY: create a tag gallery (use `new Sw.TagsGallery(...)`).
 * @param {function(string):Promise<{Tags: Tag[]}>} findTags - Candidate tags for a search text.
 * @param {function():Promise<{Tags: Tag[]}>} getTags - The current tags.
 * @param {?function(Tag):Promise<boolean>} removeTag - Remove a tag (truthy on success); null = no remove buttons.
 * @param {?function(Tag):Promise<boolean>} addTag - Add a tag (truthy on success); null = no Add button.
 * @param {number} maxTagCount - The Add button shows while there are fewer tags than this.
 * @param {TagGalleryParams} [options] - Options.
 * @returns {Promise<{Element: HTMLElement, Update: function():Promise<void>, GetTags: function():Tag[]}>} The gallery.
 */
async function CreateTagsGallery(findTags, getTags, removeTag, addTag, maxTagCount, options) {
    const g = new Sw.TagsGallery({ FindTags: findTags, GetTags: getTags, RemoveTag: removeTag, AddTag: addTag, MaxTagCount: maxTagCount, Options: options });
    await g.Ready;
    return {
        Element: g.E,
        Update: () => g.Update(),
        GetTags: () => g.GetTags(),
    };
}
