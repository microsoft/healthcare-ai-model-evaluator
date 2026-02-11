#!/bin/bash

# Pre-provision hook to configure deployment networking mode.
# - open: public ingress (default)
# - private: internal-only Container Apps Environment + VNet integration for Functions
set -euo pipefail

is_interactive() {
    # azd hooks often run with stdin not attached to a TTY.
    # Use /dev/tty so prompts still work when a terminal is available.
    [[ -z "${CI:-}" ]] && [[ -z "${GITHUB_ACTIONS:-}" ]] && [[ -r /dev/tty ]]
}

prompt() {
    # Usage: prompt "Message" varName
    local msg="$1"
    local __outvar="$2"
    local ans=""
    if is_interactive; then
        read -r -p "$msg" ans </dev/tty || true
    fi
    printf -v "$__outvar" "%s" "$ans"
}

get_env_value() {
    local key="$1"
    local value
    value=$(azd env get-value "$key" 2>/dev/null || true)
    if [ "$value" = "ERROR:" ] || echo "$value" | grep -qi "not found"; then
        echo ""
        return 0
    fi
    echo "$value"
}

set_env_value() {
    local key="$1"
    local value="$2"
    azd env set "$key" "$value" >/dev/null
}

normalize_cidr() {
    # Prints normalized CIDR (network base/prefix), or empty if invalid.
    # Uses Python's ipaddress for correctness.
    local cidr="$1"
    if ! command -v python3 >/dev/null 2>&1; then
        echo ""
        return 0
    fi
    python3 - <<'PY' "$cidr" 2>/dev/null || true
import ipaddress
import sys

s = sys.argv[1]
try:
    net = ipaddress.ip_network(s, strict=False)
    # Only support IPv4 here.
    if net.version != 4:
        raise ValueError('IPv6 not supported')
    print(f"{net.network_address}/{net.prefixlen}")
except Exception:
    pass
PY
}

validate_create_vnet_cidrs() {
    # Validate that CIDRs are properly aligned and non-overlapping.
    local vnet="$1"
    local aca="$2"
    local func="$3"

    if ! command -v python3 >/dev/null 2>&1; then
        # Best effort: skip validation if python3 isn't available.
        return 0
    fi

    python3 - <<'PY' "$vnet" "$aca" "$func" 1>&2
import ipaddress
import sys

vnet_s, aca_s, func_s = sys.argv[1], sys.argv[2], sys.argv[3]

def net(s: str) -> ipaddress.IPv4Network:
    n = ipaddress.ip_network(s, strict=False)
    if n.version != 4:
        raise ValueError(f"Only IPv4 supported: {s}")
    return n

def is_aligned(original: str, normalized: ipaddress.IPv4Network) -> bool:
    return original.strip() == f"{normalized.network_address}/{normalized.prefixlen}"

vnet_n = net(vnet_s)
aca_n = net(aca_s)
func_n = net(func_s)

errors = []

if not is_aligned(vnet_s, vnet_n):
    errors.append(("VNET_ADDRESS_SPACE", vnet_s, f"{vnet_n.network_address}/{vnet_n.prefixlen}", "CIDR is not aligned to its prefix length"))
if not is_aligned(aca_s, aca_n):
    errors.append(("ACA_INFRASTRUCTURE_SUBNET_PREFIX", aca_s, f"{aca_n.network_address}/{aca_n.prefixlen}", "CIDR is not aligned to its prefix length"))
if not is_aligned(func_s, func_n):
    errors.append(("FUNCTIONS_INTEGRATION_SUBNET_PREFIX", func_s, f"{func_n.network_address}/{func_n.prefixlen}", "CIDR is not aligned to its prefix length"))

if not aca_n.subnet_of(vnet_n):
    errors.append(("ACA_INFRASTRUCTURE_SUBNET_PREFIX", aca_s, None, "Subnet is not within VNET_ADDRESS_SPACE"))
if not func_n.subnet_of(vnet_n):
    errors.append(("FUNCTIONS_INTEGRATION_SUBNET_PREFIX", func_s, None, "Subnet is not within VNET_ADDRESS_SPACE"))
if aca_n.overlaps(func_n):
    errors.append(("ACA/FUNCTIONS", f"{aca_s} overlaps {func_s}", None, "Subnets overlap"))

if errors:
    for key, got, suggested, reason in errors:
        if suggested:
            print(f"ERROR: {key}: {got} -> suggested: {suggested} ({reason})")
        else:
            print(f"ERROR: {key}: {got} ({reason})")
    sys.exit(2)
PY
}

DEPLOYMENT_NETWORKING=$(get_env_value DEPLOYMENT_NETWORKING)

if [ -z "$DEPLOYMENT_NETWORKING" ]; then
    # Default to private (internal-only) networking.
    DEPLOYMENT_NETWORKING="private"
    if is_interactive; then
        echo ""
        echo "Deployment networking mode:"
        echo "  1) open    - public ingress"
        echo "  2) private - internal-only (VNet) (default)"
        choice=""
        prompt "Choose [1/2] (default: 2): " choice
        if [ "$choice" = "1" ]; then
            DEPLOYMENT_NETWORKING="open"
        fi
    fi
    set_env_value DEPLOYMENT_NETWORKING "$DEPLOYMENT_NETWORKING"
