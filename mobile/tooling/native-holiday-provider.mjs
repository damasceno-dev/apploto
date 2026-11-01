// Deterministic loopback stand-in for the two external holiday sources, used only by Mobile's
// owned disposable API (through its existing ExternalHolidaySources base-URL configuration).
// Nager.Date answers a fixed set of national rows; BrasilAPI is a permanent outage, so a
// single-source BrasilAPI request exercises the verbatim 502 while Composite still succeeds.
import { createServer } from 'node:http';
export const nagerRows = (year) =>
  [
    ['04-21', 'Tiradentes', 'Tiradentes'],
    ['09-07', 'Independência do Brasil', 'Independence Day'],
    ['11-15', 'Proclamação da República', 'Republic Proclamation Day'],
  ].map(([day, localName, name]) => ({
    date: `${year}-${day}`,
    localName,
    name,
    countryCode: 'BR',
    fixed: true,
    global: true,
    counties: null,
    launchYear: null,
    types: ['Public'],
  }));
export function providerHandler(request, response) {
  const nager = /^\/nager\/api\/v3\/PublicHolidays\/(\d{4})\/BR$/.exec(request.url ?? '');
  if (request.method === 'GET' && nager) {
    response.writeHead(200, { 'content-type': 'application/json' });
    response.end(JSON.stringify(nagerRows(nager[1])));
    return;
  }
  if (
    request.method === 'GET' &&
    /^\/brasilapi\/api\/feriados\/v1\/\d{4}$/.test(request.url ?? '')
  ) {
    response.writeHead(503, { 'content-type': 'application/json' });
    response.end(JSON.stringify({ message: 'synthetic outage' }));
    return;
  }
  response.writeHead(404);
  response.end();
}
if (import.meta.url === `file://${process.argv[1]}`) {
  const port = Number(process.argv[2]);
  if (!Number.isInteger(port) || port <= 0) throw new Error('Use <loopback-port>.');
  createServer(providerHandler).listen(port, '127.0.0.1');
}
