import React, { useEffect, useState } from 'react';
import {
    Stack,
    DetailsList,
    Selection,
    SelectionMode,
    DetailsListLayoutMode,
    IColumn,
    PrimaryButton,
    DefaultButton,
    Panel,
    TextField,
    Checkbox,
    MarqueeSelection,
    Text,
    ICommandBarItemProps,
    Dropdown,
    MessageBar,
    MessageBarType,
    IDropdownOption,
    IObjectWithKey,
    ISelection,
    Icon,
} from '@fluentui/react';
import { User, UserExpertise } from '../../types/auth.types';
import { UserFormData, AVAILABLE_ROLES } from '../../types/user';
import './UserManagement.scss';
import { useAppDispatch, useAppSelector } from '../../store/store';
import { RootState } from '../../store/store';
import { FloatingCommandBar } from './FloatingCommandBar'
import {
    setUsers,
    addUser,
    updateUser,
    deleteUsers,
    setSelectedUsers,
    toggleFlyout,
    setEditingUser,
} from '../../reducers/userReducer';
import { userService } from '../../services/userService';
// eslint-disable-next-line
import { toast,Toaster } from 'react-hot-toast';
import { IModel } from '../../types/admin';
import { fetchModels } from '../../reducers/modelReducer';
import { validatePasswordComplexity } from '../../utils/passwordValidation';
const commandButtonStyles = {
    // root: { 
    //     backgroundColor: '#0078d4',
    //     height: '32px',
    //     marginRight: '5px',
    // },
    // rootHovered: { 
    //     backgroundColor: '#106ebe'
    // },
    // rootPressed: { 
    //     backgroundColor: '#005a9e'
    // },
    // label: { 
    //     color: 'white',
    //     fontSize: '14px',
    // },
    // rootDisabled: {
    //     backgroundColor: '#f3f2f1',
    //     marginRight: '5px',
    // },
    // labelDisabled: {
    //     color: '#a19f9d',
    // },
};

const EXPERTISE_OPTIONS = [
    { key: 'null', text: 'None' },
    { key: 'provider', text: 'Provider' }
];

