import axiosInstance from './axiosConfig';
import { DataObject } from '../types/dataset';

export const dataObjectService = {
    getByDataSetId: async (dataSetId: string): Promise<DataObject[]> => {
        const response = await axiosInstance.get(`/api/datasets/${dataSetId}/dataobjects`);
        return response.data;
    },

    getById: async (dataSetId: string, dataObjectId: string): Promise<DataObject> => {
        const response = await axiosInstance.get(`/api/datasets/${dataSetId}/dataobjects/${dataObjectId}`);
        return response.data;
    }
}; 