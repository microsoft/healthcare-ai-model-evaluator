import React, { useState, useEffect } from 'react';
import {
    Stack,
    DetailsList,
    Text,
    IColumn,
    SelectionMode,
    DetailsListLayoutMode,
    Spinner,
    SpinnerSize,
    MessageBar,
    MessageBarType,
    ITheme,
    mergeStyles,
    useTheme,
    IStackTokens,
    
    Toggle,
    
} from '@fluentui/react';
import { useLocation } from 'react-router-dom';
import { SingleEvaluation } from './SingleEvaluation';
import { ArenaAB } from './ArenaAB';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { fetchNextTrial, clearCurrentTrial, fetchDoneTrial } from '../../reducers/arenaReducer';
import { trialService } from '../../services/trialService';
import { testScenarioService } from '../../services/testScenarioService';
import { ITestScenario } from '../../types/admin';
 

const stackTokens: IStackTokens = {
    childrenGap: 45,
    padding: 40
    
};

// Removed unused DashboardTileProps interface



const getTileStyles = (theme: ITheme) => mergeStyles({
    width: 200,
    minHeight: 50,
    padding: '0px 20px 20px 20px',
    margin: 10,
    backgroundColor: theme.palette.white,
    boxShadow: theme.effects.elevation4,
    borderRadius: theme.effects.roundedCorner2,
    transition: 'all 0.2s ease-in-out',
    selectors: {
        ':hover': {
            boxShadow: theme.effects.elevation8,
            '.icon': {
                color: theme.palette.themePrimary,
                transform: 'scale(1.1)',
            }
        }
    }
});

const getTrialStyles = (theme: ITheme) => mergeStyles({
    transition: 'all 0.2s ease-in-out',
    width: 'calc(100%)',  
    textAlign: 'right',
    padding: '6px 2px 6px 2px',
    boxSizing: 'border-box',
    borderBottom: '1px solid ' + theme.palette.neutralLight,
    selectors: {
        ':hover': {
            backgroundColor: theme.palette.neutralLighter,
            borderBottom: '1px solid ' + theme.palette.themePrimary,
            cursor: 'pointer',
        }
    }
});

const getLineTrialStyles = (theme: ITheme) => mergeStyles({
    transition: 'all 0.2s ease-in-out',
    width: 'calc(100%)',  
    textAlign: 'center',
    boxSizing: 'border-box',
    padding: '6px 2px 9px 2px',
    margin: '-6px 0px -10px',
    border: '1px solid ' + theme.palette.neutralPrimary,
    color: "#FFFFFF",
    fontWeight: 600,    
    backgroundColor: theme.palette.themeDarkAlt,
    selectors: {
        ':hover': {
            border: '1px solid ' + theme.palette.themePrimary,
            color: theme.palette.themeDark,
            backgroundColor: theme.palette.neutralLighterAlt    ,
            cursor: 'pointer',
        }
    }
});
// Removed unused iconStyles




export interface TestScenarioItem {
    id: string;
    name: string;
    description?: string;
    pendingTrials: number;
    completedTrials: number;
    totalTime?: number; // Optional, can be used for additional stats
}

type ArenaView = 'main' | 'simple-eval' | 'simple-valid' | 'arena-ab' | 'full-valid' | 'single-eval';

