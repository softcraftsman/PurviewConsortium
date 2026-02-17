import { Configuration, LogLevel, PublicClientApplication } from '@azure/msal-browser';

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_CLIENT_ID || '00000000-0000-0000-0000-000000000000',
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_TENANT_ID || 'common'}`,
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      loggerCallback: (level, message) => {
        if (level === LogLevel.Error) console.error(message);
      },
    },
  },
};

export const loginRequest = {
  scopes: [
    `api://${import.meta.env.VITE_AZURE_CLIENT_ID || '00000000-0000-0000-0000-000000000000'}/access_as_user`,
  ],
};

export const apiScopes = loginRequest.scopes;

// Single shared MSAL instance — used by both main.tsx and api.ts
export const msalInstance = new PublicClientApplication(msalConfig);
