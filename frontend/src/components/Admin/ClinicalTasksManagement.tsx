import React, { useEffect, useState } from 'react';
import {
    Stack,
    DetailsList,
    Selection,
    SelectionMode,
    IColumn,
    Text,
    Spinner,
    MessageBar,
    MessageBarType,
    CommandBar,
    ICommandBarItemProps,
    Icon,
    SpinnerSize,
    Link
} from '@fluentui/react';
import { FloatingCommandBar } from './FloatingCommandBar';
import { useClinicalTasks } from '../../hooks/useClinicalTasks';
import { IClinicalTask } from '../../types/admin';
import { ClinicalTaskPanel } from './ClinicalTaskPanel';
// eslint-disable-next-line
import { toast,Toaster } from 'react-hot-toast';
import { ISelection, IObjectWithKey } from '@fluentui/react';
import { useAppSelector } from '../../store/store';
import { useAppDispatch } from '../../store/store';
import { fetchModels } from '../../reducers/modelReducer';
import { clinicalTaskService } from '../../services/clinicalTaskService';
import { fetchDataSets } from '../../reducers/dataReducer';
import { useNavigate } from 'react-router-dom';
import { UploadMetricsDialog } from './UploadMetricsDialog';

// Add export function
const exportClinicalTaskData = (task: IClinicalTask) => {
    const exportData = {
        clinicalTask: {
            id: task.id,
            name: task.name,
            dataSetModels: task.dataSetModels,
            prompt: task.prompt,
            tags: task.tags,
            evalMetric: task.evalMetric,
            generationStatus: task.generationStatus,
            metricsGenerationStatus: task.metricsGenerationStatus,
            totalCost: task.totalCost,
            createdAt: task.createdAt,
            updatedAt: task.updatedAt
        },
        metrics: task.metrics || {},
        modelResults: task.modelResults || {},
        exportedAt: new Date().toISOString(),
        exportType: 'clinical-task'
    };

    const dataStr = JSON.stringify(exportData, null, 2);
    const dataUri = 'data:application/json;charset=utf-8,'+ encodeURIComponent(dataStr);
    
    const exportFileDefaultName = `clinical-task-${task.name.replace(/[^a-z0-9]/gi, '_').toLowerCase()}-${new Date().toISOString().split('T')[0]}.json`;
    
    const linkElement = document.createElement('a');
    linkElement.setAttribute('href', dataUri);
    linkElement.setAttribute('download', exportFileDefaultName);
    linkElement.click();
};

