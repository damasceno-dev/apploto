# Stepper

**Purpose.** Shows a guided journey's steps and each step's **confirmed** status: the Plataforma Nova lotérica wizard, Add employee and Offboard employee. Because these journeys compose separate commands with no all-in-one transaction, the stepper's job is honesty — done means the server confirmed it, not that the form was filled.

## Anatomy

Ordered list of steps · each step: index or status icon, title (`typography.role.label`), one-line status text (`typography.role.helper`) · current step marked with `sem.action.primary.bg` index and bold title · the active step's content below (wide: steps in a start column; compact: a compact header **Passo 2 de 4 · Terminais e contas** with a disclosure listing all steps).

## Step statuses

| Status | Presentation |
|---|---|
| Not started | neutral index, `sem.text.secondary` |
| Current | primary index, `sem.text.primary` |
| Saving | spinner, **Salvando…** |
| Done | check icon with `sem.feedback.success.fg`, confirmed values in the status line (**Operador criado: Ana Lima**) |
| Failed | error icon with `sem.feedback.danger.fg`, the first verbatim message and **Tentar novamente** |
| Uncertain | warning icon with `sem.feedback.warning.fg`, **Resultado não confirmado** and the step's verification action |
| Skipped / optional | `sem.text.muted`, **Opcional — não feito** |
| Unavailable | lock icon, reason (**Você não pode remover o próprio acesso**) |

## Behavior

- Each step commits on its own; moving back never undoes a done step. The wizard (Plataforma) differs: its steps only collect input and the single final command commits all of it, so its statuses before submission are **preenchido** / **incompleto**.
- Leaving with **Concluir depois** keeps done steps; returning re-derives statuses from server reads.
- Errors keep the step's input.

## Accessibility

An ordered list with the current step `aria-current="step"`; status text is part of each item. Moving to a step moves focus to its heading. Status changes announce politely; failures assertively.

## Touch and scale

Step items are not tiny dots: each has its title and status text; on compact layouts the step list lives in the disclosure, so the active form gets the width.
