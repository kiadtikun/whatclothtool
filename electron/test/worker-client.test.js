const { test } = require('node:test');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { WorkerClient } = require('../worker-client');

function childFixture() {
  const child = new EventEmitter();
  child.stdout = new EventEmitter();
  child.stderr = new EventEmitter();
  child.writes = [];
  child.stdin = { write: line => child.writes.push(JSON.parse(line)) };
  child.kill = () => { child.killed = true; };
  return child;
}

test('correlates responses by request id even when replies arrive out of order', async () => {
  const child = childFixture();
  const client = new WorkerClient(child);
  const first = client.openProject('C:\\first\\autosave.json');
  const second = client.openProject('C:\\second\\autosave.json');
  assert.equal(child.writes.length, 2);
  assert.notEqual(child.writes[0].id, child.writes[1].id);
  child.stdout.emit('data', Buffer.from(JSON.stringify({ version: 1, id: child.writes[1].id, result: { projectName: 'Second' } }) + '\n'));
  child.stdout.emit('data', Buffer.from(JSON.stringify({ version: 1, id: child.writes[0].id, result: { projectName: 'First' } }) + '\n'));
  assert.equal((await first).projectName, 'First');
  assert.equal((await second).projectName, 'Second');
});

test('propagates worker error to the matching request', async () => {
  const child = childFixture();
  const client = new WorkerClient(child);
  const request = client.openProject('C:\\missing\\autosave.json');
  child.stdout.emit('data', Buffer.from(JSON.stringify({ version: 1, id: child.writes[0].id, error: { code: 'project_error', message: 'Missing save' } }) + '\n'));
  await assert.rejects(request, /Missing save/);
});

module.exports = { childFixture };
