import React, { useState } from 'react';
import { Stack, IconButton, StackItem, CommandButton } from '@fluentui/react';
import { cornerstone, cornerstoneTools } from '../../utils/cornerstoneInit';
import { BoundingBox } from '../../types/admin';

interface CornerstoneToolbarProps {
    element: HTMLDivElement | null;
    isEnabled: boolean;
    activeMode: 'zoom' | 'contrast' | 'pan' | 'measure' | 'boundingBox';
    onModeChange: (mode: 'zoom' | 'contrast' | 'pan' | 'measure' | 'boundingBox') => void;
    initialViewport: {
        scale: number;
        translation: { x: number; y: number };
        voi: { windowWidth: number; windowCenter: number };
    };
    boundingBoxes?: BoundingBox[];
    onClearBoundingBoxes?: () => void;
    onDeleteBox?: (id: string) => void;
}

export const CornerstoneToolbar: React.FC<CornerstoneToolbarProps> = ({ 
    element, 
    isEnabled,
    activeMode,
    onModeChange,
    initialViewport,
    boundingBoxes = [],
    onClearBoundingBoxes,
    onDeleteBox
}) => {
    const [isFullscreen, setIsFullscreen] = useState(false);
    const [activeMeasureTool, setActiveMeasureTool] = useState<'Length' | 'Angle'>('Length');

    if (!element || !isEnabled) return null;

    const handleReset = () => {
        if (!element) return;
        const viewport = cornerstone.getViewport(element);
        if (viewport) {
            viewport.scale = initialViewport.scale;
            viewport.translation.x = initialViewport.translation.x;
            viewport.translation.y = initialViewport.translation.y;
            viewport.voi = {
                windowWidth: initialViewport.voi.windowWidth,
                windowCenter: initialViewport.voi.windowCenter
            };
            cornerstone.setViewport(element, viewport);
        }
    };

    const handleZoomIn = () => {
        if (!element) return;
        const viewport = cornerstone.getViewport(element);
        if (viewport) {
            viewport.scale += 0.25;
            cornerstone.setViewport(element, viewport);
        }
    };

    const handleZoomOut = () => {
        if (!element) return;
        const viewport = cornerstone.getViewport(element);
        if (viewport) {
            viewport.scale -= 0.25;
            cornerstone.setViewport(element, viewport);
        }
    };

    const handleContrastBrightness = (action: 'contrastUp' | 'contrastDown' | 'brightnessUp' | 'brightnessDown') => {
        if (!element) return;
        const viewport = cornerstone.getViewport(element);
        if (viewport) {
            const deltaWidth = viewport.voi.windowWidth * 0.1;
            const deltaCenter = viewport.voi.windowCenter * 0.1;

            switch (action) {
                case 'contrastUp':
                    viewport.voi.windowWidth = Math.max(viewport.voi.windowWidth - deltaWidth, 1);
                    break;
                case 'contrastDown':
                    viewport.voi.windowWidth += deltaWidth;
                    break;
                case 'brightnessUp':
                    viewport.voi.windowCenter += deltaCenter;
                    break;
                case 'brightnessDown':
                    viewport.voi.windowCenter -= deltaCenter;
                    break;
            }
            cornerstone.setViewport(element, viewport);
        }
    };

    const toggleFullScreen = () => {
        if (!document.fullscreenElement) {
            // Enter fullscreen
            const container = element.parentElement;
            if (container && container.requestFullscreen) {
                container.requestFullscreen().then(() => {
                    setIsFullscreen(true);
                    
                    // Set fullscreen styles
                    container.style.width = '100vw';
                    container.style.height = '100vh';
                    container.style.margin = '0';
                    container.style.padding = '0';
                    container.style.maxWidth = '100%';
                    container.style.maxHeight = '100%';
                    container.style.objectFit = 'contain';
                    
                    // Apply styles to the element itself
                    element.style.width = '100%';
                    element.style.height = '100%';
                    element.style.margin = '0';
                    element.style.padding = '0';
                    
                    // Force resize after switching to full screen
                    setTimeout(() => {
                        cornerstone.resize(element);
                        const image = cornerstone.getImage(element);
                        if (image) {
                            cornerstone.displayImage(element, image);
                        }
                    }, 100);
                }).catch(err => {
                    console.error('Error attempting to enable fullscreen:', err);
                });
            }
        } else {
            // Exit fullscreen
            if (document.exitFullscreen) {
                document.exitFullscreen().then(() => {
                    setIsFullscreen(false);
                    
                    // Restore original styles after exiting fullscreen
                    const container = element.parentElement;
                    if (container) {
                        container.style.width = '100%';
                        container.style.height = ''; // Let it adapt based on the height prop
                        container.style.margin = '';
                        container.style.padding = '';
                        container.style.maxWidth = '';
                        container.style.maxHeight = '';
                        container.style.objectFit = '';
                    }
                    
                    // Restore element styles
                    element.style.width = '100%';
                    element.style.height = '100%';
                    element.style.margin = '';
                    element.style.padding = '';
                    
                    // Force resize after exiting full screen
                    setTimeout(() => {
                        cornerstone.resize(element);
                        const image = cornerstone.getImage(element);
                        if (image) {
                            cornerstone.displayImage(element, image);
                        }
                    }, 100);
                }).catch(err => {
                    console.error('Error attempting to exit fullscreen:', err);
                });
            }
        }
    };

    const activateMeasureTool = (toolName: 'Length' | 'Angle') => {
        if (!element) return;
        
        setActiveMeasureTool(toolName);
        
        // Activate the selected tool
        cornerstoneTools.setToolActive(toolName, { 
            mouseButtonMask: 1,
            isTouchActive: true
        });
        
        // Make sure measurement mode is active
        if (activeMode !== 'measure') {
            onModeChange('measure');
        }
    };

    const clearAnnotations = () => {
        if (!element) return;
        
        // Clear all tool data from the element
        const toolState = (cornerstoneTools as any).getToolState(element, 'Length');
        if (toolState && toolState.data) {
            toolState.data = [];
        }
        
        // Also clear Angle tool data if present
        const angleToolState = (cornerstoneTools as any).getToolState(element, 'Angle');
        if (angleToolState && angleToolState.data) {
            angleToolState.data = [];
        }
        
        // Force cornerstone to redraw
        (cornerstone as any).updateImage(element);
    };

    return (
        <Stack horizontal 
            styles={{
                root: {
                    position: 'absolute',
                    top: '10px',
                    right: '10px',
                    background: 'rgba(255, 255, 255, 0.7)',
                    borderRadius: '4px',
                    padding: '2px',
                    zIndex: 100
                }
            }}
        >
            {/* Mode toggles */}
            <StackItem>
                <CommandButton 
                    iconProps={{ iconName: 'ZoomToFit' }} 
                    title="Zoom mode"
                    styles={{ root: { backgroundColor: activeMode === 'zoom' ? '#e0e0e0' : 'transparent' } }}
                    onClick={() => onModeChange('zoom')}
                />
            </StackItem>
            <StackItem>
                <CommandButton 
                    iconProps={{ iconName: 'Contrast' }} 
                    title="Contrast mode"
                    styles={{ root: { backgroundColor: activeMode === 'contrast' ? '#e0e0e0' : 'transparent' } }}
                    onClick={() => onModeChange('contrast')}
                />
            </StackItem>
            <StackItem>
                <CommandButton 
                    iconProps={{ iconName: 'Move' }} 
                    title="Pan mode"
                    styles={{ root: { backgroundColor: activeMode === 'pan' ? '#e0e0e0' : 'transparent' } }}
                    onClick={() => onModeChange('pan')}
                />
            </StackItem>
            <StackItem>
                <CommandButton 
                    iconProps={{ iconName: 'LineStyle' }} 
                    title="Measurement tools"
                    styles={{ root: { backgroundColor: activeMode === 'measure' ? '#e0e0e0' : 'transparent' } }}
                    onClick={() => onModeChange('measure')}
                />
            </StackItem>
            <StackItem>
                <CommandButton 
                    iconProps={{ iconName: 'RectangleShape' }} 
                    title="Bounding Box tools"
                    styles={{ root: { backgroundColor: activeMode === 'boundingBox' ? '#e0e0e0' : 'transparent' } }}
                    onClick={() => onModeChange('boundingBox')}
                />
            </StackItem>

            {/* Divider */}
            <StackItem>
                <div style={{ width: '1px', height: '24px', margin: '0 8px', backgroundColor: '#ccc' }} />
            </StackItem>

            {/* Action buttons */}
            <StackItem>
                <IconButton 
                    iconProps={{ iconName: 'ZoomIn' }} 
                    title="Zoom in"
                    onClick={handleZoomIn}
                />
            </StackItem>
            <StackItem>
                <IconButton 
                    iconProps={{ iconName: 'ZoomOut' }} 
                    title="Zoom out"
                    onClick={handleZoomOut}
                />
            </StackItem>

            {activeMode === 'contrast' && (
                <>
                    <StackItem>
                        <IconButton 
                            iconProps={{ iconName: 'Add' }} 
                            title="Increase brightness"
                            onClick={() => handleContrastBrightness('brightnessUp')}
                        />
                    </StackItem>
                    <StackItem>
                        <IconButton 
                            iconProps={{ iconName: 'Remove' }} 
                            title="Decrease brightness"
                            onClick={() => handleContrastBrightness('brightnessDown')}
                        />
                    </StackItem>
                    <StackItem>
                        <IconButton 
                            iconProps={{ iconName: 'FilterSettings' }} 
                            title="Increase contrast"
                            onClick={() => handleContrastBrightness('contrastUp')}
                        />
                    </StackItem>
                    <StackItem>
                        <IconButton 
                            iconProps={{ iconName: 'Filter' }} 
                            title="Decrease contrast"
                            onClick={() => handleContrastBrightness('contrastDown')}
                        />
                    </StackItem>
                </>
            )}

            {activeMode === 'measure' && (
                <>
                    <StackItem>
                        <CommandButton 
                            iconProps={{ iconName: 'Line' }} 
                            title="Length Tool"
                            styles={{ root: { backgroundColor: activeMeasureTool === 'Length' ? '#e0e0e0' : 'transparent' } }}
                            onClick={() => activateMeasureTool('Length')}
                        />
                    </StackItem>
                    <StackItem>
                        <CommandButton 
                            iconProps={{ iconName: 'TriangleSolid' }} 
                            title="Angle Tool"
                            styles={{ root: { backgroundColor: activeMeasureTool === 'Angle' ? '#e0e0e0' : 'transparent' } }}
                            onClick={() => activateMeasureTool('Angle')}
                        />
                    </StackItem>
                    <StackItem>
                        <CommandButton 
                            iconProps={{ iconName: 'Delete' }} 
                            title="Clear Measurements"
                            onClick={clearAnnotations}
                        />
                    </StackItem>
                </>
            )}

            {activeMode === 'boundingBox' && (
                <>
                    <StackItem>
                        <CommandButton 
                            iconProps={{ iconName: 'Delete' }} 
                            title="Clear Bounding Boxes"
                            onClick={onClearBoundingBoxes}
                        />
                    </StackItem>
                    <StackItem>
                        <div style={{ 
                            padding: '5px 10px', 
                            backgroundColor: '#f0f0f0', 
                            borderRadius: '3px',
                            fontSize: '12px'
                        }}>
                            {boundingBoxes.length} boxes
                        </div>
                    </StackItem>
                </>
            )}

            <StackItem>
                <IconButton 
                    iconProps={{ iconName: 'Refresh' }} 
                    title="Reset view"
                    onClick={handleReset}
                />
            </StackItem>
            <StackItem>
                <IconButton 
                    iconProps={{ iconName: isFullscreen ? 'BackToWindow' : 'FullScreen' }} 
                    title={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                    onClick={toggleFullScreen}
                />
            </StackItem>
        </Stack>
    );
}; 