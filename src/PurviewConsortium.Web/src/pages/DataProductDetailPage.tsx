import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useMsal } from '@azure/msal-react';
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
  Caption1,
} from '@fluentui/react-components';
import {
  ArrowLeft24Regular,
  LockOpen24Regular,
  Open24Regular,
} from '@fluentui/react-icons';
import { catalogApi, requestsApi, type DataProductLink } from '../api';

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
  subSection: {
    marginTop: '16px',
  },
  ownerGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
    gap: '12px',
    marginTop: '12px',
  },
  detailCard: {
    padding: '16px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  assetGrid: {
    display: 'grid',
    gap: '16px',
    marginTop: '12px',
  },
  assetMeta: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: '12px',
    marginTop: '12px',
  },
  linkGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
    gap: '16px',
    marginTop: '12px',
  },
  linkGroup: {
    padding: '16px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  linkList: {
    display: 'flex',
    flexDirection: 'column',
    gap: '10px',
    marginTop: '12px',
  },
  linkItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    paddingBottom: '10px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
  },
  externalLink: {
    color: tokens.colorBrandForegroundLink,
    textDecorationLine: 'none',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
  },
});

export default function DataProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const styles = useStyles();
  const queryClient = useQueryClient();
  const { instance } = useMsal();
  const [requestDialogOpen, setRequestDialogOpen] = useState(false);
  const [justification, setJustification] = useState('');
  const [targetWorkspace, setTargetWorkspace] = useState('');
  const [targetLakehouse, setTargetLakehouse] = useState('');
  const [durationDays, setDurationDays] = useState('');

  const { data: product, isLoading } = useQuery({
    queryKey: ['product', id],
    queryFn: () => catalogApi.getProduct(id!).then((r) => r.data),
    enabled: !!id,
  });

  const [requestError, setRequestError] = useState<string | null>(null);

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

  // Detect whether the current user is in the same tenant as the data product
  const currentAccount = instance.getActiveAccount() ?? instance.getAllAccounts()[0];
  const userTenantId: string | undefined = (currentAccount?.tenantId) ?? undefined;
  const isSameTenant = !!(userTenantId && product.institutionTenantId &&
    userTenantId.toLowerCase() === product.institutionTenantId.toLowerCase());

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
          <MetaField label="Data Assets" value={product.assetCount.toString()} />
          <MetaField label="Update Frequency" value={product.updateFrequency} />
          <MetaField
            label="Data Quality Score"
            value={product.dataQualityScore != null ? `${product.dataQualityScore}%` : undefined}
          />
          <MetaField
            label="System Data Last Modified"
            value={
              product.purviewLastModified
                ? new Date(product.purviewLastModified).toLocaleDateString()
                : undefined
            }
          />
        </div>
      </div>

      {product.ownerContacts.length > 0 && (
        <div className={styles.section}>
          <Text as="h2" size={600} weight="semibold" block>
            Data Product Owners
          </Text>
          <div className={styles.ownerGrid}>
            {product.ownerContacts.map((contact, index) => (
              <div key={`${contact.id ?? contact.emailAddress ?? contact.name ?? 'owner'}-${index}`} className={styles.detailCard}>
                <Text size={200} weight="semibold" block>
                  Description
                </Text>
                <Caption1>{contact.description || 'Unavailable'}</Caption1>
                <Text size={200} weight="semibold" block style={{ marginTop: '10px' }}>
                  Name
                </Text>
                <Text>{contact.name || 'Unavailable'}</Text>
                <Text size={200} weight="semibold" block style={{ marginTop: '10px' }}>
                  Email
                </Text>
                <Text>
                  {contact.emailAddress || 'Email unavailable'}
                </Text>
              </div>
            ))}
          </div>
        </div>
      )}

      {(product.businessUse || product.useCases) && (
        <div className={styles.section}>
          <Text as="h2" size={600} weight="semibold" block>
            Business Use
          </Text>
          <Text block style={{ whiteSpace: 'pre-wrap' }}>
            {product.businessUse || product.useCases}
          </Text>
        </div>
      )}

      {(product.termsOfUse.length > 0 || product.documentation.length > 0) && (
        <div className={styles.section}>
          <Text as="h2" size={600} weight="semibold" block>
            Related Links
          </Text>
          <div className={styles.linkGrid}>
            <LinkGroup title="Terms of Use" links={product.termsOfUse} emptyText="No terms of use links." />
            <LinkGroup title="Documentation" links={product.documentation} emptyText="No documentation links." />
          </div>
        </div>
      )}

      {product.dataAssets && product.dataAssets.length > 0 && (
        <div className={styles.section}>
          <Text as="h2" size={600} weight="semibold" block>
            Data Assets ({product.dataAssets.length})
          </Text>
          <div className={styles.assetGrid}>
            {product.dataAssets.map((asset) => (
              <Card key={asset.id}>
                <CardHeader
                  header={<Text weight="semibold">{asset.name}</Text>}
                  description={<Text size={200}>{asset.description || 'No description available.'}</Text>}
                  action={
                    <Badge appearance="outline" color="informative" size="small">
                      {formatAssetType(asset.assetType)}
                    </Badge>
                  }
                />
                <div style={{ padding: '0 16px 16px' }}>
                  <div className={styles.assetMeta}>
                    <MetaField label="Last Refreshed" value={formatDate(asset.lastRefreshedAt)} />
                    <MetaField label="Last Modified" value={formatDate(asset.purviewLastModifiedAt)} />
                    <MetaField label="Institution" value={asset.institutionName} />
                    <MetaField label="Asset ID" value={asset.purviewAssetId} />
                  </div>
                  <div className={styles.subSection}>
                    <LinkGroup title="Terms of Use" links={asset.termsOfUse} emptyText="No linked terms of use." compact />
                  </div>
                  <div className={styles.subSection}>
                    <LinkGroup title="Documentation" links={asset.documentation} emptyText="No linked documentation." compact />
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </div>
      )}

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

      <Divider style={{ margin: '24px 0' }} />

      {/* Qualified Name */}
      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
        Purview Qualified Name: {product.purviewQualifiedName}
      </Text>

      {/* Request Access Dialog */}
      <Dialog open={requestDialogOpen} onOpenChange={(_, d) => setRequestDialogOpen(d.open)}>
        <DialogSurface>
          <DialogTitle>Request Access to {product.name}</DialogTitle>
          <DialogBody>
            <DialogContent>
              {isSameTenant ? (
                <MessageBar intent="info" style={{ marginBottom: '12px' }}>
                  <MessageBarBody>
                    You are in the same tenant as this Data Product. If automated fulfillment is
                    enabled for this institution, a direct OneLake shortcut will be created
                    automatically after approval — no External Data Share required.
                  </MessageBarBody>
                </MessageBar>
              ) : (
                <MessageBar intent="info" style={{ marginBottom: '12px' }}>
                  <MessageBarBody>
                    This Data Product is owned by a different tenant. An External Data Share will
                    be requested. Fulfillment may be automated or manual depending on the institution's
                    configuration. Providing your Target Workspace and Lakehouse IDs below enables
                    automated shortcut creation.
                  </MessageBarBody>
                </MessageBar>
              )}
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
              <Field
                label="Target Fabric Workspace ID (your workspace)"
                hint="Required for automated shortcut creation. Leave blank if you will configure the shortcut manually after approval."
                style={{ marginTop: '12px' }}
              >
                <Input
                  value={targetWorkspace}
                  onChange={(_, d) => setTargetWorkspace(d.value)}
                  placeholder="Your Fabric workspace GUID"
                />
              </Field>
              <Field
                label="Target Lakehouse Item ID (your lakehouse)"
                hint="Required for automated shortcut creation. Leave blank if you will configure the shortcut manually after approval."
                style={{ marginTop: '12px' }}
              >
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

function LinkGroup({
  title,
  links,
  emptyText,
  compact = false,
}: {
  title: string;
  links: DataProductLink[];
  emptyText: string;
  compact?: boolean;
}) {
  const styles = useStyles();

  return (
    <div className={compact ? undefined : styles.linkGroup}>
      <Text weight="semibold" block>
        {title}
      </Text>
      {links.length === 0 ? (
        <Text size={300} style={{ marginTop: '8px', display: 'block' }}>
          {emptyText}
        </Text>
      ) : (
        <div className={styles.linkList}>
          {links.map((link, index) => (
            <div key={`${title}-${link.dataAssetId ?? 'unmapped'}-${link.url ?? link.name ?? index}`} className={styles.linkItem}>
              <Text size={200} weight="semibold">
                {link.dataAssetName}
              </Text>
              <Text>{link.name || 'Untitled link'}</Text>
              {link.url ? (
                <a
                  className={styles.externalLink}
                  href={link.url}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <Open24Regular fontSize={16} />
                  <span>{link.url}</span>
                </a>
              ) : (
                <Text size={200}>URL unavailable</Text>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function formatAssetType(assetType?: string): string {
  if (!assetType) return '—';
  const map: Record<string, string> = {
    fabric_lakehouse: 'Fabric Lakehouse',
    fabric_lakehouse_path: 'Fabric Lakehouse Path',
    fabric_lake_warehouse: 'Fabric Lake Warehouse',
    powerbi_dataset: 'Power BI Dataset',
    azure_blob_container: 'Azure Blob Container',
    azure_datalake_gen2_path: 'ADLS Gen2 Path',
    azure_datalake_gen2_resource_set: 'ADLS Gen2 Resource Set',
    azure_ml: 'Azure ML',
  };
  return map[assetType] ?? assetType.replace(/_/g, ' ');
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return '—';
  try {
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  } catch {
    return dateStr;
  }
}

