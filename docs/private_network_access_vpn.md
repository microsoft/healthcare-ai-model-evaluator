# Private Network Access (VPN) for `DEPLOYMENT_NETWORKING=private`

When `DEPLOYMENT_NETWORKING=private`, the app’s ingress is internal-only. That’s expected: you **won’t** be able to load the web UI/API from a normal internet-connected browser.

To test and operate the deployment without making it public, you need network connectivity **into the VNet** (VPN, bastion/jumpbox, or peering from an institutional network).

This doc covers:

1. Point-to-Site (P2S) VPN Gateway (developer/admin laptop access)
2. Integrating with an institution’s existing VPN (common production pattern)

---

## 0) Gather the basics (resource group, VNet, URL)

From the repo root:

```sh
# Resource group that azd deployed into
RG="$(azd env get-value AZURE_RESOURCE_GROUP_NAME)"

# Subscription / tenant (useful for Entra-based VPN auth)
SUB="$(azd env get-value AZURE_SUBSCRIPTION_ID)"
TENANT="$(azd env get-value AZURE_TENANT_ID)"

# Internal URL to test once connected
WEB_URL="$(azd env get-value WEB_BASE_URL)"
API_URL="$(azd env get-value API_BASE_URL)"

echo "RG=$RG"
echo "SUB=$SUB"
echo "TENANT=$TENANT"
echo "WEB_URL=$WEB_URL"
echo "API_URL=$API_URL"
```

Find the VNet that the deployment created (if `CREATE_VNET=true`):

```sh
VNET_NAME="$(az network vnet list -g "$RG" --query "[0].name" -o tsv)"
echo "VNET_NAME=$VNET_NAME"
```

If your resource group contains multiple VNets, pick the correct one explicitly:

```sh
az network vnet list -g "$RG" -o table
```

---

## 1) Method A (recommended for individual access): Point-to-Site (P2S) VPN Gateway

P2S is the simplest way to give a small number of admins/developers access from their laptops.

### 1.0 Fast path: automatic when using a new VNet

If you deploy with:

- `DEPLOYMENT_NETWORKING=private`
- `CREATE_VNET=true`

the infrastructure now provisions:

- **GatewaySubnet**
- **VPN Gateway (OpenVPN + Entra ID)**
- **DNS Private Resolver** (inbound endpoint)
- **Private Endpoint** for the Container Apps Environment
- **Private DNS Zones**:
  - `privatelink.<region>.azurecontainerapps.io` (environment → private endpoint IP)
  - `<defaultDomain>` (app FQDNs → private endpoint IP)

This makes private access work immediately once you download the VPN profile and connect.

You can override or disable with:

```sh
azd env set CREATE_VPN_GATEWAY false
```

### 1.1 Alternative: run the automation script

This repo includes an idempotent script that:

- creates `GatewaySubnet` (best-effort auto-picks an unused `/27`, or you can specify one)
- creates the VPN gateway public IP
- creates the VPN gateway (`VpnGw1` by default)
- configures P2S for **OpenVPN + Microsoft Entra ID** (Azure VPN Client)

From the repo root:

```sh
chmod +x ./infra/scripts/create-p2s-vpn-gateway.sh
./infra/scripts/create-p2s-vpn-gateway.sh
```

Common variants:

```sh
# Specify VNet explicitly
./infra/scripts/create-p2s-vpn-gateway.sh --resource-group "$RG" --vnet-name "$VNET_NAME"

# Provide a specific GatewaySubnet CIDR (recommended if your VNet is tightly segmented)
./infra/scripts/create-p2s-vpn-gateway.sh --gateway-subnet-prefix 10.30.255.0/27

# Create the gateway but don't wait (you can re-run later to configure P2S)
./infra/scripts/create-p2s-vpn-gateway.sh --no-wait
```

### 1.1 Create `GatewaySubnet` in the VNet

Azure VPN Gateway requires a dedicated subnet named exactly `GatewaySubnet`.

