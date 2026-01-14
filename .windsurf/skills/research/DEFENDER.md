---
name: research-defender
description: Research and document Windows Defender exclusion logic and WMI responses
---

### Check Defender Exclusions
To see what Defender is currently excluding (requires admin):
```powershell
powershell -Command "Get-MpPreference | Select-Object -ExpandProperty ExclusionPath"
```

### Expected Output Format
The command returns a list of strings (paths). In C#, this is parsed from the WMI `MSFT_MpPreference` class or via PowerShell execution.

### Logic to Verify
- **Status 0**: Defender is Active.
- **Status 4**: Defender is Disabled or in Passive Mode.
- **Exclusions**: Any path listed here is not being scanned. Imperial Shield monitors changes in these paths.
