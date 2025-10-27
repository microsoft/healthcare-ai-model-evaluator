import React from 'react';
import { Stack, Text, Label, IconButton } from '@fluentui/react';
import './Arena.scss';
import { ChartSection } from './types';
import { DataContent } from '../../types/dataset';
import { getImage } from 'services/imageService';
import { LoadingOverlay } from './LoadingOverlay';
import { CornerstoneViewer } from './CornerstoneViewer';
import { BoundingBox } from '../../types/admin';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';

// Let's try importing directly from the UMD bundle
// import { CornerstoneViewport } from '@ohif/ui/dist/ohif-ui.umd.js';

interface ReferenceDataProps {
    inputData: DataContent[] | null;
    isExpanded?: boolean;
    onToggleExpand?: () => void;
    onBoundingBoxesChange?: (boxes: BoundingBox[]) => void;
    initialBoundingBoxes?: BoundingBox[];
    selectedBoxId?: string;
    onDeleteBox?: (id: string) => void;
    boxToDelete?: string;
}


export const ReferenceDataPanel: React.FC<ReferenceDataProps> = ({ 
    inputData, 
    isExpanded = true,
    onToggleExpand,
    onBoundingBoxesChange,
    initialBoundingBoxes,
    selectedBoxId,
    onDeleteBox,
    boxToDelete
}) => {
    // Add state for loaded images
    const [loadedImages, setLoadedImages] = React.useState<{ [key: string]: string }>({});

    // Load image when content is an image URL
    const loadImage = React.useCallback(async (content: string) => {
        if (!loadedImages[content]) {
            try {
                const imageUrl = await getImage(content);
                setLoadedImages(prev => ({
                    ...prev,
                    [content]: imageUrl
                }));
            } catch (error) {
                console.error('Failed to load image:', error);
            }
        }
    }, [loadedImages]);

    const renderSectionContent = React.useCallback((section: ChartSection) => {
        if (section.type === 'image') {
            if (loadedImages[section.content]) {
                return (
                    <CornerstoneViewer
                        imageUrl={loadedImages[section.content]}
                        style={{ minWidth: '100%', height: isExpanded? '1200px' : '900px', flex: '1' }}
                        onBoundingBoxesChange={onBoundingBoxesChange}
                        initialBoundingBoxes={initialBoundingBoxes}
                        selectedBoxId={selectedBoxId}
                        onDeleteBox={onDeleteBox}
                        boxToDelete={boxToDelete}
                    />
                );
            } else {
                return <LoadingOverlay label="Loading image..." />;
            }
        }
        
        // Render text content as markdown
        return (
            <div className="markdown-content">
                <ReactMarkdown 
                    remarkPlugins={[remarkGfm]} 
                    rehypePlugins={[rehypeRaw]}
                >
                    {section.content}
                </ReactMarkdown>
            </div>
        );
    }, [loadedImages, isExpanded, onBoundingBoxesChange, initialBoundingBoxes, selectedBoxId, onDeleteBox, boxToDelete]);

    // Move useEffect outside of the render function
    React.useEffect(() => {
        if (inputData) {
            inputData.forEach(input => {
                if (input.type === 'imageurl') {
                    loadImage(input.content);
                }
            });
        }
    }, [inputData, loadImage]);

    if (!inputData) return null;


    const createSectionsFromInputData = () => {
        const sections: ChartSection[] = [];
        
        // Add each input data item as a section
        inputData.forEach((input, index) => 
            {
                sections.push({
                    title: ``,
                    type: input.type === 'imageurl' ? 'image' : input.type,
                    content: input.content
                });
            }
        );

        return sections;
    };

    return (
        <Stack 
            className={`reference-panel ${isExpanded ? 'expanded' : ''}`}
            tokens={{ childrenGap: 20 }}
            styles={{
                root: {
                    height: '100%',
                    display: 'flex'
                }
            }}
        >
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Label>Reference Data</Label>
                {onToggleExpand && (
                    <IconButton
                        iconProps={{ iconName: isExpanded ? 'BackToWindow' : 'FullScreen'}}
                        onClick={onToggleExpand}
                    />
                )}
            </Stack>
            <Stack tokens={{ childrenGap: 15 }} styles={{
                root: {
                    overflowY: 'auto',
                    flex: 1,
                    backgroundColor: '#ffffff',
                    padding: '10px'
                }
            }}>
                <Stack tokens={{ childrenGap: 10 }}>
                    {createSectionsFromInputData().map((section, index) => (
                        <Stack.Item key={index}>
                            {section.title && (
                                <Text variant="mediumPlus" block styles={{ root: { marginBottom: '5px' } }}>
                                    {section.title}
                                </Text>
                            )}
                            {renderSectionContent(section)}
                        </Stack.Item>
                    ))}
                </Stack>
            </Stack>
        </Stack>
    );
};

export {}; 