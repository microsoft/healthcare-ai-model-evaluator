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
    IDropdownOption,
    IconButton,
} from '@fluentui/react';
import { IModel, AIModelType, RequiredIntegrationParameters } from '../../types/admin';
import type { AIModelTypeValues } from '../../types/admin';
import { ModelIntegrationType } from 'types/dataset';

const modelTypeOptions: IDropdownOption[] = [
    { key: AIModelType.IMAGE_TO_TEXT, text: AIModelType.IMAGE_TO_TEXT },
    { key: AIModelType.TEXT_TO_TEXT, text: AIModelType.TEXT_TO_TEXT },
    { key: AIModelType.IMAGE_TO_IMAGE, text: AIModelType.IMAGE_TO_IMAGE },
    { key: AIModelType.MULTIMODAL, text: AIModelType.MULTIMODAL }
];

const integrationTypeOptions: IDropdownOption[] = [
    { key: '', text: 'None' },
    { key: 'cxrreportgen', text: 'CXR Report Gen' },
    { key: 'openai', text: 'OpenAI' },
    { key: 'openai-reasoning', text: 'OpenAI Reasoning Model' },
    { key: 'azure-serverless', text: 'Azure Serverless Endpoint' },
    { key: 'functionapp', text: 'Azure Function App' }
];

interface ModelPanelProps {
    isOpen: boolean;
    model?: IModel;
    onDismiss: () => void;
    onSave: (model: Omit<IModel, 'id'> | IModel) => void;
}

