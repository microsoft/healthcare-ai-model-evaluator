import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { IModel } from '../types/admin';
import { modelService } from '../services/modelService';

interface ModelState {
    models: IModel[];
    isLoading: boolean;
    error: string | null;
    testIntegrationStatus: string | null;
}

const initialState: ModelState = {
    models: [],
    isLoading: false,
    error: null,
    testIntegrationStatus: null
};

export const fetchModels = createAsyncThunk(
    'models/fetchAll',
    async () => {
        return await modelService.getModels();
    }
);

export const addModel = createAsyncThunk(
    'models/add',
    async (model: Omit<IModel, 'id'>) => {
        return await modelService.createModel(model);
    }
);

export const updateModel = createAsyncThunk(
    'models/update',
    async (model: IModel) => {
        return await modelService.updateModel(model);
    }
);

export const deleteModel = createAsyncThunk(
    'models/delete',
    async (id: string) => {
        await modelService.deleteModel(id);
        return id;
    }
);

export const testIntegration = createAsyncThunk(
    'models/testIntegration',
    async (id: string) => {
        return await modelService.testIntegration(id);
    }
);
const modelSlice = createSlice({
    name: 'models',
    initialState,
    reducers: {},
    extraReducers: (builder) => {
        builder
            .addCase(fetchModels.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchModels.fulfilled, (state, action) => {
                state.models = action.payload;
                state.isLoading = false;
            })
            .addCase(fetchModels.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.error.message || 'Failed to fetch models';
            })
            .addCase(addModel.fulfilled, (state, action) => {
                state.models.push(action.payload);
            })
            .addCase(updateModel.fulfilled, (state, action) => {
                const index = state.models.findIndex(m => m.id === action.payload.id);
                if (index !== -1) {
                    state.models[index] = action.payload;
                }
            })
            .addCase(deleteModel.fulfilled, (state, action) => {
                state.models = state.models.filter(m => m.id !== action.payload);
            })
            .addCase(testIntegration.pending, (state) => {
                state.testIntegrationStatus = 'pending';
            })
            .addCase(testIntegration.fulfilled, (state, action) => {
                state.testIntegrationStatus = action.payload;
            })
            .addCase(testIntegration.rejected, (state, action) => {
                state.testIntegrationStatus = 'error';
            });
    }
});

export default modelSlice.reducer; 