import React, { createContext, useReducer, useContext, useEffect } from 'react';
import { AuthState,  User, AuthActionType } from '../types/auth.types';
import { authReducer, initialAuthState } from '../reducers/authReducer';
import { userService } from '../services/userService';
import { useMsal } from "@azure/msal-react";
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { loginRequest } from "../config/authConfig";
import { authService } from '../services/authService';

interface AuthContextType extends AuthState {
    login: () => Promise<void>;
    loginWithPassword: (email: string, password: string) => Promise<void>;
    logout: () => void;
    loadUsers: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);



export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [state, dispatch] = useReducer(authReducer, initialAuthState);
    const { instance, accounts } = useMsal();

    const loadUsers = async () => {
        try {
            dispatch({ type: AuthActionType.LOAD_USERS_START });
            const users = await userService.getUsers();
            dispatch({ type: AuthActionType.LOAD_USERS_SUCCESS, payload: users });
        } catch (error) {
            dispatch({ 
                type: AuthActionType.LOAD_USERS_FAILURE, 
                payload: 'Failed to load users' 
            });
        }
    };

    // Check for existing session (local token or MSAL) on mount
    useEffect(() => {
        const initializeAuth = async () => {
            // 1) Local auth: if we have a local token, try to load current user
            console.log("Initializing auth...");
            const localToken = authService.getLocalToken();
            // 2) MSAL session
            if (accounts.length > 0) {
                instance.setActiveAccount(accounts[0]);
                console.log("Attempting login with MSAL account");
                dispatch({ type: AuthActionType.AUTH_INITIALIZE });
                try {
                    const activeAccount = accounts[0];
                    await instance.initialize();
                    await instance.acquireTokenSilent({
                        ...loginRequest,
                        account: activeAccount
                    });
                    
                    const me = await authService.me();
                    const userInfo: User = me as User;
                    dispatch({ type: AuthActionType.LOGIN_SUCCESS, payload: userInfo });
                    return;
                } catch (error) {
                    console.log('Silent token acquisition failed:', error);
                    dispatch({ 
                        type: AuthActionType.LOGIN_FAILURE, 
                        payload: 'Failed to restore session' 
                    });
                    return;
                }
            }
            if (localToken) {
                dispatch({ type: AuthActionType.LOGIN_START });
                console.log("Attempting login with local token");
                try {
                    const me = await authService.me();
                    const userInfo: User = me as User;
                    dispatch({ type: AuthActionType.LOGIN_SUCCESS, payload: userInfo });
                    return;
                } catch (e) {
                    authService.clearLocalToken();
                    dispatch({ type: AuthActionType.LOGIN_FAILURE, payload: 'Failed to restore session' });
                    return;
                }
            }
            
            
        };

        initializeAuth();
    }, [accounts, instance]);

    const login = async () => {
        try {
            dispatch({ type: AuthActionType.LOGIN_START });
            if (accounts.length > 0) {
                const activeAccount = accounts[0];
                await instance.acquireTokenSilent({
                    ...loginRequest,
                    account: activeAccount
                });
                
                    const me = await authService.me();
                    const userInfo: User = me as User;
                dispatch({ type: AuthActionType.LOGIN_SUCCESS, payload: userInfo });
            } else {
                const response = await instance.loginPopup(loginRequest);
                instance.setActiveAccount(response.account);
                const me = await authService.me();
                const userInfo: User = me as User;
                dispatch({ type: AuthActionType.LOGIN_SUCCESS, payload: userInfo });
            }
        } catch (error) {
            console.error('Login error:', error);
            if (error instanceof InteractionRequiredAuthError) {
                try {
                    await instance.loginRedirect(loginRequest);
                    return;
                } catch (redirectError) {
                    console.error('Redirect error:', redirectError);
                }
            }
            dispatch({ 
                type: AuthActionType.LOGIN_FAILURE, 
                payload: error instanceof Error ? error.message : 'Failed to login'
            });
        }
    };

    const loginWithPassword = async (email: string, password: string) => {
        dispatch({ type: AuthActionType.LOGIN_START });
        try {
            const res = await authService.login(email, password);
            const userInfo: User = res.user;
            dispatch({ type: AuthActionType.LOGIN_SUCCESS, payload: userInfo });
        } catch (e: any) {
            dispatch({ type: AuthActionType.LOGIN_FAILURE, payload: e?.response?.data?.message || 'Invalid credentials' });
            throw e;
        }
    };

    const logout = () => {
        // Clear auth state
        dispatch({ type: AuthActionType.LOGOUT });
        // Clear local token if any
        authService.clearLocalToken();
        // If MSAL account exists, also sign out from AAD
        const account = instance.getActiveAccount();
        if (account) {
            instance.logoutRedirect({ postLogoutRedirectUri: window.location.origin });
        }
    };

    // Load users when an admin logs in
    useEffect(() => {
        if (state.isAuthenticated && state.user?.roles.includes('admin')) {
            loadUsers();
        }
    }, [state.isAuthenticated, state.user]);

    const value = {
        ...state,
    login,
    loginWithPassword,
        logout,
        loadUsers
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
}; 