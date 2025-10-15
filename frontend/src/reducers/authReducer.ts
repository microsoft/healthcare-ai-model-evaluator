import { AuthState, AuthAction, AuthActionType } from '../types/auth.types';

export const initialAuthState: AuthState = {
    user: null,
    isAuthenticated: false,
    error: null,
    loading: false,
    userList: {
        users: [],
        loading: false,
        error: null
    }
};

export const authReducer = (state: AuthState, action: AuthAction): AuthState => {
    switch (action.type) {
        case AuthActionType.LOGIN_START:
            return {
                ...state,
                loading: true,
                error: null,
            };
        case AuthActionType.LOGIN_SUCCESS:
            return {
                ...state,
                user: action.payload,
                isAuthenticated: true,
                loading: false,
                error: null,
            };
        case AuthActionType.AUTH_INITIALIZE:
            return {
                ...state,
                loading: true,
                error: null,
            };
        case AuthActionType.LOGIN_FAILURE:
            return {
                ...state,
                user: null,
                isAuthenticated: false,
                loading: false,
                error: action.payload,
            };
        case AuthActionType.LOGOUT:
            return {
                ...state,
                user: null,
                isAuthenticated: false,
                loading: false,
                error: null,
            };
        case AuthActionType.LOAD_USERS_START:
            return {
                ...state,
                userList: {
                    ...state.userList,
                    loading: true,
                    error: null
                }
            };
        case AuthActionType.LOAD_USERS_SUCCESS:
            return {
                ...state,
                userList: {
                    users: action.payload,
                    loading: false,
                    error: null
                }
            };
        case AuthActionType.LOAD_USERS_FAILURE:
            return {
                ...state,
                userList: {
                    ...state.userList,
                    loading: false,
                    error: action.payload
                }
            };
        default:
            return state;
    }
}; 