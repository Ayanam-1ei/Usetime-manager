const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('usetime', {
  getSummary: (period, dateStr) => ipcRenderer.invoke('stats:summary', period, dateStr),
  getChartData: (period, dateStr) => ipcRenderer.invoke('stats:chart', period, dateStr),
  getAppUsage: (period, dateStr) => ipcRenderer.invoke('stats:apps', period, dateStr),
  getIcon: (exePath) => ipcRenderer.invoke('app:getIcon', exePath),
  showWindow: () => ipcRenderer.invoke('app:show'),
  hideWindow: () => ipcRenderer.invoke('app:hide'),
  quitApp: () => ipcRenderer.invoke('app:quit'),
  getTrackerStatus: () => ipcRenderer.invoke('tracker:status'),
  getAutostart: () => ipcRenderer.invoke('autostart:get'),
  setAutostart: (enabled) => ipcRenderer.invoke('autostart:set', enabled),
});
