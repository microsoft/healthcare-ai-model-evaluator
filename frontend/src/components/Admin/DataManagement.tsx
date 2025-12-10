import React, { useEffect, useState, useRef } from 'react';
// Disclaimer text for Data Management panel
import {
    Stack,
    PrimaryButton,
    DetailsList,
    Selection,
    SelectionMode,
    IColumn,
    Panel,
    TextField,
    Dropdown,
    IDropdownOption,
    TagPicker,
    MessageBar,
    Text,
    MessageBarType,
    MarqueeSelection,
    CommandBar,
    ICommandBarItemProps,
    DetailsListLayoutMode,
    DefaultButton,
    IconButton,
    Label,
    Dialog,
    DialogType,
    DialogFooter,
    Spinner,
    SpinnerSize
} from '@fluentui/react';
//import { useAuth } from '../../contexts/AuthContext';
import { 
    DataSet, 
    AIModelType, 
    AI_MODEL_TYPE_LABELS, 
    DataSetListItem,
    CreateDataSetRequest,
    UpdateDataSetRequest,
    DataFileMapping,
    DataFileDto,
    DataFileProcessingStatus
} from '../../types/dataset';
import { FloatingCommandBar } from './FloatingCommandBar';
import { useSelector } from 'react-redux';
import { RootState } from '../../store/store';
import { ISelection, IObjectWithKey } from '@fluentui/react';
// eslint-disable-next-line
import { toast,Toaster } from 'react-hot-toast';
import {
    fetchDataSets,
    createDataSet,
    editDataSet,
    removeDataSet,
    setDataSets,
} from '../../reducers/dataReducer';
import { useAppDispatch } from '../../store/store';
import { useNavigate } from 'react-router-dom';
const DATA_MANAGEMENT_DISCLAIMER = `DISCLAIMER: This tool illustrates an AI model evaluation and benchmarking tool for healthcare. It is not an official Microsoft product, and Microsoft makes no commitment to build such a product. All data you supply to this tool is your sole responsibility.\n\nYou must ensure that any data used with this tool is PHI-free and fully de-identified or anonymized in accordance with all applicable privacy laws, regulations, and organizational policies (e.g., HIPAA, GDPR, or local equivalents). Do not upload, process, or expose any data that could directly or indirectly identify an individual.\n\nBefore using this tool, verify that:\n1. The data has been properly de-identified or anonymized.\n2. Appropriate consents or legal bases for processing have been obtained where required.\n3. You have the legal right, authority, and ownership to use the data, and its use here does not violate any contractual, licensing, or proprietary restrictions.  \n4. All downstream uses of the data remain compliant with relevant laws and regulations.\n\nMicrosoft products and services (1) are not designed, intended, or made available as a medical device, and (2) are not designed or intended to replace professional medical advice, diagnosis, treatment, or judgment and should not be used as a substitute for professional medical advice, diagnosis, treatment, or judgment. Customers and partners are responsible for ensuring that their solutions comply with all applicable laws and regulations.`;


const aiModelTypeOptions: IDropdownOption[] = Object.entries(AI_MODEL_TYPE_LABELS).map(([key, label]) => ({
    key,
    text: label
}));

const commandButtonStyles = {
    
};

interface KeyPath {
    key: string;
    type: 'object' | 'array' | 'string' | 'number' | 'imageurl' | 'addArray';
    index?: number;
    isArray?: boolean;
}

interface JsonlMapping {
    fileName: string;
    inputDataKeyPath: KeyPath[];
    inputDataType: 'text' | 'imageurl';
    availableKeys: string[];
    outputDataKeyPath: KeyPath[];
    outputDataType: 'text';
    isArrayMapping?: boolean;
}
interface JsonMappings {
    inputDataMappings: JsonlMapping[];
    outputDataMappings: JsonlMapping[];
}

const INPUT_TYPE_OPTIONS: IDropdownOption[] = [
    { key: 'text', text: 'Text' },
    { key: 'imageurl', text: 'Image URL' }
];

const OUTPUT_TYPE_OPTIONS: IDropdownOption[] = [
    { key: 'text', text: 'Text' }
];

const parseJsonWithNaN = (text: string) => {
    if(text.includes("NaN")) {
        const processedText = text.replace(/:\s*NaN\s*([,}])/g, ':"NaN"$1');
        return JSON.parse(processedText);
    }else{
        return JSON.parse(text);
    }
};


// Helper function to get value from nested path
const getValueFromPath = (obj: any, keyPath: KeyPath[]): any => {
    if( obj == null) return null;
    return keyPath.reduce((value, pathItem) => {
        if (value === undefined || value === null) return value;
        return value[pathItem.key];
    }, obj);
};

