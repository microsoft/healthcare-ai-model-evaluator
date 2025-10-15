export type UserExpertise = 'provider' | null;

export interface User {
    id: string;
    name: string;
    email: string;
    roles: string[];
    expertise?: UserExpertise;
    isModelReviewer?: boolean;
    modelId?: string;
}

export interface UserList {
    users: User[];
    loading: boolean;
    error: string | null;
}

export interface AuthState {
    user: User | null;
    isAuthenticated: boolean;
    error: string | null;
    loading: boolean;
    userList: UserList;
}

export enum AuthActionType {
    LOGIN_START = 'LOGIN_START',
    LOGIN_SUCCESS = 'LOGIN_SUCCESS',
    AUTH_INITIALIZE = 'AUTH_INITIALIZE',
    LOGIN_FAILURE = 'LOGIN_FAILURE',
    LOGOUT = 'LOGOUT',
    LOAD_USERS_START = 'LOAD_USERS_START',
    LOAD_USERS_SUCCESS = 'LOAD_USERS_SUCCESS',
    LOAD_USERS_FAILURE = 'LOAD_USERS_FAILURE',
}

export type AuthAction =
    | { type: AuthActionType.LOGIN_START }
    | { type: AuthActionType.LOGIN_SUCCESS; payload: User }
    | { type: AuthActionType.AUTH_INITIALIZE }
    | { type: AuthActionType.LOGIN_FAILURE; payload: string }
    | { type: AuthActionType.LOGOUT }
    | { type: AuthActionType.LOAD_USERS_START }
    | { type: AuthActionType.LOAD_USERS_SUCCESS; payload: User[] }
    | { type: AuthActionType.LOAD_USERS_FAILURE; payload: string }; 