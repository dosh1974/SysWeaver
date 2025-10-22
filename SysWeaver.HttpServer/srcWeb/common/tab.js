class Tab
{

    constructor(saveKey) {
        saveKey = saveKey ?? null;
        const e = document.createElement("SysWeaver-TabBar");
        this.Selected = -1;
        this.Element = e;
        if (saveKey) {
            saveKey = "SysWeaver.Tabs." + saveKey;
            try {
                const t = parseInt(localStorage.getItem(saveKey) ?? "-1");
                if (typeof t === "number")
                    this.Selected = t | 0;
            }
            catch
            {
            }
        }
        this.SaveKey = saveKey;
    }


    Select(index) {
        let save = true;
        const p = this.Element;
        const tabCount = p.childElementCount;
        let curSel = this.Selected;
        if ((typeof index === "undefined") || (index < 0)) {
            if ((curSel >= 0) && (curSel < tabCount))
                return;
            index = 0;
            if (tabCount <= 0) {
                this.Selected = -1;
                return;
            }
            save = false;
        }
        if (index == curSel)
            return;
        const tabActive = "tabactive";
        if ((curSel >= 0) && (curSel < tabCount)){
            const ce = p.children[curSel];
            ce.title = ce.Title;
            let fn = ce.OnActivate;
            if (fn) {
                if (typeof fn !== "function")
                    fn.classList.remove(tabActive);
            }
            fn = ce.OnDeactivate;
            if (fn) {
                if (typeof fn === "function")
                    fn(ce, curSel);
            }
            fn = ce.Element;
            if (fn)
                fn.classList.remove(tabActive);
            ce.classList.remove(tabActive);
        }
        const e = p.children[index];
        e.title = e.TitleSelected;
        e.classList.add(tabActive);
        let fn = e.OnActivate;
        if (fn) {
            if (typeof fn !== "function")
                fn.classList.add(tabActive);
            else
                fn(e, index);
        }
        fn = e.Element;
        if (fn)
            fn.classList.add(tabActive);
        if (save) {
            const saveKey = this.SaveKey;
            if (saveKey) {
                try {
                    localStorage.setItem(saveKey, "" + index);
                }
                catch
                {
                }
            }
        }
        this.Selected = index;
    }

    // Add a new tab, onActive takes one argument containing the element to add data to
    AddTab(name, onActivate, title, onDeactivate, element, titleSelected) {
        const t = this;
        const p = t.Element;
        const e = document.createElement("SysWeaver-Tab");
        e.Element = element;
        e.onclick = ev => {
            if (badClick(ev))
                return;
            let index;
            let q = e;
            for (index = -1; q; ++index)
                q = q.previousElementSibling;
            t.Select(index);
        };
        e.OnActivate = onActivate;
        e.OnDeactivate = onDeactivate;
        e.innerText = name;
        if (!title)
            title = "";
        if (!titleSelected)
            titleSelected = "";
        e.Title = title;
        e.TitleSelected = titleSelected;
        e.title = title;
        const tabIndex = p.childElementCount;
        p.appendChild(e);
        if (t.Selected < 0)
            t.Select(0);
        else {
            if (tabIndex === t.Selected)
            {
                t.Selected = -1;
                t.Select(tabIndex);
            }
        }
        return e;
    }

    RemoveTab(index) {
        const p = this.Element;
        if (this.Selected == index) {
            let newSel = index > 0 ? (index - 1) : (index + 1);
            const selMax = p.childElementCount - 1;
            if (newSel > selMax)
                newSel = selMax;
            this.Selected = -1;
            if (newSel >= 0)
                Select(newSel);
        }
        const sel = this.Selected;
        if (sel > index)
            this.Selected = sel - 1;
        e.remove();
    }
}

