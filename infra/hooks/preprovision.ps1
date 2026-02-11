#!/usr/bin/env pwsh

# Pre-provision hook to configure deployment networking mode.
# - open: public ingress (default)
# - private: internal-only Container Apps Environment + VNet integration for Functions

function Get-EnvValue([string]$key) {
    try {
        $value = azd env get-value $key 2>$null
        if ($LASTEXITCODE -ne 0) { return "" }
        if ($value -match "not found" -or $value -eq "ERROR:") { return "" }
        return $value
    } catch {
        return ""
    }
}

function Set-EnvValue([string]$key, [string]$value) {
    azd env set $key $value | Out-Null
}

function Is-Interactive {
    return (-not $env:CI) -and (-not $env:GITHUB_ACTIONS) -and (-not [Console]::IsInputRedirected)
}

function Convert-IPv4ToUInt32([string]$ip) {
    $bytes = [System.Net.IPAddress]::Parse($ip).GetAddressBytes()
    if ($bytes.Length -ne 4) { throw "Only IPv4 is supported: $ip" }
    # bytes are big-endian
    return ([uint32]$bytes[0] -shl 24) -bor ([uint32]$bytes[1] -shl 16) -bor ([uint32]$bytes[2] -shl 8) -bor ([uint32]$bytes[3])
}

function Convert-UInt32ToIPv4([uint32]$value) {
    $b0 = ($value -shr 24) -band 0xFF
    $b1 = ($value -shr 16) -band 0xFF
    $b2 = ($value -shr 8) -band 0xFF
    $b3 = $value -band 0xFF
    return "$b0.$b1.$b2.$b3"
}

function Normalize-Cidr([string]$cidr) {
    $parts = $cidr.Split('/')
    if ($parts.Count -ne 2) { throw "Invalid CIDR: $cidr" }
    $ip = $parts[0].Trim()
    $prefixRaw = $parts[1].Trim()

    [int]$prefix = -1
    if (-not [int]::TryParse($prefixRaw, [ref]$prefix)) { throw "Invalid CIDR prefix: $cidr" }
    if ($prefix -lt 0 -or $prefix -gt 32) { throw "Invalid CIDR prefix: $cidr" }

    $ipU = Convert-IPv4ToUInt32 $ip
    $mask = if ($prefix -eq 0) { [uint32]0 } else { ([uint32]0xFFFFFFFF) -shl (32 - $prefix) }
    $netU = $ipU -band $mask
    $netIp = Convert-UInt32ToIPv4 $netU
    return [PSCustomObject]@{
        Original = $cidr
        Prefix = $prefix
        Network = "$netIp/$prefix"
        IsAligned = ("$netIp/$prefix" -eq $cidr.Trim())
        Start = $netU
        End = $netU + ([uint32]([math]::Pow(2, (32 - $prefix)) - 1))
    }
}

function Assert-CreateVnetCidrs([string]$vnetCidr, [string]$acaCidr, [string]$funcCidr) {
    $vnet = Normalize-Cidr $vnetCidr
    $aca = Normalize-Cidr $acaCidr
    $func = Normalize-Cidr $funcCidr

    $errors = @()
    if (-not $vnet.IsAligned) { $errors += "VNET_ADDRESS_SPACE '$($vnet.Original)' should be '$($vnet.Network)'" }
    if (-not $aca.IsAligned) { $errors += "ACA_INFRASTRUCTURE_SUBNET_PREFIX '$($aca.Original)' should be '$($aca.Network)'" }
    if (-not $func.IsAligned) { $errors += "FUNCTIONS_INTEGRATION_SUBNET_PREFIX '$($func.Original)' should be '$($func.Network)'" }

    if ($aca.Start -lt $vnet.Start -or $aca.End -gt $vnet.End) { $errors += "ACA_INFRASTRUCTURE_SUBNET_PREFIX must be within VNET_ADDRESS_SPACE" }
    if ($func.Start -lt $vnet.Start -or $func.End -gt $vnet.End) { $errors += "FUNCTIONS_INTEGRATION_SUBNET_PREFIX must be within VNET_ADDRESS_SPACE" }

    $overlap = -not (($aca.End -lt $func.Start) -or ($func.End -lt $aca.Start))
    if ($overlap) { $errors += "Subnets overlap: '$($aca.Original)' and '$($func.Original)'" }

    if ($errors.Count -gt 0) {
        Write-Host ""
        Write-Host "Invalid CIDR values detected for createVnet=true:" -ForegroundColor Red
        foreach ($e in $errors) { Write-Host "  - $e" -ForegroundColor Red }

        if (Is-Interactive) {
            Write-Host ""
            Write-Host "Azure requires the base network address for the prefix (e.g., 10.0.0.0/23, not 10.0.1.0/23)."
            $apply = Read-Host "Apply normalized values to azd env? [Y/n]"
            if (-not $apply -or $apply -match '^(Y|y|yes)$') {
                Set-EnvValue "VNET_ADDRESS_SPACE" $vnet.Network
                Set-EnvValue "ACA_INFRASTRUCTURE_SUBNET_PREFIX" $aca.Network
                Set-EnvValue "FUNCTIONS_INTEGRATION_SUBNET_PREFIX" $func.Network
                return
            }
        }

        throw "CIDR validation failed. Fix values and re-run."
    }
}

