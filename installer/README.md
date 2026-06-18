# Installer & portable build

Two ways to package the **Clarion Template Designer** together with all the
template‑authoring assets in this repo (the `templates`, the `clarion-template`
**skill**, and the `clarion-template-pro` **agent**).

Both are self‑contained — **.NET is bundled in, nothing to pre‑install** on the
target machine.

## 1. Full installer (`ClarionTemplateToolsSetup.exe`)

```powershell
pwsh installer\build-installer.ps1
```

- Publishes the WPF app (self‑contained, `win-x64`) into `installer\payload\app`.
- Runs **Inno Setup** (`ISCC.exe`) on `ClarionTemplateTools.iss`.
- Output: `installer\Output\ClarionTemplateToolsSetup.exe`.

The installer installs the app to *Program Files*, drops a local copy of the
templates/agents/skills beside it, adds Start‑menu (and optional desktop)
shortcuts, and offers two optional tasks:

| Task | What it does |
|------|--------------|
| **Install into Clarion** | copies every `.tpl`/`.png` into `C:\clarion12\accessory\template\win` (only offered if that folder exists) |
| **Install Claude assets** | copies the skill + agent into `%USERPROFILE%\.claude\skills` and `…\agents` |

> Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (`ISCC.exe`).

## 2. Portable single‑file exe (`run\` folder)

```powershell
pwsh installer\build-portable.ps1
```

- Publishes a **single, compressed, self‑contained** `.exe`.
- Copies it to `run\ClarionTemplateDesigner.exe`, with the `templates`,
  `agents`, and `skills` folders alongside so it runs out of the box.

Just double‑click the exe — no install, no runtime, fully portable.

## Notes

- `installer\payload\`, `installer\Output\`, and the repo‑root `run\` folder are
  build artifacts and are **git‑ignored** (the exes are large). Regenerate them
  with the scripts above.
