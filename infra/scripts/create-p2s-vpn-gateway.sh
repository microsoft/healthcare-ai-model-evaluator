#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Create/Configure an Azure VPN Gateway (Point-to-Site) for a MedBench private VNet.

This script is intended for "developer/admin laptop" access to a private deployment
(DEPLOYMENT_NETWORKING=private) via OpenVPN + Microsoft Entra ID (Azure VPN Client sign-in).

Prereqs:
- Azure CLI installed and logged in (az login)
- azd environment set (optional, used to auto-discover RG)

Usage:
  ./infra/scripts/create-p2s-vpn-gateway.sh [options]

Options:
  --resource-group <rg>         Azure resource group containing the VNet (default: from azd env)
  --vnet-name <name>            VNet name (default: first VNet in the RG)
  --gateway-name <name>         VPN gateway name (default: vpngw-<vnetName>)
  --public-ip-name <name>       Public IP name (default: pip-<gatewayName>)

  --gateway-subnet-prefix <cidr>  CIDR for GatewaySubnet (default: auto-pick a free /27 inside the VNet)
  --vpn-client-pool <cidr>        Client address pool (default: 172.16.200.0/24)
  --sku <sku>                     Gateway SKU (default: VpnGw1)

  --no-wait                       Create gateway and exit without waiting/configuring P2S
  --skip-p2s-config               Skip P2S configuration step (you can do it in Portal later)

  --p2s-dns-servers <ip,ip>       Optional DNS server IPs (for guidance output only).
                                 This script no longer attempts to set VPN-client DNS via ARM because
                                 `Microsoft.Network/virtualNetworkGateways` does not expose a stable
                                 `vpnClientDnsServers` property in current schemas.

  --aad-tenant-uri <uri>          Override AAD tenant URI (default: https://login.microsoftonline.com/<tenantId>)
  --aad-audience <guid>           Override AAD audience (default: Azure Public: 41b23e61-6c1e-4545-b367-cd054e0ed4b4)
  --aad-issuer <uri>              Override AAD issuer (default: https://sts.windows.net/<tenantId>/)

Examples:
  # Typical: infer RG + VNet, create everything, wait, configure P2S
  ./infra/scripts/create-p2s-vpn-gateway.sh

  # Specify the VNet explicitly
  ./infra/scripts/create-p2s-vpn-gateway.sh --resource-group rg-haimesec3 --vnet-name vnet-osy

  # Use a specific GatewaySubnet prefix
  ./infra/scripts/create-p2s-vpn-gateway.sh --gateway-subnet-prefix 10.30.255.0/27
EOF
}

log() { printf '%s\n' "$*"; }
err() { printf '%s\n' "$*" >&2; }

die() {
  err "ERROR: $*"
  err
  usage
  exit 1
}

RG=""
VNET_NAME=""
GW_NAME=""
PIP_NAME=""
GATEWAY_SUBNET_PREFIX=""
VPN_CLIENT_POOL="172.16.200.0/24"
SKU="VpnGw1"
NO_WAIT="false"
SKIP_P2S_CONFIG="false"

P2S_DNS_SERVERS=""  # comma-separated IPs

AUTH_TYPE="aad"  # aad|cert

ROOT_CERT_FILE=""
ROOT_CERT_NAME="MedBench-P2S-Root"

AAD_TENANT_URI=""
AAD_AUDIENCE=""
AAD_ISSUER=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --resource-group)
      RG="${2:-}"; shift 2 ;;
    --vnet-name)
      VNET_NAME="${2:-}"; shift 2 ;;
    --gateway-name)
      GW_NAME="${2:-}"; shift 2 ;;
    --public-ip-name)
      PIP_NAME="${2:-}"; shift 2 ;;
    --gateway-subnet-prefix)
      GATEWAY_SUBNET_PREFIX="${2:-}"; shift 2 ;;
    --vpn-client-pool)
      VPN_CLIENT_POOL="${2:-}"; shift 2 ;;
    --sku)
      SKU="${2:-}"; shift 2 ;;
    --no-wait)
      NO_WAIT="true"; shift ;;
    --skip-p2s-config)
      SKIP_P2S_CONFIG="true"; shift ;;
    --p2s-dns-servers)
      P2S_DNS_SERVERS="${2:-}"; shift 2 ;;
    --auth-type)
      AUTH_TYPE="${2:-}"; shift 2 ;;
    --aad-tenant-uri)
      AAD_TENANT_URI="${2:-}"; shift 2 ;;
    --aad-audience)
      AAD_AUDIENCE="${2:-}"; shift 2 ;;
    --aad-issuer)
      AAD_ISSUER="${2:-}"; shift 2 ;;
    --root-cert-file)
      ROOT_CERT_FILE="${2:-}"; shift 2 ;;
    --root-cert-name)
      ROOT_CERT_NAME="${2:-}"; shift 2 ;;
    *)
      die "Unknown argument: $1" ;;
  esac
