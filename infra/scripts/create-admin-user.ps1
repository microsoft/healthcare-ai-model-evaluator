#!/usr/bin/env pwsh

param(
    [string]$cosmosConnectionString,
    [string]$adminEmail,
    [string]$adminPassword,
    [string]$adminName
)

Write-Host "Setting up first admin user..."

# Check if we should create admin user
$createAdminUser = $env:CREATE_ADMIN_USER
if (-not $createAdminUser) {
    $createAdminUser = (azd env get-value CREATE_ADMIN_USER 2>$null)
}
if (-not $createAdminUser) {
    $createAdminUser = "true"
}

if ($createAdminUser.ToLower() -ne "true") {
    Write-Host "CREATE_ADMIN_USER is not true, skipping admin user creation"
    exit 0
}

# Get environment variables
if (-not $cosmosConnectionString) {
    $keyVaultName = azd env get-value AZURE_KEY_VAULT_NAME 2>$null
    if (-not $keyVaultName) {
        Write-Error "❌ AZURE_KEY_VAULT_NAME not found in environment"
        exit 1
    }
    
    Write-Host "Getting Cosmos connection string from Key Vault..."
    $cosmosConnectionString = az keyvault secret show --vault-name $keyVaultName --name "cosmos-connection-string" --query value -o tsv 2>$null
}

if (-not $cosmosConnectionString) {
    Write-Error "❌ COSMOS_CONNECTION_STRING not found in environment"
    exit 1
}

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
        Write-Host "- At least 8 characters"
        Write-Host "- Must include 3 of 4: uppercase, lowercase, number, symbol"
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
        
        # Clear the confirmation password from memory
        $adminPasswordConfirm = $null
        
    } while (-not $adminPassword)
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

# Create a temporary Node.js script
$nodeScript = @'
const { MongoClient } = require('mongodb');
const crypto = require('crypto');

function hashPassword(password) {
    const salt = crypto.randomBytes(16).toString('base64');
    const iterations = 100000;
    const keyLength = 32;
    const digest = 'sha256';
    
    const hash = crypto.pbkdf2Sync(password, Buffer.from(salt, 'base64'), iterations, keyLength, digest);
    return {
        hash: hash.toString('base64'),
        salt: salt
    };
}

function validatePasswordComplexity(password) {
    if (!password || password.length < 8) {
        throw new Error("Password must be at least 8 characters.");
    }
    
    let categories = 0;
    if (/[a-z]/.test(password)) categories++;
    if (/[A-Z]/.test(password)) categories++;
    if (/[0-9]/.test(password)) categories++;
    if (/[^a-zA-Z0-9]/.test(password)) categories++;
    
    if (categories < 3) {
        throw new Error("Password must include 3 of 4: upper, lower, number, symbol.");
    }
}

async function createAdminUser() {
    const connectionString = process.env.COSMOS_CONNECTION_STRING;
    const adminEmail = process.env.ADMIN_EMAIL;
    const adminPassword = process.env.ADMIN_PASSWORD;
    const adminName = process.env.ADMIN_NAME;
    
    if (!connectionString || !adminEmail || !adminPassword || !adminName) {
        throw new Error('Missing required environment variables');
    }
    
    // Validate password
    validatePasswordComplexity(adminPassword);
    
    const client = new MongoClient(connectionString);
    
    try {
        await client.connect();
        const db = client.db('MedBenchDB');
        const usersCollection = db.collection('users');
        
        // Check if user already exists
        const existingUser = await usersCollection.findOne({ 
            email: adminEmail.trim().toLowerCase() 
        });
        
        if (existingUser) {
            console.log(`User with email ${adminEmail} already exists. Skipping creation.`);
            return;
        }
        
        // Hash the password
        const { hash, salt } = hashPassword(adminPassword);
        
        // Create admin user
        const adminUser = {
            id: crypto.randomUUID(),
            name: adminName,
            email: adminEmail.trim().toLowerCase(),
            roles: ['admin'],
            expertise: null,
            createdAt: new Date(),
            updatedAt: null,
            isModelReviewer: false,
            modelId: null,
            stats: {},
            passwordHash: hash,
            passwordSalt: salt,
            passwordResetToken: null,
            passwordResetExpires: null
        };
        
        await usersCollection.insertOne(adminUser);
        console.log(`✅ Successfully created admin user: ${adminEmail}`);
        
    } catch (error) {
        console.error('❌ Error creating admin user:', error.message);
        throw error;
    } finally {
        await client.close();
    }
}

createAdminUser().catch(console.error);
'@

$tempScript = [System.IO.Path]::GetTempFileName() + ".js"
$nodeScript | Out-File -FilePath $tempScript -Encoding UTF8

try {
    # Set environment variables
    $env:COSMOS_CONNECTION_STRING = $cosmosConnectionString
    $env:ADMIN_EMAIL = $adminEmail
    $env:ADMIN_PASSWORD = $adminPassword
    $env:ADMIN_NAME = $adminName
    
    # Check if Node.js is available
    if (Get-Command node -ErrorAction SilentlyContinue) {
        # Install mongodb driver if needed
        if (-not (Test-Path "node_modules/mongodb")) {
            Write-Host "Installing MongoDB driver..."
            if (-not (Test-Path "package.json")) {
                npm init -y | Out-Null
            }
            npm install mongodb | Out-Null
        }
        
        node $tempScript
        
        Write-Host ""
        Write-Host "✅ Admin user creation completed!"
        Write-Host ""
        Write-Host "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
        azd env set CREATE_ADMIN_USER false
        
    } else {
        Write-Error "❌ Node.js not found. Cannot create admin user."
        Write-Host "Please install Node.js or create the admin user manually after deployment."
        exit 1
    }
    
} finally {
    # Clean up
    if (Test-Path $tempScript) {
        Remove-Item $tempScript -Force
    }
    
    # Clear password from environment
    $env:ADMIN_PASSWORD = $null
    $adminPassword = $null
}