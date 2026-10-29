# mjxClothTool Electron preview

This is the first, read-only phase of the WPF-to-Electron migration. It opens existing `autosave.json` and `autosave.external.json` projects and displays add-ons, drawables, and textures. Use the WPF app for creating, editing, importing, optimizing, previewing, saving, and building until the Electron app reaches feature parity.

## Run on Windows

From the repository root:

```powershell
dotnet build mjxClothTool.Worker\mjxClothTool.Worker.csproj -p:NuGetAudit=false
npm ci --prefix electron
node electron\node_modules\electron\install.js
npm start --prefix electron
```

The Electron binary download is a development setup step. Once installed, opening a project uses only a local .NET worker over stdin/stdout. The app does not start a web server or request updates, telemetry, an account, or GitHub access. The renderer has no direct filesystem or Node access.

The open dialog accepts only the two save filenames. Opening a project reads the JSON and does not write the save or asset files. `OriginalFileName` is shown for textures when it is stored in the project.

## Tests

```powershell
dotnet test mjxClothTool.Worker.Tests\mjxClothTool.Worker.Tests.csproj -p:NuGetAudit=false
npm test --prefix electron
```

Close the Electron preview before rebuilding the worker, because Windows can lock its DLL while the app is open.
