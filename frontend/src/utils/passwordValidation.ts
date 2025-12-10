// Common weak passwords list - top 100 most common passwords
export const COMMON_PASSWORDS = new Set([
    '123456', 'password', '12345678', 'qwerty', '123456789', '12345', '1234', '111111',
    '1234567', 'dragon', '123123', 'baseball', 'abc123', 'football', 'monkey', 'letmein',
    '696969', 'shadow', 'master', '666666', 'qwertyuiop', '123321', 'mustang', '1234567890',
    'michael', '654321', 'pussy', 'superman', '1qaz2wsx', '7777777', 'fuckyou', '121212',
    '000000', 'qazwsx', '123qwe', 'killer', 'trustno1', 'jordan', 'jennifer', 'zxcvbnm',
    'asdfgh', 'hunter', 'buster', 'soccer', 'harley', 'batman', 'andrew', 'tigger',
    'sunshine', 'iloveyou', 'fuckme', '2000', 'charlie', 'robert', 'thomas', 'hockey',
    'ranger', 'daniel', 'starwars', 'klaster', '112233', 'george', 'asshole', 'computer',
    'michelle', 'jessica', 'pepper', '1111', 'zxcvbn', '555555', '11111111', '131313',
    'freedom', '777777', 'pass', 'fuck', 'maggie', '159753', 'aaaaaa', 'ginger',
    'princess', 'joshua', 'cheese', 'amanda', 'summer', 'love', 'ashley', '6969',
    'nicole', 'chelsea', 'biteme', 'matthew', 'access', 'yankees', '987654321', 'dallas',
    'austin', 'thunder', 'taylor', 'matrix', 'william', 'corvette', 'hello', 'martin',
    'heather', 'secret', 'fucker', 'merlin', 'diamond', '1234qwer', 'gfhjkm', 'hammer',
    'silver', '222222', '88888888', 'anthony', 'justin', 'test', 'bailey', 'q1w2e3r4t5',
    'patrick', 'internet', 'scooter', 'orange', '11111', 'golfer', 'cookie', 'richard',
    'samantha', 'bigdog', 'guitar', 'jackson', 'whatever', 'mickey', 'chicken', 'sparky',
    'snoopy', 'maverick', 'phoenix', 'camaro', 'sexy', 'peanut', 'morgan', 'welcome',
    'falcon', 'cowboy', 'ferrari', 'samsung', 'andrea', 'smokey', 'steelers', 'joseph',
    'mercedes', 'dakota', 'arsenal', 'eagles', 'melissa', 'boomer', 'booboo', 'spider',
    'nascar', 'monster', 'tigers', 'yellow', 'xxxxxx', '123123123', 'gateway', 'marina',
    'diablo', 'bulldog', 'qwer1234', 'compaq', 'purple', 'hardcore', 'banana', 'junior'
]);

export interface PasswordValidationResult {
    isValid: boolean;
    errors: string[];
}

export function validatePasswordComplexity(
    password: string, 
    accountName?: string, 
    displayName?: string
): PasswordValidationResult {
    const errors: string[] = [];

    // Check minimum length
    if (!password || password.length < 8) {
        errors.push('Password must be at least 8 characters long.');
    }

    // Check for account name or display name in password (case insensitive)
    if (accountName && password.toLowerCase().includes(accountName.toLowerCase())) {
        errors.push('Password cannot contain the account name.');
    }
    
    if (displayName && password.toLowerCase().includes(displayName.toLowerCase())) {
        errors.push('Password cannot contain the display name.');
    }

    // Count character categories
    let categories = 0;
    const hasLowercase = /[a-z]/.test(password);
    const hasUppercase = /[A-Z]/.test(password);
    const hasNumbers = /\d/.test(password);
    const hasSpecialChars = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?~`]/.test(password);

    if (hasLowercase) categories++;
    if (hasUppercase) categories++;
    if (hasNumbers) categories++;
    if (hasSpecialChars) categories++;

    if (categories < 3) {
        errors.push('Password must include characters from at least three of the following categories: uppercase letters, lowercase letters, numbers, and special characters.');
    }

    // Check against common passwords
    if (COMMON_PASSWORDS.has(password.toLowerCase())) {
        errors.push('This password is too common and easily guessable. Please choose a different password.');
    }

    return {
        isValid: errors.length === 0,
        errors
    };
}