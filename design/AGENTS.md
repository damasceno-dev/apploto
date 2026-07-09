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

## Rules

- `design/` is not a runtime component package
- `web` owns web components
- `mobile` owns mobile components
- `design/` provides the shared visual source of truth
- Do not create shared runtime UI packages in `design` at this stage
- Do not force a single cross-platform component library unless there is a clear later need
- Keep the design system pragmatic and code-first
