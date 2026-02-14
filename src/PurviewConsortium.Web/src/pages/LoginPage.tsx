import {
  makeStyles,
  tokens,
  Text,
  Button,
  Card,
} from '@fluentui/react-components';
import { ShieldLock24Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '100vh',
    backgroundColor: tokens.colorNeutralBackground2,
    gap: '24px',
  },
  card: {
    padding: '48px',
    textAlign: 'center',
    maxWidth: '400px',
  },
  icon: {
    fontSize: '48px',
    color: tokens.colorBrandForeground1,
    marginBottom: '16px',
  },
});

interface LoginPageProps {
  onLogin: () => void;
}

export default function LoginPage({ onLogin }: LoginPageProps) {
  const styles = useStyles();

  return (
    <div className={styles.root}>
      <Card className={styles.card}>
        <ShieldLock24Regular className={styles.icon} />
        <Text as="h1" size={700} weight="bold" block>
          Purview Consortium
        </Text>
        <Text as="p" size={400} block style={{ margin: '16px 0 32px' }}>
          A multi-institution Data Product sharing platform powered by
          Microsoft Purview.
        </Text>
        <Button appearance="primary" size="large" onClick={onLogin}>
          Sign in with Microsoft
        </Button>
      </Card>
    </div>
  );
}
