/**
 * SPECTRA Dashboard - Cloudflare Pages Middleware
 *
 * Provides optional GitHub OAuth authentication for dashboard access.
 *
 * Environment Variables Required:
 * - GITHUB_CLIENT_ID: GitHub OAuth App Client ID
 * - GITHUB_CLIENT_SECRET: GitHub OAuth App Client Secret
 * - AUTH_ENABLED: Set to "true" to enable authentication (default: false)
 * - ALLOWED_REPO: Repository slug to check access (e.g., "owner/repo")
 * - SESSION_SECRET: Secret for signing session cookies
 */

const COOKIE_NAME = 'spectra_session';
const SESSION_DURATION = 7 * 24 * 60 * 60; // 7 days in seconds

/**
 * Main middleware handler - runs on every request.
 */
export async function onRequest(context) {
    const { request, env, next } = context;
    const url = new URL(request.url);

    // Skip auth for static assets and specific paths
    if (shouldSkipAuth(url.pathname)) {
        return next();
    }

    // Check if auth is enabled
    const authEnabled = env.AUTH_ENABLED === 'true';
    if (!authEnabled) {
        return next();
    }

    // Check for valid session
    const session = await getSession(request, env);
    if (session && session.valid) {
        // Add user info to request context
        context.data = context.data || {};
        context.data.user = session.user;
        return next();
    }

    // Handle OAuth callback
    if (url.pathname === '/auth/callback') {
        return handleCallback(request, env, url);
    }

    // Handle logout
    if (url.pathname === '/auth/logout') {
        return handleLogout(request);
    }

    // Redirect to GitHub OAuth
    return redirectToGitHub(env, url);
}

/**
 * Check if path should skip authentication.
 */
function shouldSkipAuth(pathname) {
    const skipPaths = [
        '/access-denied.html',
        '/auth/',
        '/favicon.ico',
        '/robots.txt'
    ];

    // Skip static assets
    if (pathname.match(/\.(css|js|png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)$/)) {
        return true;
    }

    return skipPaths.some(path => pathname.startsWith(path));
}

/**
 * Get and validate session from cookie.
 */
async function getSession(request, env) {
    const cookie = request.headers.get('Cookie');
    if (!cookie) return null;

    const sessionCookie = cookie
        .split(';')
        .map(c => c.trim())
        .find(c => c.startsWith(`${COOKIE_NAME}=`));

    if (!sessionCookie) return null;

    const sessionToken = sessionCookie.split('=')[1];
    if (!sessionToken) return null;

    try {
        // Decode and verify session
        const sessionData = await verifySession(sessionToken, env.SESSION_SECRET);
        if (!sessionData) return null;

        // Check expiration
        if (Date.now() > sessionData.exp) {
            return null;
        }

        return {
            valid: true,
            user: sessionData.user
        };
    } catch {
        return null;
    }
}

/**
 * Verify session token signature.
 */
async function verifySession(token, secret) {
    try {
        const [payloadB64, signatureB64] = token.split('.');
        if (!payloadB64 || !signatureB64) return null;

        const payload = JSON.parse(atob(payloadB64));
        const expectedSig = await sign(payloadB64, secret);

        if (signatureB64 !== expectedSig) {
            return null;
        }

        return payload;
    } catch {
        return null;
    }
}

/**
 * Create signed session token.
 */
async function createSessionToken(payload, secret) {
    const payloadB64 = btoa(JSON.stringify(payload));
    const signature = await sign(payloadB64, secret);
    return `${payloadB64}.${signature}`;
}

/**
 * Sign data using HMAC-SHA256.
 */
async function sign(data, secret) {
    const encoder = new TextEncoder();
    const key = await crypto.subtle.importKey(
        'raw',
        encoder.encode(secret),
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign']
    );

    const signature = await crypto.subtle.sign(
        'HMAC',
        key,
        encoder.encode(data)
    );

    return btoa(String.fromCharCode(...new Uint8Array(signature)))
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=/g, '');
}

