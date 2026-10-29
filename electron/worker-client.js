const { randomUUID } = require('node:crypto');

class WorkerClient {
  constructor(child) {
    this.child = child;
    this.pending = new Map();
    this.buffer = '';
    this.closed = false;
    child.stdout.on('data', chunk => this.onData(chunk));
    child.on('exit', () => this.failAll(new Error('Worker closed.')));
    child.on('error', error => this.failAll(error));
  }

  openProject(savePath) {
    if (this.closed) return Promise.reject(new Error('Worker closed.'));
    const id = randomUUID();
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.child.stdin.write(JSON.stringify({ version: 1, id, method: 'openProject', savePath }) + '\n');
    });
  }

  onData(chunk) {
    this.buffer += chunk.toString('utf8');
    let end;
    while ((end = this.buffer.indexOf('\n')) >= 0) {
      const line = this.buffer.slice(0, end).trim();
      this.buffer = this.buffer.slice(end + 1);
      if (!line) continue;
      let response;
      try { response = JSON.parse(line); }
      catch { this.failAll(new Error('Worker sent invalid JSON.')); return; }
      const pending = this.pending.get(response.id);
      if (!pending) continue;
      this.pending.delete(response.id);
      if (response.version !== 1) pending.reject(new Error('Worker protocol version mismatch.'));
      else if (response.error) pending.reject(new Error(response.error.message || response.error.code));
      else pending.resolve(response.result);
    }
  }

  failAll(error) {
    this.closed = true;
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }

  close() {
    if (this.closed) return;
    this.failAll(new Error('Worker closed.'));
    this.child.kill();
  }
}

module.exports = { WorkerClient };
