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
    DefaultButton,
    Icon,
    ISelection,
    IObjectWithKey
} from '@fluentui/react';
import { FloatingCommandBar } from './FloatingCommandBar';
import { useModels } from '../../hooks/useModels';
import { IModel } from '../../types/admin';
import { ModelPanel } from './ModelPanel';
// eslint-disable-next-line
import { Toaster, toast } from 'react-hot-toast';

export const ModelsManagement: React.FC = () => {
    const { models, loading, error, fetchModels, deleteModel, updateModel, addModel, testIntegration } = useModels();
    const [selectedModel, setSelectedModel] = useState<IModel | null>(null);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [testIntegrationStatuses, setTestIntegrationStatuses] = useState<Record<string, string>>({});
    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            const selectedItems = selection.getSelection();
            setSelectedModel(selectedItems[0] as IModel || null);
        },
        getKey: (item: IObjectWithKey) => (item as IModel).id,
    }));

    useEffect(() => {
        fetchModels();
    }, [fetchModels]);

    useEffect(() => {
        setTestIntegrationStatuses({});
    }, [models]);

    const columns: IColumn[] = [
        { key: 'name', name: 'Name', fieldName: 'name', minWidth: 100, isResizable: true },
        { key: 'modelType', name: 'AI Model Type', fieldName: 'modelType', minWidth: 150, isResizable: true },
        { key: 'origin', name: 'Origin', fieldName: 'origin', minWidth: 100, isResizable: true },
        { key: 'costPerToken', name: 'Cost/Token In', fieldName: 'costPerToken', minWidth: 100, isResizable: true },
        { key: 'costPerTokenOut', name: 'Cost/Token Out', fieldName: 'costPerTokenOut', minWidth: 100, isResizable: true },
        { key: 'description', name: 'Description', fieldName: 'description', minWidth: 200, isResizable: true },
        { key: 'integrationType', name: 'Integration Type', fieldName: 'integrationType', minWidth: 150, isResizable: true },
        { key: 'testIntegration', name: 'Test Integration', fieldName: 'testIntegration', minWidth: 150, isResizable: true, onRender: (item: IModel) => {
            return testIntegrationStatuses[item.id] === 'pending' ? <Spinner /> : testIntegrationStatuses[item.id] === 'success' ? <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}><Icon style={{ color: 'green', fontSize: 16 }} iconName="CheckMark" /></div> : testIntegrationStatuses[item.id] === 'error' ? <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}><Icon style={{ color: 'red', fontSize: 16 }} iconName="Error" /></div> : 
            item.integrationType ? <DefaultButton onClick={() => handleTestIntegration(item.id)}>Test Integration</DefaultButton> : null;
        } },
    ];

    const handleTestIntegration = async (id: string) => {
        try {
            setTestIntegrationStatuses(prev => ({ ...prev, [id]: 'pending' }));
            await testIntegration(id);
            setTestIntegrationStatuses(prev => ({ ...prev, [id]: 'success' }));
        } catch (err) {
            setTestIntegrationStatuses(prev => ({ ...prev, [id]: 'error' }));
            toast.error('Failed to test integration: ' + (err as Error).message);
        }
    };

    const commandItems: ICommandBarItemProps[] = [
        {
            key: 'add',
            text: 'Add Model',
            iconProps: { iconName: 'Add' },
            onClick: () => {
                setSelectedModel(null);
                setIsPanelOpen(true);
            },
        },
        {
            key: 'edit',
            text: 'Edit',
            iconProps: { iconName: 'Edit' },
            disabled: !selectedModel,
            onClick: () => setIsPanelOpen(true),
        },
        {
            key: 'delete',
            text: 'Delete',
            iconProps: { iconName: 'Delete' },
            disabled: !selectedModel,
            onClick: async () => {
                if (selectedModel) {
                    try {
                        await deleteModel(selectedModel.id);
                        setSelectedModel(null);
                        selection.setAllSelected(false);
                        toast.success('Model deleted successfully');
                    } catch (err) {
                        toast.error('Failed to delete model');
                    }
                }
            },
        },
    ];

    const handleSave = async (model: Omit<IModel, 'id'> | IModel) => {
        console.log("saving model", model);
        try {
            if ('id' in model) {
                await updateModel(model);
                toast.success('Model updated successfully');
            } else {
                await addModel(model);
                toast.success('Model created successfully');
            }
            setIsPanelOpen(false);
        } catch (err) {
            toast.error('Failed to save model');
        }
    };

    if (loading) {
        return <Spinner label="Loading models..." />;
    }

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">Models</Text>
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
                items={models}
                columns={columns}
                selection={selection}
                selectionMode={SelectionMode.single}
                setKey="models"
            />

            <ModelPanel
                isOpen={isPanelOpen}
                model={selectedModel || undefined}
                onDismiss={() => setIsPanelOpen(false)}
                onSave={handleSave}
            />
        </Stack>
    );
};

// Add an empty export to ensure this file is treated as a module
export {}; 