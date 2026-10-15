# icons

[⬆ srcWeb](../README.md)

> The general-purpose monochrome UI icons of SysWeaver, served as `icons/<name>.svg`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../README.md) |
| **Kind** | Icons |

## Purpose

The build runs `SvgOpt.exe -RemoveFill -RemoveSize` on `icons\*.svg` (compared against the untracked `WebCompare` folder) into `icons\.opt\`, then compresses `.opt` (recursively) with Brotli into `web/icons`. Removing fill and size lets pages colour and size the icons with CSS, typically as a mask image (e.g. `mask-image: url("../icons/plus.svg")` in the web application styles). Menu entries and pages throughout the framework reference these icons by name.

The sub folders are processed differently: [`color/`](color/README.md) and [`external/`](external/README.md) keep their colours, and [`ext/`](ext/README.md) is compressed without optimisation.

[`FixIcons.bat`](../../FixIcons.bat) in the project folder runs the same fill and size removal and copies the results back over the source files here.

## Contents

| Item | Description |
|---|---|
| [`color/`](color/README.md) | Coloured icons (fill kept), served at the `icons/` root |
| [`ext/`](ext/README.md) | 278 ready-made file extension icons, served at `icons/ext/<ext>.svg` |
| [`external/`](external/README.md) | Logos of external services (Google Maps, Google Search, Wikipedia) |
| `user*.svg` (15 files) | Users and account actions, e.g. [`user.svg`](user.svg), [`users.svg`](users.svg), [`user_add.svg`](user_add.svg), [`user_logout.svg`](user_logout.svg), [`user_password.svg`](user_password.svg), [`user_phone_change.svg`](user_phone_change.svg) |
| `table*.svg` (18 files) | Data tables, mostly for the debug tables, e.g. [`table.svg`](table.svg), [`table_api.svg`](table_api.svg), [`table_country.svg`](table_country.svg), [`table_session.svg`](table_session.svg), [`table_translate_to.svg`](table_translate_to.svg) |
| `preview_*.svg` (10 files) | Media preview controls, e.g. [`preview_play.svg`](preview_play.svg), [`preview_pause.svg`](preview_pause.svg), [`preview_mute.svg`](preview_mute.svg), [`preview_vol_loud.svg`](preview_vol_loud.svg) |
| `theme*.svg` (5 files) | Theme selection: [`theme.svg`](theme.svg), [`theme_auto.svg`](theme_auto.svg), [`theme_dark.svg`](theme_dark.svg), [`theme_hacker.svg`](theme_hacker.svg), [`theme_light.svg`](theme_light.svg) |
| `db*.svg` (5 files) | Databases: [`db.svg`](db.svg), [`db_add.svg`](db_add.svg), [`db_minus.svg`](db_minus.svg), [`db_remove.svg`](db_remove.svg), [`db_settings.svg`](db_settings.svg) |
| Movement and navigation (14 files) | [`AddAbove.svg`](AddAbove.svg), [`AddBelow.svg`](AddBelow.svg), [`MoveTop.svg`](MoveTop.svg), [`MoveUp.svg`](MoveUp.svg), [`MoveDown.svg`](MoveDown.svg), [`MoveBottom.svg`](MoveBottom.svg), [`up.svg`](up.svg), [`down.svg`](down.svg), [`left.svg`](left.svg), [`right.svg`](right.svg), [`next.svg`](next.svg), [`prev.svg`](prev.svg), [`expand.svg`](expand.svg), [`collapse.svg`](collapse.svg) |
| `m_*.svg` (3 files) | [`m_menu.svg`](m_menu.svg), [`m_sort_asc.svg`](m_sort_asc.svg), [`m_sort_desc.svg`](m_sort_desc.svg) |
| `os_*.svg`, `rating_*.svg` (6 files) | Operating systems ([`os_linux.svg`](os_linux.svg), [`os_mac.svg`](os_mac.svg), [`os_windows.svg`](os_windows.svg)) and rating stars ([`rating_on.svg`](rating_on.svg), [`rating_half.svg`](rating_half.svg), [`rating_off.svg`](rating_off.svg)) |
| On / off pairs (12 files) | [`eye.svg`](eye.svg) / [`eye_off.svg`](eye_off.svg), [`fav_on.svg`](fav_on.svg) / [`fav_off.svg`](fav_off.svg), [`mic_on.svg`](mic_on.svg) / [`mic_off.svg`](mic_off.svg), [`fullscreen_enter.svg`](fullscreen_enter.svg) / [`fullscreen_exit.svg`](fullscreen_exit.svg), [`lock.svg`](lock.svg) / [`unlock.svg`](unlock.svg), [`on.svg`](on.svg) / [`off.svg`](off.svg) |
| Other actions and objects (78 files) | Generic UI actions and symbols, e.g. [`search.svg`](search.svg), [`edit.svg`](edit.svg), [`trash.svg`](trash.svg), [`copy.svg`](copy.svg), [`paste.svg`](paste.svg), [`download.svg`](download.svg), [`upload.svg`](upload.svg), [`settings.svg`](settings.svg), [`help.svg`](help.svg), [`home.svg`](home.svg), [`explore.svg`](explore.svg), [`warning.svg`](warning.svg), [`Loading.svg`](Loading.svg), [`not_found.svg`](not_found.svg), [`cash_bill.svg`](cash_bill.svg), [`credit_card.svg`](credit_card.svg), [`cpu.svg`](cpu.svg), [`server.svg`](server.svg), [`world.svg`](world.svg) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../README.md)
- **Used by:** menus and pages across the framework, e.g. the web application shell in [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/srcWeb/README.md) and the icon gallery in [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md)
