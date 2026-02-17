import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { EventType } from '@azure/msal-browser';
import { MsalProvider } from '@azure/msal-react';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { msalInstance } from './authConfig';
import App from './App';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1 },
  },
});

// MSAL v3+ requires async initialization before any operations
async function startApp() {
  await msalInstance.initialize();

  // Process the redirect response if returning from login
  const response = await msalInstance.handleRedirectPromise();
  if (response?.account) {
    msalInstance.setActiveAccount(response.account);
  }

  // Set active account from cache if not already set
  if (!msalInstance.getActiveAccount()) {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      msalInstance.setActiveAccount(accounts[0]);
    }
  }

  // Set active account on future login events
  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const payload = event.payload as { account: Parameters<typeof msalInstance.setActiveAccount>[0] };
      msalInstance.setActiveAccount(payload.account);
    }
  });

  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <FluentProvider theme={webLightTheme}>
          <QueryClientProvider client={queryClient}>
            <BrowserRouter>
              <App />
            </BrowserRouter>
          </QueryClientProvider>
        </FluentProvider>
      </MsalProvider>
    </React.StrictMode>,
  );
}

startApp();
