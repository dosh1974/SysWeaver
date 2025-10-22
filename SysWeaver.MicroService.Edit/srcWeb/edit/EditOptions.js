class EditOptions {

    Title = true;
    Dev = true;
    ReadOnly = false;
    CanExpand = true;
    IsExpanded = true;
    KeyName = null;
    Border = true;
    ExpandAllCollections = false;
    MenuIcon = false;
    MenuIconClass = "SysWeaverEditIconMenu";
    MenuColorClass = "IconColorThemeAcc2";

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
        return e;
    }


    static CleanMenu = (() => {
        const o = new EditOptions();
        o.Title = false;
        o.CanExpand = false;
        o.IsExpanded = false;
        o.Border = false;
        o.Dev = false;
        return o;
    })();

    static CleanReadOnlyMenu = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.ReadOnly = true;
        return o;
    })();


    static Clean = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.MenuIcon = false;
        return o;
    })();

    static CleanReadOnly = (() => {
        const o = EditOptions.CleanMenu.Clone();
        o.ReadOnly = true;
        o.MenuIcon = false;
        return o;
    })();

}