done

if [[ "$AUTH_TYPE" != "aad" && "$AUTH_TYPE" != "cert" ]]; then
  die "--auth-type must be 'aad' or 'cert' (got: $AUTH_TYPE)"
fi

# Resolve RG from azd env if not provided
if [[ -z "$RG" ]]; then
  if command -v azd >/dev/null 2>&1; then
    RG="$(azd env get-value AZURE_RESOURCE_GROUP_NAME 2>/dev/null || true)"
  fi
fi
[[ -n "$RG" ]] || die "--resource-group is required (or run from an initialized azd environment)."

# Resolve VNet
if [[ -z "$VNET_NAME" ]]; then
  VNET_NAME="$(az network vnet list -g "$RG" --query "[0].name" -o tsv 2>/dev/null || true)"
fi
[[ -n "$VNET_NAME" ]] || die "Could not determine VNet name. Provide --vnet-name explicitly."

# Resolve location (from VNet)
LOCATION="$(az network vnet show -g "$RG" -n "$VNET_NAME" --query location -o tsv)"

# Default names
if [[ -z "$GW_NAME" ]]; then
  GW_NAME="vpngw-${VNET_NAME}"
fi
if [[ -z "$PIP_NAME" ]]; then
  PIP_NAME="pip-${GW_NAME}"
fi

# Resolve tenant + AAD settings (Azure Public defaults)
TENANT_ID="${AZURE_TENANT_ID:-}"
if [[ -z "$TENANT_ID" ]]; then
  if command -v azd >/dev/null 2>&1; then
    TENANT_ID="$(azd env get-value AZURE_TENANT_ID 2>/dev/null || true)"
  fi
fi
if [[ -z "$TENANT_ID" ]]; then
  TENANT_ID="$(az account show --query tenantId -o tsv)"
fi

if [[ -z "$AAD_TENANT_URI" ]]; then
  AAD_TENANT_URI="https://login.microsoftonline.com/${TENANT_ID}"
fi
if [[ -z "$AAD_AUDIENCE" ]]; then
  # Azure VPN (Azure Public)
  AAD_AUDIENCE="41b23e61-6c1e-4545-b367-cd054e0ed4b4"
fi
if [[ -z "$AAD_ISSUER" ]]; then
  AAD_ISSUER="https://sts.windows.net/${TENANT_ID}/"
fi

log "RG=$RG"
log "VNET_NAME=$VNET_NAME"
log "LOCATION=$LOCATION"
log "GW_NAME=$GW_NAME"
log "PIP_NAME=$PIP_NAME"
log "VPN_CLIENT_POOL=$VPN_CLIENT_POOL"
log "SKU=$SKU"
log "AUTH_TYPE=$AUTH_TYPE"
log "P2S_DNS_SERVERS=${P2S_DNS_SERVERS:-<none>}"

# Ensure GatewaySubnet exists
if ! az network vnet subnet show -g "$RG" --vnet-name "$VNET_NAME" -n GatewaySubnet >/dev/null 2>&1; then
  log "GatewaySubnet not found; creating it..."

  if [[ -z "$GATEWAY_SUBNET_PREFIX" ]]; then
    # Auto-pick an available /27 inside the VNet address space that doesn't overlap existing subnets.
    # This is best-effort. If your VNet is very segmented, pass --gateway-subnet-prefix explicitly.
    VNET_PREFIXES="$(az network vnet show -g "$RG" -n "$VNET_NAME" --query "addressSpace.addressPrefixes" -o tsv | tr '\n' ' ')"
    EXISTING_SUBNET_PREFIXES="$(az network vnet subnet list -g "$RG" --vnet-name "$VNET_NAME" --query "[].addressPrefix" -o tsv | tr '\n' ' ')"

    GATEWAY_SUBNET_PREFIX="$(VNET_PREFIXES="$VNET_PREFIXES" EXISTING_SUBNET_PREFIXES="$EXISTING_SUBNET_PREFIXES" python3 - <<'PY'
import ipaddress
import os

vnet_prefixes = os.environ.get('VNET_PREFIXES', '').split()
existing_prefixes = os.environ.get('EXISTING_SUBNET_PREFIXES', '').split()
existing = [ipaddress.ip_network(p, strict=False) for p in existing_prefixes if p]

if not vnet_prefixes:
    raise SystemExit('No VNet address prefixes found')

# Prefer the first VNet address prefix.
vnet = ipaddress.ip_network(vnet_prefixes[0], strict=False)

