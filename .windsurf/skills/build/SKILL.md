---
name: build-debug
description: Build the debug version of the project
---

To compile the project, we use debug with:
```bash
dotnet build --configuration Debug
```

If you cannot compile because the file is in use, it is because the user forgot to close it. To close it, do the following (requires admin permissions):
```bash
taskkill -f -im ImperialShield.exe
```
