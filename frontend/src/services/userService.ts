import { User } from '../types/auth.types';
import { msalInstance, apiRequest, getRuntimeConfig } from '../config/authConfig';
import axios from 'axios';
import axiosInstance from './axiosConfig';


function getApiBaseUrl() {
    return getRuntimeConfig().apiBaseUrl;
}

export const userService = {
    getUsers: async (): Promise<User[]> => {
        const response = await axiosInstance.get('/api/users');
        return response.data;
    },

    getUser: async (userId: string): Promise<User> => {
        const response = await axiosInstance.get(`/api/users/${userId}`);

        return response.data;
    },

    createUser: async (user: User): Promise<User> => {
        const response = await axiosInstance.post('/api/users', user);
        return response.data;
    },

    updateUser: async (user: User): Promise<User> => {
        const response = await axiosInstance.put(`/api/users/${user.id}`, user);
        return response.data;
    },

    deleteUser: async (id: string): Promise<void> => {
        const response = await axiosInstance.delete(`/api/users/${id}`);
        return response.data;
    },


    getUserByEmail: async (email: string): Promise<User> => {
        const response = await axiosInstance.put('/api/users/by-email',
            { email }
        );
        return response.data;
    },

    sendPasswordSetupEmail: async (email: string, organization?: string): Promise<void> => {
        await axiosInstance.post('/api/auth/admin/initiate-reset',
            { email, organization },
        );
    },

    adminSetPassword: async (email: string, newPassword: string): Promise<void> => {
        await axiosInstance.post('/api/auth/admin/set-password',
            { email, newPassword }
        );
    }
}; 