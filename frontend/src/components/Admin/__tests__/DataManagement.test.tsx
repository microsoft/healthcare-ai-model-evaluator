import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import DataManagement from '../DataManagement';
import { dataReducer } from '../../../reducers/dataReducer';
import { dataService } from '../../../services/dataService';
import { BrowserRouter } from 'react-router-dom';
import '@testing-library/jest-dom';

// Mock the data service
jest.mock('../../../services/dataService');

describe('DataManagement Component', () => {
    let store;

    beforeEach(() => {
        store = configureStore({
            reducer: {
                data: dataReducer
            }
        });

        // Reset all mocks before each test
        jest.clearAllMocks();
    });

    const renderWithProviders = (component) => {
        return render(
            <Provider store={store}>
                <BrowserRouter>
                    {component}
                </BrowserRouter>
            </Provider>
        );
    };

    it('should render the data management component', () => {
        renderWithProviders(<DataManagement />);
        expect(screen.getByText('Data Management')).toBeInTheDocument();
    });

    it('should load datasets on mount', async () => {
        const mockDataSets = [
            { id: '1', name: 'Test Dataset 1' },
            { id: '2', name: 'Test Dataset 2' }
        ];

        (dataService.getAllDataSets as jest.Mock).mockResolvedValue(mockDataSets);

        renderWithProviders(<DataManagement />);

        await waitFor(() => {
            expect(screen.getByText('Test Dataset 1')).toBeInTheDocument();
            expect(screen.getByText('Test Dataset 2')).toBeInTheDocument();
        });
    });

    it('should handle dataset creation', async () => {
        const mockNewDataSet = {
            id: '3',
            name: 'New Dataset',
            description: 'Test description'
        };

        (dataService.createDataSet as jest.Mock).mockResolvedValue(mockNewDataSet);

        renderWithProviders(<DataManagement />);

        // Click create button
        fireEvent.click(screen.getByText('Create Dataset'));

        // Fill in form
        fireEvent.change(screen.getByLabelText('Name'), {
            target: { value: 'New Dataset' }
        });
        fireEvent.change(screen.getByLabelText('Description'), {
            target: { value: 'Test description' }
        });

        // Submit form
        fireEvent.click(screen.getByText('Save'));

        await waitFor(() => {
            expect(dataService.createDataSet).toHaveBeenCalledWith({
                name: 'New Dataset',
                description: 'Test description'
            });
        });
    });

    it('should handle dataset deletion', async () => {
        const mockDataSet = {
            id: '1',
            name: 'Test Dataset'
        };

        (dataService.getAllDataSets as jest.Mock).mockResolvedValue([mockDataSet]);
        (dataService.deleteDataSet as jest.Mock).mockResolvedValue(true);

        renderWithProviders(<DataManagement />);

        await waitFor(() => {
            expect(screen.getByText('Test Dataset')).toBeInTheDocument();
        });

        // Click delete button
        fireEvent.click(screen.getByLabelText('Delete dataset'));

        // Confirm deletion
        fireEvent.click(screen.getByText('Yes'));

        await waitFor(() => {
            expect(dataService.deleteDataSet).toHaveBeenCalledWith('1');
        });
    });

    it('should handle errors gracefully', async () => {
        (dataService.getAllDataSets as jest.Mock).mockRejectedValue(
            new Error('Failed to load datasets')
        );

        renderWithProviders(<DataManagement />);

        await waitFor(() => {
            expect(screen.getByText('Error loading datasets')).toBeInTheDocument();
        });
    });
}); 