// Helper function to get available keys for current path
const getAvailableKeysForPath = (obj: any, currentPath: KeyPath[]): { key: string; type: 'object' | 'array' | 'string' | 'number' | 'addArray' }[] => {
    if (currentPath.length === 0 && obj != null) {
        return Object.entries(obj).map(([key, value]) => ({
            key,
            type: (typeof value === 'object' ? 
                Array.isArray(value) ? 'array' : 'object' 
                : typeof value) as 'string' | 'number' | 'object' | 'array'
        }));
    }

    // Get the value at the current path
    const value = getValueFromPath(obj, currentPath);

    // Check if we're in array mapping mode
    const isArrayMapping = currentPath.some(p => p.isArray);
    if (isArrayMapping) {
        // If we're in array mapping mode and the current value is an array, show keys of first element
        if (Array.isArray(value) && value.length > 0) {
            const firstElement = value[0];
            if (typeof firstElement === 'object' && firstElement !== null && !Array.isArray(firstElement)) {
                return Object.entries(firstElement).map(([key, value]) => ({
                    key,
                    type: (typeof value === 'object' ? 
                        Array.isArray(value) ? 'array' : 'object' 
                        : typeof value) as 'string' | 'number' | 'object' | 'array'
                }));
            }
        }
        // If we've selected a path within an array object, continue navigating normally
        return [];
    }

    // If we're at a terminal value (string/number), keep showing the current level's keys
    if (typeof value === 'string' || typeof value === 'number') {
        const parentPath = currentPath.slice(0, -1);
        const parentValue = getValueFromPath(obj, parentPath);
        
        return Object.entries(parentValue).map(([key, value]) => ({
            key,
            type: (typeof value === 'object' ? 
                Array.isArray(value) ? 'array' : 'object' 
                : typeof value) as 'string' | 'number' | 'object' | 'array'
        }));
    }

    // For objects (including arrays), show all keys/indices
    if (typeof value === 'object' && value !== null) {
        if (Array.isArray(value)) {
            // For arrays, create entries for each index AND add the "add array" option
            const indexEntries: { key: string; type: 'object' | 'array' | 'string' | 'number' | 'addArray' }[] = value.map((item, index) => ({
                key: index.toString(),
                type: (typeof item === 'object' ? 
                    Array.isArray(item) ? 'array' : 'object' 
                    : typeof item) as 'string' | 'number' | 'object' | 'array'
            }));
            
            // Add the "add array" option
            indexEntries.push({
                key: 'add array',
                type: 'addArray'
            });
            
            return indexEntries;
        }
        
        return Object.entries(value).map(([key, value]) => ({
            key,
            type: (typeof value === 'object' ? 
                Array.isArray(value) ? 'array' : 'object' 
                : typeof value) as 'string' | 'number' | 'object' | 'array'
        }));
    }

    return [];
};
interface DataSetWithLoading extends DataSetListItem {
    isLoading?: boolean;
    dataFiles?: DataFileDto[];
}

