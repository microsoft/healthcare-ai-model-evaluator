import axiosInstance from './axiosConfig';

export interface LoginResponse {
    token: string;
    user: {
        id: string;
        name: string;
        email: string;
        roles: string[];
        expertise?: 'provider' | null;
        isModelReviewer?: boolean;
        modelId?: string;
    };
}

const LOCAL_TOKEN_KEY = 'localAuthToken';

export const authService = {
    async login(email: string, password: string): Promise<LoginResponse> {
        const res = await axiosInstance.post<LoginResponse>('/api/auth/login', { email, password });
        const data = res.data;
        localStorage.setItem(LOCAL_TOKEN_KEY, data.token);
        return data;
    },

    async requestPasswordReset(email: string): Promise<void> {
        await axiosInstance.post('/api/auth/forgot-password', { email });
    },

    async resetPassword(payload: { token: string; newPassword: string }): Promise<void> {
        await axiosInstance.post('/api/auth/reset-password', payload);
    },

    getLocalToken(): string | null {
        return localStorage.getItem(LOCAL_TOKEN_KEY);
    },

    clearLocalToken() {
        localStorage.removeItem(LOCAL_TOKEN_KEY);
    },

    async me() {
        console.log("Fetching current user info");
        const res = await axiosInstance.get('/api/auth/me');
        return res.data;
    }
};

export default authService;
