#Requires -Version 5.1
<#
.SYNOPSIS
    Build PulseFit in Azure Container Registry and run it as an Azure Container App at
    https://gym-demo.repohelpdesk.com. No local Docker needed.

.DESCRIPTION
    1. Creates the resource group and Container Apps environment the first time.
    2. Cloud-builds the Dockerfile from a trimmed copy of the repo (no node_modules, bin, obj).
    3. Creates the Container App, or rolls it to the new image. One replica: the data is a SQLite
       file inside the container, rebuilt on every start and every night.
    4. Checks the app answers.
    5. The custom domain: until it's bound, prints the two DNS records it needs. Once they exist,
       run again with -BindDomain to add the domain and its free managed certificate.

    Needs the Azure CLI, signed in (az login).

.PARAMETER WidgetKey
    The RepoHelpdesk widget key (rhw_...) of the demo project. Kept from the last deploy when left out.

.PARAMETER DashboardUrl
    Where the "See the RepoHelpdesk dashboard" link goes. Kept from the last deploy when left out.

.PARAMETER RepoUrl
    The public source repo, for the "Source code" link. Kept from the last deploy when left out.

.PARAMETER BindDomain
    Add -Domain to the app and issue its certificate. Run once the DNS records are in place.

.EXAMPLE
    .\deploy\deploy.ps1
.EXAMPLE
    .\deploy\deploy.ps1 -WidgetKey rhw_abc123 -DashboardUrl https://app.repohelpdesk.com/demo
.EXAMPLE
    .\deploy\deploy.ps1 -BindDomain
#>
[CmdletBinding()]
param(
    [string]$Tag = (Get-Date -Format "yyyy.MM.dd-HHmm"),
    [string]$WidgetKey,
    [string]$DashboardUrl,
    [string]$RepoUrl,
    [string]$WidgetScriptUrl = "https://app.repohelpdesk.com/widget.js",
    [string]$Domain = "gym-demo.repohelpdesk.com",
    [switch]$BindDomain
)

$ErrorActionPreference = "Stop"

# ---- Where it lives -----------------------------------------------------------
$Location      = "eastus2"
$ResourceGroup = "rg-gym-demo"
$Environment   = "cae-gym-demo"
$App           = "ca-gym-demo"
# The RepoHelpdesk registry: it already holds a Node base image, imported to dodge Docker Hub limits.
$Registry      = "zigitechrhdacr"
$Image         = "pulsefit"
$NodeImage     = "$Registry.azurecr.io/node:24-alpine"

# The az client embeds Python, which crashes printing build logs to a cp1252 console.
$env:PYTHONUTF8 = "1"
$env:PYTHONIOENCODING = "utf-8"

$az = (Get-Command az -ErrorAction SilentlyContinue).Source
if (-not $az) { $az = "$env:ProgramFiles\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" }
if (-not (Test-Path $az)) { throw "Azure CLI not found. Install it, then run az login." }

# az writes warnings to stderr, which Windows PowerShell can turn into a terminating error even when az
# succeeded. Run it with stderr tolerated and judge by the exit code instead.
function Az {
    param([Parameter(Mandatory)][string]$What, [Parameter(Mandatory)][scriptblock]$Command, [switch]$AllowFail)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $out = & $Command 2>$null } finally { $ErrorActionPreference = $prev }
    if (-not $AllowFail -and $LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)." }
    $out
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$commit = (git -C $repoRoot rev-parse --short HEAD).Trim()
if (git -C $repoRoot status --porcelain) { $commit += "-dirty" }
Write-Host "==> Deploying PulseFit $Tag (commit $commit)" -ForegroundColor Cyan

# ---- 1. Resource group and environment ------------------------------------------
if ((Az 'group exists' { & $az group exists -n $ResourceGroup }) -ne 'true') {
    Write-Host "==> Creating resource group $ResourceGroup" -ForegroundColor Cyan
    Az 'group create' { & $az group create -n $ResourceGroup -l $Location -o none } | Out-Null
}
if (-not (Az 'env show' { & $az containerapp env show -n $Environment -g $ResourceGroup --query name -o tsv } -AllowFail)) {
    Write-Host "==> Creating Container Apps environment $Environment (a few minutes, first time only)" -ForegroundColor Cyan
    Az 'env create' { & $az containerapp env create -n $Environment -g $ResourceGroup -l $Location --logs-destination none -o none } | Out-Null
}

# ---- 2. Build -----------------------------------------------------------------
$ctx = Join-Path $env:TEMP "pulsefit-build-ctx"
if (Test-Path $ctx) { Remove-Item $ctx -Recurse -Force }
$null = robocopy $repoRoot $ctx /E /XD .git node_modules bin obj wwwroot .vs deploy /XF *.db *.db-shm *.db-wal /NFL /NDL /NJH /NJS /NP /R:1 /W:1
if ($LASTEXITCODE -ge 8) { throw "Copying the build context failed (robocopy exit $LASTEXITCODE)." }

