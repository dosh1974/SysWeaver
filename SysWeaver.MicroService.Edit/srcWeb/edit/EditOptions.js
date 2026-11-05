/**
 * @file Compatibility stub: `Sw.Edit.Options` (legacy global `EditOptions`) is defined by Edit.js itself, so options can be created
 * right after Edit.js ran (before `await Sw.Edit.Ready`). Edit.js no longer loads this file; pages that include it get the same
 * class. Fields and presets: README.md ("Options").
 */
(function () {
    "use strict";
    if (!window.Sw || !Sw.Edit || !Sw.Edit.Options)
        console.error("EditOptions.js: include edit/Edit.js first (it defines Sw.Edit.Options).");
    else if (!window.EditOptions)
        window.EditOptions = Sw.Edit.Options;
})();
