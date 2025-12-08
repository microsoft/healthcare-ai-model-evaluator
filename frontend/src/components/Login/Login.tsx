import React, { useMemo, useState, useEffect } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import './Login.scss';
import { Dashboard } from '../Dashboard/Dashboard';
import {
    PrimaryButton,
    DefaultButton,
    Link,
    Stack,
    Text,
    TextField,
    MessageBar,
    MessageBarType,
    Separator,
    Spinner,
    SpinnerSize
} from '@fluentui/react';
import { useLocation } from 'react-router-dom';
import authService from 'services/authService';
import { validatePasswordComplexity } from '../../utils/passwordValidation';

export const Login: React.FC = () => {
    const { login, loginWithPassword, error, isAuthenticated, loading } = useAuth();

    type Mode = 'oauth' | 'password' | 'forgot' | 'reset';
    const location = useLocation();
    const initialMode: Mode = useMemo(() => {
        const params = new URLSearchParams(location.search);
        return params.get('resetToken') ? 'reset' : 'oauth';
    }, [location.search]);

    const [mode, setMode] = useState<Mode>(initialMode);
    const [emailEnabled, setEmailEnabled] = useState<boolean>(false);
    useEffect(() => setMode(initialMode), [initialMode]);
    useEffect(() => {
        // fetch server-side config
        fetch('/api/auth/config')
            .then(r => r.ok ? r.json() : { emailEnabled: false })
            .then(cfg => setEmailEnabled(!!cfg.emailEnabled))
            .catch(() => setEmailEnabled(false));
    }, []);

    // shared fields
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [localError, setLocalError] = useState<string | null>(null);
    const [passwordErrors, setPasswordErrors] = useState<string[]>([]);
    const resetToken = useMemo(() => new URLSearchParams(location.search).get('resetToken') || '', [location.search]);

    if (isAuthenticated) {
        return <Dashboard />;
    }
    if ( loading ) {
        return <Spinner style={{ marginTop: '20px' }} size={SpinnerSize.large} />;
    }
    return (
        <div className="login-container">
            <div className="login-box">
                <h1>Healthcare AI <br/> Model Evaluator</h1>

                {mode === 'oauth' && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <PrimaryButton onClick={login} text="Sign in with Microsoft" disabled={loading} />
                        <Separator>or</Separator>
                        <Link onClick={() => setMode('password')}>Sign in with email and password</Link>
                        {error && (
                            <MessageBar messageBarType={MessageBarType.error}>{error}</MessageBar>
                        )}
                    </Stack>
                )}

                {mode === 'password' && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <Text variant="large">Sign in</Text>
                        <TextField label="Email" type="email" value={email} onChange={(_, v) => setEmail(v || '')} required />
                        <TextField 
                            label="Password" 
                            type="password" 
                            canRevealPassword 
                            value={password} 
                            onChange={(_, v) => setPassword(v || '')} 
                            onKeyDown={(e) => {
                                if (e.key === 'Enter' && !loading && email && password) {
                                    e.preventDefault();
                                    setLocalError(null);
                                    loginWithPassword(email, password).catch((e: any) => {
                                        setLocalError(e?.message || 'Login failed');
                                    });
                                }
                            }}
                            required 
                        />
                        {localError && (
                            <MessageBar messageBarType={MessageBarType.error}>{localError}</MessageBar>
                        )}
                        <PrimaryButton
                            text={loading ? 'Signing in…' : 'Sign in'}
                            disabled={loading || !email || !password}
                            onClick={async () => {
                                setLocalError(null);
                                try {
                                    await loginWithPassword(email, password);
                                } catch (e: any) {
                                    setLocalError(e?.message || 'Login failed');
                                }
                            }}
                        />
                        <Stack horizontal horizontalAlign="space-between">
                            <Link onClick={() => setMode('oauth')}>Back</Link>
                            {emailEnabled && (
                                <Link onClick={() => setMode('forgot')}>Forgot password?</Link>
                            )}
                        </Stack>
                    </Stack>
                )}

                {mode === 'forgot' && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <Text variant="large">Forgot password</Text>
                        <Text>Enter your email address. If it matches an account, we'll send a reset link.</Text>
                        <TextField label="Email" type="email" value={email} onChange={(_, v) => setEmail(v || '')} required />
                        {localError && (
                            <MessageBar messageBarType={MessageBarType.error}>{localError}</MessageBar>
                        )}
                        <PrimaryButton
                            text={loading ? 'Sending…' : 'Send reset link'}
                            disabled={loading || !email}
                            onClick={async () => {
                                setLocalError(null);
                                try {
                                    await authService.requestPasswordReset(email);
                                    setMode('oauth');
                                } catch (e: any) {
                                    // Always pretend success; still show back
                                    setMode('oauth');
                                }
                            }}
                        />
                        <DefaultButton text="Back" onClick={() => setMode('oauth')} />
                    </Stack>
                )}

                {mode === 'reset' && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <Text variant="large">Reset your password</Text>
                        <Text>
                            Password requirements:
                        </Text>
                        <ul style={{ margin: '0 0 10px 20px', fontSize: '14px' }}>
                            <li>At least 8 characters long</li>
                            <li>Include characters from at least 3 categories: uppercase letters, lowercase letters, numbers, and special characters</li>
                            <li>Cannot contain your account name or display name</li>
                            <li>Cannot be a common password</li>
                        </ul>
                        <TextField 
                            label="New password" 
                            type="password" 
                            canRevealPassword 
                            value={password} 
                            onChange={(_, v) => {
                                const pwd = v || '';
                                setPassword(pwd);
                                if (pwd) {
                                    const validation = validatePasswordComplexity(pwd, email?.split('@')[0] || '');
                                    setPasswordErrors(validation.errors);
                                } else {
                                    setPasswordErrors([]);
                                }
                            }} 
                            required 
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
                        <TextField label="Confirm new password" type="password" canRevealPassword value={confirmPassword} onChange={(_, v) => setConfirmPassword(v || '')} required />
                        {localError && (
                            <MessageBar messageBarType={MessageBarType.error}>{localError}</MessageBar>
                        )}
                        <PrimaryButton
                            text={loading ? 'Resetting…' : 'Reset password'}
                            disabled={loading || !password || !confirmPassword || passwordErrors.length > 0}
                            onClick={async () => {
                                setLocalError(null);
                                if (password !== confirmPassword) {
                                    setLocalError('Passwords do not match');
                                    return;
                                }
                                // client-side complexity check
                                const validation = validatePasswordComplexity(password, email?.split('@')[0] || '');
                                if (!validation.isValid) {
                                    setLocalError('Password does not meet complexity requirements');
                                    return;
                                }
                                try {
                                    await authService.resetPassword({ token: resetToken, newPassword: password });
                                    setMode('oauth');
                                } catch (e: any) {
                                    setLocalError(e?.message || 'Reset failed');
                                }
                            }}
                        />
                        <DefaultButton text="Back" onClick={() => setMode('oauth')} />
                    </Stack>
                )}
            </div>
        </div>
    );
};