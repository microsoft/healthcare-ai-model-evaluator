import React, { useEffect, useRef, useState, useCallback } from 'react';
import {
    cornerstone,
    cornerstoneTools
} from '../../utils/cornerstoneInit';
import { CornerstoneToolbar } from './CornerstoneToolbar';
import { Stack } from '@fluentui/react';
import { BoundingBox } from '../../types/admin';

interface CornerstoneViewerProps {
    imageUrl: string;
    style?: React.CSSProperties;
    height?: string | number;
    initialBoundingBoxes?: BoundingBox[];
    onBoundingBoxesChange?: (boundingBoxes: BoundingBox[]) => void;
    selectedBoxId?: string;
    boxToDelete?: string;
    onDeleteBox?: (id: string) => void;
}

interface CornerstoneImage {
    imageId: string;
    windowWidth?: number;
    windowCenter?: number;
    rowPixelSpacing?: number;
    columnPixelSpacing?: number;
}

export const CornerstoneViewer: React.FC<CornerstoneViewerProps> = ({ 
    imageUrl, 
    style,
    height = '800px',
    initialBoundingBoxes = [],
    onBoundingBoxesChange,
    selectedBoxId,
    boxToDelete,
    onDeleteBox
}) => {
    const elementRef = useRef<HTMLDivElement>(null);
    const [isEnabled, setIsEnabled] = useState(false);
    const imageRef = useRef<CornerstoneImage | null>(null);
    const [activeMode, setActiveMode] = useState<'zoom' | 'contrast' | 'pan' | 'measure' | 'boundingBox'>('zoom');
    const toolsInitializedRef = useRef(false);
    const hasInitializedBoxesRef = useRef(false);
    const initialViewportRef = useRef({
        scale: 1,
        translation: { x: 0, y: 0 },
        voi: { windowWidth: 255, windowCenter: 128 }
    });
    
    
   


    // Update initializeBoundingBoxes to handle percentage coordinates
    const initializeBoundingBoxes = useCallback(() => {
        if (!elementRef.current || !isEnabled || hasInitializedBoxesRef.current) return;

        const element = elementRef.current;
        const toolState = cornerstoneTools.getToolState(element, 'RectangleRoi');
        
        if (initialBoundingBoxes.length > 0) {
            console.log('Initializing bounding boxes:', initialBoundingBoxes);

            // Clear any existing tool state first
            if (toolState) {
                toolState.data = [];
            }

            // Get image dimensions for percentage conversion
            const image = cornerstone.getImage(element);
            const imageWidth = image ? image.width : 1;
            const imageHeight = image ? image.height : 1;

            // Add each bounding box
            initialBoundingBoxes.forEach(box => {
                let startX = box.x;
                let startY = box.y;
                let endX = box.x + box.width;
                let endY = box.y + box.height;

                // Convert percentage coordinates to pixel coordinates if needed
                if (box.coordinateType === 'percentage') {
                    startX = box.x * imageWidth;
                    startY = box.y * imageHeight;
                    endX = (box.x + box.width) * imageWidth;
                    endY = (box.y + box.height) * imageHeight;
                }

                const measurementData = {
                    uuid: box.id,
                    visible: true,
                    active: false,
                    invalidated: true,
                    handles: {
                        start: {
                            x: startX,
                            y: startY,
                            highlight: false,
                            active: false,
                            hasMoved: true
                        },
                        end: {
                            x: endX,
                            y: endY,
                            highlight: false,
                            active: false,
                            hasMoved: true
                        },
                        textBox: {
                            active: false,
                            hasMoved: false,
                            movesIndependently: false,
                            drawnIndependently: true,
                            allowedOutsideImage: true,
                            hasBoundingBox: true
                        }
                    },
                    annotation: box.annotation,
                    color: box.id === selectedBoxId ? 'rgb(0, 255, 0)' : 'rgb(255, 255, 0)',
                    lineWidth: box.id === selectedBoxId ? 3 : 2,
                    coordinateType: box.coordinateType // Store the original coordinate type
                };

                if (toolState) {
                    toolState.data.push(measurementData);
                } else {
                    cornerstoneTools.addToolState(element, 'RectangleRoi', measurementData);
                }
            });

            // Update the image once after all boxes are added
            cornerstone.updateImage(element);
            hasInitializedBoxesRef.current = true;
        }
    }, [isEnabled, initialBoundingBoxes, selectedBoxId]);

    // Reset initialization flag when image URL changes
    useEffect(() => {
        hasInitializedBoxesRef.current = false;
    }, [imageUrl]);

    // Update updateBoundingBoxes to convert to pixel coordinates when modified
    const updateBoundingBoxes = useCallback(() => {
        if (!elementRef.current || !isEnabled) return;
        
        const element = elementRef.current;
        const toolState = cornerstoneTools.getToolState(element, 'RectangleRoi');
        
        if (toolState && toolState.data) {
            const boxes: BoundingBox[] = toolState.data.map((data: any) => {
                const startX = Math.min(data.handles.start.x, data.handles.end.x);
                const startY = Math.min(data.handles.start.y, data.handles.end.y);
                const endX = Math.max(data.handles.start.x, data.handles.end.x);
                const endY = Math.max(data.handles.start.y, data.handles.end.y);

                // If this is a newly drawn or modified box, use pixel coordinates
                if (!data.uuid) {
                    return {
                        id: `box-${Math.random().toString(36).substr(2, 9)}`,
                        x: startX,
                        y: startY,
                        width: endX - startX,
                        height: endY - startY,
                        coordinateType: 'pixel',
                        annotation: data.annotation
                    };
                }

                // For existing boxes that haven't been modified, maintain their coordinate system
                return {
                    id: data.uuid,
                    x: startX,
                    y: startY,
                    width: endX - startX,
                    height: endY - startY,
                    coordinateType: 'pixel',
                    annotation: data.annotation
                };
            });
            
            onBoundingBoxesChange?.(boxes);
        }
    }, [isEnabled, onBoundingBoxesChange]);

    // Handle deleting a box
    const handleDeleteBox = useCallback((id: string) => {
        if (!elementRef.current || !isEnabled) return;

        const element = elementRef.current;
        const toolState = cornerstoneTools.getToolState(element, 'RectangleRoi');
        if (toolState && toolState.data && toolState.data.length > 0 && toolState.data.find((data: any) => data.uuid === id) !== undefined) {
            toolState.data = toolState.data.filter((data: any) => data.uuid !== id);
            cornerstone.updateImage(element);
            onDeleteBox?.(id);
        }
    }, [isEnabled, onDeleteBox]);
    
    useEffect(() => {
        if (boxToDelete) {
            handleDeleteBox(boxToDelete);
        }
    }, [boxToDelete, handleDeleteBox]);

    // Handle resize with debouncing
    const resizeCornerstoneElement = useCallback(() => {
        if (!elementRef.current || !isEnabled) return;
        
        const element = elementRef.current;
        
        try {
            console.log('Resizing cornerstone element');
            // Force resize
            cornerstone.resize(element);
            
            // If we have an image loaded, redisplay it
            if (imageRef.current) {
                cornerstone.displayImage(element, imageRef.current);
                
                // Reset viewport to maintain proper window/level settings
                const viewport = cornerstone.getViewport(element);
                if (viewport) {
                    viewport.voi = {
                        windowWidth: imageRef.current.windowWidth || 255,
                        windowCenter: imageRef.current.windowCenter || 128
                    };
                    cornerstone.setViewport(element, viewport);
                }
            }
        } catch (error) {
            console.error('Error during cornerstone resize:', error);
        }
    }, [isEnabled]);
    
    // Debounce resize function
    const debounceResize = useCallback(() => {
        let timeoutId: ReturnType<typeof setTimeout>;
        return () => {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(resizeCornerstoneElement, 100); // 100ms delay
        };
    }, [resizeCornerstoneElement]);
    
    // Set up resize handling
    useEffect(() => {
        if (!elementRef.current || !isEnabled) return;
        
        const element = elementRef.current;
        const debouncedResize = debounceResize();
        
        // Use ResizeObserver
        const resizeObserver = new ResizeObserver(() => {
            debouncedResize();
        });
        
        resizeObserver.observe(element);
        
        // Also listen for window resize as a fallback
        window.addEventListener('resize', debouncedResize);
        
        // Listen for transitionend events that might be fired by panel animations
        document.addEventListener('transitionend', debouncedResize);
        
        return () => {
            resizeObserver.disconnect();
            window.removeEventListener('resize', debouncedResize);
            document.removeEventListener('transitionend', debouncedResize);
        };
    }, [isEnabled, debounceResize]);
    
    // Define handleModeChange outside of useEffect for better reuse
    const handleModeChange = useCallback((mode: 'zoom' | 'contrast' | 'pan' | 'measure' | 'boundingBox') => {
        setActiveMode(mode);
        
        if (!elementRef.current || !isEnabled) return;
        
        // Deactivate all tools first
        cornerstoneTools.setToolActive('Wwwc', { mouseButtonMask: 0 });
        cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 0 });
        cornerstoneTools.setToolActive('Zoom', { mouseButtonMask: 0 });
        cornerstoneTools.setToolActive('Length', { mouseButtonMask: 0 });
        cornerstoneTools.setToolActive('Angle', { mouseButtonMask: 0 });
        cornerstoneTools.setToolActive('RectangleRoi', { mouseButtonMask: 0 });
        
        // Then activate only the ones needed for this mode
        switch (mode) {
            case 'zoom':
                cornerstoneTools.setToolActive('Zoom', { mouseButtonMask: 1 });
                cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 2 });
                break;
            case 'contrast':
                cornerstoneTools.setToolActive('Wwwc', { mouseButtonMask: 1 });
                cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 2 });
                break;
            case 'pan':
                cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 1 });
                cornerstoneTools.setToolActive('Zoom', { mouseButtonMask: 2 });
                break;
            case 'measure':
                cornerstoneTools.setToolActive('Length', { mouseButtonMask: 1 });
                cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 2 });
                break;
            case 'boundingBox':
                cornerstoneTools.setToolActive('RectangleRoi', { mouseButtonMask: 1 });
                cornerstoneTools.setToolActive('Pan', { mouseButtonMask: 2 });
                break;
        }
    }, [isEnabled]);
    // Only update colors when selection changes
    useEffect(() => {
        if (!elementRef.current || !isEnabled || !selectedBoxId) return;

        const element = elementRef.current;
        const toolState = cornerstoneTools.getToolState(element, 'RectangleRoi');
        if(isEnabled && toolState && toolState.data && toolState.data.length > 0 && toolState.data.find((data: any) => data.uuid === selectedBoxId) !== undefined){
            handleModeChange('boundingBox');
            toolState.data.forEach((data: any) => {
                if (data.uuid) {
                    data.color = data.uuid === selectedBoxId ? 'rgb(0, 255, 0)' : 'rgb(255, 255, 0)';
                    data.lineWidth = data.uuid === selectedBoxId ? 3 : 2;
                }
            });
            cornerstone.updateImage(element);
        }
    }, [isEnabled, selectedBoxId, handleModeChange]);
    // Enable cornerstone
    useEffect(() => {
        if (!elementRef.current || isEnabled) return;
        
        const element = elementRef.current;
        
        try {
            cornerstone.enable(element);
            setIsEnabled(true);
            
            // Initialize all cornerstone tools once
            if (!toolsInitializedRef.current) {
                // Add all the tools we'll need
                cornerstoneTools.addTool(cornerstoneTools.WwwcTool);
                cornerstoneTools.addTool(cornerstoneTools.PanTool);
                cornerstoneTools.addTool(cornerstoneTools.ZoomTool);
                cornerstoneTools.addTool(cornerstoneTools.ZoomMouseWheelTool);
                const lengthTool = cornerstoneTools.LengthTool;
                cornerstoneTools.addTool(lengthTool);
                cornerstoneTools.addTool(cornerstoneTools.AngleTool);
                
                // Add RectangleRoi tool for bounding boxes
                cornerstoneTools.addTool(cornerstoneTools.RectangleRoiTool);
            
                // Set default tools
                handleModeChange('zoom');
                
                toolsInitializedRef.current = true;
            }
        } catch (error) {
            console.error('Error enabling cornerstone:', error);
        }
        
        return () => {
            if (element && isEnabled) {
                try {
                    cornerstone.disable(element);
                    setIsEnabled(false);
                } catch (error) {
                    console.error('Error disabling cornerstone:', error);
                }
            }
        };
    }, [isEnabled, handleModeChange, updateBoundingBoxes]);
    
    // Load and display image
    useEffect(() => {
        if (!elementRef.current || !isEnabled) return;
        
        const element = elementRef.current;
        
        try {
            let imageId = imageUrl;
            
            // Handle different URL formats
            if (imageUrl.startsWith('dicom:')) {
                imageId = `wadouri:${imageUrl.substring(6)}`;
            } else if (imageUrl.startsWith('blob:')) {
                imageId = `blob:${imageUrl}`;
            } else if (!imageUrl.includes('://')) {
                imageId = `wadouri:${imageUrl}`;
            }

            // Create event handler for image loaded
            const handleImageLoaded = () => {
               initializeBoundingBoxes();
            };

            element.addEventListener('cornerstoneimagerendered', handleImageLoaded);
            
            cornerstone.loadImage(imageId).then((image: CornerstoneImage) => {
                // Remove pixel spacing information to force "pixels" unit
                if (image.rowPixelSpacing && image.rowPixelSpacing === 1 && image.columnPixelSpacing && image.columnPixelSpacing === 1) {
                    delete image.rowPixelSpacing;
                    delete image.columnPixelSpacing;
                }
                
                imageRef.current = image;
                
                if (element && isEnabled) {
                    cornerstone.displayImage(element, image);
                    
                    // Initialize viewport
                    const viewport = cornerstone.getViewport(element);
                    if (viewport) {
                        viewport.voi = {
                            windowWidth: image.windowWidth || 255,
                            windowCenter: image.windowCenter || 128
                        };
                        cornerstone.setViewport(element, viewport);
                        
                        // Save the initial viewport settings for reset
                        initialViewportRef.current = {
                            scale: viewport.scale,
                            translation: { 
                                x: viewport.translation.x, 
                                y: viewport.translation.y 
                            },
                            voi: {
                                windowWidth: viewport.voi.windowWidth,
                                windowCenter: viewport.voi.windowCenter
                            }
                        };
                    }
                    
                    // Force a resize to make sure image fits properly
                    resizeCornerstoneElement();
                }
            }).catch(error => {
                console.error('Error loading image:', error);
            });

            return () => {
                element.removeEventListener('cornerstoneimagerendered', handleImageLoaded);
            };
        } catch (error) {
            console.error('Error in image loading setup:', error);
        }
    }, [imageUrl, isEnabled, initializeBoundingBoxes, resizeCornerstoneElement]);
    
    // Add this effect to handle keyboard events
    useEffect(() => {
        if (!elementRef.current || !isEnabled) return;
        
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === 'Delete' || e.key === 'Backspace') {
                const element = elementRef.current;
                if (!element) return;
                
                // Get all tools that might have selected annotations
                const tools = ['Length', 'Angle'];
                let redrawNeeded = false;
                
                tools.forEach(toolName => {
                    const toolState = (cornerstoneTools as any).getToolState(element, toolName);
                    if (toolState && toolState.data) {
                        // Find selected/active annotations
                        const selectedData = toolState.data.filter((data: any) => 
                            data.active || (data.handles && Object.values(data.handles).some((handle: any) => 
                                handle && typeof handle === 'object' && handle.active)));
                        
                        if (selectedData.length > 0) {
                            // Remove selected annotations
                            toolState.data = toolState.data.filter((data: any) => 
                                !selectedData.includes(data));
                            redrawNeeded = true;
                        }
                    }
                });
                
                if (redrawNeeded) {
                    (cornerstone as any).updateImage(element);
                }
            }
        };
        
        window.addEventListener('keydown', handleKeyDown);
        
        return () => {
            window.removeEventListener('keydown', handleKeyDown);
        };
    }, [isEnabled]);
    
    // Add event listeners for tool state changes
    useEffect(() => {
        if (!elementRef.current || !isEnabled) return;

        const element = elementRef.current;
        
        function handleMeasurementCompleted() {
            updateBoundingBoxes();
        }

        function handleMeasurementModified() {
            updateBoundingBoxes();
        }

        function handleMeasurementRemoved() {
            updateBoundingBoxes();
        }

        element.addEventListener('cornerstonetoolsmeasurementcompleted', handleMeasurementCompleted);
        element.addEventListener('cornerstonetoolsmeasurementmodified', handleMeasurementModified);
        element.addEventListener('cornerstonetoolsmeasurementremoved', handleMeasurementRemoved);

        return () => {
            element.removeEventListener('cornerstonetoolsmeasurementcompleted', handleMeasurementCompleted);
            element.removeEventListener('cornerstonetoolsmeasurementmodified', handleMeasurementModified);
            element.removeEventListener('cornerstonetoolsmeasurementremoved', handleMeasurementRemoved);
        };
    }, [isEnabled, updateBoundingBoxes]);

    return (
        <Stack>
            <div 
                style={{ 
                    position: 'relative', 
                    width: '100%', 
                    height: height,
                    ...(document.fullscreenElement ? {
                        width: '100vw',
                        height: '100vh', 
                        margin: 0,
                        padding: 0,
                        boxSizing: 'border-box'
                    } : {})
                }}
            >
                <div 
                    ref={elementRef}
                    style={{
                        width: '100%',
                        height: '100%',
                        ...style,
                        ...(document.fullscreenElement ? {
                            width: '100%',
                            height: '100%',
                            margin: 0,
                            padding: 0
                        } : {})
                    }}
                />
                <CornerstoneToolbar 
                    element={elementRef.current} 
                    isEnabled={isEnabled}
                    activeMode={activeMode}
                    onModeChange={handleModeChange}
                    initialViewport={initialViewportRef.current}
                    boundingBoxes={initialBoundingBoxes}
                    onClearBoundingBoxes={() => {
                        if (elementRef.current) {
                            const element = elementRef.current;
                            const toolState = cornerstoneTools.getToolState(element, 'RectangleRoi');
                            if (toolState && toolState.data) {
                                toolState.data = [];
                            }
                            cornerstone.updateImage(element);
                            onBoundingBoxesChange?.([]);
                        }
                    }}
                    onDeleteBox={handleDeleteBox}
                />
            </div>
        </Stack>
    );
}; 