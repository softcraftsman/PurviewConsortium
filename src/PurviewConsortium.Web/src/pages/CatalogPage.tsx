import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  makeStyles,
  tokens,
  Text,
  Input,
  Button,
  Card,
  CardHeader,
  CardPreview,
  Badge,
  Spinner,
  Dropdown,
  Option,
  Divider,
} from '@fluentui/react-components';
import { Search24Regular } from '@fluentui/react-icons';
import { catalogApi, type DataProductListItem } from '../api';

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
  results: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))',
    gap: '16px',
  },
  card: {
    cursor: 'pointer',
    ':hover': {
      boxShadow: tokens.shadow8,
    },
  },
  badges: {
    display: 'flex',
    gap: '4px',
    flexWrap: 'wrap',
    marginTop: '8px',
  },
  pagination: {
    display: 'flex',
    justifyContent: 'center',
    gap: '8px',
    marginTop: '24px',
  },
  filters: {
    display: 'flex',
    gap: '12px',
    marginBottom: '16px',
    flexWrap: 'wrap',
    alignItems: 'end',
  },
});

export default function CatalogPage() {
  const styles = useStyles();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [institution, setInstitution] = useState<string>('');

  const { data: filters } = useQuery({
    queryKey: ['catalogFilters'],
    queryFn: () => catalogApi.getFilters().then((r) => r.data),
  });

  const params: Record<string, string> = {
    page: String(page),
    pageSize: '20',
  };
  if (query) params.search = query;
  if (institution) params.institutions = institution;

  const { data, isLoading } = useQuery({
    queryKey: ['catalogProducts', query, page, institution],
    queryFn: () => catalogApi.search(params).then((r) => r.data),
  });

  const handleSearch = () => {
    setPage(1);
    setQuery(search);
  };

  return (
    <div>
      <Text as="h1" size={800} weight="bold" block>
        Data Product Catalog
      </Text>
      <Text as="p" size={400} block style={{ marginBottom: '16px' }}>
        Discover and request access to shared Data Products across the consortium.
      </Text>

      {/* Search */}
      <div className={styles.searchBar}>
        <Input
          className={styles.searchInput}
          placeholder="Search data products..."
          value={search}
          onChange={(_, d) => setSearch(d.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          contentBefore={<Search24Regular />}
        />
        <Button appearance="primary" onClick={handleSearch}>
          Search
        </Button>
      </div>

      {/* Filters */}
      <div className={styles.filters}>
        <div>
          <Text size={200} weight="semibold" block>
            Institution
          </Text>
          <Dropdown
            placeholder="All institutions"
            value={institution ? filters?.institutions.find((i) => i.id === institution)?.name : ''}
            onOptionSelect={(_, d) => {
              setInstitution(d.optionValue as string ?? '');
              setPage(1);
            }}
          >
            <Option value="">All</Option>
            {filters?.institutions.map((inst) => (
              <Option key={inst.id} value={inst.id}>
                {inst.name}
              </Option>
            ))}
          </Dropdown>
        </div>
      </div>

      <Divider style={{ margin: '16px 0' }} />

      {/* Results */}
      {isLoading ? (
        <Spinner label="Searching..." />
      ) : (
        <>
          <Text size={300} style={{ marginBottom: '12px', display: 'block' }}>
            {data?.totalCount ?? 0} results
          </Text>
          <div className={styles.results}>
            {data?.items.map((product) => (
              <ProductCard
                key={product.id}
                product={product}
                onClick={() => navigate(`/catalog/${product.id}`)}
              />
            ))}
            {data?.items.length === 0 && (
              <Text>No data products found. Try adjusting your search or filters.</Text>
            )}
          </div>

          {/* Pagination */}
          {(data?.totalCount ?? 0) > 20 && (
            <div className={styles.pagination}>
              <Button disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Previous
              </Button>
              <Text style={{ alignSelf: 'center' }}>
                Page {page} of {Math.ceil((data?.totalCount ?? 0) / 20)}
              </Text>
              <Button
                disabled={page * 20 >= (data?.totalCount ?? 0)}
                onClick={() => setPage(page + 1)}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function ProductCard({
  product,
  onClick,
}: {
  product: DataProductListItem;
  onClick: () => void;
}) {
  const styles = useStyles();
  return (
    <Card className={styles.card} onClick={onClick}>
      <CardHeader
        header={<Text weight="semibold">{product.name}</Text>}
        description={<Text size={200}>{product.institutionName}</Text>}
      />
      <CardPreview>
        <div style={{ padding: '0 16px 12px' }}>
          <Text size={300} truncate block style={{ maxWidth: '100%' }}>
            {product.description || 'No description available.'}
          </Text>
          <div className={styles.badges}>
            {product.sensitivityLabel && (
              <Badge appearance="outline" color="danger" size="small">
                {product.sensitivityLabel}
              </Badge>
            )}
            {product.classifications.slice(0, 3).map((c) => (
              <Badge key={c} appearance="tint" size="small">
                {c}
              </Badge>
            ))}
            {product.sourceSystem && (
              <Badge appearance="outline" color="informative" size="small">
                {product.sourceSystem}
              </Badge>
            )}
          </div>
        </div>
      </CardPreview>
    </Card>
  );
}
