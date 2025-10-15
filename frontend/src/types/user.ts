import type { User, UserExpertise } from './auth.types';

export interface UserFormData {
    name: string;
    email: string;
    roles: string[];
    expertise: UserExpertise;
    isModelReviewer?: boolean;
    modelId?: string;
}

export const AVAILABLE_ROLES = [
    { key: 'admin', text: 'Administrator' },
    { key: 'reviewer', text: 'Reviewer' }
];

export type { User, UserExpertise };