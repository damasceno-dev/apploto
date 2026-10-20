# Notice

**Purpose.** An inline, persistent message in the flow of the page: API errors (rendered verbatim), the form error summary, **Resultado não confirmado**, permission and setup explanations, the earlier-days backlog banner, the stale-version reconciliation prompt, the retained-input notice, the branch-date-changed announcement. Notices stay until resolved; they are the opposite of a fleeting toast.

## Anatomy

Container (`sem.surface.default`, 1px `sem.border.default`, `radius.panel`, padding `spacing.inset.notice`) with a start-edge rule `spacing.size.marker-width` wide in the kind's `sem.feedback.<kind>.border` · icon · title (`typography.role.body-strong`) · body or list of messages (`typography.role.body`) · actions (Buttons, secondary/text variants; at most one primary). No large tinted fill — the rule and icon carry the kind, matching the direction's restraint.

## Variants

| Kind | Rule / icon | Use |
|---|---|---|
| Danger | `sem.feedback.danger.border`, `sem.feedback.danger.fg` | confirmed failures with verbatim `ErrorMessages` |
| Warning | `sem.feedback.warning.border`, `sem.feedback.warning.fg` | uncertain results, stale versions, backlog banner, unsaved-on-server |
| Info | `sem.feedback.info.border`, `sem.feedback.info.fg` | setup explanations, retained input, branch date changed |
| Success | `sem.feedback.success.border`, `sem.feedback.success.fg` | in-flow confirmation that must persist (invitation reissued, setup step done) |
| Neutral | `sem.feedback.neutral.border`, `sem.feedback.neutral.fg` | read-only reasons, locked periods |
| **Error summary** | danger kind at the top of a form | lists every message; each that names a field links to it |

## Content rules

- `ErrorMessages` render as a list, verbatim, in order — one item per string, no prefix, no merge, no truncation. Client copy (title, explanation) is separate from backend text.
- **Resultado não confirmado** uses the title plus **Não foi possível confirmar se a ação foi concluída.** and any backend messages, then only the recovery the catalog allows for that operation (**Verificar e tentar novamente**, **Recarregar e conferir**, **Entrar novamente**).
- The no-body fallback reads **Não foi possível concluir a ação. Verifique sua conexão e tente novamente.**
- A permission/setup notice integrates the backend message and the screen's guidance in one notice, not two banners.

## Behavior

Notices appear where the problem is (form top, region top), push content down rather than overlaying it, and disappear only when their cause is resolved or the user dismisses an informational one. A notice tied to a request is replaced, not stacked, by the next result of the same request.

## Accessibility

A failure that blocks the action is announced assertively once (`role="alert"` on insertion); informational and success notices use a polite status region. The error summary receives focus after a failed submit. Actions inside have specific names. Persistent visual presence accompanies every announcement.

## Touch and scale

Actions wrap below the text on compact layouts and meet touch minimums; long message lists wrap fully.
