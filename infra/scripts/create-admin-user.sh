#!/bin/bash
set -euo pipefail

# Admin User Creation Script for MedBench
# 
# This script uses Azure CLI to interact with Cosmos DB MongoDB API
# Since azd/az CLI is already required for deployment, this approach is:
# - Clean: No additional dependencies 
# - Reliable: Uses existing authentication
# - Fast: Direct Azure API calls

echo "Setting up first admin user..."

# Get Cosmos DB account info from environment
COSMOS_ACCOUNT_NAME=$(azd env get-value COSMOS_ACCOUNT_NAME 2>/dev/null || echo "")
RESOURCE_GROUP_NAME=$(azd env get-value AZURE_RESOURCE_GROUP_NAME 2>/dev/null || echo "")

if [ -z "$COSMOS_ACCOUNT_NAME" ] || [ -z "$RESOURCE_GROUP_NAME" ]; then
    echo "❌ Missing required environment variables:"
    echo "   COSMOS_ACCOUNT_NAME: $COSMOS_ACCOUNT_NAME"
    echo "   AZURE_RESOURCE_GROUP_NAME: $RESOURCE_GROUP_NAME"
    exit 1
fi

echo "Using Cosmos DB account: $COSMOS_ACCOUNT_NAME in resource group: $RESOURCE_GROUP_NAME"

echo "Creating first admin user for MedBench..."
echo ""

