const { test } = require('node:test');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { WorkerClient } = require('../worker-client');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('closing client terminates worker and rejects pending requests', async () => {
  const child = new EventEmitter();
  child.stdout = new EventEmitter();
  child.stderr = new EventEmitter();
  child.stdin = { write() {} };
  child.kill = () => { child.killed = true; };
  const client = new WorkerClient(child);
  const pending = client.openProject('C:\\fixture\\autosave.json');
  client.close();
  assert.equal(child.killed, true);
  await assert.rejects(pending, /closed/i);
});

test('startup failure shows error and exits Electron', async () => {
  const app = new EventEmitter();
  app.whenReady = () => Promise.reject(new Error('Worker unavailable'));
  let quit = false;
  let shown = '';
  app.quit = () => { quit = true; };
  const dialog = { showErrorBox: (_, message) => { shown = message; } };
  const electron = { app, dialog, BrowserWindow: class {}, ipcMain: { handle() {} } };
  const mainPath = path.resolve(__dirname, '../main.js');
  vm.runInNewContext(readFileSync(mainPath, 'utf8'), {
    require: name => name === 'electron' ? electron : name === './worker-client' ? require('../worker-client') : require(name),
    __dirname: path.dirname(mainPath), console
  }, { filename: mainPath });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(shown, 'Worker unavailable');
  assert.equal(quit, true);
});
