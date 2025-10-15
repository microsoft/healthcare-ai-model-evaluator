import React, { useState } from 'react';
import { Stack, Text, DefaultButton, useTheme } from '@fluentui/react';

export type PreferenceOption = 'A' | 'B' | 'both-good' | 'both-bad' | null | '1' | '2' | '3' | '4' | '5';

export interface ModelRatingSelectorProps {
    onPreferenceChange?: (preference: PreferenceOption) => void;
    onSubmit?: (rating: string) => void;
    onSkip?: () => void;
}

export const ModelRatingSelector: React.FC<ModelRatingSelectorProps> = ({ 
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

    const getButtonStyles = (preference: PreferenceOption) => ({
        root: {
            minWidth: '120px',
            margin: '2px 5px',
            backgroundColor: selectedPreference === preference ? 'rgb(0, 120, 212)' : undefined,
            color: selectedPreference === preference ? 'white' : undefined,
            ':hover': {
                backgroundColor: selectedPreference === preference ? '#106EBE' : undefined,
                color: selectedPreference === preference ? 'white' : undefined,
            }
        }
    });

    return (
        <Stack tokens={{ childrenGap: 15 }}>
            <Text variant="large" block styles={{ root: { fontWeight: 600, textAlign: 'center' } }}>
                How would you rate the model output?
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
                    text="1 (Unusable)"
                    onClick={() => handlePreferenceClick('1')}
                    styles={getButtonStyles('1')}
                />
                <DefaultButton
                    text="2 (Poor)"
                    onClick={() => handlePreferenceClick('2')}
                    styles={getButtonStyles('2')}
                />
                <DefaultButton
                    text="3 (Good)"
                    onClick={() => handlePreferenceClick('3')}
                    styles={getButtonStyles('3')}
                />
                <DefaultButton
                    text="4 (Excellent)"
                    onClick={() => handlePreferenceClick('4')}
                    styles={getButtonStyles('4')}
                />
                <DefaultButton
                    text="5 (Perfect)"
                    onClick={() => handlePreferenceClick('5')}
                    styles={getButtonStyles('5')}
                />
            </Stack>
            <Stack 
                horizontal 
                horizontalAlign="end"
                tokens={{ childrenGap: 10 }}
                styles={{
                    root: {
                        marginTop: '10px',
                        height: '60px',
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
                    onClick={() => {setSelectedPreference(null); onSubmit?.(selectedPreference || '');}}
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