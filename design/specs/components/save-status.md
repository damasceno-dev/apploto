# SaveStatus

**Purpose.** The persistent status line of a form that saves to the server — chiefly Close day and Fix & resubmit — telling the operator, truthfully and at a glance, whether the count on screen is the count on the server. The behavior it reports is owned by the catalog's [close-day autosave pattern](../../docs/screens.md#close-day-autosave-dirty-save-and-repeat-undo).

## Anatomy

Icon + one line of text (`typography.role.helper`) placed beside the **Salvar** button and repeated near the top of long forms on compact layouts; the explicit **Salvar** button; optional retry action.

## States

| State | Text | Tokens |
|---|---|---|
| Clean, saved | **Salvo às {HH:mm}** from the acknowledged `DailyClose.UpdatedAt` (with date for a historical day: **Salvo em 23/09 às 18:42**); **Salvo** when no authoritative time exists | `sem.text.muted`, check icon `sem.feedback.success.fg` |
| Never recorded | **Contagem ainda não salva** (Save enabled even with no edits) | `sem.text.secondary` |
| Dirty (autosave pending) | **Alterações não salvas** while the debounce runs | `sem.text.secondary` |
| Saving | **Salvando…** | spinner, `sem.text.secondary` |
| Unsaved on server (correction, recheck, historical day) | **Alterações não salvas no servidor** — explicit **Salvar** required | `sem.feedback.warning.fg` |
| Failed | **Não salvo — tentar novamente** with the verbatim messages in a Notice | `sem.feedback.danger.fg` |
| Uncertain | **Resultado não confirmado** — **Recarregar e conferir** | `sem.feedback.warning.fg` |
| Stale | **Este fechamento mudou em outro lugar** — **Recarregar e conferir** | `sem.feedback.warning.fg` |
| Device only | **Rascunho salvo neste dispositivo — ainda não salvo no servidor** | `sem.feedback.info.fg` |
| Storage failed | **Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.** | `sem.feedback.danger.fg` |

## Behavior

- The **Salvar** button is visible in every state; it is disabled (with the clean reason) only when clean or while saving.
- The status never claims a save that the server did not acknowledge; a preview, a device copy or a dispatched request is not "Salvo".
- Edits made while an older snapshot is saving keep the status dirty after that save returns.

## Accessibility

A polite live region that announces the final state after a save settles (**Salvo às 14:32**), never each keystroke or debounce tick; failures and uncertainty are announced assertively once. The text is always visible, not only announced.

## Touch and scale

Wraps under the button at large text; the retry action meets touch minimums.
