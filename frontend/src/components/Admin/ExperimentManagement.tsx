import React, { useEffect, useState } from 'react';
import {
    Stack,
    DetailsList,
    DetailsListLayoutMode,
    Selection,
    SelectionMode,
    IColumn,
    Text,
    CommandBar,
    ICommandBarItemProps,
    Spinner,
    SpinnerSize,
    MessageBar,
    MessageBarType,
    MarqueeSelection,
    ISelection,
    IObjectWithKey
} from '@fluentui/react';
import { IExperiment, ExperimentStatus, ProcessingStatus } from '../../types/admin';
import { ExperimentPanel } from './ExperimentPanel';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { 
    fetchExperiments, 
    processExperiment, 
    updateExperimentStatus,
    deleteExperiment,
    updateExperiment,
    addExperiment,
    setExperiments
} from '../../reducers/experimentReducer';
import { fetchScenarios } from '../../reducers/testScenarioReducer';
import { fetchModels } from '../../reducers/modelReducer';
import { toast } from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';
import { experimentService } from '../../services/experimentService';
import { FloatingCommandBar } from './FloatingCommandBar';

// Add export function for experiments
const exportExperimentData = async (experiment: IExperiment) => {
    try {
        // Use the new backend export endpoint instead of client-side data fetching
        const exportData = await experimentService.exportExperiment(experiment.id);
        
        const dataStr = JSON.stringify(exportData, null, 2);
        const dataUri = 'data:application/json;charset=utf-8,'+ encodeURIComponent(dataStr);
        
        const exportFileDefaultName = `experiment-${experiment.name.replace(/[^a-z0-9]/gi, '_').toLowerCase()}-${new Date().toISOString().split('T')[0]}.json`;
        
        const linkElement = document.createElement('a');
        linkElement.setAttribute('href', dataUri);
        linkElement.setAttribute('download', exportFileDefaultName);
        linkElement.click();
        
        return true;
    } catch (error) {
        console.error('Failed to export experiment data:', error);
        throw error;
    }
};

