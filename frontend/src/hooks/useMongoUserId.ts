import { useState, useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { userService } from '../services/userService';
import { useAppDispatch, useAppSelector } from '../store/store';
import { setMongoUserId } from '../reducers/userReducer';

export const useMongoUserId = () => {
    const { accounts } = useMsal();
    const dispatch = useAppDispatch();
    const mongoUserId = useAppSelector(state => state.user.mongoUserId);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchMongoUserId = async () => {
            if (accounts[0]?.username && !mongoUserId) {
                try {
                    const user = await userService.getUserByEmail(accounts[0].username);
                    if (user?.id) {
                        dispatch(setMongoUserId(user.id));
                    }
                } catch (err) {
                    setError('Failed to fetch MongoDB user ID');
                    console.error('Error fetching MongoDB user ID:', err);
                } finally {
                    setIsLoading(false);
                }
            } else {
                setIsLoading(false);
            }
        };

        fetchMongoUserId();
    }, [accounts, dispatch, mongoUserId]);

    return { mongoUserId, isLoading, error };
}; 