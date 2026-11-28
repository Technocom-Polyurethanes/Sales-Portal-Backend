# Sales Portal API

All endpoints are JSON over HTTPS and, except for `POST /api/auth/login`, require a
bearer token. Enum values are serialized as strings (for example `"Draft"`), dates as
`YYYY-MM-DD`, and timestamps as ISO-8601 UTC values.

Base URL in local development: `https://localhost:8443`.

## Conventions

- Validation failures return RFC 7807 `application/problem+json`. The legacy top-level
  `message` field is still present for existing clients.
- Offer validation failures keep `message`, `errors` (array of strings) and `warnings`,
  and also carry the ProblemDetails fields. `detail` always holds every error in one
  readable string, so clients can show `detail` as-is.
- `ConflictException` (409), validation (400), not found (404) and model-binding errors
  (for example an invalid GUID, 400) all return `application/problem+json` with a
  readable `detail`. Field-keyed errors (settings, model binding) use an `errors`
  dictionary; offer validation uses the `errors` array.
- Lists are 1-based and use `page` / `pageSize`.
- Fields are additive unless noted otherwise. The offer reference format and the
  required `offerType` are documented under Offer reference rules.

## Roles and permissions

Every user has exactly one role, returned as `role` by `POST /api/auth/login` and
`GET /api/auth/me`. The role is read from the database on every request (not from the
token), so a role change or a deactivation applies immediately; a token of a deactivated
or deleted user returns `401`. A role the user lacks returns `403` ProblemDetails.

| Capability | Admin | Sales | Accountant |
| --- | :---: | :---: | :---: |
| View offers, customers (with history), materials, bank accounts | yes | yes | yes |
| Create, edit, revise and change the status of offers | yes | yes | read only |
| Create a customer, material or bank account | yes | yes | yes |
| Edit an existing customer, material or bank account | yes | no | yes |
| Delete a customer, material or bank account | yes | yes | yes |
| Delete an offer | yes | yes | no |
| Data import (templates, preview, commit, history) | yes | no | yes |
| Company settings (`PUT /api/settings`) | yes | no | no |
| Users (`/api/users`) | yes | no | no |
| System status (`/api/system/status`) | yes | no | no |
| Audit log (`/api/audit-logs`) | yes | no | no |

The matrix lives in one file, `sales_portal.api/Authorization/Policies.cs`.

## Offers

### `GET /api/offers`

Returns offer summaries. By default only the highest version of each reference is
returned (`latestOnly=true`).

| Query parameter | Default | Notes |
| --- | --- | --- |
| `search` | – | Matches reference, customer name (English/Arabic) and customer code. |
| `status` | – | `Draft`, `Issued`, `Accepted`, `Rejected`, `Cancelled`; case-insensitive. An unknown value returns `400` ProblemDetails. |
| `latestOnly` | `true` | `false` returns every stored version. |
| `sortBy` | `id` | `id` (the printed offer number: issue date, serial, version), `updatedAt`, `createdAt`, `reference`, `total`, `issueDate`. |
| `sortDir` | `asc` for `id`, `desc` otherwise | `asc` or `desc`. |
| `page` | `1` | |
| `pageSize` | `20` | |

