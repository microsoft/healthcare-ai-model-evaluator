#!/usr/bin/env bash
set -euo pipefail

# One-shot setup for haime-vpn-clean: VNet + subnets + azd env vars + P2S gateway.
# Edit the variables below as needed before running.

SUBSCRIPTION_ID="2138550f-216e-49f3-ac79-87f8c4604d89"
RESOURCE_GROUP="haimhaime-vpn-clean-2-2"
VNET_NAME="haime-vnet"
VPN_GATEWAY_NAME="haime-vpn"

# Address spaces (adjust if they overlap with on-prem or other VNets)
VNET_ADDRESS_SPACE="10.30.0.0/16"
ACA_SUBNET_PREFIX="10.30.0.0/23"
FUNCTIONS_SUBNET_PREFIX="10.30.2.0/24"
DNS_RESOLVER_SUBNET_PREFIX="10.30.3.0/27"
PRIVATE_ENDPOINT_SUBNET_PREFIX="10.30.4.0/27"
GATEWAY_SUBNET_PREFIX="10.30.255.0/27"
VPN_GATEWAY_SKU="VpnGw1AZ"

# Optional: azd environment name (must already exist)
AZD_ENV_NAME=""

log() { printf '%s\n' "$*"; }

if ! command -v az >/dev/null 2>&1; then
  log "ERROR: Azure CLI (az) is required."
  exit 1
fi

if ! command -v azd >/dev/null 2>&1; then
  log "ERROR: Azure Developer CLI (azd) is required."
  exit 1
fi

log "Setting subscription: $SUBSCRIPTION_ID"
az account set --subscription "$SUBSCRIPTION_ID"

RG_LOCATION="$(az group show -g "$RESOURCE_GROUP" --query location -o tsv 2>/dev/null || true)"
if [[ -z "$RG_LOCATION" ]]; then
  log "ERROR: Resource group $RESOURCE_GROUP not found. Create it first or update RESOURCE_GROUP."
  exit 1
fi
log "Resource group location: $RG_LOCATION"

# Create VNet if missing
if ! az network vnet show -g "$RESOURCE_GROUP" -n "$VNET_NAME" >/dev/null 2>&1; then
  log "Creating VNet $VNET_NAME..."
  az network vnet create \
    -g "$RESOURCE_GROUP" \
    -n "$VNET_NAME" \
    --location "$RG_LOCATION" \
    --address-prefixes "$VNET_ADDRESS_SPACE" \
    >/dev/null
else
  log "VNet already exists: $VNET_NAME"
fi

# Subnets
log "Creating/ensuring subnets..."

az network vnet subnet create \
  -g "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  -n aca-infra \
  --address-prefixes "$ACA_SUBNET_PREFIX" \
  --delegations Microsoft.App/environments \
  >/dev/null

az network vnet subnet create \
  -g "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  -n functions-integration \
  --address-prefixes "$FUNCTIONS_SUBNET_PREFIX" \
  --delegations Microsoft.Web/serverFarms \
  >/dev/null

az network vnet subnet create \
  -g "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  -n private-endpoints \
  --address-prefixes "$PRIVATE_ENDPOINT_SUBNET_PREFIX" \
  --private-endpoint-network-policies Disabled \
  >/dev/null

az network vnet subnet create \
  -g "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  -n dns-resolver-inbound \
  --address-prefixes "$DNS_RESOLVER_SUBNET_PREFIX" \
  --delegations Microsoft.Network/dnsResolvers \
  >/dev/null

az network vnet subnet create \
  -g "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  -n GatewaySubnet \
  --address-prefixes "$GATEWAY_SUBNET_PREFIX" \
  >/dev/null

# Resolve subnet IDs
VNET_ID="$(az network vnet show -g "$RESOURCE_GROUP" -n "$VNET_NAME" --query id -o tsv)"
ACA_SUBNET_ID="$(az network vnet subnet show -g "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" -n aca-infra --query id -o tsv)"
FUNCTIONS_SUBNET_ID="$(az network vnet subnet show -g "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" -n functions-integration --query id -o tsv)"
PE_SUBNET_ID="$(az network vnet subnet show -g "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" -n private-endpoints --query id -o tsv)"

log "VNet ID: $VNET_ID"
log "ACA subnet ID: $ACA_SUBNET_ID"
log "Functions subnet ID: $FUNCTIONS_SUBNET_ID"
log "Private endpoints subnet ID: $PE_SUBNET_ID"

# azd env vars
if [[ -n "$AZD_ENV_NAME" ]]; then
  log "Selecting azd env: $AZD_ENV_NAME"
  azd env select "$AZD_ENV_NAME"
fi

log "Setting azd env vars..."
azd env set DEPLOYMENT_NETWORKING private
azd env set CREATE_VNET false
azd env set EXISTING_VNET_RESOURCE_ID "$VNET_ID"
azd env set EXISTING_ACA_INFRASTRUCTURE_SUBNET_ID "$ACA_SUBNET_ID"
azd env set EXISTING_FUNCTIONS_INTEGRATION_SUBNET_ID "$FUNCTIONS_SUBNET_ID"
azd env set CREATE_PRIVATE_ENDPOINT true
azd env set EXISTING_PRIVATE_ENDPOINT_SUBNET_ID "$PE_SUBNET_ID"

# Create/configure P2S VPN gateway
log "Creating/configuring P2S VPN gateway ($VPN_GATEWAY_NAME)..."
./infra/scripts/create-p2s-vpn-gateway.sh \
  --resource-group "$RESOURCE_GROUP" \
  --vnet-name "$VNET_NAME" \
  --gateway-name "$VPN_GATEWAY_NAME" \
  --public-ip-name "pip-$VPN_GATEWAY_NAME" \
  --gateway-subnet-prefix "$GATEWAY_SUBNET_PREFIX" \
  --sku "$VPN_GATEWAY_SKU"

log "Done. Next: run 'azd provision' in this repo."