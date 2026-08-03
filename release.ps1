param (
    [string]$Token = $env:GITHUB_TOKEN,
    [string]$Version = "",
    [switch]$Increment
)

$ErrorActionPreference = "Stop"

try {
    # 1. Determinar y sincronizar versión
    $csprojPath = "ImperialShield\ImperialShield.csproj"
    $issPath = "Installer\installer.iss"

    if (-not (Test-Path $csprojPath)) {
        throw "No se encontró el archivo $csprojPath"
    }

    $csprojContent = Get-Content $csprojPath -Raw
    if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)</Version>') {
        $currentVersion = $Matches[1]
    } else {
        throw "No se pudo obtener la versión de $csprojPath"
    }

    # Preguntar la versión si no se especificó por parámetro
    if ([string]::IsNullOrWhiteSpace($Version) -and -not $Increment) {
        $inputVer = Read-Host "Ingresa la versión del Release (ej. 1.0.9) [Presiona Enter para usar la actual '$currentVersion']"
        if (-not [string]::IsNullOrWhiteSpace($inputVer)) {
            $Version = $inputVer.Trim().TrimStart('v', 'V')
        }
    }

    if ($Increment) {
        powershell -ExecutionPolicy Bypass -File "increment_version.ps1"
        $csprojContent = Get-Content $csprojPath -Raw
        if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)</Version>') {
            $currentVersion = $Matches[1]
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($Version)) {
        $currentVersion = $Version.TrimStart('v', 'V')
        $newAssemblyVersion = "$currentVersion.0"
        $csprojContent = $csprojContent -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$currentVersion</Version>"
        $csprojContent = $csprojContent -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newAssemblyVersion</AssemblyVersion>"
        $csprojContent = $csprojContent -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newAssemblyVersion</FileVersion>"
        $csprojContent = $csprojContent -replace '<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>', "<InformationalVersion>$currentVersion</InformationalVersion>"
        Set-Content $csprojPath $csprojContent -NoNewline
    }

    # Preguntar Token de GitHub si no está definido (enmascarado con ***)
    if ([string]::IsNullOrWhiteSpace($Token)) {
        $secureToken = Read-Host "Ingresa tu GitHub Token (PAT) [Presiona Enter para solo compilar localmente]" -AsSecureString
        if ($secureToken.Length -gt 0) {
            $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
            $Token = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr).Trim()
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }

    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host " Publicando Imperial Shield v$currentVersion" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan

    # 2. Sincronizar versión en installer.iss (Inno Setup)
    $issContent = Get-Content $issPath -Raw
    $issContent = $issContent -replace 'AppVersion=\d+\.\d+\.\d+', "AppVersion=$currentVersion"
    $issContent = $issContent -replace 'OutputBaseFilename=ImperialShield-\d+\.\d+\.\d+-Setup', "OutputBaseFilename=ImperialShield-$currentVersion-Setup"
    Set-Content $issPath $issContent -NoNewline
    Write-Host "[OK] installer.iss actualizado a versión v$currentVersion" -ForegroundColor Green

    # 3. Compilar ejecutable con dotnet publish
    Write-Host "`n[1/4] Compilando ejecutable .NET autocontenido..." -ForegroundColor Yellow
    dotnet publish ImperialShield\ImperialShield.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) { throw "Error en la compilación del proyecto .NET." }

    # 4. Compilar instalador con Inno Setup (ISCC)
    Write-Host "`n[2/4] Generando instalador con Inno Setup..." -ForegroundColor Yellow
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $isccPath)) {
        $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
    }
    if (-not (Test-Path $isccPath)) {
        throw "No se encontró ISCC.exe. Asegúrate de tener Inno Setup 6 instalado."
    }

    & $isccPath $issPath
    if ($LASTEXITCODE -ne 0) { throw "Error al compilar el instalador con Inno Setup." }

    $exeInstallerPath = "Installer\InstallerOutput\ImperialShield-$currentVersion-Setup.exe"
    $zipInstallerPath = "Installer\InstallerOutput\ImperialShield-$currentVersion-Setup.zip"

    if (-not (Test-Path $exeInstallerPath)) {
        throw "No se encontró el ejecutable instalador en $exeInstallerPath"
    }

    # 5. Comprimir instalador a formato .zip
    Write-Host "`n[3/4] Comprimiendo el instalador a archivo .zip..." -ForegroundColor Yellow
    if (Test-Path $zipInstallerPath) { Remove-Item $zipInstallerPath -Force }
    Compress-Archive -Path $exeInstallerPath -DestinationPath $zipInstallerPath
    Write-Host "[OK] Paquete ZIP creado: $zipInstallerPath" -ForegroundColor Green

    # 6. Subir Release a GitHub si hay token disponible
    if ([string]::IsNullOrWhiteSpace($Token)) {
        Write-Host "`n=========================================" -ForegroundColor Yellow
        Write-Host " Compilación y empaquetado completados con éxito." -ForegroundColor Green
        Write-Host " El archivo ZIP generado se encuentra en:" -ForegroundColor Cyan
        Write-Host " $zipInstallerPath" -ForegroundColor White
        Write-Host " Para subirlo automáticamente a GitHub, ejecuta de nuevo ingresando tu Token." -ForegroundColor Yellow
        Write-Host "=========================================" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "`n[4/4] Subiendo Release v$currentVersion a GitHub..." -ForegroundColor Yellow

    # Subir cambios en git y crear etiqueta
    git add .
    git commit -m "Release v$currentVersion" --allow-empty
    git tag -a "v$currentVersion" -m "Release v$currentVersion" -f
    git push origin main
    git push origin "v$currentVersion" -f

    # API de GitHub para obtener o crear Release
    $repo = "jonathanhecl/imperial-shield"
    $headers = @{
        "Authorization" = "Bearer $Token"
        "Accept"        = "application/vnd.github+json"
        "User-Agent"    = "ImperialShield-DeployScript"
    }

    $releaseBody = @{
        tag_name         = "v$currentVersion"
        target_commitish = "main"
        name             = "v$currentVersion"
        body             = "Release de Imperial Shield v$currentVersion`n`n- Registro de cambios y mejoras de seguridad."
        draft            = $false
        prerelease       = $false
    } | ConvertTo-Json

    $releaseResponse = $null
    try {
        $createReleaseUrl = "https://api.github.com/repos/$repo/releases"
        $releaseResponse = Invoke-RestMethod -Uri $createReleaseUrl -Method Post -Headers $headers -Body $releaseBody -ContentType "application/json"
        Write-Host "[OK] Release v$currentVersion creada en GitHub." -ForegroundColor Green
    } catch {
        Write-Host "Buscando release v$currentVersion existente..." -ForegroundColor Yellow
        $getReleaseUrl = "https://api.github.com/repos/$repo/releases/tags/v$currentVersion"
        $releaseResponse = Invoke-RestMethod -Uri $getReleaseUrl -Method Get -Headers $headers
    }

    $uploadUrlRaw = $releaseResponse.upload_url
    $cleanUploadUrl = $uploadUrlRaw.Split('{')[0]
    $assetName = "ImperialShield-$currentVersion-Setup.zip"
    $uploadUrl = "${cleanUploadUrl}?name=$assetName"

    Write-Host "Subiendo asset binario $assetName a GitHub..." -ForegroundColor Yellow

    $resolvedZipPath = (Resolve-Path $zipInstallerPath).Path

    $curlOutput = & curl.exe -s -S -X POST `
        -H "Authorization: Bearer $Token" `
        -H "Accept: application/vnd.github+json" `
        -H "Content-Type: application/zip" `
        --data-binary "@$resolvedZipPath" `
        "$uploadUrl"

    $assetResult = $curlOutput | ConvertFrom-Json

    if ($assetResult.browser_download_url) {
        Write-Host "`n=========================================" -ForegroundColor Green
        Write-Host " 🎉 RELEASE PUBLICADA EXITOSAMENTE EN GITHUB!" -ForegroundColor Green
        Write-Host " URL de la Release: $($releaseResponse.html_url)" -ForegroundColor Cyan
        Write-Host " Asset descargable: $($assetResult.browser_download_url)" -ForegroundColor White
        Write-Host "=========================================" -ForegroundColor Green
    } else {
        Write-Host "Respuesta de subida de asset: $curlOutput" -ForegroundColor Yellow
        Write-Host "`n=========================================" -ForegroundColor Green
        Write-Host " Release v$currentVersion creada/actualizada en GitHub." -ForegroundColor Green
        Write-Host " URL: $($releaseResponse.html_url)" -ForegroundColor Cyan
        Write-Host " Nota: Si el asset ya existía previamente en la release, elimínalo o vuelve a correr para resubir." -ForegroundColor Yellow
        Write-Host "=========================================" -ForegroundColor Green
    }

} catch {
    Write-Host "`n=========================================" -ForegroundColor Red
    Write-Host " ❌ ERROR EN EL PROCESO DE RELEASE" -ForegroundColor Red
    Write-Host " Details: $_" -ForegroundColor Red
    Write-Host " La subida a GitHub y la creación del Tag han sido CANCELADAS." -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit 1
}
