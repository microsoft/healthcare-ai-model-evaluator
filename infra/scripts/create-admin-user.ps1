#!/usr/bin/env pwsh

param(
    [string]$adminEmail,
    [string]$adminPassword,
    [string]$adminName
)

# Admin User Creation Script for MedBench
# 
# This script uses Azure CLI to interact with Cosmos DB MongoDB API
# Since azd/az CLI is already required for deployment, this approach is:
# - Clean: No additional dependencies 
# - Reliable: Uses existing authentication
# - Fast: Direct Azure API calls

Write-Host "Setting up first admin user..."

# Get Cosmos DB account info from environment
$cosmosAccountName = azd env get-value COSMOS_ACCOUNT_NAME 2>$null
$resourceGroupName = azd env get-value AZURE_RESOURCE_GROUP_NAME 2>$null

if (-not $cosmosAccountName -or -not $resourceGroupName) {
    Write-Error "❌ Missing required environment variables:"
    Write-Host "   COSMOS_ACCOUNT_NAME: $cosmosAccountName"
    Write-Host "   AZURE_RESOURCE_GROUP_NAME: $resourceGroupName"
    exit 1
}

Write-Host "Using Cosmos DB account: $cosmosAccountName in resource group: $resourceGroupName"

Write-Host "Creating first admin user for MedBench..."
Write-Host ""

# Prompt for admin user details if not provided
if (-not $adminEmail) {
    do {
        $adminEmail = Read-Host "Enter admin email"
        if ($adminEmail -notmatch "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$") {
            Write-Host "❌ Please enter a valid email address"
            $adminEmail = $null
        }
    } while (-not $adminEmail)
}

# Prompt for password if not provided
if (-not $adminPassword) {
    do {
        Write-Host ""
        Write-Host "Password requirements:"
        Write-Host "- At least 8 characters long"
        Write-Host "- Include characters from at least 3 categories: uppercase, lowercase, numbers, special characters"
        Write-Host "- Cannot contain account name or display name"
        Write-Host "- Cannot be a common/easily guessable password"
        Write-Host ""
        
        $securePassword = Read-Host "Enter admin password" -AsSecureString
        $adminPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))
        
        $securePasswordConfirm = Read-Host "Confirm admin password" -AsSecureString
        $adminPasswordConfirm = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePasswordConfirm))
        
        if ($adminPassword -ne $adminPasswordConfirm) {
            Write-Host "❌ Passwords do not match. Please try again."
            $adminPassword = $null
            continue
        }
        
        if ($adminPassword.Length -lt 8) {
            Write-Host "❌ Password must be at least 8 characters long."
            $adminPassword = $null
            continue
        }
        
        # Check for account name in password (case insensitive)
        $accountName = $adminEmail.Split('@')[0]
        if ($adminPassword.ToLower().Contains($accountName.ToLower())) {
            Write-Host "❌ Password cannot contain the account name."
            $adminPassword = $null
            continue
        }
        
        # Validate password complexity
        $categories = 0
        if ($adminPassword -cmatch "[a-z]") { $categories++ }
        if ($adminPassword -cmatch "[A-Z]") { $categories++ }
        if ($adminPassword -match "[0-9]") { $categories++ }
        if ($adminPassword -match "[^a-zA-Z0-9]") { $categories++ }
        
        if ($categories -lt 3) {
            Write-Host "❌ Password must include characters from at least three categories: uppercase letters, lowercase letters, numbers, and special characters."
            $adminPassword = $null
            continue
        }
        
        # Check against common passwords
        $commonPasswords = @("123456", "password", "12345678", "qwerty", "123456789", "12345", "1234", "111111", "1234567", "dragon", "123123", "baseball", "abc123", "football", "monkey", "letmein", "696969", "shadow", "master", "666666", "qwertyuiop", "123321", "mustang", "1234567890", "michael", "654321", "superman", "1qaz2wsx", "7777777", "121212", "000000", "qazwsx", "123qwe", "killer", "trustno1", "jordan", "jennifer", "zxcvbnm", "asdfgh", "hunter", "buster", "soccer", "harley", "batman", "andrew", "tigger", "sunshine", "iloveyou", "2000", "charlie", "robert", "thomas", "hockey", "ranger", "daniel", "starwars", "klaster", "112233", "george", "computer", "michelle", "jessica", "pepper", "1111", "zxcvbn", "555555", "11111111", "131313", "freedom", "777777", "pass", "maggie", "159753", "aaaaaa", "ginger", "princess", "joshua", "cheese", "amanda", "summer", "love", "ashley", "6969", "nicole", "chelsea", "matthew", "access", "yankees", "987654321", "dallas", "austin", "thunder", "taylor", "matrix", "william", "corvette", "hello", "martin", "heather", "secret", "merlin", "diamond", "1234qwer", "hammer", "silver", "222222", "88888888", "anthony", "justin", "test", "bailey", "q1w2e3r4t5", "patrick", "internet", "scooter", "orange", "11111", "golfer", "cookie", "richard", "samantha", "bigdog", "guitar", "jackson", "whatever", "mickey", "chicken", "sparky", "snoopy", "maverick", "phoenix", "camaro", "peanut", "morgan", "welcome", "falcon", "cowboy", "ferrari", "samsung", "andrea", "smokey", "steelers", "joseph", "mercedes", "dakota", "arsenal", "eagles", "melissa", "boomer", "spider", "nascar", "monster", "tigers", "yellow", "xxxxxx", "123123123", "gateway", "marina", "diablo", "bulldog", "qwer1234", "compaq", "purple", "hardcore", "banana", "junior")
        
        if ($commonPasswords -contains $adminPassword.ToLower()) {
            Write-Host "❌ This password is too common and easily guessable. Please choose a different password."
            $adminPassword = $null
            continue
        }
        
        # Clear the confirmation password from memory
        $adminPasswordConfirm = $null
        break
        
    } while ($true)
}

