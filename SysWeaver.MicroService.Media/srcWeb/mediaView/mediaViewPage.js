/**
 * @file mediaViewPage.js - boot script of MediaView.html: runs Sw.MediaView.ViewMain (media.js) with the loading overlay.
 */
Sw.Page.Main(() => Sw.MediaView.ViewMain(), { Loading: true });