- **Do not** reuse the subnets created for Container Apps or Functions.
- Use a **non-overlapping** CIDR inside your VNet address space.
- Microsoft guidance commonly uses `/27` or larger for the gateway subnet.

Example (choose a range that fits your VNet; adjust if you’re not using `10.30.0.0/16`):

```sh
# Example ONLY: pick an unused subnet within your VNet
GATEWAY_SUBNET_PREFIX="10.30.255.0/27"

az network vnet subnet create \
  -g "$RG" \
  --vnet-name "$VNET_NAME" \
  -n GatewaySubnet \
  --address-prefixes "$GATEWAY_SUBNET_PREFIX"
```

### 1.2 Create a Public IP and VPN Gateway

```sh
GW_PIP_NAME="pip-vpn-${VNET_NAME}"
GW_NAME="vpngw-${VNET_NAME}"

az network public-ip create \
  -g "$RG" \
  -n "$GW_PIP_NAME" \
  --sku Standard \
  --allocation-method Static

# Creating a VPN gateway can take 30–60 minutes.
az network vnet-gateway create \
  -g "$RG" \
  -n "$GW_NAME" \
  --public-ip-addresses "$GW_PIP_NAME" \
  --vnet "$VNET_NAME" \
  --gateway-type Vpn \
  --vpn-type RouteBased \
  --sku VpnGw1 \
  --no-wait
```

Check progress:

```sh
az network vnet-gateway show -g "$RG" -n "$GW_NAME" -o table
```

### 1.3 Configure P2S authentication (Microsoft Entra ID) and OpenVPN

For macOS, the most common approach is:

- **Tunnel**: OpenVPN (SSL)
- **Auth**: Microsoft Entra ID
- **Client**: Azure VPN Client

You can configure this in the Azure Portal under:

- Virtual network gateway → **Point-to-site configuration**

Use these values (Azure Public cloud):

- **Address pool**: a CIDR *for VPN clients* that does not overlap your VNet or on-prem ranges (example: `172.16.200.0/24`)
- **Tunnel type**: `OpenVPN (SSL)`
- **Authentication type**: `Microsoft Entra ID`
- **Tenant**: `https://login.microsoftonline.com/{TenantID}`
- **Issuer**: `https://sts.windows.net/{TenantID}/` (note trailing `/`)

Your tenant ID is available via:

```sh
az account show --query tenantId -o tsv
# or: azd env get-value AZURE_TENANT_ID
```

> Note: Microsoft Entra ID auth for P2S is supported only for **OpenVPN** and requires the **Azure VPN Client**.

### 1.4 Download the VPN profile and connect (macOS)

In the Azure portal:

- Virtual network gateway → Point-to-site configuration → **Download VPN client**
- Unzip → find `AzureVPN/azurevpnconfig.xml`

Install Azure VPN Client (macOS), import the profile XML, then connect.

#### If the app FQDN does not resolve (Private Endpoint model)

When a **private endpoint** is enabled for the Container Apps Environment, the expected DNS is:

- Environment: `privatelink.<region>.azurecontainerapps.io` → private endpoint IP
- App FQDNs: `<app>.<defaultDomain>` → private endpoint IP

In this model, use the **non-internal** app FQDN (from the app’s Ingress blade) and ensure you have a Private DNS zone for `<defaultDomain>` with a wildcard `A` record (`*`) that points at the **private endpoint IP**.

Your VPN client must use a DNS resolver **inside** the VNet (DNS Private Resolver inbound endpoint or VM DNS). Update the OpenVPN profile with:

```
dhcp-option DNS <resolver-ip-in-vnet>
dhcp-option DOMAIN-SEARCH <defaultDomain>
```

Reconnect VPN after changes.

#### If the internal `*.internal.*.azurecontainerapps.io` name does not resolve (internal env model)

This is the most common "VPN connected but webapp/API still unreachable" issue.

