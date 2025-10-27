import { DataSet } from './dataset';

export interface DataSetService {
    getDataSets: () => Promise<DataSet[]>;
    addDataSet: (dataset: Omit<DataSet, 'id'>) => Promise<DataSet>;
    updateDataSet: (dataset: DataSet) => Promise<DataSet>;
    deleteDataSet: (id: string) => Promise<void>;
} 