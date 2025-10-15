import { IModel } from '../types/admin';
import axiosInstance from './axiosConfig';

export const modelService = {
    getModels: async (): Promise<IModel[]> => {
    const response = await axiosInstance.get('/api/models');
    return response.data;
    },

    getModel: async (id: string): Promise<IModel> => {
    const response = await axiosInstance.get(`/api/models/${id}`);
    return response.data;
    },

    createModel: async (model: Omit<IModel, 'id'>): Promise<IModel> => {
    const response = await axiosInstance.post('/api/models', model);
    return response.data;
    },

    updateModel: async (model: IModel): Promise<IModel> => {
    const response = await axiosInstance.put(`/api/models/${model.id}`, model);
    return response.data;
    },

    deleteModel: async (id: string): Promise<void> => {
    await axiosInstance.delete(`/api/models/${id}`);
    },

    testIntegration: async (id: string): Promise<string> => {
    const response = await axiosInstance.post(`/api/models/${id}/test`, { responseType: 'text' });
    return response.data;
    }
};

export {}; 