export const ModelPanel: React.FC<ModelPanelProps> = ({
    isOpen,
    model,
    onDismiss,
    onSave,
}) => {
    const [currentModel, setCurrentModel] = useState<Partial<IModel>>({});
    const [integrationSettings, setIntegrationSettings] = useState<Record<string, string>>({});
    const [currentKeyName, setCurrentKeyName] = useState<string>('');
    const [currentKeyValue, setCurrentKeyValue] = useState<string>('');
    const [formErrors, setFormErrors] = useState<{ [key: string]: string }>({});
    const [showPassword, setShowPassword] = useState<string | false>('');
    const [costPerTokenString, setCostPerTokenString] = useState(model?.costPerToken?.toString() || '');
    const [costPerTokenOutString, setCostPerTokenOutString] = useState(model?.costPerTokenOut?.toString() || '');

    useEffect(() => {
        if ( typeof model !== "undefined") {
            setShowPassword('');
            setCurrentKeyName('');
            setCurrentKeyValue('');
            setCostPerTokenString(model.costPerToken?.toString() || '');
            setCostPerTokenOutString(model.costPerTokenOut?.toString() || '');
            setCurrentModel(model);
            setIntegrationSettings(model.integrationSettings || {});
        } else {
            setShowPassword('');
            setCurrentKeyName('');
            setCurrentKeyValue('');
            setCostPerTokenString('');
            setCostPerTokenOutString('');
            setCurrentModel({});
            setIntegrationSettings({});
            setFormErrors({});
        }
    }, [model]);


    const validateForm = (currentModel: IModel): boolean => {
        const errors: { [key: string]: string } = {};
        if (!currentModel.name?.trim()) {
            errors.name = 'Name is required';
        }
        if (!currentModel.modelType) {
            errors.modelType = 'Model Type is required';
        }
        if (!currentModel.origin?.trim()) {
            errors.origin = 'Origin is required';
        }
        if( parseFloat(costPerTokenString) < 0) {
            errors.costPerToken = 'Cost per token must be greater than 0';
        }
        if (parseFloat(costPerTokenOutString) < 0) {
            errors.costPerTokenOut = 'Cost per token out must be greater than 0';
        }
        if (currentModel.integrationType) {
            const requiredParams = RequiredIntegrationParameters[currentModel.integrationType];
            if (!requiredParams) {
                errors.integrationType = `Unknown integration type: ${currentModel.integrationType}`;

                setFormErrors(errors);
                return false;
            }
            const missingParams = requiredParams.filter(
                param => !integrationSettings?.[param]
            );
            if (missingParams.length > 0) {
                console.log(missingParams);
                errors.integrationSettings = 
                    `Missing required parameters for ${currentModel.integrationType}: ${missingParams.join(', ')}. ` +
                    `Required parameters are: ${requiredParams.join(', ')}`;
                setFormErrors(errors);
                return false;
            }
        }
        setFormErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleSave = () => {
        const modelToSave = { ...currentModel, integrationSettings };
        setCurrentModel(modelToSave as IModel);
        console.log("saving model", modelToSave);
        if (validateForm(modelToSave as IModel)) {
            console.log(modelToSave);
            onSave(modelToSave as IModel);
        }
    };

    return (
        <Panel
            isOpen={isOpen}
            onDismiss={onDismiss}
            headerText={model ? 'Edit Model' : 'New Model'}
            closeButtonAriaLabel="Close"
        >
            <Stack tokens={{ childrenGap: 15 }}>
                <TextField
                    label="Name"
                    required
                    value={currentModel.name || ''}
                    onChange={(_, value) => setCurrentModel({ ...currentModel, name: value })}
                    errorMessage={formErrors.name}
                />
                <Dropdown
                    label="Model Type"
                    required
                    options={modelTypeOptions}
                    selectedKey={currentModel.modelType || ''}
                    onChange={(_, option) => setCurrentModel({ 
                        ...currentModel, 
                        modelType: option?.key as AIModelTypeValues 
                    })}
                    errorMessage={formErrors.modelType}
                />
                <TextField
                    label="Origin"
                    required
                    value={currentModel.origin || ''}
                    onChange={(_, value) => setCurrentModel({ ...currentModel, origin: value })}
                    errorMessage={formErrors.origin}
                />
                <TextField
                    label="Cost Per Token In"
                    value={costPerTokenString}
                    onChange={(_, value) => {
                        // Allow empty or decimal point input during typing
                        setCostPerTokenString(value || '');
                        if( parseFloat(value || '0') >= 0) {
                            setCurrentModel({ ...currentModel, costPerToken: parseFloat(value || '0') });
                        }
                    }}
                />
                <TextField
                    label="Cost Per Token Out"
                    value={costPerTokenOutString}
                    onChange={(_, value) => {
                        // Allow empty or decimal point input during typing
                        setCostPerTokenOutString(value || '');
                        if( parseFloat(value || '0') >= 0) {
                            setCurrentModel({ ...currentModel, costPerTokenOut: parseFloat(value || '0') });
                        }
                    }}
                />
                <TextField
                    label="Description"
                    multiline
                    rows={3}
                    value={currentModel.description || ''}
                    onChange={(_, value) => setCurrentModel({ ...currentModel, description: value })}
                />
                <Stack>
                    <Label>Parameters</Label>
                    {formErrors.integrationSettings && <Label style={{ color: 'red' }}>{formErrors.integrationSettings}</Label>}
                    <Stack tokens={{ childrenGap: 10 }}>
                        {Object.entries(integrationSettings || {}).map(([key, value]) => (
                            <Stack horizontal key={key} styles={{ root: { borderBottom: '1px solid #e0e0e0' } }}>
                                <Stack.Item grow>
                                    <Stack verticalAlign="center" tokens={{ childrenGap: 5 }}>
                                        <Stack.Item grow><span>{key}:&nbsp;</span></Stack.Item>
                                        <Stack.Item grow><span>{showPassword===key ? value: '•'.repeat(value.length>30?30:value.length)}</span>
                                        <IconButton
                                            iconProps={{ iconName: showPassword ? 'Hide' : 'RedEye' }}
                                            onClick={() => setShowPassword(showPassword===key?false:key)}
                                            styles={{ root: { marginLeft: 5 } }}
                                        />
                                        </Stack.Item>
                                    </Stack>
                                </Stack.Item>
                                <IconButton
                                    iconProps={{ iconName: 'Delete' }}
                                    onClick={() => setIntegrationSettings(prev => {
                                        const newSettings = { ...prev };
                                        delete newSettings[key];
                                        return newSettings;
                                        })} />
                            </Stack>
                        ))}
                    </Stack>
                    <Stack horizontal tokens={{ childrenGap: 10 }}>
                        <TextField
                            label="Name"
                            value={currentKeyName || ''}
                            onChange={(_, value) => setCurrentKeyName(value || '')}
                        />
                        <TextField
                            label="Value"
                            value={currentKeyValue || ''}
                            onChange={(_, value) => setCurrentKeyValue(value || '')}
                        />
                        <IconButton
                            styles={{ root: { marginTop: 28 } }}
                            iconProps={{ iconName: 'Add' }}
                            disabled={!currentKeyName || !currentKeyValue}
                            onClick={() => {
                                setIntegrationSettings(prev => ({ ...prev, [currentKeyName]: currentKeyValue }));
                                setCurrentKeyName('');
                                setCurrentKeyValue('');
                            }} />
                    </Stack>
                </Stack>

                <Dropdown
                    label="Integration Type"
                    options={integrationTypeOptions}
                    selectedKey={currentModel.integrationType || ''}
                    onChange={(_, option) => setCurrentModel({ 
                        ...currentModel, 
                        integrationType: option?.key as ModelIntegrationType
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
                        selectedItems={(currentModel.tags || []).map(tag => ({ key: tag, name: tag }))}
                        onChange={items => setCurrentModel({
                            ...currentModel,
                            tags: items?.map(item => item.name) || []
                        })}
                        onInputChange={(input) => {
                            if (input && input.endsWith(',')) {
                                const newTag = input.slice(0, -1).trim();
                                if (newTag) {
                                    const currentTags = currentModel.tags || [];
                                    if (!currentTags.includes(newTag)) {
                                        setCurrentModel(prev => ({
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