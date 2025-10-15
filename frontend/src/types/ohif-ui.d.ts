declare module '@ohif/ui' {
    import { FC } from 'react';

    export interface CornerstoneViewportProps {
        imageIds: string[];
        viewportIndex: number;
        tools: Array<{
            name: string;
            mode: string;
        }>;
        style?: React.CSSProperties;
    }

    export const CornerstoneViewport: FC<CornerstoneViewportProps>;

    // Add other OHIF components and types as needed
}

declare module 'dcmjs' {
    const dcmjs: any;
    export default dcmjs;
}

declare module 'gl-matrix' {
    const glMatrix: any;
    export default glMatrix;
} 