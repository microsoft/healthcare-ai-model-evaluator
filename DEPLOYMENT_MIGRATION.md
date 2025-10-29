# Deployment Script Migration - Vite Environment Variables ✅

## Summary of Changes

The deployment scripts have been successfully updated to support the new Vite environment variable format (`VITE_*` instead of `REACT_APP_*`).

## Files Updated

### 1. `/infra/scripts/update-env.sh`
- ✅ Updated to create `.env.production` with `VITE_*` variables
- ✅ Updated log messages to reflect Vite usage

### 2. `/infra/scripts/postprovision.sh`
- ✅ Updated Static Web App settings to use `VITE_CLIENT_ID`, `VITE_API_BASE_URL`, `VITE_TENANT_ID`
- ✅ Maintains backward compatibility with existing azd environment variables

### 3. `/infra/scripts/postprovision.ps1`
- ✅ Updated PowerShell version to use `VITE_*` variables for Static Web App settings
- ✅ Updated messaging to reference Vite instead of React

### 4. `/env.template`
- ✅ Updated documentation to show new `VITE_*` variable names
- ✅ Added migration note explaining the change from `REACT_APP_*`

## Environment Variable Mapping

| Old (React Scripts) | New (Vite) | Source (azd env) |
|-------------------|------------|------------------|
| `REACT_APP_CLIENT_ID` | `VITE_CLIENT_ID` | `AUTH_CLIENT_ID` |
| `REACT_APP_API_BASE_URL` | `VITE_API_BASE_URL` | `API_BASE_URL` |
| `REACT_APP_TENANT_ID` | `VITE_TENANT_ID` | `AZURE_TENANT_ID` |

## Deployment Process

The deployment process remains exactly the same:

```bash
azd up
```

### What Happens During Deployment:

1. **Infrastructure Provision** (Bicep) - No changes needed
   - Outputs `AUTH_CLIENT_ID`, `API_BASE_URL`, `AZURE_TENANT_ID` to azd environment

2. **Frontend Build** (`azure.yaml` prebuild hook)
   - Runs `infra/scripts/update-env.sh`
   - Creates `.env.production` with `VITE_*` variables
   - Vite reads these during build time

3. **Post-Provision** (after infrastructure deployment)
   - Updates Static Web App settings with `VITE_*` variables
   - Sets Azure Key Vault secrets
   - Configures Azure AD app registration

## Testing the Migration

### Option 1: Fresh Deployment
```bash
# Start completely fresh
azd down --force
azd up
```

### Option 2: Update Existing Deployment
```bash
# Update just the application
azd deploy
```

### Option 3: Manual Environment Update
If you have an existing deployment, you can manually update the Static Web App settings:

```bash
# Get your resource group and static web app name
RESOURCE_GROUP=$(azd env get-values | grep AZURE_RESOURCE_GROUP_NAME | cut -d'=' -f2)
STATIC_WEB_APP=$(az staticwebapp list -g $RESOURCE_GROUP --query "[0].name" -o tsv)

# Update with new variable names
az staticwebapp appsettings set \
  --name $STATIC_WEB_APP \
  --resource-group $RESOURCE_GROUP \
  --setting-names \
    VITE_CLIENT_ID=$(azd env get-values | grep AUTH_CLIENT_ID | cut -d'=' -f2) \
    VITE_API_BASE_URL=$(azd env get-values | grep API_BASE_URL | cut -d'=' -f2) \
    VITE_TENANT_ID=$(azd env get-values | grep AZURE_TENANT_ID | cut -d'=' -f2)
```

## Verification Steps

After deployment, verify the migration worked:

1. **Check Static Web App Settings:**
   ```bash
   az staticwebapp appsettings list --name <your-app-name> --resource-group <your-rg>
   ```
   Should show `VITE_*` variables instead of `REACT_APP_*`

2. **Check Build Logs:**
   - Look for "Updating Vite environment variables for build..." in azd logs
   - Verify `.env.production` contains `VITE_*` variables

3. **Test Application:**
   - Frontend should load and authenticate properly
   - Check browser dev tools console for any environment variable errors

## Rollback Plan

If issues arise, you can temporarily rollback by:

1. **Update scripts to use old variables:**
   ```bash
   # In update-env.sh, change back to REACT_APP_*
   # In postprovision scripts, change back to REACT_APP_*
   ```

2. **Redeploy:**
   ```bash
   azd deploy
   ```

However, the frontend code would also need to be reverted to use `process.env.REACT_APP_*`.

## Notes

- The azd environment variables (`AUTH_CLIENT_ID`, `API_BASE_URL`, `AZURE_TENANT_ID`) remain unchanged
- Only the frontend build-time variables changed from `REACT_APP_*` to `VITE_*`
- All existing deployments will continue to work until they're redeployed
- This change is only effective for new builds/deployments