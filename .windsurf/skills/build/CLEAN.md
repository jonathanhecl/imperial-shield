---
name: clean-project
description: Clean all build artifacts (bin, obj, publish)
---

To clean the project and remove all build folders:
```bash
dotnet clean
rmdir /s /q bin obj publish 2>nul
```
