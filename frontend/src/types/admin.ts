import { UserExpertise } from './auth.types';
import { DataContent } from './dataset';

export const AIModelType = {
    IMAGE_TO_TEXT: 'Image to Text',
    TEXT_TO_TEXT: 'Text to Text',
    IMAGE_TO_IMAGE: 'Image to Image',
    MULTIMODAL: 'Multimodal'
} as const;

export const ClinicalTaskType = {
    SUMMARIZATION: 'Summarization',
    CLASSIFICATION: 'Classification',
    EXTRACTION: 'Extraction',
    GENERATION: 'Generation'
} as const;

export type AIModelTypeValues = typeof AIModelType[keyof typeof AIModelType];
export type ClinicalTaskTypeValues = typeof ClinicalTaskType[keyof typeof ClinicalTaskType];

export const RequiredIntegrationParameters: Record<string, string[]> = {
  'openai': ['ENDPOINT', 'API_KEY', 'DEPLOYMENT'],
  'openai-reasoning': ['ENDPOINT', 'API_KEY', 'DEPLOYMENT'],
  'cxrreportgen': ['ENDPOINT', 'API_KEY', 'DEPLOYMENT', 'VERSION'],
  'azure-serverless': ['ENDPOINT', 'API_KEY'],
  'functionapp': ['FunctionAppType']
}

export type IntegrationType = 'openai' | 'openai-reasoning' | 'cxrreportgen' | 'azure-serverless' | 'functionapp';
export interface IModel {
    id: string;
    name: string;
    modelType: AIModelTypeValues;
    origin: string;
    description?: string;
    parameters?: string;
    endpoint?: string;
    tags?: string[];
    ownerId?: string;
    createdAt?: Date;
    updatedAt?: Date;
    integrationType?: string;
    integrationSettings?: Record<string, string>;
    costPerToken?: number;
    costPerTokenOut?: number;
}

export interface TaskDataSetModel {
    dataSetId: string;
    modelId: string;
    modelOutputIndex: number;
    generatedOutputKey: string;
    isGroundTruth?: boolean;
}

export interface IClinicalTask {
    id: string;
    name: string;
    dataSetModels: TaskDataSetModel[];
    prompt?: string;
    tags: string[];
    ownerId: string;
    createdAt: string;
    updatedAt: string;
    evalMetric: string;
    totalCost: number;
    generationStatus: string;
    metricsGenerationStatus: string;
    metrics: {
        [modelId: string]: {
            [metricName: string]: number;
        };
    };
    modelResults: {
        [modelId: string]: {
            eloScore: number;
            averageRating: number;
            correctScore: number;
            validationTime: number;
            singleEvaluationScores: Record<string, number>;
        };
    };
}

export type EvalMetricType = 
    | 'Text-based metrics'
    | 'Image-based metrics'
    | 'Accuracy metrics'
    | 'Safety metrics'
    | 'Bias metrics';

export const EvalMetricFilterOptions = {
    'All': 'All',
    'Text-based metrics': 'Text-based metrics',
    'Image-based metrics': 'Image-based metrics',
    'Accuracy metrics': 'Accuracy metrics',
    'Safety metrics': 'Safety metrics',
    'Bias metrics': 'Bias metrics'
} as const;

export type EvalMetricFilterType = typeof EvalMetricFilterOptions[keyof typeof EvalMetricFilterOptions];

export interface ITestScenario {
    id: string;
    name: string;
    taskId: string;
    description?: string;
    reviewerInstructions?: string;
    modelIds: string[];
    tags?: string[];
    experimentType?: string;
    questions?: EvalQuestion[];
    allowOutputEditing?: boolean;
}

export interface EvalQuestion {
    id: string;
    name: string;
    questionText: string;
    options: EvalQuestionOption[];
    evalMetric?: string;
    response?:string;
}

export interface EvalQuestionOption {
    id: string;
    text: string;
    value: string;
}

export enum ExperimentStatus {
    Draft = 'Draft',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Cancelled = 'Cancelled'
}

export enum ProcessingStatus {
    NotProcessed = 'NotProcessed',
    Processing = 'Processing',
    Processed = 'Processed',
    Finalizing = 'Finalizing',
    Finalized = 'Final'
}

export interface IExperiment {
    id: string;
    name: string;
    description: string;
    status: ExperimentStatus;
    processingStatus: ProcessingStatus;
    testScenarioId: string;
    experimentType: string;
    tags: string[];
    reviewerIds: string[];
    modelIds: string[];
    createdAt: string;
    updatedAt: string;
    pendingTrials?: number;
    totalTrials?: number;
    totalCost?: number;
    reviewerInstructions?: string;
    randomized?: boolean;
}

export interface IUser {
    id: string;
    name: string;
    email: string;
    roles: string[];
    expertise?: UserExpertise;
}

export interface ModelOutput {
    modelId: string;
    output: DataContent[];
}

export const FlagTags = {
    HARMFUL_CONTENT: 'Harmful content',
    HALLUCINATION: 'Hallucination',
    BIAS: 'Bias',
    OMISSION: 'Omission',
    UNINTENDED_PHI_PII: 'Unintended PHI/PII',
    OTHER: 'Other'
} as const;

export type FlagTagType = (typeof FlagTags)[keyof typeof FlagTags];

export interface TrialFlag {
    modelId: string;
    text: string;
    userId: string;
    createdAt: string;
    flagTags: FlagTagType[];
}

export interface BoundingBox {
    id: string;
    x: number;
    y: number;
    width: number;
    height: number;
    imageIndex: number;
    modelId?: string;
    annotation?: string;
    coordinateType: 'pixel' | 'percentage';
    metadata?: {
        annotation?: string;
    };
}

export interface ITrial {
    id: string;
    userId: string;
    experimentId: string;
    experimentType: string;
    experimentStatus: string;
    status: 'pending' | 'skipped' | 'done';
    prompt: string;
    modelInputs: DataContent[];
    dataObjectId: string;
    dataSetId: string;
    modelOutputs: ModelOutput[];
    reviewerInstructions?: string;
    response?: TrialResponse;
    flags?: TrialFlag[];
    boundingBoxes?: BoundingBox[];
    questions?: EvalQuestion[];
}

export interface TrialResponse {
    modelId: string;
    text: string;
}

export interface TrialUpdateRequest {
    trial: Partial<ITrial>;
    timeSpent?: number;
}

export const OutputSelectionType = {
    GENERATE_NEW: 'generate',
    USE_EXISTING: 'existing'
} as const;

export type OutputSelectionTypeValue = typeof OutputSelectionType[keyof typeof OutputSelectionType]; 

// Add an empty export to ensure this file is treated as a module
export {};