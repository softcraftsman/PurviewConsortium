import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Input,
  Badge,
  Spinner,
  Divider,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Tooltip,
} from '@fluentui/react-components';
import { Search24Regular } from '@fluentui/react-icons';
import { catalogApi, type DataAssetListItem } from '../api';

const useStyles = makeStyles({
  searchBar: {
    display: 'flex',
    gap: '8px',
    marginBottom: '24px',
    flexWrap: 'wrap',
  },
  searchInput: {
    flex: 1,
    minWidth: '300px',
  },
  table: {
    marginTop: '8px',
  },
  nameCell: {
    fontWeight: tokens.fontWeightSemibold,
  },
  fqnCell: {
    maxWidth: '300px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
});

/** Maps Purview assetType strings to user-friendly labels. */
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

export default function DataAssetsPage() {
  const styles = useStyles();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');

  const params: Record<string, string> = {};
  if (query) params.search = query;

  const { data, isLoading } = useQuery({
    queryKey: ['dataAssets', query],
    queryFn: () => catalogApi.getDataAssets(params).then((r) => r.data),
  });

  const handleSearch = () => {
    setQuery(search);
  };

  return (
    <div>
      <Text as="h1" size={800} weight="bold" block>
        Data Assets
      </Text>
      <Text as="p" size={400} block style={{ marginBottom: '16px' }}>
        Browse all data assets registered in the Purview Unified Catalog across
        consortium institutions.
      </Text>

      {/* Search */}
      <div className={styles.searchBar}>
        <Input
          className={styles.searchInput}
          placeholder="Search data assets..."
          value={search}
          onChange={(_, d) => setSearch(d.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          contentBefore={<Search24Regular />}
        />
      </div>

      <Divider style={{ margin: '16px 0' }} />

      {/* Results */}
      {isLoading ? (
        <Spinner label="Loading data assets..." />
      ) : (
        <>
          <Text size={300} style={{ marginBottom: '12px', display: 'block' }}>
            {data?.totalCount ?? 0} data asset{(data?.totalCount ?? 0) !== 1 ? 's' : ''}
          </Text>

          {data && data.items.length > 0 ? (
            <Table className={styles.table} size="small">
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Name</TableHeaderCell>
                  <TableHeaderCell>Asset Type</TableHeaderCell>
                  <TableHeaderCell>Workspace</TableHeaderCell>
                  <TableHeaderCell>State</TableHeaderCell>
                  <TableHeaderCell>Last Refreshed</TableHeaderCell>
                  <TableHeaderCell>Institution</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((asset) => (
                  <DataAssetRow key={asset.id} asset={asset} />
                ))}
              </TableBody>
            </Table>
          ) : (
            <Text>
              No data assets found.{' '}
              {query
                ? 'Try adjusting your search.'
                : 'Run a sync to import data assets from Purview.'}
            </Text>
          )}
        </>
      )}
    </div>
  );
}

function DataAssetRow({ asset }: { asset: DataAssetListItem }) {
  const styles = useStyles();
  return (
    <TableRow>
      <TableCell>
        <div>
          <Text className={styles.nameCell}>{asset.name}</Text>
          {asset.description && (
            <Text size={200} block>
              {asset.description}
            </Text>
          )}
        </div>
      </TableCell>
      <TableCell>
        <Badge appearance="outline" color="informative" size="small">
          {formatAssetType(asset.assetType)}
        </Badge>
      </TableCell>
      <TableCell>
        {asset.workspaceName ?? '—'}
      </TableCell>
      <TableCell>
        <StateBadge state={asset.provisioningState} />
      </TableCell>
      <TableCell>{formatDate(asset.lastRefreshedAt)}</TableCell>
      <TableCell>
        <Tooltip content={asset.institutionId} relationship="description">
          <Text>{asset.institutionName}</Text>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
}

function StateBadge({ state }: { state?: string }) {
  if (!state) return <Text>—</Text>;
  const color =
    state === 'Succeeded'
      ? 'success'
      : state === 'SoftDeleted'
        ? 'warning'
        : 'informative';
  return (
    <Badge
      appearance="filled"
      color={color as 'success' | 'warning' | 'informative'}
      size="small"
    >
      {state}
    </Badge>
  );
}
