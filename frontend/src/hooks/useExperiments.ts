import { useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../store/store';
import { IExperiment } from '../types/admin';
import {
    fetchExperiments,
    addExperiment,
    updateExperiment,
    deleteExperiment
} from '../reducers/experimentReducer';

export const useExperiments = () => {
    const dispatch = useAppDispatch();
    const experiments = useAppSelector((state) => state.experiments.experiments);
    const isLoading = useAppSelector((state) => state.experiments.isLoading);
    const error = useAppSelector((state) => state.experiments.error);

    const loadExperiments = useCallback(async () => {
        try {
            await dispatch(fetchExperiments()).unwrap();
        } catch (error) {
            console.error('Failed to fetch experiments:', error);
        }
    }, [dispatch]);

    const addNewExperiment = useCallback(async (experiment: Omit<IExperiment, 'id'>) => {
        try {
            await dispatch(addExperiment(experiment)).unwrap();
        } catch (error) {
            console.error('Failed to add experiment:', error);
            throw error;
        }
    }, [dispatch]);

    const updateExistingExperiment = useCallback(async (experiment: IExperiment) => {
        try {
            await dispatch(updateExperiment(experiment)).unwrap();
        } catch (error) {
            console.error('Failed to update experiment:', error);
            throw error;
        }
    }, [dispatch]);

    const deleteExistingExperiment = useCallback(async (id: string) => {
        try {
            await dispatch(deleteExperiment(id)).unwrap();
        } catch (error) {
            console.error('Failed to delete experiment:', error);
            throw error;
        }
    }, [dispatch]);

    return {
        experiments,
        loading: isLoading,
        error,
        fetchExperiments: loadExperiments,
        addExperiment: addNewExperiment,
        updateExperiment: updateExistingExperiment,
        deleteExperiment: deleteExistingExperiment
    };
}; 