import {
  Text,
  Card,
  makeStyles,
  tokens,
  Divider,
  Badge,
} from '@fluentui/react-components';
import {
  ShieldCheckmark24Regular,
  Key24Regular,
  People24Regular,
  CheckmarkCircle24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  page: {
    maxWidth: '900px',
  },
  section: {
    marginBottom: '32px',
  },
  card: {
    padding: '20px',
    marginBottom: '16px',
  },
  stepList: {
    listStyleType: 'none',
    padding: '0',
    margin: '0',
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
  },
  step: {
    display: 'flex',
    gap: '16px',
    alignItems: 'flex-start',
  },
  stepNumber: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: '32px',
    height: '32px',
    borderRadius: '50%',
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    fontWeight: 'bold',
    fontSize: '14px',
  },
  stepContent: {
    flex: 1,
  },
  codeBlock: {
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '6px',
    padding: '16px',
    fontFamily: 'monospace',
    fontSize: '13px',
    overflowX: 'auto',
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-all',
    marginTop: '8px',
    marginBottom: '8px',
    lineHeight: '1.6',
  },
  inlineCode: {
    backgroundColor: tokens.colorNeutralBackground3,
    padding: '2px 6px',
    borderRadius: '4px',
    fontFamily: 'monospace',
    fontSize: '13px',
  },
  note: {
    backgroundColor: tokens.colorPaletteYellowBackground1,
    border: `1px solid ${tokens.colorPaletteYellowBorder1}`,
    borderRadius: '6px',
    padding: '12px 16px',
    marginTop: '12px',
    marginBottom: '12px',
  },
  info: {
    backgroundColor: tokens.colorPaletteBlueBorderActive,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: '6px',
    padding: '12px 16px',
    marginTop: '12px',
    marginBottom: '12px',
    color: tokens.colorNeutralForegroundOnBrand,
  },
  tableWrapper: {
    overflowX: 'auto',
    marginTop: '8px',
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '14px',
  },
});

function Step({ number, title, children }: { number: number; title: string; children: React.ReactNode }) {
  const styles = useStyles();
  return (
    <li className={styles.step}>
      <div className={styles.stepNumber}>{number}</div>
      <div className={styles.stepContent}>
        <Text weight="semibold" size={400} block style={{ marginBottom: '4px' }}>
          {title}
        </Text>
        {children}
      </div>
    </li>
  );
}

