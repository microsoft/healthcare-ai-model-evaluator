import axiosInstance from './axiosConfig';
import { 
    DataSet, 
    CreateDataSetRequest, 
    UpdateDataSetRequest, 
    DataObject,
    DataFileDto,
    DataFileMapping 
} from '../types/dataset';

const API_URL = '/api/DataSets';

export const dataSetService = {
    getDataSets: async (): Promise<DataSet[]> => {
        const response = await axiosInstance.get(API_URL);
        return response.data;
    },

    getDataSet: async (datasetId: string): Promise<DataSet> => {
        const response = await axiosInstance.get(`${API_URL}/${datasetId}`);
        return response.data;
    },

    getDataObjects: async (datasetId: string): Promise<DataObject[]> => {
        const response = await axiosInstance.get(`${API_URL}/${datasetId}/dataobjects`);
        return response.data;
    },

    getDataObject: async (datasetId: string, objectId: string): Promise<DataObject> => {
        const response = await axiosInstance.get(`${API_URL}/${datasetId}/dataobjects/${objectId}`);
        return response.data;
    },

    addDataSet: async (dataset: CreateDataSetRequest): Promise<DataSet> => {
        const formData = new FormData();
        
        // Add basic dataset properties
        formData.append('name', dataset.name);
        formData.append('origin', dataset.origin);
        formData.append('description', dataset.description);
        formData.append('aiModelType', dataset.aiModelType);
        formData.append('tags', JSON.stringify(dataset.tags ?? []));
        formData.append('modelOutputCount', dataset.modelOutputCount.toString());
        
        // Add file if it exists
        if (dataset.file) {
            formData.append('file', dataset.file);
        }
        
        // Add mapping if it exists
        if (dataset.mapping) {
            formData.append('mapping', JSON.stringify(dataset.mapping));
        }
        
        const response = await axiosInstance.post(API_URL, formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        });
        return response.data;
    },

    updateDataSet: async (dataset: UpdateDataSetRequest): Promise<DataSet> => {
        const response = await axiosInstance.put(`${API_URL}/${dataset.id}`, dataset);
        return response.data;
    },

    deleteDataSet: async (id: string): Promise<void> => {
        await axiosInstance.delete(`${API_URL}/${id}`);
    },

    findDataObject: async (dataObjectId: string): Promise<DataObject> => {
        const response = await axiosInstance.get(`/api/DataSets/dataobjects/${dataObjectId}`);
        return response.data;
    },

    getDataFiles: async (datasetId: string): Promise<DataFileDto[]> => {
        const response = await axiosInstance.get(`${API_URL}/${datasetId}/datafiles`);
        return response.data;
    },

    addDataFile: async (datasetId: string, file: File, mapping: DataFileMapping): Promise<void> => {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('mapping', JSON.stringify(mapping));
        await axiosInstance.post(`${API_URL}/${datasetId}/datafiles`, formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        });
    },

    deleteDataFile: async (datasetId: string, fileIndex: number): Promise<void> => {
        await axiosInstance.delete(`${API_URL}/${datasetId}/datafiles/${fileIndex}`);
    }
};
