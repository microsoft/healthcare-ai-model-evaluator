export type AIModelType = 'text-to-text' | 'image-to-text';

export const AI_MODEL_TYPE_LABELS: Record<AIModelType, string> = {
    'text-to-text': 'Text to Text',
    'image-to-text': 'Image to Text'
};
export type ModelIntegrationType = 'cxrreportgen' | 'openai' | 'openai-reasoning' | 'deepseek' | 'phi4' | 'functionapp' | 'none' | '';

export interface DataContent {
    type: 'text' | 'imageurl';
    content: string;
    generatedForClinicalTask?: string;
    totalTokens?: number;
}

export interface DataObject {
    id: string;
    dataSetId: string;
    name: string;
    description: string;
    inputData: DataContent[];
    outputData: DataContent[];
    createdAt: string;
    updatedAt: string;
    generatedOutputData: DataContent[];
}

export type DataObjectWithoutId = Omit<DataObject, 'id' | 'dataSetId'>;

export interface DataSetListItem {
    id: string;
    name: string;
    origin: string;
    description: string;
    aiModelType: AIModelType;
    tags: string[];
    dataObjectCount: number;
    modelOutputCount: number;
    generatedDataList: string[];
    files?: DataFileDto[];
}

export interface DataSet {
    id: string;
    name: string;
    origin: string;
    description: string;
    aiModelType: AIModelType;
    tags: string[];
    dataObjectCount: number;
    modelOutputCount: number;
    generatedDataList: string[];
    totalTokens: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    files?: DataFileDto[];
    totalOutputTokensPerIndex: Record<string, number>;
}

export interface CreateDataSetRequest {
    name: string;
    origin: string;
    description: string;
    aiModelType: AIModelType;
    tags: string[];
    modelOutputCount: number;
    file: File | null;
    mapping: DataFileMapping | null;
}

export type UpdateDataSetRequest = Omit<DataSet, 'dataObjectCount' | 'generatedDataList' | 'totalTokens' | 'dataObjects' | 'totalOutputTokens' | 'totalOutputTokensPerIndex' | 'totalInputTokens'>;

export enum DataFileProcessingStatus {
    Unprocessed = 'Unprocessed',
    Processing = 'Processing',
    Completed = 'Completed',
    Failed = 'Failed'
}

export interface DataFileKeyPath {
    type: 'text' | 'imageurl';
    keyPath: string[];
    isArray?: boolean;
}

export interface DataFileMapping {
    inputMappings: DataFileKeyPath[];
    outputMappings: DataFileKeyPath[];
}

export interface DataFileDto {
    fileName: string;
    processingStatus: DataFileProcessingStatus;
    errorMessage: string;
    uploadedAt: string;
    processedObjectCount: number;
    totalObjectCount: number;
    mapping: DataFileMapping;
}

export {}; 