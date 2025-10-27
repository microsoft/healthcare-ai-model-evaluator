import React from 'react';
import { render, fireEvent, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { BrowserRouter } from 'react-router-dom';
import { DataManagement } from '../../../components/Admin/DataManagement';
import dataReducer from '../../../reducers/dataReducer';

// Mock the auth context
jest.mock('../../../config/authConfig', () => ({
    msalInstance: {
        getActiveAccount: () => ({ idToken: 'mock-token' }),
        acquireTokenSilent: () => Promise.resolve({ accessToken: 'mock-token' })
    },
    apiRequest: {},
    getRuntimeConfig: () => ({ apiBaseUrl: 'http://localhost' })
}));

// Mock services
jest.mock('../../../services/dataSetService', () => ({
    dataSetService: {
        getDataSets: jest.fn().mockResolvedValue([]),
        addDataSet: jest.fn().mockResolvedValue({ id: 'test-id' }),
    }
}));

// Test helper functions for the key path logic
export class KeyPathTestHelpers {
    static getValueFromPath(obj: any, keyPath: { key: string }[]): any {
        if (obj == null) return null;
        return keyPath.reduce((value, pathItem) => {
            if (value === undefined || value === null) return value;
            return value[pathItem.key];
        }, obj);
    }

    static getAvailableKeysForPath(obj: any, currentPath: { key: string; isArray?: boolean }[]): { key: string; type: string }[] {
        if (currentPath.length === 0 && obj != null) {
            return Object.entries(obj).map(([key, value]) => ({
                key,
                type: typeof value === 'object' ? 
                    Array.isArray(value) ? 'array' : 'object' 
                    : typeof value
            }));
        }

        const value = this.getValueFromPath(obj, currentPath);
        const isArrayMapping = currentPath.some(p => p.isArray);

        if (isArrayMapping) {
            if (Array.isArray(value) && value.length > 0) {
                const firstElement = value[0];
                if (typeof firstElement === 'object' && firstElement !== null && !Array.isArray(firstElement)) {
                    return Object.entries(firstElement).map(([key, value]) => ({
                        key,
                        type: typeof value === 'object' ? 
                            Array.isArray(value) ? 'array' : 'object' 
                            : typeof value
                    }));
                }
            }
            return [];
        }

        if (typeof value === 'string' || typeof value === 'number') {
            const parentPath = currentPath.slice(0, -1);
            const parentValue = this.getValueFromPath(obj, parentPath);
            
            return Object.entries(parentValue).map(([key, value]) => ({
                key,
                type: typeof value === 'object' ? 
                    Array.isArray(value) ? 'array' : 'object' 
                    : typeof value
            }));
        }

        if (typeof value === 'object' && value !== null) {
            if (Array.isArray(value)) {
                const indexEntries = value.map((item, index) => ({
                    key: index.toString(),
                    type: typeof item === 'object' ? 
                        Array.isArray(item) ? 'array' : 'object' 
                        : typeof item
                }));
                
                indexEntries.push({
                    key: 'add array',
                    type: 'addArray'
                });
                
                return indexEntries;
            }
            
            return Object.entries(value).map(([key, value]) => ({
                key,
                type: typeof value === 'object' ? 
                    Array.isArray(value) ? 'array' : 'object' 
                    : typeof value
            }));
        }

        return [];
    }
}

const mockStore = configureStore({
    reducer: {
        data: dataReducer,
    },
    preloadedState: {
        data: {
            datasets: [],
            error: null
        }
    }
});

const TestWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <Provider store={mockStore}>
        <BrowserRouter>
            {children}
        </BrowserRouter>
    </Provider>
);