```json
{
  "items": [
    {
      "id": "8f4d0f37-2a9b-4a2b-9a6b-2d9b2b5a4f10",
      "reference": "Loc/101/YL/JD",
      "offerType": "Loc",
      "serialNumber": 101,
      "version": 2,
      "customerId": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
      "customerName": "Duravit Egypt",
      "customerNameAr": "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
      "issueDate": "2026-10-21",
      "dueDate": "2026-11-20",
      "currency": "EGP",
      "status": "Draft",
      "total": 125000.00,
      "createdByName": "Test User",
      "createdAt": "2026-10-21T08:15:00+00:00",
      "updatedAt": "2026-10-22T11:40:00+00:00",
      "versionsCount": 2,
      "lastModifiedByName": "Modifier User",
      "lastModifiedAt": "2026-10-22T11:40:00+00:00",
      "parentOfferId": "1c4e45b0-f3b8-49a3-a1f4-2a5f9d4d7a10"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

New response fields: `versionsCount` (all versions of the same offer chain), `rootOfferId`,
`lastModifiedByName` and `lastModifiedAt` (the latest modifier, falling back to the
creator). `latestOnly`, `sortBy` and `sortDir` are new query parameters with defaults
that preserve the previous ordering behaviour.

### `GET /api/offers/{id}`

Returns the full offer, including creator/modifier and document data.

```json
{
  "id": "8f4d0f37-2a9b-4a2b-9a6b-2d9b2b5a4f10",
  "reference": "Loc/101/YL/JD",
  "offerType": "Loc",
  "serialNumber": 101,
  "version": 2,
  "parentOfferId": "1c4e45b0-f3b8-49a3-a1f4-2a5f9d4d7a10",
  "customerId": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
  "customer": {
    "id": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
    "customerCode": "CUST-001",
    "name": "Duravit Egypt",
    "nameAr": "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
    "contactPerson": "Nader William",
    "contactPersonAr": "نادر وليام",
    "city": "Badr City",
    "cityAr": "مدينة بدر",
    "taxId": "200-145-890",
    "defaultCurrency": "EGP",
    "email": "nader@example.com",
    "phone": "+20 100 000 0000",
    "defaultCurrency": "EGP"
  },
  "createdByUserId": "8a0f6d47-cf9d-4d22-b6a8-bf4c4b9b4a61",
  "createdByName": "Test User",
  "updatedByUserId": "f4af6fef-a6c8-43ca-bcb0-1d68f95a3b72",
  "updatedByName": "Modifier User",
  "versionsCount": 2,
  "attention": "Nader William",
  "attentionAr": "نادر وليام",
  "taxId": "200-145-890",
  "validityDays": 7,
  "issueDate": "2026-10-21",
  "dueDate": "2026-11-20",
  "paymentTermId": "0e6a9e7a-5f8e-4f1e-9c6d-8f5f0c2f7b31",
  "paymentTermLabel": "30 days",
  "currency": "EGP",
  "documentLanguage": "Ar",
  "status": "Draft",
  "subtotal": 125000.00,
  "orderDiscount": 0.00,
  "orderDiscountPercent": 0.00,
  "taxPercent": 14.00,
  "taxAmount": 17500.00,
  "otherTaxEnabled": false,
  "otherTaxName": "",
  "otherTaxPercent": 0.00,
  "otherTaxAmount": 0.00,
  "total": 142500.00,
  "createdAt": "2026-10-21T08:15:00+00:00",
  "updatedAt": "2026-10-22T11:40:00+00:00",
  "lines": [],
  "deliveryBatches": [],
  "document": null
}
```

New response fields: `updatedByUserId`, `updatedByName`, `versionsCount` and
`attentionAr`.

### `GET /api/offers/{id}/versions`

Returns every version of the offer's reference, newest first. Each item has the same
shape as the list response (including `versionsCount` and `lastModifiedByName`).

```json
[
  {
    "id": "8f4d0f37-2a9b-4a2b-9a6b-2d9b2b5a4f10",
    "reference": "Loc/101/YL/JD",
    "offerType": "Loc",
    "version": 2,
    "versionsCount": 2,
    "lastModifiedByName": "Modifier User",
    "lastModifiedAt": "2026-10-22T11:40:00+00:00"
  },
  {
    "id": "1c4e45b0-f3b8-49a3-a1f4-2a5f9d4d7a10",
    "reference": "Loc/101/YL/JD",
    "offerType": "Loc",
    "version": 1,
    "versionsCount": 2,
    "lastModifiedByName": "Test User",
    "lastModifiedAt": "2026-10-21T08:15:00+00:00"
  }
]
```

The example omits unchanged list fields for brevity; the real items contain all of
them.

### `GET /api/offers/next-sequence?offerType=Loc`

Suggests the next sequence and reference for the selected offer type. `offerType` is
required and accepts `Loc` (local) or `Int` (international).

```json
{
  "suggestedSequence": 43,
  "suggestedReference": "Loc/0043-2026/YL/JD"
}
```

The optional `issueDate` query parameter (`YYYY-MM-DD`) selects the year printed in
`suggestedReference`; without it the current year is used for the suggestion only.
The sequence is global: it starts at `1` when no offers exist and is one more than the
highest number recorded so far, whether it was stored as a serial number or parsed from
a reference. Legacy references such as `77/OCT./YL/AA` and `Loc/0377/YL/AA` still
contribute their first numeric segment. `suggestedReference` uses `YL` and the current
user's initials; the saved reference is generated by the server when the offer is created.

### Offer create / update

`POST /api/offers` and `PUT /api/offers/{id}` accept the offer draft. Request fields
include `offerType`, `attentionAr`, `bankAccountIds` and the Arabic customer/bank fields
described below.

`offerType` is required and accepts `Loc` (local) or `Int` (international). The
server generates the reference on create as
`{offerType}/{0000 sequence}-{issue date year}/YL/{initials}`, for example
`Loc/0378-2026/YL/AA`. On update the number and initials are kept and only the type
segment follows the selected offer type.

```json
{
  "reference": "Loc/0100-2026/YL/JD",
  "offerType": "Loc",
  "version": 1,
  "customerId": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
  "attention": "Nader William",
  "attentionAr": "نادر وليام",
  "taxId": "200-145-890",
  "issueDate": "2026-10-21",
  "dueDate": "2026-11-20",
  "paymentTermId": "0e6a9e7a-5f8e-4f1e-9c6d-8f5f0c2f7b31",
  "currency": "EGP",
  "documentLanguage": "Ar",
  "notes": "Delivery split over two batches.",
  "formCode": "TF-06-01A",
  "orderDiscount": 0,
  "orderDiscountPercent": 0,
  "taxPercent": 14,
  "otherTaxEnabled": false,
  "lines": [],
  "deliveryBatches": []
}
```

The response is the same `OfferDetailDto` shown above.

`validityDays` (0–365, default 0) is the number of days the offer stays valid after
`issueDate`. `0` means no validity is printed. The offer detail returns `validityDays`
and `rootOfferId`; the document metadata returns `validityDays` and `expiryDate`
(`issueDate + validityDays`, `null` when 0).

### Versions

Every version of an offer shares a `rootOfferId`, a stable id that does not change when
the reference changes. Versions are grouped by `rootOfferId`, not by `reference`.

- `POST /api/offers` without `parentOfferId` starts a new chain (`version` as sent, 1 by default).
- `POST /api/offers` with `parentOfferId` adds a version to the chain of that parent. The
  server sets `version` to the highest version of the chain + 1 and ignores the value
  sent. The `reference` may be changed in the new version and the chain stays the same.
- `POST /api/offers/{id}/revise` copies the offer as the next version with the same reference.
- `DELETE /api/offers/{id}` permanently removes the parent and every version in the chain,
  including their lines and delivery batches.
- A `reference` that belongs to another offer returns `409` ("An offer with this reference already exists.").

### Reference rules

A reference must follow:

```text
<offerType>/<0000 sequence>-<year>/YL/<initials>
```

- `offerType` is `Loc` or `Int` and must match the offer's `offerType`.
- `sequence` is one or more digits, printed zero-padded to four; the optional `-<year>`
  segment is four digits taken from the offer's issue date. References without it are
  still accepted.
- `YL` is the static company identifier.
- `initials` is one or more letters or digits. The server uses the authenticated
  user's stored `initials`, falling back to the first letters of their full name, and
  prints it in uppercase.

Matching is case-insensitive. A malformed reference, a reference whose type does not
match the offer type, or a missing `offerType` is returned through the offer
validation shape:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Reference must start with 'Loc' to match the offer type.",
  "message": "Offer validation failed.",
  "errors": [
    "Reference must start with 'Loc' to match the offer type."
  ],
  "warnings": []
}
```

