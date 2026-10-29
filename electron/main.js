const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { WorkerClient } = require('./worker-client');

let window;
let worker;

function startWorker() {
  const dll = path.resolve(__dirname, '..', 'mjxClothTool.Worker', 'bin', 'Debug', 'net10.0', 'mjxClothTool.Worker.dll');
  if (!fs.existsSync(dll)) throw new Error('Build mjxClothTool.Worker before starting Electron.');
  return new WorkerClient(spawn('dotnet', [dll], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true }));
}

function createWindow() {
  window = new BrowserWindow({
    width: 1380, height: 900, minWidth: 900, minHeight: 620,
    title: 'mjxClothTool', backgroundColor: '#f4f6fa',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, nodeIntegration: false, sandbox: true
    }
  });
  window.loadFile(path.join(__dirname, 'index.html'));
}

app.whenReady().then(() => {
  worker = startWorker();
  ipcMain.handle('project:open', async () => {
    const selected = await dialog.showOpenDialog(window, {
      title: 'Open mjxClothTool project',
      properties: ['openFile'],
      filters: [{ name: 'mjxClothTool saves', extensions: ['json'] }]
    });
    if (selected.canceled || !selected.filePaths[0]) return null;
    const savePath = selected.filePaths[0];
    if (!['autosave.json', 'autosave.external.json'].includes(path.basename(savePath).toLowerCase()))
      throw new Error('Select autosave.json or autosave.external.json.');
    return worker.openProject(savePath);
  });
  createWindow();
}).catch(error => {
  dialog.showErrorBox('mjxClothTool', error.message);
  app.quit();
});

app.on('before-quit', () => worker?.close());
app.on('window-all-closed', () => app.quit());
app.on('web-contents-created', (_, contents) => {
  contents.setWindowOpenHandler(() => ({ action: 'deny' }));
  contents.on('will-navigate', event => event.preventDefault());
});
