# Sales Portal API

All endpoints are JSON over HTTPS and, except for `POST /api/auth/login`, require a
bearer token. Enum values are serialized as strings (for example `"Draft"`), dates as
`YYYY-MM-DD`, and timestamps as ISO-8601 UTC values.

Base URL in local development: `https://localhost:8443`.

## Conventions

- Validation failures return RFC 7807 `application/problem+json`. The legacy top-level
  `message` field is still present for existing clients.
- Offer calculation failures retain the offer-specific shape
  `{ "message": "...", "errors": [], "warnings": [] }`.
- Lists are 1-based and use `page` / `pageSize`.
- Every field described below is **additive**. Existing fields, names and routes are
  unchanged.

## Offers

### `GET /api/offers`

Returns offer summaries. By default only the highest version of each reference is
returned (`latestOnly=true`).

| Query parameter | Default | Notes |
| --- | --- | --- |
| `search` | – | Matches reference, customer name (English/Arabic) and customer code. |
| `status` | – | `Draft`, `Issued`, `Accepted`, `Rejected`, `Cancelled`; case-insensitive. An unknown value returns `400` ProblemDetails. |
| `latestOnly` | `true` | `false` returns every stored version. |
| `sortBy` | `updatedAt` | `updatedAt`, `createdAt`, `reference`, `total`, `issueDate`. |
| `sortDir` | `desc` | `asc` or `desc`. |
| `page` | `1` | |
| `pageSize` | `20` | |

```json
{
  "items": [
    {
      "id": "8f4d0f37-2a9b-4a2b-9a6b-2d9b2b5a4f10",
      "reference": "101/OCT./YL/AA",
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

New response fields: `versionsCount` (all versions of the same reference),
`lastModifiedByName` and `lastModifiedAt` (the latest modifier, falling back to the
creator). `latestOnly`, `sortBy` and `sortDir` are new query parameters with defaults
that preserve the previous ordering behaviour.

### `GET /api/offers/{id}`

Returns the full offer, including creator/modifier and document data.

```json
{
  "id": "8f4d0f37-2a9b-4a2b-9a6b-2d9b2b5a4f10",
  "reference": "101/OCT./YL/AA",
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
    "reference": "101/OCT./YL/AA",
    "version": 2,
    "versionsCount": 2,
    "lastModifiedByName": "Modifier User",
    "lastModifiedAt": "2026-10-22T11:40:00+00:00"
  },
  {
    "id": "1c4e45b0-f3b8-49a3-a1f4-2a5f9d4d7a10",
    "reference": "101/OCT./YL/AA",
    "version": 1,
    "versionsCount": 2,
    "lastModifiedByName": "Test User",
    "lastModifiedAt": "2026-10-21T08:15:00+00:00"
  }
]
```

The example omits unchanged list fields for brevity; the real items contain all of
them.

### `GET /api/offers/next-sequence?year=2026&month=5`

Suggests the next sequence and reference for a month. `year` and `month` default to the
current UTC year and month.

```json
{
  "suggestedSequence": 77,
  "suggestedReference": "77/MAY/YL/AA"
}
```

`suggestedReference` is built from the company's `referencePrefix` and the current
user's initials. It is only a suggestion; the user still types the reference.

### Offer create / update

`POST /api/offers` and `PUT /api/offers/{id}` accept the offer draft. The only new
request fields are `attentionAr` and the Arabic customer/bank fields described below.

```json
{
  "reference": "100/OCT./YL/AA",
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

### Reference rules

A reference must follow:

```text
<sequence>/<month>/<prefix>/<initials>
```

- `sequence` is one or more digits.
- `month` is `JAN.`, `FEB.`, `MAR.`, `APR.`, `MAY`, `JUN.`, `JUL.`, `AUG.`, `SEP.`,
  `OCT.`, `NOV.` or `DEC.`. Only `MAY` has no period.
- Month matching is case-insensitive (`oct.` is accepted).
- `prefix` and `initials` use letters, digits, `_` or `-`.

The month segment must match the offer's `issueDate` month. A malformed reference or a
month mismatch is returned through the offer validation shape:

```json
{
  "message": "Offer validation failed.",
  "errors": [
    "Reference month (OCT.) does not match the issue date month (NOV.)."
  ],
  "warnings": []
}
```

The exact month-mismatch message is
`Reference month (OCT.) does not match the issue date month (NOV.).`

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
deleted. Omit `bankAccounts` to leave the bank list untouched.

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

## Additive contract summary

- Offer list: new `latestOnly`, `sortBy`, `sortDir` query parameters; new
  `versionsCount`, `lastModifiedByName`, `lastModifiedAt` response fields.
- Offer detail: new `updatedByUserId`, `updatedByName`, `versionsCount`, `attentionAr`.
- Offer draft: new `attentionAr`.
- Offer document: new `nameAr` (now always populated), `addressAr`, `cityAr`,
  `contactPersonAr`, `attentionAr`, `bankAr`, `branchAr`, `accountHolderNameAr`.
- Customers: new `addressAr`, `cityAr`, `contactPersonAr` on lookup/list/detail and
  upsert payloads.
- Settings: new `workspace.bankAccounts` on GET and PUT, with Arabic bank fields.
- New API documentation covers the additive fields and JSON shapes below.
- Offer next-sequence: `suggestedReference` is documented and always uses the same
  month rules as offer validation.

Nothing was removed, renamed or made required that was previously optional.
