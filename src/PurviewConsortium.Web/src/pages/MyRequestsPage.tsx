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
} from '@fluentui/react-components';
import { Delete24Regular } from '@fluentui/react-icons';
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

  if (isLoading) return <Spinner label="Loading requests..." />;

  return (
    <div>
      <Text as="h1" size={800} weight="bold" block>
        My Access Requests
      </Text>
      <Text as="p" size={400} block style={{ marginBottom: '16px' }}>
        Track the status of your data access requests.
      </Text>

      {requests && requests.length > 0 ? (
        <Table className={styles.table}>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Data Product</TableHeaderCell>
              <TableHeaderCell>Institution</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
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
