import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  makeStyles,
  tokens,
  Text,
  Card,
  CardHeader,
  Spinner,
  Badge,
  Button,
  Divider,
} from '@fluentui/react-components';
import { catalogApi } from '../api';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
    gap: '16px',
    marginBottom: '24px',
  },
  statCard: {
    padding: '20px',
    textAlign: 'center',
  },
  statValue: {
    fontSize: '36px',
    fontWeight: 'bold',
    color: tokens.colorBrandForeground1,
  },
  section: {
    marginTop: '32px',
  },
});

export default function DashboardPage() {
  const styles = useStyles();
  const navigate = useNavigate();

  const { data: stats, isLoading } = useQuery({
    queryKey: ['catalogStats'],
    queryFn: () => catalogApi.getStats().then((r) => r.data),
  });

  if (isLoading) return <Spinner label="Loading dashboard..." />;

  return (
    <div>
      <Text as="h1" size={800} weight="bold" block>
        Dashboard
      </Text>
      <Text as="p" size={400} block style={{ marginBottom: '24px' }}>
        Overview of the consortium Data Product catalog.
      </Text>

      {/* Stat Cards */}
      <div className={styles.grid}>
        <Card className={styles.statCard}>
          <div className={styles.statValue}>{stats?.totalProducts ?? 0}</div>
          <Text size={300}>Data Products</Text>
        </Card>
        <Card className={styles.statCard}>
          <div className={styles.statValue}>{stats?.totalInstitutions ?? 0}</div>
          <Text size={300}>Institutions</Text>
        </Card>
        <Card className={styles.statCard}>
          <div className={styles.statValue}>{stats?.userPendingRequests ?? 0}</div>
          <Text size={300}>Your Pending Requests</Text>
        </Card>
        <Card className={styles.statCard}>
          <div className={styles.statValue}>{stats?.userActiveShares ?? 0}</div>
          <Text size={300}>Your Active Shares</Text>
        </Card>
      </div>

      <Divider />

      {/* Products by Institution */}
      <div className={styles.section}>
        <Text as="h2" size={600} weight="semibold" block>
          Products by Institution
        </Text>
        <div className={styles.grid}>
          {stats?.productsByInstitution &&
            Object.entries(stats.productsByInstitution).map(([name, count]) => (
              <Card key={name}>
                <CardHeader
                  header={<Text weight="semibold">{name}</Text>}
                  action={<Badge appearance="filled">{count}</Badge>}
                />
              </Card>
            ))}
        </div>
      </div>

      <Button appearance="primary" onClick={() => navigate('/catalog')}>
        Browse Catalog
      </Button>
    </div>
  );
}
