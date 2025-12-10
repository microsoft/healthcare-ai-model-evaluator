#!/bin/bash

# Pre-provision hook to set up IP filtering for web application security
set -e

echo ""
echo "🔒 Setting up IP filtering for web application security..."
echo ""

# Function to get current public IP
get_current_ip() {
    # Try multiple services for reliability
    local ip=""
    
    # Try ipify first (most reliable)
    if command -v curl >/dev/null 2>&1; then
        ip=$(curl -s --connect-timeout 5 https://api.ipify.org 2>/dev/null || echo "")
    fi
    
    # Fallback to ifconfig.me
    if [ -z "$ip" ] && command -v curl >/dev/null 2>&1; then
        ip=$(curl -s --connect-timeout 5 https://ifconfig.me 2>/dev/null || echo "")
    fi
    
    # Fallback to httpbin
    if [ -z "$ip" ] && command -v curl >/dev/null 2>&1; then
        ip=$(curl -s --connect-timeout 5 https://httpbin.org/ip | grep -o '[0-9.]*' 2>/dev/null || echo "")
    fi
    
    # Final fallback using dig (if available)
    if [ -z "$ip" ] && command -v dig >/dev/null 2>&1; then
        ip=$(dig +short myip.opendns.com @resolver1.opendns.com 2>/dev/null || echo "")
    fi
    
    echo "$ip"
}

# Get current IP
CURRENT_IP=$(get_current_ip)

# Check if IP filtering is already configured
EXISTING_ENABLED=$(azd env get-value ENABLE_WEB_IP_FILTERING 2>&1 | grep -v "not found" || echo "")
EXISTING_IP=$(azd env get-value ALLOWED_WEB_IP 2>&1 | grep -v "not found" || echo "")

if [ -n "$EXISTING_ENABLED" ] && [ -n "$EXISTING_IP" ] && [ "$EXISTING_ENABLED" != "ERROR:" ] && [ "$EXISTING_IP" != "ERROR:" ]; then
    echo "✅ Web IP filtering already configured:"
    echo "   Enabled: $EXISTING_ENABLED"
    echo "   Allowed IP: $EXISTING_IP"
    echo ""
    exit 0
fi

echo "🌐 For security, the web application will be protected by IP filtering."
echo "   Only specified IP addresses will be able to access the API."
echo ""

if [ -n "$CURRENT_IP" ]; then
    echo "📍 Your current public IP address is: $CURRENT_IP"
    echo ""
    
    # Check if running in non-interactive mode (like in azd hooks)
    if [ ! -t 0 ] || [ -n "$CI" ] || [ -n "$GITHUB_ACTIONS" ]; then
        echo "🤖 Non-interactive environment detected. Auto-configuring with detected IP..."
        INPUT_IP="$CURRENT_IP/32"
        ENABLE_FILTERING="true"
    else
        # Interactive mode - prompt for user input
        read -p "Enter IP address(es) to allow (CIDR format, comma-separated, e.g., $CURRENT_IP/32) [default: $CURRENT_IP/32]: " INPUT_IP
        
        if [ -z "$INPUT_IP" ]; then
            INPUT_IP="$CURRENT_IP/32"
        fi
        
        # Validate CIDR format for each IP
        IFS=',' read -ra IP_ARRAY <<< "$INPUT_IP"
        for ip in "${IP_ARRAY[@]}"; do
            ip=$(echo "$ip" | xargs)  # trim whitespace
            if ! echo "$ip" | grep -E '^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$' >/dev/null; then
                echo "❌ Invalid CIDR format: $ip. Please use format like 203.0.113.1/32"
                exit 1
            fi
        done
        
        # Ask if user wants to enable IP filtering
        echo ""
        read -p "Enable IP filtering? This will block all other IPs [Y/n]: " ENABLE_FILTERING
        
        if [ -z "$ENABLE_FILTERING" ] || [ "$ENABLE_FILTERING" = "Y" ] || [ "$ENABLE_FILTERING" = "y" ] || [ "$ENABLE_FILTERING" = "yes" ]; then
            ENABLE_FILTERING="true"
        else
            ENABLE_FILTERING="false"
        fi
    fi
else
    echo "⚠️  Could not auto-detect your current IP address."
    
    # Check if non-interactive
    if [ ! -t 0 ] || [ -n "$CI" ] || [ -n "$GITHUB_ACTIONS" ]; then
        echo "❌ Non-interactive environment and no IP detected. Disabling IP filtering for initial deployment."
        echo "   You can enable it later with: azd env set ENABLE_WEB_IP_FILTERING true"
        echo "   And set your IP with: azd env set ALLOWED_WEB_IP your.ip.address/32"
        INPUT_IP=""
        ENABLE_FILTERING="false"
    else
        # Interactive mode - require user input
        echo "   Please enter your IP address manually."
        echo ""
        
        read -p "Enter IP address(es) to allow (CIDR format, comma-separated, e.g., 203.0.113.1/32,198.51.100.0/24): " INPUT_IP
        
        while [ -z "$INPUT_IP" ]; do
            echo "❌ IP address is required for security."
            read -p "Enter IP address(es) to allow (CIDR format, comma-separated, e.g., 203.0.113.1/32): " INPUT_IP
        done
        
        # Validate CIDR format for each IP
        IFS=',' read -ra IP_ARRAY <<< "$INPUT_IP"
        for ip in "${IP_ARRAY[@]}"; do
            ip=$(echo "$ip" | xargs)  # trim whitespace
            if ! echo "$ip" | grep -E '^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$' >/dev/null; then
                echo "❌ Invalid CIDR format: $ip. Please use format like 203.0.113.1/32"
                exit 1
            fi
        done
        
        ENABLE_FILTERING="true"
    fi
fi

# Set environment variables
echo ""
echo "📝 Setting azd environment variables..."
azd env set ENABLE_WEB_IP_FILTERING "$ENABLE_FILTERING"
azd env set ALLOWED_WEB_IP "$INPUT_IP"

echo ""
echo "✅ Web application security configured:"
echo "   IP Filtering Enabled: $ENABLE_FILTERING"
echo "   Allowed IP: $INPUT_IP"
echo ""
echo "💡 You can change these settings later with:"
echo "   azd env set ENABLE_WEB_IP_FILTERING true|false"
echo "   azd env set ALLOWED_WEB_IP your.ip.address/32"
echo ""