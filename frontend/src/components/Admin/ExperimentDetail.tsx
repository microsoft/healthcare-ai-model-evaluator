import React, { useEffect, useState } from 'react';
import { 
    Stack, 
    DetailsList,
    SelectionMode,
    IColumn,
    Text,
    Breadcrumb,
    IBreadcrumbItem,
    Panel,
    PanelType,
    DefaultButton,
    Link
} from '@fluentui/react';
import { useNavigate, useParams } from 'react-router-dom';
import { experimentService } from '../../services/experimentService';
import { IExperiment, IModel, ITrial } from '../../types/admin';
import { userService } from '../../services/userService';
import { User } from '../../types/user';
import { modelService } from 'services/modelService';

export const ExperimentDetail: React.FC = () => {
    const [experiment, setExperiment] = useState<IExperiment | null>(null);
    const [trials, setTrials] = useState<ITrial[]>([]);
    const [selectedTrial, setSelectedTrial] = useState<ITrial | null>(null);
    const [users, setUsers] = useState<{ [key: string]: User }>({});
    const [models, setModels] = useState<{ [key: string]: IModel }>({});
    const navigate = useNavigate();
    const { experimentId } = useParams();

    useEffect(() => {
        const loadData = async () => {
            if (experimentId) {
                // Load experiment and trials
                const [exp, trialList, allUsers, modelList] = await Promise.all([
                    experimentService.getExperiment(experimentId),
                    experimentService.getTrials(experimentId),
                    userService.getUsers(),
                    modelService.getModels()
                ]);

                setExperiment(exp);
                setModels(modelList.reduce((acc, model) => {
                    acc[model.id] = model;
                    return acc;
                }, {} as { [key: string]: IModel }));
                setTrials(trialList);

                // Create a map of users by ID for quick lookup
                const userMap = allUsers.reduce((acc, user) => {
                    acc[user.id] = user;
                    return acc;
                }, {} as { [key: string]: User });
                
                setUsers(userMap);
            }
        };
        loadData();
    }, [experimentId]);

    const columns: IColumn[] = [
        { 
            key: 'view', 
            name: '', 
            fieldName: '', 
            minWidth: 150, 
            maxWidth: 150,
            isResizable: true,
            onRender: (item: ITrial) => (
                <DefaultButton 
                    onClick={() => setSelectedTrial(item)}
                >
                    View
                </DefaultButton>
            )
        },
        { 
            key: 'reviewer', 
            name: 'Reviewer', 
            fieldName: 'reviewerId',
            minWidth: 200,
            maxWidth: 200,
            isResizable: true,
            onRender: (item: ITrial) => {
                if (!item.userId) return 'Unassigned';
                const reviewer = users[item.userId];
                return reviewer ? reviewer.name : 'Unknown User';
            }
        },
        {
            key: 'model',
            name: 'Models',
            fieldName: 'modelOutputs.modelId',
            minWidth: 200,
            maxWidth: 200,
            isResizable: true,
            onRender: (item: ITrial) => {
                if (!item.modelOutputs?.length) return '';
                return item.modelOutputs.map(output => models[output.modelId]?.name).join(', ');
            }
        },
        { 
            key: 'status', 
            name: 'Status', 
            fieldName: 'status',
            minWidth: 100,
            isResizable: true,
        }
    ];

    const breadcrumbItems: IBreadcrumbItem[] = [
        { key: 'experiments', text: 'Assignments', onClick: () => navigate('/admin/assignments') },
        { key: 'details', text: experiment?.name || 'Assignment Details', isCurrentItem: true }
    ];

    return (
        <Stack tokens={{ childrenGap: 20 }}>
            <Breadcrumb items={breadcrumbItems} />
            <Text variant="xxLarge">{experiment?.name}</Text>
            
            <DetailsList
                items={trials}
                columns={columns}
                selectionMode={SelectionMode.none}
            />

            <Panel
                isOpen={!!selectedTrial}
                onDismiss={() => setSelectedTrial(null)}
                type={PanelType.medium}
                isLightDismiss={true}
            >
                {selectedTrial && (
                    <Stack tokens={{ childrenGap: 15 }}>
                        <Text variant="xLarge">Trial Review</Text>
                        {selectedTrial.questions?.map(question => (
                            <Stack key={question.id} tokens={{ childrenGap: 5 }}>
                                <Text variant="mediumPlus">{question.questionText}</Text>
                                <Text>response: {question.response}</Text>
                            </Stack>
                        ))}
                        {selectedTrial.response && (
                            <Stack tokens={{ childrenGap: 10 }}>
                                <Text variant="mediumPlus">Response:</Text>
                                <Text>{selectedTrial.response.text}</Text>
                                <Text>{models[selectedTrial.response.modelId]?models[selectedTrial.response.modelId]?.name:selectedTrial.response.modelId.split(',').map(id => models[id]?.name).join(', ')}</Text>
                            </Stack>
                        )}

                        {selectedTrial.dataObjectId && (
                            <Link 
                                href={`/admin/data/${selectedTrial.dataSetId}/object/${selectedTrial.dataObjectId}`}
                            >
                                View Data Object
                            </Link>
                        )}
                    </Stack>
                )}
            </Panel>
        </Stack>
    );
};

export {}; 