import React, { useState, useEffect } from 'react';
import { 
    IconButton, 
    Panel, 
    PanelType,
    Stack,
    TextField,
    DefaultButton,
    PrimaryButton,
    Text,
    MessageBar,
    MessageBarType,
    Dropdown,
    IDropdownOption,
    IButtonStyles
} from '@fluentui/react';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { updateTrial, updateTrialFlag } from '../../reducers/arenaReducer';
import { TrialFlag, FlagTags, FlagTagType } from '../../types/admin';
import { useMongoUserId } from '../../hooks/useMongoUserId';
import { useAuth } from '../../contexts/AuthContext';

interface FlagModelOutputProps {
    modelName: string;
    modelId: string;
}

export const FlagModelOutput: React.FC<FlagModelOutputProps> = ({ modelName, modelId }) => {
    const dispatch = useAppDispatch();
    const { user } = useAuth();
    
    const { currentTrial } = useAppSelector(state => state.arena);
    const { mongoUserId } = useMongoUserId();
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [flagDescription, setFlagDescription] = useState('');
    const [selectedFlagTags, setSelectedFlagTags] = useState<FlagTagType[]>([]);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Load existing flag data when panel opens
    useEffect(() => {
        if (isPanelOpen && currentTrial?.flags) {
            const existingFlag = currentTrial.flags.find(flag => flag.modelId === modelId);
            if (existingFlag) {
                setFlagDescription(existingFlag.text);
                setSelectedFlagTags(existingFlag.flagTags || []);
            }
        }else{
            setFlagDescription('');
            setSelectedFlagTags([]);
        }
    }, [isPanelOpen, currentTrial?.flags, modelId]);

    const flagTagOptions: IDropdownOption[] = Object.entries(FlagTags).map(([_, value]) => ({
        key: value,
        text: value
    }));

    const handleSubmit = async () => {
        if (!currentTrial) return;
        
        setIsSubmitting(true);
        setError(null);
        // Prefer ID from hook; fall back to auth context
        const effectiveUserId = mongoUserId || user?.id;
        if(!effectiveUserId){
            setError('User not authenticated. Please log in again.');
            setIsSubmitting(false);
            return;
        }

        try {
            const newFlag: TrialFlag = {
                modelId,
                text: flagDescription,
                userId: effectiveUserId,
                createdAt: new Date().toISOString(),
                flagTags: selectedFlagTags
            };

            // Filter out any existing flags for this model and add the new one
            const updatedFlags = [
                ...(currentTrial.flags?.filter(flag => flag.modelId !== modelId) || []),
                newFlag
            ];

            await dispatch(updateTrialFlag({
                trialId: currentTrial.id,
                update: {
                    ...currentTrial,
                    flags: updatedFlags
                }
            })).unwrap();
            
            setIsPanelOpen(false);
        } catch (err) {
            setError('Failed to save flag. Please try again.');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleCancel = () => {
        // Reset to existing flag data if available
        if (currentTrial?.flags) {
            const existingFlag = currentTrial.flags.find(flag => flag.modelId === modelId);
            if (existingFlag) {
                setFlagDescription(existingFlag.text);
                setSelectedFlagTags(existingFlag.flagTags || []);
            } else {
                setFlagDescription('');
                setSelectedFlagTags([]);
            }
        }
        setError(null);
        setIsPanelOpen(false);
    };

    // Get styles for flag icon - red if flagged
    const getFlagIconStyles = (): IButtonStyles => ({
        root: {
            position: 'absolute' as const,
            bottom: '10px',
            right: '10px',
            color: currentTrial?.flags?.some(flag => flag.modelId === modelId) ? '#c50f1f' : '#666'
        },
        rootHovered: {
            color: currentTrial?.flags?.some(flag => flag.modelId === modelId) ? '#e81123' : '#333'
        }
    });

    return (
        <>
            <IconButton
                iconProps={{ iconName: 'Flag' }}
                title={`Flag ${modelName} output`}
                styles={getFlagIconStyles()}
                onClick={() => setIsPanelOpen(true)}
            />
            <Panel
                isOpen={isPanelOpen}
                onDismiss={handleCancel}
                type={PanelType.medium}
                headerText={`Flag ${modelName} Output`}
                closeButtonAriaLabel="Close"
                styles={{
                    content: {
                        padding: '20px'
                    }
                }}
            >
                <Stack tokens={{ childrenGap: 20 }}>
                    {error && (
                        <MessageBar messageBarType={MessageBarType.error}>
                            {error}
                        </MessageBar>
                    )}
                    <Text>
                        Please describe why you are flagging this model output:
                    </Text>
                    <TextField
                        multiline
                        rows={6}
                        value={flagDescription}
                        onChange={(_, newValue) => setFlagDescription(newValue || '')}
                        placeholder="Enter your description here..."
                        disabled={isSubmitting}
                    />
                    <Dropdown
                        label="Flag Categories"
                        placeholder="Select categories"
                        multiSelect
                        options={flagTagOptions}
                        selectedKeys={selectedFlagTags}
                        onChange={(_, item) => {
                            if (item) {
                                setSelectedFlagTags(
                                    item.selected 
                                        ? [...selectedFlagTags, item.key as FlagTagType]
                                        : selectedFlagTags.filter(key => key !== item.key)
                                );
                            }
                        }}
                        disabled={isSubmitting}
                    />
                    <Stack horizontal tokens={{ childrenGap: 10 }} horizontalAlign="end">
                        <DefaultButton 
                            onClick={handleCancel} 
                            text="Cancel" 
                            disabled={isSubmitting}
                        />
                        <PrimaryButton 
                            onClick={handleSubmit} 
                            text={isSubmitting ? 'Submitting...' : 'Submit'} 
                            disabled={!flagDescription.trim() || isSubmitting}
                        />
                    </Stack>
                </Stack>
            </Panel>
        </>
    );
};

export {}; 