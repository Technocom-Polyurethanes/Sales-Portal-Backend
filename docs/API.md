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
  "suggestedReference": "Loc/43/YL/JD"
}
```

The sequence is global: it starts at `1` when no offers exist and is one more than the
highest number recorded so far, whether it was stored as a serial number or parsed from
a reference. Legacy references such as `77/OCT./YL/AA` still contribute their first
segment. `suggestedReference` uses `YL` and the current user's initials. It is only a
suggestion; the user still types the reference.

### Offer create / update

`POST /api/offers` and `PUT /api/offers/{id}` accept the offer draft. Request fields
include `offerType`, `attentionAr`, `bankAccountIds` and the Arabic customer/bank fields
described below.

`offerType` is required and accepts `Loc` (local) or `Int` (international). When
`reference` is empty on create, the server generates one as
`{offerType}/{sequence}/YL/{initials}`. On update an omitted `reference` keeps the
stored one.

```json
{
  "reference": "Loc/100/YL/JD",
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
- A `reference` that belongs to another offer returns `409` ("An offer with this reference already exists.").

### Reference rules

A reference must follow:

```text
<offerType>/<sequence>/YL/<initials>
```

- `offerType` is `Loc` or `Int` and must match the offer's `offerType`.
- `sequence` is one or more digits.
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

- `null` or omitted selects every active account at save time. The resolved list is
  stored, so a later Settings change does not silently change this offer.
- `[]` returns `400` ("Select at least one bank account.").
- An unknown or inactive id returns `400` ("Bank account … was not found or is
  inactive.").
- The selection is stored as `bank_account_ids` (PostgreSQL `uuid[]`) with **no foreign
  key**: deleting an account in Settings never fails because an offer used it.
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

### `POST /api/import/{type}/preview` (Admin)

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

### `POST /api/import/{importId}/commit` (Admin)

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

Nothing else was removed or renamed. The only breaking change is the offer reference
format and the required `offerType`; the next-sequence `year`/`month` parameters are
replaced by `offerType`.