/**
 * Redirect to GitHub OAuth authorization page.
 */
function redirectToGitHub(env, currentUrl) {
    const clientId = env.GITHUB_CLIENT_ID;
    if (!clientId) {
        return new Response('GitHub OAuth not configured', { status: 500 });
    }

    const redirectUri = `${currentUrl.origin}/auth/callback`;
    const state = btoa(JSON.stringify({
        returnTo: currentUrl.pathname + currentUrl.search,
        ts: Date.now()
    }));

    const authUrl = new URL('https://github.com/login/oauth/authorize');
    authUrl.searchParams.set('client_id', clientId);
    authUrl.searchParams.set('redirect_uri', redirectUri);
    authUrl.searchParams.set('scope', 'read:user repo');
    authUrl.searchParams.set('state', state);

    return Response.redirect(authUrl.toString(), 302);
}

/**
 * Handle OAuth callback from GitHub.
 */
async function handleCallback(request, env, url) {
    const code = url.searchParams.get('code');
    const state = url.searchParams.get('state');

    if (!code) {
        return Response.redirect('/access-denied.html?error=no_code', 302);
    }

    // Exchange code for access token
    const tokenResponse = await fetch('https://github.com/login/oauth/access_token', {
        method: 'POST',
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            client_id: env.GITHUB_CLIENT_ID,
            client_secret: env.GITHUB_CLIENT_SECRET,
            code: code
        })
    });

    const tokenData = await tokenResponse.json();
    if (tokenData.error) {
        return Response.redirect(`/access-denied.html?error=${tokenData.error}`, 302);
    }

    const accessToken = tokenData.access_token;

    // Get user info
    const userResponse = await fetch('https://api.github.com/user', {
        headers: {
            'Authorization': `Bearer ${accessToken}`,
            'Accept': 'application/vnd.github.v3+json',
            'User-Agent': 'SPECTRA-Dashboard'
        }
    });

    if (!userResponse.ok) {
        return Response.redirect('/access-denied.html?error=user_fetch_failed', 302);
    }

    const userData = await userResponse.json();

    // Check repository access if configured
    const allowedRepo = env.ALLOWED_REPO;
    if (allowedRepo) {
        const hasAccess = await checkRepoAccess(accessToken, allowedRepo);
        if (!hasAccess) {
            return Response.redirect('/access-denied.html?error=no_repo_access', 302);
        }
    }

    // Create session
    const sessionPayload = {
        user: {
            login: userData.login,
            name: userData.name,
            avatar: userData.avatar_url
        },
        exp: Date.now() + (SESSION_DURATION * 1000)
    };

    const sessionToken = await createSessionToken(sessionPayload, env.SESSION_SECRET);

    // Parse return URL from state
    let returnTo = '/';
    try {
        const stateData = JSON.parse(atob(state));
        returnTo = stateData.returnTo || '/';
    } catch {
        // Invalid state, use default
    }

    // Set cookie and redirect
    const response = Response.redirect(new URL(returnTo, url.origin).toString(), 302);
    const headers = new Headers(response.headers);
    headers.set('Set-Cookie',
        `${COOKIE_NAME}=${sessionToken}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=${SESSION_DURATION}`
    );

    return new Response(response.body, {
        status: response.status,
        headers
    });
}

/**
 * Check if user has access to the specified repository.
 */
async function checkRepoAccess(accessToken, repoSlug) {
    const response = await fetch(`https://api.github.com/repos/${repoSlug}`, {
        headers: {
            'Authorization': `Bearer ${accessToken}`,
            'Accept': 'application/vnd.github.v3+json',
            'User-Agent': 'SPECTRA-Dashboard'
        }
    });

    // User has access if they can read the repo
    return response.ok;
}

/**
 * Handle logout request.
 */
function handleLogout(request) {
    const response = Response.redirect('/', 302);
    const headers = new Headers(response.headers);
    headers.set('Set-Cookie',
        `${COOKIE_NAME}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0`
    );

    return new Response(response.body, {
        status: response.status,
        headers
    });
}
