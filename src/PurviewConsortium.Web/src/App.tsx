import { useEffect, useState } from 'react';
import { Routes, Route, Navigate, useNavigate, useLocation } from 'react-router-dom';
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from '@azure/msal-react';
import {
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
} from '@fluentui/react-components';
import { loginRequest } from './authConfig';
import { adminApi } from './api';
import Layout from './components/Layout';
import DashboardPage from './pages/DashboardPage';
import CatalogPage from './pages/CatalogPage';
import DataProductDetailPage from './pages/DataProductDetailPage';
import MyRequestsPage from './pages/MyRequestsPage';
import InstitutionsPage from './pages/InstitutionsPage';
import SetupGuidePage from './pages/SetupGuidePage';
import LogsPage from './pages/LogsPage';
import LoginPage from './pages/LoginPage';

// In dev mode with placeholder client ID, bypass MSAL auth entirely
const isDevAuthBypass =
  !import.meta.env.VITE_AZURE_CLIENT_ID ||
  import.meta.env.VITE_AZURE_CLIENT_ID === '00000000-0000-0000-0000-000000000000';

function ConsentCallbackHandler() {
  const location = useLocation();
  const navigate = useNavigate();
  const [consentMessage, setConsentMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const adminConsent = params.get('admin_consent');
    const tenantId = params.get('tenant');

    if (adminConsent === 'True' && tenantId) {
      // Microsoft returned from admin consent — find the institution by tenant and mark consent granted
      (async () => {
        try {
          const response = await adminApi.listInstitutions();
          const institution = response.data.find(
            (inst) => inst.tenantId.toLowerCase() === tenantId.toLowerCase()
          );
          if (institution) {
            await adminApi.updateInstitution(institution.id, {
              name: institution.name,
              purviewAccountName: institution.purviewAccountName,
              fabricWorkspaceId: institution.fabricWorkspaceId,
              primaryContactEmail: institution.primaryContactEmail,
              isActive: institution.isActive,
              adminConsentGranted: true,
            });
            setConsentMessage({
              type: 'success',
              text: `Admin consent granted for ${institution.name}! You can now scan their Purview catalog.`,
            });
          } else {
            setConsentMessage({
              type: 'error',
              text: `Consent was granted but no institution with tenant ID ${tenantId} was found. Register the institution first.`,
            });
          }
        } catch (err) {
          console.error('Failed to process consent callback:', err);
          setConsentMessage({
            type: 'error',
            text: 'Consent was granted but we failed to update the institution. Please mark consent manually on the Institutions page.',
          });
        }
        // Clean the URL by navigating to /admin/institutions without the query params
        navigate('/admin/institutions', { replace: true });
      })();
    }
  }, [location.search, navigate]);

  if (!consentMessage) return null;

  return (
    <div style={{ padding: '0 24px', marginTop: 8 }}>
      <MessageBar
        intent={consentMessage.type === 'success' ? 'success' : 'error'}
        style={{ marginBottom: 16 }}
      >
        <MessageBarBody>
          <MessageBarTitle>
            {consentMessage.type === 'success' ? 'Consent Recorded' : 'Consent Error'}
          </MessageBarTitle>
          {consentMessage.text}
        </MessageBarBody>
      </MessageBar>
    </div>
  );
}

function AppRoutes() {
  return (
    <Layout>
      <ConsentCallbackHandler />
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/catalog" element={<CatalogPage />} />
        <Route path="/catalog/:id" element={<DataProductDetailPage />} />
        <Route path="/requests" element={<MyRequestsPage />} />
        <Route path="/admin/institutions" element={<InstitutionsPage />} />
        <Route path="/admin/logs" element={<LogsPage />} />
        <Route path="/setup" element={<SetupGuidePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}

function App() {
  const { instance } = useMsal();

  const handleLogin = () => {
    instance.loginRedirect(loginRequest);
  };

  if (isDevAuthBypass) {
    return <AppRoutes />;
  }

  return (
    <>
      <UnauthenticatedTemplate>
        <LoginPage onLogin={handleLogin} />
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <AppRoutes />
      </AuthenticatedTemplate>
    </>
  );
}

export default App;