export const Arena: React.FC = () => {
    const theme = useTheme();
    const [currentView, setCurrentView] = useState<ArenaView>('main');
    const [testScenarios, setTestScenarios] = useState<TestScenarioItem[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [displayMode, setDisplayMode] = useState<'grid' | 'list'>('grid');
    //const [currentTrial, setCurrentTrial] = useState<any>(null); // Replace 'any' with your trial type
    const [currentTestScenario, setCurrentTestScenario] = useState<ITestScenario | null>(null);
    const [fullTestScenarios, setFullTestScenarios] = useState<ITestScenario[]>([]);
    const currentTrial = useAppSelector((state) => state.arena.currentTrial);
    const noTrialsAvailable = useAppSelector((state) => state.arena.noTrialsAvailable);
    const [loadingMessage, setLoadingMessage] = useState('Loading Experiments...');
    const [inDoneTrialMode, setInDoneTrialMode] = useState(false);
    const dispatch = useAppDispatch();

    const columns: IColumn[] = [
        {
            key: 'name',
            name: 'Test Scenario',
            fieldName: 'name',
            minWidth: 200,
            maxWidth: 400,
            isResizable: true
        },
        {
            key: 'description',
            name: 'Description',
            fieldName: 'description',
            minWidth: 200,
            maxWidth: 400,
            isResizable: true
        },
        {
            key: 'pendingTrials',
            name: 'Pending Trials',
            fieldName: 'pendingTrials',
            minWidth: 100,
            maxWidth: 150,
            isResizable: true,
            onRender: (item: TestScenarioItem) => (
                <>
                    {item.pendingTrials > 0 && (
                        <div onClick={() => handleScenarioClick(item)} className={getLineTrialStyles(theme)}>
                            {item.pendingTrials}
                        </div>
                    )}
                    {item.pendingTrials === 0 && (
                        <div style={{textAlign: 'center', color: theme.palette.neutralTertiary}}>
                            {item.pendingTrials}
                        </div>
                    )}
                </>
            )
        },
        {
            key: 'completedTrials',
            name: 'Completed Trials',
            fieldName: 'completedTrials',
            minWidth: 100,
            maxWidth: 150,
            isResizable: true,
            onRender: (item: TestScenarioItem) => (
                <>

                    {item.completedTrials > 0 && (
                        <div onClick={() => handleShowCompletedTrials(item)} className={getLineTrialStyles(theme)}>
                            {item.completedTrials}
                        </div>
                    )}
                    {item.completedTrials === 0 && (
                        <div style={{textAlign: 'center', color: theme.palette.neutralTertiary}}>
                            {item.completedTrials}
                        </div>
                    )}
                </>
            )
        },
        {
            key: 'totalTimeSeconds',
            name: 'Total Time',
            fieldName: 'totalTime',
            onRender: (item: TestScenarioItem) => {
                if (item.totalTime === undefined) return '-';
                const hours = Math.floor(item.totalTime / 3600);
                const minutes = Math.floor((item.totalTime % 3600) / 60);
                const seconds = item.totalTime % 60;
                if(hours === 0 && minutes === 0) return `${Math.round(seconds)}s`;
                if(hours === 0) return `${Math.round(minutes)}m ${Math.round(seconds)}s`;
                if(minutes === 0) return `${Math.round(hours)}h ${Math.round(seconds)}s`;
                if(seconds === 0) return `${Math.round(hours)}h ${Math.round(minutes)}m`;
                return `${Math.round(hours)}h ${Math.round(minutes)}m ${Math.round(seconds)}s`;
            },
            minWidth: 100,
            maxWidth: 150,
        }
    ];

    useEffect(() => {
        const loadData = async () => {
            try {
                setIsLoading(true);
                const scenarios = await testScenarioService.getScenarios();
                setFullTestScenarios(scenarios);
                // Get stats for each test scenario in parallel
                const stats = await trialService.getTrialStats();
                var scenariosWithStats: TestScenarioItem[] = [];
                stats.forEach(stat => {
                    const scenario = scenarios.find((s: any) => s.id === stat.testScenarioId);
                    if (scenario && (stat.completedCount > 0 || stat.pendingCount > 0)) { // Only include scenarios with stats
                        scenariosWithStats.push({
                            id: scenario.id,
                            name: scenario.name,
                            description: scenario.description || '',
                            pendingTrials: stat.pendingCount || 0,
                            completedTrials: stat.completedCount || 0,
                            totalTime: stat.totalTimeSeconds || 0
                        });
                    }
                });
                
                setTestScenarios(scenariosWithStats);
            } catch (err) {
                setError('Failed to load experiments and trial statistics');
                console.error('Error loading data:', err);
            } finally {
                setIsLoading(false);
            }
        };

        loadData();
    }, [currentTrial]);

    // Component will always start in main view unless a trial is active
    // const location = useLocation(); // unused
    
    useEffect(() => {
        if (currentTrial) {
            // Set view based on the trial's experiment type
            const experimentType = currentTrial.experimentType;
            switch (experimentType) {
                case 'Simple Evaluation':
                    setCurrentView('single-eval');
                    break;
                case 'Simple Validation':
                    setCurrentView('single-eval');
                    break;
                case 'Arena':
                    setCurrentView('arena-ab');
                    break;
                case 'Full Validation':
                    setCurrentView('single-eval');
                    break;
                case 'Single Evaluation':
                    setCurrentView('single-eval'); 
                    break;
                default:
                    setCurrentView('main');
            }
        } else {
            if( ! noTrialsAvailable) {
                console.log('No trials available, setting to main view');
                setCurrentView('main');
                
            }
        }
    }, [currentTrial, noTrialsAvailable]);

    const handleBack = () => {
        setCurrentView('main');
        //setCurrentTrial(null);
        dispatch(clearCurrentTrial());
        setCurrentTestScenario(null);
        //navigate('/arena', { state: {} });
        // Remove trial from history state so refresh doesn't reload previous trial
    
    };
    const timeout = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));
    const handleScenarioClick = async (item: TestScenarioItem) => {
        try {
            setInDoneTrialMode(false);
            setLoadingMessage(`Starting trial for ${item.name}...`);
            setIsLoading(true);
            await timeout( 1000); // Simulate loading delay
            const trial = await dispatch(fetchNextTrial(item.id)).unwrap();
            setIsLoading(false);
            setLoadingMessage('Loading Experiments...');
            if (trial) {
                // Navigate to arena with the trial data
                //setCurrentTrial(trial);
                var testScenario = fullTestScenarios.find(s => s.id === item.id);
                setCurrentTestScenario(testScenario?? null);
               
            }
        } catch (error) {
            console.error('Error starting trial:', error);
            setIsLoading(false);
            setLoadingMessage('Loading Experiments...');
        }
    };

    const handleShowCompletedTrials = async (scenario: TestScenarioItem) => {
        // Show completed trials for the selected scenario
        try {
            setLoadingMessage(`Reviewing trial for ${scenario.name}...`);
            setIsLoading(true);
            await timeout( 1000); // Simulate loading delay
            const trial = await dispatch(fetchDoneTrial({ testScenarioId: scenario.id, trialId: '' })).unwrap();
            setIsLoading(false);
            setLoadingMessage('Loading Experiments...');
            setInDoneTrialMode(true)
            if (trial) {
                // Navigate to arena with the trial data
                //setCurrentTrial(trial);
                var testScenario = fullTestScenarios.find(s => s.id === scenario.id);
                setCurrentTestScenario(testScenario?? null);
               
            }
        } catch (error) {
            console.error('Error starting trial:', error);
            setIsLoading(false);
            setLoadingMessage('Loading Experiments...');
        }
    };

    const renderContent = () => {

        if (currentView !== 'main') {
            switch (currentView) {
                case 'arena-ab':
                    return <ArenaAB onBack={handleBack} testScenario={currentTestScenario} inDoneTrialMode={inDoneTrialMode} />;
                case 'single-eval':
                    return <SingleEvaluation onBack={handleBack} testScenario={currentTestScenario} inDoneTrialMode={inDoneTrialMode} />;
            }
        }
        
        return (
            <Stack tokens={{ childrenGap: 20 }}>
                <Stack.Item align="center" styles={{ root: { marginBottom: 20 } }}>
                    <Text variant="xxLarge">Arena</Text>
                </Stack.Item>
                <Stack.Item align='center' styles={{ root: { maxWidth: 900, textAlign: 'center' } }}>
                    <Text variant="large">Welcome to the Healthcare AI Model Evaluator Arena. Choose an experiment to begin testing and validating AI models.</Text>
                    <div style={{ position: 'absolute', top: 85, left: '100%', marginLeft: -120 }}>
                       <Toggle
                            label="List/Grid"
                            onChange={(_, checked) => setDisplayMode(checked ? 'grid' : 'list')}
                            checked={displayMode === 'grid'}
                        />
                        </div>
                </Stack.Item>
                {isLoading ? (
                    <Stack.Item>
                        <Spinner size={SpinnerSize.large} label={loadingMessage} />
                    </Stack.Item>
                ) : error ? (
                    <Stack.Item>
                        <MessageBar messageBarType={MessageBarType.error}>
                            {error}
                        </MessageBar>
                    </Stack.Item>
                ) : (
                    <Stack.Item align='center' style={{ width: '100%' }}>
                        {displayMode === 'grid' ? (
                            <Stack  horizontal horizontalAlign='center' wrap styles={{ root: { width: '100%', alignItems: 'flex-center' } }} tokens={stackTokens}>
                                {testScenarios.map(scenario => (
                                    <Stack.Item
                                        className={getTileStyles(theme)}
                                        key={scenario.id}
                                        styles={{ root: { width: '300px', alignSelf: 'flex-start', height: 'auto' } }} // <-- alignSelf and height:auto
                                    >
                                        <Stack.Item align="start" style={{ 
                                            position: 'relative',
                                                    boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)', 
                                                    marginBottom: 15, 
                                                    top: 0, 
                                                    paddingTop: 40, 
                                                    paddingBottom: 20, 
                                                    width: 'calc(100% + 30px)', 
                                                    marginLeft: -20, 
                                                    textAlign: 'left', 
                                                    backgroundColor: theme.palette.neutralLighterAlt, 
                                                    padding: '5px',
                                                    borderRadius: '0px 0px 4px 0px' }
                                                    }>
                                                <Text variant="large" style={{ padding: 4, display: 'inline-block', fontWeight: 600, lineHeight: '25px' }}>{scenario.name}</Text>
                                            </Stack.Item>
                                        {scenario.description && (
                                            <Stack style={{ marginTop: 0 }}>
                                                <Stack.Item>
                                                    <Text variant="medium" style={{ textAlign: "left", display: 'inline-block' }}>{scenario.description}</Text>
                                                    <br />
                                                    <div style={{ width: "100%", borderBottom: `1px solid ${theme.palette.neutralLight}`, margin: '10px 0 0px' }}></div>
                                                </Stack.Item>
                                            </Stack>
                                        )}
                                        { scenario.pendingTrials > 0 && (
                                            <Stack verticalAlign="center" styles={{ root: { padding: '0 0px' } }}>
                                            <Stack.Item className={getTrialStyles(theme)} onClick={()=>{handleScenarioClick(scenario)}} style={{ paddingBottom: '4px' }} align='end'>
                                                
                                                    <Text variant="small">Pending Trials: <span style={{
                                                        color: theme.palette.white,
                                                        backgroundColor: theme.palette.accent,
                                                        borderRadius: '7px',
                                                        display: "inline-block",
                                                        verticalAlign: 'top',
                                                        marginTop: '2px',
                                                        minWidth: '30px',
                                                        textAlign: 'center',
                                                        padding: '1px 4px 2px',
                                                        fontWeight: 600,
                                                    }}>{scenario.pendingTrials}</span></Text>
                                            </Stack.Item>
                                            { scenario.completedTrials > 0 && (
                                                <Stack.Item  className={getTrialStyles(theme)}  onClick={() => handleShowCompletedTrials(scenario)} align="end">
                                                   <Text variant="small" >Completed Trials: <span style={{
                                                        backgroundColor: theme.palette.neutralLight,
                                                        borderRadius: '7px',
                                                        display: "inline-block",
                                                        verticalAlign: 'top',
                                                        marginTop: '2px',
                                                        minWidth: '30px',       
                                                        textAlign: 'center',
                                                        padding: '1px 4px 2px',
                                                        fontWeight: 600
                                                    }}>{scenario.completedTrials}</span></Text><br />
                                                </Stack.Item>
                                            )}
                                            
                                            </Stack>
                                        )}
                                        { scenario.pendingTrials === 0 && (
                                            <Stack verticalAlign="center" styles={{ root: { padding: '0 0px' } }}>
                                                <Stack.Item className={getTrialStyles(theme)}   align="end">
                                                       <Text variant="small"  onClick={() => handleShowCompletedTrials(scenario)}>Completed Trials: <span style={{
                                                            backgroundColor: theme.palette.neutralLight,
                                                            borderRadius: '7px',
                                                            display: "inline-block",
                                                            verticalAlign: 'top',
                                                        marginTop: '2px',
                                                        minWidth: '30px',
                                                        textAlign: 'center',
                                                        padding: '1px 4px 2px',
                                                        fontWeight: 600
                                                    }}>{scenario.completedTrials}</span></Text><br />
                                                </Stack.Item>
                                            </Stack>
                                        )}
                                        
                                    </Stack.Item>
                                ))}
                            </Stack>
                        ) : null}
                        {displayMode === 'list' ? (
                             <DetailsList
                                items={testScenarios}
                                columns={columns}
                                selectionMode={SelectionMode.none}
                                onRenderRow={(props, defaultRender) => {
                                    if (props) {
                                        return (
                                            <div >
                                                {defaultRender?.(props)}
                                            </div>
                                        );
                                    }
                                    return null;
                                }}
                                layoutMode={DetailsListLayoutMode.justified}
                                isHeaderVisible={true}
                            />
                        ) : null}
                       
                    </Stack.Item>
                )}
            </Stack>
        );
    };

    return (
        <div style={{
            padding: '20px',
            height: '100%',
            overflow: currentView === 'main' ? 'visible' : 'hidden'
        }}>
            {renderContent()}
        </div>
    );
};

export {};
