import { useMsal } from '@azure/msal-react';
import { loginRequest } from '../config/authConfig';
import { useState, useCallback } from 'react';

export const useAuth = () => {
    const { instance, accounts } = useMsal();
    const [error, setError] = useState<string | null>(null);

    const login = useCallback(async () => {
        try {
            await instance.loginPopup(loginRequest);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'An error occurred during login');
        }
    }, [instance]);

    return {
        login,
        error,
        isAuthenticated: accounts.length > 0,
        user: accounts[0] || null
    };
};
