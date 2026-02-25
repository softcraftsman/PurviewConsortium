import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
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
  Tooltip,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import {
  Delete24Regular,
  ArrowSync24Regular,
  PlugConnected24Regular,
} from '@fluentui/react-icons';
import { requestsApi, type AccessRequest } from '../api';

const useStyles = makeStyles({
  table: {
    marginTop: '16px',
  },
});

const statusColor = (status: string): 'success' | 'warning' | 'danger' | 'informative' | 'important' => {
  switch (status) {
    case 'Active':
    case 'Fulfilled':
      return 'success';
    case 'Submitted':
    case 'UnderReview':
      return 'warning';
    case 'Denied':
    case 'Revoked':
    case 'Expired':
      return 'danger';
    case 'Cancelled':
      return 'informative';
    default:
      return 'important';
  }
};

export default function MyRequestsPage() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: requests, isLoading } = useQuery({
    queryKey: ['myRequests'],
    queryFn: () => requestsApi.list().then((r) => r.data),
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => requestsApi.cancel(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['myRequests'] }),
  });

  const retryFulfillmentMutation = useMutation({
    mutationFn: (id: string) => requestsApi.retryFulfillment(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['myRequests'] }),
  });

  const retryError = retryFulfillmentMutation.error as any;

  if (isLoading) return <Spinner label="Loading requests..." />;

  return (
    <div>
      <Text as="h1" size={800} weight="bold" block>
        My Access Requests
      </Text>
      <Text as="p" size={400} block style={{ marginBottom: '16px' }}>
        Track the status of your data access requests.
      </Text>

      {retryError && (
        <MessageBar intent="error" style={{ marginBottom: '12px' }}>
          <MessageBarBody>
            Shortcut creation failed: {retryError?.response?.data || retryError?.message || 'Unknown error'}
          </MessageBarBody>
        </MessageBar>
      )}

      {requests && requests.length > 0 ? (
        <Table className={styles.table}>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Data Product</TableHeaderCell>
              <TableHeaderCell>Institution</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Purview Workflow</TableHeaderCell>
              <TableHeaderCell>Fabric Shortcut</TableHeaderCell>
              <TableHeaderCell>Submitted</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {requests.map((req: AccessRequest) => (
              <TableRow key={req.id}>
                <TableCell>
                  <Text weight="semibold">{req.dataProductName}</Text>
                </TableCell>
                <TableCell>{req.owningInstitutionName}</TableCell>
                <TableCell>
                  <Badge appearance="filled" color={statusColor(req.status)}>
                    {req.status}
                  </Badge>
                </TableCell>
                <TableCell>
                  {req.purviewWorkflowRunId ? (
                    <Tooltip content={`Run ID: ${req.purviewWorkflowRunId}`} relationship="label">
                      <Badge
                        appearance="tint"
                        color={
                          req.purviewWorkflowStatus === 'Completed' ? 'success' :
                          req.purviewWorkflowStatus === 'Failed' ? 'danger' :
                          req.purviewWorkflowStatus === 'Canceled' ? 'informative' :
                          'brand'
                        }
                        icon={<ArrowSync24Regular />}
                      >
                        {req.purviewWorkflowStatus || 'Submitted'}
                      </Badge>
                    </Tooltip>
                  ) : (
                    <Text size={200} style={{ opacity: 0.5 }}>—</Text>
                  )}
                </TableCell>
                <TableCell>
                  {req.fabricShortcutCreated ? (
                    <Tooltip content={`Shortcut: ${req.fabricShortcutName || 'Created'}`} relationship="label">
                      <Badge appearance="filled" color="success" icon={<PlugConnected24Regular />}>
                        {req.fabricShortcutName || 'Created'}
                      </Badge>
                    </Tooltip>
                  ) : req.externalShareId ? (
                    <Tooltip content={`External share created (ID: ${req.externalShareId}). Shortcut pending.`} relationship="label">
                      <Badge appearance="tint" color="warning">
                        Share Only
                      </Badge>
                    </Tooltip>
                  ) : req.status === 'Fulfilled' || req.status === 'Active' ? (
                    <Tooltip content="Fulfilled manually (no automated shortcut)" relationship="label">
                      <Badge appearance="tint" color="informative">
                        Manual
                      </Badge>
                    </Tooltip>
                  ) : (
                    <Text size={200} style={{ opacity: 0.5 }}>—</Text>
                  )}
                </TableCell>
                <TableCell>
                  {new Date(req.createdDate).toLocaleDateString()}
                </TableCell>
                <TableCell>
                  {(req.status === 'Submitted' || req.status === 'UnderReview') && (
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={() => cancelMutation.mutate(req.id)}
                      disabled={cancelMutation.isPending}
                    >
                      Cancel
                    </Button>
                  )}
                  {req.status === 'Approved' && !req.fabricShortcutCreated && (
                    <Tooltip content="Retry automated Fabric shortcut creation" relationship="label">
                      <Button
                        appearance="subtle"
                        icon={<PlugConnected24Regular />}
                        onClick={() => retryFulfillmentMutation.mutate(req.id)}
                        disabled={retryFulfillmentMutation.isPending}
                      >
                        Create Shortcut
                      </Button>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Text>You haven't made any access requests yet. Browse the catalog to get started.</Text>
      )}
    </div>
  );
}