The validation messages are:

- `Offer type is required.`
- `Reference must follow the format Loc/42/YL/JD.`
- `Reference must start with 'Loc' to match the offer type.`

### Saving an offer (two calls)

1. `POST /api/offers` always creates the offer as `Draft`. A `status` in the body is
   ignored. `dueDate` is derived from the payment term, and `orderDiscount` from
   `orderDiscountPercent`; neither needs to be sent. Line `discountPercent` is accepted
   but not applied (discounts are order-level).
2. `PATCH /api/offers/{id}/status` with `{ "status": "Issued" }` (case-insensitive)
   moves a Draft to Issued and returns the `OfferDetailDto`.

Ids sent for lines, packaging rows and batches are used only to link allocations. The
stored ids are generated by the server, so use the ids in the response afterwards.
Empty or duplicate ids are replaced and allocations follow the final ids; an allocation
without ids is matched by `materialId` and `packageTypeId`.

Allowed transitions: `Draft → Issued`, `Issued → Accepted`, `Issued → Rejected`. Any
other change returns `400` ("Cannot transition an offer from Issued to Draft.").

When a Draft is issued it is validated again with the same checks as create/update
(reference format and month, customer, payment term, materials, packaging and
delivery allocations) plus **at least one line**. Failures return the `400` shown above
and the offer stays a Draft. Drafts without lines can still be saved; only issuing
requires a line. A `reference` used by another offer, or a duplicate version in the same
chain, returns `409`.

### Bank accounts on an offer

The offer draft (`POST /api/offers`, `PUT /api/offers/{id}`, calculate and revise) and
the offer detail accept `bankAccountIds`:

```json
{
  "bankAccountIds": [
    "b5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
    "c6d48d67-30f4-4ebf-88f5-7d8d9aef0c12"
  ]
}
```

- `null` or omitted on create/calculate selects the first two active accounts by
  `sortOrder`. Updating, revising or issuing an older offer with a missing selection
  keeps every active account. The resolved list is stored, so a later Settings change
  does not silently change this offer.
