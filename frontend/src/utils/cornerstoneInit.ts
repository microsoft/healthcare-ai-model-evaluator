import cornerstone from 'cornerstone-core';
import cornerstoneTools from 'cornerstone-tools';
import cornerstoneMath from 'cornerstone-math';
import cornerstoneWADOImageLoader from 'cornerstone-wado-image-loader';
import dicomParser from 'dicom-parser';

// Create a proxy for cornerstone that intercepts the enable method
const cornerstoneProxy = new Proxy(cornerstone, {
    get(target, prop) {
        if (prop === 'enable') {
            return (element: HTMLElement) => {
                // Set up the element for cornerstone
                element.style.minHeight = '1px';
                element.style.minWidth = '1px';
                element.style.touchAction = 'none';

                // Call the original enable method with modified flags
                const modifiedCornerstone = {
                    ...target,
                    SUPPORT_POINTER_EVENTS: false,
                    SUPPORT_TOUCH_EVENTS: false,
                    SUPPORT_MOUSE_EVENTS: true
                };
                
                const result = target.enable.call(modifiedCornerstone, element);
                
                // Apply our event listeners after enabling cornerstone
                setupEventListeners(element);
                
                return result;
            };
        }
        // Log attempts to access registerImageLoader
        if (prop === 'registerImageLoader') {
            return (scheme: string, loader: any) => {
                console.log('Registering image loader for scheme:', scheme);
                return target.registerImageLoader(scheme, loader);
            };
        }
        // Log attempts to access loadImage
        if (prop === 'loadImage') {
            return (imageId: string) => {
                console.log('Loading image with ID:', imageId);
                console.log('Image ID scheme:', imageId.split(':')[0]);
                // Check both possible locations for loaders
                const loaders = target.imageLoaders || {};
                console.log('Available loaders:', Object.keys(loaders));
                return target.loadImage(imageId);
            };
        }
        return target[prop as keyof typeof cornerstone];
    }
});

// Event listener setup function
const setupEventListeners = (element: HTMLElement) => {
    // const updateView = (element: HTMLElement) => {
    //     const image = cornerstoneProxy.getImage(element);
    //     if (image) {
    //         cornerstoneProxy.draw(element);
    //     }
    // };

    // We still need to prevent default scrolling behavior with wheel events
    element.addEventListener('wheel', (e: WheelEvent) => {
        e.preventDefault();
        
        // Add back the zoom functionality for the wheel
        const delta = e.deltaY;
        const viewport = cornerstone.getViewport(element);
        if (viewport) {
            // Use a smaller value for more controlled zooming
            viewport.scale += (delta < 0) ? 0.05 : -0.05;
            cornerstone.setViewport(element, viewport);
        }
    }, { passive: false });
    
    // REMOVE the custom mouse handling for windowing/contrast
    // REMOVE the mousedown, mousemove, and mouseup handlers that affect window/level
    
    // Just keep the touch events for mobile devices since cornerstone tools might not handle them well
    let lastTouchDistance = 0;

    element.addEventListener('touchstart', (e: TouchEvent) => {
        e.preventDefault();
        if (e.touches.length === 2) {
            const touch1 = e.touches[0];
            const touch2 = e.touches[1];
            lastTouchDistance = Math.sqrt(
                Math.pow(touch2.pageX - touch1.pageX, 2) +
                Math.pow(touch2.pageY - touch1.pageY, 2)
            );
        }
    });

    element.addEventListener('touchmove', (e: TouchEvent) => {
        e.preventDefault();
        if (e.touches.length === 2) {
            const touch1 = e.touches[0];
            const touch2 = e.touches[1];
            const currentDistance = Math.sqrt(
                Math.pow(touch2.pageX - touch1.pageX, 2) +
                Math.pow(touch2.pageY - touch1.pageY, 2)
            );
            
            const viewport = cornerstone.getViewport(element);
            if (viewport) {
                const deltaDistance = currentDistance - lastTouchDistance;
                viewport.scale += deltaDistance * 0.01;
                cornerstone.setViewport(element, viewport);
                lastTouchDistance = currentDistance;
            }
        }
    });
};

// Initialize WADO Image Loader first
cornerstoneWADOImageLoader.external.cornerstone = cornerstone;
cornerstoneWADOImageLoader.external.dicomParser = dicomParser;

// Register image loaders before setting up tools
console.log('Registering image loaders...');

// Register a custom blob loader
const blobLoader = (imageId: string) => {
    // Return a Promise that resolves with the image object
    return {
        promise: new Promise((resolve, reject) => {
            const blobUrl = imageId.replace('blob:', '');
            const image = new Image();
            
            image.onload = () => {
                const canvas = document.createElement('canvas');
                canvas.width = image.width;
                canvas.height = image.height;
                const ctx = canvas.getContext('2d');
                ctx?.drawImage(image, 0, 0);
                
                resolve({
                    imageId,
                    minPixelValue: 0,
                    maxPixelValue: 255,
                    slope: 1.0,
                    intercept: 0,
                    windowCenter: 128,
                    windowWidth: 255,
                    getPixelData: () => {
                        const imageData = ctx?.getImageData(0, 0, canvas.width, canvas.height);
                        return imageData?.data || new Uint8ClampedArray();
                    },
                    rows: image.height,
                    columns: image.width,
                    height: image.height,
                    width: image.width,
                    color: true,
                    rgba: true,
                    columnPixelSpacing: 1,
                    rowPixelSpacing: 1,
                    sizeInBytes: image.width * image.height * 4
                });
            };

            image.onerror = (error) => {
                reject(error);
            };

            image.src = blobUrl;
        }),
        cancelFn: undefined
    };
};

// Register the loaders
const registerLoader = (scheme: string, loader: any) => {
    console.log(`Registering loader for scheme: ${scheme}`);
    try {
        // Make sure the loader returns an object with a promise property
        const wrappedLoader = (imageId: string) => {
            const result = loader(imageId);
            // If the loader returns a Promise directly, wrap it
            if (result instanceof Promise) {
                return { promise: result, cancelFn: undefined };
            }
            return result;
        };
        
        cornerstone.registerImageLoader(scheme, wrappedLoader);
        const loaders = cornerstone.imageLoaders || {};
        console.log(`Available loaders after registering ${scheme}:`, Object.keys(loaders));
    } catch (error) {
        console.error(`Error registering loader for ${scheme}:`, error);
    }
};

// Register the loaders
registerLoader('wadouri', cornerstoneWADOImageLoader.wadouri.loadImage);
registerLoader('dicomweb', cornerstoneWADOImageLoader.wadouri.loadImage);
registerLoader('dicom', cornerstoneWADOImageLoader.wadouri.loadImage);
registerLoader('http', cornerstoneWADOImageLoader.loadImage);
registerLoader('https', cornerstoneWADOImageLoader.loadImage);
registerLoader('blob', blobLoader);

// Initialize cornerstone tools with our proxy
cornerstoneTools.external.cornerstone = cornerstoneProxy;
cornerstoneTools.external.cornerstoneMath = cornerstoneMath;

// Initialize tools
cornerstoneTools.init({
    mouseEnabled: true,
    touchEnabled: false,
    globalToolSyncEnabled: true,
    showSVGCursors: true
});

export {
    cornerstoneProxy as cornerstone,
    cornerstoneTools,
    cornerstoneMath,
    cornerstoneWADOImageLoader,
    dicomParser
};

// No need for ensureCornerstone anymore since initialization happens on import
export const ensureCornerstone = () => {}; // Empty function for backward compatibility 