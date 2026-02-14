# Purview Consortium Data Product Sharing Platform

## Proof of Concept — Requirements & Prompt Instructions

---

## 1. Executive Summary

Build a proof-of-concept web application that serves as a **centralized, searchable catalog of shared Data Products** across multiple institutions that use Microsoft Purview. The platform enables institutions to publish Data Products to a consortium, allows consortium members to discover and request access, and orchestrates fulfillment through Microsoft Fabric OneLake external data sharing (shortcuts).

---

## 2. Key Concepts & Definitions

| Term | Definition |
|------|-----------|
| **Consortium** | A group of institutions that agree to share Data Products through this platform. |
| **Institution** | An organization that operates its own Microsoft Purview instance and Microsoft Fabric workspace. |
| **Data Product** | A curated, governed dataset registered in Microsoft Purview, with metadata, schema, ownership, and classification information. |
| **Shareable Data Product** | A Data Product that an institution has explicitly tagged/marked as available for sharing with the consortium. |
| **Data Consumer** | A user from a consortium member institution who searches for and requests access to a shared Data Product. |
| **Data Provider** | The institution that owns and publishes a Data Product to the consortium catalog. |
| **External Data Share** | A Fabric OneLake sharing mechanism that creates a shortcut from the provider's lakehouse to the consumer's Fabric OneLake. |

---

## 3. Functional Requirements

### 3.1 Institution Onboarding & Registration

