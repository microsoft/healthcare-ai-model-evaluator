import React, { useState } from 'react';
import { Stack, Text, DefaultButton, useTheme } from '@fluentui/react';

export type YesNoOption = 'yes' | 'no' | null;

export interface ModelValidationSelectorProps {
    onPreferenceChange?: (preference: YesNoOption) => void;
    onSubmit?: (response: YesNoOption) => Promise<void>;
    onSkip?: () => void;
}

export const ModelValidationSelector: React.FC<ModelValidationSelectorProps> = ({ 
    onPreferenceChange,
    onSubmit,
    onSkip 
}) => {
    const theme = useTheme();
    const [selectedPreference, setSelectedPreference] = useState<YesNoOption>(null);

    const handlePreferenceClick = (preference: YesNoOption) => {
        setSelectedPreference(preference);
        onPreferenceChange?.(preference);
    };

    const getButtonStyles = (preference: YesNoOption) => ({
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
        <Stack tokens={{ childrenGap: 15 }}>
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
                Is this model output correct?
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
                    text="Yes"
                    onClick={() => handlePreferenceClick('yes')}
                    styles={getButtonStyles('yes')}
                />
                <DefaultButton
                    text="No"
                    onClick={() => handlePreferenceClick('no')}
                    styles={getButtonStyles('no')}
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
                        if (selectedPreference && onSubmit) {
                            setSelectedPreference(null);
                            await onSubmit(selectedPreference);
                        }
                    }}
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