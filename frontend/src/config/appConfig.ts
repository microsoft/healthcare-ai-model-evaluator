// Global application configuration
export const appConfig = {
    // Base path for the application - can be overridden via environment variable
    basePath: import.meta.env.VITE_BASE_PATH || '/webapp',
    
    // Helper function to get full path with base path
    getPath: (path: string): string => {
        const basePath = import.meta.env.VITE_BASE_PATH || '/webapp';
        // Ensure path starts with /
        const cleanPath = path.startsWith('/') ? path : `/${path}`;
        // Combine base path with the route, avoiding double slashes
        return `${basePath}${cleanPath}`.replace(/\/+/g, '/');
    }
};

// Commonly used paths
export const routes = {
    admin: {
        base: appConfig.getPath('/admin'),
        assignments: appConfig.getPath('/admin/assignments'),
        models: appConfig.getPath('/admin/models'),
        clinicalTasks: appConfig.getPath('/admin/clinical-tasks'),
        data: appConfig.getPath('/admin/data'),
        users: appConfig.getPath('/admin/users')
    },
    auth: {
        login: appConfig.getPath('/'),
        logout: appConfig.getPath('/')
    }
};