# Script para incrementar la version patch en el .csproj
$csprojPath = "ImperialShield\ImperialShield.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Host "No se encontro el archivo .csproj" -ForegroundColor Red
    exit 1
}

$content = Get-Content $csprojPath -Raw

# Extraer version actual
if ($content -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3] + 1
    
    $newVersion = "$major.$minor.$patch"
    $newAssemblyVersion = "$major.$minor.$patch.0"
    
    Write-Host "Version anterior: $($Matches[0])" -ForegroundColor Yellow
    Write-Host "Nueva version: $newVersion" -ForegroundColor Green
    
    # Reemplazar versiones
    $content = $content -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
    $content = $content -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newAssemblyVersion</AssemblyVersion>"
    $content = $content -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newAssemblyVersion</FileVersion>"
    $content = $content -replace '<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>', "<InformationalVersion>$newVersion</InformationalVersion>"
    
    Set-Content $csprojPath $content -NoNewline
    Write-Host "Version actualizada a $newVersion" -ForegroundColor Green
} else {
    Write-Host "No se pudo encontrar la version en el .csproj" -ForegroundColor Red
}
