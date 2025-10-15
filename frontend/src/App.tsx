import React, { useEffect, useState } from 'react';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { Provider } from 'react-redux';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { store } from './store/store';
import { msalConfig } from './config/authConfig';
import { Login } from './components/Login/Login';
import { Navigation } from './components/Navigation/Navigation';
import { Admin } from './components/Admin/Admin';
import { Arena } from './components/Arena/Arena';
import { Metrics } from './components/Rankings/Metrics';
import { useAuth } from './contexts/AuthContext';
import './App.scss';
import { AuthProvider } from './contexts/AuthContext';
import { initializeIcons, Spinner, SpinnerSize } from '@fluentui/react';
import { Toaster } from 'react-hot-toast';
//import { Rankings } from './components/Rankings/Rankings';
import { useMongoUserId } from './hooks/useMongoUserId';

const msalInstance = new PublicClientApplication(msalConfig);

// Initialize icons
initializeIcons();

// Protected Route Component
const ProtectedRoute: React.FC<{ children: React.ReactNode; roles?: string[] }> = ({ children, roles }) => {
    const { isAuthenticated, user, loading } = useAuth();
    const hasRequiredRole = !roles || (user && roles.some(role => user.roles.includes(role)));
    console.log("ProtectedRoute - isAuthenticated:", isAuthenticated, "hasRequiredRole:", hasRequiredRole, "isloading:", loading);
    return <>{children}</>;
};

// Initialize MSAL before rendering
const App: React.FC = () => {
    const [isInitialized, setIsInitialized] = useState(false);

    useEffect(() => {
        const initialize = async () => {
            await msalInstance.initialize();
            setIsInitialized(true);
        };
        initialize();
    }, []);

    useMongoUserId(); // This will set up the mongoUserId in the store

    if (!isInitialized) {
        return <Spinner style={{ marginTop: '20px' }} size={SpinnerSize.large} />;
    }

    return (
        <MsalProvider instance={msalInstance}>
            <AuthProvider>
                <Provider store={store}>
                    <Toaster position="top-right" />
                    <Router>
                        <div className="app">
                            <Navigation />
                            <main className="main-content">
                                <Routes>
                                    <Route path="/" element={<Login />} />
                                    <Route path="/reset-password" element={<Login />} />
                                    <Route path="/arena/*" element={<ProtectedRoute><Arena /></ProtectedRoute>} />
                                    <Route path="/rankings" element={<ProtectedRoute><Metrics /></ProtectedRoute>} />
                                    <Route path="/admin/*" element={<ProtectedRoute><Admin /></ProtectedRoute>} />
                                </Routes>
                            </main>
                        </div>
                    </Router>
                </Provider>
            </AuthProvider>
        </MsalProvider>
    );
};

export default App;
