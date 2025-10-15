import React, { useEffect, useState } from 'react';
import {
    Panel,
    PrimaryButton,
    DefaultButton,
    Stack,
    TextField,
    TagPicker,
    Dropdown,
    ComboBox,
    Label,
    Checkbox,
    StackItem,
    Text,
} from '@fluentui/react';
import { 
    ITestScenario, 
    IClinicalTask, 
    IModel,
    TaskDataSetModel,
    EvalQuestion,
    EvalQuestionOption
} from '../../types/admin';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { fetchTasks } from '../../reducers/clinicalTaskReducer';
import { fetchModels } from '../../reducers/modelReducer';
import { fetchScenarios } from '../../reducers/testScenarioReducer';
import exp from 'constants';

class ExperimentQuestionsPreset {
    questions: EvalQuestion[] = [];
    name: string = '';
}

const ExperimentQuestionPresets: ExperimentQuestionsPreset[] = [
    { name: 'Simple Evaluation',
        questions: [{
            id: '1',
            name: 'Model Evaluation',
            questionText: 'How would you rate the model?',
            options: [
                { id: '1', text: '1 - Poor', value: '1' },
                { id: '2', text: '2 - Fair', value: '2' },
                { id: '3', text: '3 - Good', value: '3' },
                { id: '4', text: '4 - Very Good', value: '4' },
                { id: '5', text: '5 - Excellent', value: '5' }
            ],
            evalMetric: 'Rating 1-5'
        }]
    },
    { name: 'Simple Validation',
        questions: [
            {
                id: '2',
                name: 'Model Validation',
                questionText: 'Is this model output acceptable?',
                options: [
                    { id: '1', text: 'Yes', value: 'yes' },
                    { id: '2', text: 'No', value: 'no' }
                ],
                evalMetric: 'Binary Validation'
            }
        ]
    }
];

interface TestScenarioPanelProps {
    isOpen: boolean;
    scenario?: ITestScenario;
    onDismiss: () => void;
    onSave: (scenario: Omit<ITestScenario, 'id'> | ITestScenario) => void;
}