Private Container Apps ingress uses private DNS records. Your laptop must use DNS servers that can resolve the VNet's private zones. A VPN connection does not automatically mean your device's DNS knows about Azure Private DNS.

Fix options (recommended first):

- Configure a DNS resolver inside the VNet (commonly an **Azure DNS Private Resolver** inbound endpoint), then set the VPN gateway P2S **DNS servers** to that resolver's IP(s).
- Re-download the VPN client profile and reconnect so the new DNS settings apply.

If you already have DNS server IPs to use (example: your Private Resolver inbound endpoint IP `10.30.3.4`), there are two practical ways to apply them:

1) **Gateway DNS servers (may not be available in the portal)**
  - Some tenants/SKUs/portal blades do **not** expose a DNS servers field for P2S (especially OpenVPN + Entra ID). If you don’t see it, use option 2 below.
  - If it *is* available: **Virtual network gateways** → select your gateway → **Point-to-site configuration** → **DNS servers**.
  - Save, then **Download VPN client** again and reconnect.

2) **Client-side split-DNS (reliable in all cases)**

On macOS, configure a resolver rule so only the Container Apps *internal* suffix uses your in-VNet resolver.

First, determine your Container Apps Environment default domain:

```sh
az containerapp env list -g "$RG" -o table

CAE_NAME="<your-container-apps-environment-name>"
DEFAULT_DOMAIN="$(az containerapp env show -g "$RG" -n "$CAE_NAME" --query properties.defaultDomain -o tsv)"
echo "DEFAULT_DOMAIN=$DEFAULT_DOMAIN"

# Internal hostnames are of the form: <app>.internal.<defaultDomain>
INTERNAL_SUFFIX="internal.${DEFAULT_DOMAIN}"
echo "INTERNAL_SUFFIX=$INTERNAL_SUFFIX"
```

Then create a macOS resolver file pointing that suffix at your DNS resolver IP (example `10.30.3.4`):

```sh
DNS_SERVER_IP="10.30.3.4"

sudo mkdir -p /etc/resolver
printf "nameserver %s\n" "$DNS_SERVER_IP" | sudo tee "/etc/resolver/${INTERNAL_SUFFIX}" >/dev/null

# Flush caches
sudo dscacheutil -flushcache
sudo killall -HUP mDNSResponder
```

Reconnect the VPN, then verify resolution:

```sh
dig +short "$(python3 -c 'import os,urllib.parse;print(urllib.parse.urlparse(os.environ["API_URL"]).hostname)')"
```

Windows and Linux equivalents:
- **Windows**: use an NRPT rule (namespace `.internal.<defaultDomain>`) pointing to your resolver IP.
- **Linux** (systemd-resolved): use `resolvectl dns <vpn-iface> <resolver-ip>` plus `resolvectl domain <vpn-iface> ~internal.<defaultDomain>`.

Important: this only fixes name resolution. You still need the Azure Private DNS zone for the internal Container Apps domain linked to the VNet (and a wildcard record pointing at the Container Apps environment static IP).

Quick sanity check on macOS (while VPN is connected):

```sh
# See which DNS servers are in use
scutil --dns | grep -E 'nameserver\[[0-9]+\]' | head

# Does the internal hostname resolve?
dig +short "$(python3 -c 'import os,urllib.parse;print(urllib.parse.urlparse(os.environ["API_URL"]).hostname)')"
```

Once connected, verify you can reach the internal endpoint:

```sh
# Basic connectivity test
curl -I "$WEB_URL"

# Or, if the UI is served from the API base
curl -I "$API_URL"
```

---

## 2) Method B (common institutional setup): connect an existing VPN into the new VNet

In enterprises, the usual pattern is **not** giving every user a P2S profile. Instead, the institution already has a private WAN / on-prem network connected to Azure via:

- Site-to-Site (S2S) VPN, and/or
- ExpressRoute, typically terminated in a **hub VNet**

Then application VNets (spokes) are connected to the hub using VNet peering.