- `[]` stores an offer with no bank accounts, for cash on delivery. The printed document omits the bank details.
- An unknown or inactive id returns `400` ("Bank account … was not found or is
  inactive.").
- The selection is stored as `bank_account_ids` (PostgreSQL `uuid[]`) with **no foreign
  key**: deleting an account never fails because an offer used it.
- The snapshot and printed document list only the selected accounts, ordered by
  `sortOrder`.
- `POST /api/offers/{id}/revise` copies the selection from the source version.
- Offers created before this feature have `bankAccountIds: null` and keep printing all
  active accounts, matching their existing snapshot.

## Customers

`GET /api/customers`, `GET /api/customers/lookup`, `GET /api/customers/{id}`,
`POST /api/customers` and `PUT /api/customers/{id}` expose three new fields:
`addressAr`, `cityAr` and `contactPersonAr`.

```json
{
  "id": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
  "customerCode": "CUST-001",
  "name": "Duravit Egypt",
  "nameAr": "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
  "taxId": "200-145-890",
  "contactPerson": "Nader William",
  "contactPersonAr": "نادر وليام",
  "email": "nader@example.com",
  "phone": "+20 100 000 0000",
  "city": "Badr City",
  "cityAr": "مدينة بدر",
  "address": "10th of Ramadan City, Egypt",
  "addressAr": "العاشر من رمضان، مصر",
  "defaultPaymentTermId": "0e6a9e7a-5f8e-4f1e-9c6d-8f5f0c2f7b31",
  "defaultPaymentTermLabel": "30 days",
  "defaultCurrency": "EGP",
  "erpNextId": null,
  "offersCount": 3,
  "offers": []
}
```

Create/update request:

```json
{
  "customerCode": "CUST-001",
  "name": "Duravit Egypt",
  "nameAr": "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
  "taxId": "200-145-890",
  "contactPerson": "Nader William",
  "contactPersonAr": "نادر وليام",
  "email": "nader@example.com",
  "phone": "+20 100 000 0000",
  "city": "Badr City",
  "cityAr": "مدينة بدر",
  "address": "10th of Ramadan City, Egypt",
  "addressAr": "العاشر من رمضان، مصر",
  "defaultPaymentTermId": "0e6a9e7a-5f8e-4f1e-9c6d-8f5f0c2f7b31",
  "defaultCurrency": "EGP"
}
```

Arabic values are optional. Existing English fields keep their previous meaning.

## Banks

### `GET /api/banks`

Paged bank directory, ordered by `sortOrder` then bank name. Cached in the `catalog`
region until a bank, settings or import write invalidates it.

Query: `search` (bank, branch or account number), `currency`, `isActive`, `page`
(default 1), `pageSize` (default 20, max 200).

```json
{
  "items": [
    {
      "id": "b1a3f0b8-3c2e-4fe2-8f4e-7a2f0f6b9c21",
      "bank": "QNB",
      "bankAr": "بنك قطر الوطني",
      "branch": "Badr City",
      "branchAr": "مدينة بدر",
      "accountHolderName": "Technocom for Commercial Agencies",
      "accountHolderNameAr": "شركة تكنوكوم للتوكيلات التجارية",
      "accountNumber": "20319811443",
      "swift": "QNBAEGCXXXX",
      "iban": "EG100037015208182031981144393",
      "currency": "EGP",
      "sortOrder": 1,
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

`GET /api/banks/{id}` returns the same shape or `404`.

### `POST /api/banks` and `PUT /api/banks/{id}`

Create: Admin, Sales and Accountant. Edit: Admin and Accountant.

Accept the account fields without `id`. `bank`, `branch` and `accountNumber` are
required. A missing or zero `sortOrder` is appended to the end of the list. There is
Delete: Admin, Sales and Accountant (`DELETE /api/banks/{id}`). Offers store bank ids
without a foreign key, so deleting an account never fails because an offer used it.

## Settings and bank accounts

### `GET /api/settings`

The workspace object now includes `bankAccounts`, ordered by `sortOrder` then bank
name.

```json
{
  "company": {
    "id": "0d8b1b6e-1a36-4e15-96d0-3e8ac4edde1a",
    "name": "TECHNOCOM",
    "nameAr": "شركة تكنوكوم للتوكيلات التجارية",
    "tagline": "For Commercial Agencies",
    "taglineAr": "للتوكيلات التجارية",
    "formCode": "TF-06-01A",
    "referencePrefix": "YL",
    "address": "Cairo, Egypt",
    "phone": "+20 2 0000 0000",
    "email": "info@example.com",
    "logoUrl": null,
    "logoWidthMm": 40,
    "logoHeightMm": 20
  },
  "workspace": {
    "id": "3a3f1a8b-0f5a-4b40-93b4-4f7f1e3f8b11",
    "remindBeforeExpiry": true,
    "daysBeforeExpiry": 30,
    "defaultCurrency": "EGP",
    "defaultTaxPercent": 14,
    "defaultPaymentTermId": "0e6a9e7a-5f8e-4f1e-9c6d-8f5f0c2f7b31",
    "defaultDocumentLanguage": "Ar",
    "erpNextUrl": null,
    "bankAccounts": [
      {
        "id": "b1a3f0b8-3c2e-4fe2-8f4e-7a2f0f6b9c21",
        "bank": "QNB",
        "bankAr": "بنك قطر الوطني",
        "branch": "Badr City",
        "branchAr": "مدينة بدر",
        "accountHolderName": "Technocom for Commercial Agencies",
        "accountHolderNameAr": "شركة تكنوكوم للتوكيلات التجارية",
        "accountNumber": "20319811443",
        "swift": "QNBAEGCXXXX",
        "iban": "EG100037015208182031981144393",
        "currency": "EGP",
        "sortOrder": 1,
        "isActive": true
      }
    ]
  }
}
```

### `PUT /api/settings` (Admin)

When `workspace.bankAccounts` is supplied, the list is reconciled: accounts with an
existing `id` are updated, items with `id: "00000000-0000-0000-0000-000000000000"`
(or omitted `id`) are created, and existing accounts not present in the list are
deleted. Omit `bankAccounts` to leave the bank list untouched. Supplying an empty
array (`[]`) returns `400` ProblemDetails with the message
"At least one bank account is required. Omit bankAccounts to leave existing accounts unchanged."

```json
{
  "workspace": {
    "defaultDocumentLanguage": "Ar",
    "bankAccounts": [
      {
        "id": "00000000-0000-0000-0000-000000000000",
        "bank": "SAIB",
        "bankAr": "البنك العربي الدولي (SAIB)",
        "branch": "Shubra",
        "branchAr": "شبرا",
        "accountHolderName": "Technocom for Commercial Agencies",
        "accountHolderNameAr": "شركة تكنوكوم للتوكيلات التجارية",
        "accountNumber": "30203004433-10460",
        "swift": "SBNKEGCXXXX",
        "iban": "EG860035003003020300443310460",
        "currency": "EGP",
        "sortOrder": 2,
        "isActive": true
      }
    ]
  }
}
```

`bank`, `branch` and `accountNumber` are required for every bank account. Missing
values return `400` ProblemDetails with field keys:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "One or more bank accounts are invalid.",
  "message": "One or more bank accounts are invalid.",
  "errors": {
    "workspace.bankAccounts[0].bank": ["Bank is required."],
    "workspace.bankAccounts[0].branch": ["Branch is required."],
    "workspace.bankAccounts[0].accountNumber": ["Account number is required."]
  }
}
```

## Offer document

`GET /api/offers/{id}/document` adds Arabic equivalents for the bill-to block and bank
accounts:

```json
{
  "billTo": {
    "customerId": "a5c48d67-30f4-4ebf-88f5-7d8d9aef0c11",
    "customerCode": "CUST-001",
    "name": "Duravit Egypt",
    "nameAr": "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
    "address": "10th of Ramadan City, Egypt",
    "addressAr": "العاشر من رمضان، مصر",
    "city": "Badr City",
    "cityAr": "مدينة بدر",
    "contactPerson": "Nader William",
    "contactPersonAr": "نادر وليام",
    "attention": "Nader William",
    "attentionAr": "نادر وليام",
    "taxId": "200-145-890",
    "email": "nader@example.com",
    "phone": "+20 100 000 0000"
  },
  "bankAccounts": [
    {
      "bank": "QNB",
      "bankAr": "بنك قطر الوطني",
      "branch": "Badr City",
      "branchAr": "مدينة بدر",
      "accountHolderName": "Technocom for Commercial Agencies",
      "accountHolderNameAr": "شركة تكنوكوم للتوكيلات التجارية",
      "accountNumber": "20319811443",
      "swift": "QNBAEGCXXXX",
      "iban": "EG100037015208182031981144393",
      "currency": "EGP"
    }
  ]
}
```

The example shows only the affected blocks; the real document also contains header,
metadata, items, totals and delivery schedule. When an Arabic value is missing, the
document returns the English value in the corresponding `...Ar` field, so Arabic
templates never render blank bill-to or bank details. English fields are unchanged.

## Data import

Four import types move spreadsheet data into the workspace:

| Type | Route segment | Updates |
| --- | --- | --- |
| Customers | `customers` | `Customer` upsert |
| Materials & products | `materials` | `Material` upsert |
| Price list | `prices` | `Material.ReferencePricePerKg` |
| Bank accounts | `bank-accounts` | `BankAccount` upsert |

The flow is three calls: download a template, preview a file, commit the preview.
The parsed preview is stored on the server; the commit never trusts client rows.

### `GET /api/import/templates/{type}`

Returns a UTF-8 CSV file (with BOM, so Excel keeps Arabic text) whose header row is
the exact template of that type. `Content-Disposition` names it
`{type}-template.csv`.

| Type | Columns |
| --- | --- |
| `customers` | `customerCode, erpNextId, name, nameAr, taxId, contactPerson, contactPersonAr, email, phone, city, cityAr, address, addressAr, defaultPaymentTerm, defaultCurrency` |
| `materials` | `itemCode, erpNextItemCode, name, category, referencePricePerKg, uom, isActive, defaultWeightKg` |
| `prices` | `itemCode, referencePricePerKg` |
| `bank-accounts` | `bank, bankAr, branch, branchAr, accountHolderName, accountHolderNameAr, accountNumber, swift, iban, currency, isActive, sortOrder` |

Header matching is case-insensitive and trimmed. Unknown columns are ignored.

### `POST /api/import/{type}/preview` (Admin, Accountant)

`multipart/form-data` with one `file` part: a `.xlsx` (first worksheet) or `.csv`
file. Limits: 5 MB and 5,000 data rows. Rows that are completely empty are ignored.
An unknown `type` returns `400` ProblemDetails.

```json
{
  "importId": "b1a3f0b8-3c2e-4fe2-8f4e-7a2f0f6b9c21",
  "summary": { "total": 3, "new": 1, "update": 1, "unchanged": 0, "error": 1 },
  "rows": [
    {
      "rowNumber": 2,
      "status": "new",
      "errors": [],
      "values": { "customerCode": "CUST-001", "name": "Duravit Egypt", "nameAr": "ديورافيت", "defaultCurrency": "EGP" },
      "existing": null,
      "changes": []
    },
    {
      "rowNumber": 3,
      "status": "update",
      "errors": [],
      "values": { "customerCode": "CUST-002", "name": "New name" },
      "existing": { "customerCode": "CUST-002", "name": "Old name" },
      "changes": [{ "field": "name", "oldValue": "Old name", "newValue": "New name" }]
    },
    {
      "rowNumber": 4,
      "status": "error",
      "errors": ["Email address 'not-an-email' is not valid.", "Unknown currency 'XYZ'."],
      "values": { "customerCode": "CUST-003", "email": "not-an-email", "defaultCurrency": "XYZ" },
      "existing": null,
      "changes": []
    }
  ]
}
```

- `status` is one of `new`, `update`, `unchanged`, `error`.
- `rowNumber` is the spreadsheet row, so the header is row 1 and the first data row
  is row 2.
- `values` only holds the template columns. `existing` is present for `update` and
  `unchanged` rows. `changes` only lists the fields the file actually filled in.
- A row with errors never blocks the other rows; it is counted in `summary.error`.

Validation rules:

- Required: `customerCode` and, for new customers, `name` and `address`;
  `itemCode` and, for new materials, `name` and `category`; `itemCode` and
  `referencePricePerKg` for prices.
- A code repeated inside the file is an error on the later row.
- Material `category` is one of `Polyol`, `Iso`, `System`, `Additive`.
- `referencePricePerKg` and `defaultWeightKg` are numbers, `>= 0`.
- `defaultCurrency` is one of `EGP, USD, EUR, GBP, SAR, AED, KWD, QAR`; empty
  defaults to `EGP` for new customers.
- `defaultPaymentTerm` is resolved by code, English label or Arabic label of an
  existing payment term. It is returned as the resolved English label.
- `email` must be a valid address.
- `isActive` accepts `true/false`, `yes/no`, `1/0`; empty defaults to `true` for
  new materials.
- Price rows need a material that already exists (`itemCode`, falling back to
  `erpNextItemCode`) and update only `ReferencePricePerKg`.
- Bank accounts are matched by `accountNumber`, trimmed and case-insensitive. `bank`,
  `branch` and `accountNumber` are required on every row. A number repeated inside the
  file is an error on the later row; if the database already holds several accounts with
  the same number the row is an error ("ambiguous") rather than guessing. `currency`
  defaults to `EGP`, `isActive` to `true` and an empty `sortOrder` to the highest
  existing order plus the row position in the file (new rows only).

### `POST /api/import/{importId}/commit` (Admin, Accountant)

Applies every row that is not an error in one transaction. Records are upserted by
`CustomerCode` / `ItemCode`, falling back to `ErpNextId` / `ErpNextItemCode`; bank
accounts are upserted by `AccountNumber`. Empty cells leave the stored value unchanged.
The call is idempotent: committing the same `importId` again returns the stored result
and writes nothing. Committing bank accounts invalidates the cached catalog, so the next
offer calculation and bootstrap see the new list. Imports never delete records.

```json
{
  "importId": "b1a3f0b8-3c2e-4fe2-8f4e-7a2f0f6b9c21",
  "created": 10,
  "updated": 3,
  "skipped": 2,
  "failed": 1
}
```

`skipped` counts unchanged rows, `failed` counts preview rows with errors (and rows
whose target disappeared). An unknown `importId` returns `404`.

### `GET /api/import/history?page=1&pageSize=20`

The paged commit log, newest first. Only committed imports are listed.

```json
{
  "items": [
    {
      "id": "b1a3f0b8-3c2e-4fe2-8f4e-7a2f0f6b9c21",
      "type": "customers",
      "fileName": "customers-october.xlsx",
      "createdByName": "Amgad Anwar",
      "createdAt": "2026-10-24T09:00:00+00:00",
      "committedAt": "2026-10-24T09:04:00+00:00",
      "created": 10,
      "updated": 3,
      "skipped": 2,
      "failed": 1
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

## Users (Admin)

Admin only. Accounts can be deactivated with `isActive: false` or deleted with
`DELETE /api/users/{id}`. A deactivated or deleted user cannot sign in and their existing
token stops working on the next request. Self-delete and deleting the last active admin
return `409`.

`UserDto`: `id`, `username`, `fullName`, `initials`, `email`, `role` (`Admin`, `Sales` or
`Accountant`), `roleTitle`, `roleTitleAr`, `team`, `teamAr`, `isActive`, `createdAt`,
`updatedAt`. The password hash is never returned.

### `GET /api/users`

| Query parameter | Default | Notes |
| --- | --- | --- |
| `search` | – | Matches username, full name and email. |
| `role` | – | `Admin`, `Sales` or `Accountant`. |
| `isActive` | – | `true` or `false`. |
| `page` | `1` | |
| `pageSize` | `12` | Maximum 100. |

Returns the usual `{ items, page, pageSize, totalCount, totalPages }` envelope, ordered by
full name. `GET /api/users/{id}` returns one `UserDto` or `404`.

### `POST /api/users`

Body: `username` (3-50 letters, digits, `.`, `-`, `_`; unique ignoring case), `fullName`,
`initials` (1-6 letters or digits, printed in offer references, stored upper case),
`role` (required), `password` (8 to 72 bytes), and optional `email`, `roleTitle`,
`roleTitleAr`, `team`, `teamAr`, `isActive` (default `true`). Returns `201` with the
`UserDto`. Invalid input returns `400` with an `errors` dictionary keyed by field name.

### `PUT /api/users/{id}`

Same fields except `username` and `password`, which do not change here. When `isActive`
is omitted the current value is kept. Returns `409` when the change would deactivate or
demote yourself, or would leave the system without an active administrator.

### `POST /api/users/{id}/reset-password`

Body: `{ "newPassword": "..." }`. Returns `204`. Users change their own password with
`POST /api/auth/change-password`.

## Live updates

### `GET /api/events` (any signed-in user)

A Server-Sent Events stream (`text/event-stream`) that tells a browser when data changed or when
the signed-in user has a new notification, so screens can load it again without a manual refresh.
`change` events carry **topic names only, never data**; `notification` events carry only the stored
DTO below (ids, labels, counts and status), never restricted entity data. Actual entity content
still goes through the normal, authorized endpoints.

Send the usual `Authorization: Bearer <token>` header. The browser `EventSource` cannot set
headers, so use `fetch` and read the response body as a stream.

```
retry: 5000
event: ready
data: {}

event: change
data: {"topics":["offers","customers"]}

event: notification
data: {"id":"...","type":"offer.created","actorName":"Amgad Anwar","entityType":"offer","entityId":"...","entityLabel":"Loc/0042-2026/YL/AA","meta":{},"createdAt":"2026-11-26T12:00:00+00:00","readAt":null}

: ping
```

| Event | Meaning |
| --- | --- |
| `ready` | The stream is open and subscribed. Sent once, first. |
| `change` | Something changed. `topics` lists what, see below. Changes that happen close together arrive as one event. |
| `notification` | One new inbox row addressed to this signed-in user. Its `data` is the stored DTO below; it never contains another user's row or restricted entity data. |
| `: ping` | A comment line every 20 seconds while idle, to keep proxies from closing the connection. |

| Topic | Announced after a write to |
| --- | --- |
| `customers` | customers |
| `materials` | materials and prices |
| `catalog` | payment terms, package types, bank accounts, company and workspace settings |
| `banks` | bank accounts (always together with `catalog`) |
| `offers` | offers, their lines, packaging and delivery batches |
| `users` | users |
| `imports` | a committed import |
| `audit` | an audit row written or removed by retention |

An event is sent only after the write is saved (and its transaction committed), and after the
server's own reference-data cache has been cleared, so reloading straight away returns the new
data.

The server ends the stream after at most 30 minutes, or when the token expires, so a
deactivated user cannot stay connected. A client reconnects (`retry` is 5 seconds) and should
reload what it shows, because changes made while it was disconnected are not replayed.

Events are announced in-process and, for direct PostgreSQL writes, through a dedicated
`LISTEN portal_changes` connection. Statement-level `AFTER INSERT OR UPDATE OR DELETE OR TRUNCATE`
triggers on the live data tables send `pg_notify('portal_changes', <table name>)`; the API
invalidates its reference-data cache first, then publishes the affected topics. The notification
payload contains only the table name, never row data.

`ConnectionStrings:PortalNotifications` / `PORTAL_NOTIFICATIONS_CONNECTION_STRING` overrides the
connection used for live updates; it otherwise falls back to the normal database connection. Set it
only when the host offers a direct/session-mode connection. Transaction poolers such as MonsterASP's
PgBouncer do not support `LISTEN`, so while that listener is disconnected the API polls
`pg_stat_all_tables` row counters every 2 seconds and announces tables whose counters changed.
In-process publishes and the poll fallback keep live updates working on poolers; `LISTEN` remains
the low-latency path where it is available. After every listener (re)connect the server re-announces
all mapped tables because notifications during a disconnect are lost.

### Notifications

The notification inbox is server-scoped: recipients are chosen from the audience matrix on the
server, the acting user never receives a notice about their own write, and every row belongs to
exactly one recipient. Delivery is SSE only — there is no email, SMS, push or user preference
endpoint. A notification failure never fails the write that triggered it. Notifications are
announced locally and through `pg_notify('portal_events', ...)` on every publish, so another API
instance can deliver them; each instance deduplicates by row id. Rows older than 60 days are
deleted by a daily cleanup job.

`GET /api/events` carries one `event: notification` frame per new row. The payload is the same
DTO returned by the inbox: `id`, `type`, `actorName`, `entityType`, `entityId`, `entityLabel`,
`meta`, `createdAt` and `readAt`. `entityId`/`entityLabel`/`meta` identify the subject only; the
browser still loads the actual entity through its authorized endpoint. Supported types are
`offer.created`, `offer.revised`, `offer.status_changed`, `offer.deleted`, `customer.created`,
`customer.deleted`, `material.created`, `material.deleted`, `bank.created`, `bank.deleted`,
`import.committed`, `user.created`, `user.deactivated` and `user.role_changed`. An import emits one
`import.committed` notice per eligible active user except the committer (admins, sales and
accountants), not one per row.

| Endpoint | Result |
| --- | --- |
| `GET /api/notifications?page=1&pageSize=20&unreadOnly=false` | The signed-in user's inbox with `items`, `totalCount`, `unreadCount`, `page` and `pageSize`. `page` and `pageSize` are bounded; an overflowing page is empty rather than an error. |
| `GET /api/notifications/unread-count` | `{ "unreadCount": n }` for the signed-in user only. |
| `POST /api/notifications/{id}/read` | `204`. Returns `404` when the id is missing or belongs to another user, so ids cannot probe another inbox. |
| `POST /api/notifications/read-all` | `204`; marks only the signed-in user's rows read. |

The browser reads the inbox on load and on every `ready` frame (reconnect), deduplicates by id, and
shows the count from the same page snapshot as its rows. Deactivation, role change or deletion
closes that user's SSE subscription; streams also recheck eligibility on each heartbeat and before
writing a frame.

### System status (Admin)

`GET /api/system/status` gives admins the diagnostics behind the anonymous `/health` smoke test.
It requires `ManageUsers`: anonymous requests return `401`, Sales/Accountant `403`.
The payload has `status` (`Healthy`, `Degraded` or `Unhealthy`), `checkedAt`, `version`,
`apiVersion`, `commit`, `startedAt`, `environment`, `uptimeSeconds`, `database`, `live`,
`realtime`, `audit`, `jobs`, `security` and `data`.

- `version` equals `apiVersion`: the app/API version from the built assembly. `commit` is the
  deployed short git SHA (`BUILD_COMMIT`, otherwise `SOURCE_COMMIT`) or `null` when absent or
  unknown; `startedAt` is the process start time in UTC. The frontend footer version comes from
  `frontend/package.json`, so the two versions may differ.
- `database`: `status` (`Up`/`Down`), `latencyMs`, `latencyHistory` (last 20 probe samples),
  `serverVersion`, `sizeBytes`, `connectionsUsed`, `connectionsMax`, `pendingMigrations`,
  `lastMigration`.
- `live`: `state` (`connected`, `disconnected` or `disabled`), `disabled`, `pollingFallback`
  (`true` while a listener is down), the individual `changes` and `notifications` listener
  states, `changesLastEventAt`, `notificationsLastEventAt`, `reconnectCount` and `lastError`.
- `realtime`: `connectedClients`, `notificationQueueDepth`, `dropped`.
- `audit`: `rowsTotal`, `queueDepth`, `dropped`, `lastWriteAt`, `retentionDays`.
- `jobs`: `[{ "name", "lastRunAt", "durationMs", "result", "nextRunAt" }]` for the audit writer,
  audit retention and notification publisher.
- `security`: `failedSignIns24h`, `lastFailedSignInAt`, `activeUsers`, `inactiveUsers`.
- `data`: `offers`, `customers`, `materials`, `activeUsers`.

A disconnected or disabled listener makes the overall status `Degraded`; a database that cannot be
reached makes it `Unhealthy`. Each probe is isolated: a failing probe yields `null` for that field
(or `ok: false` on the internal probe) and never removes the rest of the payload. The endpoint
never starts a listener or changes configuration, and returns no secrets, connection strings,
raw exception text, usernames, IPs or tokens; `lastError` is only an exception type name.

### Audit log (Admin)

All `/api/audit-logs` endpoints require `ManageUsers`: anonymous requests return `401`,
Sales/Accountant `403`. Every mutating signed-in request, login attempt, password change failure
and offer document open writes one append-only row. Reads, import previews, notification read state, the list,
summary and filter endpoints are not audited; only the CSV export writes a row.

Rows snapshot the actor (`actorUserId`, `actorUsername`, `actorFullName`,
`actorRole`) plus `action`, `category`, `entityType`, `entityId`, `entityLabel`, `description`,
`outcome` (`success`/`failure`), `httpMethod`, `path`, `statusCode`, `ipAddress`, `details` and
`occurredAt`, and now also the raw `userAgent` header (nullable, max 256 chars) and `durationMs`.
A successful login takes its actor from `LoginResponseDto`, including anonymous login; a failed
login has a null `actorUserId` and keeps a sanitized attempted username (up to 100 chars) in
`actorUsername`. Every action code has an explicit default `description`. Offer, customer,
material, bank and user writes include `entityId` and `entityLabel` (reference/name/username).
`audit.exported` is the action code for a CSV export, in the `audit` category. Passwords, hashes,
tokens, request bodies and authorization headers are never stored; `details` only holds a small
sanitized dictionary of non-secret values. The IP is the socket address reported by ASP.NET Core;
no forwarded-header middleware is configured. Rows are written by a background worker from a
bounded queue, so a full queue drops new rows instead of failing the request. A failed write batch
is retried up to three times and the queue is flushed on shutdown. A daily retention job deletes
rows older than `Audit:RetentionDays` (default `365`; set to `0` to keep everything).
The `AddAuditUserAgent` migration adds nullable `user_agent varchar(256)` and `duration_ms integer`
to `audit_logs`, with no backfill; the append-only trigger is unchanged.

| Endpoint | Result |
| --- | --- |
| `GET /api/audit-logs` | Newest first. Filters: `from`, `to`, `actorId`, `category`, `action`, `entityType`, `outcome`, `search`, `page` (default `1`), `pageSize` (default `50`, max `200`). Returns `items`, `page`, `pageSize` and `totalCount`. Each item also has `userAgentLabel` (a human label derived from the raw header, `"Unknown device"` when unrecognised), `durationMs` and `entityRoute`: `/offers/{id}`, `/customers/{id}`, `/materials/{id}`, `/banks/{id}` or `/users/{id}` only when the referenced entity still exists, otherwise `null`. Existence is resolved in one batched query per entity type. |
| `GET /api/audit-logs/summary` | Same filters as the list, without paging. `{ "totalCount", "byAction", "byCategory", "byOutcome", "byActor", "byDay" }`; each group is `{ "key", "count" }`, limited to the top 20 by count for actions, categories and actors. `byDay` keys are `yyyy-MM-dd`, in ascending date order. |
| `GET /api/audit-logs/export` | Same filters as the list, without paging. Streams a UTF-8 CSV (with BOM) as `text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="audit-log-YYYYMMDD-HHmm.csv"` and `Cache-Control: no-store`, capped at the 10,000 most recent matching rows. Columns: `occurred_at, actor, action, category, outcome, entity, description, path, status_code, duration_ms, user_agent`. Cells containing commas, quotes or line breaks are wrapped in double quotes with internal quotes doubled; cells starting with `=`, `+`, `-`, `@`, tab or carriage return are prefixed with a single quote to neutralise spreadsheet formulas. Each call enqueues one `audit.exported` row with `details.rowCount` and `details.cap` and description `Exported N audit rows`. |
| `GET /api/audit-logs/filters` | `{ "actors": [{ "id", "username", "fullName" }], "categories": [...], "actions": [...] }` for building the filter controls. |

`GET /api/events` carries an `audit` change topic when a row is written or deleted. Descriptions are
written in English on the server; clients localize by `action` and fall back to the stored text.

`GET /health` is anonymous and returns `liveListener` as `connected`, `disconnected` or
`disabled` alongside `status` and `database`. `disabled` means no notification connection string
is configured, so neither the dedicated `LISTEN` connections nor the counter poll can run;
`disconnected` means a connection string exists but the `LISTEN` socket is down, where the poll
fallback applies. The field is a health signal only: local notification delivery and in-process
live updates still work in either case.

## Additive contract summary

- Offer list: new `latestOnly`, `sortBy`, `sortDir` query parameters; new
  `versionsCount`, `lastModifiedByName`, `lastModifiedAt` response fields.
- Offer detail: new `updatedByUserId`, `updatedByName`, `versionsCount`, `attentionAr`.
- Offer draft: new `attentionAr`, `validityDays`.
- Offer detail and list: new `rootOfferId`; offer detail and document metadata: new `validityDays` (and `expiryDate` in the document).
- Versions are chained by `rootOfferId`, not by `reference` (see Versions).
- Offer document: new `nameAr` (now always populated), `addressAr`, `cityAr`,
  `contactPersonAr`, `attentionAr`, `bankAr`, `branchAr`, `accountHolderNameAr`.
- Customers: new `addressAr`, `cityAr`, `contactPersonAr` on lookup/list/detail and
  upsert payloads.
- Settings: new `workspace.bankAccounts` on GET and PUT, with Arabic bank fields.
- New API documentation covers the additive fields and JSON shapes below.
- Offer list and detail: new `offerType` (`Loc` or `Int`).
- Offer draft: `offerType` is now required. `reference` is optional on create (the
  server generates it) and keeps the stored value when omitted on update.
- Offer references now use `{offerType}/{sequence}/YL/{initials}`. The month segment
  was removed and `initials` comes from the authenticated user.
- Offer next-sequence: `offerType` query parameter; the `year` and `month` parameters
  were removed.
- Offer next-sequence: `suggestedReference` always follows the same rules as offer
  validation.
- Data import: new `/api/import/templates/{type}`, `/api/import/{type}/preview`,
  `/api/import/{importId}/commit` and `/api/import/history` routes; new
  `import_batches` table. Import types: `customers`, `materials`, `prices` and
  `bank-accounts`.
- Bank account import: `bank-accounts` type with template, preview and commit, upserted
  by `accountNumber`.
- Offer detail and draft: new `bankAccountIds` (`Guid[] | null`) and `bank_account_ids`
  column on `offers`; the printed document lists only the selected accounts.

- Roles: `UserRole` is now `Admin`, `Sales`, `Accountant`. `SalesUser` was renamed to
  `Sales` and keeps its stored value (1), so no data migration is needed. New `/api/users`
  routes (see Users). Customer, material, bank account, offer, import and settings
  endpoints now follow the role matrix above; a Sales user can no longer edit an existing
  customer or material.
- Offers already return `createdByName` and `createdAt` (list and detail), which the
  frontend now displays.

- New `GET /api/events` live-update stream (see Live updates). Nothing else changes.
- Audit log: new `actor` details for login attempts, `userAgent`/`durationMs` columns,
  `entityId`/`entityLabel` on entity writes, `audit.exported` action, and list-only
  `userAgentLabel`/`durationMs`/`entityRoute`; new admin `GET /api/audit-logs/summary`
  and `GET /api/audit-logs/export`.
- System status: new `apiVersion`, `commit`, `startedAt`, `database` metrics, `live`
  timestamps/error, `realtime`, `audit`, `jobs`, `security` and `data` payloads, with
  isolated probes.

Nothing else was removed or renamed. The only breaking change is the offer reference
format and the required `offerType`; the next-sequence `year`/`month` parameters are
replaced by `offerType`.
