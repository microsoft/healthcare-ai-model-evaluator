import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { ITestScenario } from '../types/admin';
import { testScenarioService } from '../services/testScenarioService';

interface TestScenarioState {
    scenarios: ITestScenario[];
    isLoading: boolean;
    error: string | null;
}

const initialState: TestScenarioState = {
    scenarios: [],
    isLoading: false,
    error: null
};

export const fetchScenarios = createAsyncThunk(
    'testScenarios/fetchAll',
    async () => {
        return await testScenarioService.getScenarios();
    }
);

export const addScenario = createAsyncThunk(
    'testScenarios/add',
    async (scenario: Omit<ITestScenario, 'id'>) => {
        return await testScenarioService.createScenario(scenario);
    }
);

export const updateScenario = createAsyncThunk(
    'testScenarios/update',
    async (scenario: ITestScenario) => {
        return await testScenarioService.updateScenario(scenario);
    }
);

export const deleteScenario = createAsyncThunk(
    'testScenarios/delete',
    async (id: string) => {
        await testScenarioService.deleteScenario(id);
        return id;
    }
);

const testScenarioSlice = createSlice({
    name: 'testScenarios',
    initialState,
    reducers: {},
    extraReducers: (builder) => {
        builder
            .addCase(fetchScenarios.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchScenarios.fulfilled, (state, action) => {
                state.scenarios = action.payload;
                state.isLoading = false;
            })
            .addCase(fetchScenarios.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.error.message || 'Failed to fetch scenarios';
            })
            .addCase(addScenario.fulfilled, (state, action) => {
                state.scenarios.push(action.payload);
            })
            .addCase(updateScenario.fulfilled, (state, action) => {
                const index = state.scenarios.findIndex(s => s.id === action.payload.id);
                if (index !== -1) {
                    state.scenarios[index] = action.payload;
                }
            })
            .addCase(deleteScenario.fulfilled, (state, action) => {
                state.scenarios = state.scenarios.filter(s => s.id !== action.payload);
            });
    }
});

export default testScenarioSlice.reducer; 