export const DataManagement: React.FC = () => {
    //const { user } = useAuth();
    const dispatch = useAppDispatch();
    const { datasets, error: reduxError } = useSelector((state: RootState) => state.data);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    // Collapsible disclaimer state
    const [disclaimerOpen, setDisclaimerOpen] = useState(true);
    // Open disclaimer by default each time the panel is opened
    useEffect(() => {
        if (isPanelOpen) setDisclaimerOpen(true);
    }, [isPanelOpen]);
    // ...existing code...
    const [currentDataSet, setCurrentDataSet] = useState<Partial<DataSet>>({});
    const [error, setError] = useState<string>('');
    const [selectedItems, setSelectedItems] = useState<DataSetListItem[]>([]);
    const [sortKey, setSortKey] = useState<string>('');
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);
    const [jsonlMappings, setJsonlMappings] = useState<JsonMappings | null>(null);
    const [jsonlContent, setJsonlContent] = useState<any[]>([]);
    const [jsonlError, setJsonlError] = useState<string>('');
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [isSaving, setIsSaving] = useState(false);
    

    const [currentFile, setCurrentFile] = useState<File | null>(null);
    const [currentFileName, setCurrentFileName] = useState<string>('');
    const [currentInputForm, setCurrentInputForm] = useState<JsonlMapping>({
        fileName: '',
        inputDataKeyPath: [],
        inputDataType: 'text',
        availableKeys: [],
        outputDataKeyPath: [],
        outputDataType: 'text'
    });
    const [currentOutputForm, setCurrentOutputForm] = useState<JsonlMapping>({
        fileName: '',
        inputDataKeyPath: [],
        inputDataType: 'text',
        availableKeys: [],
        outputDataKeyPath: [],
        outputDataType: 'text'
    });
    const [showConfirmDialog, setShowConfirmDialog] = useState(false);
    const [pendingInputMapping, setPendingInputMapping] = useState<any>(null);
    const [localDatasets, setLocalDatasets] = useState<DataSetWithLoading[]>([]);
    const [pendingOutputMapping, setPendingOutputMapping] = useState<any>(null);
    const navigate = useNavigate();

    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            const selected = selection.getSelection() as DataSetListItem[];
            setSelectedItems(selected);
        },
        getKey: (item: IObjectWithKey) => (item as DataSetListItem).id,
    }));

    useEffect(() => {
        dispatch(fetchDataSets());
    }, [dispatch]);

    useEffect(() => {
        const hasProcessingFiles = datasets?.some(ds => ds.files?.some(
            df => df.processingStatus === DataFileProcessingStatus.Processing || df.processingStatus === DataFileProcessingStatus.Unprocessed
        ));
        
        let intervalId: NodeJS.Timeout;
        
        if (hasProcessingFiles) {
            intervalId = setInterval(() => {
                dispatch(fetchDataSets());
            }, 5000);
        }
        
        return () => {
            if (intervalId) {
                clearInterval(intervalId);
            }
        };
    }, [datasets, dispatch]);

    useEffect(() => {
        if (reduxError) {
            setError(reduxError);
            if( reduxError !== "Network Error") {
                dispatch(fetchDataSets());
            }
            console.error("redux error", reduxError);
            
        }
    }, [reduxError, dispatch]);

    useEffect(() => {
        setLocalDatasets(datasets);
    }, [datasets]);


    const handleDelete = async () => {
        try {
            for (const item of selectedItems) {
                await dispatch(removeDataSet(item.id));
            }
            setSelectedItems([]);
            selection.setAllSelected(false);
            toast.success('Dataset(s) deleted successfully');
            dispatch(fetchDataSets());
        } catch (err) {
            setError('Failed to delete dataset(s)');
        }
    };

    const handleFileSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file) return;
        setCurrentFile(file);
        try {
            const text = await file.text();
            const lines = text.trim().split('\n');
            if (lines.length === 0) {
                setJsonlError('File is empty');
                return;
            }

            try {
                const firstObject = parseJsonWithNaN(lines[0]);
                const keys = Object.keys(firstObject);
                setCurrentFileName(file.name);
                setJsonlMappings({
                    inputDataMappings: [],
                    outputDataMappings: []
                });
                setCurrentInputForm({
                    fileName: file.name,
                    inputDataKeyPath: [],
                    inputDataType: 'text',
                    availableKeys: keys,
                    outputDataKeyPath: [],
                    outputDataType: 'text'
                });
                setCurrentOutputForm({
                    fileName: file.name,
                    inputDataKeyPath: [],
                    inputDataType: 'text',
                    availableKeys: keys,
                    outputDataKeyPath: [],
                    outputDataType: 'text'
                });

                setJsonlContent(lines.map(line => {
                    if (!line.trim()) return null;
                    return parseJsonWithNaN(line);
                }).filter(obj => obj !== null));
                
                setJsonlError('');
            } catch (e) {
                console.error(e);
                setJsonlError('Invalid JSONL format');
            }
        } catch (e) {
            setJsonlError('Error reading file');
        }
    };

    const handleAddInputMapping = () => {
        if (currentInputForm.inputDataKeyPath.length > 0) {
            var newInputForm = { ...currentInputForm };
            
            // Check if this is an array mapping
            const isArrayMapping = newInputForm.inputDataKeyPath.some(p => p.isArray);
            newInputForm.isArrayMapping = isArrayMapping;
            
            if( typeof getValueFromPath(jsonlContent[0], newInputForm.inputDataKeyPath) == "object" && !isArrayMapping) {
                setPendingInputMapping(newInputForm);
                setShowConfirmDialog(true);
                return;
            }
            setJsonlMappings(prev => prev ? {
                ...prev,
                inputDataMappings: [...prev.inputDataMappings, { ...newInputForm }]
            } : null);
            setCurrentInputForm(prev => ({
                ...prev,
                inputDataKeyPath: [],
                inputDataType: 'text',
                availableKeys: prev.availableKeys,
                isArrayMapping: false
            }));
        }
    };
    useEffect(() => {
        setLocalDatasets(datasets);
    }, [datasets]);
    const handleAddOutputMapping = () => {
        if (currentOutputForm.outputDataKeyPath.length > 0) {
            var newOutputForm = { ...currentOutputForm };
            
            // Check if this is an array mapping
            const isArrayMapping = newOutputForm.outputDataKeyPath.some(p => p.isArray);
            newOutputForm.isArrayMapping = isArrayMapping;
            
            if( typeof getValueFromPath(jsonlContent[0], newOutputForm.outputDataKeyPath) == "object" && !isArrayMapping) {
                setPendingOutputMapping(newOutputForm);
                setShowConfirmDialog(true);
                return;
            }
            setJsonlMappings(prev => prev ? {
                ...prev,
                outputDataMappings: [...prev.outputDataMappings, { ...newOutputForm }]
            } : null);
            setCurrentOutputForm(prev => ({
                ...prev,
                outputDataKeyPath: [],
                outputDataType: 'text',
                availableKeys: prev.availableKeys,
                isArrayMapping: false
            }));
        }
    };

    const handleSubmit = async () => {
        try {
            setIsSaving(true);
            if (!currentDataSet.origin || !currentDataSet.aiModelType) {
                setError('Origin and AI Model Type are required');
                return;
            }
            if (jsonlMappings && jsonlMappings.inputDataMappings.length === 0) {
                setError('At least one input mapping is required');
                return;
            }
            
            // Create new dataset object with loading state
            const tempDataset: DataSetWithLoading = {
                id: currentDataSet.id || '',
                name: currentDataSet.name || '',
                origin: currentDataSet.origin || '',
                description: currentDataSet.description || '',
                aiModelType: currentDataSet.aiModelType as AIModelType,
                tags: currentDataSet.tags || [],
                dataObjectCount: jsonlContent.length,
                modelOutputCount: jsonlMappings?.outputDataMappings.length || 0,
                isLoading: true,
                generatedDataList: [],
                daysToAutoDelete: currentDataSet.daysToAutoDelete || 180,
                createdAt: currentDataSet.createdAt || new Date().toISOString(),
                deletedAt: currentDataSet.deletedAt
            };
            // Update UI immediately
            if (currentDataSet.id) {
                setLocalDatasets(prev => prev.map(ds => 
                    ds.id === currentDataSet.id ? tempDataset : ds
                ));
            } else {
                setLocalDatasets(prev => [...prev, tempDataset]);
            }

            setIsPanelOpen(false);
            setCurrentDataSet({});
            setJsonlMappings(null);
            setCurrentFileName('');
            setJsonlContent([]);

            if (currentDataSet.id) {
                const updateRequest: UpdateDataSetRequest = {
                    id: currentDataSet.id,
                    name: currentDataSet.name || '',
                    origin: currentDataSet.origin || '',
                    description: currentDataSet.description || '',
                    aiModelType: currentDataSet.aiModelType as AIModelType,
                    tags: currentDataSet.tags || [],
                    modelOutputCount: jsonlMappings?.outputDataMappings.length || 0,
                    daysToAutoDelete: currentDataSet.daysToAutoDelete || 180
                };
                await dispatch(editDataSet(updateRequest));

                dispatch(fetchDataSets());
            } else {
                var mapping: DataFileMapping | null = null;
                if(jsonlMappings) {
                mapping = {
                    inputMappings: jsonlMappings.inputDataMappings.map(mapping => ({
                        keyPath: mapping.inputDataKeyPath.map(keyPath => keyPath.key),
                        type: mapping.inputDataType,
                        isArray: mapping.isArrayMapping || false
                    })),
                        outputMappings: jsonlMappings.outputDataMappings.map(mapping => ({
                            keyPath: mapping.outputDataKeyPath.map(keyPath => keyPath.key),
                            type: mapping.outputDataType,
                            isArray: mapping.isArrayMapping || false
                        }))
                    };
                }
                console.log("creating new dataset",currentDataSet);
                const createRequest: CreateDataSetRequest = {
                    name: currentDataSet.name || '',
                    origin: currentDataSet.origin || '',
                    description: currentDataSet.description || '',
                    aiModelType: currentDataSet.aiModelType as AIModelType,
                    tags: currentDataSet.tags || [],
                    modelOutputCount: jsonlMappings?.outputDataMappings.length || 0,
                    file: currentFile,
                    mapping: mapping,
                    daysToAutoDelete: currentDataSet.daysToAutoDelete || 180
                };
                await dispatch(createDataSet(createRequest));
                
            }
            toast.success('Dataset saved successfully');
            
            setIsPanelOpen(false);
            setCurrentDataSet({});
            setJsonlMappings(null);
            setCurrentFileName('');
            setJsonlContent([]);
            if (fileInputRef.current) {
                fileInputRef.current.value = '';
            }
        } catch (err) {
            dispatch(fetchDataSets());
            console.error('Save error:', err);
            setError('Failed to save dataset');
        } finally {
            setIsSaving(false);
        }
    };

    const onColumnClick = (ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
        const newIsSortedDescending = column.key === sortKey ? !isSortedDescending : false;
        setSortKey(column.key);
        setIsSortedDescending(newIsSortedDescending);

        const sortedItems = [...datasets].sort((a, b) => {
            let aValue = a[column.key as keyof DataSetListItem];
            let bValue = b[column.key as keyof DataSetListItem];

            // Handle special cases
            if (column.key === 'tags') {
                aValue = (a.tags || []).join(', ');
                bValue = (b.tags || []).join(', ');
            } else if (column.key === 'aiModelType') {
                aValue = AI_MODEL_TYPE_LABELS[a.aiModelType];
                bValue = AI_MODEL_TYPE_LABELS[b.aiModelType];
            }

            // Handle null/undefined values
            if (!aValue) return newIsSortedDescending ? 1 : -1;
            if (!bValue) return newIsSortedDescending ? -1 : 1;

            // Compare values
            const compareResult = aValue > bValue ? 1 : -1;
            return newIsSortedDescending ? -compareResult : compareResult;
        });

        dispatch(setDataSets(sortedItems));
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
            key: 'origin', 
            name: 'Origin', 
            fieldName: 'origin', 
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'origin',
            isSortedDescending: sortKey === 'origin' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
        },
        { 
            key: 'description', 
            name: 'Description', 
            fieldName: 'description', 
            minWidth: 200,
            isResizable: true,
            isSorted: sortKey === 'description',
            isSortedDescending: sortKey === 'description' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
        },
        { 
            key: 'aiModelType', 
            name: 'AI Model Type', 
            fieldName: 'aiModelType', 
            minWidth: 150,
            isResizable: true,
            isSorted: sortKey === 'aiModelType',
            isSortedDescending: sortKey === 'aiModelType' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: DataSet) => AI_MODEL_TYPE_LABELS[item.aiModelType]
        },
        { 
            key: 'tags', 
            name: 'Tags', 
            fieldName: 'tags', 
            minWidth: 150,
            isResizable: true,
            isSorted: sortKey === 'tags',
            isSortedDescending: sortKey === 'tags' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: DataSet) => item.tags?.join(', ')
        },
        { 
            key: 'dataObjectCount', 
            name: 'Data Objects', 
            fieldName: 'dataObjectCount', 
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'dataObjectCount',
            isSortedDescending: sortKey === 'dataObjectCount' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: DataSetWithLoading) => (
                item.isLoading || item.dataFiles?.some(file => file.processingStatus === DataFileProcessingStatus.Processing || file.processingStatus === DataFileProcessingStatus.Unprocessed) ? 
                    <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}><Spinner size={SpinnerSize.small} />uploading...</div> : 
                    item.dataObjectCount
            ),
        },
        { 
            key: 'retention', 
            name: 'Retention', 
            minWidth: 120,
            isResizable: true,
            onRender: (item: DataSet) => {
                const createdDate = new Date(item.createdAt);
                const now = new Date();
                const daysSinceCreation = Math.floor((now.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24));
                const daysRemaining = Math.max(0, item.daysToAutoDelete - daysSinceCreation);
                
                const isExpiringSoon = daysRemaining <= 30;
                const isExpired = daysRemaining === 0;
                
                return (
                    <div style={{ 
                        color: isExpired ? 'red' : isExpiringSoon ? 'orange' : 'inherit',
                        fontWeight: isExpiringSoon ? 'bold' : 'normal'
                    }}>
                        {isExpired ? 'Expired' : `${daysRemaining} days left`}
                    </div>
                );
            }
        }
    ];

    const getCommandItems = (): ICommandBarItemProps[] => {
        const isItemSelected = selectedItems.length === 1;

        return [
            {
                key: 'add',
                text: 'Add Dataset',
                iconProps: { iconName: 'Add' },
                onClick: () => {
                    setCurrentDataSet({});
                    setJsonlMappings({
                        inputDataMappings: [],
                        outputDataMappings: []
                    });
                    setJsonlContent([]);
                    if (fileInputRef.current) {
                        fileInputRef.current.value = '';
                    }
                    setError('');
                    setIsPanelOpen(true);
                },
                buttonStyles: commandButtonStyles
            },
            {
                key: 'edit',
                text: 'Edit',
                iconProps: { iconName: 'Edit' },
                disabled: !isItemSelected,
                onClick: () => {
                    setCurrentDataSet(selectedItems[0]);
                    setIsPanelOpen(true);
                },
                buttonStyles: commandButtonStyles
            },
            {
                key: 'delete',
                text: 'Delete',
                iconProps: { iconName: 'Delete' },
                disabled: selectedItems.length === 0,
                onClick: handleDelete,
                buttonStyles: commandButtonStyles
            },
            {
                key: 'explore',
                text: 'Explore',
                iconProps: { iconName: 'Search' },
                disabled: !isItemSelected || !selectedItems[0].id,
                onClick: () => {
                    if (selectedItems[0] && selectedItems[0].id) {
                        navigate(`/admin/data/${selectedItems[0].id}`);
                    }
                },
                buttonStyles: commandButtonStyles
            }
        ];
    };

    const renderKeyPathPicker = (
        isInput: boolean,
        currentPath: KeyPath[],
        onPathChange: (newPath: KeyPath[]) => void
    ) => {
        const availableKeys = getAvailableKeysForPath(jsonlContent[0], currentPath);
        const currentValue = getValueFromPath(jsonlContent[0], currentPath);
        const isTerminal = typeof currentValue === 'string' || typeof currentValue === 'number';

        const pathString = currentPath
            .map(p => p.key)
            .join(', ');
        
        const labelPrefix = isInput ? 'Input' : 'Output';
        const label = pathString ? `Current ${labelPrefix} Data Key Path: ${pathString}` : `Select a ${labelPrefix} Data Key Path`;

        return (
            <Stack tokens={{ childrenGap: 10 }}>
                { isTerminal && (
                    <Label>{label}</Label>
                )}
                { ! isTerminal && (
                   <Dropdown
                        label={label}
                        placeholder={`Select ${currentPath.length > 0 ? "next" : "a"} key in the JSONL file`}
                        options={availableKeys.map(({ key, type }) => ({
                            key,
                            text: `${key}${type === 'object' ? ' (Object)' : type === 'array' ? ' (Array)' : type === 'addArray' ? ' (Add Array)' : ''}`
                        }))}
                        selectedKey={null}
                        onChange={(_, option) => {
                            if (option) {
                                const keyInfo = availableKeys.find(k => k.key === option.key);
                                if (keyInfo) {
                                    if (keyInfo.type === 'addArray') {
                                        // Handle "add array" selection
                                        const currentArrayValue = getValueFromPath(jsonlContent[0], currentPath);
                                        if (Array.isArray(currentArrayValue) && currentArrayValue.length > 0) {                                            
                                            // Mark the current path as an array mapping
                                            const newPath = [...currentPath];
                                            if (newPath.length > 0) {
                                                newPath[newPath.length - 1].isArray = true;
                                            }
                                            
                                            onPathChange(newPath);
                                        }
                                    } else {
                                        // Handle regular key selection
                                        const testPath = [...currentPath, {
                                            key: keyInfo.key,
                                            type: keyInfo.type,
                                            index: keyInfo.type === 'array' ? 0 : undefined
                                        }];
                                        onPathChange(testPath);
                                    }
                                }
                            }
                        }}
                    />
                )}
                {currentPath.length > 0 && (
                    <DefaultButton
                        text="Go Back One Level"
                        onClick={() => onPathChange(currentPath.slice(0, -1))}
                    />
                )}
            </Stack>
        );
    };

    return (
        <Stack tokens={{ childrenGap: 20 }} styles={{ root: { padding: 20 } }}>
            <Stack horizontal horizontalAlign="space-between">
                <Text variant="xxLarge">DataSet Management</Text>
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error} onDismiss={() => setError('')}>
                    {error}
                </MessageBar>
            )}

            <FloatingCommandBar
                items={getCommandItems()}
                parentId='adminContent'
                stickOffsetId='navigationbar'
            />

            <MarqueeSelection selection={selection}>
                <DetailsList
                    items={localDatasets}
                    columns={columns}
                    selection={selection}
                    selectionMode={SelectionMode.multiple}
                    setKey="set"
                    layoutMode={DetailsListLayoutMode.justified}
                    selectionPreservedOnEmptyClick={true}
                />
            </MarqueeSelection>

            <Panel
                isOpen={isPanelOpen}
                onDismiss={() => setIsPanelOpen(false)}
                headerText={currentDataSet.id ? 'Edit DataSet' : 'Add DataSet'}
                closeButtonAriaLabel="Close"
                styles={{
                    main: {
                        '@media (max-width: 768px)': {
                            width: '100% !important',
                            maxWidth: '100% !important',
                        },
                        '@media (min-width: 480px)': {
                            width: '840px'
                        }
                    }
                }}
                onRenderFooterContent={() => (
                    <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                        <PrimaryButton 
                            onClick={handleSubmit}
                            disabled={isSaving}
                        >
                            {isSaving ? 'Saving...' : 'Save'}
                        </PrimaryButton>
                        <DefaultButton 
                            onClick={() => {
                                setIsPanelOpen(false);
                                setCurrentDataSet({});
                                setJsonlMappings(null);
                                setCurrentFileName('');
                                setJsonlContent([]);
                            }}
                            disabled={isSaving}
                        >
                            Cancel
                        </DefaultButton>
                    </Stack>
                )}
            >
                <Stack tokens={{ childrenGap: 15 }}>
                    {/* Collapsible disclaimer above Name field */}
                    <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                        <IconButton
                            iconProps={{ iconName: disclaimerOpen ? 'ChevronDown' : 'ChevronRight' }}
                            title={disclaimerOpen ? 'Hide disclaimer' : 'Show disclaimer'}
                            ariaLabel="Toggle disclaimer"
                            onClick={() => setDisclaimerOpen(open => !open)}
                            styles={{ root: { marginLeft: -8, marginRight: 0 } }}
                        />
                        <Text variant="medium" styles={{ root: { fontWeight: 600 } }}>Data Use Disclaimer</Text>
                    </Stack>
                    {disclaimerOpen && (
                        <div style={{
                            background: '#f3f2f1',
                            border: '1px solid #e1dfdd',
                            borderRadius: 6,
                            padding: 14,
                            marginBottom: 8,
                            whiteSpace: 'pre-line',
                            fontSize: 14,
                            color: '#333',
                            maxHeight: 320,
                            overflowY: 'auto'
                        }}>
                            {DATA_MANAGEMENT_DISCLAIMER}
                        </div>
                    )}
                    <TextField
                        label="Name"
                        required
                        value={currentDataSet.name || ''}
                        onChange={(_, value) => 
                            setCurrentDataSet(prev => ({ ...prev, name: value }))}
                    />
                    <TextField
                        label="Origin"
                        required
                        value={currentDataSet.origin || ''}
                        onChange={(_, value) => 
                            setCurrentDataSet(prev => ({ ...prev, origin: value }))}
                    />
                    <TextField
                        label="Description"
                        multiline
                        value={currentDataSet.description || ''}
                        onChange={(_, value) => 
                            setCurrentDataSet(prev => ({ ...prev, description: value }))}
                    />
                    <Dropdown
                        label="AI Model Type"
                        required
                        options={aiModelTypeOptions}
                        selectedKey={currentDataSet.aiModelType}
                        onChange={(_, option) => 
                            setCurrentDataSet(prev => ({ ...prev, aiModelType: option?.key as AIModelType }))}
                    />
                    <TagPicker
                        label="Tags"
                        onResolveSuggestions={(filterText) => {
                            if (filterText) {
                                return [{ key: filterText, name: filterText }];
                            }
                            return [];
                        }}
                        selectedItems={currentDataSet.tags?.map(tag => ({ key: tag, name: tag })) || []}
                        onChange={(items) => {
                            const tags = items ? items.map(item => item.name) : [];
                            setCurrentDataSet(prev => ({ ...prev, tags }));
                        }}
                        itemLimit={10}
                        onInputChange={(input) => {
                            if (input && input.endsWith(',')) {
                                const newTag = input.slice(0, -1).trim();
                                if (newTag) {
                                    const currentTags = currentDataSet.tags || [];
                                    if (!currentTags.includes(newTag)) {
                                        setCurrentDataSet(prev => ({
                                            ...prev,
                                            tags: [...currentTags, newTag]
                                        }));
                                    }
                                }
                                return '';
                            }
                            return input;
                        }}
                        pickerSuggestionsProps={{
                            noResultsFoundText: 'Type any tag and press Enter or comma to add',
                            suggestionsHeaderText: 'Suggested Tags'
                        }}
                    />
                    <TextField
                        label="Days to Auto Delete"
                        type="number"
                        value={currentDataSet.daysToAutoDelete?.toString() || '180'}
                        onChange={(_, value) => 
                            setCurrentDataSet(prev => ({ 
                                ...prev, 
                                daysToAutoDelete: parseInt(value || '180') 
                            }))}
                        description="Number of days after creation when this dataset will be automatically deleted. Default is 180 days."
                        min={1}
                        max={3650} // ~10 years max
                    />
                    {!currentDataSet.id && (
                    <>
                    <Stack.Item>
                        <Text>Data Objects from JSONL</Text>
                        <input
                            type="file"
                            accept=".jsonl"
                            onChange={handleFileSelect}
                            ref={fileInputRef}
                            style={{ display: 'none' }}
                        /><br />
                        <PrimaryButton
                            text={currentFileName ? `Selected: ${currentFileName}` : "Select JSONL File"}
                            onClick={() => fileInputRef.current?.click()}
                        />
                    </Stack.Item>

                    {jsonlError && (
                        <MessageBar messageBarType={MessageBarType.error}>
                            {jsonlError}
                        </MessageBar>
                    )}
                    
                        <Stack tokens={{ childrenGap: 10 }} styles={{ root: { border: '1px solid #ccc', padding: 5 } }}>
                        {currentFileName && (<Text>Add Key Paths for Input Data Objects</Text>)}
                       
                    

                    {jsonlMappings?.inputDataMappings.map((mapping, index) => (
                        <Stack 
                            key={index} 
                            horizontal 
                            verticalAlign="center" 
                            tokens={{ childrenGap: 10 }}
                        >
                            <Text>
                                {`${mapping.isArrayMapping ? '(array) ' : ''}Input ${index + 1}: ${mapping.inputDataKeyPath.map(p => p.key).join('.')} (${mapping.inputDataType})`}
                            </Text>
                            <IconButton
                                iconProps={{ iconName: 'Delete' }}
                                onClick={() => {
                                    setJsonlMappings(prev => prev ? {
                                        ...prev,
                                        inputDataMappings: prev.inputDataMappings.filter((_, i) => i !== index)
                                    } : null)
                                }}
                            />
                        </Stack>
                    ))}
                    
                    {jsonlContent.length > 0 && (
                        <Stack tokens={{ childrenGap: 10 }}>
                            {renderKeyPathPicker(
                                true,
                                currentInputForm.inputDataKeyPath,
                                (newPath) => {
                                    setCurrentInputForm(prev => ({
                                        ...prev,
                                        inputDataKeyPath: newPath
                                    }));
                                }
                            )}
                            {currentInputForm.inputDataKeyPath.length > 0  && (
                                <>
                                    <Dropdown
                                        label="Input Data Type"
                                        options={INPUT_TYPE_OPTIONS}
                                        selectedKey={currentInputForm.inputDataType}
                                        onChange={(_, option) => {
                                            setCurrentInputForm(prev => ({
                                                ...prev,
                                                inputDataType: option?.key as 'text' | 'imageurl'
                                            }));
                                        }}
                                    />
                                    {currentInputForm.inputDataKeyPath.length > 0 && (
                                        <PrimaryButton
                                            text="Add Input Mapping"
                                            onClick={handleAddInputMapping}
                                        />
                                    )}
                                </>
                            )}
                        </Stack>
                    )}
                     </Stack>

                     <Stack tokens={{ childrenGap: 10 }} styles={{ root: { border: '1px solid #ccc', padding: 5 } }}>
                        {currentFileName && (<Text>Add Key Paths for Pregenerated Output Data Objects</Text>)}

                    {jsonlMappings?.outputDataMappings.map((mapping, index) => (
                        <Stack 
                            key={index} 
                            horizontal 
                            verticalAlign="center" 
                            tokens={{ childrenGap: 10 }}
                        >
                            <Text>
                                {`${mapping.isArrayMapping ? '(array) ' : ''}Output ${index + 1}: ${mapping.outputDataKeyPath.map(p => p.key).join('.')} (${mapping.outputDataType})`}
                            </Text>
                            <IconButton
                                iconProps={{ iconName: 'Delete' }}
                                onClick={() => {
                                    setJsonlMappings(prev => prev ? {
                                        ...prev,
                                        outputDataMappings: prev.outputDataMappings.filter((_, i) => i !== index)
                                    } : null)
                                }}
                            />
                        </Stack>
                    ))}
                    
                    {jsonlContent.length > 0 && (
                        <Stack tokens={{ childrenGap: 10 }}>
                            {renderKeyPathPicker(
                                false,
                                currentOutputForm.outputDataKeyPath,
                                (newPath) => {
                                    setCurrentOutputForm(prev => ({
                                        ...prev,
                                        outputDataKeyPath: newPath
                                    }));
                                }
                            )}
                            {currentOutputForm.outputDataKeyPath.length > 0 && (
                                <>
                                    <Dropdown
                                        label="Output Data Type"
                                        options={OUTPUT_TYPE_OPTIONS}
                                        selectedKey={currentOutputForm.outputDataType}
                                        onChange={(_, option) => {
                                            setCurrentOutputForm(prev => ({
                                                ...prev,
                                                outputDataType: option?.key as 'text'
                                            }));
                                        }}
                                    />
                                    {currentOutputForm.outputDataKeyPath.length > 0 && (
                                        <PrimaryButton
                                            text="Add Output Mapping"
                                            onClick={handleAddOutputMapping}
                                        />
                                    )}
                                </>
                            )}
                        </Stack>
                    )}
                    </Stack>
                    </>)}
                </Stack>
            </Panel>

            <Dialog
                hidden={!showConfirmDialog}
                onDismiss={() => setShowConfirmDialog(false)}
                dialogContentProps={{
                    type: DialogType.normal,
                    title: 'Confirm Mapping',
                    subText: 'The selected key path contains raw json data. Are you sure you want to add this mapping?'
                }}
            >
                <DialogFooter>
                    <PrimaryButton onClick={() => {
                        setShowConfirmDialog(false);
                        setIsPanelOpen(true);
                        if (pendingInputMapping) {
                            setCurrentInputForm({
                                fileName: pendingInputMapping.fileName,
                                inputDataKeyPath: [],
                                inputDataType: 'text',
                                availableKeys: [],
                                outputDataKeyPath: [],
                                outputDataType: 'text'
                            });
                            setJsonlMappings(prev => prev ? {
                                ...prev,
                                inputDataMappings: [...prev.inputDataMappings, { ...pendingInputMapping }]
                            } : null);
                            setPendingInputMapping(null);
                        }
                        if (pendingOutputMapping) {
                            setCurrentOutputForm({
                                fileName: pendingOutputMapping.fileName,
                                inputDataKeyPath: [],
                                inputDataType: 'text',
                                availableKeys: [],
                                outputDataKeyPath: [],
                                outputDataType: 'text'
                            });
                            setJsonlMappings(prev => prev ? {
                                ...prev,
                                outputDataMappings: [...prev.outputDataMappings, { ...pendingOutputMapping }]
                            } : null);
                            setPendingOutputMapping(null);
                        }
                    }} text="Yes" />
                    <DefaultButton onClick={() => {
                        setShowConfirmDialog(false);
                        setIsPanelOpen(true);
                        setPendingInputMapping(null);
                        setPendingOutputMapping(null);
                    }} text="No" />
                </DialogFooter>
            </Dialog>
        </Stack>
    );
};

// Add empty export to ensure file is treated as a module
export {}; 