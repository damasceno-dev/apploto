# Design Agent Notes

## Direction

- Keep `design` non-runtime for now
- Use `design` for tokens, specs, assets, and references
- Share design guidance for colors, spacing, typography, radii, and core visual primitives

## Writing style

Write the text with a natural, engaging, and highly fluid narrative style. Focus on readability so that it flows effortlessly, much like a well-written book. To achieve this, follow these guidelines:

1. **Seamless transitions:** use natural linking words and phrases to connect sentences and paragraphs logically. Do not leave concepts disconnected.
2. **Cohesive vocabulary:** avoid robotic, overly complex jargon or disjointed phrasing. The vocabulary should feel tightly knit and natural.
3. **Show, don't just tell:** thread the information together like a cohesive story or an engaging article, keeping the reader hooked from start to finish.

## What goes in `design`

- tokens
- specs
- assets
- references
- visual guidelines for web and mobile

## Design system layout

- `docs/` — product framing, the semantic screen catalog (`screens.md`), milestones and the visual-direction decision.
- `tokens/` — plain nested JSON, one file per category (`colors`, `typography`, `spacing`, `radii`, `shadows`, `breakpoints`, `motion`, `layers`); the model and every table live in `tokens/README.md`.
- `specs/components/` — the component contract (Design M3): anatomy, states, token mapping and accessibility per component; `specs/components/README.md` is the index.
- `specs/screens/` — per-feature screen blueprints (Design M4); `specs/screens/README.md` maps every cataloged screen to its blueprint.
- `specs/` root — pt-BR formatting and copy contracts that `shared/core` and both apps implement.
- `references/` — inputs only (palette reference, selected screenshots and measurements); never executable prototypes.

## Token model

- Three layers: **primitive → semantic → domain**. Semantic and domain colors are keyed under `light` and `dark` with identical names; domain groups mirror backend enums value-for-value (`daily-close-status.Submitted`).
- Values are literals or whole-value `{path.to.token}` references; keys are letters, digits and hyphens. Primitives are literals; semantic tokens reference primitives; domain tokens reference same-theme semantic tokens or primitives.
- **Apps consume semantic and domain tokens only**, at build time, through their own adapter (web Tailwind preset, mobile NativeWind theme). No raw hex and no primitive reference outside that adapter; nothing imports `design/` at runtime.
- Units: logical px for type, spacing, sizes, radii and breakpoints (web emits rem/em, native dp/pt with OS font scaling for text); ms and cubic-bezier for motion; integers for layers.
- Validate every change with `node scripts/check-contrast.mjs` (resolution, structure, enum coverage against the committed OpenAPI document, WCAG contrast in both themes, coverage of every text and indicator token) and `node scripts/test-check-contrast.mjs` (disposable negative cases). Never relax thresholds or drop a pair to make the gate pass; a passing gate proves color pairs, not overall accessibility.
- Component specs and blueprints name tokens by their semantic/domain path and never introduce a parallel palette; `node scripts/check-design-docs.mjs` verifies those references, local links, screen coverage and copy labels (`--scope components` checks tokens and component specs alone).

## Rules

- `design/` is not a runtime component package
- `web` owns web components
- `mobile` owns mobile components
- `design/` provides the shared visual source of truth
- Do not create shared runtime UI packages in `design` at this stage
- Do not force a single cross-platform component library unless there is a clear later need
- Keep the design system pragmatic and code-first
