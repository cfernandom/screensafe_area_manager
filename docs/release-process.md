# Release Process

This document describes how to create a new release of ScreenSafe Area Manager and publish it to GitHub.

## Prerequisites

- [gh CLI](https://cli.github.com/) installed and authenticated (`gh auth status`)
- .NET SDK 8.0+ installed (`dotnet --version`)
- Write access to the GitHub repository
- Working tree clean (no uncommitted changes)

## Step-by-Step

### 1. Prepare the working tree

Ensure the working tree is clean before building — untracked files don't affect the EXE but can make the release "dirty":

```powershell
git status
```

If there are uncommitted changes, stash them:

```powershell
git stash push --include-untracked --message "pre-release: stash before <version>"
```

### 2. Build the Release

Publish the console project in Release mode:

```powershell
dotnet publish src\ScreenSafe.Console\ScreenSafe.Console.csproj `
  -f net48 -c Release -o .\release-<version>
```

This produces:
- `ScreenSafe.Console.exe` — main executable
- `*.dll` — project and dependency assemblies
- `appsettings.json` — configuration file
- `*.pdb` — debug symbols (useful for crash diagnostics)

### 3. Create the Distribution Archive

```powershell
Compress-Archive -Path ".\release-<version>\*" -DestinationPath ".\ScreenSafe-<version>-win81.zip"
```

### 4. Publish the GitHub Release

```powershell
gh release create <version> `
  ".\ScreenSafe-<version>-win81.zip" `
  --title "<version>" `
  --notes "<release-notes>"
```

**Flags reference:**

| Flag | Purpose |
|---|---|
| `--title` | Display name for the release |
| `--notes` | Markdown release notes (use `--notes-file` for longer notes) |
| `--draft` | Create as draft (invisible to non-admins); remove for public release |
| `--prerelease` | Mark as pre-release |

### 5. Clean Up Temporary Files

```powershell
Remove-Item -LiteralPath ".\release-<version>" -Recurse -Force
Remove-Item -LiteralPath ".\ScreenSafe-<version>-win81.zip" -Force
```

### 6. Restore Stashed Changes (if any)

```powershell
git stash pop
```

## Release Asset URL Format

Once published, the ZIP is downloadable at:

```
https://github.com/<owner>/<repo>/releases/download/<version>/ScreenSafe-<version>-win81.zip
```

Example for v0.1.0:

```
https://github.com/cfernandom/screensafe_area_manager/releases/download/v0.1.0/ScreenSafe-v0.1.0-win81.zip
```

This URL works **without authentication** — ideal for downloading directly on a test VM.

## Release Notes Template

```markdown
## ScreenSafe Area Manager <version>

### Changes
- <!-- list changes since last release -->

### Assets
- `ScreenSafe-<version>-win81.zip` — executable + dependencies for Windows 8.1+

### Requirements
- Windows 8.1 or later
- .NET Framework 4.8 (included in Windows 8.1+)
```

## Versioning

This project follows [Semantic Versioning](https://semver.org/):

- **Major**: breaking changes (e.g., config format, CLI interface)
- **Minor**: new features, backward-compatible
- **Patch**: bug fixes, internal improvements

Tags use the `v` prefix: `v0.1.0`, `v1.0.0`, etc.