fi

if [ "$DEPLOYMENT_NETWORKING" != "private" ]; then
    # Open mode: do not auto-configure IP filtering.
    exit 0
fi

CREATE_VNET=$(get_env_value CREATE_VNET)
EXISTING_VNET_RESOURCE_ID=$(get_env_value EXISTING_VNET_RESOURCE_ID)
EXISTING_ACA_INFRA_SUBNET_ID=$(get_env_value EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID)
EXISTING_FUNCTIONS_SUBNET_ID=$(get_env_value EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID)

# If we're creating a VNet, validate CIDR inputs early to prevent ARM 400s like InvalidCIDRNotation.
if [ "$CREATE_VNET" = "true" ] || [ -z "$CREATE_VNET" ]; then
    VNET_ADDRESS_SPACE=$(get_env_value VNET_ADDRESS_SPACE)
    ACA_INFRASTRUCTURE_SUBNET_PREFIX=$(get_env_value ACA_INFRASTRUCTURE_SUBNET_PREFIX)
    FUNCTIONS_INTEGRATION_SUBNET_PREFIX=$(get_env_value FUNCTIONS_INTEGRATION_SUBNET_PREFIX)

    VNET_ADDRESS_SPACE=${VNET_ADDRESS_SPACE:-10.30.0.0/16}
    ACA_INFRASTRUCTURE_SUBNET_PREFIX=${ACA_INFRASTRUCTURE_SUBNET_PREFIX:-10.30.0.0/23}
    FUNCTIONS_INTEGRATION_SUBNET_PREFIX=${FUNCTIONS_INTEGRATION_SUBNET_PREFIX:-10.30.2.0/24}

    if ! validate_create_vnet_cidrs "$VNET_ADDRESS_SPACE" "$ACA_INFRASTRUCTURE_SUBNET_PREFIX" "$FUNCTIONS_INTEGRATION_SUBNET_PREFIX"; then
        # Attempt auto-normalization for common misaligned CIDRs.
        norm_vnet=$(normalize_cidr "$VNET_ADDRESS_SPACE")
        norm_aca=$(normalize_cidr "$ACA_INFRASTRUCTURE_SUBNET_PREFIX")
        norm_func=$(normalize_cidr "$FUNCTIONS_INTEGRATION_SUBNET_PREFIX")

        if is_interactive; then
            echo ""
            echo "One or more CIDR values are invalid for Azure (not aligned to the prefix length)."
            echo "Azure requires the network base address for the prefix (e.g., 10.0.0.0/23, not 10.0.1.0/23)."
            echo ""
            echo "Current values:"
            echo "  VNET_ADDRESS_SPACE=$VNET_ADDRESS_SPACE"
            echo "  ACA_INFRASTRUCTURE_SUBNET_PREFIX=$ACA_INFRASTRUCTURE_SUBNET_PREFIX"
            echo "  FUNCTIONS_INTEGRATION_SUBNET_PREFIX=$FUNCTIONS_INTEGRATION_SUBNET_PREFIX"
            echo ""
            echo "Suggested normalized values:"
            [ -n "$norm_vnet" ] && echo "  VNET_ADDRESS_SPACE=$norm_vnet"
            [ -n "$norm_aca" ] && echo "  ACA_INFRASTRUCTURE_SUBNET_PREFIX=$norm_aca"
            [ -n "$norm_func" ] && echo "  FUNCTIONS_INTEGRATION_SUBNET_PREFIX=$norm_func"
            echo ""
            fix=""
            prompt "Apply suggested values to azd env? [Y/n]: " fix
            if [ -z "$fix" ] || [ "$fix" = "Y" ] || [ "$fix" = "y" ] || [ "$fix" = "yes" ]; then
                [ -n "$norm_vnet" ] && set_env_value VNET_ADDRESS_SPACE "$norm_vnet"
                [ -n "$norm_aca" ] && set_env_value ACA_INFRASTRUCTURE_SUBNET_PREFIX "$norm_aca"
                [ -n "$norm_func" ] && set_env_value FUNCTIONS_INTEGRATION_SUBNET_PREFIX "$norm_func"
            else
                exit 1
            fi
        else
            echo "Invalid CIDR values detected for createVnet=true. Fix and re-run." 1>&2
            [ -n "$norm_aca" ] && echo "For ACA infra subnet, try: azd env set ACA_INFRASTRUCTURE_SUBNET_PREFIX $norm_aca" 1>&2
            exit 1
        fi
    fi
fi

if [ "$CREATE_VNET" = "true" ]; then
    # Already configured to create a VNet.
    exit 0
fi

