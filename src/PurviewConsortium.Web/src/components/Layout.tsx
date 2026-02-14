import { ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Avatar,
  Tab,
  TabList,
} from '@fluentui/react-components';
import {
  SignOut24Regular,
  Home24Regular,
  Search24Regular,
  DocumentBulletList24Regular,
  Building24Regular,
  BookQuestionMark24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 24px',
    height: '56px',
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    boxShadow: tokens.shadow4,
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  nav: {
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    paddingLeft: '16px',
  },
  content: {
    flex: 1,
    padding: '24px',
    maxWidth: '1200px',
    width: '100%',
    margin: '0 auto',
    boxSizing: 'border-box',
  },
});

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const { instance, accounts } = useMsal();

  const account = accounts[0];
  const pathSegments = location.pathname.split('/');
  const currentPath = pathSegments[1] === 'admin'
    ? '/admin/institutions'
    : pathSegments[1] === 'setup'
    ? '/setup'
    : '/' + pathSegments[1];

  return (
    <div className={styles.root}>
      {/* Header */}
      <header className={styles.header}>
        <div className={styles.headerLeft}>
          <Text size={500} weight="bold" style={{ color: 'white' }}>
            Purview Consortium
          </Text>
        </div>
        <div className={styles.headerRight}>
          <Avatar
            name={account?.name ?? 'User'}
            size={32}
            color="neutral"
          />
          <Text size={300} style={{ color: 'white' }}>
            {account?.name}
          </Text>
          <Button
            appearance="subtle"
            icon={<SignOut24Regular />}
            onClick={() => instance.logoutRedirect()}
            title="Sign out"
            style={{ color: 'white' }}
          />
        </div>
      </header>

      {/* Navigation */}
      <nav className={styles.nav}>
        <TabList
          selectedValue={currentPath}
          onTabSelect={(_, data) => navigate(data.value as string)}
        >
          <Tab value="/" icon={<Home24Regular />}>
            Dashboard
          </Tab>
          <Tab value="/catalog" icon={<Search24Regular />}>
            Catalog
          </Tab>
          <Tab value="/requests" icon={<DocumentBulletList24Regular />}>
            My Requests
          </Tab>
          <Tab value="/admin/institutions" icon={<Building24Regular />}>
            Admin
          </Tab>
          <Tab value="/setup" icon={<BookQuestionMark24Regular />}>
            Setup Guide
          </Tab>
        </TabList>
      </nav>

      {/* Content */}
      <main className={styles.content}>{children}</main>
    </div>
  );
}
