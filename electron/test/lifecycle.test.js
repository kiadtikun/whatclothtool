const { test } = require('node:test');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { WorkerClient } = require('../worker-client');

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
