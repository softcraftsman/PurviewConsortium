import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Text,
  Spinner,
  Badge,
  Button,
  Table,
  TableHeader,
  TableHeaderCell,
  TableRow,
  TableBody,
  TableCell,
  Input,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Field,
  tokens,
} from '@fluentui/react-components';
import {
  Play24Regular,
  ArrowSync24Regular,
  Add24Regular,
  Delete24Regular,
  Copy24Regular,
  Link24Regular,
  CheckmarkCircle24Regular,
  DismissCircle24Regular,
  Edit24Regular,
} from '@fluentui/react-icons';
import { adminApi, type Institution, type SyncHistoryItem } from '../api';

const emptyForm = {
  name: '',
  tenantId: '',
  purviewAccountName: '',
  fabricWorkspaceId: '',
  primaryContactEmail: '',
};

export default function InstitutionsPage() {
  const queryClient = useQueryClient();
  const [addOpen, setAddOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [deleteTarget, setDeleteTarget] = useState<Institution | null>(null);
  const [createdInstitution, setCreatedInstitution] = useState<{ name: string; tenantId: string } | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [editTarget, setEditTarget] = useState<Institution | null>(null);
  const [editForm, setEditForm] = useState({
    name: '',
    purviewAccountName: '',
    fabricWorkspaceId: '',
    primaryContactEmail: '',
    consortiumDomainIds: '',
    isActive: true,
    adminConsentGranted: false,
  });

  const clientId = import.meta.env.VITE_AZURE_CLIENT_ID || '';
  const redirectUri = window.location.origin;

  const buildConsentUrl = (tenantId: string) =>
    `https://login.microsoftonline.com/${tenantId}/adminconsent?client_id=${encodeURIComponent(clientId)}&redirect_uri=${encodeURIComponent(redirectUri)}`;

  const copyToClipboard = async (text: string, id?: string) => {
    await navigator.clipboard.writeText(text);
    if (id) {
      setCopiedId(id);
      setTimeout(() => setCopiedId(null), 2000);
    }
  };

  const { data: institutions, isLoading } = useQuery({
    queryKey: ['institutions'],
    queryFn: () => adminApi.listInstitutions().then((r) => r.data),
  });

  const { data: syncHistory } = useQuery({
    queryKey: ['syncHistory'],
    queryFn: () => adminApi.getSyncHistory().then((r) => r.data),
  });

  const scanMutation = useMutation({
    mutationFn: (id: string) => adminApi.triggerScan(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['syncHistory'] }),
  });

  const fullScanMutation = useMutation({
    mutationFn: () => adminApi.triggerFullScan(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['syncHistory'] }),
  });

  const createMutation = useMutation({
    mutationFn: () => adminApi.createInstitution(form),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['institutions'] });
      setCreatedInstitution({ name: form.name, tenantId: form.tenantId });
      setForm(emptyForm);
      setAddOpen(false);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => adminApi.deleteInstitution(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['institutions'] });
      setDeleteTarget(null);
    },
  });

  const consentToggleMutation = useMutation({
    mutationFn: (inst: Institution) =>
      adminApi.updateInstitution(inst.id, {
        name: inst.name,
        purviewAccountName: inst.purviewAccountName,
        fabricWorkspaceId: inst.fabricWorkspaceId,
        primaryContactEmail: inst.primaryContactEmail,
        isActive: inst.isActive,
        adminConsentGranted: !inst.adminConsentGranted,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['institutions'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      adminApi.updateInstitution(editTarget!.id, {
        name: editForm.name,
        purviewAccountName: editForm.purviewAccountName,
        fabricWorkspaceId: editForm.fabricWorkspaceId,
        primaryContactEmail: editForm.primaryContactEmail,
        consortiumDomainIds: editForm.consortiumDomainIds || undefined,
        isActive: editForm.isActive,
        adminConsentGranted: editForm.adminConsentGranted,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['institutions'] });
      setEditTarget(null);
    },
  });

  const openEdit = (inst: Institution) => {
    setEditForm({
      name: inst.name,
      purviewAccountName: inst.purviewAccountName ?? '',
      fabricWorkspaceId: inst.fabricWorkspaceId ?? '',
      primaryContactEmail: inst.primaryContactEmail,
      consortiumDomainIds: inst.consortiumDomainIds ?? '',
      isActive: inst.isActive,
      adminConsentGranted: inst.adminConsentGranted,
    });
    setEditTarget(inst);
  };

  const handleFieldChange = (field: keyof typeof form) => (
    _: unknown,
    data: { value: string }
  ) => setForm((prev) => ({ ...prev, [field]: data.value }));

  const handleEditFieldChange = (field: keyof typeof editForm) => (
    _: unknown,
    data: { value: string } | { checked: boolean }
  ) => setEditForm((prev) => ({ ...prev, [field]: 'value' in data ? data.value : data.checked }));

  const isFormValid =
    form.name.trim() !== '' &&
    form.tenantId.trim() !== '' &&
    form.purviewAccountName.trim() !== '' &&
    form.primaryContactEmail.trim() !== '';

  if (isLoading) return <Spinner label="Loading..." />;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <Text as="h1" size={800} weight="bold" block>
            Institutions
          </Text>
          <Text size={400}>Manage consortium member institutions and Purview scans.</Text>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <Dialog open={addOpen} onOpenChange={(_, data) => setAddOpen(data.open)}>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="primary" icon={<Add24Regular />}>
                Add Institution
              </Button>
            </DialogTrigger>
            <DialogSurface>
              <DialogBody>
                <DialogTitle>Add Institution</DialogTitle>
                <DialogContent>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', paddingTop: '8px' }}>
                    <Field label="Institution Name" required>
                      <Input
                        value={form.name}
                        onChange={handleFieldChange('name')}
                        placeholder="e.g. Contoso University"
                      />
                    </Field>
                    <Field label="Entra Tenant ID" required>
                      <Input
                        value={form.tenantId}
                        onChange={handleFieldChange('tenantId')}
                        placeholder="e.g. xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                      />
                    </Field>
                    <Field label="Purview Account Name" required>
                      <Input
                        value={form.purviewAccountName}
                        onChange={handleFieldChange('purviewAccountName')}
                        placeholder="e.g. contoso-purview"
                      />
                    </Field>
                    <Field label="Fabric Workspace ID">
                      <Input
                        value={form.fabricWorkspaceId}
                        onChange={handleFieldChange('fabricWorkspaceId')}
                        placeholder="Optional — used for OneLake shortcut guidance"
                      />
                    </Field>
                    <Field label="Primary Contact Email" required>
                      <Input
                        value={form.primaryContactEmail}
                        onChange={handleFieldChange('primaryContactEmail')}
                        placeholder="e.g. admin@contoso.edu"
                        type="email"
                      />
                    </Field>
                    {form.tenantId.trim() && (
                      <div style={{
                        backgroundColor: tokens.colorNeutralBackground3,
                        borderRadius: '6px',
                        padding: '12px',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '6px',
                      }}>
                        <Text size={200} weight="semibold" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                          <Link24Regular style={{ fontSize: '14px' }} /> Admin Consent Link
                        </Text>
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                          Send this link to the institution's Entra admin so they can grant your app access to their tenant.
                        </Text>
                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginTop: '4px' }}>
                          <Input
                            readOnly
                            value={buildConsentUrl(form.tenantId.trim())}
                            style={{ flex: 1, fontSize: '12px' }}
                            size="small"
                          />
                          <Button
                            appearance="subtle"
                            icon={<Copy24Regular />}
                            size="small"
                            onClick={() => copyToClipboard(buildConsentUrl(form.tenantId.trim()), 'dialog')}
                          >
                            {copiedId === 'dialog' ? 'Copied!' : 'Copy'}
                          </Button>
                        </div>
                      </div>
                    )}
                    {createMutation.isError && (
                      <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
                        Failed to create institution. Please try again.
                      </Text>
                    )}
                  </div>
                </DialogContent>
                <DialogActions>
                  <DialogTrigger disableButtonEnhancement>
                    <Button appearance="secondary">Cancel</Button>
                  </DialogTrigger>
                  <Button
                    appearance="primary"
                    onClick={() => createMutation.mutate()}
                    disabled={!isFormValid || createMutation.isPending}
                  >
                    {createMutation.isPending ? 'Creating...' : 'Add Institution'}
                  </Button>
                </DialogActions>
              </DialogBody>
            </DialogSurface>
          </Dialog>
          <Button
            appearance="secondary"
            icon={<ArrowSync24Regular />}
            onClick={() => fullScanMutation.mutate()}
            disabled={fullScanMutation.isPending}
          >
            Scan All
          </Button>
        </div>
      </div>

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteTarget !== null} onOpenChange={(_, data) => { if (!data.open) setDeleteTarget(null); }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Remove Institution</DialogTitle>
            <DialogContent>
              Are you sure you want to remove <Text weight="bold">{deleteTarget?.name}</Text> from the consortium?
              This will also remove all associated data products and access requests.
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setDeleteTarget(null)}>Cancel</Button>
              <Button
                appearance="primary"
                style={{ backgroundColor: tokens.colorPaletteRedBackground3, borderColor: tokens.colorPaletteRedBackground3 }}
                onClick={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Removing...' : 'Remove'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Edit Institution Dialog */}
      <Dialog open={editTarget !== null} onOpenChange={(_, data) => { if (!data.open) setEditTarget(null); }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Edit Institution — {editTarget?.name}</DialogTitle>
            <DialogContent>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', paddingTop: '8px' }}>
                <Field label="Institution Name" required>
                  <Input
                    value={editForm.name}
                    onChange={handleEditFieldChange('name')}
                    placeholder="e.g. Contoso University"
                  />
                </Field>
                <Field label="Purview Account Name" required>
                  <Input
                    value={editForm.purviewAccountName}
                    onChange={handleEditFieldChange('purviewAccountName')}
                    placeholder="e.g. contoso-purview"
                  />
                </Field>
                <Field label="Fabric Workspace ID">
                  <Input
                    value={editForm.fabricWorkspaceId}
                    onChange={handleEditFieldChange('fabricWorkspaceId')}
                    placeholder="Optional — used for OneLake shortcut guidance"
                  />
                </Field>
                <Field label="Primary Contact Email" required>
                  <Input
                    value={editForm.primaryContactEmail}
                    onChange={handleEditFieldChange('primaryContactEmail')}
                    placeholder="e.g. admin@contoso.edu"
                    type="email"
                  />
                </Field>
                <Field label="Consortium Domain IDs">
                  <Input
                    value={editForm.consortiumDomainIds}
                    onChange={handleEditFieldChange('consortiumDomainIds')}
                    placeholder="Comma-separated Purview domain GUIDs"
                  />
                </Field>
                <div style={{ display: 'flex', gap: '24px', paddingTop: '4px' }}>
                  <Field label="Active">
                    <input
                      type="checkbox"
                      checked={editForm.isActive}
                      onChange={(e) => setEditForm((prev) => ({ ...prev, isActive: e.target.checked }))}
                      style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                    />
                  </Field>
                  <Field label="Admin Consent Granted">
                    <input
                      type="checkbox"
                      checked={editForm.adminConsentGranted}
                      onChange={(e) => setEditForm((prev) => ({ ...prev, adminConsentGranted: e.target.checked }))}
                      style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                    />
                  </Field>
                </div>
                {updateMutation.isError && (
                  <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
                    Failed to update institution. Please try again.
                  </Text>
                )}
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setEditTarget(null)}>Cancel</Button>
              <Button
                appearance="primary"
                onClick={() => updateMutation.mutate()}
                disabled={
                  !editForm.name.trim() ||
                  !editForm.purviewAccountName.trim() ||
                  !editForm.primaryContactEmail.trim() ||
                  updateMutation.isPending
                }
              >
                {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Post-creation Admin Consent Link Dialog */}      <Dialog open={createdInstitution !== null} onOpenChange={(_, data) => { if (!data.open) setCreatedInstitution(null); }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Institution Created</DialogTitle>
            <DialogContent>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', paddingTop: '8px' }}>
                <Text>
                  <Text weight="bold">{createdInstitution?.name}</Text> has been added to the consortium.
                </Text>
                <Text>
                  Copy the admin consent link below and send it to the institution's Entra ID administrator.
                  Once they approve, their Purview catalog can be synced.
                </Text>
                <div style={{
                  backgroundColor: tokens.colorNeutralBackground3,
                  borderRadius: '6px',
                  padding: '12px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '8px',
                }}>
                  <Text size={200} weight="semibold">Admin Consent URL</Text>
                  <Input
                    readOnly
                    value={createdInstitution ? buildConsentUrl(createdInstitution.tenantId) : ''}
                    style={{ fontSize: '12px' }}
                    size="small"
                  />
                  <Button
                    appearance="primary"
                    icon={<Copy24Regular />}
                    onClick={() => createdInstitution && copyToClipboard(buildConsentUrl(createdInstitution.tenantId), 'created')}
                  >
                    {copiedId === 'created' ? 'Copied to Clipboard!' : 'Copy Consent Link'}
                  </Button>
                </div>
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setCreatedInstitution(null)}>Done</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Institutions Table */}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Name</TableHeaderCell>
            <TableHeaderCell>Purview Account</TableHeaderCell>
            <TableHeaderCell>Contact</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell>Consent</TableHeaderCell>
            <TableHeaderCell>Actions</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {institutions?.map((inst: Institution) => (
            <TableRow key={inst.id}>
              <TableCell>
                <Text weight="semibold">{inst.name}</Text>
              </TableCell>
              <TableCell>{inst.purviewAccountName}</TableCell>
              <TableCell>{inst.primaryContactEmail}</TableCell>
              <TableCell>
                <Badge appearance="filled" color={inst.isActive ? 'success' : 'danger'}>
                  {inst.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </TableCell>
              <TableCell>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Badge appearance="outline" color={inst.adminConsentGranted ? 'success' : 'warning'}>
                    {inst.adminConsentGranted ? 'Granted' : 'Pending'}
                  </Badge>
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={inst.adminConsentGranted ? <DismissCircle24Regular /> : <CheckmarkCircle24Regular />}
                    onClick={() => consentToggleMutation.mutate(inst)}
                    disabled={consentToggleMutation.isPending}
                    title={inst.adminConsentGranted ? 'Revoke consent' : 'Mark consent as granted'}
                  >
                    {inst.adminConsentGranted ? 'Revoke' : 'Grant'}
                  </Button>
                </div>
              </TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: '4px' }}>
                  <Button
                    appearance="subtle"
                    icon={<Edit24Regular />}
                    onClick={() => openEdit(inst)}
                    title="Edit institution"
                    size="small"
                  >
                    Edit
                  </Button>
                  <Button
                    appearance="subtle"
                    icon={<Copy24Regular />}
                    onClick={() => copyToClipboard(buildConsentUrl(inst.tenantId), inst.id)}
                    title="Copy admin consent link"
                    size="small"
                  >
                    {copiedId === inst.id ? 'Copied!' : 'Consent Link'}
                  </Button>
                  <Button
                    appearance="subtle"
                    icon={<Play24Regular />}
                    onClick={() => scanMutation.mutate(inst.id)}
                    disabled={scanMutation.isPending}
                  >
                    Scan
                  </Button>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => setDeleteTarget(inst)}
                    title="Remove institution"
                  />
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {/* Sync History */}
      <div style={{ marginTop: '32px' }}>
        <Text as="h2" size={600} weight="semibold" block style={{ marginBottom: '16px' }}>
          Recent Sync History
        </Text>
        {syncHistory && syncHistory.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Institution</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell>Found</TableHeaderCell>
                <TableHeaderCell>Added</TableHeaderCell>
                <TableHeaderCell>Updated</TableHeaderCell>
                <TableHeaderCell>Delisted</TableHeaderCell>
                <TableHeaderCell>Time</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {syncHistory.map((h: SyncHistoryItem) => (
                <TableRow key={h.id}>
                  <TableCell>{h.institutionName}</TableCell>
                  <TableCell>
                    <Badge
                      appearance="filled"
                      color={h.status === 'Completed' ? 'success' : h.status === 'Failed' ? 'danger' : 'warning'}
                    >
                      {h.status}
                    </Badge>
                  </TableCell>
                  <TableCell>{h.productsFound}</TableCell>
                  <TableCell>{h.productsAdded}</TableCell>
                  <TableCell>{h.productsUpdated}</TableCell>
                  <TableCell>{h.productsDelisted}</TableCell>
                  <TableCell>{new Date(h.startTime).toLocaleString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <Text>No sync history yet.</Text>
        )}
      </div>
    </div>
  );
}