There are two common approaches.

### 2.1 Preferred: Hub-and-spoke with gateway transit (use the institution’s existing hub gateway)

If the institution already has a hub VNet with VPN/ExpressRoute connectivity:

1. **Peer** the MedBench VNet (spoke) to the hub VNet
2. Enable **gateway transit** on the hub peering
3. Enable **use remote gateways** on the spoke peering

High-level Azure CLI sketch (replace values):

```sh
# IDs
SPOKE_VNET_ID="$(az network vnet show -g "$RG" -n "$VNET_NAME" --query id -o tsv)"
HUB_RG="<hubResourceGroup>"
HUB_VNET_NAME="<hubVnetName>"
HUB_VNET_ID="$(az network vnet show -g "$HUB_RG" -n "$HUB_VNET_NAME" --query id -o tsv)"

# Hub -> Spoke peering (allow gateway transit)
az network vnet peering create \
  -g "$HUB_RG" \
  --vnet-name "$HUB_VNET_NAME" \
  -n hub-to-medbench \
  --remote-vnet "$SPOKE_VNET_ID" \
  --allow-vnet-access \
  --allow-gateway-transit

# Spoke -> Hub peering (use remote gateways)
az network vnet peering create \
  -g "$RG" \
  --vnet-name "$VNET_NAME" \
  -n medbench-to-hub \
  --remote-vnet "$HUB_VNET_ID" \
  --allow-vnet-access \
  --use-remote-gateways
```

Operational notes for institutions:

- **Address spaces must not overlap** (on-prem, hub, and this spoke VNet).
- Ensure routing allows client subnets to reach the spoke.
- Ensure any firewalls/NSGs allow required traffic.
- **DNS**: clients must be able to resolve the private/internal hostnames. If the institution uses custom DNS, you may need conditional forwarders or an Azure DNS Private Resolver pattern.

### 2.2 Alternative: direct Site-to-Site (S2S) VPN into this VNet

If there is no hub, you can connect on-prem directly to this VNet.

At a high level:

1. Create a VPN Gateway in this VNet (same as the P2S steps: you still need `GatewaySubnet` + a VPN gateway)
2. Create a **Local Network Gateway** representing the on-prem VPN device and address spaces
3. Create a **Connection** (IPsec/IKE) between the Azure gateway and the on-prem device

High-level Azure CLI sketch (replace values):

```sh
ONPREM_PUBLIC_IP="<onPremVpnDevicePublicIp>"
ONPREM_PREFIXES="<onPremCidr1> <onPremCidr2>"  # space-separated
LNG_NAME="lng-onprem"
CONN_NAME="conn-onprem"
SHARED_KEY="<strongSharedKey>"

az network local-gateway create \
  -g "$RG" \
  -n "$LNG_NAME" \
  --gateway-ip-address "$ONPREM_PUBLIC_IP" \
  --local-address-prefixes $ONPREM_PREFIXES

az network vpn-connection create \
  -g "$RG" \
  -n "$CONN_NAME" \
  --vnet-gateway1 "$GW_NAME" \
  --local-gateway2 "$LNG_NAME" \
  --shared-key "$SHARED_KEY"
```

Then the institution configures their VPN device with the Azure gateway public IP and matching IPsec/IKE parameters.

---

## 3) Testing checklist once you’re “inside” the VNet

- Resolve the internal hostname:
  ```sh
  python3 - <<'PY'
import os, socket
host = os.environ.get('HOST')
print(host, socket.gethostbyname(host))
PY
  ```
  (Set `HOST` to the hostname part of `WEB_URL` / `API_URL`.)
- `curl -I` the `WEB_URL` and/or `API_URL`
- Sign in via the web UI and verify:
  - API calls succeed
  - jobs/metrics processing works (Functions outbound connectivity)

If DNS resolves but requests time out, it’s almost always routing (VPN not advertising the right prefixes, missing peering/gateway transit, or firewall rules).
