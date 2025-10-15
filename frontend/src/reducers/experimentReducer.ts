import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { IExperiment, ExperimentStatus } from '../types/admin';
import { experimentService } from '../services/experimentService';

interface ExperimentState {
    experiments: IExperiment[];
    isLoading: boolean;
    error: string | null;
}

const initialState: ExperimentState = {
    experiments: [],
    isLoading: false,
    error: null
};

// Create thunks
const fetchExperiments = createAsyncThunk(
    'experiments/fetchAll',
    async () => {
        const response = await experimentService.getExperiments();
        return response;
    }
);

const addExperiment = createAsyncThunk(
    'experiments/add',
    async (experiment: Omit<IExperiment, 'id'>) => {
        return await experimentService.createExperiment(experiment);
    }
);

const updateExperiment = createAsyncThunk(
    'experiments/update',
    async (experiment: IExperiment) => {
        return await experimentService.updateExperiment(experiment);
    }
);

const deleteExperiment = createAsyncThunk(
    'experiments/delete',
    async (id: string) => {
        await experimentService.deleteExperiment(id);
        return id;
    }
);

const processExperiment = createAsyncThunk<IExperiment, string>(
    'experiments/process',
    async (id) => {
        const response = await experimentService.processExperiment(id);
        return response;
    }
);

const updateExperimentStatus = createAsyncThunk<IExperiment, { id: string; status: ExperimentStatus }>(
    'experiments/updateStatus',
    async ({ id, status }) => {
        const response = await experimentService.updateStatus(id, status);
        return response;
    }
);

// Create slice
const experimentSlice = createSlice({
    name: 'experiments',
    initialState,
    reducers: {
        setExperiments: (state, action) => {
            state.experiments = action.payload;
        },
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchExperiments.pending, (state) => {
                if (state.experiments.length === 0) {
                    state.isLoading = true;
                }
            })
            .addCase(fetchExperiments.fulfilled, (state, action) => {
                state.isLoading = false;
                const updatedExperiments = action.payload;
                state.experiments = state.experiments.map(existingExp => {
                    const updatedExp = updatedExperiments.find(e => e.id === existingExp.id);
                    if (updatedExp && 
                        (existingExp.processingStatus === 'Processing' || 
                         existingExp.processingStatus === 'Finalizing')) {
                        return {
                            ...existingExp,
                            processingStatus: updatedExp.processingStatus,
                            status: updatedExp.status
                        };
                    }
                    return existingExp;
                });
                
                const existingIds = new Set(state.experiments.map(e => e.id));
                const newExperiments = updatedExperiments.filter(e => !existingIds.has(e.id));
                state.experiments.push(...newExperiments);
            })
            .addCase(fetchExperiments.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.error.message || 'Failed to fetch experiments';
            })
            .addCase(addExperiment.fulfilled, (state, action) => {
                state.experiments.push(action.payload);
            })
            .addCase(updateExperiment.fulfilled, (state, action) => {
                const index = state.experiments.findIndex(e => e.id === action.payload.id);
                if (index !== -1) {
                    state.experiments[index] = action.payload;
                }
            })
            .addCase(deleteExperiment.fulfilled, (state, action) => {
                state.experiments = state.experiments.filter(e => e.id !== action.payload);
            })
            .addCase(processExperiment.fulfilled, (state, action) => {
                const index = state.experiments.findIndex(e => e.id === action.payload.id);
                if (index !== -1) {
                    state.experiments[index] = action.payload;
                }
            })
            .addCase(updateExperimentStatus.fulfilled, (state, action) => {
                const index = state.experiments.findIndex(e => e.id === action.payload.id);
                if (index !== -1) {
                    state.experiments[index] = action.payload;
                }
            });
    }
});

// Export everything
export {
    fetchExperiments,
    addExperiment,
    updateExperiment,
    deleteExperiment,
    processExperiment,
    updateExperimentStatus,
};

// Export actions and reducer correctly
export const { setExperiments } = experimentSlice.actions;
export const { reducer } = experimentSlice;
export default reducer;