#!/bin/bash
# Pre-deployment hook to detect and set allowed IP address

echo "🌐 Detecting current machine's public IP address..."

# Function to get public IP
get_public_ip() {
    # Try multiple services in case one is down
    local ip=""
    
    # Try ipify.org first (simple and reliable)
    ip=$(curl -s --max-time 5 https://api.ipify.org/ 2>/dev/null)
    if [[ $ip =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
        echo "$ip"
        return 0
    fi
    
    # Try icanhazip.com as backup
    ip=$(curl -s --max-time 5 https://icanhazip.com/ | tr -d '\n' 2>/dev/null)
    if [[ $ip =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
        echo "$ip"
        return 0
    fi
    
    # Try httpbin.org as another backup
    ip=$(curl -s --max-time 5 https://httpbin.org/ip | python3 -c "import sys, json; print(json.load(sys.stdin)['origin'])" 2>/dev/null)
    if [[ $ip =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
        echo "$ip"
        return 0
    fi
    
    return 1
}

# Get current IP from azd env or detect it
CURRENT_IP=$(azd env get-value allowedIpAddress 2>/dev/null || echo "")

if [ -z "$CURRENT_IP" ]; then
    echo "No IP address configured. Detecting current public IP..."
    DETECTED_IP=$(get_public_ip)
    
    if [ -n "$DETECTED_IP" ]; then
        echo "✅ Detected public IP: $DETECTED_IP"
        read -p "Use this IP address for Azure resource access? [Y/n]: " -n 1 -r
        echo
        
        if [[ $REPLY =~ ^[Nn]$ ]]; then
            read -p "Enter a different IP address (or press Enter to allow all): " CUSTOM_IP
            if [ -n "$CUSTOM_IP" ]; then
                azd env set allowedIpAddress "$CUSTOM_IP"
                echo "✅ Set allowed IP address to: $CUSTOM_IP"
            else
                echo "⚠️  Warning: Deploying with open access (no IP restrictions)"
            fi
        else
            azd env set allowedIpAddress "$DETECTED_IP"
            echo "✅ Set allowed IP address to: $DETECTED_IP"
        fi
    else
        echo "❌ Could not detect public IP address automatically"
        read -p "Enter your public IP address (or press Enter to allow all): " MANUAL_IP
        if [ -n "$MANUAL_IP" ]; then
            azd env set allowedIpAddress "$MANUAL_IP"
            echo "✅ Set allowed IP address to: $MANUAL_IP"
        else
            echo "⚠️  Warning: Deploying with open access (no IP restrictions)"
        fi
    fi
else
    echo "✅ Using configured IP address: $CURRENT_IP"
    read -p "Update IP address? [y/N]: " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        DETECTED_IP=$(get_public_ip)
        if [ -n "$DETECTED_IP" ]; then
            echo "Detected current IP: $DETECTED_IP"
            read -p "Use detected IP [$DETECTED_IP] or enter custom IP: " NEW_IP
            if [ -z "$NEW_IP" ]; then
                NEW_IP="$DETECTED_IP"
            fi
            azd env set allowedIpAddress "$NEW_IP"
            echo "✅ Updated allowed IP address to: $NEW_IP"
        else
            read -p "Enter new IP address: " NEW_IP
            if [ -n "$NEW_IP" ]; then
                azd env set allowedIpAddress "$NEW_IP"
                echo "✅ Updated allowed IP address to: $NEW_IP"
            fi
        fi
    fi
fi

echo "🚀 Proceeding with deployment..."