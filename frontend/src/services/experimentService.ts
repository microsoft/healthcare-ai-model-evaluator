import { IExperiment, ITrial } from '../types/admin';
import axiosInstance from './axiosConfig';

export const experimentService = {
    getExperiments: async (): Promise<IExperiment[]> => {
    const response = await axiosInstance.get('/api/experiments');
    return response.data;
    },

    getExperiment: async (id: string): Promise<IExperiment> => {
        const response = await axiosInstance.get(`/api/experiments/${id}`);
        return response.data;
    },

    createExperiment: async (experiment: Omit<IExperiment, 'id'>): Promise<IExperiment> => {
    const response = await axiosInstance.post('/api/experiments', experiment);
    return response.data;
    },

    updateExperiment: async (experiment: IExperiment): Promise<IExperiment> => {
    const response = await axiosInstance.put(`/api/experiments/${experiment.id}`, experiment);
    return response.data;
    },

    deleteExperiment: async (id: string): Promise<void> => {
    await axiosInstance.delete(`/api/experiments/${id}`);
    },

    getAssignedExperiments: async (): Promise<IExperiment[]> => {
    const response = await axiosInstance.get('/api/experiments/assigned');
    return response.data;
    },

    processExperiment: async (id: string): Promise<IExperiment> => {
        const response = await axiosInstance.put(`/api/experiments/${id}/process`);
        return response.data;
    },

    updateStatus: async (id: string, status: string): Promise<IExperiment> => {
        const response = await axiosInstance.put(`/api/experiments/${id}/status`, JSON.stringify(status), {
            headers: { 'Content-Type': 'application/json' }
        });
        return response.data;
    },

    getTrials: async (experimentId: string): Promise<ITrial[]> => {
        const response = await axiosInstance.get(`/api/trials/experiment/${experimentId}`);
        return response.data;
    }
};

export const EXPERIMENT_TYPES = [
    'Simple Evaluation',
    'Simple Validation',
    'Arena',
    'Full Validation'
] as const;

export const EXPERIMENT_STATUS = [
    'Draft',
    'In Progress',
    'Completed',
    'Cancelled'
] as const; 