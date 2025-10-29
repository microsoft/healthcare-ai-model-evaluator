import { PublicClientApplication, Configuration } from "@azure/msal-browser";

// Configuration that uses environment variables (available at build time via azd)
const runtimeConfig = {
    clientId: import.meta.env.VITE_CLIENT_ID || "432521be-fddf-45d4-8a9e-f9ff8495db08",
    tenantId: import.meta.env.VITE_TENANT_ID || "72f988bf-86f1-41af-91ab-2d7cd011db47",
    apiBaseUrl: import.meta.env.VITE_API_BASE_URL || "http://localhost:5000"
};

console.log("Configuration loaded:", {
    clientId: runtimeConfig.clientId,
    tenantId: runtimeConfig.tenantId,
    apiBaseUrl: runtimeConfig.apiBaseUrl,
    usingFallback: !import.meta.env.VITE_CLIENT_ID
});

export const msalConfig: Configuration = {
    auth: {
        clientId: runtimeConfig.clientId,
        authority: `https://login.microsoftonline.com/${runtimeConfig.tenantId}`,
        redirectUri: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false,
    },
};

// Function to get current runtime config
export const getRuntimeConfig = () => runtimeConfig;

// Scopes for Microsoft Graph API
export const loginRequest = {
    scopes: ["User.Read"]
};

// Scopes for our custom API
export const apiRequest = {
    scopes: [`${runtimeConfig.clientId}/.default`]
};

// Create MSAL instance
const msalInstance = new PublicClientApplication(msalConfig);
msalInstance.initialize();

export { msalInstance };
