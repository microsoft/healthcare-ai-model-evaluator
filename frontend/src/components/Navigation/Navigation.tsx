import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
    Stack,
    Image,
    Text,
    Separator,
    PersonaCoin,
    IButtonStyles,
    CommandButton,
    ContextualMenu,
    IContextualMenuItem,
    useTheme,
    PersonaSize,
    IconButton,
    IIconProps,
} from '@fluentui/react';
import { useAuth } from '../../contexts/AuthContext';
import './Navigation.scss';
import { useResponsive } from '../../hooks/useResponsive';

const NAV_ITEMS = [
    { key: 'arena', text: 'Arena', path: '/arena' },
    { key: 'rankings', text: 'Rankings', path: '/rankings' },
    { key: 'admin', text: 'Admin', path: '/admin', adminOnly: true },
];

const isPathActive = (navPath: string, currentPath: string) => {
    if (navPath === '/admin/users') {
        return currentPath.startsWith('/admin');
    }
    return currentPath.includes(navPath);
};

const menuIcon: IIconProps = { iconName: 'GlobalNavButton' };

export const Navigation: React.FC = () => {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const theme = useTheme();
    const [showProfileMenu, setShowProfileMenu] = React.useState(false);
    const isMobile = useResponsive('(max-width: 768px)');
    const [isMenuOpen, setIsMenuOpen] = React.useState(false);

    if (!user) return null;

    const isAdmin = user.roles.includes('admin');

    const getButtonStyles = (isActive: boolean): IButtonStyles => ({
        root: {
            height: '50px',
            width: '100px',
            padding: '0 20px',
            fontSize: '20px',
            fontFamily: theme.fonts.large.fontFamily,
        },
        label: {
            fontWeight: isActive ? '600' : '400',
            borderBottom: isActive ? `2px solid ${theme.palette.themePrimary}` : 'none',
            paddingBottom: isActive ? '8px' : '5px',
            marginTop: isActive ? '4px' : '0px',
        },
    });

    const profileMenuItems: IContextualMenuItem[] = [
        {
            key: 'username',
            text: user.email,
            onClick: () => {
                
            },
        },
        {
            key: 'logout',
            text: 'Logout',
            onClick: () => {
                logout();
            },
        },
    ];

    const navMenuItems: IContextualMenuItem[] = NAV_ITEMS
        .filter(item => !item.adminOnly || isAdmin)
        .map(item => ({
            key: item.key,
            text: item.text,
            onClick: () => {
                navigate(item.path);
                return void 0;
            },
            style: isPathActive(item.path, location.pathname) ? {
                fontWeight: '600',
                borderBottom: `2px solid ${theme.palette.themePrimary}`
            } : undefined
        }));

    return (
        <Stack
            horizontal
            verticalAlign="center"
            className="nav-container"
            id="navigationbar"
            styles={{
                root: {
                    borderRadius: '10px 10px 0px 0px',
                    height: '60px',
                    padding: '0 20px',
                    boxShadow: theme.effects.elevation4,
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    backgroundColor: theme.palette.white,
                    zIndex: 100,
                    minWidth: 'min-content',
                }
            }}
        >
            {/* Logo Section */}
            <Stack
                horizontal
                verticalAlign="center"
                onClick={() => navigate('/')}
                className="nav-logo"
            >
                <Image
                    src="/logo.png"
                    alt="Haime Logo"
                    width={30}
                    height={30}
                />
                <Text
                    variant="large"
                    className="nav-logo-text"
                    styles={{
                        root: {
                            marginLeft: '5px',
                            fontSize: '20px',
                            fontFamily: theme.fonts.large.fontFamily,
                            verticalAlign: 'text-top'
                        }
                    }}
                >
                    HAIME
                </Text>
            </Stack>

            {/* Vertical Divider */}
            <Separator
                vertical
                styles={{
                    root: {
                        height: '30px',
                        color: theme.palette.neutralSecondary,
                        margin: '0 20px',
                    }
                }}
            />

            {/* Navigation Items - Responsive */}
            {isMobile ? (
                <Stack.Item>
                    <IconButton
                        iconProps={menuIcon}
                        onClick={() => setIsMenuOpen(!isMenuOpen)}
                        className="nav-menu-button"
                    />
                    <ContextualMenu
                        items={navMenuItems}
                        hidden={!isMenuOpen}
                        target=".nav-menu-button"
                        onDismiss={() => setIsMenuOpen(false)}
                    />
                </Stack.Item>
            ) : (
                <Stack horizontal tokens={{ childrenGap: 10 }}>
                    {NAV_ITEMS.filter(item => !item.adminOnly || isAdmin).map(item => (
                        <CommandButton
                            key={item.key}
                            text={item.text}
                            onClick={() => navigate(item.path)}
                            styles={getButtonStyles(isPathActive(item.path, location.pathname))}
                        />
                    ))}
                </Stack>
            )}

            {/* Profile Section - Always visible */}
            <Stack.Item grow={true}>
                <Stack horizontal horizontalAlign="end">
                    <PersonaCoin
                        text={user.email}
                        size={PersonaSize.size32}
                        onClick={() => setShowProfileMenu(true)}
                        className="profile-button"
                    />
                    <ContextualMenu
                        items={profileMenuItems}
                        hidden={!showProfileMenu}
                        target=".profile-button"
                        onDismiss={() => setShowProfileMenu(false)}
                    />
                </Stack>
            </Stack.Item>
        </Stack>
    );
}; 