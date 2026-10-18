# Claude — historical prototype reference

These are actual captures made on **2026-10-15**, retained as historical comparison material. **No M1 direction is selected or locked.** The owner reset the earlier selection record on **2026-10-18** and will compare visual tools using the Rascunho guide after backend and Shared completion; [`decisions.md`](../../docs/decisions.md) records that pending process. These captures do not establish a Claude Design trial or a production implementation, and their styling is not binding token input.

## Provenance

The original remains outside this checkout at `../Prototypes/prototype-claude/` (relative to the checkout root). Its displayed build is **`2026-10-08.c9-seed-v3`**. It contains a local fake database, calculations and state controls; its clock is frozen at **24/09/2026 · 14:32**. None of that code is included here or consumed by an app. There is no published Claude artifact URL available in this handoff.

[`manifest.json`](manifest.json) records SHA-256 fingerprints for all 111 non-hidden source files, the captured files, their screen paths, demo roles, themes, requested browser viewports and actual raster dimensions. This preserves attribution without depending on a machine-specific absolute path or a running server. Source files were served unchanged by a temporary loopback-only HTTP server. The browser used the demo login, branch and workstation controls, navigation, theme selection and one keyboard Tab action; no financial value was changed or saved and no real service or message was used.

The screenshot API returned JPEG bytes; the `.jpg` files preserve those bytes without cropping, retouching or synthesis. Requested desktop viewport: **1440 × 1000**, returned raster: **1425 × 990**. Requested narrow viewport: **390 × 844**, returned raster: **375 × 812**. These are browser captures, not native-device screenshots. They show a viewport, not an entire page.

The prototype's text preference is **150%**. Its `assets/theme.js` uses CSS zoom, clamped by available width: effective factor **1.5** for desktop and **1.0** for the narrow captures. The preference label still says 150% on narrow screens. Apparent screenshot sizes therefore differ from authored CSS sizes. The production system must support platform text scaling; the prototype's zoom and breakpoint rewriting are not a runtime recipe.

## Representative evidence

| Context | Light | Dark | What the capture establishes |
|---|---|---|---|
| Manager · desktop · Work Queue | [View](manager-desktop-light.jpg) | [View](manager-desktop-dark.jpg) | Blue navigation, cool surfaces, exception sections and compact tabular hierarchy |
| Manager · narrow web · Work Queue | [View](manager-mobile-light.jpg) | [View](manager-mobile-dark.jpg) | Wrapping filters and a horizontally scrolling table container |
| Member/operator · desktop · Cockpit | [View](operator-desktop-light.jpg) | [View](operator-desktop-dark.jpg) | One next-action block, sparse orange CTA, own/other Draft context |
| Member/operator · narrow web · Cockpit | [View](operator-mobile-light.jpg) | [View](operator-mobile-dark.jpg) | Same colors and content, **with a layout defect requiring adaptation** |

The operator is the fake Joana session on Terminal 2, with P3 blockers (four own Drafts and one other operator's). The manager is the fake Ricardo session. The images are visual evidence only; these fixtures do not define shop facts, account assignments or API semantics.

Additional detail:

- [Open narrow navigation drawer, light](manager-mobile-drawer-light.jpg): branch context, grouped navigation and backdrop.
- [Close day, desktop, light, keyboard focus](operator-count-desktop-focus-light.jpg): Tab from the Dinheiro input to its repeat action; visible focus treatment, aligned opening values and editable count column. No value was changed.
- [Close day, narrow web, light](operator-count-mobile-light.jpg): the same form scrolled to its middle; the table keeps its own horizontal scroll. This is a detail capture, not proof that all fields/actions fit the viewport.

## Reading limits

The narrow Cockpit keeps a two-column grid and squeezes the next-action block into a very thin column. **Do not reproduce that layout.** M4 must stack the blocks, keep the main action usable and test long pt-BR text and scaled text. The fixed prototype state bar also obscures content in several captures; production has no such bar or its reserved bottom padding.

The dashed “contrato pendente · gap W” annotation is stale: the current catalog and real `ResponseDashboardJson` already cover backlog and unlocked Draft counts. Likewise, a prototype control, source badge, state switcher, fake saved state or report link does not establish a shipped contract. Consult the live catalog's feature gates.

These captures establish appearance at the listed states. They do not establish contrast compliance, complete keyboard order, screen-reader announcements, touch-target compliance, native readiness, session recovery, atomic financial behavior or complete loading/error coverage. The source contains focus and reduced-motion styles; those remain implementation and verification work in M2–M4 and the apps.
