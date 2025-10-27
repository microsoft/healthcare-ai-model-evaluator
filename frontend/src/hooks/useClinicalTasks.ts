import { useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../store/store';
import { RootState } from '../store/store';
import { IClinicalTask } from '../types/admin';
import {
    fetchTasks,
    addTask,
    updateTask,
    deleteTask
} from '../reducers/clinicalTaskReducer';

export const useClinicalTasks = () => {
    const dispatch = useAppDispatch();
    const tasks = useAppSelector((state: RootState) => state.clinicalTasks.tasks);
    const isLoading = useAppSelector((state: RootState) => state.clinicalTasks.isLoading);
    const error = useAppSelector((state: RootState) => state.clinicalTasks.error);

    const loadTasks = useCallback(async () => {
        try {
            await dispatch(fetchTasks()).unwrap();
        } catch (error) {
            console.error('Failed to fetch tasks:', error);
        }
    }, [dispatch]);

    const addNewTask = useCallback(async (task: Omit<IClinicalTask, 'id'>) => {
        try {
            await dispatch(addTask(task)).unwrap();
        } catch (error) {
            console.error('Failed to add task:', error);
            throw error;
        }
    }, [dispatch]);

    const updateExistingTask = useCallback(async (task: IClinicalTask) => {
        try {
            await dispatch(updateTask(task)).unwrap();
        } catch (error) {
            console.error('Failed to update task:', error);
            throw error;
        }
    }, [dispatch]);

    const deleteExistingTask = useCallback(async (id: string) => {
        try {
            await dispatch(deleteTask(id)).unwrap();
        } catch (error) {
            console.error('Failed to delete task:', error);
            throw error;
        }
    }, [dispatch]);

    return {
        tasks,
        loading: isLoading,
        error,
        fetchTasks: loadTasks,
        addTask: addNewTask,
        updateTask: updateExistingTask,
        deleteTask: deleteExistingTask
    };
};

export {}; 