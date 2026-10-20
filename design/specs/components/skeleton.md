# Skeleton

**Purpose.** A content-shaped placeholder for the first load of a region, keeping layout stable and making clear that values are not known yet — never zero, never an all-clear.

## Anatomy

Bars and blocks in `sem.skeleton.base` with an optional moving highlight in `sem.skeleton.highlight`, `radius.xs`, sized like the content they stand for: table rows with column widths, a summary figure, a heading line, a badge. No spinners for whole regions.

## Behavior

- Used only for initial loads of a region; refreshes keep the old content marked **Atualizando…** (see [Table](table.md)).
- Shimmer uses `motion.transition.skeleton` (1200 ms linear loop) and becomes a static placeholder under `motion.reduced.skeleton`.
- Independent regions load and resolve independently; a slow region does not hold back others.
- A skeleton that lasts beyond the platform's request timeout turns into the region's error state, not an endless placeholder.

## Accessibility

The region is `aria-busy="true"` and announces **Carregando…** once; the skeleton shapes themselves are hidden from assistive technology. When content arrives, the busy state clears without stealing focus.

## Touch and scale

Shapes scale with text metrics so the layout does not jump when real text arrives at large sizes.