| ID | Requirement | Priority |
|----|------------|----------|
| FR-01 | The system shall allow consortium administrators to register new member institutions. | Must |
| FR-02 | Each institution registration shall capture: institution name, Purview tenant ID, Purview account name, Fabric workspace ID, and primary contact. (No per-institution credentials needed — the consortium's multi-tenant app is used after the institution grants admin consent.) | Must |
| FR-03 | The system shall validate connectivity to each institution's Purview instance upon registration. | Must |
| FR-04 | The system shall support enabling/disabling an institution without deleting its configuration. | Should |

### 3.2 Data Product Discovery & Ingestion

| ID | Requirement | Priority |
|----|------------|----------|
| FR-10 | The system shall periodically scan each registered institution's Purview catalog for Data Products tagged as consortium-shareable. | Must |
| FR-11 | The tagging convention for shareable Data Products shall use the Purview glossary term `Consortium-Shareable`. Institutions apply this term to Data Products they wish to share with the consortium. | Must |
| FR-12 | The system shall extract and store the following metadata for each shareable Data Product: name, description, owner, schema/columns, classifications, sensitivity labels, glossary terms, source system, last updated date, and institution of origin. | Must |
| FR-13 | The scan shall run on a configurable schedule (default: every 6 hours) and support on-demand triggering. | Must |
| FR-14 | The system shall detect when a Data Product is no longer tagged as shareable and mark it as delisted in the catalog (soft delete). | Should |
| FR-15 | The system shall maintain a sync history/audit log of all scan operations and their results. | Should |

### 3.3 Searchable Consortium Catalog (Website)

| ID | Requirement | Priority |
|----|------------|----------|
| FR-20 | The website shall display all consortium-shared Data Products in a browsable, paginated catalog. | Must |
| FR-21 | The website shall provide full-text search across Data Product name, description, schema columns, glossary terms, and classifications. | Must |
| FR-22 | The website shall support filtering by: institution, classification, sensitivity label, glossary term, source system type, and date range. | Must |
| FR-23 | The website shall support sorting by: relevance, name, institution, last updated date. | Must |
| FR-24 | Each Data Product detail page shall display: full metadata, schema with column-level descriptions and classifications, owning institution, contact information, and an "Request Access" action. | Must |
| FR-25 | The website shall display the current access request status for Data Products the logged-in user has previously requested. | Should |
| FR-26 | The website shall provide a dashboard summarizing catalog statistics: total Data Products, products per institution, recent additions, and pending requests. | Should |

### 3.4 Access Request Workflow

| ID | Requirement | Priority |
|----|------------|----------|
| FR-30 | A logged-in consortium user shall be able to submit an access request for a Data Product. | Must |
| FR-31 | The access request shall capture: requesting user, requesting institution, target Fabric workspace/lakehouse, business justification, and requested access duration (or indefinite). | Must |
| FR-32 | Upon submission, the system shall notify the owning institution via email that a new access request is pending. | Must |
| FR-33 | The system shall provide an API endpoint that the source institution's internal workflow can call to update request status (approved, denied, fulfilled, revoked). | Must |
| FR-34 | The system shall track the full lifecycle of each request: Submitted → Under Review → Approved → Fulfilled → Active → Revoked/Expired. | Must |
| FR-35 | The system shall allow the requesting user to cancel a pending request. | Should |
| FR-36 | The system shall support configurable auto-expiration of access grants. | Could |

### 3.5 Data Share Fulfillment (Fabric OneLake Shortcuts)

| ID | Requirement | Priority |
|----|------------|----------|
| FR-40 | Upon approval, the system shall provide the owning institution with the information needed to create an external data share (target tenant, workspace, lakehouse details). | Must |
| FR-41 | The system shall provide a guided manual fulfillment flow showing institution admins all details needed to create the share via the Fabric portal. | Must |
| FR-41a | As a stretch goal, the system shall support an automated flow where, upon approval, a Fabric external data share / OneLake shortcut is created programmatically via API. | Could |
| FR-42 | The system shall update the access request status to "Fulfilled" once the shortcut/share is confirmed active. | Must |
| FR-43 | The system shall support revoking an active share, which should trigger removal of the OneLake shortcut. | Should |

### 3.6 Authentication & Authorization

| ID | Requirement | Priority |
|----|------------|----------|
| FR-50 | The website shall authenticate users via Microsoft Entra ID (Azure AD) with support for multi-tenant sign-in. | Must |
| FR-51 | The system shall authorize users based on roles: Consortium Admin, Institution Admin, Data Consumer. | Must |
| FR-52 | Consortium Admins can manage institutions, view all requests, and configure system settings. | Must |
| FR-53 | Institution Admins can view/manage requests for their institution's Data Products and trigger scans. | Must |
| FR-54 | Data Consumers can browse the catalog, search, and submit access requests. | Must |

---

## 4. Non-Functional Requirements

| ID | Requirement | Priority |
|----|------------|----------|
| NFR-01 | The website shall be responsive and usable on desktop and tablet browsers. | Must |
| NFR-02 | The system shall handle a catalog of up to 10,000 Data Products across 50 institutions for the PoC. | Should |
| NFR-03 | Search results shall return within 2 seconds for typical queries. | Should |
| NFR-04 | All API communications shall use HTTPS/TLS 1.2+. | Must |
| NFR-05 | Service principal credentials and secrets shall be stored in Azure Key Vault. | Must |
| NFR-06 | The system shall log all user actions and API calls for audit purposes. | Must |
| NFR-07 | The system shall be deployable to Azure (App Service, Azure SQL/Cosmos DB, Azure Functions). | Must |

---

## 5. Technical Architecture (PoC Target)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Consortium Web Portal                        │
│              (React/Vite SPA + Entra ID Auth)                   │
└─────────────────────┬───────────────────────────────────────────┘
                      │ REST API
┌─────────────────────▼───────────────────────────────────────────┐
│                  Backend API Service                            │
│           (ASP.NET Core Web API / Azure Functions)              │
│                                                                 │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────────────┐     │
│  │ Catalog  │  │ Access Req.  │  │ Purview Scanner       │     │
│  │ Service  │  │ Workflow Svc │  │ Service (per-tenant)  │     │
│  └────┬─────┘  └──────┬───────┘  └──────────┬────────────┘     │
│       │               │                      │                  │
└───────┼───────────────┼──────────────────────┼──────────────────┘
        │               │                      │
┌───────▼───────────────▼──────┐  ┌────────────▼──────────────────┐
│       Database               │  │   Microsoft Purview           │
│  (Azure SQL / Cosmos DB)     │  │   (per institution tenant)    │
│                              │  │   - Data Map API              │
│  • Institutions              │  │   - Catalog Search API        │
│  • DataProducts (cached)     │  │   - Data Products API         │
│  • AccessRequests            │  └───────────────────────────────┘
│  • AuditLog                  │
│  • SyncHistory               │  ┌───────────────────────────────┐
└──────────────────────────────┘  │   Microsoft Fabric            │
                                  │   - OneLake Shortcuts API     │
                                  │   - External Data Sharing     │
                                  └───────────────────────────────┘
```

### 5.1 Proposed Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React + TypeScript (Vite SPA) with Fluent UI v9 |
| Backend API | ASP.NET Core 8 Web API (C#) |
| Background Jobs | Azure Functions (Timer-triggered for Purview scans) |
| Database | Azure SQL Database (structured relational data) |
| Authentication | Microsoft Entra ID (MSAL.js + MSAL.NET), multi-tenant app registration |
| Secrets | Azure Key Vault |
| Search | Azure AI Search (primary search infrastructure for catalog) |
| Notifications | Azure Communication Services (email) |
| Hosting | Azure App Service (API), Azure Static Web Apps (frontend) |
| IaC | Bicep / Terraform |

---

## 6. Data Model (Core Entities)

### Institution
```
- InstitutionId (GUID, PK)
- Name
- TenantId (Azure AD tenant ID)
- PurviewAccountName
- FabricWorkspaceId
- PrimaryContactEmail
- IsActive (bool)
- AdminConsentGranted (bool — whether the institution has granted admin consent to the consortium app)
- CreatedDate
- ModifiedDate
```

> **Note**: No per-institution service principal credentials are stored. The consortium's single multi-tenant app registration is used; each institution grants admin consent during onboarding.

### DataProduct (Cached from Purview)
```
- DataProductId (GUID, PK)
- PurviewAssetId (qualified name from Purview)
- InstitutionId (FK)
- Name
- Description
- Owner
- SourceSystem
- SchemaJson (JSON blob of columns, types, classifications)
- Classifications (array)
- GlossaryTerms (array)
- SensitivityLabel
- LastSyncedFromPurview (datetime)
- IsListed (bool — false if delisted)
- PurviewLastModified (datetime)
- CreatedDate
- ModifiedDate
```

### AccessRequest
```
- RequestId (GUID, PK)
- DataProductId (FK)
- RequestingUserId
- RequestingUserEmail
- RequestingInstitutionId (FK)
- TargetFabricWorkspaceId
- TargetLakehouseName
- BusinessJustification (text)
- RequestedDuration (nullable, days or "indefinite")
- Status (enum: Submitted, UnderReview, Approved, Denied, Fulfilled, Active, Revoked, Expired, Cancelled)
- StatusChangedDate
- StatusChangedBy
- ExternalShareId (nullable — populated when shortcut is created)
- ExpirationDate (nullable)
- CreatedDate
- ModifiedDate
```

### SyncHistory
```
- SyncId (GUID, PK)
- InstitutionId (FK)
- StartTime
- EndTime
- Status (Success, PartialFailure, Failed)
- ProductsFound (int)
- ProductsAdded (int)
- ProductsUpdated (int)
- ProductsDelisted (int)
- ErrorDetails (text, nullable)
```

### AuditLog
```
- AuditId (GUID, PK)
- Timestamp
- UserId
- Action (enum: Search, ViewProduct, RequestAccess, ApproveRequest, DenyRequest, FulfillRequest, RevokeAccess, etc.)
- EntityType
- EntityId
- Details (JSON)
- IpAddress
```

---

## 7. API Endpoints (Draft)

### Catalog
| Method | Endpoint | Description |
|--------|---------|-------------|
| GET | `/api/catalog/products` | Search/list Data Products (query, filters, paging) |
| GET | `/api/catalog/products/{id}` | Get Data Product detail |
| GET | `/api/catalog/stats` | Dashboard statistics |
| GET | `/api/catalog/filters` | Available filter values (institutions, classifications, etc.) |

### Access Requests
| Method | Endpoint | Description |
|--------|---------|-------------|
| POST | `/api/requests` | Submit a new access request |
| GET | `/api/requests` | List requests (filtered by user/institution/status) |
| GET | `/api/requests/{id}` | Get request detail |
| PATCH | `/api/requests/{id}/status` | Update request status (webhook for institution workflows) |
| DELETE | `/api/requests/{id}` | Cancel a pending request |

### Institutions (Admin)
| Method | Endpoint | Description |
|--------|---------|-------------|
| GET | `/api/admin/institutions` | List all institutions |
| POST | `/api/admin/institutions` | Register a new institution |
| PUT | `/api/admin/institutions/{id}` | Update institution config |
| DELETE | `/api/admin/institutions/{id}` | Deactivate institution |
| POST | `/api/admin/institutions/{id}/scan` | Trigger on-demand Purview scan |

### Sync
| Method | Endpoint | Description |
|--------|---------|-------------|
| GET | `/api/admin/sync/history` | View sync history across institutions |

---

## 8. Purview Integration Details

### 8.1 Scanning for Shareable Data Products

The scanner service will use the **Microsoft Purview Data Map** and **Catalog APIs** to discover Data Products:

1. **Authentication**: Use the consortium's single multi-tenant app registration (service principal) to authenticate against each institution's Purview instance via Entra ID. Each institution grants admin consent to this app during onboarding.
2. **Discovery**: Call the Purview Data Products API to retrieve Data Products that have the `Consortium-Shareable` glossary term applied.
   - Purview REST API: `GET https://{accountName}.purview.azure.com/dataproducts/api/data-products`
   - Filter results client-side or server-side for the `Consortium-Shareable` glossary term.
3. **Metadata Extraction**: For each qualifying Data Product, extract full metadata including schema, classifications, glossary terms, and ownership.
4. **Upsert**: Compare with cached records in the consortium database and insert/update/delist as needed.

### 8.2 Tagging Convention

Institutions mark Data Products for consortium sharing by applying the **Purview glossary term `Consortium-Shareable`** to the Data Product entity. This glossary term must exist in each institution's Purview glossary (can be created during onboarding).

The scanner will search specifically for Purview Data Product entities that have this glossary term applied.

---

## 9. Fabric OneLake Integration Details

### 9.1 Shortcut Creation Flow

**Phase 1 — Guided Manual (PoC default):**

1. Data Provider institution approves the access request.
2. The portal displays a **"Fulfillment Details" panel** containing all information the institution admin needs to create the share manually:
   - Source workspace ID, item ID, item name
   - Recipient tenant ID, recipient user/principal
   - Target workspace ID, target lakehouse name
   - Step-by-step instructions with links to the Fabric portal
3. The institution admin creates the external data share and OneLake shortcut manually via the Fabric portal.
4. The institution admin returns to the consortium portal and marks the request as "Fulfilled", optionally entering the external share ID for tracking.

**Phase 2 — Automated (stretch goal):**

1. Data Provider institution approves the access request.
2. The system calls the **Fabric REST API** to create an external data share:
   - `POST https://api.fabric.microsoft.com/v1/workspaces/{workspaceId}/items/{itemId}/externalDataShares`
3. The recipient institution accepts the share, creating a **OneLake shortcut** in their designated lakehouse:
   - `POST https://api.fabric.microsoft.com/v1/workspaces/{workspaceId}/items/{lakehouseId}/shortcuts`
4. The system updates the access request status to "Fulfilled" and records the share/shortcut identifiers automatically.

### 9.2 Share Revocation

1. When access expires or is revoked, the system calls the Fabric API to remove the external data share.
2. The shortcut in the consumer's lakehouse becomes invalid.
3. The access request status is updated to "Revoked" or "Expired".

---

## 10. User Experience Flows

### 10.1 Data Consumer Journey
```
1. User logs in via Entra ID (multi-tenant)
2. Lands on consortium catalog dashboard
3. Searches for "patient demographics" or filters by classification "PHI"
4. Browses results, clicks into a Data Product from Institution B
5. Reviews schema, classifications, owner info
6. Clicks "Request Access"
7. Fills in: target workspace, justification, desired duration
8. Submits request → status shows "Submitted"
9. Receives email when status changes to Approved/Denied
10. If approved & fulfilled, data appears as shortcut in their Fabric lakehouse
```

### 10.2 Institution Admin Journey
```
1. Receives notification of new access request
2. Reviews request details in consortium portal (or their internal system via webhook)
3. Runs internal governance/approval workflow
4. Updates request status via API call (or portal UI)
5. If approved, triggers OneLake shortcut creation
6. Monitors active shares, can revoke if needed
```

---

## 11. Security Considerations

- **Least Privilege**: Service principals should have read-only access to Purview catalogs; write access only to Fabric for share creation.
- **Credential Isolation**: The consortium's multi-tenant app registration credentials stored in Key Vault. Each institution grants admin consent to the consortium app rather than sharing their own credentials.
- **Data Residency**: The consortium portal stores only **metadata**, never the actual data. Data remains in the provider's Fabric lakehouse and is accessed via shortcut.
- **Consent**: Multi-tenant app registration requires admin consent from each institution's Azure AD tenant.
- **Network**: Consider Private Endpoints for Purview and Fabric API access in production.
- **Audit Trail**: All access requests, approvals, and data access events logged for compliance.

---

## 12. PoC Scope & Phasing

### Phase 1 — Foundation (Weeks 1–3)
- [ ] Project scaffolding (frontend + backend)
- [ ] Entra ID multi-tenant authentication
- [ ] Institution registration (admin UI + API)
- [ ] Purview scanner service (single institution)
- [ ] Database schema and migrations
- [ ] Basic catalog UI (list + search)

### Phase 2 — Core Workflow (Weeks 4–6)
- [ ] Multi-institution scanning
- [ ] Data Product detail page with schema viewer
- [ ] Access request submission and tracking
- [ ] Notification system (email on request status change)
- [ ] Institution admin request management UI
- [ ] Status update webhook/API for external workflows

### Phase 3 — Fulfillment & Polish (Weeks 7–8)
- [ ] Guided manual OneLake shortcut fulfillment flow (with copy-paste details)
- [ ] Automated OneLake shortcut creation via Fabric API (stretch goal)
- [ ] Share revocation flow
- [ ] Dashboard with catalog statistics
- [ ] Audit log viewer (admin)
- [ ] End-to-end testing with 2–3 institutions
- [ ] Documentation and deployment guide

---

## 13. Prompt Instructions for AI-Assisted Development

The following prompt instructions should be used when leveraging AI coding assistants (e.g., GitHub Copilot) to build this application. Each prompt targets a specific component or feature area.

---

### Prompt 1: Project Scaffolding

```
Create a monorepo project structure for a Purview Consortium Data Product Sharing Platform with:

1. A React + TypeScript frontend using Vite (SPA, no SSR) and Fluent UI v9 components, deployable to Azure Static Web Apps
2. An ASP.NET Core 8 Web API backend in C#
3. An Azure Functions project (isolated worker, .NET 8) for background jobs

The frontend should have:
- MSAL.js authentication integration (multi-tenant Entra ID)
- React Router v6 for client-side navigation
- A layout with sidebar navigation: Dashboard, Catalog, My Requests, Admin
- Axios-based API client with auth token injection

The backend API should have:
- Controllers for: Catalog, AccessRequests, Institutions, Sync
- Entity Framework Core with Azure SQL provider
- MSAL.NET for downstream API auth (Purview, Fabric)
- Swagger/OpenAPI documentation
- Structured logging with Serilog

The Azure Functions project should have:
- A timer-triggered function for Purview scanning (every 6 hours)
- An HTTP-triggered function for on-demand scanning
- Shared library references for data access and Purview client

Include a solution file, docker-compose for local development, and a README.
```

---

### Prompt 2: Database Schema & Entity Framework

```
Create Entity Framework Core 8 models and DbContext for the Purview Consortium platform with these entities:

1. Institution: Id (Guid), Name, TenantId, PurviewAccountName, FabricWorkspaceId, PrimaryContactEmail, IsActive, AdminConsentGranted (bool), CreatedDate, ModifiedDate

2. DataProduct: Id (Guid), PurviewQualifiedName, InstitutionId (FK), Name, Description, Owner, OwnerEmail, SourceSystem, SchemaJson (JSON column), Classifications (JSON array), GlossaryTerms (JSON array), SensitivityLabel, IsListed, LastSyncedFromPurview, PurviewLastModified, CreatedDate, ModifiedDate

3. AccessRequest: Id (Guid), DataProductId (FK), RequestingUserId, RequestingUserEmail, RequestingUserName, RequestingInstitutionId (FK), TargetFabricWorkspaceId, TargetLakehouseName, BusinessJustification, RequestedDurationDays (nullable int), Status (enum), StatusChangedDate, StatusChangedBy, ExternalShareId (nullable), ExpirationDate (nullable), CreatedDate, ModifiedDate

4. SyncHistory: Id (Guid), InstitutionId (FK), StartTime, EndTime, Status (enum), ProductsFound, ProductsAdded, ProductsUpdated, ProductsDelisted, ErrorDetails

5. AuditLog: Id (Guid), Timestamp, UserId, UserEmail, Action (enum), EntityType, EntityId, Details (JSON), IpAddress

Include:
- Proper indexes (full-text on DataProduct.Name/Description, composite on AccessRequest status + institution)
- Enum-to-string conversions
- JSON column configuration for SQL Server
- Seed data for testing
- Migration commands
- A repository pattern with interfaces
```

---

### Prompt 3: Purview Scanner Service

```
Create a C# service class PurviewScannerService that:

1. Accepts an institution's Purview account name and service principal credentials
2. Authenticates using Azure.Identity (ClientSecretCredential) against the institution's tenant
3. Queries the Purview Data Products API to find Data Products that have the glossary term "Consortium-Shareable" applied
4. Filters results for the glossary term (client-side if the API doesn't support server-side filtering)
5. For each found Data Product, extracts: name, description, owner, schema (columns with types and classifications), glossary terms, classifications, sensitivity labels, source system info
6. Returns a list of DataProductSyncResult objects

Also create a SyncOrchestrator service that:
1. Iterates over all active institutions
2. Calls PurviewScannerService for each
3. Compares results with existing cached DataProducts in the database
4. Inserts new, updates changed, and delists removed products
5. Records a SyncHistory entry for each institution scan
6. Handles errors per-institution (one failure doesn't stop others)

Use the Azure.Analytics.Purview.DataMap SDK where available, falling back to raw REST calls where needed. Include retry policies with Polly.
```

---

### Prompt 4: Catalog Search API

```
Create an ASP.NET Core controller CatalogController with these endpoints:

GET /api/catalog/products
- Accepts query parameters: search (text), institutions (comma-separated GUIDs), classifications (comma-separated), sensitivityLabels, glossaryTerms, sourceSystem, updatedAfter, updatedBefore, sortBy (relevance|name|institution|updated), sortDirection, page, pageSize
- Performs full-text search across DataProduct Name, Description, SchemaJson, and GlossaryTerms
- Returns paginated results with total count, facet counts for filters
- Only returns listed (IsListed = true) products

GET /api/catalog/products/{id}
- Returns full DataProduct detail including parsed schema, institution info, and the requesting user's existing access request status (if any)

GET /api/catalog/stats
- Returns: total products, products by institution, products by classification, recent additions (last 30 days), pending request count for current user

GET /api/catalog/filters
- Returns available filter values: list of institutions (id + name), distinct classifications, glossary terms, sensitivity labels, source systems

Use AutoMapper for DTOs. Include proper error handling, input validation, and authorize attributes.
```

---

### Prompt 5: Access Request Workflow API

```
Create an ASP.NET Core controller AccessRequestsController with these endpoints:

POST /api/requests
- Creates a new access request. Body: { dataProductId, targetFabricWorkspaceId, targetLakehouseName, businessJustification, requestedDurationDays }
- Auto-populates requesting user info from the auth token claims
- Validates the Data Product exists and is listed
- Prevents duplicate active requests for same user + product
- Sends notification to the owning institution's contact email
- Returns 201 with the created request

GET /api/requests
- Lists requests filtered by: status, dataProductId, requestingInstitutionId, owningInstitutionId
- For Data Consumers: returns only their own requests
- For Institution Admins: returns requests for their institution's products
- For Consortium Admins: returns all requests

GET /api/requests/{id}
- Returns full request detail with Data Product and institution info

PATCH /api/requests/{id}/status
- Updates request status. Body: { newStatus, comment }
- Enforces valid state transitions (e.g., Submitted → UnderReview → Approved/Denied)
- Only Institution Admins of the owning institution (or Consortium Admins) can approve/deny
- Sends notification to requesting user on status change
- If status is "Fulfilled", expects externalShareId in the body

DELETE /api/requests/{id}
- Cancels a pending request (only by the requesting user, only if status is Submitted or UnderReview)

Include an INotificationService interface for sending emails and an IAccessRequestWorkflowService for business logic validation.
```

---

### Prompt 6: Frontend Catalog UI

```
Create React components for the Consortium Data Product Catalog using Fluent UI v9 and TypeScript:

1. CatalogPage: Main catalog view with:
   - Search bar (Fluent SearchBox) with debounced search
   - Filter panel (sidebar or drawer) with multi-select filters for Institution, Classification, Sensitivity Label, Glossary Term, Source System
   - Results grid showing DataProduct cards with: name, institution badge, description snippet, classification tags, last updated
   - Pagination controls
   - "No results" empty state
   - Loading skeleton states

2. DataProductDetailPage: Detail view with:
   - Breadcrumb navigation
   - Header with product name, institution, owner, last updated
   - Description section
   - Schema table showing columns, data types, and column-level classifications
   - Tags section showing classifications, glossary terms, sensitivity label
   - "Request Access" button (disabled if user already has active request, with status shown)
   - Request access dialog/panel with form fields: target workspace, lakehouse name, business justification, duration

3. RequestAccessDialog: Modal dialog with:
   - Form validation (all fields required except duration)
   - Submit with loading state
   - Success/error feedback
   - Redirect to My Requests page on success

Use React Query (TanStack Query) for data fetching and caching. Use React Router v6 for navigation.
```

---

### Prompt 7: Dashboard & Statistics

```
Create a React Dashboard page for the Consortium portal using Fluent UI v9:

1. Summary cards row showing:
   - Total Data Products in catalog
   - Number of member institutions
   - User's pending access requests
   - User's active data shares

2. "Recently Added" section: list of the 10 most recently added Data Products with links

3. "My Recent Requests" section: table of user's last 5 access requests with status badges (color-coded by status)

4. For Consortium/Institution Admins, additional sections:
   - "Requests Awaiting Action" count and link
   - "Products by Institution" bar chart (use a lightweight chart library)
   - "Last Sync Status" per institution with timestamp and success/failure indicator

Fetch all data from the backend API. Use React Query for caching. Show loading skeletons while data loads.
```

---

### Prompt 8: Fabric OneLake Integration Service

```
Create a C# service class FabricDataShareService that handles OneLake external data sharing:

1. CreateExternalDataShare method:
   - Accepts: source workspace ID, source item ID (lakehouse/warehouse), recipient tenant ID, recipient principal
   - Authenticates using the source institution's service principal via Azure.Identity
   - Calls Fabric REST API: POST /v1/workspaces/{workspaceId}/items/{itemId}/externalDataShares
   - Returns the created share ID and invitation link

2. CreateShortcut method:
   - Accepts: target workspace ID, target lakehouse ID, shortcut name, source path, external share connection info
   - Authenticates using the target institution's service principal
   - Calls Fabric REST API: POST /v1/workspaces/{workspaceId}/items/{lakehouseId}/shortcuts
   - Returns the shortcut details

3. RevokeExternalDataShare method:
   - Accepts: workspace ID, item ID, share ID
   - Calls Fabric REST API to delete the external data share
   - Returns success/failure

4. GetShareStatus method:
   - Checks the current status of an external data share

Include proper error handling, retry logic with Polly, and logging. Use HttpClient with IHttpClientFactory.
```

---

### Prompt 9: Authentication & Authorization Setup

```
Configure Microsoft Entra ID (Azure AD) multi-tenant authentication for the Purview Consortium platform:

Backend (ASP.NET Core):
- Configure JWT Bearer authentication with multi-tenant validation
- Create a custom ClaimsTransformer that maps user tenant ID to institution and determines roles (ConsortiumAdmin, InstitutionAdmin, DataConsumer)
- Create authorization policies: "RequireConsortiumAdmin", "RequireInstitutionAdmin", "RequireAuthenticated"
- Create a middleware that extracts and logs tenant context for each request
- Store role assignments in the database (UserId, InstitutionId, Role)

Frontend (React):
- Configure MSAL.js with PublicClientApplication for multi-tenant sign-in
- Create an AuthProvider context component that provides: user, isAuthenticated, login, logout, getAccessToken
- Create a ProtectedRoute component that redirects to login if not authenticated
- Create an API interceptor that attaches the Bearer token to all API calls
- Handle token refresh automatically

Provide the Azure AD app registration configuration needed:
- Multi-tenant app registration settings
- Required API permissions (Purview, Fabric, Microsoft Graph)
- Redirect URIs for local dev and production
```

---

### Prompt 10: Infrastructure as Code

```
Create Bicep templates to deploy the Purview Consortium platform to Azure:

1. Main template (main.bicep) orchestrating all resources
2. Parameters file for dev and production environments

Resources to deploy:
- Azure App Service (Linux, .NET 8) for the backend API
- Azure Static Web App for the React frontend
- Azure SQL Database (serverless tier for PoC)
- Azure Key Vault for storing institution credentials
- Azure Functions App (Consumption plan) for background scanning
- Azure Communication Services (email) for notifications
- Application Insights for monitoring
- Azure AI Search (basic tier) for catalog search
- Storage Account for Functions and any file storage

Include:
- Managed Identity for App Service → Key Vault access
- App Settings configuration referencing Key Vault secrets
- SQL Database firewall rules
- CORS configuration on App Service
- Deployment slots (staging) for the API

Output the deployed resource URLs and connection strings (as Key Vault references).
```

---

## 14. Design Decisions (Confirmed)

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Backend framework | **ASP.NET Core 8 (C#)** | Strong Purview/Fabric SDK support, enterprise standard |
| 2 | Frontend framework | **React + Vite (SPA)** | Lightweight PoC, easy Azure Static Web Apps deployment, no SSR needed |
| 3 | Service principal strategy | **Single multi-tenant app registration** | Consortium registers one app; each institution grants admin consent. Simpler onboarding. |
| 4 | OneLake shortcut creation | **Guided manual first, automate later** | Manual fulfillment with detailed instructions for PoC; automated Fabric API integration as stretch goal. |
| 5 | Search infrastructure | **Azure AI Search** | Richer relevance ranking, faceted navigation, and semantic search capabilities. |
| 6 | Notification system | **Email only** | Azure Communication Services for email; no Teams/Slack integration for PoC. |
| 7 | Tagging mechanism | **Purview glossary term** | Institutions apply the `Consortium-Shareable` glossary term to Data Products. Easy to apply, broadly supported. |
| 8 | Data Product scope | **Purview Data Products only** | Scanner targets the Purview Data Products API specifically, not generic catalog assets. |
| 9 | Database model | **Single database, no RLS** | All consortium metadata in one Azure SQL database. Simple and low-cost for PoC. |
| 10 | Purview API strategy | **Data Products API (accept preview risk)** | Build against the Data Products REST API directly. If still in preview at build time, add a disclaimer. |

## 15. Open Questions

_All questions have been resolved. See Design Decisions above._

---

## 16. Success Criteria for PoC

- [ ] At least 2 institutions registered with Purview connectivity verified
- [ ] Automated scanning discovers and caches shareable Data Products from both institutions
- [ ] Users can search and filter the catalog by multiple criteria
- [ ] Users can submit access requests with full tracking
- [ ] Institution admins can approve/deny requests
- [ ] At least 1 end-to-end flow completed: discovery → request → approval → OneLake shortcut created
- [ ] Authentication works across tenants (users from both institutions can log in)
- [ ] All actions are audit-logged

---

*Document Version: 1.3*
*Created: February 13, 2026*
*Updated: February 13, 2026 — All design decisions finalized, ready to build*
*Status: Approved — Ready for Implementation*
