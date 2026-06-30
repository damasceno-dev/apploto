# Caixa / Loterias CAIXA — Palette Reference

> **Status:** Reference (color-inspiration input to M1 Visual Direction and M2 Tokens; not a checkbox deliverable)
> **Source:** `manual-de-identidade-visual-loterias-caixa.pdf`, §7.2.1 *paleta de cores* (internal pp. 416–420).
> **Role:** *reference only* — one input to the **Visual Direction** milestone, **not our palette.** Our palette is inspired by the Caixa blue/orange but built with our own tone (warmer / higher-contrast / more modern, as decided in Visual Direction); M2 then builds the ramps from that decision. See "What we do differently" below.

RGB values are transcribed verbatim from the manual; hex is the direct conversion. Pantone refs kept for traceability.

## Paleta fixa — institutional anchor (the part we lean on)

| Name                  | RGB           | Hex       | Pantone | Note                                                      |
|-----------------------|---------------|-----------|---------|-----------------------------------------------------------|
| **Azul CAIXA**        | 0, 92, 169    | `#005CA9` | 287C    | The primary brand blue — our `primary` anchor candidate   |
| **Laranja CAIXA**     | 243, 146, 0   | `#F39200` | 151C    | The accent orange — our `accent` candidate                |
| **Turquesa**          | 84, 187, 171  | `#54BBAB` | 326C    | Secondary; the "Oceano CAIXA" gradient runs Azul→Turquesa |
| Azul (apoio, mid)     | 0, 117, 191   | `#0075BF` | —       | Lighter blue for ramps/hover                              |
| Azul (apoio, deep)    | 0, 86, 157    | `#00569D` | —       | Darker blue for active/pressed                            |
| Azul (apoio, deepest) | 0, 75, 139    | `#004B8B` | —       | Deepest blue                                              |
| **Gelo / Cinza**      | 208, 224, 227 | `#D0E0E3` | 552C    | Cool neutral; "Gelo CAIXA" runs white→this                |
| White                 | 255, 255, 255 | `#FFFFFF` | —       | Surface base                                              |

## Paleta flexível — "otimismo e brasilidade" (gradient accents, drawn from turquesa)

| Name      | RGB (pure)    | Hex       | Pantone |
|-----------|---------------|-----------|---------|
| Céu       | 0, 181, 229   | `#00B5E5` | 306C    |
| Uva       | 178, 111, 155 | `#B26F9B` | 258C    |
| Limão     | 175, 202, 17  | `#AFCA11` | 382C    |
| Tangerina | 249, 176, 0   | `#F9B000` | 1235C   |
| Goiaba    | 239, 118, 94  | `#EF765E` | 1645C   |

## Paleta jogos — **exclusive to the lottery games; don’t reuse as UI colors**

The manual is explicit: *"A paleta jogos é de uso exclusivo dos jogos da CAIXA Loterias e não deve usada em outras expressões CAIXA."* We treat these as **hue inspiration only** — the *spirit* of the feedback colors, never literal status tokens. Captured for reference: Vermelho Loteca `#ED1C24`, Verde Mega-Sena `#00AB67`, Amarelo Timemania `#FFDD00`, Roxo Lotofácil `#803594`, Verde-limão `#A6CE39`, Azul Quina `#005DA4`.

## What we deliberately do differently (inspired, not cloned)

Lotero is an internal management tool for lotérica *operators and managers*, not a consumer-facing CAIXA product, and it is **not an official CAIXA property**. So:

1. **Anchor on the fixed palette, not the games palette.** Azul CAIXA `#005CA9` + Laranja CAIXA `#F39200` + a turquesa secondary give instant lotérica familiarity. The games palette stays out of the UI entirely (it's CAIXA-trademark-loaded and visually loud).
2. **We build full tonal ramps; the manual ships spot colors.** M2 derives 50→900 ramps from these anchors (the manual only gives a handful of stops). Our ramps are ours.
3. **Feedback colors are defined for contrast, not copied.** Success/danger/warning take their *hue family* from the brasilidade spirit (green/red/amber) but are tuned to pass our WCAG AA gate against our surfaces — not lifted from `paleta jogos`.
4. **Calm, dense, light-first.** This is a finance tool read all day; we pull the *restraint* of the institutional palette (lots of white/gelo neutral, blue structure, orange used sparingly as accent), not the festive gradients.
5. **No trevo, no wordmark, no padronagem.** The clover symbol, the "loterias CAIXA" lockup, and the clover-tile patterns are CAIXA brand assets — Lotero gets its own mark. We borrow the *color feeling*, nothing trademarked.

## Suggested mapping into the M2 token layers (for M2 to decide, not binding)

- `primary` ← Azul CAIXA `#005CA9` (ramp: `#0075BF` lighter / `#00569D` / `#004B8B` darker)
- `accent` ← Laranja CAIXA `#F39200`
- `secondary` ← Turquesa `#54BBAB`
- neutral cool ramp seeded from Gelo/Cinza `#D0E0E3` → white
- `success` ~green family, `danger` ~red family, `warning` ~amber/tangerina `#F9B000`, `info` ~Céu `#00B5E5` — all re-tuned for AA, not copied

## Typography note

The Loterias CAIXA wordmark uses **Futura Std** (the "CAIXA" endorsement is Futura Std bold, lowercase) — a brand-display face, not a UI face. For Lotero screens, M2 should pick a screen-grade family (system-first stack recommended for v1) and reserve any Futura-like geometric face for headings only if licensing allows. Body/table typography is an M2 decision, not a brand-manual mandate.