$deploymentNetworking = Get-EnvValue "DEPLOYMENT_NETWORKING"

if (-not $deploymentNetworking) {
    $deploymentNetworking = "private"
    if (Is-Interactive) {
        Write-Host ""
        Write-Host "Deployment networking mode:"
        Write-Host "  1) open    - public ingress"
        Write-Host "  2) private - internal-only (VNet) (default)"
        $choice = Read-Host "Choose [1/2] (default: 2)"
        if ($choice -eq "1") { $deploymentNetworking = "open" }
    }
    Set-EnvValue "DEPLOYMENT_NETWORKING" $deploymentNetworking
}

if ($deploymentNetworking -ne "private") {
    # Open mode: do not auto-configure IP filtering.
    exit 0
}

$createVnet = Get-EnvValue "CREATE_VNET"
$existingVnetResourceId = Get-EnvValue "EXISTING_VNET_RESOURCE_ID"
$existingAcaSubnetId = Get-EnvValue "EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID"
$existingFunctionsSubnetId = Get-EnvValue "EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID"

# If we're creating a VNet (or it's not set yet), validate CIDRs early to prevent ARM 400 InvalidCIDRNotation.
if (-not $createVnet -or $createVnet -eq "true") {
    $vnetCidr = Get-EnvValue "VNET_ADDRESS_SPACE"
    $acaCidr = Get-EnvValue "ACA_INFRASTRUCTURE_SUBNET_PREFIX"
    $funcCidr = Get-EnvValue "FUNCTIONS_INTEGRATION_SUBNET_PREFIX"

    if (-not $vnetCidr) { $vnetCidr = "10.30.0.0/16" }
    if (-not $acaCidr) { $acaCidr = "10.30.0.0/23" }
    if (-not $funcCidr) { $funcCidr = "10.30.2.0/24" }

    Assert-CreateVnetCidrs -vnetCidr $vnetCidr -acaCidr $acaCidr -funcCidr $funcCidr
}

if ($createVnet -eq "true") {
    exit 0
}

if ($existingAcaSubnetId -and $existingFunctionsSubnetId) {
    exit 0
}

if (-not (Is-Interactive)) {
    Write-Host "DEPLOYMENT_NETWORKING=private but VNet settings are not configured."
    Write-Host "Set either CREATE_VNET=true to create a new VNet, or provide existing subnet IDs:"
    Write-Host "  azd env set CREATE_VNET true"
    Write-Host "  -or-"
    Write-Host "  azd env set CREATE_VNET false"
    Write-Host "  azd env set EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID <subnetResourceId>"
    Write-Host "  azd env set EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID <subnetResourceId>"
    exit 1
}

Write-Host ""
Write-Host "Private networking selected. Configure VNet:"
$createChoice = Read-Host "Create a new VNet (recommended)? [Y/n]"
if (-not $createChoice -or $createChoice -match '^(Y|y|yes)$') {
    Set-EnvValue "CREATE_VNET" "true"
    Set-EnvValue "EXISTING_VNET_RESOURCE_ID" ""
    Set-EnvValue "EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID" ""
    Set-EnvValue "EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID" ""
    exit 0
}

