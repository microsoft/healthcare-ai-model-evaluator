import React, { useEffect, useState } from 'react';
import { Stack, TextField, Label, IconButton, Text, Dropdown,DefaultButton } from '@fluentui/react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { ArenaLayout } from './ArenaLayout';
import { FlagModelOutput } from './FlagModelOutput';
import { fetchNextTrial, updateTrial, fetchDoneTrial } from '../../reducers/arenaReducer';
import { LoadingOverlay } from './LoadingOverlay';
import { BoundingBox, ITestScenario, ITrial, ModelOutput, EvalQuestion } from '../../types/admin';
import { BoundingBoxList } from './BoundingBoxList';
import { useTheme } from '@fluentui/react';
import { all } from 'axios';

interface ArenaABProps {
    onBack: () => void;
    testScenario?: ITestScenario | null;
    inDoneTrialMode: boolean;
}

export const SingleEvaluation: React.FC<ArenaABProps> = ({ onBack, testScenario, inDoneTrialMode }) => {
    const dispatch = useAppDispatch();
    const location = useLocation();
    const navigate = useNavigate();
    const theme = useTheme();
    
    const { currentTrial, isLoading } = useAppSelector((state) => state.arena);
    const [boundingBoxes, setBoundingBoxes] = useState<BoundingBox[]>([]);
    const [hasInitializedBoxes, setHasInitializedBoxes] = useState(false);
    const [pendingQuestionCount, setPendingQuestionCount] = useState(currentTrial?.questions?.filter(q => q.response === '').length || 0);
    const [editedOutput, setEditedOutput] = useState('');
    const [questionResponses, setQuestionResponses] = useState<{ [questionId: string]: string }>({});
    const [isRightPanelExpanded, setIsRightPanelExpanded] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [lastSaveTime, setLastSaveTime] = useState(new Date());
    const [selectedBoxId, setSelectedBoxId] = useState<string>();
    const [boxToDelete, setBoxToDelete] = useState<string>();
    const [hasChanges, setHasChanges] = useState(false);

    const handleBoundingBoxesChange = (newBoxes: BoundingBox[]) => {
        setHasChanges(true);
        boundingBoxes.forEach(box => {
            const newBox = newBoxes.find(b => b.id === box.id);
            if (newBox) {
                newBox.metadata = box.metadata;
            }
        });
        setBoundingBoxes(newBoxes);
    };
   const handleBoundingBoxDelete = (id: string) => {
        if (id === selectedBoxId) {
            setSelectedBoxId(undefined);
        }
        setHasChanges(true);
        setBoxToDelete(id);
        const updatedBoxes = boundingBoxes.filter(box => box.id !== id);
        setBoundingBoxes(updatedBoxes);
        handleBoundingBoxesChange(updatedBoxes);
    };
    // Initialize edited output
    useEffect(() => {
        if (currentTrial?.modelOutputs?.[0]?.output) {
            const outputText = currentTrial.modelOutputs[0].output
                .map(line => line.content)
                .join('\n');
            setEditedOutput(outputText);
        }
    }, [currentTrial]);

    // Initialize question responses
    useEffect(() => {
        if (currentTrial?.questions) {
            const responses: { [questionId: string]: string } = {};
            currentTrial.questions.forEach(question => {
                responses[question.id] = question.response || '';
            });
            setQuestionResponses(responses);
        }
        if (currentTrial?.boundingBoxes) {
            setBoundingBoxes(currentTrial.boundingBoxes);
            setHasInitializedBoxes(true);
        }
    }, [currentTrial?.id]);

    const calculateTimeSpent = () => {
        const sessionStart = sessionStorage.getItem('trialStartTime');
        if (sessionStart) {
            return Date.now() - parseInt(sessionStart);
        }
        return 0;
    };

    const handleQuestionResponse = (questionId: string, response: string) => {
        setHasChanges(true);
        setQuestionResponses(prev => ({
            ...prev,
            [questionId]: response
        }));
        setPendingQuestionCount((currentTrial?.questions?.length || 0) - Object.keys(questionResponses).length);
        console.log('pendingQuestionCount', pendingQuestionCount);
        console.log('Question response updated:', questionId, response);
    };
     const handleBoundingBoxAnnotation = (id: string, annotation: string) => {
        setSelectedBoxId(id);
        setHasChanges(true);
        const updatedBoxes = boundingBoxes.map(box => 
            box.id === id 
                ? { ...box, metadata: { ...box.metadata }, annotation }
                : box
        );
        setBoundingBoxes(updatedBoxes);
    };

    const handleSelectBox = (id: string) => {
        setSelectedBoxId(id);
    };

    const handleSave = async () => {
        if (currentTrial) {
            setHasChanges(false);
            const boxesToSave = boundingBoxes.map(box => ({
                ...box,
                annotation: box.annotation
            }));
            
            // Update questions with responses
            const updatedQuestions = currentTrial.questions?.map(question => ({
                ...question,
                response: questionResponses[question.id] || question.response || ''
            })) || [];
            
            const timeSpent = calculateTimeSpent();
            await dispatch(updateTrial({
                trialId: currentTrial.id,
                update: { 
                    ...currentTrial,
                    boundingBoxes: boxesToSave,
                    questions: updatedQuestions,
                    modelOutputs: [{
                        ...currentTrial.modelOutputs[0],
                        output: editedOutput.split('\n').map(line => ({ 
                            type: 'text' as const, 
                            content: line 
                        }))
                    }]
                },
                timeSpent
            }));
        }
    };

    const handleNext = async () => {
        if (currentTrial) {

            setHasChanges(false);
            var allquestionsanswered = true;
            currentTrial.questions?.forEach(question => {
                if (!questionResponses[question.id]) {
                    allquestionsanswered = false;
                }
            });
            if( ! allquestionsanswered ){
                alert('Please answer all questions before proceeding.');
                return;
            }
            setIsSubmitting(true);
            try {

            // Update questions with responses
            const updatedQuestions = currentTrial.questions?.map(question => ({
                ...question,
                response: questionResponses[question.id] || question.response || ''
            })) || [];
                const boxesToSave = boundingBoxes.map(box => ({
                    ...box,
                    annotation: box.annotation
                }));
                const timeSpent = calculateTimeSpent();
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: {
                        ...currentTrial,
                        status: 'done',
                        boundingBoxes: boxesToSave,
                        questions: updatedQuestions,
                        modelOutputs: [{
                            ...currentTrial.modelOutputs[0],
                            output: editedOutput.split('\n').map(line => ({ 
                                type: 'text' as const, 
                                content: line 
                            }))
                        }]
                    },
                    timeSpent
                }));
                setBoundingBoxes([]);
                if( inDoneTrialMode ) {
                    await dispatch(fetchDoneTrial({testScenarioId: testScenario?.id || '', trialId: currentTrial.id}));
                } else {
                    await dispatch(fetchNextTrial(testScenario?.id || ''));
                }
                setLastSaveTime(new Date());
            } finally {
                setIsSubmitting(false);
            }
        }
    };
    const handleRefreshChart = () => {

            setHasChanges(false);
            setBoundingBoxes([]);
            dispatch(fetchNextTrial(testScenario?.id || ''));
        };
    
    // Handler for right panel expansion that will collapse the reference panel
    const handleRightPanelExpand = () => {
        setIsRightPanelExpanded(!isRightPanelExpanded);
    };
    const handleSkip = async () => {
        if (currentTrial) {
            
            setIsSubmitting(true);
            if (hasChanges) {
                const confirmSkip = window.confirm(
                    'You have unsaved changes that will be lost. Are you sure you want to skip this trial?'
                );
                if (!confirmSkip) {
                    return;
                }
            }

            setHasChanges(false);
            const timeSpent = calculateTimeSpent();
            if( inDoneTrialMode ) {
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: { status: 'done' },
                    timeSpent
                }));
            }else{
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: { status: 'skipped' },
                    timeSpent
                }));
            }
            setBoundingBoxes([]);
            if( inDoneTrialMode ) {
                dispatch(fetchDoneTrial({testScenarioId: testScenario?.id || '', trialId: currentTrial.id}));
            }else{
                dispatch(fetchNextTrial(testScenario?.id || ''));
            }
            setLastSaveTime(new Date());
            setIsSubmitting(false);
        }
    };

    const handleAddBoundingBox = (newBox: BoundingBox) => {
        if( testScenario?.allowOutputEditing !== true ){
            return;
        }
        setBoundingBoxes(prev => [...prev, newBox]);
    };

    const handleUpdateBoundingBox = (updatedBox: BoundingBox) => {
        if( testScenario?.allowOutputEditing !== true ){
            return;
        }
        setBoundingBoxes(prev => 
            prev.map(box => box.id === updatedBox.id ? updatedBox : box)
        );
    };

    const handleDeleteBoundingBox = (boxId: string) => {
        if( testScenario?.allowOutputEditing !== true ){
            return;
        }
        setBoundingBoxes(prev => prev.filter(box => box.id !== boxId));
    };

    const renderQuestionSelector = (question: EvalQuestion) => {
        const currentResponse = questionResponses[question.id] || '';
        
        return (
            <Stack key={question.id} tokens={{ childrenGap: 10 }}>
                <Text variant="medium" styles={{ root: { fontWeight: 600 } }}>
                    {question.questionText}
                </Text>
                {question.options && question.options.length > 0 ? (
                    <Dropdown
                        placeholder="Select an option"
                        options={question.options.map(option => ({
                            key: option.value,
                            text: option.text
                        }))}
                        selectedKey={questionResponses[question.id] || ''}
                        onChange={(_, option) => {
                            if (option) {
                                handleQuestionResponse(question.id, option.key as string);
                            }
                        }}
                    />
                ) : (
                    <TextField
                        placeholder="Enter your response"
                        value={questionResponses[question.id] || ''}
                        onChange={(_, value) => handleQuestionResponse(question.id, value || '')}
                        multiline
                        rows={3}
                    />
                )}
            </Stack>
        );
    };

    if (isLoading) {
        return <LoadingOverlay />;
    }

    return (
        <ArenaLayout 
            title="Single Evaluation"
            onBack={onBack}
            onSave={handleSave}
            onNewChart={handleSkip}
            inputData={currentTrial?.modelInputs || null}
            isRightExpanded={isRightPanelExpanded}
            arenaType="Single Evaluation"
            onRefreshChart={handleRefreshChart}
            onBoundingBoxesChange={handleBoundingBoxesChange}
            initialBoundingBoxes={boundingBoxes}
            selectedBoxId={selectedBoxId}
            onDeleteBox={handleBoundingBoxDelete}
            boxToDelete={boxToDelete}
            testScenario={testScenario || undefined}
            trial={currentTrial || undefined}
            inDoneTrialMode={inDoneTrialMode}
        >
            {currentTrial && (
            <Stack grow styles={{
                root: {
                    minHeight: '100%',
                    padding: '10px',
                    width: '100%',
                    position: 'relative',
                    overflow: 'scroll'
                }
            }} tokens={{ childrenGap: 0 }}>
                {isSubmitting && <LoadingOverlay />}
                <Stack.Item>
                    <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                        <Label 
                            styles={{ 
                                root: { 
                                    fontSize: '14px',
                                    fontWeight: '600',
                                    marginBottom: '5px'
                                } 
                            }}
                        >
                            Reviewer Instructions
                        </Label>
                        <IconButton
                            iconProps={{ iconName: isRightPanelExpanded ? 'BackToWindow' : 'FullScreen' }}
                            onClick={handleRightPanelExpand}
                            title={isRightPanelExpanded ? 'Collapse' : 'Expand'}
                            styles={{
                                root: {
                                    marginBottom: '5px'
                                }
                            }}
                        />
                    </Stack>
                    <TextField
                        multiline
                        rows={3}
                        value={currentTrial?.reviewerInstructions || ""}
                        styles={{
                            root: { backgroundColor: '#fff' },
                            field: { padding: '10px', resize: "vertical" }
                        }}
                        readOnly
                    />
                </Stack.Item>
                <Stack.Item styles={{ root: { position:'relative',minHeight: '300px' } }}>
                    
                        <Label>Model Output {testScenario?.allowOutputEditing ? '(Editable)' : ''    }</Label>
                        <TextField
                            multiline
                            rows={10}
                            value={editedOutput}
                            readOnly={!testScenario?.allowOutputEditing}    
                            onChange={(_, newValue) => setEditedOutput(newValue || '')}
                            styles={{
                                root: {
                                    backgroundColor: '#fff',
                                    flex: '1 1 auto'
                                },
                                fieldGroup: { height: '100%' },
                                field: {
                                    padding: '10px',
                                    resize: 'vertical',
                                    height: '100%'
                                }
                            }}
                        />
                        <FlagModelOutput 
                            modelName="Model" 
                            modelId={currentTrial?.modelOutputs[0]?.modelId || ''}
                        />
                </Stack.Item>
                 {/* Dynamic Questions Section */}
                 {boundingBoxes.length > 0 && (
                    <Stack styles={{ root: { flex: '2', marginTop: '20px', paddingBottom: '150px' } }}>
                        <BoundingBoxList 
                            boundingBoxes={boundingBoxes}
                            trial={currentTrial}
                            allowEditing={testScenario?.allowOutputEditing}
                            onDelete={handleBoundingBoxDelete}
                            onAnnotationChange={handleBoundingBoxAnnotation}
                            selectedBoxId={selectedBoxId}
                            onSelectBox={handleSelectBox}
                        />
                    </Stack>
                )}
                {currentTrial.questions && currentTrial.questions.length > 0 && (
                    <Stack tokens={{ childrenGap: 15 }}>
                        <Label style={{ fontSize: '18px', fontWeight: 'bold' }}>
                            Evaluation Questions
                        </Label>
                        {currentTrial.questions.map(question => renderQuestionSelector(question))}
                    </Stack>
                )}
                    <Stack 
                        horizontal 
                        horizontalAlign="end"
                        tokens={{ childrenGap: 10 }}
                        styles={{
                            root: {
                                marginTop: '10px',
                                '@media (max-width: 480px)': {
                                    justifyContent: 'center',
                                    flexDirection: 'column',
                                    alignItems: 'stretch',
                                    '> *': {
                                        margin: '5px 0'
                                    }
                                }
                            }
                        }}
                    >
                                <DefaultButton
                                    iconProps={{ iconName: 'CheckMark' }}
                                    text="Submit"
                                    onClick={async () => {
                                        if (pendingQuestionCount === 0 && handleNext) {
                                            await handleNext();
                                        }
                                    }}
                                    disabled={pendingQuestionCount!=0}
                                    styles={{
                                        root: {
                                            color: theme.palette.white,
                                            backgroundColor: theme.palette.themePrimary,
                                            minWidth: 'auto',
                                            padding: '0 12px',
                                            height: '32px',
                                            ':hover': {
                                                backgroundColor: theme.palette.themeDarkAlt
                                            },
                                            ':disabled': {
                                                backgroundColor: theme.palette.neutralLighter
                                            },
                                            '@media (max-width: 480px)': {
                                                width: '100%'
                                            }
                                        },
                                        icon: {
                                            fontSize: 12,
                                            marginRight: 4
                                        }
                                    }}
                                />
                                <DefaultButton
                                    iconProps={{ iconName: 'Next' }}
                                    text="Skip"
                                    onClick={handleSkip}
                                    styles={{
                                        root: {
                                            minWidth: 'auto',
                                            padding: '0 12px',
                                            height: '32px',
                                            border: `1px solid ${theme.palette.neutralTertiary}`,
                                            ':hover': {
                                                backgroundColor: theme.palette.neutralLighter
                                            },
                                            '@media (max-width: 480px)': {
                                                width: '100%',
                                                marginLeft: '0!important'
                                            }
                                        },
                                        icon: {
                                            fontSize: 12,
                                            marginRight: 4
                                        }
                                    }}
                                />
                            </Stack>
            </Stack>
            )}
        </ArenaLayout>
    );
};
