# Design Agent Notes

## Direction

- Keep `design` non-runtime for now
- Use `design` for tokens, specs, assets, and references
- Share design guidance for colors, spacing, typography, radii, and core visual primitives

## What goes in `design`

- tokens
- specs
- assets
- references
- visual guidelines for web and mobile

## Rules

- `design/` is not a runtime component package
- `web` owns web components
- `mobile` owns mobile components
- `design/` provides the shared visual source of truth
- Do not create shared runtime UI packages in `design` at this stage
- Do not force a single cross-platform component library unless there is a clear later need
- Keep the design system pragmatic and code-first
