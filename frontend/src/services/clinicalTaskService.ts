import { IClinicalTask } from '../types/admin';
import axiosInstance from './axiosConfig';

export const clinicalTaskService = {
    getTasks: async (): Promise<IClinicalTask[]> => {
    const response = await axiosInstance.get('/api/clinicaltasks');
    return response.data;
    },

    getTask: async (id: string): Promise<IClinicalTask> => {
    const response = await axiosInstance.get(`/api/clinicaltasks/${id}`);
    return response.data;
    },

    createTask: async (task: Omit<IClinicalTask, 'id'>): Promise<IClinicalTask> => {
    const response = await axiosInstance.post('/api/clinicaltasks', task);
    return response.data;
    },

    updateTask: async (task: IClinicalTask): Promise<IClinicalTask> => {
    const response = await axiosInstance.put(`/api/clinicaltasks/${task.id}`, task);
    return response.data;
    },

    deleteTask: async (id: string): Promise<void> => {
    await axiosInstance.delete(`/api/clinicaltasks/${id}`);
    },

    generateOutputs: async (id: string): Promise<void> => {
    await axiosInstance.post(`/api/clinicaltasks/${id}/generate`);
    },

    generateMetrics: async (id: string): Promise<void> => {
    await axiosInstance.post(`/api/clinicaltasks/${id}/generate-metrics`);
    },

    estimateCost: async (request: { dataSetId: string; modelId: string }): Promise<number> => {
    const response = await axiosInstance.post('/api/clinicaltasks/estimate-cost', request);
    return response.data;
    },

    getPendingTasksForUser: async (): Promise<IClinicalTask[]> => {
        const response = await axiosInstance.get('/api/clinicaltasks/pending-for-user');
        return response.data;
    },

    uploadMetrics: async (taskId: string, metrics: Record<string, Record<string, number>>): Promise<void> => {
        await axiosInstance.post(`/api/clinicaltasks/${taskId}/upload-metrics`, metrics);
    }
};

export {}; 