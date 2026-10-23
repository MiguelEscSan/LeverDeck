# Prepares this PC for LeverDeck's optional HTTPS on the LAN.
# Installs mkcert when it is missing, trusts its local root CA, and opens the private firewall port.
# The agent issues the certificate itself on start (agent/src/LeverDeck.Infrastructure/Tls/LanCertificate.cs).
# Run from the repository root: powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1

$ErrorActionPreference = 'Stop'
$port = 8765
$ruleName = "LeverDeck agent TCP $port"

function Update-SessionPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Get-Command mkcert -ErrorAction SilentlyContinue)) {
    Write-Host 'mkcert not found. Installing it with winget...'
    winget install --id FiloSottile.mkcert -e --accept-package-agreements --accept-source-agreements
    Update-SessionPath
    if (-not (Get-Command mkcert -ErrorAction SilentlyContinue)) {
        throw 'mkcert was installed but is not on PATH yet. Open a new terminal and run this script again.'
    }
}

Write-Host 'Trusting the mkcert local root CA (Windows may ask you to confirm)...'
mkcert -install
# mkcert also tries the Java keystore; that step fails without administrator rights when a JDK is installed.
# The Windows store, which is what the agent needs, is trusted either way, so only the Java step is skipped.
if ($LASTEXITCODE -ne 0) {
    Write-Warning 'mkcert -install reported an error. If the Windows store was updated, only the Java keystore was skipped.'
}

$caRoot = mkcert -CAROOT
if (-not (Test-Path (Join-Path $caRoot 'rootCA.pem'))) {
    throw "The mkcert root CA was not created in $caRoot."
}
Write-Host "Root CA folder: $caRoot (copy rootCA.pem to each phone and install it as a CA certificate)."

if (Test-Administrator) {
    netsh advfirewall firewall delete rule name="$ruleName" | Out-Null
    netsh advfirewall firewall add rule name="$ruleName" dir=in action=allow protocol=TCP localport=$port profile=private | Out-Null
    Write-Host "Firewall rule '$ruleName' added for the private network."
} else {
    Write-Host 'Not running as administrator, so the firewall rule was not added. Run this once from an elevated prompt:'
    Write-Host "netsh advfirewall firewall add rule name=`"$ruleName`" dir=in action=allow protocol=TCP localport=$port profile=private"
}

Write-Host ''
Write-Host 'Done. Start the agent with HTTPS:'
Write-Host '  $env:LeverDeck__Https__Enabled = "true"; dotnet run --project agent/src/LeverDeck.App'