export const TestScenarioPanel: React.FC<TestScenarioPanelProps> = ({
    isOpen,
    scenario,
    onDismiss,
    onSave,
}) => {
    const dispatch = useAppDispatch();
    const { tasks: clinicalTasks } = useAppSelector((state) => state.clinicalTasks);
    const { models } = useAppSelector((state) => state.models);
    const [currentScenario, setCurrentScenario] = useState<Partial<ITestScenario>>({});
    const testScenarios: ITestScenario[] = useAppSelector((state) => state.testScenarios.scenarios);
    const [selectedTaskId, setSelectedTaskId] = useState<string>('');
    const [availableModels, setAvailableModels] = useState<IModel[]>([]);
    const [formErrors, setFormErrors] = useState<{ [key: string]: string }>({});
    const [experimentType, setCurrentExperimentType] = useState<string>('Single Evaluation');
    const [experimentQuestions, setExperimentQuestions] = useState<EvalQuestion[]>([]);
    const [allowOutputEditing, setAllowOutputEditing] = useState<string>('No');
    const [questionsWithEvalMetrics, setQuestionsWithEvalMetrics] = useState<EvalQuestion[]>([]);
    const [questionErrors, setQuestionErrors] = useState<{ [key: number]: { errorMessage: string } }>({});
    
    // Helper function to update question options immutably
    const updateQuestionOption = (
        questionIndex: number, 
        optionIndex: number, 
        field: 'text' | 'value', 
        value: string
    ) => {
        setExperimentQuestions(prevQuestions => 
            prevQuestions.map((question, qIndex) => {
                if (qIndex !== questionIndex) return question;
                
                return {
                    ...question,
                    options: question.options?.map((option, oIndex) => {
                        if (oIndex !== optionIndex) return option;
                        
                        return {
                            ...option,
                            [field]: value
                        };
                    }) || []
                };
            })
        );
    };

    // Helper function to remove question option immutably
    const removeQuestionOption = (questionIndex: number, optionIndex: number) => {
        setExperimentQuestions(prevQuestions => 
            prevQuestions.map((question, qIndex) => {
                if (qIndex !== questionIndex) return question;
                
                return {
                    ...question,
                    options: question.options?.filter((_, oIndex) => oIndex !== optionIndex) || []
                };
            })
        );
    };
    
    useEffect(() => {
        var allQuestions = testScenarios.flatMap((scenario) => scenario.questions);
        var questionsWithEvalMetrics = allQuestions.filter((question): question is EvalQuestion => !!question && !!question.evalMetric);
        console.log("Questions with eval metrics:", questionsWithEvalMetrics);
        setQuestionsWithEvalMetrics(questionsWithEvalMetrics.concat(ExperimentQuestionPresets.flatMap(preset => preset.questions)));
    },[testScenarios]);

    useEffect(() => {
        if (isOpen) {
            dispatch(fetchTasks());
            dispatch(fetchModels());
        }
    }, [isOpen, dispatch]);

    
    useEffect(() => {
        if( scenario)
        {
            setCurrentScenario(prev => ({
                ...prev,
                allowOutputEditing: allowOutputEditing === 'Yes'
            }));
        }
    }, [allowOutputEditing]);
    // Update available models when task selection changes
    useEffect(() => {
        if (selectedTaskId) {
            const selectedTask = clinicalTasks.find((task: IClinicalTask) => task.id === selectedTaskId);
            if (selectedTask) {
                // Get unique model IDs from the task's dataset-model pairs
                const taskModelIds = Array.from(new Set(
                    selectedTask.dataSetModels.map((pair: TaskDataSetModel) => pair.modelId)
                ));
                // Filter models to only those in the task's pairs
                const filteredModels = models.filter(model => taskModelIds.includes(model.id));
                setAvailableModels(filteredModels);
                
                // Clear selected models if they're not available for this task
                setCurrentScenario(prev => ({
                    ...prev,
                    modelIds: prev.modelIds?.filter(id => taskModelIds.includes(id)) || []
                }));

            }
        } else {
            setAvailableModels([]);
        }
    }, [selectedTaskId, clinicalTasks, models]);

    // Initialize form when opened
    useEffect(() => {
        if (scenario) {
            setCurrentScenario(scenario);
            setSelectedTaskId(scenario.taskId);
            setCurrentExperimentType(scenario.experimentType || 'Single Evaluation');
            var questions = JSON.parse(JSON.stringify(scenario.questions || []));
            setExperimentQuestions(questions);
            setAllowOutputEditing(scenario.allowOutputEditing ? 'Yes' : 'No');
        } else {
            setCurrentScenario({
                name: '',
                description: '',
                taskId: '',
                modelIds: [],
                tags: []
            });
            setExperimentQuestions([]);
            setSelectedTaskId('');
        }
    }, [scenario, isOpen]);

   
    const validateForm = (): boolean => {
        const questionErrors: { [key: number]: { errorMessage: string } } = {};
        const errors: { [key: string]: string } = {};
        if (!currentScenario.name?.trim()) {
            errors.name = 'Name is required';
        }
        if (!currentScenario.taskId) {
            errors.taskId = 'Task is required';
        }
        if (!currentScenario.modelIds?.length) {
            errors.modelIds = 'At least one model is required';
        }
        if( experimentQuestions.length === 0 && allowOutputEditing === 'No' && experimentType === 'Single Evaluation') {
            errors.questions = 'At least one question is required if output editing is disabled';
        }
        if( experimentQuestions.length > 0) {
            experimentQuestions.forEach((question, index) => {
                question.options?.forEach((option) => {
                    if (!option.text?.trim()) {
                        questionErrors[index] = { errorMessage: 'Option text is required' };
                    }
                    if (!option.value?.trim()) {
                        questionErrors[index] = { errorMessage: 'Option answer is required' };
                    }
                });

            });
        }


        // if (experimentType === 'Preference Assessment' && (currentScenario.modelIds?.length || 0) < 2) {
        //     errors.modelIds = 'Preference Assessment requires at least two models';
        // }
        // if( experimentQuestions.length === 0 && allowOutputEditing === 'No') {
        //     errors.questions = 'At least one question is required if output editing is disabled';
        // }
        setFormErrors(errors);
        setQuestionErrors(questionErrors);
        return Object.keys(errors).length === 0 && Object.keys(questionErrors).length === 0;
    };

    const handleSave = () => {
        console.log('Current scenario before save:', currentScenario);
        if (validateForm()) {
            var updateScenario = {
                ...currentScenario,
                experimentType: experimentType || 'Single Evaluation',
                allowOutputEditing: allowOutputEditing === 'Yes',
                questions: experimentQuestions
            };
            onSave(updateScenario as Omit<ITestScenario, 'id'>);
        }
    };

    return (
        <Panel
            isOpen={isOpen}
            onDismiss={onDismiss}
            headerText={scenario ? 'Edit Experiment' : 'New Experiment'}
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
        >
            <Stack tokens={{ childrenGap: 15 }}>
                <TextField
                    label="Name"
                    required
                    value={currentScenario.name || ''}
                    onChange={(_, value) => 
                        setCurrentScenario(prev => ({ ...prev, name: value }))}
                />

                <Dropdown
                    label="Clinical Task"
                    required
                    options={clinicalTasks.map((task: IClinicalTask) => ({
                        key: task.id,
                        text: task.name
                    }))}
                    selectedKey={selectedTaskId}
                    onChange={(_, option) => {
                        setSelectedTaskId(option?.key as string);
                        setCurrentScenario(prev => ({
                            ...prev,
                            taskId: option?.key as string,
                            modelIds: [] // Clear selected models when task changes
                        }));
                    }}
                />

                {selectedTaskId && (
                    <Stack>
                        <Label>Models</Label>
                        {availableModels.map(model => (
                            <Checkbox
                                key={model.id}
                                label={`${model.name} (${model.modelType})`}
                                checked={currentScenario.modelIds?.includes(model.id)}
                                onChange={(_, checked) => {
                                    setCurrentScenario(prev => ({
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

                <TextField
                    label="Description"
                    multiline
                    rows={3}
                    value={currentScenario.description || ''}
                    onChange={(_, value) => setCurrentScenario({ ...currentScenario, description: value })}
                />
                <TextField
                    label="Reviewer Instructions"
                    multiline
                    rows={3}
                    value={currentScenario.reviewerInstructions || ''}
                    onChange={(_, value) => setCurrentScenario({ ...currentScenario, reviewerInstructions: value })}
                />
                <Dropdown
                    label="Allow Output Editing"
                    options={[
                        { key: 'Yes', text: 'Yes' },
                        { key: 'No', text: 'No' },
                    ]}
                    selectedKey={allowOutputEditing || 'No'}
                    onChange={(_, option) => setAllowOutputEditing(option?.key as string)}
                />
                <Dropdown
                    label="Experiment Type"
                    options={[
                        { key: 'Single Evaluation', text: 'Single Evaluation' },
                        { key: 'Arena', text: 'Preference Assessment' },
                    ]}
                    selectedKey={experimentType || 'Single Evaluation'}
                    onChange={(_, option) => setCurrentExperimentType(option?.key as string)}
                />
                
                { experimentType === 'Single Evaluation' && (
                    <Stack tokens={{ childrenGap: 15 }}>
                        <Label>Experiment Questions</Label>
                        
                        {experimentQuestions.map((question, questionIndex) => (
                            <Stack key={questionIndex} styles={{ root: { border: '1px solid #d1d1d1', padding: '15px', borderRadius: '4px' } }}>
                                <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                    <Label>Question {questionIndex + 1}</Label>
                                    <DefaultButton
                                        iconProps={{ iconName: 'Delete' }}
                                        onClick={() => {
                                            const newQuestions = [...experimentQuestions];
                                            newQuestions.splice(questionIndex, 1);
                                            setExperimentQuestions(newQuestions);
                                        }}
                                        text="Remove"
                                    />
                                </Stack>
                                
                                <ComboBox
                                    label="Metric Name"
                                    allowFreeform
                                    autoComplete="on"
                                    options={[
                                        { key: '', text: 'Enter new metric...' },
                                        ...Array.from(new Set(questionsWithEvalMetrics.map(q => q.evalMetric)))
                                            .filter((metric): metric is string => !!metric)
                                            .map(metric => ({ key: metric, text: metric }))
                                    ]}
                                    selectedKey={question.evalMetric || ''}
                                    text={question.evalMetric || ''}
                                    onChange={(_, option, index, value) => {
                                        const newQuestions = [...experimentQuestions];
                                        const selectedMetric = value || option?.key as string || '';
                                        newQuestions[questionIndex].evalMetric = selectedMetric;
                                        
                                        // If an existing metric is selected, populate with its question data
                                        if (selectedMetric && option) {
                                            const existingQuestion = questionsWithEvalMetrics.find(q => q.evalMetric === selectedMetric);
                                            if (existingQuestion) {
                                                newQuestions[questionIndex].questionText = existingQuestion.questionText;
                                                newQuestions[questionIndex].options = existingQuestion.options ? [...existingQuestion.options] : [];
                                            }
                                        }else{
                                            var newValue = isNaN(parseFloat(value || '')) ? '' : parseFloat(value || '').toString();
                                            newQuestions[questionIndex].options.forEach(option => {
                                                option.text = '';
                                                option.value = newValue;
                                            });
                                        }
                                        setExperimentQuestions(newQuestions);
                                    }}
                                />

                                <TextField
                                    label="Question Text"
                                    value={question.questionText || ''}
                                    onChange={(_, value) => {
                                        const newQuestions = [...experimentQuestions];
                                        newQuestions[questionIndex].questionText = value || '';
                                        setExperimentQuestions(newQuestions);
                                    }}
                                />

                                <Stack>
                                    <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                        <Label>Response Options</Label>
                                        <DefaultButton
                                            iconProps={{ iconName: 'Add' }}
                                            onClick={() => {
                                                const newQuestions = [...experimentQuestions];
                                                if (!newQuestions[questionIndex].options) {
                                                    newQuestions[questionIndex].options = [];
                                                }
                                                newQuestions[questionIndex].options!.push({ id: 'option_' + Date.now(), text: '', value: '' });
                                                setExperimentQuestions(newQuestions);
                                            }}
                                            text="Add Option"
                                        />
                                    </Stack>
                                    
                                    {(!question.options || question.options.length === 0) ? (
                                        <Label style={{ fontStyle: 'italic', color: '#666' }}>Free response</Label>
                                    ) : (
                                        question.options.map((option, optionIndex) => (
                                            <div className="optionDialoge" key={optionIndex}>
                                            { !question.evalMetric && (
                                                <Stack key={optionIndex} horizontal tokens={{ childrenGap: 10 }} verticalAlign="end">
                                                    <TextField
                                                        label="Option Text"
                                                        value={option.text}
                                                        onChange={(_, value) => {
                                                            updateQuestionOption(questionIndex, optionIndex, 'text', value || '');
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <TextField
                                                        label="Option Value"
                                                        value={option.value}
                                                        onChange={(_, value) => {
                                                            updateQuestionOption(questionIndex, optionIndex, 'value', value || '');
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <DefaultButton
                                                        iconProps={{ iconName: 'Delete' }}
                                                        onClick={() => {
                                                            removeQuestionOption(questionIndex, optionIndex);
                                                        }}
                                                        styles={{ root: { minWidth: 'auto' } }}
                                                    />
                                                </Stack>
                                            )}
                                            { question.evalMetric && questionsWithEvalMetrics.filter(q => q.evalMetric === question.evalMetric).length === 0 && (
                                                <Stack key={optionIndex} horizontal tokens={{ childrenGap: 10 }} verticalAlign="end">
                                                    <TextField
                                                        label="Option Text"
                                                        value={option.text}
                                                        onChange={(_, value) => {
                                                            updateQuestionOption(questionIndex, optionIndex, 'text', value || '');
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <TextField
                                                        label="Option Value"
                                                        value={option.value}
                                                        onChange={(_, value) => {
                                                            // Only allow numeric input
                                                            if (/^\d*\.?\d*$/.test(value || '')) {
                                                                updateQuestionOption(questionIndex, optionIndex, 'value', value || '');
                                                            }
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <DefaultButton
                                                        iconProps={{ iconName: 'Delete' }}
                                                        onClick={() => {
                                                            removeQuestionOption(questionIndex, optionIndex);
                                                        }}
                                                        styles={{ root: { minWidth: 'auto' } }}
                                                    />
                                                </Stack>
                                            )}
                                            { question.evalMetric && questionsWithEvalMetrics.filter(q => q.evalMetric === question.evalMetric).length > 0 && (
                                                <Stack key={optionIndex} horizontal tokens={{ childrenGap: 10 }} verticalAlign="end">
                                                    <TextField
                                                        label="Option Text"
                                                        value={option.text}
                                                        onChange={(_, value) => {
                                                            updateQuestionOption(questionIndex, optionIndex, 'text', value || '');
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <TextField
                                                        label="Option Value"
                                                        value={option.value}
                                                        onChange={(_, value) => {
                                                            // Only allow numeric input
                                                            if (/^\d*\.?\d*$/.test(value || '')) {
                                                                updateQuestionOption(questionIndex, optionIndex, 'value', value || '');
                                                            }
                                                        }}
                                                        styles={{ root: { flex: 1 } }}
                                                    />
                                                    <DefaultButton
                                                        iconProps={{ iconName: 'Delete' }}
                                                        onClick={() => {
                                                            removeQuestionOption(questionIndex, optionIndex);
                                                        }}
                                                        styles={{ root: { minWidth: 'auto' } }}
                                                    />
                                                </Stack>
                                            )}
                                            </div>
                                        ))
                                    )}
                                </Stack>
                                 <Stack.Item>
                                    <Text style={{ color: 'red' }}>{questionErrors[questionIndex]?.errorMessage}</Text>
                                </Stack.Item>
                            </Stack>
                        ))}

                        <DefaultButton
                            iconProps={{ iconName: 'Add' }}
                            onClick={() => {
                                setExperimentQuestions([...experimentQuestions, { name:'', id: 'question_' + Date.now(), questionText: '', options: [] }]);
                            }}
                            text="Add Question"
                        />
                    </Stack>
                )}
                { experimentType === 'Preference Assessment' && currentScenario.modelIds && currentScenario.modelIds.length < 2 && (
                    <Label style={{ color: 'red' }}>
                        Preference Assessment requires at least two models to compare.
                    </Label>    
                )}
                <Stack>
                    <Label>Tags</Label>
                    <TagPicker
                        onResolveSuggestions={(filterText) => {
                            if (filterText) {
                                return [{ key: filterText, name: filterText }];
                            }
                            return [];
                        }}
                        selectedItems={(currentScenario.tags || []).map(tag => ({ key: tag, name: tag }))}
                        onChange={items => setCurrentScenario({
                            ...currentScenario,
                            tags: items?.map(item => item.name) || []
                        })}
                        onInputChange={(input) => {
                            if (input && input.endsWith(',')) {
                                const newTag = input.slice(0, -1).trim();
                                if (newTag) {
                                    const currentTags = currentScenario.tags || [];
                                    if (!currentTags.includes(newTag)) {
                                        setCurrentScenario(prev => ({
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
                <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                    {Object.values(formErrors).map((error, index) => (
                        <div key={index}>{error}</div>
                    ))}
                </Stack>
            </Stack>
        </Panel>
    );
};

export {}; 