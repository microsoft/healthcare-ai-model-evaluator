import React, { useState, useMemo, useEffect } from 'react';
import {
    Stack,
    DetailsList,
    Selection,
    SelectionMode,
    IColumn,
    MarqueeSelection,
    DetailsListLayoutMode,
    Text as UIText,
    TextField,
    ITextFieldStyles,
    IDetailsColumnProps,
    Dropdown,
    IDropdownOption,
    MessageBar,
    MessageBarType
} from '@fluentui/react';
import { rankingService, ModelRanking as ModelRankingType } from '../../services/rankingService';
import { LoadingOverlay } from '../Arena/LoadingOverlay';
import { EvalMetricFilterOptions, EvalMetricFilterType } from '../../types/admin';

// Remove this interface since we're using the imported type
type ModelRanking = ModelRankingType;

export const Rankings: React.FC = () => {
    const [rankings, setRankings] = useState<ModelRanking[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    //ignore linting error
    // eslint-disable-next-line
    const [selectedItems, setSelectedItems] = useState<ModelRanking[]>([]);
    const [sortKey, setSortKey] = useState<string>('eloScore');
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(true);
    const [nameFilter, setNameFilter] = useState('');
    const [typeFilter, setTypeFilter] = useState('');
    const [selectedMetric, setSelectedMetric] = useState<EvalMetricFilterType>('All');

    useEffect(() => {
        const loadRankings = async () => {
            try {
                setIsLoading(true);
                const data = await rankingService.getRankings();
                setRankings(data);
                setError(null);
            } catch (err) {
                setError('Failed to load rankings');
                console.error('Error loading rankings:', err);
            } finally {
                setIsLoading(false);
            }
        };

        loadRankings();
    }, []);

    // Filter the rankings based on name and type filters
    const filteredRankings = useMemo(() => {
        console.log('filteredRankings:', rankings);
        console.log('selectedMetric:', selectedMetric);
        for (const ranking of rankings) {
            if( ranking.experimentResultsByMetric[selectedMetric] !== undefined) {
                ranking.averageRating = ranking.experimentResultsByMetric[selectedMetric].averageRating;
                ranking.correctScore = ranking.experimentResultsByMetric[selectedMetric].correctScore;
                ranking.validationTime = ranking.experimentResultsByMetric[selectedMetric].validationTime;
                ranking.eloScore = ranking.experimentResultsByMetric[selectedMetric].eloScore;
            }
        }
        var result = rankings.filter(ranking => {
            const nameMatch = ranking.name.toLowerCase().includes(nameFilter.toLowerCase());
            const typeMatch = ranking.type.toLowerCase().includes(typeFilter.toLowerCase());
            const results = ranking.experimentResultsByMetric[selectedMetric];
            return nameMatch && typeMatch && results;
        });
        return result;
    }, [rankings, nameFilter, typeFilter, selectedMetric]);

    const selection = new Selection({
        onSelectionChanged: () => {
            const selected = selection.getSelection() as ModelRanking[];
            setSelectedItems(selected);
        },
    });

    const onColumnClick = (ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
        const newIsSortedDescending = column.key === sortKey ? !isSortedDescending : false;
        setSortKey(column.key);
        setIsSortedDescending(newIsSortedDescending);

        const sortedItems = [...rankings].sort((a, b) => {
            const aValue = a[column.key as keyof ModelRanking];
            const bValue = b[column.key as keyof ModelRanking];

            if (typeof aValue === 'number' && typeof bValue === 'number') {
                return (newIsSortedDescending ? -1 : 1) * (aValue - bValue);
            }

            if (!aValue) return newIsSortedDescending ? 1 : -1;
            if (!bValue) return newIsSortedDescending ? -1 : 1;

            return (newIsSortedDescending ? -1 : 1) * (aValue > bValue ? 1 : -1);
        });

        setRankings(sortedItems);
    };

    // Get unique types for dropdown options
    const typeOptions: IDropdownOption[] = useMemo(() => {
        const uniqueTypes = Array.from(new Set(rankings.map(r => r.type)));
        return [
            { key: '', text: 'All Types' },
            ...uniqueTypes.map(type => ({ key: type, text: type }))
        ];
    }, [rankings]);

    // Add evalMetric options
    const evalMetricOptions: IDropdownOption[] = Object.entries(EvalMetricFilterOptions).map(([key, value]) => ({
        key,
        text: value
    }));

    const columns: IColumn[] = [
        {
            key: 'name',
            name: 'Model Name',
            fieldName: 'name',
            minWidth: 200,
            isResizable: true,
            isSorted: sortKey === 'name',
            isSortedDescending: sortKey === 'name' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            isFiltered: !!nameFilter,
            onRender: (item: ModelRanking) => item.name,
            headerClassName: 'column-header',
            onRenderHeader: (props?: IDetailsColumnProps) => {
                if (!props) return null;
                return (
                    <Stack styles={{ root: { padding: '8px' } }}>
                        <UIText>{props.column.name}</UIText>
                    </Stack>
                );
            }
        },
        {
            key: 'type',
            name: 'Type',
            fieldName: 'type',
            minWidth: 200,
            isResizable: true,
            isSorted: sortKey === 'type',
            isSortedDescending: sortKey === 'type' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            isFiltered: !!typeFilter,
            onRender: (item: ModelRanking) => item.type,
            headerClassName: 'column-header',
            onRenderHeader: (props?: IDetailsColumnProps) => {
                if (!props) return null;
                return (
                    <Stack styles={{ root: { padding: '8px' } }}>
                        <UIText>{props.column.name}</UIText>
                    </Stack>
                );
            }
        },
        {
            key: 'eloScore',
            name: 'ELO Score',
            fieldName: 'eloScore',
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'eloScore',
            isSortedDescending: sortKey === 'eloScore' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: ModelRanking) => {
                const results = item.experimentResultsByMetric[selectedMetric] || item.experimentResultsByMetric['All'];
                return results?.eloScore.toFixed(0) || '-';
            }
        },
        {
            key: 'averageRating',
            name: 'Avg Rating',
            fieldName: 'averageRating',
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'averageRating',
            isSortedDescending: sortKey === 'averageRating' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: ModelRanking) => item.averageRating.toFixed(1)
        },
        {
            key: 'correctScore',
            name: 'Correct Score',
            fieldName: 'correctScore',
            minWidth: 100,
            isResizable: true,
            isSorted: sortKey === 'correctScore',
            isSortedDescending: sortKey === 'correctScore' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: ModelRanking) => `${(item.correctScore).toFixed(1)}%`
        },
        {
            key: 'validationTime',
            name: 'Avg Validation Time',
            fieldName: 'validationTime',
            minWidth: 130,
            isResizable: true,
            isSorted: sortKey === 'validationTime',
            isSortedDescending: sortKey === 'validationTime' ? isSortedDescending : undefined,
            onColumnClick: onColumnClick,
            onRender: (item: ModelRanking) => `${item.validationTime.toFixed(1)}s`
        },
    ];

    // Add some CSS to ensure the header tooltips are visible
    const detailsListStyles = {
        root: {
            '.ms-DetailsHeader': {
                paddingTop: 0,
            },
            '.column-header': {
                paddingBottom: '32px', // Make room for the filter
            }
        }
    };

    const filterBarStyles = {
        root: {
            backgroundColor: '#f8f8f8',
            padding: '10px',
            borderRadius: '2px',
            marginBottom: '10px'
        }
    };

    const textFieldStyles: Partial<ITextFieldStyles> = {
        root: {
            width: 200,
            marginRight: 10
        },
        fieldGroup: {
            height: 32
        }
    };

    const dropdownStyles = {
        root: {
            width: 200
        },
        title: {
            height: 32
        }
    };

    return (
        <Stack tokens={{ childrenGap: 20 }} styles={{ root: { padding: 20, position: 'relative' } }}>
            {isLoading && <LoadingOverlay />}
            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}
            
            <Stack horizontal horizontalAlign="space-between">
                <UIText variant="xxLarge">Model Rankings</UIText>
            </Stack>

            <Stack 
                horizontal 
                verticalAlign="center" 
                styles={filterBarStyles}
                tokens={{ childrenGap: 10 }}
            >
                <UIText variant="medium" styles={{ root: { marginRight: 10 } }}>Filters:</UIText>
                <TextField
                    placeholder="Filter by name"
                    value={nameFilter}
                    onChange={(_, newValue) => setNameFilter(newValue || '')}
                    styles={textFieldStyles}
                />
                <Dropdown
                    placeholder="Filter by type"
                    selectedKey={typeFilter}
                    options={typeOptions}
                    onChange={(_, option) => setTypeFilter(option?.key as string || '')}
                    styles={dropdownStyles}
                />
                <Dropdown
                    placeholder="Filter by metric"
                    selectedKey={selectedMetric}
                    options={evalMetricOptions}
                    onChange={(_, option) => option && setSelectedMetric(option.key as EvalMetricFilterType)}
                    styles={dropdownStyles}
                />
            </Stack>

            <MarqueeSelection selection={selection}>
                <DetailsList
                    items={filteredRankings}
                    columns={columns}
                    selection={selection}
                    selectionMode={SelectionMode.single}
                    setKey="set"
                    layoutMode={DetailsListLayoutMode.justified}
                    selectionPreservedOnEmptyClick={true}
                    styles={detailsListStyles}
                />
            </MarqueeSelection>
        </Stack>
    );
};

export {};