import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit';
import { IExperiment, ITrial } from '../types/admin';
import { experimentService } from '../services/experimentService';
import { trialService } from '../services/trialService';
import { RootState } from '../store/store';
import exp from 'constants';

interface ArenaState {
    assignedExperiments: IExperiment[];
    activeExperiment: IExperiment | null;
    currentTrial: ITrial | null;
    pendingTrialCounts: {
        [key: string]: number | null;
    };
    isLoading: boolean;
    error: string | null;
    leftPanelWidth: number;
    rightModelsPanelSplit: number;
    noTrialsAvailable: boolean;
    selectedClinicalTaskIds: string[];
    doneTrialIds?: string[];    
}

const initialState: ArenaState = {
    assignedExperiments: [],
    activeExperiment: null,
    currentTrial: null,
    pendingTrialCounts: {
        'Simple Evaluation': null,
        'Simple Validation': null,
        'Arena': null,
        'Full Validation': null
    },
    isLoading: false,
    error: null,
    leftPanelWidth: 50,
    rightModelsPanelSplit: 50,
    noTrialsAvailable: false,
    selectedClinicalTaskIds: [],
    doneTrialIds: []
};

export const fetchAssignedExperiments = createAsyncThunk(
    'arena/fetchAssigned',
    async () => {
        return await experimentService.getAssignedExperiments();
    }
);

export const setActiveExperiment = createAsyncThunk(
    'arena/setActive',
    async (experimentId: string) => {
        const experiment = await experimentService.getExperiment(experimentId);
        return experiment;
    }
);

export const getTrialById = createAsyncThunk(
    'arena/getTrialById',
    async (trialId: string) => {
        const trial = await trialService.getDoneTrialById(trialId);
        return trial;
    }
);

export const fetchDoneTrialIds = createAsyncThunk(
    'arena/fetchDoneTrialIds',
    async (testScenarioIds: string) => {
        return await trialService.getDoneTrialIds([testScenarioIds]);
    }
);

export const fetchPendingTrialCounts = createAsyncThunk(
    'arena/fetchPendingTrialCounts',
    async () => {
        return await trialService.getPendingTrialCounts();
    }
);

export const fetchNextTrial = createAsyncThunk(
    'arena/fetchNextTrial',
    async (testScenarioId: string) => {
        const response = await trialService.getNextPendingTrial([testScenarioId]);
        return response;
    }
);

export const fetchDoneTrial = createAsyncThunk(
    'arena/fetchDoneTrial',
    async ({ testScenarioId, trialId }: { testScenarioId: string; trialId: string }) => {
        const response = await trialService.getDoneTrial([testScenarioId], trialId);
        return response;
    }
);

export const updateTrial = createAsyncThunk(
    'arena/updateTrial',
    async ({ trialId, update, timeSpent }: { 
        trialId: string; 
        update: Partial<ITrial>;
        timeSpent?: number;
    }) => {
        console.log(trialId, update, timeSpent);
        const response = await trialService.updateTrial(trialId, update );
        return response;
    }
);

export const updateTrialFlag = createAsyncThunk(
    'arena/updateTrialFlag',
    async ({ trialId, update, timeSpent }: { 
        trialId: string; 
        update: Partial<ITrial>;
        timeSpent?: number;
    }) => {
        console.log(trialId, update, timeSpent);
        const response = await trialService.updateTrialFlag(trialId, update );
        return response;
    }
);

const arenaSlice = createSlice({
    name: 'arena',
    initialState,
    reducers: {
        clearActiveExperiment: (state) => {
            state.activeExperiment = null;
        },
        clearCurrentTrial: (state) => {
            state.currentTrial = null;
        },
        setClinicalTaskFilter: (state, action: PayloadAction<string[]>) => {
            state.selectedClinicalTaskIds = action.payload;
        }
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchAssignedExperiments.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchAssignedExperiments.fulfilled, (state, action) => {
                state.assignedExperiments = action.payload;
                state.isLoading = false;
            })
            .addCase(fetchAssignedExperiments.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.error.message || 'Failed to fetch assigned experiments';
            })
            .addCase(setActiveExperiment.fulfilled, (state, action) => {
                state.activeExperiment = action.payload;
            })
            .addCase(fetchDoneTrialIds.fulfilled, (state, action) => {
                state.doneTrialIds = action.payload;
            })
            .addCase(fetchDoneTrialIds.rejected, (state, action) => {
                state.error = action.error.message || 'Failed to fetch done trial IDs';
            })
            .addCase(fetchPendingTrialCounts.fulfilled, (state, action) => {
                state.pendingTrialCounts = {
                    'Simple Evaluation': action.payload['Simple Evaluation'],
                    'Simple Validation': action.payload['Simple Validation'],
                    'Arena': action.payload['Arena'],
                    'Full Validation': action.payload['Full Validation']
                };
            })
            .addCase(getTrialById.pending, (state) => {
                state.noTrialsAvailable = false;
                state.error = null;
            })
            .addCase(getTrialById.fulfilled, (state, action) => {
                state.currentTrial = action.payload;
            })
            .addCase(getTrialById.rejected, (state, action) => {
                state.noTrialsAvailable = true;
                state.currentTrial = null;
            })
            .addCase(fetchNextTrial.pending, (state) => {
                state.noTrialsAvailable = false;
                state.error = null;
            })
            .addCase(fetchNextTrial.fulfilled, (state, action) => {
                state.currentTrial = action.payload;
                state.noTrialsAvailable = false;
            })
            .addCase(fetchNextTrial.rejected, (state, action) => {
                if (action.error.message?.includes('404')) {
                    state.noTrialsAvailable = true;
                }
                state.currentTrial = null;
            })
            .addCase(fetchDoneTrial.pending, (state) => {
                state.noTrialsAvailable = false;
                state.error = null;
            })
            .addCase(fetchDoneTrial.fulfilled, (state, action) => {
                state.currentTrial = action.payload;
                state.noTrialsAvailable = false;
            })
            .addCase(fetchDoneTrial.rejected, (state, action) => {
                state.noTrialsAvailable = true;
                state.currentTrial = null;
            })
            .addCase(updateTrial.fulfilled, (state, action) => {
                if (action.payload.status === 'done') {
                    //state.currentTrial = null;
                } else {
                    state.currentTrial = action.payload;
                }
            })
            .addCase(updateTrialFlag.fulfilled, (state, action) => {
                if (state.currentTrial) {
                    console.log('Updating flags in state:', action.payload.flags);
                    state.currentTrial.flags = action.payload.flags;
                }
            });
    }
});

export const { clearActiveExperiment, clearCurrentTrial, setClinicalTaskFilter } = arenaSlice.actions;
export default arenaSlice.reducer; 