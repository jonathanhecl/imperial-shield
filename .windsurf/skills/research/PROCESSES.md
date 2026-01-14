---
name: research-processes
description: Research and document process signature verification and suspicious paths
---

### Verify File Signature
To check if a process executable is signed (requires admin for some paths):
```powershell
powershell -Command "Get-AuthenticodeSignature -FilePath 'C:\Path\To\Process.exe'"
```

### Suspicious Path Logic
Imperial Shield marks as **CRITICAL** if a system process runs from:
- `%TEMP%`
- `%APPDATA%`
- `C:\Users\...\Downloads`

### Expected Signature Status
- **Valid**: Signed by trusted CA.
- **NotSigned**: No signature found.
- **HashMismatch**: File has been modified after signing.
