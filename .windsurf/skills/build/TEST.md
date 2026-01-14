---
name: run-tests
description: Run the unit tests for detection logic and system services
---

To run the unit tests and verify the detection logic:
```bash
dotnet test ImperialShield.Tests/ImperialShield.Tests.csproj
```

To run tests with a detailed summary:
```bash
dotnet test ImperialShield.Tests/ImperialShield.Tests.csproj --logger "console;verbosity=detailed"
```