# Prompt for admin user details
read -p "Enter admin email: " ADMIN_EMAIL
while [[ ! "$ADMIN_EMAIL" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; do
    echo "❌ Please enter a valid email address"
    read -p "Enter admin email: " ADMIN_EMAIL
done

# Prompt for password (hidden input)
while true; do
    echo ""
    echo "Password requirements:"
    echo "- At least 8 characters"
    echo "- Must include 3 of 4: uppercase, lowercase, number, symbol"
    echo ""
    read -s -p "Enter admin password: " ADMIN_PASSWORD
    echo
    read -s -p "Confirm admin password: " ADMIN_PASSWORD_CONFIRM
    echo
    
    if [ "$ADMIN_PASSWORD" != "$ADMIN_PASSWORD_CONFIRM" ]; then
        echo "❌ Passwords do not match. Please try again."
        continue
    fi
    
    # Enhanced password validation
    if [ ${#ADMIN_PASSWORD} -lt 8 ]; then
        echo "❌ Password must be at least 8 characters long."
        continue
    fi
    
    # Check for account name in password (case insensitive)
    ACCOUNT_NAME=$(echo "$ADMIN_EMAIL" | cut -d'@' -f1 | tr '[:upper:]' '[:lower:]')
    if echo "$ADMIN_PASSWORD" | tr '[:upper:]' '[:lower:]' | grep -q "$ACCOUNT_NAME"; then
        echo "❌ Password cannot contain the account name."
        continue
    fi
    
    # Check character categories
    categories=0
    if echo "$ADMIN_PASSWORD" | grep -q '[a-z]'; then
        categories=$((categories + 1))
    fi
    if echo "$ADMIN_PASSWORD" | grep -q '[A-Z]'; then
        categories=$((categories + 1))
    fi
    if echo "$ADMIN_PASSWORD" | grep -q '[0-9]'; then
        categories=$((categories + 1))
    fi
    if echo "$ADMIN_PASSWORD" | grep -q '[^a-zA-Z0-9]'; then
        categories=$((categories + 1))
    fi
    
    if [ $categories -lt 3 ]; then
        echo "❌ Password must include characters from at least three categories: uppercase letters, lowercase letters, numbers, and special characters."
        continue
    fi
    
    # Check against common passwords
    LOWER_PASSWORD=$(echo "$ADMIN_PASSWORD" | tr '[:upper:]' '[:lower:]')
    if echo "123456 password 12345678 qwerty 123456789 12345 1234 111111 1234567 dragon 123123 baseball abc123 football monkey letmein 696969 shadow master 666666 qwertyuiop 123321 mustang 1234567890 michael 654321 superman 1qaz2wsx 7777777 121212 000000 qazwsx 123qwe killer trustno1 jordan jennifer zxcvbnm asdfgh hunter buster soccer harley batman andrew tigger sunshine iloveyou 2000 charlie robert thomas hockey ranger daniel starwars klaster 112233 george computer michelle jessica pepper 1111 zxcvbn 555555 11111111 131313 freedom 777777 pass maggie 159753 aaaaaa ginger princess joshua cheese amanda summer love ashley 6969 nicole chelsea matthew access yankees 987654321 dallas austin thunder taylor matrix william corvette hello martin heather secret merlin diamond 1234qwer hammer silver 222222 88888888 anthony justin test bailey q1w2e3r4t5 patrick internet scooter orange 11111 golfer cookie richard samantha bigdog guitar jackson whatever mickey chicken sparky snoopy maverick phoenix camaro peanut morgan welcome falcon cowboy ferrari samsung andrea smokey steelers joseph mercedes dakota arsenal eagles melissa boomer spider nascar monster tigers yellow xxxxxx 123123123 gateway marina diablo bulldog qwer1234 compaq purple hardcore banana junior" | grep -q "\\b$LOWER_PASSWORD\\b"; then
        echo "❌ This password is too common and easily guessable. Please choose a different password."
        continue
    fi
    
    break
done

read -p "Enter admin full name: " ADMIN_NAME
if [ -z "$ADMIN_NAME" ]; then
    ADMIN_NAME="Administrator"
fi

echo ""
echo "Creating admin user with:"
echo "Email: $ADMIN_EMAIL"
echo "Name: $ADMIN_NAME"
echo ""

# Function to hash password using PBKDF2 (matching PowerShell implementation)
hash_password() {
    local password="$1"
    local salt_base64=$(openssl rand -base64 16)
    
    # Use Node.js to generate PBKDF2 hash for consistency
    if command -v node >/dev/null 2>&1; then
        local result=$(node -e "
        const crypto = require('crypto');
        const password = '$password';
        const saltBase64 = '$salt_base64';
        const salt = Buffer.from(saltBase64, 'base64');
        const hash = crypto.pbkdf2Sync(password, salt, 100000, 32, 'sha256');
        const hashBase64 = hash.toString('base64');
        console.log(hashBase64 + ':' + saltBase64);
        ")
        echo "$result"
    else
        echo "❌ Node.js required for PBKDF2 password hashing"
        exit 1
    fi
}

# Function to validate password complexity
validate_password() {
    local password="$1"
    local length=${#password}
    local categories=0
    
    if [ $length -lt 8 ]; then
        echo "❌ Password must be at least 8 characters long."
        return 1
    fi
    
    # Check for lowercase
    if [[ "$password" =~ [a-z] ]]; then
        ((categories++))
    fi
    
    # Check for uppercase
    if [[ "$password" =~ [A-Z] ]]; then
        ((categories++))
    fi
    
    # Check for digits
    if [[ "$password" =~ [0-9] ]]; then
        ((categories++))
    fi
    
    # Check for special characters
    if [[ "$password" =~ [^a-zA-Z0-9] ]]; then
        ((categories++))
    fi
    
    if [ $categories -lt 3 ]; then
        echo "❌ Password must include 3 of 4: upper, lower, number, symbol."
        return 1
    fi
    
    return 0
}

# Validate password
if ! validate_password "$ADMIN_PASSWORD"; then
    exit 1
fi

# Generate user ID and hash password
USER_ID=$(date +%s%3N)  # Generate timestamp-based ID like the working example
PASSWORD_HASH_SALT=$(hash_password "$ADMIN_PASSWORD")
PASSWORD_HASH=${PASSWORD_HASH_SALT%:*}
PASSWORD_SALT=${PASSWORD_HASH_SALT#*:}
CURRENT_DATE=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")

# Get connection string for MongoDB operations
echo "Getting Cosmos DB connection string..."
COSMOS_CONNECTION_STRING=$(az cosmosdb keys list \
    --name "$COSMOS_ACCOUNT_NAME" \
    --resource-group "$RESOURCE_GROUP_NAME" \
    --type connection-strings \
    --query "connectionStrings[?description=='Primary MongoDB Connection String'].connectionString | [0]" \
    --output tsv 2>/dev/null || true)

# If that fails, try the alternative method
if [ -z "$COSMOS_CONNECTION_STRING" ]; then
    echo "Trying alternative connection string retrieval..."
    COSMOS_CONNECTION_STRING=$(az cosmosdb show-connection-string \
        --name "$COSMOS_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --type mongodb \
        --query "connectionString" \
        --output tsv 2>/dev/null || true)
fi

# If still no connection string, try getting it from Key Vault
if [ -z "$COSMOS_CONNECTION_STRING" ]; then
    echo "Trying to get connection string from Key Vault..."
    KEY_VAULT_NAME=$(azd env get-value AZURE_KEY_VAULT_NAME 2>/dev/null || echo "")
    if [ -n "$KEY_VAULT_NAME" ]; then
        COSMOS_CONNECTION_STRING=$(az keyvault secret show \
            --vault-name "$KEY_VAULT_NAME" \
            --name "cosmos-connection-string" \
            --query value \
            --output tsv 2>/dev/null || true)
    fi
fi

if [ -z "$COSMOS_CONNECTION_STRING" ]; then
    echo "❌ Failed to get Cosmos DB connection string"
    echo "Please ensure:"
    echo "1. The Cosmos DB account exists and is accessible"
    echo "2. You have proper permissions on the resource group"
    echo "3. The account is configured for MongoDB API"
    exit 1
fi

echo "✅ Connection string retrieved successfully"

# Check if user already exists using MongoDB query
echo "Checking if user already exists..."
if command -v mongosh >/dev/null 2>&1; then
    # Use mongosh - properly quote the connection string
    EXISTING_USER=$(mongosh "$COSMOS_CONNECTION_STRING" --quiet --eval "db = db.getSiblingDB('HAIMEDB'); db.Users.findOne({Email: '$(echo "$ADMIN_EMAIL" | tr '[:upper:]' '[:lower:]')'})" 2>/dev/null || echo "null")
    echo "User check result: $EXISTING_USER"
else
    # Fallback: assume user doesn't exist
    EXISTING_USER="null"
    echo "mongosh not available, assuming user doesn't exist"
fi

if [ "$EXISTING_USER" != "null" ] && [ "$EXISTING_USER" != "" ] && [ "$EXISTING_USER" != "null " ]; then
    echo "✅ User with email $ADMIN_EMAIL already exists. Skipping creation."
    echo ""
    echo "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
    azd env set CREATE_ADMIN_USER false
    exit 0
fi

# Create admin user document
echo "Creating admin user..."
if command -v mongosh >/dev/null 2>&1; then
    # Use mongosh to insert document - use single quotes to avoid variable expansion issues
    echo "Inserting user document with mongosh..."
    mongosh "$COSMOS_CONNECTION_STRING" --quiet --eval "
        db = db.getSiblingDB('HAIMEDB');
        var result = db.Users.insertOne({
            _id: '$USER_ID',
            Name: '$ADMIN_NAME',
            Email: '$(echo "$ADMIN_EMAIL" | tr '[:upper:]' '[:lower:]')',
            Roles: ['admin'],
            Expertise: null,
            CreatedAt: new Date('$CURRENT_DATE'),
            UpdatedAt: null,
            IsModelReviewer: false,
            ModelId: null,
            Stats: {},
            PasswordHash: '$PASSWORD_HASH',
            PasswordSalt: '$PASSWORD_SALT',
            PasswordResetToken: null,
            PasswordResetExpires: null
        });
        print('✅ Successfully created admin user: $ADMIN_EMAIL');
        print('Insert result: ' + JSON.stringify(result));
    " 2>/dev/null || {
        echo "❌ Failed to create user with mongosh"
        exit 1
    }
else
    echo "❌ MongoDB shell (mongosh) not found. Please install mongosh to create the admin user."
    echo "Install with: brew install mongosh"
    exit 1
fi

echo ""
echo "✅ Admin user creation completed!"
echo ""
echo "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
azd env set CREATE_ADMIN_USER false