export const ExperimentManagement: React.FC = () => {
    const dispatch = useAppDispatch();
    const { experiments, isLoading: isInitialLoading, error } = useAppSelector((state) => state.experiments);
    const { scenarios } = useAppSelector((state) => state.testScenarios);
    const { models } = useAppSelector((state) => state.models);
    const [selectedExperiment, setSelectedExperiment] = useState<IExperiment | null>(null);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [processingExperiments, setProcessingExperiments] = useState<Set<string>>(new Set());
    const [selectedExperiments, setSelectedExperiments] = useState<IExperiment[]>([]);
    const [sortKey, setSortKey] = useState<string>('');
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);
    const [selectionKey, setSelectionKey] = useState(0);
    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            const selectedItems = selection.getSelection() as IExperiment[];
            setSelectedExperiments(selectedItems);
            setSelectedExperiment(selectedItems[0] || null);
        },
        getKey: (item: IObjectWithKey) => (item as IExperiment).id,
    }));
    const navigate = useNavigate();
    

    useEffect(() => {
        // Initial fetch
        void dispatch(fetchExperiments());
        void dispatch(fetchScenarios());
        void dispatch(fetchModels());

        // Set up polling
        const pollInterval = setInterval(async () => {
            try {
                const result = await dispatch(fetchExperiments()).unwrap();
                dispatch(setExperiments(result)); // Explicitly update the state
                
            } catch (error) {
                console.error('Polling failed:', error);
            }
        }, 5000);

        return () => clearInterval(pollInterval);
    }, [dispatch]);

    useEffect(() => {
        const processing = new Set(
            experiments.filter(exp => 
                exp.processingStatus === ProcessingStatus.Processing || 
                exp.processingStatus === ProcessingStatus.Finalizing
            ).map(exp => exp.id)
        );
        setProcessingExperiments(processing);

        if (processing.size > 0) {
            const pollInterval = setInterval(async () => {
                
                    await dispatch(fetchExperiments()).unwrap();
                
            }, 5000);

            return () => clearInterval(pollInterval);
        }else{
            const pollInterval = setInterval(async () => {
                
                await dispatch(fetchExperiments()).unwrap();
                
            }, 15000);

            return () => clearInterval(pollInterval);
        }
    }, [dispatch, experiments]);

    useEffect(() => {
        if (selectedExperiment) {
            const updatedExperiment = experiments.find(exp => exp.id === selectedExperiment.id);
            if (!updatedExperiment || JSON.stringify(updatedExperiment) !== JSON.stringify(selectedExperiment)) {
                setSelectedExperiment(updatedExperiment || null);
            }
        }
    }, [experiments, selectedExperiment]);

    const onColumnClick = (ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
        const newIsSortedDescending = column.key === sortKey ? !isSortedDescending : false;
        setSortKey(column.key);
        setIsSortedDescending(newIsSortedDescending);
        selection.setAllSelected(false);
        setSelectedExperiments([]);
        setSelectedExperiment(null);
        setSelectionKey(prev => prev + 1);

        const sortedItems = [...experiments].sort((a, b) => {
            let aValue = a[column.key as keyof IExperiment];
            let bValue = b[column.key as keyof IExperiment];

            // Handle special cases
            if (column.key === 'totalCost') {
                aValue = Number(aValue) || 0;
                bValue = Number(bValue) || 0;
            }

            // Handle null/undefined values
            if (!aValue) return newIsSortedDescending ? 1 : -1;
            if (!bValue) return newIsSortedDescending ? -1 : 1;

            // Compare values
            const compareResult = aValue > bValue ? 1 : -1;
            return newIsSortedDescending ? -compareResult : compareResult;
        });

        dispatch(setExperiments(sortedItems));
    };

    const columns: IColumn[] = [
        { 
            key: 'name', 
            name: 'Name', 
            fieldName: 'name', 
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'name',
            isSortedDescending: sortKey === 'name' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
        },
        { 
            key: 'status', 
            name: 'Status', 
            fieldName: 'status', 
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'status',
            isSortedDescending: sortKey === 'status' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
        },
        { 
            key: 'processingStatus', 
            name: 'Processing', 
            minWidth: 100,
            isResizable: true,
            onRender: (item: IExperiment) => {
                if (processingExperiments.has(item.id)) {
                    return <Stack horizontal verticalAlign="center">
                        <Spinner size={SpinnerSize.small} />
                        <span style={{ marginLeft: 8 }}>
                            {item.processingStatus === ProcessingStatus.Finalizing ? 'Finalizing' : 'Preparing'}
                        </span>
                    </Stack>;
                }
                return item.processingStatus === ProcessingStatus.Processed ? 'Prepared' : item.processingStatus;
            }
        },
        { key: 'experimentType', name: 'Type', fieldName: 'experimentType', minWidth: 150, isResizable: true },
        { 
            key: 'createdAt', 
            name: 'Created', 
            fieldName: 'createdAt', 
            isResizable: true,
            minWidth: 150,
            onRender: (item: IExperiment) => {
                const date = new Date(item.createdAt);
                if( date.toString() === 'Invalid Date')
                {
                    return '';
                }
                return date.toLocaleDateString('en-US', {
                    year: 'numeric',
                    month: 'short',
                    day: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit'
                });
            },
            isSorted: sortKey === 'createdAt',
            isSortedDescending: sortKey === 'createdAt' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
        },
        { key: 'pendingTrials', name: 'Pending Trials', fieldName: 'pendingTrials', minWidth: 100, isResizable: true },
        { key: 'totalTrials', name: 'Total Trials', fieldName: 'totalTrials', minWidth: 100, isResizable: true   },
        { key: 'totalCost', name: 'Total Cost', fieldName: 'totalCost', minWidth: 100, isResizable: true, onRender: (item: IExperiment) => {
            return item.totalCost ? item.totalCost.toFixed(2) : '0.00';
        } },
        { key: 'testScenario', name: 'Experiment', fieldName: 'testScenario', minWidth: 100, isResizable: true, onRender: (item: IExperiment) => {
            return item.testScenarioId ? scenarios.find((scenario)=>{return scenario.id === item.testScenarioId})?.name : '';
        } },

        { key: 'models', name: 'Models', fieldName: 'testScenario', minWidth: 100, isResizable: true, onRender: (item: IExperiment) => {
            var testScenario = scenarios.find((scenario)=>{return scenario.id === item.testScenarioId});
            return testScenario?.modelIds.map((modelId)=>{return models.find((model)=>{return model.id === modelId})?.name}).join(', ');
        } }
    ];


    const getCommandItems = (): ICommandBarItemProps[] => {
        var isProcessable = selectedExperiment?.status === ExperimentStatus.Draft && 
            selectedExperiment?.processingStatus !== ProcessingStatus.Processing &&
            selectedExperiment?.processingStatus !== ProcessingStatus.Processed;
       
        
        var canMoveToInProgress = selectedExperiment?.status === ExperimentStatus.Draft && 
            selectedExperiment?.processingStatus === ProcessingStatus.Processed;
        
        if( selectedExperiments.length > 1){
            isProcessable = false;
            canMoveToInProgress = false;
        }
        var canComplete = selectedExperiment?.status === ExperimentStatus.InProgress;
        var canCancel = selectedExperiment?.status === ExperimentStatus.InProgress;

        if( selectedExperiments.length > 1){
            isProcessable = false;
            canMoveToInProgress = false;
            canComplete = false;
            canCancel = false;
        }

        return [
            
            {
                key: 'add',
                text: 'New Assignment',
                iconProps: { iconName: 'Add' },
                onClick: () => {
                    setSelectedExperiments([]);
                    setSelectedExperiment(null);
                    setIsPanelOpen(true);
                },
            },
            {
                key: 'edit',
                text: 'Edit',
                iconProps: { iconName: 'Edit' },
                disabled: selectedExperiments.length > 1 || selectedExperiments.length === 0,
                onClick: () => setIsPanelOpen(true),
            },
            {
                key: 'delete',
                text: 'Delete',
                iconProps: { iconName: 'Delete' },
                disabled: selectedExperiments.length === 0,
                onClick: async () => {
                    for (const experiment of selectedExperiments) {
                        await handleDelete(experiment.id);
                    }
                    setSelectedExperiments([]);
                    setSelectedExperiment(null);
                },
            },
            {
                key: 'export',
                text: 'Export Data',
                iconProps: { iconName: 'Download' },
                disabled: selectedExperiments.length !== 1,
                onClick: async () => {
                    if (selectedExperiments.length === 1) {
                        try {
                            await exportExperimentData(selectedExperiments[0]);
                            toast.success('Experiment data exported successfully');
                        } catch (err) {
                            toast.error('Failed to export experiment data');
                        }
                    }
                },
            },
            {
                key: 'run',
                text: 'Run',
                iconProps: { iconName: 'Play' },
                disabled: !canMoveToInProgress,
                onClick: () => {
                    if (selectedExperiments.length > 0) {
                        handleStatusChange(ExperimentStatus.InProgress)
                    }
                }
            },
            {
                key: 'process',
                text: 'Prepare',
                iconProps: { iconName: 'TestBeaker' },
                disabled: !isProcessable,
                onClick: () => {
                    if (selectedExperiments.length > 0) {
                        dispatch(processExperiment(selectedExperiments[0].id));
                    }
                }
            },
            {
                key: 'status',
                text: 'Change Status',
                iconProps: { iconName: 'StatusCircleRing' },
                subMenuProps: {
                    items: [
                        {
                            key: 'inProgress',
                            text: 'Move to In Progress',
                            disabled: !canMoveToInProgress,
                            onClick: () => {
                                if (selectedExperiments.length > 0) {
                                    for (const experiment of selectedExperiments) {
                                        handleStatusChangeId(ExperimentStatus.InProgress, experiment)
                                    }
                                }
                            }
                        },
                        {
                            key: 'complete',
                            text: 'Complete',
                            disabled: !canComplete,
                            onClick: () => {
                                if (selectedExperiments.length > 0) {
                                    for (const experiment of selectedExperiments) {
                                        handleStatusChangeId(ExperimentStatus.Completed, experiment)
                                    }
                                }
                            }
                        },
                        {
                            key: 'cancel',
                            text: 'Cancel',
                            disabled: !canCancel,
                            onClick: () => {
                                if (selectedExperiments.length > 0) {
                                    for (const experiment of selectedExperiments) {
                                        handleStatusChangeId(ExperimentStatus.Cancelled, experiment)
                                    }
                                }
                            }
                        }
                    ]
                }
            },
            {
                key: 'explore',
                text: 'Explore',
                iconProps: { iconName: 'Search' },
                disabled: !selectedExperiments.length || selectedExperiments[0].processingStatus !== ProcessingStatus.Processed,
                onClick: () => {
                    if (selectedExperiments.length > 0) {
                        navigate(`/admin/experiments/${selectedExperiments[0].id}`);
                    }
                }
            },
        ];
    };

    const handleStatusChangeId = (newStatus: ExperimentStatus, experiment: IExperiment) => {
        if (selectedExperiments.length > 0) {
            var selectedExperiment = experiment;
            dispatch(updateExperimentStatus({ 
                id: selectedExperiment.id, 
                status: newStatus 
            }));
            if (newStatus === ExperimentStatus.Completed) {
                setProcessingExperiments(prev => new Set(prev.add(selectedExperiment.id)));
            }
            void dispatch(fetchExperiments());
        }
    };

    const handleStatusChange = (newStatus: ExperimentStatus) => {
        if (selectedExperiments.length > 0) {
            var selectedExperiment = selectedExperiments[0];
            dispatch(updateExperimentStatus({ 
                id: selectedExperiment.id, 
                status: newStatus 
            }));
            if (newStatus === ExperimentStatus.Completed) {
                setProcessingExperiments(prev => new Set(prev.add(selectedExperiment.id)));
            }
            void dispatch(fetchExperiments());
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await dispatch(deleteExperiment(id)).unwrap();
            setSelectedExperiment(null);
            selection.setAllSelected(false);
            setSelectedExperiments([]);
            setSelectionKey(prev => prev + 1);
            toast.success('Experiment deleted successfully');
            void dispatch(fetchExperiments());
        } catch (err) {
            toast.error('Failed to delete experiment');
        }
    };

    const handlePanelDismiss = () => {
        setIsPanelOpen(false);
    };

    const handlePanelSave = async (experiment: Omit<IExperiment, 'id'> | IExperiment) => {
        try {
            if ('id' in experiment) {
                await dispatch(updateExperiment(experiment)).unwrap();
                toast.success('Assignment updated successfully');
            } else {
                await dispatch(addExperiment(experiment)).unwrap();
                toast.success('Assignment created successfully');
            }
            await dispatch(fetchExperiments());
            setIsPanelOpen(false);
            setSelectedExperiments([]);
            setSelectedExperiment(null);
            void dispatch(fetchExperiments());
        } catch (error) {
            toast.error('Failed to save experiment');
        }
    };

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">Experiment Assignments</Text>
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}

          
            
            <FloatingCommandBar
                items={getCommandItems()}
                parentId='adminContent'
                stickOffsetId='navigationbar'
            />
            {isInitialLoading ? (
                <>
                    <DetailsList
                        items={[]}
                        columns={columns}
                        selection={selection}
                        selectionMode={SelectionMode.single}
                        setKey="experiments"
                    />
                    <Spinner label="Loading experiments..." />
                </>
            ) : (
                <MarqueeSelection selection={selection}>
                    <DetailsList
                        items={experiments}
                        columns={columns}
                        selection={selection}
                        selectionMode={SelectionMode.multiple}
                        setKey={`experiments-${selectionKey}`}
                        layoutMode={DetailsListLayoutMode.justified}
                        isHeaderVisible={true}
                    />
                </MarqueeSelection>
            )}

            <ExperimentPanel
                isOpen={isPanelOpen}
                experiment={selectedExperiment || undefined}
                onDismiss={handlePanelDismiss}
                onSave={handlePanelSave}
            />
        </Stack>
    );
};

export {}; 