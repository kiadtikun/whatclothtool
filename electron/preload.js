const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('mjx', Object.freeze({
  openProject: () => ipcRenderer.invoke('project:open'),
  getVersion: () => process.versions.electron
}));
