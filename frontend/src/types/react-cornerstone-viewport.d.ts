declare module 'react-cornerstone-viewport' {
    import { FC } from 'react';

    interface Tool {
        name: string;
        mode: string;
        modeOptions?: {
            mouseButtonMask?: number;
        };
    }

    interface CornerstoneViewportProps {
        tools: Tool[];
        imageIds: string[];
        style?: React.CSSProperties;
        onElementEnabled?: (elementEnabledEvt: any) => void;
    }

    const CornerstoneViewport: FC<CornerstoneViewportProps>;
    export default CornerstoneViewport;
} 