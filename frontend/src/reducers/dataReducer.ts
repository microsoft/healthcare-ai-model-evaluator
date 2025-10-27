import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import { dataSetService } from '../services/dataSetService';
import { DataSet, CreateDataSetRequest, UpdateDataSetRequest } from '../types/dataset';

interface DataState {
    datasets: DataSet[];
    error: string | null;
}

const initialState: DataState = {
    datasets: [],
    error: null
};

export const fetchDataSets = createAsyncThunk(
    'data/fetchAll',
    async () => {
        const response = await dataSetService.getDataSets();
        return response;
    }
);

export const createDataSet = createAsyncThunk(
    'data/create',
    async (dataset: CreateDataSetRequest) => {
        const response = await dataSetService.addDataSet(dataset);
        return response;
    }
);

export const editDataSet = createAsyncThunk(
    'data/edit',
    async (dataset: UpdateDataSetRequest) => {
        const response = await dataSetService.updateDataSet(dataset);
        return response;
    }
);

export const removeDataSet = createAsyncThunk(
    'data/remove',
    async (id: string) => {
        await dataSetService.deleteDataSet(id);
        return id;
    }
);

const dataSlice = createSlice({
    name: 'data',
    initialState,
    reducers: {
        setDataSets: (state, action) => {
            state.datasets = action.payload;
        }
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchDataSets.fulfilled, (state, action) => {
                state.datasets = action.payload;
                state.error = null;
            })
            .addCase(fetchDataSets.rejected, (state, action) => {
                state.error = action.error.message || 'Failed to fetch datasets';
            })
            .addCase(createDataSet.fulfilled, (state, action) => {
                state.datasets.push(action.payload);
                state.error = null;
            })
            .addCase(createDataSet.rejected, (state, action) => {
                state.error = action.error.message || 'Failed to create dataset';
            })
            .addCase(editDataSet.fulfilled, (state, action) => {
                const index = state.datasets.findIndex(d => d.id === action.payload.id);
                if (index !== -1) {
                    state.datasets[index] = action.payload;
                }
                state.error = null;
            })
            .addCase(editDataSet.rejected, (state, action) => {
                state.error = action.error.message || 'Failed to update dataset';
            })
            .addCase(removeDataSet.fulfilled, (state, action) => {
                state.datasets = state.datasets.filter(d => d.id !== action.payload);
                state.error = null;
            })
            .addCase(removeDataSet.rejected, (state, action) => {
                state.error = action.error.message || 'Failed to delete dataset';
            });
    }
});

export const { setDataSets } = dataSlice.actions;
export default dataSlice.reducer; 