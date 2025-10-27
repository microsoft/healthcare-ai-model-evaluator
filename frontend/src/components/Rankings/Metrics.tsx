import React, { useState, useEffect, useMemo } from 'react';
import { 
    Stack, 
    Text, 
    Pivot, 
    PivotItem, 
    SearchBox, 
    Checkbox, 
    DetailsList, 
    SelectionMode, 
    Selection, 
    IColumn,
    Dropdown,
    IDropdownOption,
    useTheme,
    ScrollablePane,
    Sticky,
    StickyPositionType,
    StackItem,
    SelectionZone
} from '@fluentui/react';
import { useDispatch, useSelector } from 'react-redux';
import { RootState } from '../../store/store';
import { fetchTasks } from '../../reducers/clinicalTaskReducer';
import { fetchModels } from '../../reducers/modelReducer';
import { EvalMetricFilterOptions, EvalMetricFilterType, AIModelType, IModel } from '../../types/admin';
import { fetchDataSets } from '../../reducers/dataReducer';
import {  CartesianGrid, XAxis, YAxis, Tooltip, Legend, ScatterChart, Scatter } from 'recharts';

export const Metrics: React.FC = () => {
    const theme = useTheme();
    const dispatch = useDispatch();
    
    // State for active tab
    const [activeTab, setActiveTab] = useState<'clinicalTasks' | 'models'>('clinicalTasks');
    
    // State for filters
    const [evalMetricFilter, setEvalMetricFilter] = useState<EvalMetricFilterType>('All');
    const [modelTypeFilter, setModelTypeFilter] = useState<string>('All');
    const [nameFilter, setNameFilter] = useState<string>('');
    // Get data from Redux store
    const { tasks, isLoading: tasksLoading } = useSelector((state: RootState) => state.clinicalTasks);
    const { models, isLoading: modelsLoading } = useSelector((state: RootState) => state.models);
    const [ modelsWithResults, setModelsWithResults ] = useState<IModel[]>([]);
    const { datasets } = useSelector((state: RootState) => state.data);
    
    // State for selections
    const [selectedTasks, setSelectedTasks] = useState<Set<string>>(new Set());
    const [selectedModels, setSelectedModels] = useState<Set<string>>(new Set());
    
     // Set up selections for DetailsList
     const taskSelection = useMemo(() => new Selection({
        onSelectionChanged: () => {
            const selectedItems = taskSelection.getSelection().map(item => (item as any).id);
            setSelectedTasks(new Set(selectedItems));
        },
        getKey: (item) => (item as any).id
    }), []); // Empty dependency array since we want this to be created once

    const modelSelection = useMemo(() => new Selection({
        onSelectionChanged: () => {
            const selectedItems = modelSelection.getSelection().map(item => (item as any).id);
            setSelectedModels(new Set(selectedItems));
        },
        getKey: (item) => (item as any).id
    }), []); // Empty dependency array since we want this to be created once

    useEffect(() => {
        const modelsWithResults = models;
        setModelsWithResults(modelsWithResults);
    }, [models, tasks]);
    
    // Fetch data on component mount
    useEffect(() => {
        dispatch(fetchTasks() as any);
        dispatch(fetchModels() as any);
        dispatch(fetchDataSets() as any);
        
    }, [dispatch]);
    
    // Filter tasks based on filters
    const filteredTasks = tasks.filter(task => {
        const nameMatch = task.name.toLowerCase().includes(nameFilter.toLowerCase());
        const metricMatch = evalMetricFilter === 'All' || task.evalMetric === evalMetricFilter;
        return nameMatch && metricMatch;
    });
    
    // Filter models based on filters
    const filteredModels = modelsWithResults.filter(model => {
        const nameMatch = model.name.toLowerCase().includes(nameFilter.toLowerCase());
        const typeMatch = modelTypeFilter === 'All' || model.modelType === modelTypeFilter;
        return nameMatch && typeMatch;
    });
    
    // Eval Metric filter options
    const evalMetricOptions: IDropdownOption[] = Object.entries(EvalMetricFilterOptions).map(([key, value]) => ({
        key,
        text: value
    }));
    
    // Model Type filter options
    const modelTypeOptions: IDropdownOption[] = [
        { key: 'All', text: 'All Types' },
        { key: AIModelType.TEXT_TO_TEXT, text: 'Text to Text' },
        { key: AIModelType.IMAGE_TO_TEXT, text: 'Image to Text' },
        { key: AIModelType.IMAGE_TO_IMAGE, text: 'Image to Image' },
        { key: AIModelType.MULTIMODAL, text: 'Multimodal' }
    ];
    
    // Column definitions for tasks
    const taskColumns: IColumn[] = [
        {
            key: 'name',
            name: 'Name',
            fieldName: 'name',
            minWidth: 100,
            isResizable: true
        }
    ];
    
    // Column definitions for models
    const modelColumns: IColumn[] = [
        {
            key: 'name',
            name: 'Name',
            fieldName: 'name',
            minWidth: 100,
            isResizable: true
        },
        {
            key: 'modelType',
            name: 'Type',
            fieldName: 'modelType',
            minWidth: 100,
            isResizable: true
        }
    ];
    
    // Toggle all tasks selection
    const toggleAllTasks = (checked: boolean) => {
        if (checked) {
            const allIds = filteredTasks.map(task => task.id);
            setSelectedTasks(new Set(allIds));
            // Clear and re-select all items
            taskSelection.setAllSelected(false);
            filteredTasks.forEach((task, index) => {
                taskSelection.setIndexSelected(index, true, false);
            });
        } else {
            setSelectedTasks(new Set());
            taskSelection.setAllSelected(false);
        }
    };
    
    // Toggle all models selection
    const toggleAllModels = (checked: boolean) => {
        if (checked) {
            const allIds = filteredModels.map(model => model.id);
            setSelectedModels(new Set(allIds));
            // Clear and re-select all items
            modelSelection.setAllSelected(false);
            filteredModels.forEach((model, index) => {
                modelSelection.setIndexSelected(index, true, false);
            });
        } else {
            setSelectedModels(new Set());
            modelSelection.setAllSelected(false);
        }
    };
    // Check if all items are selected
    const areAllTasksSelected = selectedTasks.size > 0 && selectedTasks.size === filteredTasks.length;
    const areAllModelsSelected = selectedModels.size > 0 && selectedModels.size === filteredModels.length;
    
    // Right panel state
    const [activeRightTab, setActiveRightTab] = useState<'performance' | 'cost' | 'costPerformance'>('performance');
    const [performanceMetric, setPerformanceMetric] = useState<string>('eloScore');
    const [selectedColumns, setSelectedColumns] = useState<Set<string>>(new Set([
        'rank', 'modelName', 'taskName', 'eloScore', 'bertScore'
    ]));
    // Get all available columns from performance data
    const getAvailableColumns = () => {
        const data = getPerformanceData();
        const allKeys = new Set<string>();
        data.forEach(item => {
            Object.keys(item).forEach(key => allKeys.add(key));
        });
        return Array.from(allKeys).map(key => ({
            key,
            text: key.charAt(0).toUpperCase() + key.slice(1).replace(/([A-Z])/g, ' $1')
        }));
    };

    const getAvailablePerformanceMetrics = () => {
        const data = getPerformanceData();
        const allKeys = new Set<string>();
        data.forEach(item => {
            Object.keys(item).forEach(key => allKeys.add(key));
        });
        return Array.from(allKeys).map(key => ({
            key,
            text: key.charAt(0).toUpperCase() + key.slice(1).replace(/([A-Z])/g, ' $1')
        }));
    }
    // Update performance columns based on selection
    const [performanceColumns, setPerformanceColumns] = useState<IColumn[]>([
        {
            key: 'rank',
            name: 'Rank',
            fieldName: 'rank',
            minWidth: 50,
            maxWidth: 50,
            isResizable: false
        },
        {
            key: 'modelName',
            name: 'Model',
            fieldName: 'modelName',
            minWidth: 100,
            isResizable: true
        },
        {
            key: 'taskName',
            name: 'Clinical Task',
            fieldName: 'taskName',
            minWidth: 150,
            isResizable: true
        }
    ]);

    useEffect(() => {
        const baseColumns = [
            {
                key: 'rank',
                name: 'Rank',
                fieldName: 'rank',
                minWidth: 50,
                maxWidth: 50,
                isResizable: false
            },
            {
                key: 'modelName',
                name: 'Model',
                fieldName: 'modelName',
                minWidth: 100,
                isResizable: true
            },
            {
                key: 'taskName',
                name: 'Clinical Task',
                fieldName: 'taskName',
                minWidth: 150,
                isResizable: true
            }
        ];

        const metricColumns = Array.from(selectedColumns)
            .filter(key => !['rank', 'modelName', 'taskName'].includes(key))
            .map(key => ({
                key,
                name: key.charAt(0).toUpperCase() + key.slice(1).replace(/([A-Z])/g, ' $1'),
                fieldName: key,
                minWidth: 80,
                isResizable: true,
                onRender: (item: any) => typeof item[key] === 'number' ? item[key].toFixed(2) : item[key]
            }));

        setPerformanceColumns([...baseColumns, ...metricColumns]);
    }, [selectedColumns]);


    // Create data structures for panels
    const getPerformanceData = () => {
        const performanceData: any[] = [];
        
        if (activeTab === 'clinicalTasks') {
            // Get data for selected clinical tasks
            const selectedTasksList = tasks.filter(task => selectedTasks.has(task.id));
            
            selectedTasksList.forEach(task => {
                task.dataSetModels.forEach(dsModel => {
                    // Skip ground truth models
                    if (dsModel.isGroundTruth) return;
                    
                    const model = models.find(m => m.id === dsModel.modelId);
                    if (!model) return;
                    
                    // Get model results and metrics
                    const modelResult = task.modelResults?.[model.id] || {};
                    const metrics = task.metrics?.[model.id] || {};
                    var perfData: any = {
                        modelId: model.id,
                        modelName: model.name,
                        taskId: task.id,
                        taskName: task.name,
                        eloScore: modelResult.eloScore || 0,
                        averageRating: modelResult.averageRating || 0,
                        correctScore: modelResult.correctScore || 0,
                        bertScore: metrics.bert_score || 0,
                        // Add other metrics as needed
                    }
                    Object.keys(metrics).forEach(key => {
                        perfData[key] = metrics[key];
                    });
                    if( modelResult.singleEvaluationScores) {
                        Object.keys(modelResult.singleEvaluationScores).forEach(key => {
                            perfData[key] = modelResult.singleEvaluationScores[key];
                        });
                    }

                    performanceData.push(perfData);
                });
            });
        } else {
            // Get data for selected models
            const selectedModelsList = models.filter(model => selectedModels.has(model.id));
            
            tasks.forEach(task => {
                task.dataSetModels.forEach(dsModel => {
                    // Skip ground truth models
                    if (dsModel.isGroundTruth) return;
                    
                    // Check if this model is selected
                    if (!selectedModelsList.some(m => m.id === dsModel.modelId)) return;
                    
                    const model = models.find(m => m.id === dsModel.modelId);
                    if (!model) return;
                    
                    // Get model results and metrics
                    const modelResult = task.modelResults?.[model.id] || {};
                    const metrics = task.metrics?.[model.id] || {};
                    var perfData: any = {   
                        modelId: model.id,
                        modelName: model.name,
                        taskId: task.id,
                        taskName: task.name,
                        eloScore: modelResult.eloScore || 0,
                        averageRating: modelResult.averageRating || 0,
                        correctScore: modelResult.correctScore || 0,
                        bertScore: metrics.bert_score || 0
                        // Add other metrics as needed
                    }
                    Object.keys(metrics).forEach(key => {
                        perfData[key] = metrics[key];
                    });
                    if( modelResult.singleEvaluationScores) {
                        Object.keys(modelResult.singleEvaluationScores).forEach(key => {
                            perfData[key] = modelResult.singleEvaluationScores[key];
                        });
                    }
                    performanceData.push(perfData);
                });
            });
        }
        
        return performanceData;
    };

    const getCostData = () => {
        const costData: any[] = [];
        
        if (activeTab === 'clinicalTasks') {
            // Get data for selected clinical tasks
            const selectedTasksList = tasks.filter(task => selectedTasks.has(task.id));
            
            selectedTasksList.forEach(task => {
                task.dataSetModels.forEach(dsModel => {
                    // Skip ground truth models
                    if (dsModel.isGroundTruth) return;
                    
                    const model = models.find(m => m.id === dsModel.modelId);
                    if (!model) return;
                    
                    const dataset = datasets.find(ds => ds.id === dsModel.dataSetId);
                    if (!dataset) return;
                    
                    // Calculate token averages and costs
                    const dataObjectCount = dataset.dataObjectCount || 1; // Avoid division by zero
                    const inputTokensAvg = dataset.totalInputTokens / dataObjectCount;
                    
                    // Get output tokens using either modelOutputIndex or generatedOutputKey
                    let outputTokens = 0;
                    if (dsModel.modelOutputIndex !== -1) {
                        outputTokens = dataset.totalOutputTokensPerIndex?.[dsModel.modelOutputIndex.toString()] || 0;
                    } else if (dsModel.generatedOutputKey) {
                        outputTokens = dataset.totalOutputTokensPerIndex?.[dsModel.generatedOutputKey] || 0;
                    }
                    
                    const outputTokensAvg = outputTokens / dataObjectCount;
                    
                    // Calculate cost
                    const cost = (dataset.totalInputTokens * (model.costPerToken || 0)) + 
                                 (outputTokens * (model.costPerTokenOut || 0));
                    
                    costData.push({
                        modelId: model.id,
                        modelName: model.name,
                        taskId: task.id,
                        taskName: task.name,
                        inputTokensAvg,
                        outputTokensAvg,
                        cost
                    });
                });
            });
        } else {
            // Get data for selected models
            const selectedModelsList = modelsWithResults.filter(model => selectedModels.has(model.id));
            
            tasks.forEach(task => {
                task.dataSetModels.forEach(dsModel => {
                    // Skip ground truth models
                    if (dsModel.isGroundTruth) return;
                    
                    // Check if this model is selected
                    if (!selectedModelsList.some(m => m.id === dsModel.modelId)) return;
                    
                    const model = modelsWithResults.find(m => m.id === dsModel.modelId);
                    if (!model) return;
                    
                    const dataset = datasets.find(ds => ds.id === dsModel.dataSetId);
                    if (!dataset) return;
                    
                    // Calculate token averages and costs
                    const dataObjectCount = dataset.dataObjectCount || 1; // Avoid division by zero
                    const inputTokensAvg = dataset.totalInputTokens / dataObjectCount;
                    
                    // Get output tokens using either modelOutputIndex or generatedOutputKey
                    let outputTokens = 0;
                    if (dsModel.modelOutputIndex !== -1) {
                        outputTokens = dataset.totalOutputTokensPerIndex?.[dsModel.modelOutputIndex.toString()] || 0;
                    } else if (dsModel.generatedOutputKey) {
                        outputTokens = dataset.totalOutputTokensPerIndex?.[dsModel.generatedOutputKey] || 0;
                    }
                    
                    const outputTokensAvg = outputTokens / dataObjectCount;
                    
                    // Calculate cost
                    const cost = (dataset.totalInputTokens * (model.costPerToken || 0)) + 
                                 (outputTokens * (model.costPerTokenOut || 0));
                    
                    costData.push({
                        modelId: model.id,
                        modelName: model.name,
                        taskId: task.id,
                        taskName: task.name,
                        inputTokensAvg,
                        outputTokensAvg,
                        cost
                    });
                });
            });
        }
        
        return costData;
    };

    // Get combined data for the cost vs performance chart
    const getCostVsPerformanceData = () => {
        const performanceData = getPerformanceData();
        const filteredPerformanceData = performanceData.filter(perfItem => {
            if( perfItem[performanceMetric] === undefined) {
                return false;
            }
            return true;
        });
        const costData = getCostData();
        // Combine the data using modelId and taskId as keys
        return filteredPerformanceData.map(perfItem => {
            const costItem = costData.find(
                cost => cost.modelId === perfItem.modelId && cost.taskId === perfItem.taskId
            );
            
            return {
                ...perfItem,
                ...costItem
            };
        });
    };


    // Define columns for cost table
    const costColumns: IColumn[] = [
        {
            key: 'rank',
            name: 'Rank',
            fieldName: 'rank',
            minWidth: 50,
            maxWidth: 50,
            isResizable: false
        },
        {
            key: 'modelName',
            name: 'Model',
            fieldName: 'modelName',
            minWidth: 100,
            isResizable: true
        },
        {
            key: 'taskName',
            name: 'Clinical Task',
            fieldName: 'taskName',
            minWidth: 150,
            isResizable: true
        },
        {
            key: 'inputTokensAvg',
            name: 'Input Tokens Avg',
            fieldName: 'inputTokensAvg',
            minWidth: 120,
            isResizable: true,
            onRender: (item) => Math.round(item.inputTokensAvg).toLocaleString()
        },
        {
            key: 'outputTokensAvg',
            name: 'Output Tokens Avg',
            fieldName: 'outputTokensAvg',
            minWidth: 120,
            isResizable: true,
            onRender: (item) => Math.round(item.outputTokensAvg).toLocaleString()
        },
        {
            key: 'cost',
            name: 'Cost ($)',
            fieldName: 'cost',
            minWidth: 80,
            isResizable: true,
            isSorted: true,
            isSortedDescending: false,
            onRender: (item) => item.cost.toFixed(2)
        }
    ];


    // Generate colors and shapes for models and tasks
    const getColor = (index: number) => {
        const colors = ['#0078d4', '#107c10', '#d83b01', '#5c2d91', '#008272', '#e3008c', '#00188f'];
        return colors[index % colors.length];
    };

    const getShape = (index: number) => {
        // These are the shapes we'll use in the legend for different tasks
        const shapes = ['circle', 'square', 'triangle', 'diamond', 'star'];
        return shapes[index % shapes.length];
    };

    // Memoize filtered lists to prevent selection reset on re-renders
    const memoizedFilteredTasks = useMemo(() => filteredTasks, [filteredTasks]);
    const memoizedFilteredModels = useMemo(() => filteredModels, [filteredModels]);

    // Add click handler to prevent selection resets
    const preventSelectionReset = (e: React.MouseEvent) => {
        // Stop event propagation to prevent selection reset
        e.stopPropagation();
    };

    return (
        <Stack className="metrics-container" horizontal styles={{ root: { height: '100%', padding: 20 } }} onClick={preventSelectionReset}>
            {/* Left Panel */}
            <Stack.Item styles={{
                root: {
                    width: 300,
                    borderRight: `1px solid ${theme.palette.neutralLight}`,
                    paddingRight: 20
                }
            }}>
                <Pivot 
                    selectedKey={activeTab} 
                    onLinkClick={(item?: PivotItem) => {
                        if (item) setActiveTab(item.props.itemKey as 'clinicalTasks' | 'models');
                    }}
                >
                    <PivotItem headerText="Clinical Tasks" itemKey="clinicalTasks" />
                    <PivotItem headerText="Models" itemKey="models" />
                </Pivot>
                
                <Stack tokens={{ childrenGap: 10, padding: '10px 0' }}>
                   
                        <Stack style={{ display: activeTab !== 'clinicalTasks' ? 'none' : 'block' }} tokens={{ childrenGap: 10 }}>
                            <Dropdown
                                label="Use Case"
                                selectedKey={evalMetricFilter}
                                onChange={(_, option) => {
                                    if (option) setEvalMetricFilter(option.key as EvalMetricFilterType);
                                }}
                                options={evalMetricOptions}
                                styles={{ dropdown: { width: '100%' } }}
                            />
                            
                            <SearchBox 
                                placeholder="Filter by name"
                                onChange={(_, newValue) => setNameFilter(newValue || '')}
                                value={nameFilter}
                                styles={{ root: { marginBottom: 10 } }}
                            />
                            
                            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                <Checkbox 
                                    label="Select All"
                                    checked={areAllTasksSelected}
                                    onChange={(_, checked) => toggleAllTasks(checked || false)}
                                />
                                <Text>{`${selectedTasks.size} selected`}</Text>
                            </Stack>
                            
                            {tasksLoading ? (
                                <Text>Loading clinical tasks...</Text>
                            ) : (
                                <div className="task-selection-container">
                                    <SelectionZone selection={taskSelection}>
                                        <DetailsList
                                            items={memoizedFilteredTasks}
                                            columns={taskColumns}
                                            selectionMode={SelectionMode.multiple}
                                            selection={taskSelection}
                                            setKey="tasks"
                                            checkboxVisibility={1} // Always visible
                                            selectionPreservedOnEmptyClick={true}
                                        />
                                    </SelectionZone>
                                </div>
                            )}
                        </Stack>
                   
                        <Stack style={{ display: activeTab !== 'models' ? 'none' : 'block' }} tokens={{ childrenGap: 10 }}>
                            <Dropdown
                                label="Model Type"
                                selectedKey={modelTypeFilter}
                                onChange={(_, option) => {
                                    if (option) setModelTypeFilter(option.key as string);
                                }}
                                options={modelTypeOptions}
                                styles={{ dropdown: { width: '100%' } }}
                            />
                            
                            <SearchBox 
                                placeholder="Filter by name"
                                onChange={(_, newValue) => setNameFilter(newValue || '')}
                                value={nameFilter}
                                styles={{ root: { marginBottom: 10 } }}
                            />
                            
                            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                                <Checkbox 
                                    label="Select All"
                                    checked={areAllModelsSelected}
                                    onChange={(_, checked) => toggleAllModels(checked || false)}
                                />
                                <Text>{`${selectedModels.size} selected`}</Text>
                            </Stack>
                            
                            {modelsLoading ? (
                                <Text>Loading models...</Text>
                            ) : (
                                <div className="model-selection-container">
                                    <SelectionZone selection={modelSelection}>
                                        <DetailsList
                                            items={memoizedFilteredModels}
                                            columns={modelColumns}
                                            selectionMode={SelectionMode.multiple}
                                            selection={modelSelection}
                                            setKey="models"
                                            checkboxVisibility={1} // Always visible
                                            selectionPreservedOnEmptyClick={true}
                                        />
                                    </SelectionZone>
                                </div>
                            )}
                        </Stack>
                </Stack>
            </Stack.Item>
            
            
            <Stack.Item grow styles={{
                root: {
                    padding: '0 20px',
                    maxHeight: '100%',
                    overflow: 'hidden'
                }
            }}>
                <Text variant="xLarge" block>Metrics Dashboard</Text>
                
                {((activeTab === 'clinicalTasks' && selectedTasks.size > 0) || 
                  (activeTab === 'models' && selectedModels.size > 0)) ? (
                    <Stack styles={{ root: { height: 'calc(100% - 40px)' }}} tokens={{ childrenGap: 15 }}>
                        <Pivot 
                            selectedKey={activeRightTab}
                            onLinkClick={(item) => {
                                if (item) setActiveRightTab(item.props.itemKey as 'performance' | 'cost' | 'costPerformance');
                            }}
                            styles={{ root: { borderBottom: `1px solid ${theme.palette.neutralLight}` } }}
                        >
                            <PivotItem headerText="Performance" itemKey="performance" />
                            <PivotItem headerText="Cost" itemKey="cost" />
                            <PivotItem headerText="Cost vs Performance" itemKey="costPerformance" />
                        </Pivot>
                        
                        <Stack.Item grow styles={{ root: { position: 'relative', height: '100%' } }}>
                            {activeRightTab === 'performance' && (
                                <Stack tokens={{ childrenGap: 10 }}>
                                    <StackItem>
                                    <Dropdown
                                        label="Select Columns"
                                        placeholder="Select columns to display"
                                        multiSelect
                                        selectedKeys={Array.from(selectedColumns)}
                                        onChange={(_, item?: IDropdownOption) => {
                                            if (item) {
                                                const newSelected = new Set(selectedColumns);
                                                if (item.selected) {
                                                    newSelected.add(item.key as string);
                                                } else {
                                                    newSelected.delete(item.key as string);
                                                }
                                                setSelectedColumns(newSelected);
                                            }
                                        }}
                                        options={getAvailableColumns()}
                                        styles={{ root: { marginBottom: 190 }, dropdown: { width: 300 } }}
                                    />
                                    <div style={{ height: 190 }}></div>
                                    </StackItem>
                                    <StackItem>
                                    <ScrollablePane style={{ marginTop: 90 }}>
                                            <DetailsList
                                                items={getPerformanceData()
                                                    .sort((a, b) => b.eloScore - a.eloScore)
                                                    .map((item, index) => ({ ...item, rank: index + 1 }))}
                                                columns={performanceColumns}
                                                selectionMode={SelectionMode.none}
                                                setKey="performance"
                                            />
                                    </ScrollablePane>
                                    </StackItem>
                                </Stack>
                            )}
                            
                            {activeRightTab === 'cost' && (
                                <ScrollablePane>
                                    <Sticky stickyPosition={StickyPositionType.Header}>
                                        <DetailsList
                                            items={getCostData()
                                                .sort((a, b) => a.cost - b.cost)
                                                .map((item, index) => ({ ...item, rank: index + 1 }))}
                                            columns={costColumns}
                                            selectionMode={SelectionMode.none}
                                            setKey="cost"
                                        />
                                    </Sticky>
                                </ScrollablePane>
                            )}
                            
                            {activeRightTab === 'costPerformance' && (
                                <Stack tokens={{ childrenGap: 20 }}>
                                    <Dropdown
                                        label="Select Performance Metric"
                                        selectedKey={performanceMetric}
                                        onChange={(_, option) => option && setPerformanceMetric(option.key as string)}
                                        options={getAvailablePerformanceMetrics() as IDropdownOption[]}
                                        styles={{ dropdown: { width: 200 } }}
                                    />
                                    
                                    <div style={{ height: 500 }}>
                                        <ScatterChart
                                            width={800}
                                            height={500}
                                            margin={{ top: 20, right: 20, bottom: 80, left: 30 }}
                                        >
                                            <CartesianGrid strokeDasharray="3 3" />
                                            <XAxis 
                                                type="number" 
                                                dataKey="cost" 
                                                name="Cost" 
                                                label={{ value: 'Cost ($)', position: 'bottom', offset: 10 }} 
                                            />
                                            <YAxis 
                                                type="number" 
                                                dataKey={performanceMetric} 
                                                name={getAvailablePerformanceMetrics().find(opt => opt.key === performanceMetric)?.text || ''}
                                                label={{ 
                                                    value: getAvailablePerformanceMetrics().find(opt => opt.key === performanceMetric)?.text || '', 
                                                    angle: -90, 
                                                    position: 'left' 
                                                }} 
                                            />
                                            <Tooltip 
                                                formatter={(value: any, name: string) => {
                                                    if (name === 'Cost') return [`$${parseFloat(value).toFixed(2)}`, name];
                                                    return [parseFloat(value).toFixed(2), name];
                                                }}
                                                labelFormatter={(label) => ''}
                                                content={(props) => {
                                                    const { payload } = props;
                                                    if (!payload || payload.length === 0) return null;
                                                    
                                                    const data = payload[0].payload;
                                                    return (
                                                        <div style={{ 
                                                            backgroundColor: 'white', 
                                                            padding: '10px', 
                                                            border: '1px solid #ccc' 
                                                        }}>
                                                            <p style={{ margin: 0 }}><b>Model:</b> {data.modelName}</p>
                                                            <p style={{ margin: 0 }}><b>Task:</b> {data.taskName}</p>
                                                            <p style={{ margin: 0 }}><b>Cost:</b> ${data.cost.toFixed(2)}</p>
                                                            <p style={{ margin: 0 }}>
                                                                <b>{getAvailablePerformanceMetrics().find(opt => opt.key === performanceMetric)?.text}:</b> {data[performanceMetric].toFixed(2)}
                                                            </p>
                                                        </div>
                                                    );
                                                }}
                                            />
                                            <Legend 
                                                layout="horizontal" 
                                                verticalAlign="bottom" 
                                                align="center"
                                                wrapperStyle={{ paddingTop: 30 }}
                                            />
                                            
                                            {/* Generate scatter plot groups by model */}
                                            {Array.from(new Set(getCostVsPerformanceData().map(d => d.modelId))).map((modelId, mIndex) => {
                                                const modelData = getCostVsPerformanceData().filter(d => d.modelId === modelId);
                                                const model = models.find(m => m.id === modelId);
                                                return (
                                                    <Scatter
                                                        key={modelId}
                                                        name={model?.name || `Model ${mIndex}`}
                                                        data={modelData}
                                                        fill={getColor(mIndex)}
                                                        shape={(props: any) => {
                                                            const { cx, cy, fill } = props;
                                                            const taskIndex = tasks.findIndex(t => t.id === modelData[0]?.taskId);
                                                            const shape = getShape(taskIndex);
                                                            
                                                            // Render different shapes based on task
                                                            switch(shape) {
                                                                case 'square':
                                                                    return <rect x={cx - 5} y={cy - 5} width={10} height={10} fill={fill} />;
                                                                case 'triangle':
                                                                    return <polygon points={`${cx},${cy-5} ${cx+5},${cy+5} ${cx-5},${cy+5}`} fill={fill} />;
                                                                case 'diamond':
                                                                    return <polygon points={`${cx},${cy-7} ${cx+7},${cy} ${cx},${cy+7} ${cx-7},${cy}`} fill={fill} />;
                                                                case 'star':
                                                                    const points = [];
                                                                    for (let i = 0; i < 5; i++) {
                                                                        points.push(cx + 6 * Math.cos(i * 2 * Math.PI / 5 - Math.PI / 2));
                                                                        points.push(cy + 6 * Math.sin(i * 2 * Math.PI / 5 - Math.PI / 2));
                                                                        points.push(cx + 3 * Math.cos((i * 2 + 1) * Math.PI / 5 - Math.PI / 2));
                                                                        points.push(cy + 3 * Math.sin((i * 2 + 1) * Math.PI / 5 - Math.PI / 2));
                                                                    }
                                                                    return <polygon points={points.join(',')} fill={fill} />;
                                                                default: // circle
                                                                    return <circle cx={cx} cy={cy} r={5} fill={fill} />;
                                                            }
                                                        }}
                                                    />
                                                );
                                            })}
                                        </ScatterChart>
                                    </div>
                                    
                                    {/* Task Shape Legend */}
                                    <div style={{ padding: '10px 0' }}>
                                        <Text variant="medium">Clinical Tasks:</Text>
                                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', marginTop: '5px' }}>
                                            {tasks.filter(task => 
                                                activeTab === 'clinicalTasks' 
                                                    ? selectedTasks.has(task.id)
                                                    : Array.from(selectedModels).some((modelId: string) => 
                                                        task.dataSetModels.some(dsm => dsm.modelId === modelId && !dsm.isGroundTruth)
                                                    )
                                            ).map((task, index) => (
                                                <div key={task.id} style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                                                    <div style={{ 
                                                        width: 12, 
                                                        height: 12, 
                                                        display: 'inline-block',
                                                        background: '#666',
                                                        clipPath: getShape(index) === 'circle' 
                                                            ? 'circle(50%)' 
                                                            : getShape(index) === 'square'
                                                            ? 'polygon(0% 0%, 100% 0%, 100% 100%, 0% 100%)'
                                                            : getShape(index) === 'triangle'
                                                            ? 'polygon(50% 0%, 100% 100%, 0% 100%)'
                                                            : getShape(index) === 'diamond'
                                                            ? 'polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%)'
                                                            : 'polygon(50% 0%, 61% 35%, 98% 35%, 68% 57%, 79% 91%, 50% 70%, 21% 91%, 32% 57%, 2% 35%, 39% 35%)'
                                                    }} />
                                                    <Text style={{ fontSize: 12 }}>{task.name}</Text>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                </Stack>
                            )}
                        </Stack.Item>
                    </Stack>
                ) : (
                    <Text>Please select clinical tasks or models to view metrics</Text>
                )}
            </Stack.Item>
        </Stack>
    );
};
export {};