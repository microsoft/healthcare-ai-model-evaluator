import { useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../store/store';
import { ITestScenario } from '../types/admin';
import {
    fetchScenarios,
    addScenario,
    updateScenario,
    deleteScenario
} from '../reducers/testScenarioReducer';

export const useTestScenarios = () => {
    const dispatch = useAppDispatch();
    const scenarios = useAppSelector((state) => state.testScenarios.scenarios);
    const isLoading = useAppSelector((state) => state.testScenarios.isLoading);
    const error = useAppSelector((state) => state.testScenarios.error);

    const loadScenarios = useCallback(async () => {
        try {
            await dispatch(fetchScenarios()).unwrap();
        } catch (error) {
            console.error('Failed to fetch scenarios:', error);
        }
    }, [dispatch]);

    const addNewScenario = useCallback(async (scenario: Omit<ITestScenario, 'id'>) => {
        try {
            await dispatch(addScenario(scenario)).unwrap();
        } catch (error) {
            console.error('Failed to add scenario:', error);
            throw error;
        }
    }, [dispatch]);

    const updateExistingScenario = useCallback(async (scenario: ITestScenario) => {
        try {
            await dispatch(updateScenario(scenario)).unwrap();
        } catch (error) {
            console.error('Failed to update scenario:', error);
            throw error;
        }
    }, [dispatch]);

    const deleteExistingScenario = useCallback(async (id: string) => {
        try {
            await dispatch(deleteScenario(id)).unwrap();
        } catch (error) {
            console.error('Failed to delete scenario:', error);
            throw error;
        }
    }, [dispatch]);

    return {
        scenarios,
        loading: isLoading,
        error,
        fetchScenarios: loadScenarios,
        addScenario: addNewScenario,
        updateScenario: updateExistingScenario,
        deleteScenario: deleteExistingScenario
    };
};

export {}; 