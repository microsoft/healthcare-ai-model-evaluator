import axios from './axiosConfig';

export const getImage = async (imageId: string): Promise<string> => {
    const response = await axios.get(`/api/images/${imageId}`, {
        responseType: 'blob'
    });
    return URL.createObjectURL(response.data);
}; 