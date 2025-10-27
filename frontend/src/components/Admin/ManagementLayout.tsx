import React from 'react';
import {
    Stack,
    DetailsList,
    IColumn,
    CommandBar,
    ICommandBarItemProps,
    Spinner,
    Text,
    MessageBar,
    MessageBarType,
    SelectionMode
} from '@fluentui/react';

interface ManagementLayoutProps {
    title: string;
    items: any[];
    columns: IColumn[];
    commandItems: ICommandBarItemProps[];
    isLoading: boolean;
    error?: string | null;
    children?: React.ReactNode;
}

export const ManagementLayout: React.FC<ManagementLayoutProps> = ({
    title,
    items,
    columns,
    commandItems,
    isLoading,
    error,
    children
}) => {
    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">{title}</Text>
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}

            <CommandBar items={commandItems} />
            
            <Stack styles={{ root: { position: 'relative' } }}>
                <DetailsList
                    items={isLoading ? [] : items}
                    columns={columns}
                    selectionMode={SelectionMode.single}
                    isHeaderVisible={true}
                />
                {isLoading && (
                    <Stack 
                        horizontalAlign="center" 
                        verticalAlign="center" 
                        styles={{
                            root: {
                                position: 'absolute',
                                top: 0,
                                left: 0,
                                right: 0,
                                bottom: 0,
                                background: 'rgba(255, 255, 255, 0.7)'
                            }
                        }}
                    >
                        <Spinner label="Loading..." />
                    </Stack>
                )}
            </Stack>

            {children}
        </Stack>
    );
}; 