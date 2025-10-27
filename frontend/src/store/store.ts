import { configureStore } from '@reduxjs/toolkit';
import { TypedUseSelectorHook, useSelector } from 'react-redux';
import { userReducer } from '../reducers/userReducer';
import dataReducer from '../reducers/dataReducer';
import modelReducer from '../reducers/modelReducer';
import clinicalTaskReducer from '../reducers/clinicalTaskReducer';
import testScenarioReducer from '../reducers/testScenarioReducer';
import experimentReducer from '../reducers/experimentReducer';
import arenaReducer from '../reducers/arenaReducer';
import { useDispatch as useReduxDispatch } from 'react-redux';

export const store = configureStore({
    reducer: {
        user: userReducer,
        data: dataReducer,
        models: modelReducer,
        clinicalTasks: clinicalTaskReducer,
        testScenarios: testScenarioReducer,
        experiments: experimentReducer,
        arena: arenaReducer
    },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware({
        serializableCheck: false
    })
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export const useAppDispatch = () => useReduxDispatch<AppDispatch>();
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector; 