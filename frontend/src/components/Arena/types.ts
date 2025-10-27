import { DataContent } from '../../types/dataset';

export interface ChartSection {
    title?: string;
    type: 'text' | 'image';
    content: string;
}

export interface PatientInfo {
    name: string;
    age: number;
    gender: string;
    ethnicity: string;
    dateOfVisit: string;
    mrn: string;
}

export interface ChartData {
    inputData?: DataContent[];
    sections?: ChartSection[];
    patientInfo?: PatientInfo;
} 