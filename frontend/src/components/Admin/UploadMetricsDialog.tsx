import React, { useState } from 'react';
import { 
    Dialog, 
    DialogType, 
    DialogFooter, 
    TextField, 
    PrimaryButton, 
    DefaultButton,
    MessageBar,
    MessageBarType,
    Stack
} from '@fluentui/react';
import { IModel } from '../../types/admin';
import { clinicalTaskService } from '../../services/clinicalTaskService';
import { toast } from 'react-hot-toast';

interface UploadMetricsDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    clinicalTaskId: string;
    models: IModel[];
    onSuccess: () => void;
}

export const UploadMetricsDialog: React.FC<UploadMetricsDialogProps> = ({
    isOpen,
    onDismiss,
    clinicalTaskId,
    models,
    onSuccess
}) => {
    const [jsonInput, setJsonInput] = useState<string>('');
    const [error, setError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const validateAndTransformJson = () => {
        try {
            // Parse the JSON input
            const metricsJson = JSON.parse(jsonInput);
            
            if (typeof metricsJson !== 'object' || metricsJson === null || Array.isArray(metricsJson)) {
                setError('JSON must be an object with model names as keys');
                return null;
            }

            // Create a map of lowercase model names to model IDs
            const modelNameToIdMap = new Map(
                models.map(model => [model.name.toLowerCase(), model.id])
            );

            // Create the transformed metrics object using model IDs as keys
            const transformedMetrics: Record<string, Record<string, number>> = {};
            
            for (const [modelName, metrics] of Object.entries(metricsJson)) {
                const lowercaseModelName = modelName.toLowerCase();
                const modelId = modelNameToIdMap.get(lowercaseModelName);
                
                if (!modelId) {
                    setError(`Model not found: ${modelName}`);
                    return null;
                }
                
                if (typeof metrics !== 'object' || metrics === null || Array.isArray(metrics)) {
                    setError(`Metrics for model "${modelName}" must be an object`);
                    return null;
                }
                
                // Validate that all metric values are numbers
                const metricsObj = metrics as Record<string, any>;
                for (const [metricName, value] of Object.entries(metricsObj)) {
                    if (typeof value !== 'number') {
                        setError(`Metric "${metricName}" for model "${modelName}" must be a number`);
                        return null;
                    }
                }
                
                transformedMetrics[modelId] = metrics as Record<string, number>;
            }

            return transformedMetrics;
        } catch (err) {
            setError('Invalid JSON format');
            return null;
        }
    };

    const handleSubmit = async () => {
        setError(null);
        setIsSubmitting(true);
        
        const transformedMetrics = validateAndTransformJson();
        if (!transformedMetrics) {
            setIsSubmitting(false);
            return;
        }
        
        try {
            await clinicalTaskService.uploadMetrics(clinicalTaskId, transformedMetrics);
            toast.success('Metrics uploaded successfully');
            setJsonInput('');
            setIsSubmitting(false);
            onSuccess();
            onDismiss();
        } catch (err) {
            setError('Failed to upload metrics');
            setIsSubmitting(false);
        }
    };

    return (
        <Dialog
            hidden={!isOpen}
            onDismiss={onDismiss}
            dialogContentProps={{
                type: DialogType.normal,
                title: 'Upload Metrics',
                subText: 'Paste JSON metrics data. Format should be: {"Model Name": {"metric1": value1, "metric2": value2}}'
            }}
            minWidth={600}
        >
            <Stack tokens={{ childrenGap: 15 }}>
                {error && (
                    <MessageBar messageBarType={MessageBarType.error}>
                        {error}
                    </MessageBar>
                )}
                
                <TextField
                    label="Metrics JSON"
                    multiline
                    rows={10}
                    value={jsonInput}
                    onChange={(_, newValue) => setJsonInput(newValue || '')}
                    placeholder='{ "Model Name 1": { "accuracy": 0.95, "precision": 0.92 }, "Model Name 2": { "accuracy": 0.88, "precision": 0.89 } }'
                />
                
                <DialogFooter>
                    <PrimaryButton 
                        onClick={handleSubmit} 
                        text="Upload" 
                        disabled={!jsonInput || isSubmitting} 
                    />
                    <DefaultButton 
                        onClick={onDismiss} 
                        text="Cancel" 
                    />
                </DialogFooter>
            </Stack>
        </Dialog>
    );
}; 