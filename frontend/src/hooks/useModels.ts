import { useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../store/store';
import { RootState } from '../store/store';
import { IModel } from '../types/admin';
import {
    fetchModels,
    addModel,
    updateModel,
    deleteModel,
    testIntegration
} from '../reducers/modelReducer';

export const useModels = () => {
    const dispatch = useAppDispatch();
    const models = useAppSelector((state: RootState) => state.models.models);
    const isLoading = useAppSelector((state: RootState) => state.models.isLoading);
    const error = useAppSelector((state: RootState) => state.models.error);
    const testIntegrationStatus = useAppSelector((state: RootState) => state.models.testIntegrationStatus || '');

    const loadModels = useCallback(async () => {
        try {
            await dispatch(fetchModels()).unwrap();
        } catch (error) {
            console.error('Failed to fetch models:', error);
        }
    }, [dispatch]);

    const addNewModel = useCallback(async (model: Omit<IModel, 'id'>) => {
        try {
            await dispatch(addModel(model)).unwrap();
        } catch (error) {
            console.error('Failed to add model:', error);
            throw error;
        }
    }, [dispatch]);

    const updateExistingModel = useCallback(async (model: IModel) => {
        try {
            await dispatch(updateModel(model)).unwrap();
        } catch (error) {
            console.error('Failed to update model:', error);
            throw error;
        }
    }, [dispatch]);

    const deleteExistingModel = useCallback(async (id: string) => {
        try {
            await dispatch(deleteModel(id)).unwrap();
        } catch (error) {
            console.error('Failed to delete model:', error);
            throw error;
        }
    }, [dispatch]);

    const testModelIntegration = useCallback(async (id: string) => {
        try {
            await dispatch(testIntegration(id)).unwrap();
        } catch (error) {
            console.error('Failed to test integration:', error);
            throw error;
        }
    }, [dispatch]);

    return {
        models,
        loading: isLoading,
        error,
        testIntegrationStatus,
        fetchModels: loadModels,
        addModel: addNewModel,
        updateModel: updateExistingModel,
        deleteModel: deleteExistingModel,
        testIntegration: testModelIntegration
    };
};

export {}; 