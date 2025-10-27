import React, { useEffect, useState } from 'react';
import { 
    Stack, 
    DetailsList,
    SelectionMode,
    IColumn,
    Text,
    Breadcrumb,
    IBreadcrumbItem,
    Spinner,
    MessageBar,
    MessageBarType,
    CommandBar,
    ICommandBarItemProps,
    Panel,
    PanelType,
    IContextualMenuItem
} from '@fluentui/react';
import { useNavigate, useParams } from 'react-router-dom';
import { DataObject, DataSet } from '../../types/dataset';
import { dataSetService } from '../../services/dataSetService';
import { ReferenceDataPanel } from '../Arena/ReferenceDataPanel';

export const DatasetFilteredView: React.FC = () => {
    const [dataSet, setDataSet] = useState<DataSet | null>(null);
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const [dataObjects, setDataObjects] = useState<DataObject[]>([]);
    const [filteredObjects, setFilteredObjects] = useState<DataObject[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedObject, setSelectedObject] = useState<DataObject | null>(null);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    
    const navigate = useNavigate();
    const { datasetId, outputIndex, generatedKey } = useParams();

    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);
                setError(null);
                
                if (!datasetId) {
                    setError('Dataset ID is required');
                    setLoading(false);
                    return;
                }
                
                const dataset = await dataSetService.getDataSet(datasetId);
                setDataSet(dataset);
                
                const objects = await dataSetService.getDataObjects(datasetId);
                setDataObjects(objects);
                
                // Filter objects based on parameters
                let filtered = [...objects];
                
                if (outputIndex && !isNaN(Number(outputIndex))) {
                    // Filter by output index
                    const index = Number(outputIndex);
                    filtered = filtered.filter(obj => 
                        obj.outputData.length > index
                    );
                } else if (generatedKey) {
                    // Filter by generated output key
                    filtered = filtered.filter(obj => 
                        obj.generatedOutputData.some(data => 
                            data.generatedForClinicalTask === generatedKey
                        )
                    );
                }
                
                setFilteredObjects(filtered);
            } catch (err) {
                setError('Failed to load dataset details');
                console.error(err);
            } finally {
                setLoading(false);
            }
        };
        
        loadData();
    }, [datasetId, outputIndex, generatedKey]);

    const breadcrumbItems: IBreadcrumbItem[] = [
        { 
            text: 'Admin', 
            key: 'admin', 
            onClick: () => navigate('/admin')
        },
        { 
            text: 'Clinical Tasks', 
            key: 'clinicalTasks', 
            onClick: () => navigate('/admin/clinical-tasks')
        },
        { 
            text: dataSet?.name || 'Dataset Details', 
            key: 'datasetDetails',
            isCurrentItem: true
        }
    ];

    const columns: IColumn[] = [
        { 
            key: 'outputData', 
            name: outputIndex ? `Output ${Number(outputIndex) + 1}` : (generatedKey ? 'Generated Output' : 'Output Data'),
            minWidth: 250,
            maxWidth: 400,
            isResizable: true,
            onRender: (item: DataObject) => {
                if (outputIndex && !isNaN(Number(outputIndex))) {
                    const index = Number(outputIndex);
                    return item.outputData.length > index ? 
                        <div style={{width: '100%', whiteSpace: 'wrap'}}><Text>{item.outputData[index].content}</Text></div> :
                        <Text style={{ color: 'gray' }}>No output data at this index</Text>;
                } else if (generatedKey) {
                    const generated = item.generatedOutputData.find(
                        data => data.generatedForClinicalTask === generatedKey
                    );
                    return generated ? 
                        <div style={{width: '100%', whiteSpace: 'wrap'}}><Text>{generated.content}</Text></div> :
                        <Text style={{ color: 'gray' }}>No generated data with this key</Text>;
                } else {
                    return <Text>{item.outputData.map(output => output.content).join(' | ')}</Text>;
                }
            }
        },
    ];

    const commandItems: ICommandBarItemProps[] = [
        {
            key: 'back',
            text: 'Back to Clinical Tasks',
            iconProps: { iconName: 'Back' },
            onClick: (_ev?: React.MouseEvent<HTMLElement> | React.KeyboardEvent<HTMLElement>, _item?: IContextualMenuItem) => {
                navigate('/admin/clinical-tasks');
                return false;
            }
        }
    ];

    if (loading) {
        return (
            <Stack tokens={{ childrenGap: 20 }}>
                <Breadcrumb items={breadcrumbItems} />
                <Spinner label="Loading dataset details..." />
            </Stack>
        );
    }

    if (error) {
        return (
            <Stack tokens={{ childrenGap: 20 }}>
                <Breadcrumb items={breadcrumbItems} />
                <MessageBar messageBarType={MessageBarType.error}>{error}</MessageBar>
            </Stack>
        );
    }

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Breadcrumb items={breadcrumbItems} />
            
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">
                    {outputIndex ? 
                        `${dataSet?.name} - Output ${Number(outputIndex) + 1}` : 
                        (generatedKey ? 
                            `${dataSet?.name} - ${generatedKey}` : 
                            dataSet?.name)
                    }
                </Text>
            </Stack>
            
            <CommandBar items={commandItems} />
            
            <DetailsList
                items={filteredObjects}
                columns={columns}
                selectionMode={SelectionMode.single}
                onItemInvoked={(item) => {
                    setSelectedObject(item);
                    setIsPanelOpen(true);
                }}
            />
            
            <Panel
                isOpen={isPanelOpen}
                onDismiss={() => setIsPanelOpen(false)}
                type={PanelType.medium}
                headerText="Data Object Details"
            >
                {selectedObject && (
                    <Stack tokens={{ childrenGap: 15 }}>
                        <Text variant="large">{selectedObject.name}</Text>
                        <Text>{selectedObject.description}</Text>
                        
                        <Text variant="mediumPlus">Input Data:</Text>
                        <ReferenceDataPanel inputData={selectedObject.inputData} />
                        
                        {outputIndex && !isNaN(Number(outputIndex)) && selectedObject.outputData.length > Number(outputIndex) && (
                            <>
                                <Text variant="mediumPlus">Output Data:</Text>
                                <ReferenceDataPanel inputData={[selectedObject.outputData[Number(outputIndex)]]} />
                            </>
                        )}
                        
                        {generatedKey && selectedObject.generatedOutputData.some(d => d.generatedForClinicalTask === generatedKey) && (
                            <>
                                <Text variant="mediumPlus">Generated Output:</Text>
                                <ReferenceDataPanel 
                                    inputData={selectedObject.generatedOutputData.filter(
                                        d => d.generatedForClinicalTask === generatedKey
                                    )} 
                                />
                            </>
                        )}
                    </Stack>
                )}
            </Panel>
        </Stack>
    );
}; 