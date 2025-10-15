import React, { useEffect, useState } from 'react';
import { 
    Stack, 
    DetailsList,
    SelectionMode,
    IColumn,
    Text,
    Breadcrumb,
    IBreadcrumbItem,
    Panel,
    PanelType,
    Selection,
    DefaultButton
} from '@fluentui/react';
import { useNavigate, useParams } from 'react-router-dom';
import { DataObject, DataContent, DataSet } from '../../types/dataset';
import { dataSetService } from '../../services/dataSetService';
import { ReferenceDataPanel } from '../Arena/ReferenceDataPanel';

interface FlatDataContent extends DataContent {
    index: number;
    source: 'input' | 'output' | 'generated';
}

export const DataObjectDetails: React.FC = () => {
    const [dataObject, setDataObject] = useState<DataObject | null>(null);
    const [dataset, setDataset] = useState<DataSet | null>(null);
    const [selectedContent, setSelectedContent] = useState<FlatDataContent | null>(null);
    const navigate = useNavigate();
    const { datasetId, objectId } = useParams();

    useEffect(() => {
        const loadDataObject = async () => {
            const object = await dataSetService.getDataObject(datasetId!, objectId!);
            setDataObject(object);
        };
        const loadDataset = async () => {
            if(datasetId){
                const dataset = await dataSetService.getDataSet(datasetId);
                setDataset(dataset);
            }
        };
        loadDataset();
        loadDataObject();
    }, [datasetId, objectId]);
    const flattenedContent = React.useMemo(() => {
        if (!dataObject) return [];
        return [
            ...dataObject.inputData.map((content, index) => ({
                ...content,
                index,
                source: 'input' as const
            })),
            ...dataObject.outputData.map((content, index) => ({
                ...content,
                index,
                source: 'output' as const
            })),
            ...dataObject.generatedOutputData.map((content, index) => ({
                ...content,
                index,
                source: 'generated' as const
            }))
        ];
    }, [dataObject]);

    const columns: IColumn[] = [
        { key: 'id', name: '', fieldName: '', minWidth: 20,isResizable: true, maxWidth: 120, onRender: (item: FlatDataContent) => {
            return <DefaultButton onClick={() => {
                setSelectedContent(item);
            }}>View</DefaultButton>
            }
        },
        { key: 'index', name: 'Index', fieldName: 'index', minWidth: 50, isResizable: true, onRender: (item: FlatDataContent) => {
            return <Text>{item.index}</Text>
        }
        },
        { key: 'source', name: 'Source', fieldName: 'source', minWidth: 100, isResizable: true, onRender:( item: FlatDataContent) => {
           
            if( item.generatedForClinicalTask === '' || typeof item.generatedForClinicalTask === 'undefined'){
               if( item.source !== 'input'){
                    return <Text>{dataset?.files?.find(() => true)?.mapping?.outputMappings?.[item.index]?.keyPath?.join('.')}</Text>
                } else {
                    return <Text>{dataset?.files?.find(() => true)?.mapping?.inputMappings?.[item.index]?.keyPath?.join('.')}</Text>
                }   
            }
        }
        },
        { key: 'type', name: 'Type', fieldName: 'type', minWidth: 100, isResizable: true },
        { key: 'generatedForClinicalTask', name: 'Generated For Clinical Task', fieldName: 'generatedForClinicalTask', minWidth: 100, isResizable: true },
        { key: 'source', name: 'Source', fieldName: 'source', minWidth: 100, isResizable: true },
        
    ];

    const breadcrumbItems: IBreadcrumbItem[] = [
        { key: 'datasets', text: 'Datasets', onClick: () => navigate('/admin/data') },
        { key: 'dataset-details', text: 'Dataset Details', onClick: () => navigate(`/admin/data/${datasetId}`) },
        { key: 'object-details', text: 'Data Object Details', isCurrentItem: true }
    ];

    const selection = new Selection({
        onSelectionChanged: () => {
            const selected = selection.getSelection()[0] as FlatDataContent;
            setSelectedContent(selected);
        }
    });

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Breadcrumb items={breadcrumbItems} />
            <Text variant="xxLarge">Data Object Contents</Text>
            <DetailsList
                items={flattenedContent}
                columns={columns}
                selectionMode={SelectionMode.none}
            />
            <Panel
                isOpen={!!selectedContent}
                onDismiss={() => {
                    setSelectedContent(null)
                    selection.setAllSelected(false);
                }}
                type={PanelType.medium}
                isLightDismiss={true}
            >
                {selectedContent && (
                    <ReferenceDataPanel 
                        inputData={[selectedContent]} 
                    />
                )}
            </Panel>
        </Stack>
    );
};

export {}; 