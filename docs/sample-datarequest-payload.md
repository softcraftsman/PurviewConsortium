
### Request URL
PUT https://df29ef2b-b644-4d26-89d4-83bd021f1050-api.purview-service.microsoft.com/datagovernance/dataaccess/dataSubscriptions/0be9a8ab-c307-42b9-80c8-f190528cadcc?api-version=2023-10-01-preview

### Sample Payload for a Data Request initiated form the Purview

{
    "dataProductId":"06d56da7-828e-4cc4-9245-ecce407df472",
    "subscriberIdentity":
    {
        "identityType":"User",
        "objectId":"3d890138-cbb2-4d97-a644-96ecffd55a98"
    },
   "policySetValues":
    {
        "businessJustification":"BJSU",
        "useCase":"Research"
    }
}


### Response from 201 Created
{
    "dataSubscriptionId": "0be9a8ab-c307-42b9-80c8-f190528cadcc",
    "useWorkflow": true,
    "workflowId": "a31aab6e-78c7-489e-a171-78fb3e552a5b",
    "writeAccess": false,
    "subscriptionStatus": "Pending",
    "subscriberIdentity": {
        "identityType": "User",
        "objectId": "3d890138-cbb2-4d97-a644-96ecffd55a98",
        "tenantId": "df29ef2b-b644-4d26-89d4-83bd021f1050",
        "displayName": "Student User"
    },
    "requestorIdentity": {
        "identityType": "User",
        "objectId": "4bafb39b-fbd3-42c4-8e20-1c8e485f23c5",
        "tenantId": "df29ef2b-b644-4d26-89d4-83bd021f1050",
        "displayName": "System Administrator",
        "email": "admin@fauxuni.org"
    },
    "domainId": "268252c1-39a2-43fe-b22a-f59fcdb4b8d4",
    "dataProductId": "06d56da7-828e-4cc4-9245-ecce407df472",
    "dataProductName": "Synthea Data",
    "businessDomainName": "Consortium",
    "policySetValues": {
        "accessDuration": {
            "durationType": "Years",
            "length": 3
        },
        "useCase": "Research",
        "businessJustification": "BJSU",
        "termsOfUseAccepted": false,
        "partnerSharingRequested": false,
        "customerSharingRequested": false,
        "approverDecisions": [
            {
                "approver": {
                    "identityType": "User",
                    "objectId": "4bafb39b-fbd3-42c4-8e20-1c8e485f23c5",
                    "tenantId": "df29ef2b-b644-4d26-89d4-83bd021f1050",
                    "displayName": "System Administrator",
                    "email": "admin@fauxuni.org"
                },
                "decision": "NoResponse",
                "approverDecisionType": 1
            }
        ]
    },
    "createdAt": "2026-03-14T15:41:28.4341597Z",
    "createdBy": "admin@fauxuni.org",
    "dataAssets": [
        {
            "dataAssetName": "ShareableEMR",
            "dataAssetId": "211a190f-edc1-43d9-9c98-5d03faafcca4",
            "dataAssetStatus": 0,
            "comment": "",
            "updatedBySelfService": false
        },
        {
            "dataAssetName": "ShareableFinancialData",
            "dataAssetId": "9d030546-b68f-4706-8c4c-7cadcb9fb77c",
            "dataAssetStatus": 0,
            "comment": "",
            "updatedBySelfService": false
        }
    ],
    "accessProviders": [
        "4bafb39b-fbd3-42c4-8e20-1c8e485f23c5"
    ],
    "usePowerAutomate": false,
    "provisioningState": "Succeeded"
}