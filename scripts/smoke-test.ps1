[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString,

    [string] $BaseUrl = 'http://127.0.0.1:18080',

    [string] $BackendApiKey = 'smoke-backend-key'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj'
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 10).ToUpperInvariant()
$residentialId = "SMOKE-RES-$runId"
$heartbeatDeviceId = "SMOKE-DEVICE-$runId"
$heartbeatSecret = "smoke-secret-$runId"
$clockAId = "SMOKE-CLOCK-A-$runId"
$clockBId = "SMOKE-CLOCK-B-$runId"
$clockASn = "SMOKE-SN-A-$runId"
$clockBSn = "SMOKE-SN-B-$runId"
$employee = "SMOKE-EMP-$runId"
$stdoutPath = Join-Path ([IO.Path]::GetTempPath()) "apireloj-smoke-$runId.stdout.log"
$stderrPath = Join-Path ([IO.Path]::GetTempPath()) "apireloj-smoke-$runId.stderr.log"

$savedEnvironment = @{}
$environment = @{
    ASPNETCORE_ENVIRONMENT = 'Production'
    ASPNETCORE_URLS = $BaseUrl
    ConnectionStrings__Default = $ConnectionString
    Security__Backend__ApiKey = $BackendApiKey
    Security__Backend__AllowedIp = '127.0.0.1'
    BackfillPolling__RunOnStartup = 'false'
    JornadaProcessing__WorkerIntervalSeconds = '1'
}

foreach ($name in $environment.Keys) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
}

$client = [Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]$BaseUrl
$client.Timeout = [TimeSpan]::FromSeconds(15)
$process = $null
$smokePassed = $false

function Send-ApiRequest {
    param(
        [Parameter(Mandatory = $true)] [Net.Http.HttpMethod] $Method,
        [Parameter(Mandatory = $true)] [string] $Path,
        [object] $Body,
        [string] $ApiKey
    )

    $request = [Net.Http.HttpRequestMessage]::new($Method, $Path)
    try {
        if ($PSBoundParameters.ContainsKey('ApiKey')) {
            [void] $request.Headers.TryAddWithoutValidation('X-Api-Key', $ApiKey)
        }
        if ($PSBoundParameters.ContainsKey('Body')) {
            $json = $Body | ConvertTo-Json -Depth 12 -Compress
            $request.Content = [Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                'application/json')
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                StatusCode = [int] $response.StatusCode
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)] $Response,
        [Parameter(Mandatory = $true)] [int[]] $Expected,
        [Parameter(Mandatory = $true)] [string] $Description
    )

    if ($Expected -notcontains $Response.StatusCode) {
        throw "$Description devolvio HTTP $($Response.StatusCode). Body: $($Response.Body)"
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)] [bool] $Condition,
        [Parameter(Mandatory = $true)] [string] $Description
    )

    if (-not $Condition) {
        throw $Description
    }
}

