/**
 * SPECTRA Dashboard - OAuth Callback Handler
 *
 * Note: The actual OAuth callback logic is handled in _middleware.js
 * This file exists for clarity and potential future expansion.
 *
 * The middleware intercepts requests to /auth/callback and processes
 * the OAuth flow directly.
 */
 
export async function onRequest(context) {
    // This should not be reached as middleware handles /auth/callback
    // If we get here, redirect to home
    const url = new URL(context.request.url);
    return Response.redirect(`${url.origin}/`, 302);
}
 