if [ -n "$EXISTING_ACA_INFRA_SUBNET_ID" ] && [ -n "$EXISTING_FUNCTIONS_SUBNET_ID" ]; then
    # Existing subnet IDs already configured.
    exit 0
fi

if ! is_interactive; then
    echo "DEPLOYMENT_NETWORKING=private but VNet settings are not configured."
    echo "Set either CREATE_VNET=true to create a new VNet, or provide existing subnet IDs:"
    echo "  azd env set CREATE_VNET true"
    echo "  -or-"
    echo "  azd env set CREATE_VNET false"
    echo "  azd env set EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID <subnetResourceId>"
    echo "  azd env set EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID <subnetResourceId>"
    exit 1
fi

echo ""
echo "Private networking selected. Configure VNet:"
create_choice=""
prompt "Create a new VNet (recommended)? [Y/n]: " create_choice
if [ -z "$create_choice" ] || [ "$create_choice" = "Y" ] || [ "$create_choice" = "y" ] || [ "$create_choice" = "yes" ]; then
    set_env_value CREATE_VNET true
    set_env_value EXISTING_VNET_RESOURCE_ID ""
    set_env_value EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID ""
    set_env_value EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID ""
    exit 0
fi

set_env_value CREATE_VNET false

if ! command -v az >/dev/null 2>&1; then
    echo "Azure CLI (az) is required to query existing VNets."
    echo "Either install az, or set subnet IDs manually with azd env set."
    exit 1
fi

echo ""
echo "Fetching VNets in the current Azure subscription..."
mapfile -t vnets < <(az network vnet list --query "[].{name:name,rg:resourceGroup,id:id}" -o tsv 2>/dev/null || true)
if [ ${#vnets[@]} -eq 0 ]; then
    echo "No VNets found. Switch to CREATE_VNET=true or create one first."
    exit 1
fi

echo ""
echo "Select a VNet:"
for i in "${!vnets[@]}"; do
    IFS=$'\t' read -r vnetName vnetRg vnetId <<< "${vnets[$i]}"
    printf "  %d) %s (rg: %s)\n" $((i+1)) "$vnetName" "$vnetRg"
done

vnetIndex=""
prompt "Enter number: " vnetIndex
if ! echo "$vnetIndex" | grep -Eq '^[0-9]+$'; then
    echo "Invalid selection."
    exit 1
fi
vnetIndex=$((vnetIndex-1))
if [ $vnetIndex -lt 0 ] || [ $vnetIndex -ge ${#vnets[@]} ]; then
    echo "Invalid selection."
    exit 1
fi

IFS=$'\t' read -r selectedVnetName selectedVnetRg selectedVnetId <<< "${vnets[$vnetIndex]}"
set_env_value EXISTING_VNET_RESOURCE_ID "$selectedVnetId"

echo ""
echo "Fetching subnets for $selectedVnetName..."
mapfile -t subnets < <(az network vnet subnet list -g "$selectedVnetRg" --vnet-name "$selectedVnetName" --query "[].{name:name,id:id}" -o tsv 2>/dev/null || true)
if [ ${#subnets[@]} -eq 0 ]; then
    echo "No subnets found in selected VNet."
    exit 1
fi

echo ""
echo "Select subnet for Container Apps Environment infrastructure (must be dedicated and delegated to Microsoft.App/environments):"
for i in "${!subnets[@]}"; do
    IFS=$'\t' read -r subnetName subnetId <<< "${subnets[$i]}"
    printf "  %d) %s\n" $((i+1)) "$subnetName"
done
acaIndex=""
prompt "Enter number: " acaIndex
if ! echo "$acaIndex" | grep -Eq '^[0-9]+$'; then
    echo "Invalid selection."
    exit 1
fi
acaIndex=$((acaIndex-1))
if [ $acaIndex -lt 0 ] || [ $acaIndex -ge ${#subnets[@]} ]; then
    echo "Invalid selection."
    exit 1
fi
IFS=$'\t' read -r acaSubnetName acaSubnetId <<< "${subnets[$acaIndex]}"
set_env_value EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID "$acaSubnetId"

echo ""
echo "Select subnet for Azure Functions VNet integration (delegated to Microsoft.Web/serverFarms):"
for i in "${!subnets[@]}"; do
    IFS=$'\t' read -r subnetName subnetId <<< "${subnets[$i]}"
    printf "  %d) %s\n" $((i+1)) "$subnetName"
done
funcIndex=""
prompt "Enter number: " funcIndex
if ! echo "$funcIndex" | grep -Eq '^[0-9]+$'; then
    echo "Invalid selection."
    exit 1
fi
funcIndex=$((funcIndex-1))
if [ $funcIndex -lt 0 ] || [ $funcIndex -ge ${#subnets[@]} ]; then
    echo "Invalid selection."
    exit 1
fi
IFS=$'\t' read -r funcSubnetName funcSubnetId <<< "${subnets[$funcIndex]}"
set_env_value EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID "$funcSubnetId"

echo ""
echo "Configured private networking."