import { expect, type BrowserContext, type Page } from '@playwright/test';
import type { SessionSnapshot } from '../src/lib/bff-protocol';
import type { StackState, StackUser } from './stack';

export async function snapshot(page: Page): Promise<SessionSnapshot> {
  return JSON.parse((await page.getByTestId('session').textContent()) ?? 'null') as SessionSnapshot;
}

export async function signIn(
  page: Page,
  state: StackState,
  user: StackUser,
): Promise<SessionSnapshot> {
  await page.goto(`${state.webUrl}/e2e/session`);
  await page.getByLabel('E-mail').fill(user.email);
  await page.getByLabel('Senha').fill(user.password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect.poll(async () => (await snapshot(page)).user?.email ?? null).toBe(user.email);
  return snapshot(page);
}

export async function openBranch(page: Page, name: string): Promise<SessionSnapshot> {
  await page.getByRole('button', { name: `Abrir ${name}` }).click();
  await expect.poll(async () => (await snapshot(page)).branch?.name ?? null).toBe(name);
  // The command's own result: `resume` is delivered once, with the transition.
  const outcome = JSON.parse((await page.getByTestId('outcome').textContent()) ?? 'null') as {
    snapshot: SessionSnapshot;
  };
  return outcome.snapshot;
}

/** Sair, waiting for its result: a command issued meanwhile would target the old scope. */
export async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByTestId('outcome')).toContainText('"serverConfirmed"');
}

/** Session cookies (the non-secret `__Host-loto-device` id is not a session cookie). */
export async function sessionCookies(context: BrowserContext, state: StackState) {
  const cookies = await context.cookies(state.webUrl);
  return Object.fromEntries(
    cookies
      .filter(
        (cookie) => cookie.name.startsWith('__Host-loto-') && cookie.name !== '__Host-loto-device',
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
