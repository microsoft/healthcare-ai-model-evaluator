import React from 'react';
import { useNavigate } from 'react-router-dom';
import {
    Stack,
    Text,
    FontIcon,
    mergeStyles,
    ITheme,
    useTheme,
    initializeIcons,
    IStackTokens,
} from '@fluentui/react';
import { useAuth } from '../../contexts/AuthContext';


// Initialize icons
initializeIcons();

interface DashboardTileProps {
    icon: string;
    title: string;
    description: string;
    path: string;
}



const tiles: DashboardTileProps[] = [
    {
        icon: 'Ribbon',
        title: 'Arena',
        description: 'Test and validate AI models in various evaluation modes. Compare performance and assess model capabilities.',
        path: '/arena'
    },
    {
        icon: 'People',
        title: 'User Management',
        description: 'Manage user accounts, roles, and permissions for system access.',
        path: '/admin/users'
    },
    {
        icon: 'Robot',
        title: 'Models',
        description: 'Configure and manage AI models for various clinical tasks and evaluations.',
        path: '/admin/models'
    },
    {
        icon: 'TaskGroup',
        title: 'Clinical Tasks',
        description: 'Define and organize clinical tasks for AI model evaluation and testing.',
        path: '/admin/clinical-tasks'
    },
    {
        icon: 'TestBeaker',
        title: 'Experiments',
        description: 'Create and manage experiments to evaluate model performance.',
        path: '/admin/experiments'
    },
    {
        icon: 'TestPlan',
        title: 'Assignments',
        description: 'Assign and run experiments to assess model capabilities and performance.',
        path: '/admin/assignments'
    },
    {
        icon: 'Database',
        title: 'Data Management',
        description: 'Manage and organize datasets used for model training and evaluation.',
        path: '/admin/data'
    },
    {
        icon: 'Trophy',
        title: 'Rankings',
        description: 'View and analyze results from model evaluations and experiments.',
        path: '/rankings'
    }
];



const getTileStyles = (theme: ITheme) => mergeStyles({
    width: 300,
    height: 250,
    padding: '50px 20px 20px 20px',
    margin: 10,
    backgroundColor: theme.palette.white,
    boxShadow: theme.effects.elevation4,
    borderRadius: theme.effects.roundedCorner2,
    cursor: 'pointer',
    transition: 'all 0.2s ease-in-out',
    selectors: {
        ':hover': {
            transform: 'translateY(-4px)',
            boxShadow: theme.effects.elevation8,
            '.icon': {
                color: theme.palette.themePrimary,
                transform: 'scale(1.1)',
            }
        }
    }
});

const iconStyles = mergeStyles({
    fontSize: 36,
    marginBottom: 15,
    transition: 'all 0.2s ease-in-out',
});

const DashboardTile: React.FC<DashboardTileProps> = ({ icon, title, description, path }) => {
    const theme = useTheme();
    const navigate = useNavigate();
    const tileStyles = getTileStyles(theme);

    return (
        <Stack
            className={tileStyles}
            onClick={() => navigate(path)}
            tokens={{ childrenGap: 10 }}
            styles={{
                root: {
                    textAlign: 'center',
                }
            }}
        >
            <FontIcon iconName={icon} className={mergeStyles(iconStyles, 'icon')} />
            <Text variant="xLarge" block styles={{ root: { fontWeight: 600 } }}>
                {title}
            </Text>
            <Text variant="medium" styles={{ root: { color: theme.palette.neutralSecondary } }}>
                {description}
            </Text>
        </Stack>
    );
};

// Add these style definitions
const stackTokens: IStackTokens = {
    childrenGap: 35,
    padding: 20,
};

export const Dashboard: React.FC = () => {
    // Lightweight role check from context is not available here; use URL based restrictions later if needed.
    // We can still selectively render based on a simple auth hook.
    const { user } = useAuth();
    const isAdmin = user?.roles?.includes('admin') || false;
    const visibleTiles = isAdmin ? tiles : tiles.filter(t => t.title === 'Arena' || t.title === 'Rankings');
    return (
        <Stack tokens={stackTokens}>
            <Stack.Item align="center">
                <Text variant="xxLarge">Dashboard</Text>
            </Stack.Item>

            <Stack.Item align="center">
                <Text variant="large" styles={{ root: { maxWidth: 900, textAlign: 'center' } }}>
                    Welcome to Healthcare AI Model Evaluator. Choose an option below to get started.
                </Text>
            </Stack.Item>

            <Stack.Item align="center">
                <Stack 
                    horizontal 
                    wrap 
                    horizontalAlign="center" 
                    tokens={{ childrenGap: 35 }}
                    styles={{
                        root: {
                            maxWidth: 1020, // (320px * 3) + (20px gap * 2)
                            margin: '0 auto'
                        }
                    }}
                >
                    {visibleTiles.map((tile, index) => (
                        <Stack.Item 
                            key={index} 
                            styles={{ 
                                root: { 
                                    width: 320,
                                    margin: '10px 5px',
                                    flex: '0 0 320px' // This prevents the items from growing or shrinking
                                } 
                            }}
                        >
                            <DashboardTile {...tile} />
                        </Stack.Item>
                    ))}
                </Stack>
            </Stack.Item>
        </Stack>
    );
}; 