/**
 * @file mediaPreviewPage.js - boot script of MediaPreview.html: runs Sw.MediaView.PreviewMain (media.js, which signals
 * Sw.Page.Loaded itself; no loading overlay so screenshots never contain it).
 */
Sw.Page.Main(() => Sw.MediaView.PreviewMain());
