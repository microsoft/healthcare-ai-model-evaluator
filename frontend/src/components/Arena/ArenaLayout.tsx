import React from 'react';
import { Stack, Spinner, SpinnerSize, MessageBar, MessageBarType, TooltipHost, DirectionalHint, FontIcon } from '@fluentui/react';
import { useSelector } from 'react-redux';
import { RootState } from '../../store/store';
import { SubNavigation } from './SubNavigation';
import { ReferenceDataPanel } from './ReferenceDataPanel';
import { DataContent } from '../../types/dataset';
import { BoundingBox, ITestScenario, ITrial } from '../../types/admin';

export interface ArenaLayoutProps {
    title: string;
    onBack: () => void;
    onSave?: () => void;
    onNewChart?: () => void;
    inputData: DataContent[] | null;
    children: React.ReactNode;
    isRightExpanded?: boolean;
    arenaType: 'A/B Testing' | 'Simple Evaluation' | 'Simple Validation' | 'Full Validation' | 'Single Evaluation';
    onRefreshChart?: () => void;
    onBoundingBoxesChange?: (boxes: BoundingBox[]) => void;
    initialBoundingBoxes?: BoundingBox[];
    selectedBoxId?: string;
    onDeleteBox?: (id: string) => void;
    boxToDelete?: string;
    testScenarioId?: string;
    testScenarioName?: string;
    testScenario?: ITestScenario;
    trial?:ITrial
    inDoneTrialMode?: boolean;
}
const ARENA_DISCLAIMER = `DISCLAIMER: This tool showcases an AI model evaluation and benchmarking tool for healthcare that uses various AI technologies, including foundation models and large language models (such as Azure OpenAI GPT-4). It is not an existing Microsoft product, and Microsoft makes no commitment to build such a product. Generative AI can produce inaccurate or incomplete information. You must thoroughly test and validate that any AI model or evaluation result is suitable for its intended use and identify and mitigate any risks to end users. Carefully review the documentation for every AI tool and service employed.\n\nMicrosoft products and services (1) are not designed, intended, or made available as a medical device, and (2) are not designed or intended to replace professional medical advice, diagnosis, treatment, or judgment and should not be used as a substitute for professional medical advice, diagnosis, treatment, or judgment. Customers and partners are responsible for ensuring that their solutions comply with all applicable laws and regulations.`;
// Floating info icon component
const FloatingInfo: React.FC = () => {
    return (
        <div style={{
            position: 'fixed',
            bottom: 10,
            right: 10,
            zIndex: 1000,
            background: 'rgba(255,255,255,0.95)',
            borderRadius: '50%',
            boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
            width: 20,
            height: 20,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
        }}>
            <TooltipHost
                content={<span style={{ whiteSpace: 'pre-line', maxWidth: 350, display: 'block' }}>{ARENA_DISCLAIMER}</span>}
                directionalHint={DirectionalHint.topLeftEdge}
                styles={{ root: { display: 'flex' } }}
            >
                <FontIcon iconName="Info" style={{ fontSize: 22, color: '#0078d4' }} />
            </TooltipHost>
        </div>
    );
};
const ArenaLayout: React.FC<ArenaLayoutProps> = ({ 
    onBack, 
    onSave, 
    onNewChart,
    inputData,
    children,
    isRightExpanded = false,
    arenaType,
    onRefreshChart,
    onBoundingBoxesChange,
    initialBoundingBoxes,
    selectedBoxId,
    onDeleteBox,
    boxToDelete,
    testScenarioId,
    testScenarioName,
    testScenario,
    trial,
    inDoneTrialMode = false
}) => {
    const { leftPanelWidth } = useSelector((state: RootState) => state.arena);
    const [isLeftPanelExpanded, setIsLeftPanelExpanded] = React.useState(false);
    const { noTrialsAvailable } = useSelector((state: RootState) => state.arena);

    // Default handlers for optional callbacks
    const handleSave = onSave || (() => {});
    const handleNewChart = onNewChart || (() => {});
    if(noTrialsAvailable){
        return (
            <Stack styles={{
                root: {
                    height: 'calc(100%)',
                    position: 'relative'
                }
            }}>
                <Stack.Item>
                <SubNavigation 
                        onBack={onBack}
                        onSave={handleSave} 
                        onNewChart={handleNewChart}
                        arenaType={arenaType}
                        onRefreshChart={onRefreshChart}
                        noTrialsAvailable={noTrialsAvailable}
                        testScenarioName={testScenario?.name || testScenarioName}
                        testScenarioId={testScenarioId}
                        testScenario={testScenario}
                        inDoneTrialMode={inDoneTrialMode}
                        trial={trial}
                    />
                </Stack.Item>
            
            <Stack 
                horizontalAlign="center" 
                verticalAlign="center" 
                styles={{
                    root: {
                        height: 'calc(100vh - 104px)', // 60px main nav + 44px subnav
                        width: '100%'
                    }
                }}
            >
                
                <MessageBar messageBarType={MessageBarType.info} styles={{ root: { width: '400px' } }}>
                    You have completed your assigned trials for experiment: {testScenario?.name}. 
                </MessageBar>
            </Stack>
            </Stack>
        );
    }else if (!inputData) {
        return (
        <Stack 
                horizontalAlign="center" 
                verticalAlign="center" 
                styles={{
                    root: {
                        height: 'calc(100vh - 104px)', // 60px main nav + 44px subnav
                        width: '100%'
                    }
                }}
            >
                <Spinner size={SpinnerSize.large} label="Loading..." />
            </Stack>
        );
    }

    return (
        <Stack styles={{
            root: {
                height: 'calc(100vh - 104px)',
                position: 'relative'
            }
        }}>
            <Stack.Item>
                <SubNavigation 
                    onBack={onBack}
                    onSave={handleSave} 
                    onNewChart={handleNewChart}
                    arenaType={arenaType}
                    onRefreshChart={onRefreshChart}
                    noTrialsAvailable={noTrialsAvailable}
                    testScenarioName={testScenario?.name || testScenarioName}
                    testScenarioId={testScenarioId}
                    testScenario={testScenario}
                    trial={trial}
                    inDoneTrialMode={inDoneTrialMode}
                />
            </Stack.Item>

            <Stack.Item grow>
                <Stack 
                    horizontal 
                    styles={{
                        root: {
                            height: 'calc(100vh - 104px)', // 60px main nav + 44px subnav
                            overflow: 'hidden'
                        }
                    }}
                >
                    {!isRightExpanded && (
                        <Stack.Item 
                            grow 
                            className="reference-data"
                            styles={{ 
                                root: { 
                                    width: isLeftPanelExpanded ? '100%' : `${leftPanelWidth}%`,
                                    position: 'relative',
                                    zIndex: isLeftPanelExpanded ? 1 : 'auto',
                                    height: '100%'
                                } 
                            }}
                        >
                            <ReferenceDataPanel 
                                inputData={inputData}
                                isExpanded={isLeftPanelExpanded}
                                onToggleExpand={() => setIsLeftPanelExpanded(!isLeftPanelExpanded)}
                                onBoundingBoxesChange={onBoundingBoxesChange}
                                initialBoundingBoxes={initialBoundingBoxes}
                                selectedBoxId={selectedBoxId}
                                onDeleteBox={onDeleteBox}
                                boxToDelete={boxToDelete}
                            />
                        </Stack.Item>
                    )}
                    {! isLeftPanelExpanded && (
                        <Stack.Item 
                            grow 
                            styles={{ 
                                root: { 
                                    width: isRightExpanded ? '100%' : `${100 - leftPanelWidth}%`,
                                    overflow:'scroll'
                                } 
                            }}
                        >
                            {children}
                        </Stack.Item>
                    )}
                    <br/>
                    <br/>
                    <br/>
                </Stack>
            </Stack.Item>
            <FloatingInfo />
        </Stack>
    );
};

export { ArenaLayout }; 