try {
    $process = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $project, '-c', 'Release', '--no-build', '--no-restore', '--no-launch-profile') `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        if ($process.HasExited) {
            throw "La API termino antes del smoke test con codigo $($process.ExitCode)."
        }

        try {
            $probe = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) -Path '/Residential'
            if ($probe.StatusCode -eq 401) {
                $ready = $true
                break
            }
        }
        catch {
            # El puerto todavia puede estar iniciando.
        }
        Start-Sleep -Milliseconds 250
    }
    Assert-True $ready 'La API no quedo disponible dentro del tiempo esperado.'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) -Path '/Residential'
    Assert-Status $response 401 'Endpoint backend sin API key'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) -Path '/Residential' -ApiKey 'incorrecta'
    Assert-Status $response 401 'Endpoint backend con API key incorrecta'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) -Path '/Residential' -ApiKey $BackendApiKey
    Assert-Status $response 200 'Endpoint backend autorizado'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Residential' -ApiKey $BackendApiKey -Body @{
        idResidential = $residentialId
        ipActual = '127.0.0.1'
    }
    Assert-Status $response 200 'Creacion de residencial'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Device' -ApiKey $BackendApiKey -Body @{
        _deviceId = $heartbeatDeviceId
        _secretKey = $heartbeatSecret
        _residentialId = $residentialId
    }
    Assert-Status $response 200 'Creacion de dispositivo heartbeat'
    Assert-True ($response.Body.IndexOf($heartbeatSecret, [StringComparison]::Ordinal) -lt 0) 'La respuesta de Device expuso SecretKey.'
    Assert-True ($response.Body.IndexOf('_secretKey', [StringComparison]::OrdinalIgnoreCase) -lt 0) 'La respuesta de Device serializo el campo SecretKey.'

    foreach ($clock in @(
        @{ Id = $clockAId; Sn = $clockASn; Port = 80 },
        @{ Id = $clockBId; Sn = $clockBSn; Port = 81 }
    )) {
        $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Reloj' -ApiKey $BackendApiKey -Body @{
            _idReloj = $clock.Id
            _puerto = $clock.Port
            _residentialId = $residentialId
        }
        Assert-Status $response 200 "Creacion del reloj $($clock.Id)"

        $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Put) -Path '/Reloj' -ApiKey $BackendApiKey -Body @{
            _idReloj = $clock.Id
            _puerto = $clock.Port
            _deviceSn = $clock.Sn
        }
        Assert-Status $response 200 "Asignacion de DeviceSn al reloj $($clock.Id)"
    }

    $heartbeatTimestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $canonicalHeartbeat = "$heartbeatTimestamp|$heartbeatDeviceId|$residentialId"
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($heartbeatSecret))
    try {
        $heartbeatHash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonicalHeartbeat))
        $heartbeatSignature = -join ($heartbeatHash | ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $hmac.Dispose()
    }
    $heartbeat = @{
        deviceId = $heartbeatDeviceId
        residentialId = $residentialId
        timeStamp = $heartbeatTimestamp
        signature = $heartbeatSignature
    }

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Residential/heartbeat' -Body $heartbeat
    Assert-Status $response 204 'Heartbeat publico firmado'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Residential/heartbeat' -Body $heartbeat
    Assert-Status $response 204 'Reintento idempotente de heartbeat'

    $invalidHeartbeat = $heartbeat.Clone()
    $invalidHeartbeat.signature = [string]::new('0', 64)
    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path '/Residential/heartbeat' -Body $invalidHeartbeat
    Assert-Status $response 401 'Heartbeat con firma incorrecta'

    $start = [DateTimeOffset]::UtcNow.AddHours(-8)
    $end = [DateTimeOffset]::UtcNow.AddMinutes(-1)
    $checkIn = @{
        dateTime = $start.ToString('o')
        eventType = 'AccessControllerEvent'
        deviceID = $clockASn
        accessControllerEvent = @{
            serialNo = 1
            employeeNoString = $employee
            majorEventType = 5
            subEventType = 1
            attendanceStatus = 'checkIn'
        }
    }
    $checkOut = @{
        dateTime = $end.ToString('o')
        eventType = 'AccessControllerEvent'
        deviceID = $clockBSn
        accessControllerEvent = @{
            serialNo = 1
            employeeNoString = $employee
            majorEventType = 5
            subEventType = 1
            attendanceStatus = 'checkOut'
        }
    }

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path "/AccessEvents/push/$clockAId" -Body $checkIn
    Assert-Status $response 200 'Push check-in autorizado por IP residencial'
    Assert-True (($response.Body | ConvertFrom-Json).status -eq 'inserted') 'El check-in no fue insertado.'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path "/AccessEvents/push/$clockAId" -Body $checkIn
    Assert-Status $response 200 'Reintento del push check-in'
    Assert-True (($response.Body | ConvertFrom-Json).status -eq 'duplicate') 'El reintento no fue detectado como duplicado.'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Post) -Path "/AccessEvents/push/$clockBId" -Body $checkOut
    Assert-Status $response 200 'Push check-out desde otro reloj del residencial'
    Assert-True (($response.Body | ConvertFrom-Json).status -eq 'inserted') 'El check-out no fue insertado.'

    $encodedEmployee = [Uri]::EscapeDataString($employee)
    $encodedResidential = [Uri]::EscapeDataString($residentialId)
    $projectionReady = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) `
            -Path "/admin/jornadas/projection-states?status=READY&limit=500" `
            -ApiKey $BackendApiKey
        Assert-Status $response 200 'Consulta del estado de proyeccion'
        $state = $null
        foreach ($candidate in ($response.Body | ConvertFrom-Json)) {
            if ($candidate.employeeNumber -eq $employee -and
                $candidate.residentialId -eq $residentialId) {
                $state = $candidate
                break
            }
        }
        if ($null -ne $state -and $state.status -eq 'READY' -and $state.appliedRevision -eq $state.requestedRevision) {
            $projectionReady = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    Assert-True $projectionReady "La cola persistente no completo la proyeccion de jornada. Ultima respuesta: $($response.Body)"

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) `
        -Path "/Jornadas?employeeNumber=$encodedEmployee&residentialId=$encodedResidential&limit=10" `
        -ApiKey $BackendApiKey
    Assert-Status $response 200 'Consulta de jornadas'
    $jornadas = @()
    foreach ($item in ($response.Body | ConvertFrom-Json)) {
        $jornadas += ,$item
    }
    Assert-True ($jornadas.Count -eq 1) 'No se obtuvo exactamente una jornada para el empleado del smoke test.'
    $jornada = $jornadas[0]
    Assert-True ($jornada.statusCheck -eq 'OK') 'La jornada no quedo cerrada correctamente.'
    Assert-True ($jornada.statusBreak -eq 'NO_BREAK') 'La jornada sin descanso no quedo marcada como NO_BREAK.'
    Assert-True ($jornada.startDeviceSn -eq $clockASn) 'La jornada no conservo el reloj del check-in.'
    Assert-True ($jornada.endDeviceSn -eq $clockBSn) 'La jornada no permitio cerrar desde otro reloj del residencial.'

    $response = Send-ApiRequest -Method ([Net.Http.HttpMethod]::Get) `
        -Path "/AccessEvents?employeeNumber=$encodedEmployee&residentialId=$encodedResidential&limit=10" `
        -ApiKey $BackendApiKey
    Assert-Status $response 200 'Consulta de eventos de acceso'
    $events = @()
    foreach ($item in ($response.Body | ConvertFrom-Json)) {
        $events += ,$item
    }
    Assert-True ($events.Count -eq 2) 'El smoke test esperaba dos eventos persistidos y deduplicados.'

    $smokePassed = $true
    Write-Host 'HTTP smoke test passed: auth, heartbeat, replay, push, deduplication, queue and cross-clock jornada.'
}
catch {
    Write-Error $_
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Host '--- API stdout ---'
        Get-Content -LiteralPath $stdoutPath
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Host '--- API stderr ---'
        Get-Content -LiteralPath $stderrPath
    }
    throw
}
finally {
    $client.Dispose()
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    if ($smokePassed) {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "Smoke logs preserved at $stdoutPath and $stderrPath"
    }
}
