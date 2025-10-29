import React, { useEffect, useState } from 'react';
import { Stack, Nav, INavLinkGroup, INavLink, Text, useTheme, SpinnerSize, Spinner } from '@fluentui/react';
import { useNavigate, useLocation, Routes, Route } from 'react-router-dom';
import { UserManagement } from './UserManagement';
import { ModelsManagement } from './ModelsManagement';
import { ClinicalTasksManagement } from './ClinicalTasksManagement';
import { TestScenariosManagement } from './TestScenariosManagement';
import { ExperimentManagement } from './ExperimentManagement';
import { useResponsive } from '../../hooks/useResponsive';
import { DataManagement } from './DataManagement';
import { DatasetDetails } from './DatasetDetails';
import { DataObjectDetails } from './DataObjectDetails';
import { ExperimentDetail } from './ExperimentDetail';
import { DatasetFilteredView } from './DatasetFilteredView';
import { MetricsManagement } from './MetricsManagement';
import { useAuth } from '../../contexts/AuthContext';
const navLinks: INavLinkGroup[] = [
    {
        links: [
            { key: 'users', name: 'User Management', url: '/admin/users', icon: 'People' },
            { key: 'data', name: 'Data', url: '/admin/data', icon: 'Database' },
            { key: 'models', name: 'Models', url: '/admin/models', icon: 'Robot' },
            { key: 'clinicalTasks', name: 'Clinical Tasks', url: '/admin/clinical-tasks', icon: 'TaskGroup' },
            { key: 'testScenarios', name: 'Experiments', url: '/admin/experiments', icon: 'TestBeaker' },
            { key: 'experiments', name: 'Assignments', url: '/admin/assignments', icon: 'TestPlan' },
            //{ key: 'metrics', name: 'Metrics', url: '/admin/metrics', icon: 'Chart' },
           // { key: 'settings', name: 'Settings', url: '/admin/settings', icon: 'Settings' },
        ],
    },
];

export const Admin: React.FC = () => {
    const { user,loading } = useAuth();
    const isAdmin = !!user?.roles?.includes('admin');
    const [selectedKey, setSelectedKey] = useState<string>('users');
    const theme = useTheme();
    const isMobile = useResponsive('(max-width: 768px)');
    const navigate = useNavigate();
    const location = useLocation();

    useEffect(() => {
        const path = location.pathname.split('/');
        const currentSection = path[2]; // Get the second segment after /admin/
        const matchingLink = navLinks[0].links.find(link => 
            link.url.includes(`/${currentSection}`)
        );
        if (matchingLink?.key) {
            setSelectedKey(matchingLink.key);
        }
    }, [location]);

    const handleNavClick = (_ev?: React.MouseEvent<HTMLElement>, item?: INavLink) => {
        if (_ev) {
            _ev.preventDefault();
        }
        if (item?.url) {
            navigate(item.url, { replace: true });
        }
    };
    if( loading ){
        return <Spinner style={{ marginTop: '20px' }} size={SpinnerSize.large} />;
    }
    if (!isAdmin) {
        return (
            <Stack styles={{ root: { padding: 40 } }}>
                <Text variant="xLarge">Access denied</Text>
                <Text>You do not have permission to view admin pages.</Text>
            </Stack>
        );
    }

    return (
        <Stack horizontal styles={{ root: { height: '100vh' } }}>
            {!isMobile && (
                <Stack.Item styles={{
                    root: {
                        width: 250,
                        borderRight: `1px solid ${theme.palette.neutralLight}`,
                        padding: '20px 0',
                    }
                }}>
                    <Nav
                        groups={navLinks}
                        selectedKey={selectedKey}
                        onLinkClick={handleNavClick}
                    />
                </Stack.Item>
            )}
            <Stack.Item id="adminContent" grow styles={{ root: { overflow: 'auto', padding: 20 } }}>
                {isMobile && (
                    <Stack.Item styles={{ root: { marginBottom: 20 } }}>
                        <Nav
                            groups={navLinks}
                            selectedKey={selectedKey}
                            onLinkClick={handleNavClick}
                        />
                    </Stack.Item>
                )}
                <Routes>
                    <Route path="/" element={<UserManagement />} />
                    <Route path="users" element={<UserManagement />} />
                    <Route path="models" element={<ModelsManagement />} />
                    <Route path="clinical-tasks" element={<ClinicalTasksManagement />} />
                    <Route path="experiments" element={<TestScenariosManagement />} />
                    <Route path="assignments" element={<ExperimentManagement />} />
                    <Route path="experiments/:experimentId" element={<ExperimentDetail />} />
                    <Route path="data" element={<DataManagement />} />
                    <Route path="data/:datasetId" element={<DatasetDetails />} />
                    <Route path="data/:datasetId/object/:objectId" element={<DataObjectDetails />} />
                    <Route path="settings" element={<Text>Settings (Coming Soon)</Text>} />
                    <Route path="metrics" element={<MetricsManagement />} />
                    <Route path="clinical-tasks/dataset/:datasetId/output/:outputIndex" element={<DatasetFilteredView />} />
                    <Route path="clinical-tasks/dataset/:datasetId/generated/:generatedKey" element={<DatasetFilteredView />} />
                    <Route path="*" element={<UserManagement />} />
                </Routes>
            </Stack.Item>
        </Stack>
    );
}; 