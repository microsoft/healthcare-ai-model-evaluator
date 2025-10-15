import React, { useEffect, useState } from 'react';
import { Stack, TextField, Label, IconButton } from '@fluentui/react';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { fetchNextTrial, updateTrial, fetchDoneTrial } from '../../reducers/arenaReducer';
import { ArenaLayout } from './ArenaLayout';
import { FlagModelOutput } from './FlagModelOutput';
import { ModelPreferenceSelector, PreferenceOption } from './ModelPreferenceSelector';
import { LoadingOverlay } from './LoadingOverlay';
import { BoundingBox } from '../../types/admin';
import { BoundingBoxList } from './BoundingBoxList';
import { ITrial, ITestScenario } from '../../types/admin';

interface ArenaABProps {
    onBack: () => void;
    testScenario?: ITestScenario | null;
    inDoneTrialMode: boolean;
}

export const ArenaAB: React.FC<ArenaABProps> = ({ onBack, testScenario, inDoneTrialMode }) => {
    const dispatch = useAppDispatch();
    const currentTrial = useAppSelector(state => state.arena.currentTrial);
    const [isRightPanelExpanded, setIsRightPanelExpanded] = useState(false);
    const [boundingBoxes, setBoundingBoxes] = useState<BoundingBox[]>([]);
    const [selectedBoxId, setSelectedBoxId] = useState<string>();
    const [selectedModelId, setSelectedModelId] = useState<string>();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [lastSaveTime, setLastSaveTime] = useState(new Date());
    const [boxToDelete, setBoxToDelete] = useState<string>();
    const [hasInitializedBoxes, setHasInitializedBoxes] = useState(false);
    const [expandedModel, setExpandedModel] = useState<'A' | 'B' | null>(null);

   

    // Initialize bounding boxes from trial data
    useEffect(() => {
        if (currentTrial?.boundingBoxes && !hasInitializedBoxes) {
            const initialBoundingBoxes: BoundingBox[] = currentTrial.boundingBoxes.map(box => ({
                id: box.id,
                x: box.x,
                y: box.y,
                width: box.width,
                height: box.height,
                imageIndex: box.imageIndex,
                coordinateType: box.coordinateType,
                modelId: box.modelId,
                metadata: {
                    annotation: box.annotation
                }
            }));
            setBoundingBoxes(initialBoundingBoxes);
            setHasInitializedBoxes(true);
        }
    }, [currentTrial?.boundingBoxes, hasInitializedBoxes]);

    // Reset timer and initialization flag when trial changes
    useEffect(() => {
        setLastSaveTime(new Date());
        setHasInitializedBoxes(false);
    }, [currentTrial?.id]);

    const handleBoundingBoxesChange = (newBoxes: BoundingBox[]) => {
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
        setBoxToDelete(id);
        const updatedBoxes = boundingBoxes.filter(box => box.id !== id);
        setBoundingBoxes(updatedBoxes);
        handleBoundingBoxesChange(updatedBoxes);
    };

    const handleBoundingBoxAnnotation = (id: string, annotation: string) => {
        setSelectedBoxId(id);
        const updatedBoxes = boundingBoxes.map(box => 
            box.id === id 
                ? { ...box, metadata: { ...box.metadata, annotation } }
                : box
        );
        setBoundingBoxes(updatedBoxes);
    };

    const handleModelSelect = (boxId: string, modelId: string) => {
        setSelectedModelId(modelId);
        const updatedBoxes = boundingBoxes.map(box => 
            box.id === boxId 
                ? { ...box, modelId }
                : box
        );
        setBoundingBoxes(updatedBoxes);
    };

    const handleSelectBox = (id: string) => {
        setSelectedBoxId(id);
        const box = boundingBoxes.find(b => b.id === id);
        if (box?.modelId) {
            setSelectedModelId(box.modelId);
        }
    };

    const handleRefreshChart = () => {
        dispatch(fetchNextTrial(testScenario?.id || ''));
    };

    const handleSave = async () => {
        if (currentTrial) {
            const boxesToSave = boundingBoxes.map(box => ({
                ...box,
                annotation: box.annotation
            }));
            const timeSpent = calculateTimeSpent();
            await dispatch(updateTrial({
                trialId: currentTrial.id,
                update: { 
                    ...currentTrial,
                    boundingBoxes: boxesToSave
                },
                timeSpent
            }));
            setLastSaveTime(new Date());
        }
    };

    const handleSkip = async () => {
        if (currentTrial) {
            const timeSpent = calculateTimeSpent();
            if( inDoneTrialMode ) {
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: { status: 'done' },
                    timeSpent
                }));
                dispatch(fetchDoneTrial({testScenarioId: testScenario?.id || '', trialId: currentTrial.id}));
            } else {
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: { status: 'skipped' },
                    timeSpent
                }));
                dispatch(fetchNextTrial(testScenario?.id || ''));
            }
            
            setLastSaveTime(new Date());
        }
    };

    const handlePreferenceSubmit = async (preference: PreferenceOption) => {
        if (currentTrial && preference) {
            setIsSubmitting(true);
            try {
                const timeSpent = calculateTimeSpent();
                await dispatch(updateTrial({
                    trialId: currentTrial.id,
                    update: {
                        ...currentTrial,
                        status: 'done',
                        response: {
                            modelId: `${currentTrial.modelOutputs[0]?.modelId},${currentTrial.modelOutputs[1]?.modelId}`,
                            text: preference
                        }
                    },
                    timeSpent
                }));
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

    const calculateTimeSpent = () => {
        const now = new Date();
        const timeSpentSeconds = (now.getTime() - lastSaveTime.getTime()) / 1000; // Convert to seconds
        return timeSpentSeconds;
    };

    return (
        <ArenaLayout
            title="Arena A/B"
            onBack={onBack}
            onSave={handleSave}
            onNewChart={handleSkip}
            inputData={currentTrial?.modelInputs || null}
            isRightExpanded={isRightPanelExpanded}
            arenaType="A/B Testing"
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
            <Stack styles={{ 
                root: { 
                    height: '100%', 
                    padding: '10px', 
                    width: '100%', 
                    position: 'relative',
                    overflow: 'auto'
                } 
            }} tokens={{ childrenGap: 20 }}>
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
                            onClick={() => setIsRightPanelExpanded(!isRightPanelExpanded)}
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

                <Stack.Item grow styles={{ root: { minHeight: '500px' } }}>
                    <Stack horizontal styles={{ root: { height: '80%' } }} tokens={{ childrenGap: 20 }}>
                        <Stack.Item grow style={{ 
                            height: '100%', 
                            visibility: (expandedModel === 'B' ? 'hidden' : 'visible'), 
                            paddingBottom: boundingBoxes.length === 0 ? '0px' : '20px',
                            marginBottom: boundingBoxes.length === 0 ? '20px' : '0px',
                            width: (expandedModel === 'A' ? '100%' : expandedModel === 'B' ? '0%' : '50%'),
                            display: 'flex',
                            flexDirection: 'column'
                        }}>
                            <Stack styles={{ root: { flex: '3', position: 'relative' } }}>
                                <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                    <Label>Model A Output</Label>
                                    <IconButton
                                        iconProps={{ iconName: expandedModel === 'A' ? 'BackToWindow' : 'FullScreen' }}
                                        onClick={() => setExpandedModel(expandedModel === 'A' ? null : 'A')}
                                        title={expandedModel === 'A' ? 'Collapse' : 'Expand'}
                                    />
                                </Stack>
                                <TextField
                                    multiline
                                    readOnly
                                    value={currentTrial?.modelOutputs[0]?.output.map(content => content.content).join('\n') || ""}
                                    styles={{
                                        root: {
                                            backgroundColor: '#fff',
                                            height: '100%',
                                            '> div': { height: '100%' }
                                        },
                                        fieldGroup: { height: '100%' },
                                        field: {
                                            padding: '10px',
                                            height: '100%'
                                        }
                                    }}
                                />
                                <FlagModelOutput 
                                    modelName="Model A" 
                                    modelId={currentTrial?.modelOutputs[0]?.modelId || ''}
                                />
                            </Stack>
                            {boundingBoxes.length > 0 && (
                                <Stack styles={{ root: { flex: '2', marginTop: '20px' , paddingBottom: '50px'} }}>
                                    <BoundingBoxList 
                                        boundingBoxes={boundingBoxes.filter(box => box.modelId === currentTrial?.modelOutputs[0]?.modelId || typeof box.modelId === 'undefined')}
                                    onDelete={handleBoundingBoxDelete}
                                    trial={currentTrial!}
                                    onAnnotationChange={handleBoundingBoxAnnotation}
                                    selectedBoxId={selectedBoxId}
                                    onSelectBox={handleSelectBox}
                                    modelOptions={currentTrial?.modelOutputs.map(output => ({
                                        id: output.modelId,
                                        name: output.modelId === currentTrial.modelOutputs[0].modelId ? 'Model A' : 'Model B'
                                    }))}
                                    onModelSelect={handleModelSelect}
                                    selectedModelId={selectedModelId}
                                    title="Model A Bounding Boxes"
                                />
                                </Stack>
                            )}
                        </Stack.Item>

                        <Stack.Item grow style={{ 
                            height: '100%', 
                            visibility: (expandedModel === 'A' ? 'hidden' : 'visible'), 
                            width: (expandedModel === 'B' ? '100%' : expandedModel === 'A' ? '0%' : '50%'),
                            display: 'flex',
                            flexDirection: 'column'
                        }}>
                            <Stack styles={{ root: { flex: '3', position: 'relative'  } }}>
                                <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                    <Label>Model B Output</Label>
                                    <IconButton
                                        iconProps={{ iconName: expandedModel === 'B' ? 'BackToWindow' : 'FullScreen' }}
                                        onClick={() => setExpandedModel(expandedModel === 'B' ? null : 'B')}
                                        title={expandedModel === 'B' ? 'Collapse' : 'Expand'}
                                    />
                                </Stack>
                                <TextField
                                    multiline
                                    readOnly
                                    value={currentTrial?.modelOutputs[1]?.output.map(content => content.content).join('\n') || ""}
                                    styles={{
                                        root: {
                                            backgroundColor: '#fff',
                                            height: '100%',
                                            '> div': { height: '100%' }
                                        },
                                        fieldGroup: { height: '100%' },
                                        field: {
                                            padding: '10px',
                                            height: '100%'
                                        }
                                    }}
                                />
                                <FlagModelOutput 
                                    modelName="Model B" 
                                    modelId={currentTrial?.modelOutputs[1]?.modelId || ''}
                                />
                            </Stack>
                            {boundingBoxes.length > 0 && (
                                <Stack styles={{ root: { flex: '2', marginTop: '20px', paddingBottom: '50px' } }}>
                                    <BoundingBoxList 
                                        boundingBoxes={boundingBoxes.filter(box => box.modelId === currentTrial?.modelOutputs[1]?.modelId)}
                                    onDelete={handleBoundingBoxDelete}
                                    onAnnotationChange={handleBoundingBoxAnnotation}
                                    selectedBoxId={selectedBoxId}
                                    onSelectBox={handleSelectBox}
                                    modelOptions={currentTrial?.modelOutputs.map(output => ({
                                        id: output.modelId,
                                        name: output.modelId === currentTrial.modelOutputs[0].modelId ? 'Model A' : 'Model B'
                                    }))}
                                    onModelSelect={handleModelSelect}
                                    selectedModelId={selectedModelId}
                                    title="Model B Bounding Boxes"
                                    />
                                </Stack>
                            )}
                        </Stack.Item>
                    </Stack>
                </Stack.Item>

                
            </Stack>
            <Stack styles={{ root: { right: 10, backgroundColor: '#f5f5f5', position: 'absolute', bottom: 0, width: (isRightPanelExpanded ? '100%' : '50%') } }}>
                <Stack.Item>
                    <ModelPreferenceSelector 
                        onPreferenceChange={() => {}}
                        onSubmit={handlePreferenceSubmit}
                        onSkip={handleSkip}
                    />
                </Stack.Item>
            </Stack>
        </ArenaLayout>
    );
}; 