describe('DataManagement Array Mapping Tests', () => {
    describe('KeyPath Helper Functions', () => {
        const testData = {
            tags: ['tag1', 'tag2', 'tag3'],
            images: [
                { url: 'image1.jpg', caption: 'First image' },
                { url: 'image2.jpg', caption: 'Second image' }
            ],
            nestedArrays: [
                { urls: ['url1', 'url2'] },
                { urls: ['url3', 'url4'] }
            ],
            text: 'simple text',
            metadata: {
                title: 'Sample title',
                author: 'John Doe'
            }
        };

        test('getAvailableKeysForPath returns correct root level keys', () => {
            const result = KeyPathTestHelpers.getAvailableKeysForPath(testData, []);
            
            expect(result).toEqual([
                { key: 'tags', type: 'array' },
                { key: 'images', type: 'array' },
                { key: 'nestedArrays', type: 'array' },
                { key: 'text', type: 'string' },
                { key: 'metadata', type: 'object' }
            ]);
        });

        test('getAvailableKeysForPath shows array indices and "add array" option for arrays', () => {
            const result = KeyPathTestHelpers.getAvailableKeysForPath(testData, [{ key: 'tags' }]);
            
            expect(result).toEqual([
                { key: '0', type: 'string' },
                { key: '1', type: 'string' },
                { key: '2', type: 'string' },
                { key: 'add array', type: 'addArray' }
            ]);
        });

        test('getAvailableKeysForPath shows object keys for array of objects', () => {
            const result = KeyPathTestHelpers.getAvailableKeysForPath(testData, [{ key: 'images' }]);
            
            expect(result).toEqual([
                { key: '0', type: 'object' },
                { key: '1', type: 'object' },
                { key: 'add array', type: 'addArray' }
            ]);
        });

        test('getAvailableKeysForPath handles array mapping mode correctly', () => {
            const result = KeyPathTestHelpers.getAvailableKeysForPath(testData, [{ key: 'images', isArray: true }]);
            
            expect(result).toEqual([
                { key: 'url', type: 'string' },
                { key: 'caption', type: 'string' }
            ]);
        });

        test('getAvailableKeysForPath shows nested array structure', () => {
            const result = KeyPathTestHelpers.getAvailableKeysForPath(testData, [{ key: 'nestedArrays', isArray: true }]);
            
            expect(result).toEqual([
                { key: 'urls', type: 'array' }
            ]);
        });

        test('getValueFromPath extracts correct values', () => {
            expect(KeyPathTestHelpers.getValueFromPath(testData, [{ key: 'text' }])).toBe('simple text');
            expect(KeyPathTestHelpers.getValueFromPath(testData, [{ key: 'metadata' }, { key: 'title' }])).toBe('Sample title');
            expect(KeyPathTestHelpers.getValueFromPath(testData, [{ key: 'images' }])).toEqual(testData.images);
        });
    });

    describe('Array Mapping Scenarios', () => {
        const scenarios = [
            {
                name: 'Simple array of strings',
                data: { tags: ['tag1', 'tag2', 'tag3'] },
                path: ['tags'],
                expectedArrayOptions: ['0', '1', '2', 'add array'],
                expectedArrayMapping: true
            },
            {
                name: 'Array of objects',
                data: { 
                    images: [
                        { url: 'img1.jpg', caption: 'First' },
                        { url: 'img2.jpg', caption: 'Second' }
                    ]
                },
                path: ['images'],
                expectedArrayOptions: ['0', '1', 'add array'],
                expectedArrayMapping: true
            },
            {
                name: 'Nested arrays',
                data: {
                    items: [
                        { tags: ['a', 'b'] },
                        { tags: ['c', 'd'] }
                    ]
                },
                path: ['items'],
                expectedArrayOptions: ['0', '1', 'add array'],
                expectedArrayMapping: true
            },
            {
                name: 'Deep nesting with arrays',
                data: {
                    data: {
                        nested: {
                            items: [
                                { value: 'item1' },
                                { value: 'item2' }
                            ]
                        }
                    }
                },
                path: ['data', 'nested', 'items'],
                expectedArrayOptions: ['0', '1', 'add array'],
                expectedArrayMapping: true
            }
        ];

        scenarios.forEach(scenario => {
            test(`handles ${scenario.name}`, () => {
                const path = scenario.path.map(key => ({ key }));
                const result = KeyPathTestHelpers.getAvailableKeysForPath(scenario.data, path);
                const resultKeys = result.map(r => r.key);
                
                expect(resultKeys).toEqual(scenario.expectedArrayOptions);
                expect(resultKeys.includes('add array')).toBe(scenario.expectedArrayMapping);
            });
        });
    });

    describe('UI Component Integration Tests', () => {
        test('renders DataManagement component without crashing', () => {
            render(
                <TestWrapper>
                    <DataManagement />
                </TestWrapper>
            );
            
            expect(screen.getByText('DataSet Management')).toBeInTheDocument();
        });

        test('displays file selection button', () => {
            render(
                <TestWrapper>
                    <DataManagement />
                </TestWrapper>
            );
            
            const addButton = screen.getByText('Add Dataset');
            fireEvent.click(addButton);
            
            expect(screen.getByText('Select JSONL File')).toBeInTheDocument();
        });
    });
});

