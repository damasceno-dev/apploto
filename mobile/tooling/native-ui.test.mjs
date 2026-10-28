import { test } from 'node:test';
import assert from 'node:assert/strict';
import { nativeNodes } from './native-ui.mjs';
test('reads native JSON input from a single-quoted attribute alongside double-quoted IDs', () => {
  const [node] = nativeNodes(
    `<node text='{"value":12.34}' resource-id="session-financial-payload" enabled="true"/>`,
  );
  assert.equal(node['resource-id'], 'session-financial-payload');
  assert.deepEqual(JSON.parse(node.text), { value: 12.34 });
});
test('decodes native XML escapes without normalizing the measured Unicode', () => {
  const [node] = nativeNodes(
    `<node text="−R$&#160;1,00 &amp; &quot;teste&quot;" resource-id='reading'/>`,
  );
  assert.equal(node.text, '−R$\u00a01,00 & "teste"');
  assert.equal(node['resource-id'], 'reading');
});
