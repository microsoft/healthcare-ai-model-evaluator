import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { User } from '../types/user';

export interface UserState {
    users: User[];
    selectedUsers: string[];
    loading: boolean;
    error: string | null;
    isFlyoutOpen: boolean;
    editingUser: User | null;
    mongoUserId?: string;
}

export const initialUserState: UserState = {
    users: [],
    selectedUsers: [],
    loading: false,
    error: null,
    isFlyoutOpen: false,
    editingUser: null,
    mongoUserId: undefined,
};

const userSlice = createSlice({
    name: 'user',
    initialState: initialUserState,
    reducers: {
        setLoading: (state, action: PayloadAction<boolean>) => {
            state.loading = action.payload;
        },
        setError: (state, action: PayloadAction<string | null>) => {
            state.error = action.payload;
        },
        setUsers: (state, action: PayloadAction<User[]>) => {
            state.users = action.payload;
            state.selectedUsers = [];
        },
        addUser: (state, action: PayloadAction<User>) => {
            state.users.push(action.payload);
            state.isFlyoutOpen = false;
        },
        updateUser: (state, action: PayloadAction<User>) => {
            const index = state.users.findIndex(u => u.id === action.payload.id);
            if (index !== -1) {
                state.users[index] = action.payload;
            }
            state.isFlyoutOpen = false;
            state.editingUser = null;
        },
        deleteUsers: (state, action: PayloadAction<string[]>) => {
            state.users = state.users.filter(user => !action.payload.includes(user.id));
            state.selectedUsers = [];
        },
        setSelectedUsers: (state, action: PayloadAction<string[]>) => {
            state.selectedUsers = action.payload;
        },
        toggleFlyout: (state) => {
            state.isFlyoutOpen = !state.isFlyoutOpen;
            if (!state.isFlyoutOpen) {
                state.editingUser = null;
            }
        },
        setEditingUser: (state, action: PayloadAction<User | null>) => {
            state.editingUser = action.payload;
            state.isFlyoutOpen = true;
        },
        setMongoUserId: (state, action: PayloadAction<string>) => {
            state.mongoUserId = action.payload;
        },
    },
});

export const {
    setLoading,
    setError,
    setUsers,
    addUser,
    updateUser,
    deleteUsers,
    setSelectedUsers,
    toggleFlyout,
    setEditingUser,
    setMongoUserId,
} = userSlice.actions;

export const userReducer = userSlice.reducer; 