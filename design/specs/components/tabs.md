# Tabs

**Purpose.** Switches between sibling views of the same scope without changing the query's meaning elsewhere: Fiado balance's **Dívidas em aberto** / **Saldo crédito**, the Plataforma area's list and lookup. The segmented-control variant handles small immediate preferences (theme **Claro / Escuro / Sistema**).

## Anatomy

Tab list (bottom rule `sem.border.default`) · tabs (`typography.role.label`; selected tab `sem.text.primary` with a `spacing.size.marker-width` underline in `sem.selection.indicator`; unselected `sem.text.secondary`) · optional count after the label (**Dívidas em aberto (12)**) · panel.

Segmented control: a bordered group (`sem.border.control`, `radius.control`) of equal buttons; the selected one filled with `sem.surface.selected` and marked by text weight and a check icon, not by color alone.

## States

Default, hover (`sem.surface.hover`), focus (`sem.focus.ring`), selected, disabled (with reason). Panels have their own loading/empty/error states.

## Behavior

- The selected tab is part of the preserved context (drill-down and return restore it).
- Each tab's panel keeps its own scroll position and filters within the session.
- Tabs never hide data needed for the other tab's decision; each tab shows its own total.

## Accessibility

Web tabs pattern (`role="tablist"`, arrow keys move between tabs, Tab moves into the panel); segmented control as a radio group. Native: an accessible tab bar or segmented control with selected state announced.

## Touch and scale

Tabs meet touch minimums; when labels do not fit, the tab list scrolls horizontally with visible overflow cues rather than truncating labels.
