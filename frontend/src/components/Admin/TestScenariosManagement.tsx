import React, { useEffect, useState } from 'react';
import {
    Stack,
    DetailsList,
    Selection,
    SelectionMode,
    IColumn,
    PrimaryButton,
    DefaultButton,
    Text,
    Spinner,
    MessageBar,
    MessageBarType,
    MarqueeSelection,
    IObjectWithKey,
    ISelection,
} from '@fluentui/react';
import { FloatingCommandBar } from './FloatingCommandBar';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { RootState } from '../../store/store';
import {
    fetchScenarios,
    deleteScenario,
    updateScenario,
    addScenario,
} from '../../reducers/testScenarioReducer';
import { ITestScenario } from '../../types/admin';
import { TestScenarioPanel } from './TestScenarioPanel';
// eslint-disable-next-line
import { toast,Toaster } from 'react-hot-toast';
import { fetchModels } from '../../reducers/modelReducer';
import { fetchTasks } from '../../reducers/clinicalTaskReducer';
export const TestScenariosManagement: React.FC = () => {
    const dispatch = useAppDispatch();
    const { scenarios, isLoading, error } = useAppSelector((state: RootState) => state.testScenarios);
    const { tasks } = useAppSelector((state) => state.clinicalTasks);
    const { models } = useAppSelector((state) => state.models);
    
    // Local UI state
    const [selectedScenarios, setSelectedScenarios] = useState<string[]>([]);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [editingScenario, setEditingScenario] = useState<ITestScenario | undefined>();
    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            
            const selectedItems = selection.getSelection() as ITestScenario[];
            console.log("Selected items:", selection.getSelection());
            console.log("Selected items:", selectedItems.map(item => item.id));
            setSelectedScenarios(selectedItems.map(item => item.id));
        },
        getKey: (item: IObjectWithKey) => (item as ITestScenario).id,
    }));

  

    const columns: IColumn[] = [
        { key: 'name', name: 'Name', fieldName: 'name', minWidth: 50, maxWidth: 200, isResizable: true },
        { 
            key: 'taskId', 
            name: 'Task', 
            fieldName: 'taskId',
            minWidth: 50, maxWidth: 200,
            isResizable: true,
            onRender: (item: ITestScenario) => {
                const task = tasks.find(t => t.id === item.taskId);
                return task?.name || 'Unknown Task';
            }
        },
        { key: 'description', name: 'Description', fieldName: 'description', minWidth: 200 , maxWidth: 300, isResizable: true},
        { 
            key: 'modelIds', 
            name: 'Models', 
            fieldName: 'modelIds', 
            minWidth: 150,
            isResizable: true,
            onRender: (item: ITestScenario) => {
                if( item.modelIds === undefined || item.modelIds.length === 0)
                {
                    return 'No models selected';
                }
                return item.modelIds
                    .map(id => {
                        const model = models.find(m => m.id === id);
                        return model ? `${model.name} (${model.modelType})` : 'Unknown Model';
                    })
                    .join(', ');
            }
        }
    ];

    useEffect(() => {
        dispatch(fetchScenarios());
        dispatch(fetchModels());
        dispatch(fetchTasks());
    }, [dispatch]);


    const handleDelete = async () => {
        try {
            for (const id of selectedScenarios) {
                await dispatch(deleteScenario(id)).unwrap();
            }
            setSelectedScenarios([]);
            selection.setAllSelected(false);
            toast.success('Test scenario(s) deleted successfully');
        } catch (error) {
            toast.error('Failed to delete test scenario(s)');
        }
    };

    const handleNewClick = () => {
        setEditingScenario(undefined);
        setIsPanelOpen(true);
    };

    const handleEditClick = () => {
        const selectedScenario = scenarios.find(s => s.id === selectedScenarios[0]);
        if (selectedScenario) {
            setEditingScenario(selectedScenario);
            setIsPanelOpen(true);
        }
    };

    const handlePanelDismiss = () => {
        setIsPanelOpen(false);
        setEditingScenario(undefined);
    };

      const commandItems = [
        {
            key: 'new',
            text: 'New',
            iconProps: { iconName: 'Add' },
            onClick: handleNewClick
        },
        {
            key: 'edit',
            text: 'Edit',
            iconProps: { iconName: 'Edit' },
            onClick: handleEditClick,
            disabled: selectedScenarios.length !== 1
        },
        {
            key: 'delete',
            text: 'Delete',
            iconProps: { iconName: 'Delete' },
            onClick: handleDelete,
            disabled: selectedScenarios.length === 0
        }
    ];

    const handlePanelSave = async (scenario: Omit<ITestScenario, 'id'> | ITestScenario) => {
        try {
            if ('id' in scenario) {
                await dispatch(updateScenario(scenario))
                toast.success('Test scenario updated successfully');
            } else {
                await dispatch(addScenario(scenario))
                toast.success('Test scenario created successfully');
            }
            dispatch(fetchScenarios());
            setIsPanelOpen(false);
            setEditingScenario(undefined);
        } catch (error) {
            toast.error('Failed to save test scenario');
        }
    };

    if (isLoading) {
        return <Spinner label="Loading Experiments..." />;
    }

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}

            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">Experiments</Text>
            </Stack>

            <FloatingCommandBar 
                items={commandItems} 
                parentId='adminContent'
                stickOffsetId='navigationbar'
                />

            <MarqueeSelection selection={selection}>
                <DetailsList
                    items={scenarios}
                    columns={columns}
                    selection={selection}
                    selectionMode={SelectionMode.multiple}
                    setKey="scenarios"
                />
            </MarqueeSelection>

            <TestScenarioPanel
                isOpen={isPanelOpen}
                scenario={editingScenario}
                onDismiss={handlePanelDismiss}
                onSave={handlePanelSave}
            />
        </Stack>
    );
}; 