if (-not $adminName) {
    $adminName = Read-Host "Enter admin full name"
    if (-not $adminName) {
        $adminName = "Administrator"
    }
}

Write-Host ""
Write-Host "Creating admin user with:"
Write-Host "Email: $adminEmail"
Write-Host "Name: $adminName"
Write-Host ""

# Function to hash password using .NET cryptography (PBKDF2)
function Get-HashedPassword {
    param([string]$password)
    
    # Generate salt
    $saltBytes = New-Object byte[] 16
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($saltBytes)
    $salt = [Convert]::ToBase64String($saltBytes)
    
    # Generate hash using PBKDF2
    $pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $saltBytes, 100000)
    $hashBytes = $pbkdf2.GetBytes(32)
    $hash = [Convert]::ToBase64String($hashBytes)
    
    return @{
        Hash = $hash
        Salt = $salt
    }
}

# Generate user ID and hash password
$userId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()  # Generate timestamp-based ID like the working example
$passwordResult = Get-HashedPassword $adminPassword
$currentDate = Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ"

# Get connection string for MongoDB operations
Write-Host "Getting Cosmos DB connection string..."
$cosmosConnectionString = az cosmosdb keys list `
    --name $cosmosAccountName `
    --resource-group $resourceGroupName `
    --type connection-strings `
    --query "connectionStrings[?description=='Primary MongoDB Connection String'].connectionString | [0]" `
    --output tsv 2>$null

# If that fails, try the alternative method
if (-not $cosmosConnectionString) {
    Write-Host "Trying alternative connection string retrieval..."
    $cosmosConnectionString = az cosmosdb show-connection-string `
        --name $cosmosAccountName `
        --resource-group $resourceGroupName `
        --type mongodb `
        --query "connectionString" `
        --output tsv 2>$null
}

# If still no connection string, try getting it from Key Vault
if (-not $cosmosConnectionString) {
    Write-Host "Trying to get connection string from Key Vault..."
    $keyVaultName = azd env get-value AZURE_KEY_VAULT_NAME 2>$null
    if ($keyVaultName) {
        $cosmosConnectionString = az keyvault secret show `
            --vault-name $keyVaultName `
            --name "cosmos-connection-string" `
            --query value `
            --output tsv 2>$null
    }
}