export default function SetupGuidePage() {
  const styles = useStyles();

  return (
    <div className={styles.page}>
      <Text as="h1" size={800} weight="bold" block>
        Setup Guide
      </Text>
      <Text size={400} block style={{ marginBottom: '24px' }}>
        How to configure the consortium multi-tenant app and onboard institutions.
      </Text>

      {/* Part 1: Consortium App Registration */}
      <div className={styles.section}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <ShieldCheckmark24Regular />
          <Text as="h2" size={600} weight="semibold">
            Part 1: Register the Consortium App in Entra ID
          </Text>
        </div>

        <Card className={styles.card}>
          <Text block style={{ marginBottom: '16px' }}>
            The consortium operates a single multi-tenant Entra ID application. This app is registered in the
            consortium operator's tenant and is what each institution will grant consent to.
          </Text>

          <ol className={styles.stepList}>
            <Step number={1} title="Go to the Azure Portal">
              <Text block>
                Navigate to{' '}
                <span className={styles.inlineCode}>Azure Portal → Microsoft Entra ID → App registrations → New registration</span>
              </Text>
            </Step>

            <Step number={2} title="Configure the registration">
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'left', padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Field</th>
                    <th style={{ textAlign: 'left', padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Name</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <span className={styles.inlineCode}>Purview Consortium Platform</span>
                    </td>
                  </tr>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Supported account types</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <strong>Accounts in any organizational directory (Any Microsoft Entra ID tenant — Multitenant)</strong>
                    </td>
                  </tr>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Redirect URI (SPA)</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <span className={styles.inlineCode}>http://localhost:5173</span> (dev) /{' '}
                      <span className={styles.inlineCode}>https://your-domain.com</span> (prod)
                    </td>
                  </tr>
                </tbody>
              </table>
            </Step>

            <Step number={3} title="Add API permissions">
              <Text block>
                Under <span className={styles.inlineCode}>API permissions → Add a permission</span>, add:
              </Text>
              <div className={styles.codeBlock}>
{`Microsoft Graph
  └─ User.Read                         (Delegated — sign-in)

Microsoft Purview (if available in your tenant)
  └─ Purview.Read.All                  (Application — read catalog data)

Azure Service Management
  └─ user_impersonation                (Delegated — optional, for Fabric API)`}
              </div>
              <div className={styles.note}>
                <Text weight="semibold">Note: </Text>
                <Text>
                  The Purview Data Products API (preview) may use Azure Resource Manager permissions instead.
                  Check the latest Microsoft documentation for the exact permissions required in your environment.
                </Text>
              </div>
            </Step>

            <Step number={4} title="Create a client secret">
              <Text block>
                Under <span className={styles.inlineCode}>Certificates & secrets → New client secret</span>:
              </Text>
              <ul style={{ margin: '8px 0', paddingLeft: '20px' }}>
                <li>Description: <span className={styles.inlineCode}>Consortium Platform Secret</span></li>
                <li>Expiry: 12 or 24 months</li>
                <li>Copy the <strong>Value</strong> immediately — it won't be shown again</li>
              </ul>
            </Step>

            <Step number={5} title="Expose an API (for SPA auth)">
              <Text block>Under <span className={styles.inlineCode}>Expose an API</span>:</Text>
              <ul style={{ margin: '8px 0', paddingLeft: '20px' }}>
                <li>
                  Set Application ID URI to{' '}
                  <span className={styles.inlineCode}>api://{'<'}your-client-id{'>'}</span>
                </li>
                <li>
                  Add a scope: <span className={styles.inlineCode}>access_as_user</span> (Admins and users)
                </li>
              </ul>
            </Step>

            <Step number={6} title="Configure app roles (optional)">
              <Text block>Under <span className={styles.inlineCode}>App roles</span>, create:</Text>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'left', padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Role</th>
                    <th style={{ textAlign: 'left', padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Value</th>
                    <th style={{ textAlign: 'left', padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Description</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Consortium Admin</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <span className={styles.inlineCode}>Consortium.Admin</span>
                    </td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Full platform admin</td>
                  </tr>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Institution Admin</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <span className={styles.inlineCode}>Institution.Admin</span>
                    </td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Manages their institution</td>
                  </tr>
                  <tr>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Member</td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <span className={styles.inlineCode}>Consortium.Member</span>
                    </td>
                    <td style={{ padding: '8px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Browse catalog, request access</td>
                  </tr>
                </tbody>
              </table>
            </Step>

            <Step number={7} title="Record your values">
              <Text block>Save these values for the platform configuration:</Text>
              <div className={styles.codeBlock}>
{`Application (client) ID:   xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Directory (tenant) ID:     xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx  (consortium tenant)
Client Secret Value:       ••••••••••••••••••••••••••••••••••••••`}
              </div>
              <Text block style={{ marginTop: '8px' }}>
                Set these in the API's <span className={styles.inlineCode}>appsettings.json</span> under{' '}
                <span className={styles.inlineCode}>AzureAd</span> and in the frontend's{' '}
                <span className={styles.inlineCode}>.env</span> as{' '}
                <span className={styles.inlineCode}>VITE_AZURE_CLIENT_ID</span> and{' '}
                <span className={styles.inlineCode}>VITE_AZURE_TENANT_ID</span>.
              </Text>
            </Step>
          </ol>
        </Card>
      </div>

      <Divider style={{ margin: '32px 0' }} />

      {/* Part 2: Institution Onboarding */}
      <div className={styles.section}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <People24Regular />
          <Text as="h2" size={600} weight="semibold">
            Part 2: Onboarding an Institution (Admin Consent)
          </Text>
        </div>

        <Card className={styles.card}>
          <Text block style={{ marginBottom: '16px' }}>
            Each institution that joins the consortium must grant admin consent so the platform can read their
            Purview catalog. This is a one-time step performed by the institution's IT/Entra ID administrator.
          </Text>

          <ol className={styles.stepList}>
            <Step number={1} title="Register the institution in the platform">
              <Text block>
                Go to the <strong>Admin</strong> tab and click <strong>Add Institution</strong>. Fill in the
                institution's name, Entra tenant ID, Purview account name, and contact email.
              </Text>
            </Step>

            <Step number={2} title="Send the admin consent link">
              <Text block>
                Share the following URL with the institution's Entra ID administrator (typically a Global Admin
                or Application Administrator). Replace the placeholders:
              </Text>
              <div className={styles.codeBlock}>
{`https://login.microsoftonline.com/{institution-tenant-id}/adminconsent
  ?client_id={your-consortium-app-client-id}
  &redirect_uri={your-app-url}/admin/consent-callback
  &state={institution-id}`}
              </div>
              <Text block style={{ marginTop: '8px' }}>
                <strong>Example</strong> (for Contoso University with tenant{' '}
                <span className={styles.inlineCode}>contoso-tenant-id</span>):
              </Text>
              <div className={styles.codeBlock}>
{`https://login.microsoftonline.com/contoso-tenant-id/adminconsent
  ?client_id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  &redirect_uri=https://consortium.example.com/admin/consent-callback
  &state=11111111-1111-1111-1111-111111111111`}
              </div>
            </Step>

            <Step number={3} title="Institution admin grants consent">
              <Text block>
                The institution admin follows these steps:
              </Text>
              <ul style={{ margin: '8px 0', paddingLeft: '20px', lineHeight: '1.8' }}>
                <li>Click the consent URL → Entra ID sign-in page appears</li>
                <li>Sign in with their admin account</li>
                <li>
                  Review the permissions requested:
                  <ul style={{ marginTop: '4px', paddingLeft: '16px' }}>
                    <li><Badge appearance="outline" color="informative">User.Read</Badge> — Read user profile</li>
                    <li><Badge appearance="outline" color="informative">Purview.Read.All</Badge> — Read Purview catalog data</li>
                  </ul>
                </li>
                <li>Click <strong>"Accept"</strong> to grant consent on behalf of their organization</li>
                <li>Entra ID redirects back to the platform with a success confirmation</li>
              </ul>
            </Step>

            <Step number={4} title="Platform marks consent as granted">
              <Text block>
                After the redirect, the platform automatically updates the institution's{' '}
                <span className={styles.inlineCode}>adminConsentGranted</span> flag to{' '}
                <Badge appearance="filled" color="success">Granted</Badge>. The institution's
                Purview catalog is now accessible for scanning.
              </Text>
              <div className={styles.note}>
                <Text weight="semibold">Dev mode: </Text>
                <Text>
                  In development, you can manually toggle consent status via the API:
                </Text>
                <div className={styles.codeBlock} style={{ backgroundColor: 'transparent', padding: '8px 0' }}>
{`PUT /api/admin/institutions/{id}
{
  "adminConsentGranted": true
}`}
                </div>
              </div>
            </Step>

            <Step number={5} title="Run the first scan">
              <Text block>
                On the <strong>Admin</strong> page, click the <strong>Scan</strong> button next to the institution.
                The platform will authenticate using its client credentials against the institution's tenant and
                call the Purview Data Products API to discover shared data products tagged with the{' '}
                <span className={styles.inlineCode}>Consortium-Shareable</span> glossary term.
              </Text>
            </Step>
          </ol>
        </Card>
      </div>

      <Divider style={{ margin: '32px 0' }} />

      {/* Part 3: What happens under the hood */}
      <div className={styles.section}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <Key24Regular />
          <Text as="h2" size={600} weight="semibold">
            Part 3: How Authentication Works
          </Text>
        </div>

        <Card className={styles.card}>
          <Text as="h3" size={400} weight="semibold" block style={{ marginBottom: '8px' }}>
            User Authentication (SPA → API)
          </Text>
          <Text block style={{ marginBottom: '16px' }}>
            Users sign in via MSAL.js in the browser. The SPA acquires a JWT token from Entra ID scoped
            to your API (<span className={styles.inlineCode}>api://{'<'}client-id{'>'}/access_as_user</span>).
            This token is sent as a Bearer token on every API request. The backend validates it using
            Microsoft.Identity.Web.
          </Text>

          <Text as="h3" size={400} weight="semibold" block style={{ marginBottom: '8px' }}>
            Service-to-Service Authentication (API → Purview)
          </Text>
          <Text block style={{ marginBottom: '16px' }}>
            When scanning an institution's Purview, the API uses <strong>client credentials flow</strong>:
          </Text>
          <div className={styles.codeBlock}>
{`1. API creates a ClientSecretCredential with:
     - Consortium app Client ID
     - Consortium app Client Secret
     - Institution's Tenant ID

2. Requests a token for scope:
     https://purview.azure.net/.default

3. Calls the Purview Data Products API:
     GET https://{account}.purview.azure.com/dataproducts/...

4. This works because the institution admin
   granted consent to the consortium app in Step 2 above.`}
          </div>

          <Text as="h3" size={400} weight="semibold" block style={{ marginBottom: '8px', marginTop: '16px' }}>
            Why Multi-Tenant?
          </Text>
          <Text block>
            Each institution has its own Entra ID tenant and Purview account. By using a multi-tenant app
            registration, the consortium operates a single app that can authenticate against any institution's
            tenant — once they've granted admin consent. No per-institution app registrations or stored
            credentials are needed.
          </Text>
        </Card>
      </div>

      <Divider style={{ margin: '32px 0' }} />

      {/* Quick Reference */}
      <div className={styles.section}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <CheckmarkCircle24Regular />
          <Text as="h2" size={600} weight="semibold">
            Quick Reference: Checklist
          </Text>
        </div>

        <Card className={styles.card}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', padding: '10px', borderBottom: `2px solid ${tokens.colorNeutralStroke1}` }}>Step</th>
                <th style={{ textAlign: 'left', padding: '10px', borderBottom: `2px solid ${tokens.colorNeutralStroke1}` }}>Who</th>
                <th style={{ textAlign: 'left', padding: '10px', borderBottom: `2px solid ${tokens.colorNeutralStroke1}` }}>Where</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Register multi-tenant app</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Consortium Admin</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Azure Portal (consortium tenant)</td>
              </tr>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Configure API permissions</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Consortium Admin</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Azure Portal → App registration</td>
              </tr>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Set Client ID & Secret in config</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Consortium Admin</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>appsettings.json / .env</td>
              </tr>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Add institution in platform</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Consortium Admin</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Admin tab → Add Institution</td>
              </tr>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Grant admin consent</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Institution Admin</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Admin consent URL (Entra ID)</td>
              </tr>
              <tr>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Tag data products in Purview</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Institution Data Steward</td>
                <td style={{ padding: '10px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>Microsoft Purview portal</td>
              </tr>
              <tr>
                <td style={{ padding: '10px' }}>Run scan</td>
                <td style={{ padding: '10px' }}>Consortium Admin</td>
                <td style={{ padding: '10px' }}>Admin tab → Scan button</td>
              </tr>
            </tbody>
          </table>
        </Card>
      </div>
    </div>
  );
}
