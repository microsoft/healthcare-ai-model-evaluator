declare module 'cornerstone-core' {
    interface ImageCache {
        cachedImages: any[];
    }

    interface ImageLoadObject {
        [key: string]: any;
    }

    interface Viewport {
        scale: number;
        translation: {
            x: number;
            y: number;
        };
        voi: {
            windowWidth: number;
            windowCenter: number;
        };
        invert: boolean;
        pixelReplication: boolean;
        rotation: number;
        hflip: boolean;
        vflip: boolean;
    }

    interface CornerstoneStatic {
        enable: (element: HTMLElement) => void;
        disable: (element: HTMLElement) => void;
        loadImage: (imageId: string) => Promise<any>;
        displayImage: (element: HTMLElement, image: any) => void;
        registerImageLoader: (scheme: string, loader: any) => void;
        imageCache: ImageCache;
        SUPPORT_POINTER_EVENTS?: boolean;
        SUPPORT_TOUCH_EVENTS?: boolean;
        SUPPORT_MOUSE_EVENTS?: boolean;
        getImage: (element: HTMLElement) => any;
        draw: (element: HTMLElement) => void;
        imageLoadObject?: ImageLoadObject;
        imageLoaders?: { [key: string]: any };
        getViewport: (element: HTMLElement) => Viewport;
        setViewport: (element: HTMLElement, viewport: Viewport) => void;
        resize: (element: HTMLElement) => void;
        updateImage(element: HTMLElement): void;
    }

    const cornerstone: CornerstoneStatic;
    export default cornerstone;
}

declare module 'cornerstone-tools' {
    interface Tool {
        name: string;
        mode: string;
        modeOptions?: {
            mouseButtonMask?: number;
        };
    }

    interface ToolOptions {
        mouseButtonMask: number;
        isTouchActive?: boolean;
    }

    type ToolName = 'Wwwc' | 'Pan' | 'Zoom' | 'Length' | 'Angle';
    type ToolClassName = `${ToolName}Tool`;

    interface CornerstoneToolsStatic {
        init: (config?: any) => void;
        addTool: (tool: any) => void;
        setToolActive: (toolName: string, options: ToolOptions) => void;
        external: {
            cornerstone: any;
            cornerstoneMath: any;
        };
        [key: `${ToolName}Tool`]: any;
        WwwcTool: any;
        PanTool: any;
        ZoomTool: any;
        LengthTool: any;
        AngleTool: any;
        ZoomMouseWheelTool: any;
        getToolState(element: HTMLElement, toolName: string): any;
        setToolState(element: HTMLElement, toolName: string, toolState: any): void;
        addToolState(element: HTMLElement, toolName: string, measurementData: any): void;
        setConfiguration(configuration: any): void;
        EraseSegmentationTool: any;
        RectangleRoiTool: any;
    }

    const cornerstoneTools: CornerstoneToolsStatic;
    export default cornerstoneTools;
}

declare module 'cornerstone-math' {
    const cornerstoneMath: any;
    export default cornerstoneMath;
}

declare module 'cornerstone-wado-image-loader' {
    const cornerstoneWADOImageLoader: {
        external: {
            cornerstone: any;
            dicomParser: any;
        };
        loadImage: any;
        wadouri: {
            loadImage: any;
        };
        configure: (config: {
            beforeSend?: (xhr: XMLHttpRequest) => void;
            [key: string]: any;
        }) => void;
    };
    export default cornerstoneWADOImageLoader;
}

declare module 'dicom-parser' {
    const dicomParser: any;
    export default dicomParser;
} 