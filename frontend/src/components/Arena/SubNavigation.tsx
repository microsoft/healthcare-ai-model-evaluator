import React, { useEffect, useState } from 'react';
import { Stack, CommandBar, ICommandBarItemProps, Text, IconButton, TooltipHost } from '@fluentui/react';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { fetchDoneTrialIds, getTrialById } from '../../reducers/arenaReducer';
import { trialService } from '../../services/trialService';
import { ITestScenario, ITrial } from '../../types/admin';
import {TestScenarioItem} from './Arena';
// Removed unused IClinicalTaskStats interface


export interface SubNavigationProps {
    onBack: () => void;
    onSave: () => void;
    onNewChart: () => void;
    arenaType: 'A/B Testing' | 'Simple Evaluation' | 'Simple Validation' | 'Full Validation' | 'Single Evaluation';
    onRefreshChart?: () => void;
    testScenarioId?: string;
    noTrialsAvailable?: boolean;
    testScenario?: ITestScenario;
    testScenarioName?: string;
    trial?: ITrial;
    inDoneTrialMode?: boolean;
}
const loadScenarioWithStats = async (testScenario: ITestScenario) => {
    const stats = await trialService.getTrialStats();
    var scenariosWithStats: TestScenarioItem | null = null;
    stats.forEach(stat => {
        if (stat.testScenarioId === testScenario.id) {
            scenariosWithStats = ({
                id: testScenario.id,
                name: testScenario.name,
                description: testScenario.description || '',
                pendingTrials: stat.pendingCount || 0,
                completedTrials: stat.completedCount || 0,
                totalTime: stat.totalTimeSeconds || 0
            });
        }
    });
    return scenariosWithStats;
}
export const SubNavigation = ({ 
    onBack, 
    onSave, 
    onNewChart, 
    arenaType, 
    onRefreshChart,
    noTrialsAvailable,
    testScenarioId,
    testScenarioName,
    testScenario,
    trial,
    inDoneTrialMode = false
}: SubNavigationProps) => {
    // Removed unused clinicalTasks, taskStats, isLoading, selectedTaskIds state
    const doneTrailIds = useAppSelector(state => state.arena.doneTrialIds || []);
    const dispatch = useAppDispatch();
    const [copyTooltipText, setCopyTooltipText] = useState<string>('Copy Trial ID');
    const [trialIndex, setTrialIndex] = useState<string>('');
    const [scenarioWithStats, setScenarioWithStats] = useState<TestScenarioItem | null>(null);
    
    useEffect(() => {
        console.log('testScenarioId or inDoneTrialMode changed:', testScenario, inDoneTrialMode);
        if (inDoneTrialMode && testScenario ) {
            dispatch(fetchDoneTrialIds(testScenario.id));
        }
       
        
    }, [testScenario, inDoneTrialMode, dispatch]);

    useEffect(() => {
        if( ! inDoneTrialMode) {
            const fetchScenariosWithStats = async () => {
                if( !testScenario) return;
                let scenarioWithStats = await loadScenarioWithStats(testScenario);
                setScenarioWithStats(scenarioWithStats ? scenarioWithStats : null);
            };
            if (testScenario) {
                fetchScenariosWithStats();
            }
        }
    }, [trial, inDoneTrialMode, testScenario]);
    // Removed unused handleTaskSelectionChange

    const handleIndexNavigation = (trialIndex: string) => {
        // Logic to navigate to a specific trial by its ID
        var trialId = null;
        // Check if trialIndex is a pure integer (not a GUID that starts with numbers)
        const isInteger = /^\d+$/.test(trialIndex);
        
        if (isInteger) {
            const index = parseInt(trialIndex);
            if (index > doneTrailIds.length) {
                trialIndex = doneTrailIds.length.toString();
            }
            trialId = doneTrailIds[parseInt(trialIndex) - 1];
        } else {
            // Treat as GUID
            trialId = trialIndex;
        }

        console.log('Navigating to trial ID:', trialId);
        if( doneTrailIds.indexOf(trialId) === -1 ){
            console.error('Trial ID not found in doneTrialIds:', trialId);
            toast.error('Trial ID not found');
            return;
        }
        
        dispatch(getTrialById(trialId));
        //setTrialIndex("");
    }

    const items: ICommandBarItemProps[] = [
        {
            key: 'back',
            text: 'Back to Arena',
            iconProps: { iconName: 'Back' },
            onClick: onBack
        },
        // Only show "Skip" if trials are available
        ...(!noTrialsAvailable ? [{
            key: 'newChart',
            className: 'ms-CommandBar-itemSkip',
            text: 'Skip',
            iconProps: { iconName: 'Refresh' },
            onClick: onNewChart
        }] : [])
    ];

    const farItems: ICommandBarItemProps[] = [
        // Only show "Save" if trials are available
        ...(!noTrialsAvailable ? [{
            key: 'save',
            text: 'Save',
            iconProps: { iconName: 'Save' },
            onClick: onSave,
            className: 'ms-CommandBar-itemSave',
            disabled: noTrialsAvailable
        }] : [])
    ];

    return (
        <Stack>
            <Stack 
                horizontal 
                styles={{
                    root: {
                        position: 'relative',
                        height: '44px',
                        backgroundColor: '#fff',
                        padding: '0 20px'
                    }
                }}
            >
                <CommandBar
                    items={items}
                    farItems={farItems}
                    styles={{
                        root: {
                            padding: 0
                        }
                    }}
                />
                {!inDoneTrialMode &&(
                <Stack style={{marginLeft: '3px', marginTop: '-2px', fontSize: '12px'}} horizontal verticalAlign="center" tokens={{ childrenGap: 4 }}>
                    {scenarioWithStats ? scenarioWithStats.pendingTrials + " Trial"+(scenarioWithStats.pendingTrials === 1 ? "" : "s")+" remaining" : ""}
                </Stack>
                )}
                {inDoneTrialMode && (
                    <Stack style={{marginLeft: '8px'}} horizontal verticalAlign="center" tokens={{ childrenGap: 4 }}>
                        <input
                            type="number"
                            min="1"
                            max={doneTrailIds.length}
                            placeholder="Go to trial"
                            value={trialIndex}
                            style={{
                                width: '100px',
                                height: '24px',
                                fontSize: '12px',
                                padding: '2px 2px 2px 4px',
                                border: '1px solid #ccc',
                                borderRadius: '2px'
                            }}
                            onChange={(e) => {
                                try{
                                    var index = Math.min(doneTrailIds.length,parseInt(e.target.value)).toString();
                                    setTrialIndex(index)
                                } catch (error) {
                                   // setTrialIndex(doneTrailIds.length.toString());
                                    console.error('Error parsing trial index:', error);
                                }
                            }}
                            onKeyDown={(e) => {
                                if (e.key === 'Enter') {
                                    handleIndexNavigation(trialIndex);
                                }
                            }}

                            onKeyUp={(e) => {
                                if( parseInt(trialIndex) === 0 || trialIndex === ''){
                                    setTrialIndex('1');
                                }
                                if( parseInt(trialIndex) > doneTrailIds.length || isNaN(parseInt(trialIndex))){
                                   // setTrialIndex(doneTrailIds.length.toString());
                                }
                            }}
                        />
                        <IconButton
                            iconProps={{ iconName: "NavigateForward" }}
                            title="Go to trial"
                            onClick={() => {
                                handleIndexNavigation(trialIndex);
                            }}
                            styles={{
                                root: {
                                    width: '40px',
                                    height: '40px'
                                },
                                icon: {
                                    fontSize: '12px'
                                }
                            }}
                        />
                    </Stack>
                )}
                <Stack
                    horizontalAlign="center"
                    verticalAlign="center"
                    horizontal
                    styles={{
                        root: {
                            position: 'absolute',
                            left: '50%',
                            top: '50%',
                            transform: 'translate(-50%, -50%)',
                            pointerEvents: 'none'
                        }
                    }}
                >

                    <Stack.Item>
                        {inDoneTrialMode && (
                        <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                            <Text
                                styles={{
                                    root: {
                                        fontSize: '16px',
                                        color: '#333'
                                    }
                                }}
                            >
                                Reviewing: {testScenario?.name}, Trial {doneTrailIds.indexOf(trial?.id || '') + 1} of {doneTrailIds.length}
                            </Text>
                            </Stack>
                        )}
                        {!inDoneTrialMode && (
                            <Text
                            styles={{
                                root: {
                                    fontSize: '16px',
                                    color: '#333'
                                }
                            }}
                            >
                                {testScenarioName || 'Test Scenario'}
                            </Text>
                        )}
                        
                    </Stack.Item>
                   
                </Stack>
                 <Stack.Item styles={{ root: { marginLeft: 'auto', marginTop: '10px' } }}>
                    {trial && (
                        <>
                            <Text
                                styles={{
                                    root: {
                                        fontSize: '12px',
                                        color: '#747474ff'
                                    }
                                }}
                            >
                                {inDoneTrialMode ? 'Reviewing ' : ''}
                                Trial: {trial?.id}
                            </Text>
                            <TooltipHost content={copyTooltipText} id="copy-tooltip">
                            <IconButton
                                iconProps={{ iconName: "Copy" }}
                                title="Copy trial ID to clipboard"
                                onClick={() => { 
                                    setCopyTooltipText('Copied!');
                                    window.navigator.clipboard.writeText(trial?.id || ''); 
                                    setTimeout(() => setCopyTooltipText('Copy Trial ID'), 2000);
                                }}
                                styles={{
                                    root: {
                                        width: '20px',
                                        height: '20px',
                                        marginLeft: '8px'
                                    },
                                    icon: {
                                        fontSize: '12px',
                                        color: '#747474ff'
                                    }
                                }}
                            />
                            </TooltipHost>
                        </>
                    )}
                </Stack.Item>
            </Stack>
        </Stack>
    );
}; 