

class TagGalleryParams {
    ShowAllTags = false;

    AddAtFront = true;

    Class = null;

    AddText = _TF("Add", "The text of a button that when pressed will add a tag to a list of tags");
    AddTitle = _TF("Click to add a tag", "The tool tip description of a button that when pressed will add a tag to a list of tags");
    AddError = _TF("Failed to add tag", "An error message displayed when a tag couldn't be added to the list of tags");
    AddIcon = "IconTagAdd";
    AddClass = null;

    RemoveTitle = _TF('Remove the tag "{0}"', "The tool tip description of a button that when pressed will remove a tag from a list of tags. {0} is replaced with the name of the tag");
    RemoveError = _TF("Failed to remove tag", "An error message displayed when a tag couldn't be removed from the list of tags");
    RemoveIcon = "IconTagRemove";
    RemoveClass = null;
    RemoveIconSize = 24;

    SearchText = _TF("Search for a tag", "The text of a label for a text input field that is used for searching");
    SearchTitle = _TF("Enter some text to search for a tag", "The tool tip of a text input field that is used for searching");
    SearchError = _TF("Failed to find tags", "An error message that is displayed when a search for tags yielded no results");

    FilterText = _TF("Filter tags", "The text of a label for a text input field that is used for filtering");
    FilterTitle = _TF("Enter some text to filter the tags", "The tool tip of a text input field that is used for filtering");
    FilterError = _TF("Failed to filter tags", "An error message that is displayed when filtering tags yields no results");

    Embedded = false;
}


class Tag {
    Code = 0; // Unique code (since names may be localized)
    Name = null; // Name (shown)
    Desc = null; // Optional title (tooltip)
    Class = null; // Optional class (styling)
}

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
        if (tempName <= 0)
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
        dest.slice(maxCount, ol - maxCount);
    return dest;
}


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

async function CreateTagsGallery(findTags, getTags, removeTag, addTag, maxTagCount, options) {
    if (!options)
        options = new TagGalleryParams();
    const g = document.createElement("SysWeaver-TagsGallery");
    if (options.Embedded)
        g.classList.add("Embedded");
    g.AllTags = null;
    if (options.Class)
        g.classList.add(options.Class);

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