Set-EnvValue "CREATE_VNET" "false"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Azure CLI (az) is required to query existing VNets."
    Write-Host "Either install az, or set subnet IDs manually with azd env set."
    exit 1
}

Write-Host ""
Write-Host "Fetching VNets in the current Azure subscription..."
$vnets = az network vnet list --query "[].{name:name,rg:resourceGroup,id:id}" -o tsv 2>$null
if (-not $vnets) {
    Write-Host "No VNets found. Switch to CREATE_VNET=true or create one first."
    exit 1
}

$vnetLines = $vnets -split "`n" | Where-Object { $_ -and $_.Trim().Length -gt 0 }
Write-Host ""
Write-Host "Select a VNet:"
for ($i = 0; $i -lt $vnetLines.Count; $i++) {
    $parts = $vnetLines[$i] -split "`t"
    Write-Host "  $($i+1)) $($parts[0]) (rg: $($parts[1]))"
}

$vnetIndexRaw = Read-Host "Enter number"
[int]$vnetIndex = -1
if (-not [int]::TryParse($vnetIndexRaw, [ref]$vnetIndex)) {
    Write-Host "Invalid selection."
    exit 1
}
$vnetIndex = $vnetIndex - 1
if ($vnetIndex -lt 0 -or $vnetIndex -ge $vnetLines.Count) {
    Write-Host "Invalid selection."
    exit 1
}

$selectedParts = $vnetLines[$vnetIndex] -split "`t"
$selectedVnetName = $selectedParts[0]
$selectedVnetRg = $selectedParts[1]
$selectedVnetId = $selectedParts[2]
Set-EnvValue "EXISTING_VNET_RESOURCE_ID" $selectedVnetId

Write-Host ""
Write-Host "Fetching subnets for $selectedVnetName..."
$subnets = az network vnet subnet list -g $selectedVnetRg --vnet-name $selectedVnetName --query "[].{name:name,id:id}" -o tsv 2>$null
if (-not $subnets) {
    Write-Host "No subnets found in selected VNet."
    exit 1
}

$subnetLines = $subnets -split "`n" | Where-Object { $_ -and $_.Trim().Length -gt 0 }

Write-Host ""
Write-Host "Select subnet for Container Apps Environment infrastructure (must be dedicated and delegated to Microsoft.App/environments):"
for ($i = 0; $i -lt $subnetLines.Count; $i++) {
    $parts = $subnetLines[$i] -split "`t"
    Write-Host "  $($i+1)) $($parts[0])"
}
$acaIndexRaw = Read-Host "Enter number"
[int]$acaIndex = -1
if (-not [int]::TryParse($acaIndexRaw, [ref]$acaIndex)) {
    Write-Host "Invalid selection."
    exit 1
}
$acaIndex = $acaIndex - 1
if ($acaIndex -lt 0 -or $acaIndex -ge $subnetLines.Count) {
    Write-Host "Invalid selection."
    exit 1
}
$acaParts = $subnetLines[$acaIndex] -split "`t"
Set-EnvValue "EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID" $acaParts[1]

Write-Host ""
Write-Host "Select subnet for Azure Functions VNet integration (delegated to Microsoft.Web/serverFarms):"
for ($i = 0; $i -lt $subnetLines.Count; $i++) {
    $parts = $subnetLines[$i] -split "`t"
    Write-Host "  $($i+1)) $($parts[0])"
}
$funcIndexRaw = Read-Host "Enter number"
[int]$funcIndex = -1
if (-not [int]::TryParse($funcIndexRaw, [ref]$funcIndex)) {
    Write-Host "Invalid selection."
    exit 1
}
$funcIndex = $funcIndex - 1
if ($funcIndex -lt 0 -or $funcIndex -ge $subnetLines.Count) {
    Write-Host "Invalid selection."
    exit 1
}
$funcParts = $subnetLines[$funcIndex] -split "`t"
Set-EnvValue "EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID" $funcParts[1]

Write-Host ""
Write-Host "Configured private networking."