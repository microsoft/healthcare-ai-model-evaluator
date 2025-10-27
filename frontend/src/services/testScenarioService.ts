import { ITestScenario } from '../types/admin';
import axiosInstance from './axiosConfig';

export const testScenarioService = {
    getScenarios: async (): Promise<ITestScenario[]> => {
    const response = await axiosInstance.get('/api/testscenarios');
    return response.data;
    },

    getScenario: async (id: string): Promise<ITestScenario> => {
    const response = await axiosInstance.get(`/api/testscenarios/${id}`);
    return response.data;
    },

    createScenario: async (scenario: Omit<ITestScenario, 'id'>): Promise<ITestScenario> => {
    const response = await axiosInstance.post('/api/testscenarios', scenario);
    return response.data;
    },

    updateScenario: async (scenario: ITestScenario): Promise<ITestScenario> => {
    const response = await axiosInstance.put(`/api/testscenarios/${scenario.id}`, scenario);
    return response.data;
    },

    deleteScenario: async (id: string): Promise<void> => {
    await axiosInstance.delete(`/api/testscenarios/${id}`);
    }
}; 