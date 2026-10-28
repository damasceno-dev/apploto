// Owned localhost proxy/input-data server. Authentication is performed by the real .NET API.
import http from 'node:http';
import { Buffer } from 'node:buffer';
import { readFile, appendFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const [statePath, fixturePath, portText, phase, evidencePath] = process.argv.slice(2);
const port = Number(portText);
if (
  !statePath ||
  !fixturePath ||
  !phase ||
  !evidencePath ||
  !Number.isInteger(port) ||
  port < 1024 ||
  port > 65535
)
  throw new Error(
    'Usage: node tooling/native-fixture-server.mjs <owned-state> <fixture-json> <port> <phase> <outside-checkout-http.jsonl>',
  );
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(evidencePath) || path.resolve(evidencePath).startsWith(root))
  throw new Error('Keep native evidence outside the checkout.');
const state = JSON.parse(await readFile(statePath, 'utf8'));
const fixture = JSON.parse(await readFile(fixturePath, 'utf8'));
const fixtureUser = state.users?.[process.env.LOTO_NATIVE_FIXTURE_USER ?? 'ana'];
const fixtureBranch = state.branches?.[process.env.LOTO_NATIVE_FIXTURE_BRANCH ?? 'alfa'];
const api = new URL(state.apiUrl);
if (
  api.protocol !== 'http:' ||
  !['127.0.0.1', 'localhost'].includes(api.hostname) ||
  !state.owned ||
  !fixtureBranch ||
  !fixtureUser?.email?.endsWith('@example.invalid')
)
  throw new Error('Use only an owned disposable loopback stack with fake identities.');
await mkdir(path.dirname(evidencePath), { recursive: true });
const server = http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/__native-fixture') {
      res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      res.end(
        JSON.stringify({
          login: fixtureUser,
          branchId: fixtureBranch,
          notes: `${phase.startsWith('ios') ? 'ios' : 'android'} retained input final`,
          payload: fixture.body,
        }),
      );
      return;
    }
    const chunks = [];
    let size = 0;
    for await (const chunk of req) {
      size += chunk.length;
      if (size > 1048576) {
        res.writeHead(413);
        res.end();
        return;
      }
      chunks.push(chunk);
    }
    const body = Buffer.concat(chunks);
    const headers = new Headers();
    for (const [key, value] of Object.entries(req.headers))
      if (value && !['host', 'connection', 'content-length'].includes(key))
        headers.set(key, Array.isArray(value) ? value.join(',') : value);
    const response = await fetch(state.apiUrl + req.url, {
      method: req.method,
      headers,
      body: body.length ? body : undefined,
    });
    const data = Buffer.from(await response.arrayBuffer());
    const route = req.url.split('?')[0];
    const entry = {
      phase,
      method: req.method,
      path: route,
      status: response.status,
      date: response.headers.get('date'),
      etag: response.headers.get('etag'),
    };
    if (response.status >= 400 && ['/branch/current', '/branch/session'].includes(route)) {
      try {
        const rejected = JSON.parse(data.toString('utf8'));
        if (
          Array.isArray(rejected.errorMessages) &&
          rejected.errorMessages.every((message) => typeof message === 'string')
        )
          entry.errorMessages = rejected.errorMessages;
      } catch {
        /* A non-contract error body has no message oracle. */
      }
    }
    if (req.method === 'POST' && route === '/transaction')
      Object.assign(entry, {
        key: headers.get('idempotency-key'),
        bodySha256: createHash('sha256').update(body).digest('hex'),
      });
    // Only whitelisted branch error messages supplement metadata; credentials, auth bodies and query capabilities are never logged.
    await appendFile(evidencePath, JSON.stringify(entry) + '\n', { mode: 0o600 });
    res.writeHead(
      response.status,
      Object.fromEntries(
        [...response.headers].filter(
          ([key]) =>
            !['transfer-encoding', 'content-length', 'content-encoding', 'connection'].includes(
              key,
            ),
        ),
      ),
    );
    res.end(data);
  } catch {
    res.writeHead(502);
    res.end();
  }
});
server.listen(port, '127.0.0.1', () =>
  console.log(`Owned native fixture server listening on IPv4 localhost:${port}.`),
);