Write-Host "==> Building $($Image):$Tag in $Registry (logs in the portal: Registry > Tasks > Runs)" -ForegroundColor Cyan
Az 'acr build' { & $az acr build -r $Registry -t "$($Image):$Tag" -t "$($Image):latest" --build-arg "NODE_IMAGE=$NodeImage" --no-logs $ctx -o none } | Out-Null
$run = Az 'acr runs' { & $az acr task list-runs -r $Registry --top 1 --query "[0].{status:status, id:runId}" -o json } | ConvertFrom-Json
while ($run.status -in 'Queued', 'Started', 'Running') {
    Start-Sleep 10
    $run = Az 'acr runs' { & $az acr task list-runs -r $Registry --top 1 --query "[0].{status:status, id:runId}" -o json } | ConvertFrom-Json
}
if ($run.status -ne 'Succeeded') { throw "The image build $($run.id) ended $($run.status). See: az acr task logs -r $Registry --run-id $($run.id)" }
Remove-Item $ctx -Recurse -Force

# ---- 3. Container App -----------------------------------------------------------
$imageRef = "$Registry.azurecr.io/$($Image):$Tag"
$exists = Az 'app show' { & $az containerapp show -n $App -g $ResourceGroup --query name -o tsv } -AllowFail

# Settings the page reads (RepoHelpdesk links). Only what was passed: the rest stay as they are.
$settings = @("ASPNETCORE_FORWARDEDHEADERS_ENABLED=true", "RepoHelpdesk__WidgetScriptUrl=$WidgetScriptUrl")
if ($PSBoundParameters.ContainsKey('WidgetKey'))    { $settings += "RepoHelpdesk__WidgetKey=$WidgetKey" }
if ($PSBoundParameters.ContainsKey('DashboardUrl')) { $settings += "RepoHelpdesk__DashboardUrl=$DashboardUrl" }
if ($PSBoundParameters.ContainsKey('RepoUrl'))      { $settings += "RepoHelpdesk__RepoUrl=$RepoUrl" }

if (-not $exists) {
    Write-Host "==> Creating Container App $App" -ForegroundColor Cyan
    $acrPassword = Az 'acr credentials' { & $az acr credential show -n $Registry --query "passwords[0].value" -o tsv }
    Az 'app create' { & $az containerapp create -n $App -g $ResourceGroup --environment $Environment --image $imageRef `
        --registry-server "$Registry.azurecr.io" --registry-username $Registry --registry-password $acrPassword `
        --ingress external --target-port 8080 --min-replicas 1 --max-replicas 1 --cpu 0.25 --memory 0.5Gi `
        --env-vars @settings -o none } | Out-Null
    $acrPassword = $null
}
else {
    Write-Host "==> Rolling $App to $Tag" -ForegroundColor Cyan
    Az 'app update' { & $az containerapp update -n $App -g $ResourceGroup --image $imageRef --set-env-vars @settings -o none } | Out-Null
}

# ---- 4. Check it answers --------------------------------------------------------
$fqdn = Az 'app fqdn' { & $az containerapp show -n $App -g $ResourceGroup --query "properties.configuration.ingress.fqdn" -o tsv }
$url = "https://$fqdn"
Write-Host "==> Waiting for $url" -ForegroundColor Cyan
$ok = $false
for ($i = 0; $i -lt 36 -and -not $ok; $i++) {
    try {
        $config = Invoke-RestMethod "$url/api/config" -TimeoutSec 10
        $ok = $true
    }
    catch { Start-Sleep 5 }
}
if (-not $ok) { throw "The app didn't answer at $url/api/config. Check: az containerapp logs show -n $App -g $ResourceGroup" }
Write-Host "    Up: $($config.gymName). Widget $(if ($config.widgetKey) { 'on' } else { 'off (no -WidgetKey yet)' })." -ForegroundColor Green

# ---- 5. Custom domain -------------------------------------------------------------
# Filtered here, not in --query: Windows PowerShell strips the quotes inside "[?name=='...']" on the way to az.
$hostnames = @(Az 'hostnames' { & $az containerapp hostname list -n $App -g $ResourceGroup -o json } -AllowFail | ConvertFrom-Json)
$bound = ($hostnames | Where-Object { $_.name -eq $Domain } | Select-Object -First 1).bindingType
if ($bound -eq 'SniEnabled') {
    Write-Host "==> Live at https://$Domain" -ForegroundColor Green
}
elseif ($BindDomain) {
    Write-Host "==> Binding $Domain and issuing its certificate (a few minutes)" -ForegroundColor Cyan
    if (-not $bound) { Az 'hostname add' { & $az containerapp hostname add --hostname $Domain -n $App -g $ResourceGroup -o none } | Out-Null }
    Az 'hostname bind' { & $az containerapp hostname bind --hostname $Domain -n $App -g $ResourceGroup `
        --environment $Environment --validation-method CNAME -o none } | Out-Null
    Write-Host "==> Live at https://$Domain" -ForegroundColor Green
}
else {
    $verification = Az 'verification id' { & $az containerapp show -n $App -g $ResourceGroup --query "properties.customDomainVerificationId" -o tsv }
    $sub = $Domain.Split('.')[0]
    Write-Host ""
    Write-Host "Running at $url" -ForegroundColor Green
    Write-Host "For https://$Domain add these at the DNS provider for $($Domain.Substring($sub.Length + 1)), then run again with -BindDomain:" -ForegroundColor Yellow
    Write-Host "    CNAME  $sub         ->  $fqdn"
    Write-Host "    TXT    asuid.$sub   ->  $verification"
}
