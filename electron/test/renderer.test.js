const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createBrowserModel } = require('../renderer');

test('keeps drawable order and shows textures for selected drawable', () => {
  const browser = createBrowserModel({ projectName: 'Tale', addons: [{ name: 'Addon 1', drawables: [
    { id: 'one', name: 'jbib_102_u', number: 102, textures: [{ originalFileName: 'first.ytd' }] },
    { id: 'two', name: 'jbib_075_u', number: 75, textures: [{ originalFileName: 'second.ytd' }] }
  ] }] });
  assert.deepEqual(browser.drawables.map(item => item.name), ['jbib_102_u', 'jbib_075_u']);
  browser.selectDrawable('two');
  assert.equal(browser.selectedDrawable.textures[0].originalFileName, 'second.ytd');
});
