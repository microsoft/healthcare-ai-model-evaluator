import React, { useEffect, useState, useRef, useCallback } from 'react';
import { 
    Stack, 
    DetailsList,
    SelectionMode,
    IColumn,
    Text,
    Breadcrumb,
    IBreadcrumbItem,
    CommandBar,
    ICommandBarItemProps,
    Selection,
    Panel,
    PrimaryButton,
    DefaultButton,
    Dropdown,
    IDropdownOption,
    MessageBar,
    MessageBarType,
    Dialog,
    DialogType,
    DialogFooter,
    ProgressIndicator,
    Pivot,
    PivotItem,
    IconButton,
    Label
} from '@fluentui/react';
import { useNavigate, useParams } from 'react-router-dom';
import { DataObject, DataSet, DataFileDto, DataFileProcessingStatus, DataFileMapping } from '../../types/dataset';
import { dataSetService } from '../../services/dataSetService';

const INPUT_TYPE_OPTIONS: IDropdownOption[] = [
    { key: 'text', text: 'Text' },
    { key: 'imageurl', text: 'Image URL' }
];

const OUTPUT_TYPE_OPTIONS: IDropdownOption[] = [
    { key: 'text', text: 'Text' }
];

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
    if (obj == null) return null;
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

export const DatasetDetails: React.FC = () => {
    const [dataObjects, setDataObjects] = useState<DataObject[]>([]);
    const [dataSet, setDataSet] = useState<DataSet | null>(null);
    const [dataFiles, setDataFiles] = useState<DataFileDto[]>([]);
    const [selectedDataObjects, setSelectedDataObjects] = useState<DataObject[]>([]);
    const [selectedDataFile, setSelectedDataFile] = useState<DataFileDto | null>(null);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [currentFile, setCurrentFile] = useState<File | null>(null);
    const [error, setError] = useState<string>('');
    const [isUploading, setIsUploading] = useState(false);
    const [confirmDelete, setConfirmDelete] = useState(false);
    const [selectedDataFileIndex, setSelectedDataFileIndex] = useState<number>(-1);
    
    const [jsonlMappings, setJsonlMappings] = useState<JsonMappings | null>(null);
    const [jsonlContent, setJsonlContent] = useState<any[]>([]);
    const [jsonlError, setJsonlError] = useState<string>('');
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
    const [pendingOutputMapping, setPendingOutputMapping] = useState<any>(null);
    
    const fileInputRef = useRef<HTMLInputElement>(null);
    const navigate = useNavigate();
    const { datasetId } = useParams();

    const dataObjectSelection = new Selection({
        onSelectionChanged: () => {
            const selected = dataObjectSelection.getSelection() as DataObject[];
            setSelectedDataObjects(selected);
        }
    });
    
    const dataFileSelection = new Selection({
        onSelectionChanged: () => {
            const selected = dataFileSelection.getSelection() as DataFileDto[];
            if (selected.length > 0) {
                setSelectedDataFile(selected[0]);
                setSelectedDataFileIndex(dataFiles.findIndex(df => df.fileName === selected[0].fileName));
            } else {
                setSelectedDataFile(null);
                setSelectedDataFileIndex(-1);
            }
        }
    });

    const loadData = useCallback(async () => {
        if (!datasetId) return;
        
        try {
            const dataset = await dataSetService.getDataSet(datasetId);
            setDataSet(dataset);
            
            const objects = await dataSetService.getDataObjects(datasetId);
            setDataObjects(objects);
            
            const files = await dataSetService.getDataFiles(datasetId);
            setDataFiles(files);
        } catch (error) {
            console.error("Error loading data:", error);
            setError("Failed to load dataset details");
        }
    }, [datasetId]);

    // First useEffect for initial data load and processing status check
    useEffect(() => {
        loadData();
    }, [loadData]); // Only depends on loadData which is memoized with useCallback

    // Separate useEffect for polling when files are processing
    useEffect(() => {
        const hasProcessingFiles = dataFiles.some(
            df => df.processingStatus === DataFileProcessingStatus.Processing || df.processingStatus === DataFileProcessingStatus.Unprocessed
        );
        
        let intervalId: NodeJS.Timeout;
        
        if (hasProcessingFiles) {
            intervalId = setInterval(() => {
                loadData();
            }, 5000);
        }
        
        return () => {
            if (intervalId) {
                clearInterval(intervalId);
            }
        };
    }, [dataFiles, loadData]); // Remove datasetId since it's included in loadData

    // Check for updates in dataFiles
    useEffect(() => {
        const hasProcessingFiles = dataFiles.some(
            df => df.processingStatus === DataFileProcessingStatus.Processing || df.processingStatus === DataFileProcessingStatus.Unprocessed
        );
        
        if (!hasProcessingFiles) {
            // If no files are processing, reload the data objects
            if (datasetId) {
                dataSetService.getDataObjects(datasetId)
                    .then(objects => setDataObjects(objects))
                    .catch(err => console.error("Error loading data objects:", err));
            }
        }
    }, [dataFiles, datasetId]);

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
            
            if (typeof getValueFromPath(jsonlContent[0], newInputForm.inputDataKeyPath) == "object" && !isArrayMapping) {
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

    const handleAddOutputMapping = () => {
        if (currentOutputForm.outputDataKeyPath.length > 0) {
            var newOutputForm = { ...currentOutputForm };
            
            // Check if this is an array mapping
            const isArrayMapping = newOutputForm.outputDataKeyPath.some(p => p.isArray);
            newOutputForm.isArrayMapping = isArrayMapping;
            
            if (typeof getValueFromPath(jsonlContent[0], newOutputForm.outputDataKeyPath) == "object" && !isArrayMapping) {
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

    const handleUploadDataFile = async () => {
        if (!currentFile || !datasetId) return;


        
        if (jsonlMappings && jsonlMappings.inputDataMappings.length === 0) {
            setError("At least one input mapping is required");
            return;
        }
        console.log(dataFiles);
        if ( dataFiles.length > 0 && jsonlMappings && dataFiles[0].mapping && (dataFiles[0].mapping.inputMappings.length !== jsonlMappings.inputDataMappings.length || dataFiles[0].mapping.outputMappings.length !== jsonlMappings.outputDataMappings.length)) {
            setError("The number of input and output mappings must match the number of mappings in the existing data files");
            return;
        }
        
        setIsUploading(true);
        
        try {
            // Convert our internal mapping format to the API format
            const mapping: DataFileMapping = {
                inputMappings: jsonlMappings?.inputDataMappings.map(mapping => ({
                    keyPath: mapping.inputDataKeyPath.map(keyPath => keyPath.key),
                    type: mapping.inputDataType,
                    isArray: mapping.isArrayMapping || false
                })) || [],
                outputMappings: jsonlMappings?.outputDataMappings.map(mapping => ({
                    keyPath: mapping.outputDataKeyPath.map(keyPath => keyPath.key),
                    type: mapping.outputDataType,
                    isArray: mapping.isArrayMapping || false
                })) || []
            };
            
            await dataSetService.addDataFile(datasetId, currentFile, mapping);
            setIsPanelOpen(false);
            setCurrentFile(null);
            setJsonlContent([]);
            setJsonlMappings(null);
            setCurrentFileName('');
            setCurrentInputForm({
                fileName: '',
                inputDataKeyPath: [],
                inputDataType: 'text',
                availableKeys: [],
                outputDataKeyPath: [],
                outputDataType: 'text'
            });
            setCurrentOutputForm({
                fileName: '',
                inputDataKeyPath: [],
                inputDataType: 'text',
                availableKeys: [],
                outputDataKeyPath: [],
                outputDataType: 'text'
            });
            
            if (fileInputRef.current) {
                fileInputRef.current.value = '';
            }
            
            // Reload data
            loadData();
        } catch (err) {
            console.error("Error uploading file:", err);
            setError("Failed to upload file");
        } finally {
            setIsUploading(false);
        }
    };

    const handleDeleteDataFile = async () => {
        if (selectedDataFileIndex < 0 || !datasetId) return;
        
        try {
            await dataSetService.deleteDataFile(datasetId, selectedDataFileIndex);
            setConfirmDelete(false);
            
            // Reload data
            loadData();
        } catch (err) {
            console.error("Error deleting file:", err);
            setError("Failed to delete file");
        }
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

    const dataObjectColumns: IColumn[] = [
        { key: 'id', name: 'ID', fieldName: 'id', minWidth: 100, isResizable: true },
        { 
            key: 'inputCount', 
            name: 'Input Count', 
            minWidth: 100,
            onRender: (item: DataObject) => item.inputData.length
        },
        { 
            key: 'outputCount', 
            name: 'Output Count', 
            minWidth: 100,
            onRender: (item: DataObject) => item.outputData.length,
            isResizable: true,
        }
    ];

    const dataFileColumns: IColumn[] = [
        { key: 'fileName', name: 'File Name', fieldName: 'fileName', minWidth: 200,isResizable: true},
        { 
            key: 'processingStatus', 
            name: 'Status', 
            fieldName: 'processingStatus',
            minWidth: 100,
            isResizable: true,
            onRender: (item: DataFileDto) => {
                switch (item.processingStatus) {
                    case DataFileProcessingStatus.Processing:
                        return (
                            <Stack horizontal verticalAlign="center">
                                <Text>Processing</Text>
                                <ProgressIndicator percentComplete={item.totalObjectCount > 0 ? item.processedObjectCount / item.totalObjectCount : undefined} />
                            </Stack>
                        );
                    case DataFileProcessingStatus.Completed:
                        return <Text>Completed</Text>;
                    case DataFileProcessingStatus.Failed:
                        return <Text style={{ color: 'red' }}>Failed</Text>;
                    default:
                        return <Text>Unprocessed</Text>;
                }
            }
        },
        {
            key: 'source',
            name: 'Source',
            fieldName: 'source',
            minWidth: 100,
            isResizable: true,
            onRender: (item: DataFileDto) => {
                
            }
        },
        {
            key: 'mapping',
            name: 'Input / Output',
            fieldName: 'mapping',
            minWidth: 100,
            isResizable: true,
            onRender: (item: DataFileDto) => item.mapping.inputMappings.length + " / " + item.mapping.outputMappings.length
        },
        { 
            key: 'objectCount', 
            name: 'Objects', 
            minWidth: 100,
            isResizable: true,
            onRender: (item: DataFileDto) => `${item.processedObjectCount}${item.totalObjectCount > 0 ? ` / ${item.totalObjectCount}` : ''}`
        },
        {
            key: 'uploadedAt',
            name: 'Uploaded',
            fieldName: 'uploadedAt',
            minWidth: 150,
            isResizable: true,
            onRender: (item: DataFileDto) => new Date(item.uploadedAt).toLocaleString()
        }
    ];

    const breadcrumbItems: IBreadcrumbItem[] = [
        { key: 'datasets', text: 'Datasets', onClick: () => navigate('/admin/data') },
        { key: 'details', text: dataSet?.name || 'Dataset Details', isCurrentItem: true }
    ];

    const getDataObjectCommandItems = (): ICommandBarItemProps[] => {
        const isItemSelected = selectedDataObjects.length === 1;

        return [
            {
                key: 'explore',
                text: 'Explore',
                iconProps: { iconName: 'Search' },
                disabled: !isItemSelected,
                onClick: () => {
                    if (selectedDataObjects[0] && datasetId) {
                        navigate(`/admin/data/${datasetId}/object/${selectedDataObjects[0].id}`);
                    }
                }
            }
        ];
    };

    const getDataFileCommandItems = (): ICommandBarItemProps[] => {
        return [
            {
                key: 'add',
                text: 'Add Data File',
                iconProps: { iconName: 'Add' },
                onClick: () => {
                    setIsPanelOpen(true);
                    setCurrentFile(null);
                    setJsonlContent([]);
                    setJsonlMappings(null);
                    setCurrentFileName('');
                    setError('');
                }
            },
            {
                key: 'delete',
                text: 'Delete',
                iconProps: { iconName: 'Delete' },
                disabled: !selectedDataFile,
                onClick: () => {
                    setConfirmDelete(true);
                }
            }
        ];
    };

    return (
        <Stack tokens={{ childrenGap: 20 }} style={{ padding: 20 }}>
            <Breadcrumb items={breadcrumbItems} />
            
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">{dataSet?.name || 'Loading...'}</Text>
            </Stack>
            
            {error && (
                <MessageBar
                    messageBarType={MessageBarType.error}
                    onDismiss={() => setError('')}
                    dismissButtonAriaLabel="Close"
                >
                    {error}
                </MessageBar>
            )}
            
            <Pivot>
                <PivotItem headerText="Data Files">
                    <Stack tokens={{ childrenGap: 10 }}>
                        <CommandBar items={getDataFileCommandItems()} />
                        
                        <DetailsList
                            items={dataFiles}
                            columns={dataFileColumns}
                            selectionMode={SelectionMode.single}
                            selection={dataFileSelection}
                            setKey="datafiles"
                        />
                        
                        
                    </Stack>
                </PivotItem>
                
                <PivotItem headerText="Data Objects">
                    <Stack tokens={{ childrenGap: 10 }}>
                        <CommandBar items={getDataObjectCommandItems()} />

            <DetailsList
                items={dataObjects}
                            columns={dataObjectColumns}
                selectionMode={SelectionMode.single}
                            selection={dataObjectSelection}
                            setKey="dataobjects"
                            onItemInvoked={(item) => datasetId && navigate(`/admin/data/${datasetId}/object/${item.id}`)}
                        />
                        
                        
                    </Stack>
                </PivotItem>
            </Pivot>
            
            {/* Add Data File Panel - Replace with the panel from DataManagement.tsx */}
            <Panel
                isOpen={isPanelOpen}
                onDismiss={() => setIsPanelOpen(false)}
                headerText="Add Data File"
                closeButtonAriaLabel="Close"
                isLightDismiss={!isUploading}
                isBlocking={isUploading}
                onRenderFooterContent={() => (
                    <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                        <PrimaryButton 
                            onClick={handleUploadDataFile}
                            disabled={(isUploading || !currentFile || (jsonlMappings && jsonlMappings.inputDataMappings.length === 0))??false}
                        >
                            {isUploading ? 'Uploading...' : 'Upload File'}
                        </PrimaryButton>
                        <DefaultButton 
                            onClick={() => {
                                setIsPanelOpen(false);
                                setCurrentFile(null);
                                setJsonlContent([]);
                                setJsonlMappings(null);
                            }}
                            disabled={isUploading}
                        >
                            Cancel
                        </DefaultButton>
                    </Stack>
                )}
            >
                <Stack tokens={{ childrenGap: 15 }}>
                    <Stack.Item>
                        <Text>Data Objects from JSONL</Text>
                        <input
                            type="file"
                            accept=".jsonl"
                            onChange={handleFileSelect}
                            ref={fileInputRef}
                            style={{ display: 'none' }}
                        />
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
                    
                    {currentFileName && (
                        <Stack tokens={{ childrenGap: 10 }} styles={{ root: { border: '1px solid #ccc', padding: 5 } }}>
                            <Text>Add Key Paths for Input Data Objects</Text>
                            
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
                    )}

                    {currentFileName && (
                        <Stack tokens={{ childrenGap: 10 }} styles={{ root: { border: '1px solid #ccc', padding: 5 } }}>
                            <Text>Add Key Paths for Pregenerated Output Data Objects</Text>

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
                    )}
                </Stack>
            </Panel>
            
            {/* Confirm Dialog */}
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
            
            {/* Confirm Delete Dialog */}
            <Dialog
                hidden={!confirmDelete}
                onDismiss={() => setConfirmDelete(false)}
                dialogContentProps={{
                    type: DialogType.normal,
                    title: 'Delete Data File',
                    subText: `Are you sure you want to delete "${selectedDataFile?.fileName}"? This will remove all data objects created from this file.`
                }}
            >
                <DialogFooter>
                    <PrimaryButton text="Delete" onClick={handleDeleteDataFile} />
                    <DefaultButton text="Cancel" onClick={() => setConfirmDelete(false)} />
                </DialogFooter>
            </Dialog>
        </Stack>
    );
};

export default DatasetDetails; 