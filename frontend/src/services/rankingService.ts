import axiosInstance from './axiosConfig';

export interface ExperimentResults {
    eloScore: number;
    averageRating: number;
    correctScore: number;
    validationTime: number;
}

export interface ModelRanking {
    id: string;
    name: string;
    type: string;
    eloScore: number;
    averageRating: number;
    correctScore: number;
    validationTime: number;
    experimentResultsByMetric: Record<string, ExperimentResults>;
}

export const rankingService = {
    getRankings: async (): Promise<ModelRanking[]> => {
        const response = await axiosInstance.get('/api/models/rankings');
        return response.data;
    }
}; 