import { expect, type BrowserContext, type Page } from '@playwright/test';
import type { SessionSnapshot } from '../src/lib/bff-protocol';
import type { StackState, StackUser } from './stack';

export async function snapshot(page: Page): Promise<SessionSnapshot> {
  return JSON.parse((await page.getByTestId('session').textContent()) ?? 'null') as SessionSnapshot;
}

/**
 * Signs in through the identity-level session fixture and returns the login's own result (with a
 * single branch, the branch it opened; `landing` is where this tab would go).
 */
export async function signIn(
  page: Page,
  state: StackState,
  user: StackUser,
): Promise<SessionSnapshot & { readonly landing: string }> {
  await page.goto(`${state.webUrl}/e2e/session`);
  await page.getByLabel('E-mail').fill(user.email);
  await page.getByLabel('Senha').fill(user.password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect
    .poll(async () => (await outcome(page))?.snapshot?.user?.email ?? null)
    .toBe(user.email);
  await expect.poll(async () => (await snapshot(page)).user?.email ?? null).toBe(user.email);
  const result = (await outcome(page))?.snapshot as SessionSnapshot;
  return { ...result, landing: (await page.getByTestId('landing').textContent()) ?? '' };
}

/** The command's own result in the session fixture (`resume` is delivered once, with it). */
export async function outcome(page: Page): Promise<{ snapshot: SessionSnapshot } | null> {
  return JSON.parse((await page.getByTestId('outcome').textContent()) ?? 'null') as {
    snapshot: SessionSnapshot;
  } | null;
}

/**
 * Opens a branch from the identity-level session fixture (as the M4 picker will). It changes no
 * other tab; the caller navigates this tab to `landing` (or `branchUrl`).
 */
export async function openBranch(
  page: Page,
  name: string,
): Promise<SessionSnapshot & { readonly landing: string }> {
  await page.getByRole('button', { name: `Abrir ${name}` }).click();
  await expect.poll(async () => (await outcome(page))?.snapshot?.branch?.name ?? null).toBe(name);
  const opened = (await outcome(page))?.snapshot as SessionSnapshot;
  return { ...opened, landing: (await page.getByTestId('landing').textContent()) ?? '' };
}

/** A branch-scoped address in the `/f/{branchId}` namespace (D-M2-1). */
export function branchUrl(state: StackState, branchId: string, rest: string): string {
  return `${state.webUrl}/f/${branchId}${rest}`;
}

/** The branch session cookie of one branch (the `__Host-loto-b-{branchId}` slot), decoded. */
export function branchSlot(
  cookies: Record<string, { readonly value: string }>,
  branchId: string,
): { readonly token: string; readonly gen: string; readonly sid: string } | null {
  const raw = cookies[`__Host-loto-b-${branchId}`]?.value;
  return raw ? (JSON.parse(Buffer.from(raw, 'base64url').toString('utf8')) as never) : null;
}

/** Writes a branch slot back with one field changed (test harness only). */
export async function putBranchSlot(
  context: BrowserContext,
  state: StackState,
  branchId: string,
  change: Record<string, unknown>,
) {
  const cookies = await sessionCookies(context, state);
  const raw = cookies[`__Host-loto-b-${branchId}`]?.value ?? '';
  const slot = JSON.parse(Buffer.from(raw, 'base64url').toString('utf8')) as object;
  const next = Buffer.from(JSON.stringify({ ...slot, ...change })).toString('base64url');
  await putCookie(context, state, `__Host-loto-b-${branchId}`, next);
}

/** Sair, waiting for its result: a command issued meanwhile would target the old scope. */
export async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByTestId('outcome')).toContainText('"serverConfirmed"');
}

/** Session cookies (the non-secret device id and theme preference are not session cookies). */
export async function sessionCookies(context: BrowserContext, state: StackState) {
  const cookies = await context.cookies(state.webUrl);
  return Object.fromEntries(
    cookies
      .filter(
        (cookie) =>
          cookie.name.startsWith('__Host-loto-') &&
          cookie.name !== '__Host-loto-device' &&
          cookie.name !== '__Host-loto-theme',
      )
      // Values as the server reads them (Next percent-encodes on write).
      .map((cookie) => [cookie.name, { ...cookie, value: decodeURIComponent(cookie.value) }]),
  );
}

/**
 * Replaces one session cookie as the browser holds it (test harness only). CDP refuses a `__Host-`
 * cookie for an `http:` URL, so it is set through the `https:` form of the same host: the cookie is
 * still host-only on `localhost` (cookies ignore scheme and port) and replaces the existing one.
 */
export async function putCookie(
  context: BrowserContext,
  state: StackState,
  name: string,
  value: string,
) {
  await context.addCookies([
    {
      name,
      value,
      url: `https://${new URL(state.webUrl).host}/`,
      httpOnly: true,
      secure: true,
      sameSite: 'Lax',
    },
  ]);
}

/** A JWT-shaped value with the given expiry, unsigned by the API (it refuses it). */
export function forgedJwt(expiresAtSeconds: number): string {
  const encode = (value: object) => Buffer.from(JSON.stringify(value)).toString('base64url');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ exp: expiresAtSeconds })}.Zm9yZ2Vk`;
}

export const branchNames = {
  alfa: 'Lotérica Alfa E2E',
  beta: 'Lotérica Beta E2E',
  gama: 'Lotérica Gama E2E',
} as const;
