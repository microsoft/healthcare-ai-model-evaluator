import { createSlice, PayloadAction } from '@reduxjs/toolkit';

interface AuthState {
    isAuthenticated: boolean;
    user: any | null;
    error: string | null;
}

const initialState: AuthState = {
    isAuthenticated: false,
    user: null,
    error: null
};

const authSlice = createSlice({
    name: 'auth',
    initialState,
    reducers: {
        setAuthenticated(state, action: PayloadAction<boolean>) {
            state.isAuthenticated = action.payload;
        },
        setUser(state, action: PayloadAction<any>) {
            state.user = action.payload;
        },
        setError(state, action: PayloadAction<string | null>) {
            state.error = action.payload;
        },
        logout(state) {
            state.isAuthenticated = false;
            state.user = null;
            state.error = null;
        }
    }
});

export const { setAuthenticated, setUser, setError, logout } = authSlice.actions;
export default authSlice.reducer;