describe('Array Mapping Edge Cases', () => {
    test('handles empty arrays gracefully', () => {
        const data = { emptyArray: [] };
        const result = KeyPathTestHelpers.getAvailableKeysForPath(data, [{ key: 'emptyArray' }]);
        
        expect(result).toEqual([
            { key: 'add array', type: 'addArray' }
        ]);
    });

    test('handles null values in arrays', () => {
        const data = { mixedArray: ['valid', null, 'another'] };
        const result = KeyPathTestHelpers.getAvailableKeysForPath(data, [{ key: 'mixedArray' }]);
        
        expect(result).toEqual([
            { key: '0', type: 'string' },
            { key: '1', type: 'object' }, // null is typeof 'object'
            { key: '2', type: 'string' },
            { key: 'add array', type: 'addArray' }
        ]);
    });

    test('handles deeply nested object structures', () => {
        const data = {
            level1: {
                level2: {
                    level3: {
                        items: [{ value: 'deep' }]
                    }
                }
            }
        };
        
        const path = [
            { key: 'level1' },
            { key: 'level2' },
            { key: 'level3' },
            { key: 'items' }
        ];
        
        const result = KeyPathTestHelpers.getAvailableKeysForPath(data, path);
        
        expect(result).toEqual([
            { key: '0', type: 'object' },
            { key: 'add array', type: 'addArray' }
        ]);
    });

    test('handles mixed data types in arrays', () => {
        const data = {
            mixedItems: [
                'string',
                123,
                { key: 'object' },
                ['nested', 'array']
            ]
        };
        
        const result = KeyPathTestHelpers.getAvailableKeysForPath(data, [{ key: 'mixedItems' }]);
        
        expect(result).toEqual([
            { key: '0', type: 'string' },
            { key: '1', type: 'number' },
            { key: '2', type: 'object' },
            { key: '3', type: 'array' },
            { key: 'add array', type: 'addArray' }
        ]);
    });
});

// Integration test scenarios for the complete workflow
describe('Complete Array Mapping Workflow Tests', () => {
    const testScenarios = [
        {
            name: 'Medical image dataset with array URLs',
            jsonl: `{"patient_id": "123", "images": [{"url": "xray1.jpg", "type": "chest"}, {"url": "xray2.jpg", "type": "lateral"}], "diagnosis": "normal"}`,
            expectedSteps: [
                { path: [], availableKeys: ['patient_id', 'images', 'diagnosis'] },
                { path: ['images'], availableKeys: ['0', '1', 'add array'] },
                { path: ['images'], arrayMode: true, availableKeys: ['url', 'type'] }
            ]
        },
        {
            name: 'Text classification with tag arrays',
            jsonl: `{"text": "sample text", "tags": ["medical", "urgent", "followup"], "metadata": {"priority": "high"}}`,
            expectedSteps: [
                { path: [], availableKeys: ['text', 'tags', 'metadata'] },
                { path: ['tags'], availableKeys: ['0', '1', '2', 'add array'] }
            ]
        },
        {
            name: 'Complex nested structure',
            jsonl: `{"data": {"patients": [{"info": {"name": "John"}, "visits": [{"date": "2024-01-01", "notes": "good"}]}]}}`,
            expectedSteps: [
                { path: [], availableKeys: ['data'] },
                { path: ['data'], availableKeys: ['patients'] },
                { path: ['data', 'patients'], availableKeys: ['0', 'add array'] },
                { path: ['data', 'patients'], arrayMode: true, availableKeys: ['info', 'visits'] }
            ]
        }
    ];

    testScenarios.forEach(scenario => {
        test(`handles ${scenario.name}`, () => {
            const jsonData = JSON.parse(scenario.jsonl);
            
            scenario.expectedSteps.forEach(step => {
                const path = step.path.map(key => ({ 
                    key, 
                    isArray: step.arrayMode 
                }));
                
                const result = KeyPathTestHelpers.getAvailableKeysForPath(jsonData, path);
                const resultKeys = result.map(r => r.key);
                
                step.availableKeys.forEach(expectedKey => {
                    expect(resultKeys).toContain(expectedKey);
                });
            });
        });
    });
}); 