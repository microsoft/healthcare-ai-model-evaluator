import React, { useEffect, useState } from 'react';
import {
    Panel,
    PrimaryButton,
    DefaultButton,
    Stack,
    TextField,
    TagPicker,
    Label,
    Dropdown,
    Text,
    IconButton,
    MessageBar,
    MessageBarType,
    Checkbox
} from '@fluentui/react';
import { IClinicalTask, OutputSelectionType, type OutputSelectionTypeValue, EvalMetricType } from '../../types/admin';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { fetchDataSets } from '../../reducers/dataReducer';
import { fetchModels } from '../../reducers/modelReducer';

interface ClinicalTaskPanelProps {
    isOpen: boolean;
    task?: IClinicalTask;
    onDismiss: () => void;
    onSave: (task: Omit<IClinicalTask, 'id'> | IClinicalTask) => void;
}

interface DataSetModelPair {
    dataSetId: string;
    modelId: string;
    modelOutputIndex: number;
    dataSetName: string;
    modelName: string;
    generatedOutputKey: string;
    isGroundTruth?: boolean;
}

export const ClinicalTaskPanel: React.FC<ClinicalTaskPanelProps> = ({
    isOpen,
    task,
    onDismiss,
    onSave,
}) => {
    const dispatch = useAppDispatch();
    const { datasets } = useAppSelector((state) => state.data);
    const { models } = useAppSelector((state) => state.models);
    const [currentTask, setCurrentTask] = useState<Partial<IClinicalTask>>({});
    const [formErrors, setFormErrors] = useState<{ [key: string]: string }>({});
    const [generatedOutputKey, setGeneratedOutputKey] = useState<string>('');
    const [selectedDataSetId, setSelectedDataSetId] = useState<string>('');
    const [selectedModelId, setSelectedModelId] = useState<string>('');
    const [selectedModelOutputIndex, setSelectedModelOutputIndex] = useState<number>(0);
    const [dataSetModels, setDataSetModels] = useState<DataSetModelPair[]>([]);
    const [outputSelectionType, setOutputSelectionType] = useState<OutputSelectionTypeValue>(OutputSelectionType.GENERATE_NEW);
    const [isGroundTruth, setIsGroundTruth] = useState<boolean>(false);
    const [groundTruthExists, setGroundTruthExists] = useState<boolean>(false);

    // Load data only when panel opens
    useEffect(() => {
        if (isOpen) {
            dispatch(fetchDataSets());
            dispatch(fetchModels());
        }
    }, [isOpen, dispatch]);

    // Handle form initialization separately
    useEffect(() => {
        if (!isOpen) return;

        if (task) {
            setCurrentTask(task);
            const pairs = task.dataSetModels.map(dsm => ({
                dataSetId: dsm.dataSetId,
                modelId: dsm.modelId,
                modelOutputIndex: dsm.modelOutputIndex,
                dataSetName: datasets.find(ds => ds.id === dsm.dataSetId)?.name || 'Unknown Dataset',
                modelName: models.find(m => m.id === dsm.modelId)?.name || 'Unknown Model',
                generatedOutputKey: dsm.generatedOutputKey,
                isGroundTruth: dsm.isGroundTruth
            }));
            setDataSetModels(pairs);
        } else {
            setCurrentTask({
                dataSetModels: [],
                tags: []
            });
            setDataSetModels([]);
        }
        setGeneratedOutputKey('');
        setSelectedDataSetId('');
        setSelectedModelId('');
        setSelectedModelOutputIndex(0);
        setOutputSelectionType(OutputSelectionType.GENERATE_NEW);
        setFormErrors({});
    }, [isOpen, task, datasets, models]); 

    // Check for existing ground truth when dataset changes
    useEffect(() => {
        if (selectedDataSetId) {
            const hasGroundTruth = dataSetModels.some(
                pair => pair.dataSetId === selectedDataSetId && pair.isGroundTruth
            );
            setGroundTruthExists(hasGroundTruth);
            
            if (hasGroundTruth && isGroundTruth) {
                setIsGroundTruth(false);
            }
        } else {
            setGroundTruthExists(false);
        }
    }, [selectedDataSetId, dataSetModels, isGroundTruth]);

    const validateForm = (): boolean => {
        const errors: { [key: string]: string } = {};
        if (!currentTask.name?.trim()) {
            errors.name = 'Name is required';
        }
        if (!dataSetModels.length) {
            errors.dataSetModels = 'At least one dataset-model pair is required';
        }
        setFormErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleAddPair = () => {
        if (selectedDataSetId) {
            const dataset = datasets.find(ds => ds.id === selectedDataSetId);
            
            // For ground truth, we don't need a model
            if (isGroundTruth) {
                const newPair: DataSetModelPair = {
                    dataSetId: selectedDataSetId,
                    modelId: '', // Empty for ground truth
                    modelOutputIndex: selectedModelOutputIndex,
                    dataSetName: dataset?.name || 'Unknown Dataset',
                    modelName: 'Ground Truth',
                    generatedOutputKey: '',
                    isGroundTruth: true
                };
                
                setDataSetModels([...dataSetModels, newPair]);
                setSelectedDataSetId('');
                setSelectedModelOutputIndex(-1);
                setIsGroundTruth(false);
                return;
            }
            
            // Normal model-dataset pair
            if (!selectedModelId) {
                setFormErrors({ ...formErrors, modelSelection: 'Please select a model' });
                return;
            }
            
            const model = models.find(m => m.id === selectedModelId);
            const isGenerating = outputSelectionType === OutputSelectionType.GENERATE_NEW;
            
            const newPair: DataSetModelPair = {
                dataSetId: selectedDataSetId,
                modelId: selectedModelId,
                modelOutputIndex: isGenerating ? -1 : selectedModelOutputIndex,
                dataSetName: dataset?.name || 'Unknown Dataset',
                modelName: model?.name || 'Unknown Model',
                generatedOutputKey: isGenerating ? (model?.name || '') : generatedOutputKey,
                isGroundTruth: false
            };
            setDataSetModels([...dataSetModels, newPair]);
            setSelectedDataSetId('');
            setSelectedModelId('');
            setSelectedModelOutputIndex(-1);
            setGeneratedOutputKey('');
            setOutputSelectionType(OutputSelectionType.GENERATE_NEW);
        }
    };

    const handleRemovePair = (dataSetId: string, modelId: string, modelOutputIndex: number, generatedOutputKey: string) => {
        setDataSetModels(dataSetModels.filter(
            pair => !(pair.dataSetId === dataSetId && 
                    pair.modelId === modelId && 
                    pair.modelOutputIndex === modelOutputIndex && 
                    pair.generatedOutputKey === generatedOutputKey)
        ));
    };

    const handleSave = () => {
        if (validateForm()) {
            const taskToSave = {
                ...currentTask,
                dataSetModels: dataSetModels.map(pair => ({
                    dataSetId: pair.dataSetId,
                    modelId: pair.modelId,
                    modelOutputIndex: pair.modelOutputIndex,
                    generatedOutputKey: pair.generatedOutputKey,
                    isGroundTruth: pair.isGroundTruth
                }))
            };
            onSave(taskToSave as IClinicalTask);
        }
    };


    return (
        <Panel
            isOpen={isOpen}
            onDismiss={onDismiss}
            headerText={task ? 'Edit Clinical Task' : 'New Clinical Task'}
            closeButtonAriaLabel="Close"
        >
            <Stack tokens={{ childrenGap: 15 }}>
                <TextField
                    label="Name"
                    required
                    value={currentTask.name || ''}
                    onChange={(_, value) => setCurrentTask({ ...currentTask, name: value })}
                    errorMessage={formErrors.name}
                />

                <Stack tokens={{ childrenGap: 10 }}>
                    <Text variant="mediumPlus">Dataset Model Pairs</Text>
                    <Stack tokens={{ childrenGap: 10 }}>
                        <Dropdown
                            label="Dataset"
                            options={datasets.map(ds => ({ key: ds.id, text: ds.name }))}
                            selectedKey={selectedDataSetId}
                            onChange={(_, option) => {
                                const newDatasetId = option?.key as string ?? '';
                                setSelectedDataSetId(newDatasetId);
                                setSelectedModelId('');
                                setSelectedModelOutputIndex(0);
                                
                                // Check if ground truth exists for this dataset
                                const hasGroundTruth = dataSetModels.some(
                                    pair => pair.dataSetId === newDatasetId && pair.isGroundTruth
                                );
                                setGroundTruthExists(hasGroundTruth);
                                
                                if (hasGroundTruth) {
                                    setIsGroundTruth(false);
                                }
                            }}
                        />
                        
                        <Checkbox
                            label="Set ground truth"
                            checked={isGroundTruth}
                            onChange={(_, checked) => setIsGroundTruth(checked || false)}
                            disabled={groundTruthExists}
                        />
                        {groundTruthExists && (
                            <Text styles={{ root: { color: 'red', fontSize: 12 } }}>
                                Ground truth already set for this dataset
                            </Text>
                        )}
                        
                        {!isGroundTruth && selectedDataSetId && (
                            <>
                                <Dropdown
                                    label="Model"
                                    options={models.map(model => ({
                                        key: model.id,
                                        text: `${model.name} (${model.modelType})`
                                    }))}
                                    selectedKey={selectedModelId}
                                    onChange={(_, option) => {
                                        setSelectedModelId(option?.key as string);
                                        setGeneratedOutputKey(models.find(m => m.id === option?.key)?.name || '');
                                    }}
                                />
                                
                                <Dropdown
                                    label="Output Type"
                                    options={[
                                        { key: OutputSelectionType.GENERATE_NEW, text: 'Generate Data' },
                                        { key: OutputSelectionType.USE_EXISTING, text: 'Use Pregenerated Data' }
                                    ]}
                                    selectedKey={outputSelectionType}
                                    onChange={(_, option) => {
                                        if (option) {
                                            setOutputSelectionType(option.key as OutputSelectionTypeValue);
                                            if(option.key !== OutputSelectionType.GENERATE_NEW) {
                                                setGeneratedOutputKey('');
                                                setSelectedModelOutputIndex(0);
                                            } else {
                                                setGeneratedOutputKey(models.find(m => m.id === selectedModelId)?.name || '');
                                                setSelectedModelOutputIndex(-1);
                                            }
                                        }
                                    }}
                                />
                                
                                {(outputSelectionType || OutputSelectionType.GENERATE_NEW) === OutputSelectionType.USE_EXISTING && (
                                    <>
                                        <Dropdown
                                            label="Uploaded Output Index"
                                            options={Array.from(
                                                { length: datasets.find(ds => ds.id === selectedDataSetId)?.modelOutputCount || 1 },
                                                (_, i) => ({
                                                    key: i,
                                                    text: `Output ${i + 1} ${datasets.find(ds => ds.id === selectedDataSetId)?.files?.find(()=>true)?.mapping?.outputMappings?.[i]?.keyPath?.join('.') ?? ''}`
                                                })
                                            )}
                                            selectedKey={selectedModelOutputIndex}
                                            onChange={(_, option) => {
                                                setSelectedModelOutputIndex(option?.key as number);
                                            }}
                                        />
                                        <Dropdown
                                            label="Previously Generated Output Set"
                                            options={
                                                datasets.find(ds => ds.id === selectedDataSetId)?.generatedDataList?.length
                                                    ? datasets.find(ds => ds.id === selectedDataSetId)?.generatedDataList.map(key => ({
                                                        key,
                                                        text: key
                                                    })) || []
                                                    : [{ key: 'none', text: '(no output generated)', disabled: true }]
                                            }
                                            disabled={!datasets.find(ds => ds.id === selectedDataSetId)?.generatedDataList?.length}
                                            selectedKey={generatedOutputKey}
                                            onChange={(_, option) => {
                                                if (option) {
                                                    setGeneratedOutputKey(option.key as string);
                                                    setSelectedModelOutputIndex(-1); // Reset output index when changing generated set
                                                }
                                            }}
                                        />
                                    </>
                                )}
                            </>
                        )}
                        
                        {isGroundTruth && selectedDataSetId && (
                            <Dropdown
                                label="Uploaded Output Index"
                                options={Array.from(
                                    { length: datasets.find(ds => ds.id === selectedDataSetId)?.modelOutputCount || 1 },
                                    (_, i) => ({
                                        key: i,
                                        text: `Output ${i + 1} ${datasets.find(ds => ds.id === selectedDataSetId)?.files?.find(()=>true)?.mapping?.outputMappings?.[i]?.keyPath?.join('.') ?? ''}`
                                    })
                                )}
                                selectedKey={selectedModelOutputIndex}
                                onChange={(_, option) => {
                                    setSelectedModelOutputIndex(option?.key as number);
                                }}
                            />
                        )}
                        
                        <PrimaryButton
                            text="Add Dataset-Model Pair"
                            onClick={handleAddPair}
                            disabled={!selectedDataSetId || (!isGroundTruth && !selectedModelId)}
                        />
                    </Stack>

                    {dataSetModels.map((pair) => (
                        <Stack 
                            key={`${pair.dataSetId}-${pair.modelId || 'groundtruth'}-${pair.modelOutputIndex}`}
                            horizontal 
                            verticalAlign="center"
                            tokens={{ childrenGap: 10 }}
                        >
                            <Text>
                                {pair.isGroundTruth ? 
                                    `${pair.dataSetName} - Ground Truth (Output ${pair.modelOutputIndex + 1})` : 
                                    `${pair.dataSetName} with ${pair.modelName} ${!pair.generatedOutputKey ? 
                                        "Uploaded Output:" + (pair.modelOutputIndex + 1) : 
                                        (pair.generatedOutputKey.replace(pair.modelName,'') === "" ? 
                                            "pending generation" : 
                                            "generated on" + pair.generatedOutputKey.replace(pair.modelName,''))
                                    }`
                                }
                            </Text>
                            <IconButton
                                iconProps={{ iconName: 'Delete' }}
                                onClick={() => handleRemovePair(
                                    pair.dataSetId, 
                                    pair.modelId, 
                                    pair.modelOutputIndex, 
                                    pair.generatedOutputKey
                                )}
                            />
                        </Stack>
                    ))}

                    {formErrors.dataSetModels && (
                        <MessageBar messageBarType={MessageBarType.error}>
                            {formErrors.dataSetModels}
                        </MessageBar>
                    )}
                </Stack>

                <TextField
                    label="Prompt"
                    multiline
                    rows={3}
                    value={currentTask.prompt || ''}
                    onChange={(_, value) => setCurrentTask({ ...currentTask, prompt: value })}
                />
                
                <Dropdown
                    label="Evaluation Metric"
                    options={[
                        { key: 'Text-based metrics', text: 'Text-based metrics' },
                        { key: 'Image-based metrics', text: 'Image-based metrics' },
                        { key: 'Accuracy metrics', text: 'Accuracy metrics' },
                        { key: 'Safety metrics', text: 'Safety metrics' },
                        { key: 'Bias metrics', text: 'Bias metrics' },
                    ]}
                    selectedKey={currentTask.evalMetric || 'Text-based metrics'}
                    onChange={(_, option) => setCurrentTask({ 
                        ...currentTask, 
                        evalMetric: option?.key as EvalMetricType 
                    })}
                />
                
                <Stack>
                    <Label>Tags</Label>
                    <TagPicker
                        onResolveSuggestions={(filterText) => {
                            if (filterText) {
                                return [{ key: filterText, name: filterText }];
                            }
                            return [];
                        }}
                        selectedItems={(currentTask.tags || []).map(tag => ({ key: tag, name: tag }))}
                        onChange={items => setCurrentTask({
                            ...currentTask,
                            tags: items?.map(item => item.name) || []
                        })}
                        onInputChange={(input) => {
                            if (input && input.endsWith(',')) {
                                const newTag = input.slice(0, -1).trim();
                                if (newTag) {
                                    const currentTags = currentTask.tags || [];
                                    if (!currentTags.includes(newTag)) {
                                        setCurrentTask(prev => ({
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
                </Stack>
                
                <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                    <DefaultButton onClick={onDismiss} text="Cancel" />
                    <PrimaryButton onClick={handleSave} text="Save" />
                </Stack>

                
            </Stack>
        </Panel>
    );
}; 