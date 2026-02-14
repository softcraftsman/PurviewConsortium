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

  const handleFieldChange = (field: keyof typeof form) => (
    _: unknown,
    data: { value: string }
  ) => setForm((prev) => ({ ...prev, [field]: data.value }));

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
                <Badge appearance="outline" color={inst.adminConsentGranted ? 'success' : 'warning'}>
                  {inst.adminConsentGranted ? 'Granted' : 'Pending'}
                </Badge>
              </TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: '4px' }}>
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
