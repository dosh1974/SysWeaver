

/**
 * @file Tag gallery control: renders a list of tags as removable chips plus an "Add" button that opens a
 * searchable selection pop-up. Optional library; no first-party page references it by name. A page using it
 * loads it with a script tag or `includeJs` after common/common.js and app/application.js, and links tags.css
 * and app/application.css.
 *
 * Globals defined: `TagGalleryParams`, `Tag`, `FilterTags`, `SetTagClassName`, `CreateTagsGallery`.
 * Dependencies: common.js (`_TF`, `ColorIcon`, `Button`, `ValueFormat`, `badClick`, `Fail`) and
 * application.js (`PopUpSelection`).
 * Conventions: tags are identified by their numeric `Code` (names may be localized); tag sources return
 * objects shaped `{Tags: Tag[]}`.
 */

/**
 * Options for `CreateTagsGallery`: texts (translated with `_TF` when the instance is created), icon classes,
 * extra CSS classes and layout flags. Create with `new TagGalleryParams()` and override fields as needed.
 */
class TagGalleryParams {
    /**
     * When true the add pop-up lists all tags and the input acts as a filter (Filter* texts, `getAll` mode of
     * PopUpSelection); when false the input is a search (Search* texts).
     * @type {boolean}
     */
    ShowAllTags = false;

    /** If true the Add button is inserted as the first child of the gallery, else appended last. @type {boolean} */
    AddAtFront = true;

    /** Optional extra CSS class added to the `<SysWeaver-TagsGallery>` element (e.g. "Keep" / "Ignore"). @type {?string} */
    Class = null;

    /** Add button text, tool tip, error message prefix, icon class and optional extra CSS class. @type {string} */
    AddText = _TF("Add", "The text of a button that when pressed will add a tag to a list of tags");
    AddTitle = _TF("Click to add a tag", "The tool tip description of a button that when pressed will add a tag to a list of tags");
    AddError = _TF("Failed to add tag", "An error message displayed when a tag couldn't be added to the list of tags");
    AddIcon = "IconTagAdd";
    AddClass = null;

    /**
     * Remove icon tool tip (format string, {0} = tag name, filled with ValueFormat.stringFormat), error message
     * prefix, icon class, optional extra CSS class and icon size in pixels (width = height).
     * @type {string}
     */
    RemoveTitle = _TF('Remove the tag "{0}"', "The tool tip description of a button that when pressed will remove a tag from a list of tags. {0} is replaced with the name of the tag");
    RemoveError = _TF("Failed to remove tag", "An error message displayed when a tag couldn't be removed from the list of tags");
    RemoveIcon = "IconTagRemove";
    RemoveClass = null;
    RemoveIconSize = 24;

    /** Pop-up input label, tool tip and error prefix used when ShowAllTags is false (search mode). @type {string} */
    SearchText = _TF("Search for a tag", "The text of a label for a text input field that is used for searching");
    SearchTitle = _TF("Enter some text to search for a tag", "The tool tip of a text input field that is used for searching");
    SearchError = _TF("Failed to find tags", "An error message that is displayed when a search for tags yielded no results");

    /** Pop-up input label, tool tip and error prefix used when ShowAllTags is true (filter mode). @type {string} */
    FilterText = _TF("Filter tags", "The text of a label for a text input field that is used for filtering");
    FilterTitle = _TF("Enter some text to filter the tags", "The tool tip of a text input field that is used for filtering");
    FilterError = _TF("Failed to filter tags", "An error message that is displayed when filtering tags yields no results");

    /** If true the gallery gets class `Embedded` (no border, padding or margin; see tags.css). @type {boolean} */
    Embedded = false;
}


/**
 * Shape of a tag as used by the gallery (documentation / default-value class; the gallery accepts any object
 * with these members). `FilterTags` additionally writes a `Rank` number onto matched tags.
 */
class Tag {
    Code = 0; // Unique code (since names may be localized)
    Name = null; // Name (shown)
    Desc = null; // Optional title (tooltip)
    Class = null; // Optional class (styling)
}

/**
 * Client-side tag search helper (e.g. for implementing a `findTags` callback). Case-insensitive.
 * A tag matches if its Name contains the text or its Desc contains it; note the Desc "hit count" is
 * `Desc.length - Desc.replace(text, "").length`, i.e. the text length if found (only the first occurrence is
 * removed). Rank = 0.25/len(text) * descHit + (100 - namePos), where namePos is replaced by 100 when the name
 * does not match (so earlier name matches rank higher). Writes `Rank` onto each
 * matched tag and returns them sorted by descending Rank. Requires every tag to have a non-null Desc.
 * When more than `maxCount` tags match, the sorted result is truncated to the first `maxCount` tags.
 * @param {Tag[]} tags - Tags to search.
 * @param {string} searchFor - Search text; falsy returns `tags` unchanged (same array).
 * @param {number} maxCount - Maximum result count (no limit when undefined).
 * @returns {Tag[]} The original array (empty search) or a new array of matching tags.
 */
