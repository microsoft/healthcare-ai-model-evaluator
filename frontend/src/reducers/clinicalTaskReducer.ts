import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { IClinicalTask } from '../types/admin';
import { clinicalTaskService } from '../services/clinicalTaskService';

interface ClinicalTaskState {
    tasks: IClinicalTask[];
    isLoading: boolean;
    error: string | null;
}

const initialState: ClinicalTaskState = {
    tasks: [],
    isLoading: false,
    error: null
};

export const fetchTasks = createAsyncThunk(
    'clinicalTasks/fetchAll',
    async () => {
        return await clinicalTaskService.getTasks();
    }
);

export const addTask = createAsyncThunk(
    'clinicalTasks/add',
    async (task: Omit<IClinicalTask, 'id'>) => {
        return await clinicalTaskService.createTask(task);
    }
);

export const updateTask = createAsyncThunk(
    'clinicalTasks/update',
    async (task: IClinicalTask) => {
        return await clinicalTaskService.updateTask(task);
    }
);

export const deleteTask = createAsyncThunk(
    'clinicalTasks/delete',
    async (id: string) => {
        await clinicalTaskService.deleteTask(id);
        return id;
    }
);

const clinicalTaskSlice = createSlice({
    name: 'clinicalTasks',
    initialState,
    reducers: {},
    extraReducers: (builder) => {
        builder
            .addCase(fetchTasks.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchTasks.fulfilled, (state, action) => {
                state.tasks = action.payload;
                state.isLoading = false;
            })
            .addCase(fetchTasks.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.error.message || 'Failed to fetch tasks';
            })
            .addCase(addTask.fulfilled, (state, action) => {
                state.tasks.push(action.payload);
            })
            .addCase(updateTask.fulfilled, (state, action) => {
                const index = state.tasks.findIndex(t => t.id === action.payload.id);
                if (index !== -1) {
                    state.tasks[index] = action.payload;
                }
            })
            .addCase(deleteTask.fulfilled, (state, action) => {
                state.tasks = state.tasks.filter(t => t.id !== action.payload);
            });
    }
});

export default clinicalTaskSlice.reducer; 