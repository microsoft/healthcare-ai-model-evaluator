import React, { useState } from 'react';
import { Stack, Text, DefaultButton, useTheme } from '@fluentui/react';

export type PreferenceOption = 'A' | 'B' | 'both-good' | 'both-bad' | null;

export interface ModelPreferenceSelectorProps {
    onPreferenceChange?: (preference: PreferenceOption) => void;
    onSubmit?: (preference: PreferenceOption) => Promise<void>;
    onSkip?: () => void;
}

export const ModelPreferenceSelector: React.FC<ModelPreferenceSelectorProps> = ({ 
    onPreferenceChange,
    onSubmit,
    onSkip 
}) => {
    const theme = useTheme();
    const [selectedPreference, setSelectedPreference] = useState<PreferenceOption>(null);

    const handlePreferenceClick = (preference: PreferenceOption) => {
        setSelectedPreference(preference);
        onPreferenceChange?.(preference);
    };

    const handleSubmit = async () => {
        if (selectedPreference && onSubmit) {
            await onSubmit(selectedPreference);
            setSelectedPreference(null); // Clear selection after submit
        }
    };

    const getButtonStyles = (preference: PreferenceOption) => ({
        root: {
            minWidth: '120px',
            margin: '5px',
            backgroundColor: selectedPreference === preference ? 'rgb(0, 120, 212)' : undefined,
            color: selectedPreference === preference ? 'white' : undefined,
            ':hover': {
                backgroundColor: selectedPreference === preference ? '#106EBE' : undefined,
                color: selectedPreference === preference ? 'white' : undefined,
            }
        }
    });

    return (
        <Stack tokens={{ childrenGap: 15 }} style={{ paddingBottom: '10px' }}>
            <Text 
                variant="large" 
                block 
                styles={{ 
                    root: { 
                        fontWeight: 600,
                        textAlign: 'center',
                        marginBottom: '5px'
                    } 
                }}
            >
                Which output do you prefer?
            </Text>
            <Stack 
                horizontal 
                horizontalAlign="center" 
                wrap 
                tokens={{ childrenGap: 10 }}
                styles={{
                    root: {
                        '@media (max-width: 480px)': {
                            display: 'flex',
                            flexDirection: 'column',
                            alignItems: 'stretch'
                        }
                    }
                }}
            >
                <DefaultButton
                    text="Model A"
                    onClick={() => handlePreferenceClick('A')}
                    styles={getButtonStyles('A')}
                />
                <DefaultButton
                    text="Model B"
                    onClick={() => handlePreferenceClick('B')}
                    styles={getButtonStyles('B')}
                />
                <DefaultButton
                    text="Both are good"
                    onClick={() => handlePreferenceClick('both-good')}
                    styles={getButtonStyles('both-good')}
                />
                <DefaultButton
                    text="Both are bad"
                    onClick={() => handlePreferenceClick('both-bad')}
                    styles={getButtonStyles('both-bad')}
                />
            </Stack>
            <Stack 
                horizontal 
                horizontalAlign="end"
                tokens={{ childrenGap: 10 }}
                styles={{
                    root: {
                        marginTop: '10px',
                        '@media (max-width: 480px)': {
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
                    onClick={handleSubmit}
                    disabled={!selectedPreference}
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
                    onClick={onSkip}
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
    );
}; 