function FilterTags(tags, searchFor, maxCount) {
    if (!searchFor)
        return tags;
    const sl = searchFor.toLowerCase();
    const isl = 0.25 / sl.length;
    const l = tags.length;
    const dest = [];
    for (let i = 0; i < l; ++i) {
        const s = tags[i];
        let tempName = s.Name.toLowerCase().indexOf(sl);
        const dl = s.Desc.toLowerCase();
        const tempDesc = dl.length - dl.replace(sl, "").length;
        if ((tempName < 0) && (tempDesc <= 0))
            continue;
        if (tempName < 0)
            tempName = 100;
        s.Rank = isl * tempDesc + (100 - tempName);
        dest.push(s);
    }
    const ol = dest.length;
    if (ol <= 1)
        return dest;
    dest.sort((a, b) => {
        const d = b.Rank - a.Rank;
        if (d < 0)
            return -1;
        if (d > 0)
            return 1;
        return 0;
    });
    if (ol > maxCount)
        dest.length = maxCount;
    return dest;
}


/**
 * Sets `Class` on every tag of a tag result (mutates in place), so all chips get the same CSS class.
 * @param {{Tags: Tag[]}} tagRet - Tag result object (as returned by getTags / findTags).
 * @param {?string} className - CSS class to assign to each tag.
 * @returns {{Tags: Tag[]}} The same `tagRet` object.
 */
function SetTagClassName(tagRet, className) {
    const tags = tagRet.Tags;
    const l = tags.length;
    for (let i = 0; i < l; ++i)
        tags[i].Class = className;
    return tagRet;
}


// findTags(searchText) returns an object with a Tags property { Tags: [] }.
// getTags() returns an object with a Tags property with the current tages { Tags: [] }.
// removeTag(tag) optional, used to remove tags, should return true if tag was removed.
// addTag(tag) optionsl, used to remove tags, should return true if tag was removed.
// maxTagCount maximum allowed tags to use.

/**
 * Creates a tag gallery element (`<SysWeaver-TagsGallery>`, plus class `Embedded` / `options.Class` as
 * configured) and renders it via the inner `update` function, which is also re-run after every add/remove.
 * Each current tag becomes a `<SysWeaver-TagEdit>` (class tag.Class, title tag.Desc) holding a
 * `<SysWeaver-TagText>` and, if `removeTag` is given, a remove `ColorIcon`. While the tag count is below
 * `maxTagCount` and `addTag` is given, an Add `Button` opens `PopUpSelection` whose item callback calls
 * `findTags(searchText)` and returns chip elements (class `TagSel`, tabIndex 0) for tags whose Code is not
 * already present; clicking or pressing Enter on one calls `addTag(tag)`, re-renders and closes the pop-up.
 * Failures (false result or exception) are reported with `Fail(<error text> + ...)`.
 * @param {function(string):Promise<{Tags: Tag[]}>} findTags - Returns candidate tags for a search text.
 * @param {function():Promise<{Tags: Tag[]}>} getTags - Returns the current tags.
 * @param {?function(Tag):Promise<boolean>} removeTag - Removes a tag, truthy on success; null = no remove icons.
 * @param {?function(Tag):Promise<boolean>} addTag - Adds a tag, truthy on success; null = no Add button.
 * @param {number} maxTagCount - Add button is shown only while there are fewer tags than this.
 * @param {TagGalleryParams} [options] - Options; a default `TagGalleryParams` when falsy.
 * @returns {Promise<{Element: HTMLElement, Update: function():Promise<void>, GetTags: function():Tag[]}>}
 *   The gallery element, the re-render function and a getter for the last fetched tag list (`g.AllTags`).
 */
