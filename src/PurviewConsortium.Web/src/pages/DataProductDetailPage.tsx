import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Spinner,
  Button,
  Badge,
  Card,
  CardHeader,
  Divider,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Textarea,
  Input,
  Field,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import {
  ArrowLeft24Regular,
  LockOpen24Regular,
} from '@fluentui/react-icons';
import { catalogApi, requestsApi } from '../api';

const useStyles = makeStyles({
  header: {
    display: 'flex',
    gap: '16px',
    alignItems: 'flex-start',
    marginBottom: '24px',
  },
  section: {
    marginTop: '24px',
  },
  badges: {
    display: 'flex',
    gap: '6px',
    flexWrap: 'wrap',
  },
  meta: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: '12px',
    marginTop: '16px',
  },
  metaItem: {
    display: 'flex',
    flexDirection: 'column',
  },
});

export default function DataProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [requestDialogOpen, setRequestDialogOpen] = useState(false);
  const [justification, setJustification] = useState('');
  const [targetWorkspace, setTargetWorkspace] = useState('');
  const [targetLakehouse, setTargetLakehouse] = useState('');
  const [durationDays, setDurationDays] = useState('');

  const [editLakehouseId, setEditLakehouseId] = useState('');
  const [editSourceOpen, setEditSourceOpen] = useState(false);

  const { data: product, isLoading } = useQuery({
    queryKey: ['product', id],
    queryFn: () => catalogApi.getProduct(id!).then((r) => r.data),
    enabled: !!id,
  });

  const [requestError, setRequestError] = useState<string | null>(null);

  const saveSourceLakehouse = useMutation({
    mutationFn: () =>
      catalogApi.updateProductFabric(id!, {
        sourceLakehouseItemId: editLakehouseId || undefined,
      }),
    onSuccess: () => {
      setEditSourceOpen(false);
      queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
  });

  const submitRequest = useMutation({
    mutationFn: () =>
      requestsApi.create({
        dataProductId: id!,
        businessJustification: justification,
        targetFabricWorkspaceId: targetWorkspace || undefined,
        targetLakehouseItemId: targetLakehouse || undefined,
        requestedDurationDays: durationDays ? parseInt(durationDays) : undefined,
      }),
    onSuccess: () => {
      setRequestError(null);
      setRequestDialogOpen(false);
      setJustification('');
      setTargetWorkspace('');
      setTargetLakehouse('');
      setDurationDays('');
      queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error: any) => {
      const message = error?.response?.data || error?.message || 'Failed to submit access request.';
      setRequestError(typeof message === 'string' ? message : JSON.stringify(message));
    },
  });

  if (isLoading) return <Spinner label="Loading..." />;
  if (!product) return <Text>Data Product not found.</Text>;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/catalog')}
      >
        Back to Catalog
      </Button>

      <div className={styles.header}>
        <div style={{ flex: 1 }}>
          <Text as="h1" size={800} weight="bold" block>
            {product.name}
          </Text>
          <Text size={400} block>
            {product.institutionName}
          </Text>
        </div>
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          {product.currentUserRequest && (
            <Badge appearance="filled" color="informative" size="large">
              Existing: {product.currentUserRequest.status}
            </Badge>
          )}
          <Button
            appearance="primary"
            icon={<LockOpen24Regular />}
            onClick={() => { setRequestError(null); setRequestDialogOpen(true); }}
          >
            Request Access
          </Button>
        </div>
      </div>

      {/* Description */}
      <Card>
        <CardHeader header={<Text weight="semibold">Description</Text>} />
        <div style={{ padding: '0 16px 16px' }}>
          <Text>{product.description || 'No description available.'}</Text>
        </div>
      </Card>

      {/* Metadata */}
      <div className={styles.section}>
        <Text as="h2" size={600} weight="semibold" block>
          Details
        </Text>
        <div className={styles.meta}>
          <MetaField label="Owner" value={product.owner} />
          <MetaField label="Owner Email" value={product.ownerEmail} />
          <MetaField label="Source System" value={product.sourceSystem} />
          <MetaField label="Sensitivity Label" value={product.sensitivityLabel} />
          <MetaField label="Contact" value={product.institutionContactEmail} />
          <MetaField
            label="Source Lakehouse Item ID"
            value={product.sourceLakehouseItemId || '(not configured)'}
          />
          <div style={{ display: 'flex', alignItems: 'flex-end' }}>
            <Button
              size="small"
              onClick={() => {
                setEditLakehouseId(product.sourceLakehouseItemId || '');
                setEditSourceOpen(true);
              }}
            >
              {product.sourceLakehouseItemId ? 'Edit' : 'Set Source Lakehouse'}
            </Button>
          </div>
          <MetaField
            label="Last Updated in Purview"
            value={
              product.purviewLastModified
                ? new Date(product.purviewLastModified).toLocaleDateString()
                : undefined
            }
          />
        </div>
      </div>

      {/* Classifications & Glossary Terms */}
      <div className={styles.section}>
        <Text as="h2" size={600} weight="semibold" block>
          Classifications
        </Text>
        <div className={styles.badges}>
          {product.classifications.length > 0
            ? product.classifications.map((c) => (
                <Badge key={c} appearance="tint">
                  {c}
                </Badge>
              ))
            : <Text size={300}>None</Text>}
        </div>
      </div>

      <div className={styles.section}>
        <Text as="h2" size={600} weight="semibold" block>
          Glossary Terms
        </Text>
        <div className={styles.badges}>
          {product.glossaryTerms.length > 0
            ? product.glossaryTerms.map((t) => (
                <Badge key={t} appearance="tint" color="brand">
                  {t}
                </Badge>
              ))
            : <Text size={300}>None</Text>}
        </div>
      </div>

      <Divider style={{ margin: '24px 0' }} />

      {/* Qualified Name */}
      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
        Purview Qualified Name: {product.purviewQualifiedName}
      </Text>

      {/* Edit Source Lakehouse Dialog */}
      <Dialog open={editSourceOpen} onOpenChange={(_, d) => setEditSourceOpen(d.open)}>
        <DialogSurface>
          <DialogTitle>Set Source Lakehouse Item ID</DialogTitle>
          <DialogBody>
            <DialogContent>
              <Text size={300} block style={{ marginBottom: '8px' }}>
                This is the Fabric lakehouse item ID of the Purview Data Asset. 
                It will be used as the source when creating cross-tenant shortcuts.
              </Text>
              <Field label="Source Lakehouse Item ID">
                <Input
                  value={editLakehouseId}
                  onChange={(_, d) => setEditLakehouseId(d.value)}
                  placeholder="Fabric lakehouse item GUID"
                />
              </Field>
            </DialogContent>
          </DialogBody>
          <DialogActions>
            <Button onClick={() => setEditSourceOpen(false)}>Cancel</Button>
            <Button
              appearance="primary"
              disabled={saveSourceLakehouse.isPending}
              onClick={() => saveSourceLakehouse.mutate()}
            >
              {saveSourceLakehouse.isPending ? 'Saving...' : 'Save'}
            </Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>

      {/* Request Access Dialog */}
      <Dialog open={requestDialogOpen} onOpenChange={(_, d) => setRequestDialogOpen(d.open)}>
        <DialogSurface>
          <DialogTitle>Request Access to {product.name}</DialogTitle>
          <DialogBody>
            <DialogContent>
              {requestError && (
                <MessageBar intent="error" style={{ marginBottom: '12px' }}>
                  <MessageBarBody>{requestError}</MessageBarBody>
                </MessageBar>
              )}
              <Field label="Business Justification" required>
                <Textarea
                  value={justification}
                  onChange={(_, d) => setJustification(d.value)}
                  placeholder="Explain why you need access to this Data Product..."
                  resize="vertical"
                  rows={4}
                />
              </Field>
              <Field label="Target Fabric Workspace ID (your workspace)" style={{ marginTop: '12px' }}>
                <Input
                  value={targetWorkspace}
                  onChange={(_, d) => setTargetWorkspace(d.value)}
                  placeholder="Your Fabric workspace GUID"
                />
              </Field>
              <Field label="Target Lakehouse Item ID (your lakehouse)" style={{ marginTop: '12px' }}>
                <Input
                  value={targetLakehouse}
                  onChange={(_, d) => setTargetLakehouse(d.value)}
                  placeholder="Your lakehouse item GUID"
                />
              </Field>
              <Field label="Requested Duration (days)" style={{ marginTop: '12px' }}>
                <Input
                  type="number"
                  value={durationDays}
                  onChange={(_, d) => setDurationDays(d.value)}
                  placeholder="Optional: leave blank for indefinite"
                />
              </Field>
            </DialogContent>
          </DialogBody>
          <DialogActions>
            <Button onClick={() => setRequestDialogOpen(false)}>Cancel</Button>
            <Button
              appearance="primary"
              disabled={!justification.trim() || submitRequest.isPending}
              onClick={() => submitRequest.mutate()}
            >
              {submitRequest.isPending ? 'Submitting...' : 'Submit Request'}
            </Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>
    </div>
  );
}

function MetaField({ label, value }: { label: string; value?: string | null }) {
  const styles = useStyles();
  return (
    <div className={styles.metaItem}>
      <Text size={200} weight="semibold" style={{ color: tokens.colorNeutralForeground3 }}>
        {label}
      </Text>
      <Text>{value || '—'}</Text>
    </div>
  );
}
