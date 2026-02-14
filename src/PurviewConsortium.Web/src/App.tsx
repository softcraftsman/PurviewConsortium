import { Routes, Route, Navigate } from 'react-router-dom';
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from '@azure/msal-react';
import { loginRequest } from './authConfig';
import Layout from './components/Layout';
import DashboardPage from './pages/DashboardPage';
import CatalogPage from './pages/CatalogPage';
import DataProductDetailPage from './pages/DataProductDetailPage';
import MyRequestsPage from './pages/MyRequestsPage';
import InstitutionsPage from './pages/InstitutionsPage';
import SetupGuidePage from './pages/SetupGuidePage';
import LoginPage from './pages/LoginPage';

// In dev mode with placeholder client ID, bypass MSAL auth entirely
const isDevAuthBypass =
  !import.meta.env.VITE_AZURE_CLIENT_ID ||
  import.meta.env.VITE_AZURE_CLIENT_ID === '00000000-0000-0000-0000-000000000000';

function AppRoutes() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/catalog" element={<CatalogPage />} />
        <Route path="/catalog/:id" element={<DataProductDetailPage />} />
        <Route path="/requests" element={<MyRequestsPage />} />
        <Route path="/admin/institutions" element={<InstitutionsPage />} />
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