export const UserManagement: React.FC = () => {
    const dispatch = useAppDispatch();
    const { users, selectedUsers, editingUser, isFlyoutOpen } = useAppSelector((state: RootState) => state.user);
    const { models } = useAppSelector((state: RootState) => state.models);
    const [formData, setFormData] = useState<UserFormData>({
        name: '',
        email: '',
        roles: [],
        expertise: null,
        isModelReviewer: false,
        modelId: ''
    });
    const [formErrors, setFormErrors] = useState<Partial<UserFormData>>({});
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [orgForInvite, setOrgForInvite] = useState<string>('Haime');
    const [formMode, setFormMode] = useState<'edit' | 'password'>('edit');
    const [selection] = useState<ISelection<IObjectWithKey>>(new Selection<IObjectWithKey>({
        onSelectionChanged: () => {
            const selectedItems = selection.getSelection() as User[];
            dispatch(setSelectedUsers(selectedItems.map(user => user.id)));
        },
        getKey: (item: IObjectWithKey) => (item as User).id,
    }));
    const [emailEnabled, setEmailEnabled] = useState<boolean>(false);
    const [newPassword, setNewPassword] = useState<string>('');
    const [passwordErrors, setPasswordErrors] = useState<string[]>([]);
    
    const validatePassword = (password: string, email: string, name: string) => {
        const validation = validatePasswordComplexity(password, email?.split('@')[0], name);
        setPasswordErrors(validation.errors);
        return validation.isValid;
    };
    
    useEffect(() => {
        fetch('/api/auth/config')
            .then(r => r.ok ? r.json() : { emailEnabled: false })
            .then(cfg => setEmailEnabled(!!cfg.emailEnabled))
            .catch(() => setEmailEnabled(false));
    }, []);

    const handleSubmit = async () => {
        const validateForm = (): boolean => {
            const errors: Partial<UserFormData> = {};
            if (!formData.name) errors.name = 'Name is required';
            if (!formData.email) errors.email = 'Email is required';
            if (formData.roles.length === 0) errors.roles = ['At least one role is required'];
            
            setFormErrors(errors);
            return Object.keys(errors).length === 0;
        };

        if (!validateForm()) return;

        const userData: User = {
            id: editingUser?.id || Date.now().toString(),
            ...formData,
        };

        setIsLoading(true);
        try {
            if (editingUser) {
                const updatedUser = await userService.updateUser(userData);
                dispatch(updateUser(updatedUser));
            } else {
                const newUser = await userService.createUser(userData);
                dispatch(addUser(newUser));
            }
            dispatch(setEditingUser(null));
            dispatch(toggleFlyout());
            setFormData({
                name: '',
                email: '',
                roles: [],
                expertise: null
            });
            setError(null);
            toast.success('User updated successfully');
        } catch (err) {
            setError(editingUser ? 'Failed to update user' : 'Failed to create user');
            console.error(err);
        } finally {
            setIsLoading(false);
        }
    };

    const handleDelete = async (userIds: string[]) => {
        setIsLoading(true);
        try {
            for (const id of userIds) {
                await userService.deleteUser(id);
            }
            dispatch(deleteUsers(userIds));
            selection.setAllSelected(false);
            dispatch(setSelectedUsers([]));
            setError(null);
        } catch (err) {
            setError('Failed to delete users');
            console.error(err);
        } finally {
            setIsLoading(false);
        }
    };

    const commandItems: ICommandBarItemProps[] = [
        {
            key: 'add',
            text: 'New User',
            onClick: () => {
                setFormMode('edit');
                setFormData({ name: '', email: '', roles: [], expertise: null });
                dispatch(toggleFlyout());
            },
            iconProps: { iconName: 'Add' },
            buttonStyles: commandButtonStyles,
        },
    emailEnabled ? {
            key: 'sendReset',
            text: 'Send Password Setup',
            onClick: async () => {
                // Use selected user or form email if adding
                const targetEmail = selectedUsers.length === 1
                    ? (users.find(u => u.id === selectedUsers[0])?.email || '')
                    : formData.email;
                if (!targetEmail) {
                    setError('Select a user or enter an email first');
                    return;
                }
                setIsLoading(true);
                try {
                    await userService.sendPasswordSetupEmail(targetEmail, orgForInvite || 'Haime');
                    toast.success('Password setup email sent');
                } catch (e) {
                    setError('Failed to send password setup email');
                    console.error(e);
                } finally {
                    setIsLoading(false);
                }
            },
            disabled: (selectedUsers.length !== 1 && !formData.email) || isLoading,
            buttonStyles: commandButtonStyles,
    } : undefined,
        {
            key: 'edit',
            text: 'Edit',
            iconProps: { iconName: 'Edit' },
            onClick: () => {
                const selectedUser = users.find((user: User) => user.id === selectedUsers[0]);
                 setFormMode('edit');
                if (selectedUser) {
                    setFormData({
                        name: selectedUser.name,
                        email: selectedUser.email,
                        roles: selectedUser.roles,
                        expertise: selectedUser.expertise ?? null,
                        isModelReviewer: selectedUser.isModelReviewer,
                        modelId: selectedUser.modelId
                    });
                    dispatch(setEditingUser(selectedUser));
                }
            },
            disabled: selectedUsers.length !== 1,
            buttonStyles: commandButtonStyles,
        },
        {
            key: 'delete',
            iconProps: { iconName: 'Delete' },
            text: 'Delete',
            onClick: () => {
                if (selectedUsers.length > 0) {
                    handleDelete(selectedUsers);
                }
            },
            disabled: selectedUsers.length === 0 || isLoading,
            buttonStyles: commandButtonStyles,
        },
        {
            key:'setPassword',
            text: 'Set Password',
            iconProps: { iconName: 'PasswordField' },
            onClick: () => {
                const selectedUser = users.find((user: User) => user.id === selectedUsers[0]);
                setFormMode('password');
                 if (selectedUser) {
                    setFormData({
                        name: selectedUser.name,
                        email: selectedUser.email,
                        roles: selectedUser.roles,
                        expertise: selectedUser.expertise ?? null,
                        isModelReviewer: selectedUser.isModelReviewer,
                        modelId: selectedUser.modelId
                    });
                    dispatch(setEditingUser(selectedUser));
                }
            }
        }
    ].filter(Boolean) as ICommandBarItemProps[];

    const columns: IColumn[] = [
        {
            key: 'name',
            name: 'Name',
            fieldName: 'name',
            minWidth: 100,
            maxWidth: 200,
            isResizable: true,
        },
        {
            key: 'email',
            name: 'Email',
            fieldName: 'email',
            minWidth: 200,
            maxWidth: 300,
            isResizable: true,
        },
        {
            key: 'roles',
            name: 'Roles',
            fieldName: 'roles',
            minWidth: 200,
            isResizable: true,
            onRender: (item: User) => item.roles.join(', '),
        },
        {
            key: 'expertise',
            name: 'Expertise',
            fieldName: 'expertise',
            minWidth: 100,
            isResizable: true,
            onRender: (item: User) => item.expertise || 'None',
        },
        {
            key: 'isModelReviewer',
            name: 'Model',
            fieldName: 'isModelReviewer',
            minWidth: 100,
            isResizable: true,
            onRender: (item: User) => item.isModelReviewer ? (item.modelId ? models.find((model: IModel) => model.id === item.modelId)?.name : 'No model selected') : 'Human Reviewer',
        }
    ];

    useEffect(() => {
        const loadUsers = async () => {
            setIsLoading(true);
            try {
                const users = await userService.getUsers();
                dispatch(setUsers(users));
                setError(null);
            } catch (error) {
                setError('Failed to load users');
                console.error('Failed to load users:', error);
            } finally {
                setIsLoading(false);
            }
        };
        loadUsers();
    }, [dispatch]);

    useEffect(() => {
        dispatch(fetchModels());
    }, [dispatch]);

    const modelOptions: IDropdownOption[] = [
        { key: '', text: 'None' },
        ...models.map(model => ({
            key: model.id,
            text: model.name
        }))
    ];

    if (error) {
        // You might want to add a MessageBar component from Fluent UI
    }

    return (
        <Stack tokens={{ childrenGap: 20 }} styles={{ root: { padding: 20 } }}>
            {error && (
                <MessageBar messageBarType={MessageBarType.error}>
                    {error}
                </MessageBar>
            )}
            
            <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                <Text variant="xxLarge">User Management</Text>
            </Stack>

            <FloatingCommandBar
                items={commandItems}
                parentId='adminContent'
                stickOffsetId='navigationbar'
            />

            <MarqueeSelection selection={selection}>
                <DetailsList
                    items={users}
                    columns={columns}
                    selection={selection}
                    selectionMode={SelectionMode.multiple}
                    setKey="set"
                    layoutMode={DetailsListLayoutMode.justified}
                    selectionPreservedOnEmptyClick={true}
                />
            </MarqueeSelection>

            <Panel
                isOpen={isFlyoutOpen}
                onDismiss={() => dispatch(toggleFlyout())}
                headerText={editingUser ? (formMode === 'edit' ? 'Edit User' : 'Set Password') : 'Add User'}
                closeButtonAriaLabel="Close"
                isLightDismiss={true}
            >
                {formMode === 'edit' && (
                <Stack tokens={{ childrenGap: 15 }}>
                    <TextField
                        label="Name"
                        value={formData.name}
                        onChange={(_, value) => 
                            setFormData(prev => ({ ...prev, name: value || '' }))
                        }
                        errorMessage={formErrors.name}
                        required
                    />
                    <TextField
                        label="Email"
                        value={formData.email}
                        onChange={(_, value) => 
                            setFormData(prev => ({ ...prev, email: value || '' }))
                        }
                        errorMessage={formErrors.email}
                        required
                    />
                    <Stack>
                        <Text>Roles</Text>
                        {AVAILABLE_ROLES.map(role => (
                            <Checkbox
                                key={role.key}
                                label={role.text}
                                checked={formData.roles.includes(role.key)}
                                onChange={(_, checked) => {
                                    setFormData(prev => ({
                                        ...prev,
                                        roles: checked
                                            ? [...prev.roles, role.key]
                                            : prev.roles.filter(r => r !== role.key),
                                    }));
                                }}
                            />
                        ))}
                        {formErrors.roles && (
                            <Text className="error-text">{formErrors.roles}</Text>
                        )}
                    </Stack>
                    <Dropdown
                        label="Expertise"
                        options={EXPERTISE_OPTIONS}
                        selectedKey={formData.expertise === null ? 'null' : formData.expertise}
                        onChange={(_, option) => setFormData(prev => ({
                            ...prev,
                            expertise: option?.key === 'null' ? null : option?.key as UserExpertise
                        }))}
                    />
                    <Dropdown
                        label="Reviewer Type"
                        options={[{key: 'true', text: 'Model Reviewer'}, {key: 'false', text: 'Human Reviewer'}]}
                        selectedKey={formData.isModelReviewer ? 'true' : 'false'}
                        onChange={(_, option) => setFormData(prev => ({ ...prev, isModelReviewer: option?.key === 'true' }))}
                    />
                    {emailEnabled && (
                        <TextField
                            label="Organization (for invite email)"
                            value={orgForInvite}
                            onChange={(_, v) => setOrgForInvite(v || '')}
                            description="Used in the email copy: 'the admin of <org> has requested you to set a password'"
                        />
                    )}
                    {formData.isModelReviewer && (
                        <Stack.Item>
                            <Dropdown
                                label="Assigned Model"
                                selectedKey={formData.modelId}
                                options={modelOptions}
                                onChange={(_, option) => 
                                    setFormData(prev => ({ ...prev, modelId: option?.key as string }))
                                }
                            />
                        </Stack.Item>
                    )}
                    
                        <Stack horizontal  tokens={{ childrenGap: 10 }} horizontalAlign="end">
                        <Stack.Item>
                        
                        </Stack.Item>
                        <PrimaryButton 
                            onClick={handleSubmit} 
                            disabled={isLoading}
                        >
                            {isLoading ? 'Saving...' : 'Save'}
                        </PrimaryButton>
                        <DefaultButton onClick={() => dispatch(toggleFlyout())}>Cancel</DefaultButton>
                        
                    </Stack>
                    <Stack>
                        <Text variant="small" styles={{ root: { color: 'red', marginTop: 10 } }}>
                            {error}
                        </Text>
                    </Stack>
                </Stack>
                )}
                {formMode === 'password' && (
                    <Stack tokens={{ childrenGap: 15 }}>
                        <Stack tokens={{ childrenGap: 10 }} >
                            <br/>
                            <Text>Set a new password for <b>{formData.email}</b></Text>
                            <Text variant="small" styles={{ root: { color: '#605e5c' } }}>
                                Requirements: at least 8 characters, one uppercase letter, one lowercase letter, one number, and one special character.
                            </Text>
                            <Text variant="small" styles={{ root: { color: '#605e5c' } }}>
                                Note: This will not notify the user. To send a password setup email, use the "Send Password Setup" button.
                            </Text>
                        </Stack>
                        <Stack tokens={{ childrenGap: 10 }}>
                            {emailEnabled && (
                                <DefaultButton 
                                    onClick={async () => {
                                        const target = formData.email || editingUser?.email || '';
                                        if (!target) { setError('Enter an email first'); return; }
                                        setIsLoading(true);
                                        try {
                                            await userService.sendPasswordSetupEmail(target, orgForInvite || 'Haime');
                                            toast.success('Password setup email sent');
                                        } catch (e) {
                                            setError('Failed to send password setup email');
                                            console.error(e);
                                        } finally {
                                            setIsLoading(false);
                                        }
                                    }}
                                    disabled={isLoading}
                                >
                                    Send Password Setup Email
                                </DefaultButton>
                            )}
                            <Stack.Item>
                            <TextField
                                type="password"
                                canRevealPassword
                                value={newPassword}
                                onChange={(_, v) => {
                                    const pwd = v || '';
                                    setNewPassword(pwd);
                                    if (pwd) {
                                        const email = formData.email || editingUser?.email || '';
                                        const name = formData.name || editingUser?.name || '';
                                        validatePassword(pwd, email, name);
                                    } else {
                                        setPasswordErrors([]);
                                    }
                                }}
                                placeholder="Set password (admin)"
                                errorMessage={passwordErrors.length > 0 ? passwordErrors.join(' ') : undefined}
                            />
                            {passwordErrors.length > 0 && (
                                <MessageBar messageBarType={MessageBarType.error} isMultiline>
                                    <ul style={{ margin: 0, paddingLeft: '20px' }}>
                                        {passwordErrors.map((error, index) => (
                                            <li key={index}>{error}</li>
                                        ))}
                                    </ul>
                                </MessageBar>
                            )}
                            </Stack.Item>
                            <Stack.Item>
                                <DefaultButton
                                onClick={async () => {
                                    const target = formData.email || editingUser?.email || '';
                                    const name = formData.name || editingUser?.name || '';
                                    if (!target) { setError('Enter an email first'); return; }
                                    if (!newPassword) { setError('Enter a new password'); return; }
                                    
                                    // Validate password complexity
                                    if (!validatePassword(newPassword, target, name)) {
                                        setError('Password does not meet complexity requirements');
                                        return;
                                    }
                                    
                                    setIsLoading(true);
                                    try {
                                        await userService.adminSetPassword(target, newPassword);
                                        setNewPassword('');
                                        setPasswordErrors([]);
                                        toast.success('Password updated');
                                        setError(null);
                                        setEditingUser(null);
                                        dispatch(toggleFlyout());
                                    } catch (e: any) {
                                        setError(e.response.data.message || 'Failed to set password');
                                        console.error(e);
                                    } finally {
                                        setIsLoading(false);
                                    }
                                }}
                                disabled={isLoading || passwordErrors.length > 0}
                            >
                                Set Password
                            </DefaultButton>
                            </Stack.Item>
                            <Stack.Item>
                                <Text variant="small" styles={{ root: { color: 'red', marginTop: 10 } }}>
                                    {error}
                                </Text>
                            </Stack.Item>
                            </Stack>
                        </Stack>
                )}
            </Panel>
        </Stack>
    );
}; 
