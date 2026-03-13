/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AZURE_CLIENT_ID: string;
  readonly VITE_AZURE_TENANT_ID: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare const __BUILD_TIMESTAMP__: string;
declare const __DEPLOY_TIMESTAMP__: string;
