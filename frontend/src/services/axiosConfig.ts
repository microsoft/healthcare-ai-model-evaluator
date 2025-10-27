import axios from 'axios';
import { msalInstance, apiRequest, getRuntimeConfig } from '../config/authConfig';

// Use the runtime configuration to get the API base URL
const getApiBaseUrl = () => {
    const config = getRuntimeConfig();
    return config.apiBaseUrl;
};

const axiosInstance = axios.create({
    baseURL: getApiBaseUrl(),
    headers: {
        'Content-Type': 'application/json'
    }
});

// Add a request interceptor to handle authentication
axiosInstance.interceptors.request.use(async (config) => {
    // Update the baseURL in case it changed at runtime
    config.baseURL = getApiBaseUrl();

    // Prefer local auth token if present
    const localToken = typeof window !== 'undefined' ? localStorage.getItem('localAuthToken') : null;
    if (localToken) {
        config.headers.Authorization = `Bearer ${localToken}`;
        return config;
    }

    // Fall back to MSAL if signed in via AAD
    const account = msalInstance.getActiveAccount();
    if (account) {
        const response = await msalInstance.acquireTokenSilent({
            ...apiRequest,
            account: account
        });
        config.headers.Authorization = `Bearer ${response.accessToken}`;
    }
    return config;
}, (error) => {
    return Promise.reject(error);
});

axios.defaults.headers.common['Cache-Control'] = 'no-cache';
axios.defaults.headers.common['Pragma'] = 'no-cache';

export default axiosInstance; 