async function CreateTagsGallery(findTags, getTags, removeTag, addTag, maxTagCount, options) {
    if (!options)
        options = new TagGalleryParams();
    const g = document.createElement("SysWeaver-TagsGallery");
    if (options.Embedded)
        g.classList.add("Embedded");
    g.AllTags = null;
    if (options.Class)
        g.classList.add(options.Class);

    /**
     * Re-renders the gallery: awaits `getTags()`, clears the element, stores the list in `g.AllTags`, builds
     * one chip per tag (with remove icon) and the Add button (see CreateTagsGallery). The remove icon handler
     * disables the icon, shows `IconWorking`, calls `removeTag(tag)` and re-renders.
     * @returns {Promise<void>}
     */
    let update = null;
    update = async function () {
        const tagRes = await getTags();
        g.innerText = "";
        const tags = tagRes.Tags;
        g.AllTags = tags;
        const tl = tags.length;
        const existing = new Map();
        for (let i = 0; i < tl; ++i) {
            const tag = tags[i];
            const te = document.createElement("SysWeaver-TagEdit");
            if (tag.Class)
                te.classList.add(tag.Class);
            existing.set(tag.Code, tag);
            te.title = tag.Desc;
            g.appendChild(te);
            const tex = document.createElement("SysWeaver-TagText");
            tex.innerText = tag.Name;
            te.appendChild(tex);
            if (removeTag) {
                const removeIcon = new ColorIcon(options.RemoveIcon, "IconColorThemeBackground", options.RemoveIconSize, options.RemoveIconSize,
                    ValueFormat.stringFormat(options.RemoveTitle, tag.Name), async ev => {
                        removeIcon.SetEnabled(false);
                        removeIcon.StartWorking("IconWorking");
                        try {
                            if (!await removeTag(tag))
                                Fail(options.RemoveError + "!");
                        }
                        catch (e) {
                            Fail(options.RemoveError + ": " + e);
                        }
                        await update();
                        removeIcon.StopWorking();
                    });
                if (options.RemoveClass)
                    removeIcon.Element.classList.add(options.RemoveClass);
                te.appendChild(removeIcon.Element);
            }
        }

        if (tl < maxTagCount) {
            if (addTag) {
                const allTags = options.ShowAllTags;
                const err = allTags ? options.FilterError : options.SearchError;
                const addButton = new Button(null, options.AddText, options.AddTitle, options.AddIcon, true, async () => {

                    await PopUpSelection(allTags ? options.FilterText : options.SearchText, allTags ? options.FilterTitle : options.SearchTitle, async (searchFor, close) => {
                        const tagEls = [];
                        let tagRes;
                        try {
                            tagRes = await findTags(searchFor);
                            if (!tagRes)
                                Fail(err + "!");
                        }
                        catch (e) {
                            Fail(err + ": " + e);
                            return tagEls;
                        }
                        if (!tagRes)
                            return tagEls;
                        const tags = tagRes.Tags;
                        if (!tags)
                            return tagEls;
                        const tagl = tags.length;
                        if (tagl <= 0)
                            return tagEls;
                        for (let i = 0; i < tagl; ++i) {
                            const tag = tags[i];
                            if (existing.get(tag.Code))
                                continue;
                            const tagE = document.createElement("SysWeaver-TagEdit");
                            if (tag.Class)
                                tagE.classList.add(tag.Class);
                            tagEls.push(tagE);
                            tagE.classList.add("TagSel");
                            tagE.title = tag.Desc;
                            const tagT = document.createElement("SysWeaver-TagText");
                            tagE.appendChild(tagT);
                            tagT.innerText = tag.Name;
                            tagE.tabIndex = "0";
                            /**
                             * Selects this pop-up chip: ignores bad clicks, shows the working icon on the Add
                             * button, detaches the chip handlers, calls `addTag(tag)` (Fail on false/exception),
                             * re-renders the gallery and closes the pop-up.
                             * @param {Event} ev - Click or Enter keyup event.
                             * @returns {Promise<void>}
                             */
                            const fn = async ev => {
                                if (badClick(ev))
                                    return;
                                addButton.StartWorking("IconWorking");
                                tagE.onclick = null;
                                tagE.onkeyup = null;
                                try {
                                    if (!await addTag(tag))
                                        Fail(options.AddError + "!");
                                }
                                catch (e) {
                                    Fail(options.AddError + ":\n" + e);
                                }
                                await update();
                                addButton.StopWorking();
                                await close();
                            };
                            tagE.onclick = fn;
                            tagE.onkeyup = async ev => {
                                if (ev.key === "Enter")
                                    await fn(ev);
                            };
                        }
                        return tagEls;
                    }, null, allTags);
                });
                if (options.AddClass)
                    addButton.Element.classList.add(options.AddClass);
                if (options.AddAtFront)
                    g.insertBefore(addButton.Element, g.firstChild);
                else
                    g.appendChild(addButton.Element);
            }
        }
    }
    await update();
    return {
        Element: g,
        Update: update,
        GetTags: () => g.AllTags,
    };
}