if (-not $cosmosConnectionString) {
    Write-Error "❌ Failed to get Cosmos DB connection string"
    Write-Host "Please ensure:"
    Write-Host "1. The Cosmos DB account exists and is accessible"
    Write-Host "2. You have proper permissions on the resource group"
    Write-Host "3. The account is configured for MongoDB API"
    exit 1
}

Write-Host "✅ Connection string retrieved successfully"

# Temporarily enable public network access for admin user creation
Write-Host "Temporarily enabling public network access for Cosmos DB..."
try {
    az cosmosdb update `
        --name $cosmosAccountName `
        --resource-group $resourceGroupName `
        --enable-public-network true `
        --output none 2>$null
} catch {
    Write-Host "⚠️  Warning: Could not enable public network access. This might fail if using private endpoints."
}

# Wait a moment for the setting to take effect
Start-Sleep -Seconds 10

# Check if user already exists using MongoDB query
Write-Host "Checking if user already exists..."
if (Get-Command mongosh -ErrorAction SilentlyContinue) {
    # Use mongosh
    $existingUserCheck = mongosh "$cosmosConnectionString" --quiet --eval "db = db.getSiblingDB('HAIMEDB'); db.Users.findOne({Email: '$($adminEmail.ToLower())'})" 2>$null
    Write-Host "User check result: $existingUserCheck"
    if ($existingUserCheck -and $existingUserCheck -ne "null" -and $existingUserCheck -ne "null ") {
        $existingUser = $existingUserCheck
    } else {
        $existingUser = $null
    }
} else {
    # Fallback: assume user doesn't exist
    $existingUser = $null
    Write-Host "mongosh not available, assuming user doesn't exist"
}

if ($existingUser) {
    Write-Host "✅ User with email $adminEmail already exists. Skipping creation."
    Write-Host ""
    Write-Host "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
    azd env set CREATE_ADMIN_USER false
    
    # Disable public network access again for security
    Write-Host "Disabling public network access for Cosmos DB..."
    try {
        az cosmosdb update `
            --name $cosmosAccountName `
            --resource-group $resourceGroupName `
            --enable-public-network false `
            --output none 2>$null
    } catch {
        Write-Host "⚠️  Warning: Could not disable public network access."
    }
    
    Write-Host "🔒 Cosmos DB network access secured."
    exit 0
}

# Create admin user document
Write-Host "Creating admin user..."
if (Get-Command mongosh -ErrorAction SilentlyContinue) {
    # Use mongosh to insert document
    Write-Host "Inserting user document with mongosh..."
    $insertScript = @"
db = db.getSiblingDB('HAIMEDB');
var result = db.Users.insertOne({
    _id: '$userId',
    Name: '$adminName',
    Email: '$($adminEmail.ToLower())',
    Roles: ['admin'],
    Expertise: null,
    CreatedAt: new Date('$currentDate'),
    UpdatedAt: null,
    IsModelReviewer: false,
    ModelId: null,
    Stats: {},
    PasswordHash: '$($passwordResult.Hash)',
    PasswordSalt: '$($passwordResult.Salt)',
    PasswordResetToken: null,
    PasswordResetExpires: null
});
print('✅ Successfully created admin user: $adminEmail');
print('Insert result: ' + JSON.stringify(result));
"@
    
    $result = mongosh "$cosmosConnectionString" --quiet --eval $insertScript 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Failed to create user with mongosh"
        exit 1
    }
} else {
    Write-Error "❌ MongoDB shell (mongosh) not found. Please install mongosh to create the admin user."
    Write-Host "Install with: brew install mongosh"
    exit 1
}

Write-Host ""
Write-Host "✅ Admin user creation completed!"
Write-Host ""
Write-Host "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
azd env set CREATE_ADMIN_USER false

# Disable public network access again for security
Write-Host "Disabling public network access for Cosmos DB..."
try {
    az cosmosdb update `
        --name $cosmosAccountName `
        --resource-group $resourceGroupName `
        --enable-public-network false `
        --output none 2>$null
} catch {
    Write-Host "⚠️  Warning: Could not disable public network access."
}

Write-Host "🔒 Cosmos DB network access secured."

# Clear password from memory
$adminPassword = $null