candidates = list(vnet.subnets(new_prefix=27))
# Choose from the end (keeps it out of the way in the address space)
for c in reversed(candidates):
    if any(c.overlaps(e) for e in existing):
        continue
    print(str(c))
    break
PY
)"

    if [[ -z "$GATEWAY_SUBNET_PREFIX" ]]; then
      die "Could not auto-pick a free /27 for GatewaySubnet. Re-run with --gateway-subnet-prefix <cidr>."
    fi

    log "Auto-selected GatewaySubnet prefix: $GATEWAY_SUBNET_PREFIX"
  fi

  az network vnet subnet create \
    -g "$RG" \
    --vnet-name "$VNET_NAME" \
    -n GatewaySubnet \
    --address-prefixes "$GATEWAY_SUBNET_PREFIX" \
    >/dev/null
else
  log "GatewaySubnet already exists."
fi

# Ensure public IP exists
if ! az network public-ip show -g "$RG" -n "$PIP_NAME" >/dev/null 2>&1; then
  log "Creating public IP $PIP_NAME..."
  PIP_ZONES_ARGS=()
  if [[ "$SKU" == *"AZ" ]]; then
    # AZ VPN gateways require zone-redundant Standard public IPs.
    PIP_ZONES_ARGS=(--zone 1 2 3)
  fi
  az network public-ip create \
    -g "$RG" \
    -n "$PIP_NAME" \
    --location "$LOCATION" \
    --sku Standard \
    --allocation-method Static \
    "${PIP_ZONES_ARGS[@]}" \
    >/dev/null
else
  log "Public IP already exists: $PIP_NAME"
fi

# Ensure VPN gateway exists
if ! az network vnet-gateway show -g "$RG" -n "$GW_NAME" >/dev/null 2>&1; then
  log "Creating VPN gateway $GW_NAME (this can take 30–60 minutes)..."
  if [[ "$NO_WAIT" == "true" ]]; then
    az network vnet-gateway create \
      -g "$RG" \
      -n "$GW_NAME" \
      --location "$LOCATION" \
      --public-ip-addresses "$PIP_NAME" \
      --vnet "$VNET_NAME" \
      --gateway-type Vpn \
      --vpn-type RouteBased \
      --sku "$SKU" \
      --no-wait \
      >/dev/null

    log "Gateway create started (no-wait). Re-run without --no-wait to configure P2S once it's ready."
    exit 0
  fi

  az network vnet-gateway create \
    -g "$RG" \
    -n "$GW_NAME" \
    --location "$LOCATION" \
    --public-ip-addresses "$PIP_NAME" \
    --vnet "$VNET_NAME" \
    --gateway-type Vpn \
    --vpn-type RouteBased \
    --sku "$SKU" \
    >/dev/null
else
  log "VPN gateway already exists: $GW_NAME"
fi

if [[ "$SKIP_P2S_CONFIG" == "true" ]]; then
  log "Skipping P2S configuration. Configure it in Azure Portal: Virtual network gateway -> Point-to-site configuration."
  exit 0
fi

log "Waiting for VPN gateway provisioning to complete..."
az network vnet-gateway wait -g "$RG" -n "$GW_NAME" --created --interval 30 --timeout 7200

if [[ -z "${P2S_DNS_SERVERS:-}" ]]; then
  err "NOTE: P2S VPN connectivity does not guarantee DNS resolution for internal-only endpoints."
  err "If internal hostnames like '*.internal.*.azurecontainerapps.io' do not resolve while connected,"
  err "configure a DNS resolver inside the VNet (commonly Azure DNS Private Resolver inbound endpoint)"
  err "and then configure your client to use that resolver for the internal zone."
  err "You can re-run with --p2s-dns-servers <ip,ip> to print helper guidance."
  err ""
fi

if [[ -n "${P2S_DNS_SERVERS:-}" ]]; then
  err "NOTE: --p2s-dns-servers is informational; this script does not push DNS settings into the VPN profile."
  err "Provided DNS server IP(s): $P2S_DNS_SERVERS"
  err ""
  err "Recommended: configure split-DNS on your client so *.internal.<env>.<region>.azurecontainerapps.io"
  err "queries go to your in-VNet resolver (commonly an Azure DNS Private Resolver inbound endpoint)."
  err "See docs/private_network_access_vpn.md for macOS / Windows / Linux steps."
  err ""
fi

