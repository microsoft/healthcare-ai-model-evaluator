import { User } from './auth.types';

export interface UserFormData {
    name: string;
    email: string;
    roles: string[];
}

export interface UserState {
    users: User[];
    selectedUsers: string[];
    isLoading: boolean;
    error: string | null;
    isFlyoutOpen: boolean;
    editingUser: User | null;
    mongoUserId?: string;
}

export enum UserActionType {
    LOAD_USERS = 'LOAD_USERS',
    SELECT_USER = 'SELECT_USER',
    DESELECT_USER = 'DESELECT_USER',
    ADD_USER = 'ADD_USER',
    UPDATE_USER = 'UPDATE_USER',
    DELETE_USERS = 'DELETE_USERS',
    SET_ERROR = 'SET_ERROR',
    TOGGLE_FLYOUT = 'TOGGLE_FLYOUT',
    SET_EDITING_USER = 'SET_EDITING_USER',
}

export type UserAction =
    | { type: UserActionType.LOAD_USERS; payload: User[] }
    | { type: UserActionType.SELECT_USER; payload: string[] }
    | { type: UserActionType.DESELECT_USER; payload: string[] }
    | { type: UserActionType.ADD_USER; payload: User }
    | { type: UserActionType.UPDATE_USER; payload: User }
    | { type: UserActionType.DELETE_USERS; payload: string[] }
    | { type: UserActionType.SET_ERROR; payload: string }
    | { type: UserActionType.TOGGLE_FLYOUT }
    | { type: UserActionType.SET_EDITING_USER; payload: User | null }; 