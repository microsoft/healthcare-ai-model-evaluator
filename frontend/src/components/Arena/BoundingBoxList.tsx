import React from 'react';
import {
    Stack,
    Text,
    TextField,
    IStackTokens,
    DefaultButton,
    useTheme,
    Dropdown,
    IDropdownOption
} from '@fluentui/react';
import { ITrial } from '../../types/admin';
import { BoundingBox } from '../../types/admin';

interface BoundingBoxListProps {
    boundingBoxes: BoundingBox[];
    onDelete: (id: string) => void;
    trial?: ITrial;
    onAnnotationChange: (id: string, annotation: string) => void;
    selectedBoxId?: string;
    onSelectBox?: (id: string) => void;
    modelOptions?: { id: string, name: string }[];
    onModelSelect?: (boxId: string, modelId: string) => void;
    selectedModelId?: string;
    title?: string;
    allowEditing?: boolean;
}

const stackTokens: IStackTokens = {
    childrenGap: 10,
    padding: 10
};

export const BoundingBoxList: React.FC<BoundingBoxListProps> = ({
    boundingBoxes,
    onDelete,
    trial,
    onAnnotationChange,
    selectedBoxId,
    onSelectBox,
    allowEditing = true,
    modelOptions,
    onModelSelect,
    selectedModelId,
    title = "Bounding Boxes"
}) => {
    const theme = useTheme();
    if (boundingBoxes.length === 0) {
        return null;
    }
    const dropdownOptions: IDropdownOption[] = modelOptions?.map(model => ({
        key: model.id,
        text: model.name
    })) || [];

    return (
        <Stack tokens={stackTokens}>
            <Text variant="large" styles={{ root: { fontWeight: 600 } }}>
                {title} ({boundingBoxes.length})
            </Text>
            <Stack tokens={{ childrenGap: 8 }}>
                {boundingBoxes.map((box, index) => (
                    <Stack
                        key={box.id}
                        horizontal
                        verticalAlign="center"
                        tokens={{ childrenGap: 8 }}
                        styles={{
                            root: {
                                padding: 8,
                                backgroundColor: box.id === selectedBoxId 
                                    ? theme.palette.neutralQuaternaryAlt 
                                    : theme.palette.neutralLighter,
                                borderRadius: theme.effects.roundedCorner2,
                                borderLeft: box.id === selectedBoxId 
                                    ? `4px solid ${theme.palette.themePrimary}`
                                    : undefined
                            }
                        }}
                    >
                        <Text styles={{ root: { minWidth: 30 } }}>{index + 1}.</Text>
                        <Stack.Item grow>
                            <Stack tokens={{ childrenGap: 4 }}>
                                {modelOptions && (
                                    <Dropdown
                                        selectedKey={box.modelId}
                                        options={dropdownOptions}
                                        onChange={(_, option) => option && onModelSelect?.(box.id, option.key as string)}
                                        styles={{
                                            root: { minWidth: 120 },
                                            title: { borderColor: theme.palette.neutralTertiary }
                                        }}
                                    />
                                )}
                                <TextField
                                    readOnly={allowEditing !== true}
                                    placeholder="Add annotation..."
                                    value={box.annotation || ''}
                                    onChange={(_, newValue) => onAnnotationChange(box.id, newValue || '')}
                                    onFocus={() => onSelectBox?.(box.id)}
                                    styles={{
                                        root: { flex: 1 },
                                        field: { backgroundColor: theme.palette.white }
                                    }}
                                />
                            </Stack>
                        </Stack.Item>
                        <DefaultButton
                            disabled={allowEditing !== true}
                            iconProps={{ iconName: 'Delete' }}
                            onClick={() => onDelete(box.id)}
                            styles={{
                                root: { minWidth: 32, padding: '0 4px' }
                            }}
                        />
                    </Stack>
                ))}
            </Stack>
        </Stack>
    );
}; 