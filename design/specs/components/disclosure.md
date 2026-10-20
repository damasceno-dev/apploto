# Disclosure

**Purpose.** Collapsed secondary content the user may open on demand: **Ajuda sobre diferença de caixa** (the generic formula and teaching text, kept out of the main area), **Ver o que mudou** (before/after details of an opening change), expandable cheque-plan groups, optional checklist items.

## Anatomy

Trigger row (chevron icon rotating 90°, label in `sem.action.ghost.fg` with `typography.role.label`) · optional summary on the trigger line (**3 produtos alterados**) · content region (indented by `spacing.inset.panel` or bordered on the start edge with `sem.border.default`).

## States

Collapsed (default unless the catalog says otherwise), expanded, focus (`sem.focus.ring` on the trigger), loading content (Skeleton inside, when the content is fetched on expand), unavailable content (the catalog's honest fallback text, e.g. **Detalhes da alteração indisponíveis para este fechamento**).

## Behavior

Toggling never navigates and never changes data. Expansion state is kept while the screen lives (and across a return when the screen restores context); help disclosures start collapsed on every new visit. The chevron rotation uses `motion.transition.disclosure` (instant under `motion.reduced.disclosure`).

## Accessibility

A button with `aria-expanded` and `aria-controls`; native expandable element with state announced. The label stays the same in both states; the state is conveyed by the expanded attribute and the icon.

## Touch and scale

The whole trigger row is the hit area (`spacing.size.touch-target-min`); content reflows at any text size.
