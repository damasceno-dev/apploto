// Compiled by typecheck, never imported into the app. Missing/stale route generation must fail.
import type { Href } from 'expo-router';

const home: Href = '/';
void home;

// @ts-expect-error This route does not exist: generated types must reject it on a clean install.
const nonexistent: Href = '/__loto_route_that_must_not_exist__';
void nonexistent;
