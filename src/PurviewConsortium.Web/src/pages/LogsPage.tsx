import { Fragment, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  makeStyles,
  Text,
  Spinner,
  Badge,
  Table,
  TableHeader,
  TableHeaderCell,
  TableRow,
  TableBody,
  TableCell,
  Select,
  tokens,
  Card,
  Tooltip,
  Button,
} from '@fluentui/react-components';
import {
  ArrowClockwise24Regular,
  ChevronDown24Regular,
  ChevronRight24Regular,
} from '@fluentui/react-icons';
import { logsApi, type AuditLogEntry } from '../api';

const useStyles = makeStyles({
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: '16px',
  },
  filters: {
    display: 'flex',
    gap: '12px',
    alignItems: 'center',
    marginBottom: '16px',
  },
  table: {
    marginTop: '8px',
  },
  detailsCell: {
    maxWidth: '300px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    fontSize: tokens.fontSizeBase200,
    fontFamily: 'monospace',
  },
  timestamp: {
    whiteSpace: 'nowrap',
    fontSize: tokens.fontSizeBase200,
  },
  expandedDetails: {
    padding: '12px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '4px',
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-all',
    maxHeight: '200px',
    overflow: 'auto',
  },
});

const actionColor = (action: string): 'success' | 'warning' | 'danger' | 'informative' | 'important' | 'brand' => {
  switch (action) {
    case 'ApproveRequest':
    case 'FulfillRequest':
      return 'success';
    case 'DenyRequest':
    case 'RevokeAccess':
    case 'DeactivateInstitution':
      return 'danger';
    case 'RequestAccess':
    case 'CancelRequest':
      return 'warning';
    case 'Login':
    case 'Logout':
      return 'informative';
    case 'TriggerScan':
    case 'RegisterInstitution':
    case 'UpdateInstitution':
      return 'brand';
    default:
      return 'important';
  }
};

function formatAction(action: string): string {
  // Insert spaces before uppercase letters: "FulfillRequest" -> "Fulfill Request"
  return action.replace(/([A-Z])/g, ' $1').trim();
}

function formatTimestamp(ts: string): string {
  const d = new Date(ts);
  return d.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

function tryFormatJson(json: string | undefined | null): string {
  if (!json) return '';
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}

export default function LogsPage() {
  const styles = useStyles();
  const [actionFilter, setActionFilter] = useState<string>('');
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const { data: actionTypes } = useQuery({
    queryKey: ['logActionTypes'],
    queryFn: () => logsApi.getActionTypes().then((r) => r.data),
  });

  const {
    data: logs,
    isLoading,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ['auditLogs', actionFilter],
    queryFn: () =>
      logsApi
        .getRecent({ count: 200, action: actionFilter || undefined })
        .then((r) => r.data),
  });

  return (
    <div>
      <div className={styles.header}>
        <div>
          <Text as="h1" size={800} weight="bold" block>
            Audit Logs
          </Text>
          <Text as="p" size={400} block>
            View platform activity including access requests, approvals, shortcut creation, and scans.
          </Text>
        </div>
        <Button
          appearance="subtle"
          icon={<ArrowClockwise24Regular />}
          onClick={() => refetch()}
          disabled={isFetching}
        >
          Refresh
        </Button>
      </div>

      <div className={styles.filters}>
        <Text weight="semibold">Filter by action:</Text>
        <Select
          value={actionFilter}
          onChange={(_, data) => setActionFilter(data.value)}
          style={{ minWidth: '200px' }}
        >
          <option value="">All actions</option>
          {actionTypes?.map((a) => (
            <option key={a} value={a}>
              {formatAction(a)}
            </option>
          ))}
        </Select>
        {logs && (
          <Text size={200} style={{ opacity: 0.7 }}>
            {logs.length} entries
          </Text>
        )}
      </div>

      {isLoading ? (
        <Spinner label="Loading logs..." />
      ) : logs && logs.length > 0 ? (
        <Table className={styles.table} size="small">
          <TableHeader>
            <TableRow>
              <TableHeaderCell style={{ width: '140px' }}>Time</TableHeaderCell>
              <TableHeaderCell style={{ width: '140px' }}>Action</TableHeaderCell>
              <TableHeaderCell style={{ width: '160px' }}>User</TableHeaderCell>
              <TableHeaderCell style={{ width: '100px' }}>Entity</TableHeaderCell>
              <TableHeaderCell>Details</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {logs.map((log: AuditLogEntry) => (
              <Fragment key={log.id}>
                <TableRow
                  onClick={() => setExpandedId(expandedId === log.id ? null : log.id)}
                  style={{ cursor: log.detailsJson ? 'pointer' : 'default' }}
                >
                  <TableCell>
                    <span className={styles.timestamp}>
                      {formatTimestamp(log.timestamp)}
                    </span>
                  </TableCell>
                  <TableCell>
                    <Badge
                      appearance="tint"
                      color={actionColor(log.action)}
                      size="small"
                    >
                      {formatAction(log.action)}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Text size={200}>{log.userEmail || log.userId || '—'}</Text>
                  </TableCell>
                  <TableCell>
                    {log.entityType ? (
                      <Tooltip
                        content={`${log.entityType} ${log.entityId || ''}`}
                        relationship="label"
                      >
                        <Text size={200}>{log.entityType}</Text>
                      </Tooltip>
                    ) : (
                      <Text size={200} style={{ opacity: 0.4 }}>—</Text>
                    )}
                  </TableCell>
                  <TableCell>
                    {log.detailsJson ? (
                      <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                        {expandedId === log.id ? (
                          <ChevronDown24Regular style={{ flexShrink: 0, width: 16, height: 16 }} />
                        ) : (
                          <ChevronRight24Regular style={{ flexShrink: 0, width: 16, height: 16 }} />
                        )}
                        <span className={styles.detailsCell}>
                          {log.detailsJson}
                        </span>
                      </div>
                    ) : (
                      <Text size={200} style={{ opacity: 0.4 }}>—</Text>
                    )}
                  </TableCell>
                </TableRow>
                {expandedId === log.id && log.detailsJson && (
                  <tr>
                    <td colSpan={5} style={{ padding: 0 }}>
                      <Card style={{ margin: '4px 8px 8px 8px' }}>
                        <pre className={styles.expandedDetails}>
                          {tryFormatJson(log.detailsJson)}
                        </pre>
                      </Card>
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Card style={{ padding: '24px', textAlign: 'center' }}>
          <Text>No audit log entries found.</Text>
        </Card>
      )}
    </div>
  );
}
