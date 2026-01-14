---
name: publish-single-file
description: Publish the project as a single, self-contained executable for Windows x64
---

To create a single-file executable for distribution:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
