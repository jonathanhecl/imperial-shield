---
name: build-release
description: Build the release version of the project
---

To compile the project in release mode, use:
```bash
dotnet build --configuration Release
```

If the compilation fails because the file is in use, terminate the process (requires admin):
```bash
taskkill -f -im ImperialShield.exe
```
