# Lotero — selected visual direction

> **Owner decision · 2026-10-20 · Design M1 / D03:** The owner ended the visual exploration and selected the **existing Claude clickable prototype** as Lotero's direction: “we are going with the claude idea”; the GPT and Google results did not improve on its appearance in the owner's judgment. This supersedes the pending-selection records of 2026-10-18/19. **M1's visual choice is locked and may feed M2.** This records the owner's choice, not a claim that the prototype is production-ready or that the new handoff has passed independent technical review.

## Selection and scope

The selected artifact is the original Claude prototype, build **`2026-10-08.c9-seed-v3`**, not an assumed Claude Design export. There is no verified published artifact URL or tool-session provenance for its generation. The owner's comparison and taste decision are the evidence for choosing it; no comparative scores or accessibility endorsements have been inferred.

The owner has closed the exploration now. The earlier plan to compare Dashboard before choosing a direction no longer blocks M1: Dashboard will inherit this visual language when its actual reporting contract and catalog entry are ready. This does not approve invented Dashboard metrics, choose its final layout, change the Gestão home, or close its backend/catalog gates. The existing 33 prototype screens are visual examples; they do not expand the current catalog or certify its behavior.

The prototype was run unchanged on an isolated loopback origin on **2026-10-20**. Static evidence covers all 33 screen paths, with additional light/dark, narrow-layout, focus, dialog and simulated-state references. See the [selected reference index](../references/claude/selected-2026-10-20/README.md), [source and capture manifest](../references/claude/selected-2026-10-20/manifest.json), and [implementation handoff](../references/claude/implementation-handoff.md). The earlier October 15 captures remain historical evidence.

## The direction to preserve

- **Character:** calm, practical, finance-oriented; clear tasks and legible records rather than decorative dashboards.
- **Palette:** deep blue navigation and blue ordinary actions; white/cool-grey work surfaces; restrained orange for the principal operator next action. Dark mode uses deep blue-black surfaces with brighter blue links and semantic feedback. Turquoise is a secondary contextual accent, not another primary CTA.
- **Typography:** system UI sans-serif, strong but compact headings, labelled controls, tabular numerals and right-aligned money. Do not introduce a downloaded display face or a CAIXA wordmark.
- **Geometry and density:** 4px spacing base; rectangular controls and panels with corners no greater than 4px; thin dividers; little elevation outside menus, drawers and dialogs. Small rectangular status badges retain text. Avatars and tiny indicator dots may remain circular.
- **Shell:** persistent grouped blue sidebar when space permits; a compact top bar with branch/session context, theme controls and avatar menu; an off-canvas drawer on narrow web. Gestão ↔ Meu turno belongs in the avatar menu and keeps the catalog's permissions and route ownership.
- **Work hierarchy:** manager exception sections with scannable tables; one clear operator next action; visible access to today's close even when another task has priority; filter controls above ledgers; form and audit sections divided by rules.
- **Themes:** light-first, with Claro / Escuro / Sistema as a device preference. Both themes belong to the implementation. Retain selected readability while supporting browser/platform text enlargement.

The [implementation handoff](../references/claude/implementation-handoff.md) supplies concrete palette pairs, authored sizes, component anatomy, motion/layers, screen families and the required adaptations. Its [style inventory](../references/claude/selected-2026-10-20/style-inventory.json) records observed values; it is **not** the M2 token system or a runtime stylesheet.

## Adaptations are part of the handoff

Preserve the direction while correcting the observed narrow Cockpit grid, clipped/competing header context, insufficient contrast in the named pairs, small controls, and the prototype's CSS-zoom/text-size clamp. The desktop screenshots normally use **150% prototype zoom**; copying 14px authored text at 100% would not reproduce that reading size. M2 curates a platform-appropriate scale and validates it; global CSS zoom and runtime breakpoint rewriting are not the implementation pattern.

Remove prototype controls, demo logins, fake clock/data, source/gap annotations and fixed statebar from the product. The current [catalog](screens.md), [product brief](product.md), [copy](../specs/copy-guidelines.md), [formatting](../specs/formatting-ptbr.md), actual API and feature gates remain authoritative. In particular, server-owned variance/FIFO/authorization/atomicity must never be copied from the fake database. Older screen text about pending contracts is not current evidence of an API gap.

## What proceeds next

1. **Design M2 · Phases 1–4:** turn the observed inputs into the specified primitive → semantic → domain JSON model, with light/dark contrast validation and complete typography, spacing, geometry, motion, touch and layer guidance. M1 introduces no token files or completed M2 checkboxes.
2. **Design M3:** specify each component's anatomy, tokens, interaction, access and all states. Web and mobile implement their own components; `design/` stays non-runtime.
3. **Design M4:** produce the shell and feature blueprints just ahead of their app milestones, including a usable single-column mobile Cockpit. Retain the catalog's per-feature gap gates, notably retired-product recovery and uncataloged account/Plataforma screens.
4. **Web/Mobile foundations:** follow their own prerequisite maps; scaffold work may proceed when its Shared prerequisites are ready. App design-system implementation consumes Design M2 + M3; feature UI also waits for the corresponding M4 blueprint. Dashboard and unanswered product rules continue on their existing tracks.

The selection is an owner decision. The extraction documents and implementation adaptations remain reviewable technical work; this record does not assert an independent review, WCAG certification, native-device acceptance, full financial-flow validation or production launch acceptance.

> **Follow-up · 2026-10-20 · D04:** the direction above is now encoded as Design M2 [tokens](../tokens/README.md), the M3 [component contract](../specs/components/README.md) and the M4 [screen blueprints](../specs/screens/README.md), with the four contrast failures (and three more found by the gate) corrected inside the same look. The account and Plataforma screens are cataloged (0.4.6). This record's owner decision is unchanged. M2 token fixes passed independent repeat verification against v54; 0.4.6 and M4 are separate acceptance scopes despite sharing the D04 commit. Their corrected semantics, the component credential-retention clarification and the final integrated server references retain the review gates in the milestone plan.