if [[ "$AUTH_TYPE" == "aad" ]]; then
  log "Configuring Point-to-Site (OpenVPN + Microsoft Entra ID)..."

  # Best-effort: CLI syntax varies across versions. Try the simplest form first.
  set +e
  az network vnet-gateway update \
    -g "$RG" \
    -n "$GW_NAME" \
    --address-prefixes "$VPN_CLIENT_POOL" \
    --client-protocol OpenVPN \
    --vpn-auth-type AAD \
    --aad-tenant "$AAD_TENANT_URI" \
    --aad-audience "$AAD_AUDIENCE" \
    --aad-issuer "$AAD_ISSUER" \
    >/dev/null
  rc=$?

  if [[ $rc -ne 0 ]]; then
    # Fallback: explicitly set vpnAuthenticationTypes via --set.
    az network vnet-gateway update \
      -g "$RG" \
      -n "$GW_NAME" \
      --address-prefixes "$VPN_CLIENT_POOL" \
      --client-protocol OpenVPN \
      --aad-tenant "$AAD_TENANT_URI" \
      --aad-audience "$AAD_AUDIENCE" \
      --aad-issuer "$AAD_ISSUER" \
      --set "vpnClientConfiguration.vpnAuthenticationTypes=['AAD']" \
      >/dev/null
    rc=$?
  fi
  set -e

  if [[ $rc -ne 0 ]]; then
    err ""
    err "Failed to configure P2S (AAD) via Azure CLI. This usually means your Azure CLI version"
    err "expects a different shape for vpnClientConfiguration." 
    err ""
    err "You can finish the configuration in Azure Portal:" 
    err "  Virtual network gateway -> Point-to-site configuration" 
    err ""
    err "Use these values (Azure Public cloud):"
    err "  Address pool: $VPN_CLIENT_POOL"
    err "  Tunnel type: OpenVPN (SSL)"
    err "  Authentication: Microsoft Entra ID"
    err "  Tenant: $AAD_TENANT_URI"
    err "  Audience: $AAD_AUDIENCE"
    err "  Issuer: $AAD_ISSUER"
    exit 1
  fi
else
  log "Configuring Point-to-Site (OpenVPN + Azure certificate)..."

  [[ -n "$ROOT_CERT_FILE" ]] || die "--root-cert-file is required when --auth-type cert"
  [[ -f "$ROOT_CERT_FILE" ]] || die "Root cert file not found: $ROOT_CERT_FILE"

  tmpdir="$(mktemp -d)"
  trap 'rm -rf "$tmpdir"' EXIT
  root_der="$tmpdir/root.cer"

  # Convert PEM -> DER if needed; otherwise assume DER.
  if grep -q "BEGIN CERTIFICATE" "$ROOT_CERT_FILE" 2>/dev/null; then
    openssl x509 -in "$ROOT_CERT_FILE" -outform der -out "$root_der"
  else
    cp "$ROOT_CERT_FILE" "$root_der"
  fi

  ROOT_CERT_DATA="$(base64 < "$root_der" | tr -d '\n')"

  # CLI surface varies; attempt to set auth type + root cert via --set.
  set +e
  az network vnet-gateway update \
    -g "$RG" \
    -n "$GW_NAME" \
    --address-prefixes "$VPN_CLIENT_POOL" \
    --client-protocol OpenVPN \
    --set "vpnClientConfiguration.vpnAuthenticationTypes=['Certificate']" \
    --set "vpnClientConfiguration.vpnClientRootCertificates=[{name:'$ROOT_CERT_NAME',publicCertData:'$ROOT_CERT_DATA'}]" \
    >/dev/null
  rc=$?
  set -e

  if [[ $rc -ne 0 ]]; then
    err ""
    err "Failed to configure P2S (Azure certificate) via Azure CLI. Finish in Portal instead:"
    err "  Virtual network gateway -> Point-to-site configuration"
    err ""
    err "Set:"
    err "  Address pool: $VPN_CLIENT_POOL"
    err "  Tunnel type: OpenVPN (SSL)"
    err "  Authentication: Azure certificate"
    err "  Root certificate name: $ROOT_CERT_NAME"
    err "  Root certificate public data: base64 DER from $ROOT_CERT_FILE"
    err ""
    err "After saving, re-download the VPN client ZIP and re-import AzureVPN/azurevpnconfig.xml."
    exit 1
  fi

  log "Uploaded Root certificate '$ROOT_CERT_NAME' from: $ROOT_CERT_FILE"
  log "IMPORTANT: Re-download the VPN client ZIP and re-import AzureVPN/azurevpnconfig.xml after changing auth type."
fi

GW_PUBLIC_IP="$(az network public-ip show -g "$RG" -n "$PIP_NAME" --query ipAddress -o tsv)"

log "Done. VPN gateway is created and P2S is configured."
log "Gateway public IP: $GW_PUBLIC_IP"
log "Next steps (manual):"
log "- Azure Portal -> Virtual network gateway -> Point-to-site configuration -> Download VPN client"
log "- Import the azurevpnconfig.xml into Azure VPN Client and connect."
