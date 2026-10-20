# Card

**Purpose.** A bounded panel that groups one coherent unit of work when a rule alone would not hold it together: the Cockpit's next action, the owner setup checklist, a login sheet, a filter toolbar, a form section on a busy screen. Cards are used sparingly — the direction prefers ruled sections over piles of boxes, and never uses tinted highlight cards.

## Anatomy

Container (`sem.surface.default`, 1px `sem.border.default`, `radius.panel`, padding `spacing.inset.panel`) · optional overline/eyebrow (`typography.role.overline`, `sem.text.muted`) · heading (`typography.role.section-title`, or `typography.role.next-action` for the Cockpit) · body · actions row. An optional top or left accent rule (`spacing.size.marker-width`) marks priority: `sem.action.primary.bg` for the next action, a feedback border for a status card.

## Variants

| Variant | Use |
|---|---|
| Section panel | groups a form or filter set; no shadow |
| Next action | the Cockpit's single recommended step: top rule `sem.action.primary.bg`, heading `typography.role.next-action`, one CTA [Button](button.md) plus at most one secondary action |
| Checklist | the owner setup checklist: list of items with status text and a link each |
| Sheet | centered auth surfaces (Login, Invitation): maximum width `spacing.size.dialog-max`, page background around it |

There is no interactive whole-card click: a card's actions are explicit buttons or links.

## States

Loading (the card keeps its frame and shows a [Skeleton](skeleton.md) inside), error (a [Notice](notice.md) inside the card, keeping the card's heading so the region stays identifiable), empty (an [EmptyState](empty-state.md) inside).

## Accessibility

A card with a heading is a labelled region when it is a landmark-worthy unit (the Cockpit next action: `region` named by its heading); otherwise it is just grouped content. Reading order: eyebrow, heading, body, actions.

## Touch and scale

Padding stays `spacing.inset.panel` on compact layouts; content inside stacks (actions wrap below text). Cards never sit side by side below `breakpoint.medium`; this is the fix for the prototype's squeezed Cockpit grid.

## Web and native

Same anatomy; native may drop the border in favor of a surface change only when the surface contrast keeps the boundary visible — otherwise keep the 1px border.
