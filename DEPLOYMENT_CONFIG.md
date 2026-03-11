# Deployment Configuration Guide

## Problem: Database Connection Not Working in Production

The website now loads and renders, but the API cannot connect to the database because configuration is incomplete. Follow these steps to fix it.

---

## 1. Azure App Service Configuration

You need to set environment variables on the **Azure App Service** where the API is deployed:

### Required Environment Variables:

| Variable Name | Description | Example |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | `Server=tcp:sql-server.database.windows.net,1433;Initial Catalog=YourDB;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Default;` |
| `AzureAd__ClientSecret` | App registration client secret | (from Entra ID) |
| `AzureAISearch__ApiKey` | Azure AI Search admin key | (from Search service) |
| `AzureAISearch__Endpoint` | Azure AI Search endpoint | `https://your-search.search.windows.net` |
| `CORS_ALLOWED_ORIGINS` | Frontend domain(s) | `https://your-swa-domain.azurestaticapps.net,http://localhost:5173` |

### How to Set Them:

1. Go to **Azure Portal → App Services → (Your API app) → Configuration**
2. Click **New application setting** for each variable
3. Enter the name and value
4. Click **Save**

---

## 2. GitHub Repository Secrets & Variables

These are used by the CI/CD pipeline to build and authenticate:

### Repository Variables (Settings → Secrets and variables → Variables):

| Variable | Description | Example |
|---|---|---|
| `VITE_AZURE_CLIENT_ID` | Entra ID app client ID | `6561c659-2dfd-44a5-9931-cd059de86857` |
| `VITE_AZURE_TENANT_ID` | Entra ID tenant ID | `common` (for multi-tenant) |
| `VITE_API_BASE_URL` | API base URL for frontend | `https://your-api-domain.azurewebsites.net` |
| `AZURE_APP_SERVICE_NAME` | Name of API App Service | `purview-consortium-api-prod` |

### Repository Secrets (Settings → Secrets and variables → Secrets):

| Secret | Description | Source |
|---|---|---|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | App Service publish profile | Download from App Service → Overview → Get publish profile |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Static Web App deployment token | Azure Portal → Static Web App → Manage deployment token |

---

## 3. Get Information From Azure Portal

### API Connection String:
1. Go to **Azure Portal → SQL Databases → (Your DB)**
2. Copy the connection string from **Connection strings** tab
3. Add `;Authentication=Active Directory Default;` for managed identity

### Search Service Details:
1. Go to **Azure Portal → Azure AI Search → (Your service)**
2. Copy **Endpoint** from Overview
3. Copy **Keys** → use Primary admin key for `ApiKey`

### Static Web App URL:
1. Go to **Azure Portal → Static Web Apps → (Your app)**
2. Copy the URL from **Overview** tab (e.g., `https://xxx.azurestaticapps.net`)
3. Use this for `VITE_API_BASE_URL` in GitHub vars and `CORS_ALLOWED_ORIGINS` in App Service

---

## 4. Run Deployment

After setting all configuration:

1. Push a commit to trigger deployment:
   ```bash
   git add . && git commit -m "Update deployment" && git push
   ```

2. Or manually trigger:
   - Go to **GitHub → Actions → Deploy workflow → Run workflow**

3. Wait for the workflow to complete and verify:
   - Check API health: `https://your-api.azurewebsites.net/healthz`
   - Check frontend: `https://your-swa.azurestaticapps.net`
   - Try accessing `/catalog` or another page that queries data

---

## 5. Troubleshooting

### Frontend loads but data doesn't:
- Check browser console for CORS errors
- Verify `CORS_ALLOWED_ORIGINS` includes the frontend domain
- Verify `VITE_API_BASE_URL` matches actual API domain

### API returns 500 errors:
- Check **Azure App Service → Log stream** for error messages
- Verify connection string is correct
- Verify database migrations have run (check `/healthz` endpoint)

### Database connection fails:
- Verify connection string in App Service config
- Check SQL Server firewall rules allow App Service IP
- For managed identity: ensure app has role assignment on SQL Server

---

## Current Status

✅ Frontend deployed and renders  
✅ API deployed successfully  
❌ Database not connected (waiting for App Service config)  
❌ CORS not configured (blocking API calls)  

**Next Step:** Set the environment variables in Azure App Service Configuration, then re-deploy or restart the API app.
