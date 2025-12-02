#!/bin/bash
set -euo pipefail

echo "Setting up first admin user..."

# Check if we should create admin user
CREATE_ADMIN_USER=$(azd env get-value CREATE_ADMIN_USER 2>/dev/null || echo "true")
if [ "${CREATE_ADMIN_USER,,}" != "true" ]; then
    echo "CREATE_ADMIN_USER is set to '${CREATE_ADMIN_USER}', skipping admin user creation"
    exit 0
fi

# Get environment variables
KEY_VAULT_NAME=$(azd env get-value AZURE_KEY_VAULT_NAME 2>/dev/null || echo "")
if [ -z "$KEY_VAULT_NAME" ]; then
    echo "❌ AZURE_KEY_VAULT_NAME not found in environment"
    exit 1
fi

echo "Getting Cosmos connection string from Key Vault..."
COSMOS_CONNECTION_STRING=$(az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "cosmos-connection-string" --query value -o tsv 2>/dev/null || echo "")
if [ -z "$COSMOS_CONNECTION_STRING" ]; then
    echo "❌ COSMOS_CONNECTION_STRING not found in environment"
    exit 1
fi

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
    
    # Basic password validation
    if [ ${#ADMIN_PASSWORD} -lt 8 ]; then
        echo "❌ Password must be at least 8 characters long."
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

# Create a temporary Node.js script to create the user
cat > /tmp/create-admin-user.js << 'EOF'
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
EOF

# Set environment variables for the Node.js script
export COSMOS_CONNECTION_STRING="$COSMOS_CONNECTION_STRING"
export ADMIN_EMAIL="$ADMIN_EMAIL"
export ADMIN_PASSWORD="$ADMIN_PASSWORD"
export ADMIN_NAME="$ADMIN_NAME"

# Run the Node.js script
if command -v node >/dev/null 2>&1; then
    # Install mongodb driver if needed
    if [ ! -d "node_modules/mongodb" ]; then
        echo "Installing MongoDB driver..."
        npm init -y >/dev/null 2>&1 || true
        npm install mongodb >/dev/null 2>&1
    fi
    
    node /tmp/create-admin-user.js
    
    # Clean up
    rm -f /tmp/create-admin-user.js
    
    echo ""
    echo "✅ Admin user creation completed!"
    echo ""
    echo "Setting CREATE_ADMIN_USER=false to prevent prompting on future deployments"
    azd env set CREATE_ADMIN_USER false
    
else
    echo "❌ Node.js not found. Cannot create admin user."
    echo "Please install Node.js or create the admin user manually after deployment."
    exit 1
fi