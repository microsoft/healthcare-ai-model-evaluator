import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { RootState } from '../../store/store';
import { fetchModels } from '../../reducers/modelReducer';
import { fetchTasks } from '../../reducers/clinicalTaskReducer';
import { AppDispatch } from '../../store/store';
import {
  DetailsList,
  DetailsListLayoutMode,
  IColumn,
   SelectionMode,
  Spinner,
  Stack,
  Text,
  Dropdown,
  IDropdownOption,
 IStackTokens,
  IDropdownStyles,
  Pivot,
  PivotItem,
  Label,
  DirectionalHint,
} from '@fluentui/react';
import { VerticalBarChart } from '@fluentui/react-charting';

const stackTokens: IStackTokens = { childrenGap: 20 };
const dropdownStyles: Partial<IDropdownStyles> = { dropdown: { width: 300 } };

export const MetricsManagement: React.FC = () => {
  const dispatch = useDispatch<AppDispatch>();
  const models = useSelector((state: RootState) => state.models.models);
  const tasks = useSelector((state: RootState) => state.clinicalTasks.tasks);
  const isLoading = useSelector((state: RootState) => 
    state.models.isLoading || state.clinicalTasks.isLoading
  );

  // State for filters
  const [selectedModelIds, setSelectedModelIds] = useState<string[]>([]);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [selectedMetrics, setSelectedMetrics] = useState<string[]>([]);
  const [availableMetrics, setAvailableMetrics] = useState<string[]>([]);

  // State for chart data
  const [chartData, setChartData] = useState<any[]>([]);

  useEffect(() => {
    // Load models and clinical tasks
    dispatch(fetchModels());
    dispatch(fetchTasks());
  }, [dispatch]);

  // Extract unique metrics from all tasks
  useEffect(() => {
    const metrics = new Set<string>();
    
    tasks.forEach(task => {
      if (task.metrics && Object.keys(task.metrics).length > 0) {
        Object.values(task.metrics).forEach(metricObject => {
          Object.keys(metricObject).forEach(metric => {
            metrics.add(metric);
          });
        });
      }
    });
    
    setAvailableMetrics(Array.from(metrics));
    if (metrics.size > 0 && selectedMetrics.length === 0) {
      // Select first metric by default
      setSelectedMetrics([Array.from(metrics)[0]]);
    }
  }, [tasks, selectedMetrics]);



  // Update chart data when filters change
  useEffect(() => {
    // Move the function inside useEffect
    const updateChartData = () => {
      const filteredModels = selectedModelIds.length > 0 
        ? models.filter(model => selectedModelIds.includes(model.id))
        : models;
      
      const filteredTasks = selectedTaskIds.length > 0
        ? tasks.filter(task => selectedTaskIds.includes(task.id))
        : tasks;
      
      const newChartData: any[] = [];
      
      filteredModels.forEach(model => {
        filteredTasks.forEach(task => {
          if (task.metrics && task.metrics[model.id]) {
            const modelMetrics = task.metrics[model.id];
            
            selectedMetrics.forEach(metricName => {
              if (metricName in modelMetrics) {
                newChartData.push({
                  modelId: model.id,
                  modelName: model.name,
                  taskId: task.id,
                  taskName: task.name,
                  metricName: metricName,
                  metricValue: modelMetrics[metricName]
                });
              }
            });
          }
        });
      });
      
      setChartData(newChartData);
    };
    
    // Call the function inside useEffect
    updateChartData();
  }, [selectedModelIds, selectedTaskIds, selectedMetrics, tasks, models]); // updateChartData removed from dependencies

  // Model selection dropdown options
  const modelOptions: IDropdownOption[] = models.map(model => ({
    key: model.id,
    text: model.name
  }));

  // Task selection dropdown options
  const taskOptions: IDropdownOption[] = tasks.filter(task => task.metrics && Object.keys(task.metrics).length > 0).map(task => ({
    key: task.id,
    text: task.name
  }));

  // Metric selection dropdown options
  const metricOptions: IDropdownOption[] = availableMetrics.map(metric => ({
    key: metric,
    text: metric
  }));

  // Handle model selection changes
  const onModelSelectionChanged = (
    event: React.FormEvent<HTMLDivElement>,
    item?: IDropdownOption
  ): void => {
    if (item) {
      setSelectedModelIds(
        item.selected
          ? [...selectedModelIds, item.key as string]
          : selectedModelIds.filter(id => id !== item.key)
      );
    }
  };

  // Handle task selection changes
  const onTaskSelectionChanged = (
    event: React.FormEvent<HTMLDivElement>,
    item?: IDropdownOption
  ): void => {
    if (item) {
      setSelectedTaskIds(
        item.selected
          ? [...selectedTaskIds, item.key as string]
          : selectedTaskIds.filter(id => id !== item.key)
      );
    }
  };

  // Handle metric selection changes
  const onMetricSelectionChanged = (
    event: React.FormEvent<HTMLDivElement>,
    item?: IDropdownOption
  ): void => {
    if (item) {
      setSelectedMetrics(
        item.selected
          ? [...selectedMetrics, item.key as string]
          : selectedMetrics.filter(metric => metric !== item.key)
      );
    }
  };

  // Prepare bar chart data with unique keys and different colors
  const getBarChartData = () => {
    // Group data by metric name
    const groupedByMetric: Record<string, any[]> = chartData.reduce((acc, item) => {
      if (!acc[item.metricName]) {
        acc[item.metricName] = [];
      }
      acc[item.metricName].push(item);
      return acc;
    }, {} as Record<string, any[]>);
    
    // Define a color palette for different models
    const colorPalette = [
      '#0078D4', // Blue
      '#107C10', // Green
      '#D83B01', // Orange
      '#5C2D91', // Purple
      '#E3008C', // Magenta
      '#00B294', // Teal
      '#FFB900', // Gold
      '#F7630C', // Burnt Orange
      '#EA4300'  // Red
    ];
    
    // Create chart data with unique keys and different colors
    let chartPoints: any[] = [];
    let colorIndex = 0;
    const modelColors: {[key: string]: string} = {};
    let pointIndex = 1; // Simple numeric label for x-axis
    
    Object.entries(groupedByMetric).forEach(([metricName, items]) => {
      // Sort items by model name for consistent ordering
      const sortedItems = [...items].sort((a, b) => a.modelName.localeCompare(b.modelName));
      
      sortedItems.forEach((item) => {
        // Assign a consistent color to each model
        if (!modelColors[item.modelId]) {
          modelColors[item.modelId] = colorPalette[colorIndex % colorPalette.length];
          colorIndex++;
        }
        
        // Use a simple numeric identifier for the x-axis
        chartPoints.push({
          x: pointIndex.toString(), // Use simple numbers on x-axis
          y: item.metricValue,
          legend: `${item.modelName}`,
          color: modelColors[item.modelId],
          // Store full info for tooltip/callout
          xAxisCalloutData: `${item.metricName} (${item.modelName} on ${item.taskName})`,
          // Keep the original data for reference
          metricName: item.metricName,
          modelName: item.modelName,
          taskName: item.taskName
        });
        
        pointIndex++;
      });
    });
    
    return chartPoints;
  };

  // Render visualization based on selected view mode
  const renderVisualization = () => {
    // Check if no selections have been made
    const noSelections = 
      (selectedModelIds.length === 0 || selectedTaskIds.length === 0 || selectedMetrics.length === 0);
    
    if (noSelections) {
      return (
        <Stack horizontalAlign="center" verticalAlign="center" styles={{ root: { height: 400, width: 600 } }}>
          <Text variant="large" styles={{ root: { textAlign: 'center' } }}>
            Please select at least one model, clinical task, and metric to display chart data.
          </Text>
        </Stack>
      );
    }
    
    if (chartData.length === 0) {
      return <Text>No data available for the selected filters.</Text>;
    }
    
    // Check if all values are zero
    const allZeroValues = chartData.every(item => item.metricValue === 0);
    if (allZeroValues) {
      return <Text>All metric values are zero. The model may not have produced measurable results.</Text>;
    }

    return (
      <VerticalBarChart
        data={getBarChartData()}
        width={600}
        height={400}
        enabledLegendsWrapLines
        legendProps={{
          allowFocusOnLegends: true,
          canSelectMultipleLegends: true,
        }}
        calloutProps={{ directionalHint: DirectionalHint.topCenter }}
      />
    );
  };

  // Render detail list columns
  const columns: IColumn[] = [
    {
      key: 'modelName',
      name: 'Model Name',
      fieldName: 'modelName',
      minWidth: 100,
      maxWidth: 200,
      isResizable: true
    },
    {
      key: 'taskName',
      name: 'Task Name',
      fieldName: 'taskName',
      minWidth: 100,
      maxWidth: 200,
      isResizable: true
    },
    {
      key: 'metricName',
      name: 'Metric',
      fieldName: 'metricName',
      minWidth: 100,
      maxWidth: 150,
      isResizable: true
    },
    {
      key: 'metricValue',
      name: 'Value',
      fieldName: 'metricValue',
      minWidth: 70,
      maxWidth: 100,
      isResizable: true
    }
  ];

  if (isLoading) {
    return <Spinner label="Loading metrics data..." />;
  }

  return (
    <Stack tokens={stackTokens}>
      <Text variant="xxLarge">Metrics</Text>
      
      <Stack horizontal tokens={stackTokens}>
        <Stack tokens={stackTokens} styles={{ root: { width: '30%' } }}>
          <Label>Filter Options</Label>
          
          <Dropdown
            label="Select Models"
            selectedKeys={selectedModelIds}
            multiSelect
            options={modelOptions}
            onChange={onModelSelectionChanged}
            styles={dropdownStyles}
          />
          
          <Dropdown
            label="Select Clinical Tasks"
            selectedKeys={selectedTaskIds}
            multiSelect
            options={taskOptions}
            onChange={onTaskSelectionChanged}
            styles={dropdownStyles}
          />
          
          <Dropdown
            label="Select Metrics"
            selectedKeys={selectedMetrics}
            multiSelect
            options={metricOptions}
            onChange={onMetricSelectionChanged}
            styles={dropdownStyles}
          />
          
        </Stack>
        
        <Stack.Item grow styles={{ root: { width: '70%' } }}>
          <Pivot>
            <PivotItem headerText="Visualization">
              {renderVisualization()}
            </PivotItem>
            <PivotItem headerText="Data Table">
              <DetailsList
                items={chartData}
                columns={columns}
                layoutMode={DetailsListLayoutMode.justified}
                selectionMode={SelectionMode.none}
                isHeaderVisible={true}
              />
            </PivotItem>
          </Pivot>
        </Stack.Item>
      </Stack>
    </Stack>
  );
};

export default MetricsManagement; 
