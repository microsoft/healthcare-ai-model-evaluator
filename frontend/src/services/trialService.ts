import { ITrial } from '../types/admin';
import axiosInstance from './axiosConfig';

interface TrialStats {
    pendingCount: number;
    completedCount: number;
    averageConcordance: number;
    totalTimeSeconds: number;
    testScenarioId: string;
}

export const trialService = {
    getPendingTrialCounts: async (): Promise<{ [key: string]: number }> => {
        const response = await axiosInstance.get('/api/trials/pending-counts');
        return response.data;
    },

    getNextPendingTrial: async (
        testScenarioIds?: string[]
    ): Promise<ITrial | null> => {
        const response = await axiosInstance.get(`/api/trials/next-pending`, {
            params: {
                testScenarioIds: testScenarioIds?.length ? testScenarioIds.join(',') : undefined
            }
        });
        return response.data;
    },

    getDoneTrialIds: async (testScenarioIds: string[]): Promise<string[]> => {
        const response = await axiosInstance.get('/api/trials/get-done-ids',{
            params: {
                testScenarioIds: testScenarioIds?.length ? testScenarioIds.join(',') : undefined
            }
        });
        return response.data;
    },

    getDoneTrialById: async (trialId: string): Promise<ITrial | null> => {
        const response = await axiosInstance.get(`/api/trials/get-done`,{
            params: { trialId }
        });
        return response.data;
    },

    getDoneTrial: async (testScenarioIds: string[], trialId: string) => {
        const response = await axiosInstance.get(`/api/trials/next-done`, {
            params: {
                testScenarioIds: testScenarioIds?.length ? testScenarioIds.join(',') : undefined,
                afterTrialId:trialId
            }
        });
        return response.data;
    },

    updateTrial: async (trialId: string, update: Partial<ITrial>): Promise<ITrial> => {
        const response = await axiosInstance.put(`/api/trials/${trialId}`, {
            id: trialId,
            ...update
        });
        return response.data;
    },

    updateTrialFlag: async (trialId: string, update: Partial<ITrial>): Promise<ITrial> => {
        const response = await axiosInstance.put(`/api/trials/flags/${trialId}`, {
            id: trialId,
            ...update
        });
        return response.data;
    },

    getTrialStats: async (): Promise<TrialStats[]> => {
        const response = await axiosInstance.get('/api/trials/stats');
        return response.data;
    },

    getTrialsByExperimentId: async (experimentId: string): Promise<ITrial[]> => {
        const response = await axiosInstance.get(`/api/trials/experiment/${experimentId}`);
        return response.data;
    }
}; 