export const ClinicalTasksManagement: React.FC = () => {
    const { tasks, error, fetchTasks, deleteTask, updateTask, addTask } = useClinicalTasks();
    const [selectedTask, setSelectedTask] = useState<IClinicalTask | null>(null);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const { datasets } = useAppSelector((state) => state.data);
    const { models } = useAppSelector((state) => state.models);
    const dispatch = useAppDispatch();
    const navigate = useNavigate();
    const [isUploadMetricsDialogOpen, setIsUploadMetricsDialogOpen] = useState(false);
    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            const selectedItems = selection.getSelection();
            setSelectedTask(selectedItems[0] as IClinicalTask || null);
        },
        getKey: (item: IObjectWithKey) => (item as IClinicalTask).id,
    }));

    useEffect(() => {
        fetchTasks();
        dispatch(fetchModels());
        dispatch(fetchDataSets());
    }, [fetchTasks, dispatch]);

    useEffect(() => {
        const interval = setInterval(() => {
            fetchTasks();
        }, 10000); // 10 seconds

        // Cleanup function to clear interval when component unmounts
        return () => clearInterval(interval);
    }, [fetchTasks]);

    const needsGeneration = (task: IClinicalTask): boolean => {
        return task.dataSetModels.some(pair => {
            var model = models.find(m => m.id === pair.modelId);
            return !pair.isGroundTruth && // Not ground truth
            pair.modelId && // Has a model
            pair.modelOutputIndex === -1 && // Set to generate output
            pair.generatedOutputKey === model?.name}
        );
    };

    const onlyUploadedOutputs = (task: IClinicalTask): boolean => {
        return task.dataSetModels.every(pair => {
            return pair.isGroundTruth || (pair.modelOutputIndex !== -1)
        });
    };

    const hasGroundTruthAndOutput = (task: IClinicalTask): boolean => {
        return task.dataSetModels.some(pair => (pair.isGroundTruth)) && task.dataSetModels.some(pair => (!pair.isGroundTruth));
    };


    const columns: IColumn[] = [
        { key: 'name', name: 'Name', fieldName: 'name', minWidth: 150, isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (column) {
                    column.minWidth = 0; // Allow multiline rendering
                }
                return item.name;
            }
        },
        { 
            key: 'dataSetModels', 
            name: 'Dataset-Model Pairs', 
            fieldName: 'dataSetModels',
            minWidth: 150,
            isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (column) {
                    column.minWidth = 0; // Allow multiline rendering
                }
                return (
                    <div>
                        {item.dataSetModels.map((pair, index) => {
                            const dataset = datasets.find(d => d.id === pair.dataSetId);
                            const model = models.find(m => m.id === pair.modelId);
                            
                            // Create appropriate navigation link based on pair type
                            let linkPath = '';
                            if (pair.isGroundTruth || pair.modelOutputIndex !== -1) {
                                // Navigate to output index view
                                linkPath = `/webapp/admin/clinical-tasks/dataset/${pair.dataSetId}/output/${pair.modelOutputIndex}`;
                            } else if (pair.generatedOutputKey && pair.generatedOutputKey !== model?.name) {
                                // Navigate to generated output view
                                linkPath = `/webapp/admin/clinical-tasks/dataset/${pair.dataSetId}/generated/${encodeURIComponent(pair.generatedOutputKey)}`;
                            } else {
                                // Default dataset view if neither condition is met
                                linkPath = `/admin/data/${pair.dataSetId}`;
                            }
                            
                            return (
                                <div key={index}>
                                    <Link
                                        onClick={() => navigate(linkPath)}
                                        underline
                                    >
                                        {dataset?.name || 'Unknown'}
                                        {pair.isGroundTruth ? 
                                            ' output ' + pair.modelOutputIndex + ' (Ground Truth)' : 
                                            pair.modelOutputIndex !== -1 ?
                                            ` output ${pair.modelOutputIndex} ${model?.name|| 'Unknown'}` :
                                            pair.generatedOutputKey !== model?.name ?
                                            ` output ${pair.generatedOutputKey}` :
                                            " " + pair.generatedOutputKey + ' pending generation'
                                        }
                                    </Link>
                                    
                                </div>
                            );
                        })}
                    </div>
                );
            }
        },
        { key: 'prompt', name: 'Prompt', fieldName: 'prompt', minWidth: 200, isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (column) {
                    column.minWidth = 0; // Allow multiline rendering
                }
                return item.prompt || '';
            }
         },
        { 
            key: 'tags', 
            name: 'Tags', 
            fieldName: 'tags',
            minWidth: 100,
            isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (column) {
                    column.minWidth = 0; // Allow multiline rendering
                }
                return item.tags?.join(', ') || ''; 
            }
        },
        { 
            key: 'evalMetric', 
            name: 'Evaluation Metric', 
            fieldName: 'evalMetric', 
            minWidth: 100,
            isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {

                if (column) {
                    column.minWidth = 0; // Allow multiline rendering
                }

                return item.evalMetric || '';
            }
        },
        { 
            key: 'generationStatus', 
            name: 'Output Generation Status', 
            fieldName: 'generationStatus', 
            minWidth: 100,
            isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (onlyUploadedOutputs(item)) {
                    return 'n/a'
                }
                if (!needsGeneration(item)) {
                    return <div><Icon iconName="CheckMark" styles={{ root: { color: 'green' } }} />  <Text variant="small">Complete</Text></div>;
                }
                
                switch(item.generationStatus) {
                    case 'processing':
                        return <div><Spinner style={{display: 'inline-block', marginRight:"5px", marginBottom:"-4px"}} size={SpinnerSize.small} /><Text variant="small">Processing</Text></div>;
                    case 'complete':
                        return <div><Icon iconName="CheckMark" styles={{ root: { color: 'green' } }} />  <Text variant="small">Complete</Text></div>;
                    case 'error':
                        return <div><Icon iconName="Error" styles={{ root: { color: 'red' } }} /> <Text variant="small">Error</Text></div>;
                    default:
                        return 'Not started';
                }
            }
        },
        { 
            key: 'metricsGenerationStatus', 
            name: 'Metrics Generation Status', 
            fieldName: 'metricsGenerationStatus', 
            minWidth: 100,
            isResizable: true,
            onRender: (item: IClinicalTask, index?: number, column?: IColumn) => {
                if (!hasGroundTruthAndOutput(item) ) {
                    return 'n/a';
                }
                if( column ){
                    column.minWidth = 0; // Allow multiline rendering
                }
                
                switch(item.metricsGenerationStatus) {
                    case 'processing':
                        return <div><Spinner style={{display: 'inline-block', marginRight:"5px", marginBottom:"-4px"}} size={SpinnerSize.small} /><Text variant="small">Processing</Text></div>;
                    case 'complete':
                        return <div><Icon iconName="CheckMark" styles={{ root: { color: 'green' } }} />  <Text variant="small">Complete</Text></div>;
                    case 'error':
                        return <div><Icon iconName="Error" styles={{ root: { color: 'red' } }} /> <Text variant="small">Error</Text></div>;
                    default:
                        return 'Not started';
                }
            }
        },
    ];

    const commandItems: ICommandBarItemProps[] = [
        {
            key: 'add',
            text: 'Add Task',
            iconProps: { iconName: 'Add' },
            onClick: () => {
                setSelectedTask(null);
                setIsPanelOpen(true);
            },
        },
        {
            key: 'edit',
            text: 'Edit',
            iconProps: { iconName: 'Edit' },
            disabled: !selectedTask,
            onClick: () => setIsPanelOpen(true),
        },
        {
            key: 'delete',
            text: 'Delete',
            iconProps: { iconName: 'Delete' },
            disabled: !selectedTask,
            onClick: async () => {
                if (selectedTask) {
                    try {
                        await deleteTask(selectedTask.id);
                        setSelectedTask(null);
                        toast.success('Clinical task deleted successfully');
                    } catch (err) {
                        toast.error('Failed to delete clinical task');
                    }
                }
            },
        },
        {
            key: 'export',
            text: 'Export Data',
            iconProps: { iconName: 'Download' },
            disabled: !selectedTask,
            onClick: () => {
                if (selectedTask) {
                    try {
                        exportClinicalTaskData(selectedTask);
                        toast.success('Clinical task data exported successfully');
                    } catch (err) {
                        toast.error('Failed to export clinical task data');
                    }
                }
            },
        },
        {
            key: 'generate',
            text: 'Generate Outputs',
            iconProps: { iconName: 'Play' },
            disabled: !selectedTask || (selectedTask && !needsGeneration(selectedTask)),
            onClick: async () => {
                if (selectedTask) {
                    try {
                        await clinicalTaskService.generateOutputs(selectedTask.id);
                        toast.success('Output generation started');
                        
                        setTimeout(() => {
                            fetchTasks();
                        }, 2000);
                    } catch (err) {
                        toast.error('Failed to generate outputs');
                    }
                }
            },
        },
        {
            key: 'generateMetrics',
            text: 'Generate Metrics',
            iconProps: { iconName: 'BarChart4' },
            disabled: !selectedTask || (selectedTask && !hasGroundTruthAndOutput(selectedTask) ) || needsGeneration(selectedTask),
            onClick: async () => {
                if (selectedTask) {
                    try {
                        await clinicalTaskService.generateMetrics(selectedTask.id);
                        toast.success('Metrics generation started');
                        fetchTasks();
                    } catch (err) { 
                        toast.error('Failed to generate metrics');
                    }
                }
            },
        },
        {
            key: 'uploadMetrics',
            text: 'Upload Metrics',
            iconProps: { iconName: 'Upload' },
            disabled: !selectedTask,
            onClick: () => setIsUploadMetricsDialogOpen(true),
        }
        
    ];

    const handleSave = async (task: Omit<IClinicalTask, 'id'> | IClinicalTask) => {
        try {
            if ('id' in task) {
                await updateTask(task);
                toast.success('Clinical task updated successfully');
            } else {
                await addTask(task);
                toast.success('Clinical task created successfully');
            }
            setIsPanelOpen(false);
        } catch (err) {
            toast.error('Failed to save clinical task');
        }
    };

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">Clinical Tasks</Text>
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}

            
            <FloatingCommandBar
                items={commandItems}
                parentId='adminContent'
                stickOffsetId='navigationbar'
            />
            
            <DetailsList
                items={tasks}
                columns={columns}
                selection={selection}
                selectionMode={SelectionMode.single}
                setKey="tasks"
            />

            <ClinicalTaskPanel
                isOpen={isPanelOpen}
                task={selectedTask || undefined}
                onDismiss={() => setIsPanelOpen(false)}
                onSave={handleSave}
            />

            {selectedTask && (
                <UploadMetricsDialog
                    isOpen={isUploadMetricsDialogOpen}
                    onDismiss={() => setIsUploadMetricsDialogOpen(false)}
                    clinicalTaskId={selectedTask.id}
                    models={models}
                    onSuccess={() => {
                        fetchTasks();
                    }}
                />
            )}
        </Stack>
    );
}; 