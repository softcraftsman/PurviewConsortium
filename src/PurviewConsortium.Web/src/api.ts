import axios from 'axios';
import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig, apiScopes } from './authConfig';

const msalInstance = new PublicClientApplication(msalConfig);

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

// Attach bearer token to every request
api.interceptors.request.use(async (config) => {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    try {
      const response = await msalInstance.acquireTokenSilent({
        scopes: apiScopes,
        account: accounts[0],
      });
      config.headers.Authorization = `Bearer ${response.accessToken}`;
    } catch {
      // If silent fails, user will need to re-login via the UI
      console.warn('Token acquisition failed silently');
    }
  }
  return config;
});

export default api;

// ----- Types -----

export interface DataProductListItem {
  id: string;
  name: string;
  description?: string;
  owner?: string;
  sourceSystem?: string;
  sensitivityLabel?: string;
  classifications: string[];
  glossaryTerms: string[];
  institutionId: string;
  institutionName: string;
  purviewLastModified?: string;
}

export interface DataProductDetail extends DataProductListItem {
  purviewQualifiedName: string;
  ownerEmail?: string;
  schemaJson?: string;
  institutionContactEmail: string;
  lastSyncedFromPurview?: string;
  createdDate: string;
  currentUserRequest?: { requestId: string; status: string; createdDate: string };
}

export interface CatalogSearchResponse {
  items: DataProductListItem[];
  totalCount: number;
  facets: Record<string, { value: string; count: number }[]>;
}

export interface AccessRequest {
  id: string;
  dataProductId: string;
  dataProductName: string;
  owningInstitutionName: string;
  requestingUserId: string;
  requestingUserEmail: string;
  requestingUserName: string;
  requestingInstitutionId: string;
  requestingInstitutionName: string;
  targetFabricWorkspaceId?: string;
  targetLakehouseName?: string;
  businessJustification: string;
  requestedDurationDays?: number;
  status: string;
  statusChangedDate?: string;
  statusChangedBy?: string;
  externalShareId?: string;
  expirationDate?: string;
  createdDate: string;
}

export interface Institution {
  id: string;
  name: string;
  tenantId: string;
  purviewAccountName: string;
  fabricWorkspaceId?: string;
  primaryContactEmail: string;
  isActive: boolean;
  adminConsentGranted: boolean;
  createdDate: string;
  modifiedDate: string;
}

export interface CatalogStats {
  totalProducts: number;
  totalInstitutions: number;
  userPendingRequests: number;
  userActiveShares: number;
  recentAdditions: DataProductListItem[];
  productsByInstitution: Record<string, number>;
}

export interface SyncHistoryItem {
  id: string;
  institutionId: string;
  institutionName: string;
  startTime: string;
  endTime?: string;
  status: string;
  productsFound: number;
  productsAdded: number;
  productsUpdated: number;
  productsDelisted: number;
  errorDetails?: string;
}

// ----- API Functions -----

export const catalogApi = {
  search: (params: Record<string, string>) =>
    api.get<CatalogSearchResponse>('/catalog/products', { params }),
  getProduct: (id: string) =>
    api.get<DataProductDetail>(`/catalog/products/${id}`),
  getStats: () => api.get<CatalogStats>('/catalog/stats'),
  getFilters: () =>
    api.get<{
      institutions: { id: string; name: string }[];
      classifications: string[];
      glossaryTerms: string[];
      sensitivityLabels: string[];
      sourceSystems: string[];
    }>('/catalog/filters'),
};

export const requestsApi = {
  create: (data: {
    dataProductId: string;
    targetFabricWorkspaceId?: string;
    targetLakehouseName?: string;
    businessJustification: string;
    requestedDurationDays?: number;
  }) => api.post<AccessRequest>('/requests', data),
  list: (params?: Record<string, string>) =>
    api.get<AccessRequest[]>('/requests', { params }),
  get: (id: string) => api.get<AccessRequest>(`/requests/${id}`),
  updateStatus: (id: string, data: { newStatus: string; comment?: string; externalShareId?: string }) =>
    api.patch<AccessRequest>(`/requests/${id}/status`, data),
  cancel: (id: string) => api.delete(`/requests/${id}`),
  getFulfillment: (id: string) => api.get(`/requests/${id}/fulfillment`),
};

export const adminApi = {
  listInstitutions: (activeOnly = false) =>
    api.get<Institution[]>('/admin/institutions', { params: { activeOnly } }),
  getInstitution: (id: string) =>
    api.get<Institution>(`/admin/institutions/${id}`),
  createInstitution: (data: Partial<Institution>) =>
    api.post<Institution>('/admin/institutions', data),
  updateInstitution: (id: string, data: Partial<Institution>) =>
    api.put<Institution>(`/admin/institutions/${id}`, data),
  deleteInstitution: (id: string) =>
    api.delete(`/admin/institutions/${id}`),
  triggerScan: (id: string) =>
    api.post(`/admin/institutions/${id}/scan`),
  getSyncHistory: (params?: Record<string, string>) =>
    api.get<SyncHistoryItem[]>('/admin/sync/history', { params }),
  triggerFullScan: () => api.post('/admin/sync/trigger'),
};
