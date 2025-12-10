#!/usr/bin/env pwsh

# Pre-provision hook to set up IP filtering for web application security

Write-Host ""
Write-Host "🔒 Setting up IP filtering for web application security..."
Write-Host ""

# Function to get current public IP
function Get-CurrentIP {
    $ip = $null
    
    # Try multiple services for reliability
    try {
        $ip = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 5 -ErrorAction SilentlyContinue)
    } catch {}
    
    if (-not $ip) {
        try {
            $ip = (Invoke-RestMethod -Uri "https://ifconfig.me" -TimeoutSec 5 -ErrorAction SilentlyContinue)
        } catch {}
    }
    
    if (-not $ip) {
        try {
            $response = (Invoke-RestMethod -Uri "https://httpbin.org/ip" -TimeoutSec 5 -ErrorAction SilentlyContinue)
            $ip = $response.origin
        } catch {}
    }
    
    return $ip
}

# Get current IP
$currentIP = Get-CurrentIP

# Check if IP filtering is already configured
$existingEnabled = ""
$existingIP = ""

try {
    $existingEnabled = azd env get-value ENABLE_WEB_IP_FILTERING 2>$null
    if ($LASTEXITCODE -ne 0) { $existingEnabled = "" }
} catch {
    $existingEnabled = ""
}

try {
    $existingIP = azd env get-value ALLOWED_WEB_IP 2>$null
    if ($LASTEXITCODE -ne 0) { $existingIP = "" }
} catch {
    $existingIP = ""
}

if ($existingEnabled -and $existingIP -and $existingEnabled -ne "ERROR:" -and $existingIP -ne "ERROR:") {
    Write-Host "✅ Web IP filtering already configured:"
    Write-Host "   Enabled: $existingEnabled"
    Write-Host "   Allowed IP: $existingIP"
    Write-Host ""
    exit 0
}

Write-Host "🌐 For security, the web application will be protected by IP filtering."
Write-Host "   Only specified IP addresses will be able to access the API."
Write-Host ""

if ($currentIP) {
    Write-Host "📍 Your current public IP address is: $currentIP"
    Write-Host ""
    
    # Check if running in non-interactive mode (like in azd hooks)
    if ($env:CI -or $env:GITHUB_ACTIONS -or [Environment]::UserInteractive -eq $false) {
        Write-Host "🤖 Non-interactive environment detected. Auto-configuring with detected IP..."
        $inputIP = "$currentIP/32"
        $enableFiltering = "true"
    } else {
        # Interactive mode - prompt for user input
        $inputIP = Read-Host "Enter IP address(es) to allow (CIDR format, comma-separated, e.g., $currentIP/32) [default: $currentIP/32]"
        
        if (-not $inputIP) {
            $inputIP = "$currentIP/32"
        }
        
        # Validate CIDR format for each IP
        $ipArray = $inputIP -split ',' | ForEach-Object { $_.Trim() }
        foreach ($ip in $ipArray) {
            if ($ip -notmatch '^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$') {
                Write-Host "❌ Invalid CIDR format: $ip. Please use format like 203.0.113.1/32"
                exit 1
            }
        }
        
        # Ask if user wants to enable IP filtering
        Write-Host ""
        $enableFiltering = Read-Host "Enable IP filtering? This will block all other IPs [Y/n]"
        
        if (-not $enableFiltering -or $enableFiltering -in @("Y", "y", "yes", "Yes")) {
            $enableFiltering = "true"
        } else {
            $enableFiltering = "false"
        }
    }
} else {
    Write-Host "⚠️  Could not auto-detect your current IP address."
    
    # Check if non-interactive
    if ($env:CI -or $env:GITHUB_ACTIONS -or [Environment]::UserInteractive -eq $false) {
        Write-Host "❌ Non-interactive environment and no IP detected. Disabling IP filtering for initial deployment."
        Write-Host "   You can enable it later with: azd env set ENABLE_WEB_IP_FILTERING true"
        Write-Host "   And set your IP with: azd env set ALLOWED_WEB_IP your.ip.address/32"
        $inputIP = ""
        $enableFiltering = "false"
    } else {
        # Interactive mode - require user input
        Write-Host "   Please enter your IP address manually."
        Write-Host ""
        
        do {
            $inputIP = Read-Host "Enter IP address(es) to allow (CIDR format, comma-separated, e.g., 203.0.113.1/32,198.51.100.0/24)"
            if (-not $inputIP) {
                Write-Host "❌ IP address is required for security."
            }
        } while (-not $inputIP)
        
        # Validate CIDR format for each IP
        $ipArray = $inputIP -split ',' | ForEach-Object { $_.Trim() }
        foreach ($ip in $ipArray) {
            if ($ip -notmatch '^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$') {
                Write-Host "❌ Invalid CIDR format: $ip. Please use format like 203.0.113.1/32"
                exit 1
            }
        }
        
        $enableFiltering = "true"
    }
}

# Set environment variables
Write-Host ""
Write-Host "📝 Setting azd environment variables..."
azd env set ENABLE_WEB_IP_FILTERING $enableFiltering
azd env set ALLOWED_WEB_IP $inputIP

Write-Host ""
Write-Host "✅ Web application security configured:"
Write-Host "   IP Filtering Enabled: $enableFiltering"
Write-Host "   Allowed IP: $inputIP"
Write-Host ""
Write-Host "💡 You can change these settings later with:"
Write-Host "   azd env set ENABLE_WEB_IP_FILTERING true|false"
Write-Host "   azd env set ALLOWED_WEB_IP your.ip.address/32"
Write-Host ""