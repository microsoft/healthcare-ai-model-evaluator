import React, { useEffect, useState } from 'react';
import {
    Panel,
    PrimaryButton,
    DefaultButton,
    Stack,
    TextField,
    Dropdown,
    IDropdownOption,
    TagPicker,
    Label,
    MessageBar,
    MessageBarType,
    ITag,
    IBasePickerSuggestionsProps,
    PanelType,
    Checkbox,
    Text
} from '@fluentui/react';
import { IExperiment, IUser, ITestScenario, ExperimentStatus, IModel } from '../../types/admin';
import { testScenarioService } from '../../services/testScenarioService';
import { userService } from '../../services/userService';
import { modelService } from 'services/modelService';
import { Form } from 'react-router-dom';


interface ExperimentPanelProps {
    isOpen: boolean;
    experiment?: IExperiment;
    onDismiss: () => void;
    onSave: (experiment: IExperiment | Omit<IExperiment, 'id'>) => Promise<void>;
}

const tagPickerSuggestionsProps: IBasePickerSuggestionsProps = {
    suggestionsHeaderText: 'Suggested tags',
    noResultsFoundText: 'No tags found'
};

export const ExperimentPanel: React.FC<ExperimentPanelProps> = ({
    isOpen,
    experiment,
    onDismiss,
    onSave
}) => {
    const [currentExperiment, setCurrentExperiment] = useState<Partial<IExperiment>>({
        name: '',
        description: '',
        status: ExperimentStatus.Draft,
        experimentType: '',
        testScenarioId: '',
        reviewerIds: [],
        tags: [],
        modelIds: [],
        randomized: false,
    });
    const [formErrors, setFormErrors] = useState<{ [key: string]: string }>({});
    const [scenarioOptions, setScenarioOptions] = useState<IDropdownOption[]>([]);
    const [scenarios, setScenarios] = useState<ITestScenario[]>([]);
    const [models, setModels] = useState<IModel[]>([]);
    const [userOptions, setUserOptions] = useState<IDropdownOption[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [tags, setTags] = useState<string[]>([]);
    const [overrideInstructions, setOverrideInstructions] = useState<boolean>(false);
    const [enableRandomization, setEnableRandomization] = useState<boolean>(false);
    const [testScenarioInstructions, setTestScenarioInstructions] = useState<string>('');

    useEffect(() => {
        if (isOpen) {
            const loadOptions = async () => {
                setIsLoading(true);
                try {
                    const [scenarios, users, models] = await Promise.all([
                        testScenarioService.getScenarios(),
                        userService.getUsers(),
                        modelService.getModels()
                    ]);
                    setScenarios(scenarios);
                    setModels(models);
                    
                    setScenarioOptions(scenarios.map((scenario: ITestScenario) => ({
                        key: scenario.id,
                        text: scenario.name
                    })));
                    
                    setUserOptions(users.map((user: IUser) => ({
                        key: user.id,
                        text: user.name
                    })));
                } catch (error) {
                    console.error('Failed to load options:', error);
                } finally {
                    setIsLoading(false);
                }
            };
            loadOptions();
        }
    }, [isOpen]);

    useEffect(() => {
        if (!isOpen) return;

        if (experiment) {
            setCurrentExperiment({
                name: experiment.name,
                id: experiment.id,
                description: experiment.description,
                status: experiment.status,
                experimentType: experiment.experimentType,
                testScenarioId: experiment.testScenarioId,
                reviewerIds: experiment.reviewerIds || [],
                tags: experiment.tags || [],
                modelIds: experiment.modelIds || [],
                randomized: experiment.randomized,
            });
            setTags(experiment.tags || []);
            setEnableRandomization(experiment.randomized || false);
        } else {
            setCurrentExperiment({
                name: '',
                description: '',
                status: ExperimentStatus.Draft,
                experimentType: '',
                testScenarioId: '',
                reviewerIds: [],
                tags: [],
                modelIds: [],
                randomized: true,
            });
            setEnableRandomization(true);
            setTags([]);
        }
        setFormErrors({});
    }, [experiment, isOpen]);

    useEffect(() => {
        if (currentExperiment.testScenarioId) {
            const selectedScenario = scenarios.find(s => s.id === currentExperiment.testScenarioId);
            if (selectedScenario) {
                setTestScenarioInstructions(selectedScenario.reviewerInstructions || '');
                
                if (experiment?.reviewerInstructions) {
                    setOverrideInstructions(true);
                }
            }
        }
    }, [currentExperiment.testScenarioId, scenarios, experiment]);

    const [availableModels, setAvailableModels] = useState<IModel[]>([]);   

    const validateForm = (): boolean => {
        const errors: { [key: string]: string } = {};
        if (!currentExperiment.name?.trim()) {
            errors.name = 'Name is required';
        }
        if (!currentExperiment.testScenarioId) {
            errors.testScenarioId = 'Experiment is required';
        }
        if (!currentExperiment.experimentType) {
            var testScenario = scenarios.find(s => s.id === currentExperiment.testScenarioId);
            if (testScenario) {
                currentExperiment.experimentType = testScenario.experimentType || '';
            } else {
                currentExperiment.experimentType = 'Simple Evaluation';
            }
            //errors.experimentType = 'Experiment Type is required';
        }

        // Add validation for AB experiment type
        const modelIds = currentExperiment.modelIds || [];
        if (currentExperiment.experimentType === 'AB' && modelIds.length < 2) {
            errors.modelIds = 'AB experiments require at least 2 models';
        }
        if( currentExperiment.status === 'InProgress') {
            errors.name = 'Cannot edit an in progress experiment';
        }
        console.log(errors)
        setFormErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleSave = async () => {
        if (!validateForm()) return;

        const completeExperiment: IExperiment = {
            ...currentExperiment as IExperiment,
            tags,
            updatedAt: new Date().toISOString(),
            randomized: enableRandomization
        };

        await onSave(completeExperiment);
        handleDismiss();
    };
    const handleDismiss = () => {
        setCurrentExperiment({});
        setTags([]);
        onDismiss();
    };

    const experimentTypeOptions: IDropdownOption[] = [
        { key: 'Simple Evaluation', text: 'Simple Evaluation' },
        { key: 'Simple Validation', text: 'Simple Validation' },
        { key: 'Arena', text: 'A/B Testing' },
        { key: 'Full Validation', text: 'Full Validation' }
    ];

    const filterSuggestedTags = (
        filterText: string,
        selectedItems?: ITag[]
    ): ITag[] => {
        if (!filterText) return [];
        
        // Create a new tag from the input
        const newTag = filterText.trim();
        
        // Don't suggest if it's already selected
        if (selectedItems?.some(item => item.name === newTag)) return [];
        
        // Return the new tag as a suggestion
        return [{ key: newTag, name: newTag }];
    };

    return (
        <Panel
            isOpen={isOpen}
            onDismiss={handleDismiss}
            type={PanelType.largeFixed}
            headerText={experiment ? 'Edit Experiment Assignment' : 'New Experiment Assignment'}
            closeButtonAriaLabel="Close"
        >
            <Stack tokens={{ childrenGap: 15 }}>
                <TextField
                    label="Name"
                    required
                    value={currentExperiment.name || ''}
                    onChange={(_, value) => setCurrentExperiment({ ...currentExperiment, name: value })}
                    errorMessage={formErrors.name}
                />
                <TextField
                    label="Description"
                    multiline
                    rows={3}
                    value={currentExperiment.description || ''}
                    onChange={(_, value) => setCurrentExperiment({ ...currentExperiment, description: value })}
                />
                {/* Only show status dropdown when editing an existing experiment */}
                {experiment && (
                    <Dropdown
                        label="Status"
                        required
                        options={Object.values(ExperimentStatus).map(status => ({
                            key: status,
                            text: status
                        }))}
                        selectedKey={currentExperiment.status}
                        onChange={(_, option) => setCurrentExperiment({ 
                            ...currentExperiment, 
                            status: option?.key as ExperimentStatus 
                        })}
                    />
                )}
                <Dropdown
                    label="Experiment"
                    required
                    options={scenarioOptions}
                    selectedKey={currentExperiment.testScenarioId}
                    onChange={(_, option) => {
                        setCurrentExperiment({ 
                            ...currentExperiment, 
                            testScenarioId: option?.key as string 
                        });
                        var scenario = scenarios.find(scenario => scenario.id === option?.key as string);
                        if (scenario?.modelIds) {  // Using optional chaining
                            const filteredModels = models.filter(model => scenario?.modelIds.includes(model.id));
                            setAvailableModels(filteredModels);
                        }
                    }}
                    errorMessage={formErrors.testScenarioId}
                    disabled={isLoading}
                />
                {false &&currentExperiment.testScenarioId && currentExperiment.experimentType === 'Arena' && (
                    <Stack>
                        <Label>Models To Test</Label>
                        {availableModels.map(model => (
                            <Checkbox
                                key={model.id}
                                label={`${model.name} (${model.modelType})`}
                                checked={currentExperiment.modelIds?.includes(model.id)}
                                onChange={(_, checked) => {
                                    setCurrentExperiment(prev => ({
                                        ...prev,
                                        modelIds: checked
                                            ? [...(prev.modelIds || []), model.id]
                                            : (prev.modelIds || []).filter(id => id !== model.id)
                                    }));
                                }}
                            />
                        ))}
                    </Stack>
                )}
                <Dropdown
                    label="Reviewers"
                    multiSelect
                    options={userOptions}
                    selectedKeys={currentExperiment.reviewerIds || []}
                    onChange={(_, item) => {
                        if (item) {
                            setCurrentExperiment({
                                ...currentExperiment,
                                reviewerIds: item.selected 
                                    ? [...(currentExperiment.reviewerIds || []), item.key as string]
                                    : (currentExperiment.reviewerIds || []).filter((id: string) => id !== item.key)
                            });
                        }
                    }}
                    disabled={isLoading}
                />
                {formErrors.modelIds && (
                    <MessageBar messageBarType={MessageBarType.error}>
                        {formErrors.modelIds}
                    </MessageBar>
                )}
                <Stack.Item>
                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="mediumPlus">Reviewer Instructions</Text>
                        <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                            <Text>From test scenario:</Text>
                            <Checkbox
                                label="Override"
                                checked={overrideInstructions}
                                onChange={(_, checked) => setOverrideInstructions(checked || false)}
                            />
                        </Stack>
                        
                        {!overrideInstructions ? (
                            <Text>{testScenarioInstructions || 'No instructions in test scenario'}</Text>
                        ) : (
                            <TextField
                                multiline
                                rows={4}
                                value={currentExperiment.reviewerInstructions || testScenarioInstructions}
                                onChange={(_, value) => 
                                    setCurrentExperiment(prev => ({ ...prev, reviewerInstructions: value }))}
                            />
                        )}
                    </Stack>
                </Stack.Item>
                <Stack.Item>
                    <Checkbox
                        label="Enable Randomization"
                        checked={enableRandomization}
                        onChange={(_, checked) => setEnableRandomization(checked || false)}
                    />
                </Stack.Item>
                <Stack>
                    <Label>Tags</Label>
                    <TagPicker
                        onResolveSuggestions={filterSuggestedTags}
                        selectedItems={tags.map(tag => ({ key: tag, name: tag }))}
                        onChange={items => setTags(items ? items.map(item => item.name) : [])}
                        pickerSuggestionsProps={tagPickerSuggestionsProps}
                        itemLimit={10}
                        inputProps={{
                            placeholder: 'Enter tags...'
                        }}
                    />
                </Stack>
                <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                    <DefaultButton onClick={handleDismiss} text="Cancel" />
                    <PrimaryButton onClick={handleSave} text="Save" disabled={isLoading} />
                </Stack>
            </Stack>
        </Panel>
    );
}; 