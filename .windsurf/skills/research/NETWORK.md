---
name: research-network
description: Research and document network connection mapping logic (NetStat)
---

### Check Active TCP Connections
To see active connections with PID (standard netstat):
```bash
netstat -ano | findstr ESTABLISHED
```

### Reverse Shell Detection Logic
Imperial Shield flags connections as CRITICAL if:
1. Process is `powershell.exe`, `cmd.exe`, or `wscript.exe`.
2. State is `ESTABLISHED`.
3. Destination port is in common malware list (4444, 6666, 31337).

### Test Command for Mapping
To verify how Windows maps PIDs to names:
```powershell
powershell -Command "Get-NetTCPConnection -State Established | Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, OwningProcess"
```
