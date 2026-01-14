---
name: build-debug
description: Build the debug version of the project with cleanup and process handling
---

To compile the project in debug mode, it is recommended to clean first and ensure the process is not running:

1. Terminate the process if it's running (requires admin):
```bash
taskkill -f -im ImperialShield.exe 2>nul
```

2. Clean and compile:
```bash
dotnet clean
dotnet build --configuration Debug
```

Note: If compilation fails due to "file in use", make sure to close any instance of the application or the debugger.
