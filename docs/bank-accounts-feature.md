# Feature: bank account import + per-offer bank selection

Shared spec for backend and frontend. Decisions below are defaults; change them here first if they are wrong.

## 1. Import bank accounts

New import kind `bank-accounts`, same flow as customers/materials: template, preview, commit, history
(`/api/import/bank-accounts/preview` etc.). Mirror the customers import in `ImportService` / `ImportKinds`.

CSV columns (header names exact, camelCase like the other templates):
`bank,bankAr,branch,branchAr,accountHolderName,accountHolderNameAr,accountNumber,swift,iban,currency,isActive,sortOrder`

Rules:
- Natural key = `accountNumber` (trimmed, case-insensitive). Upsert: unknown key -> `new`, known key with changes -> `update`
  (list changed fields), identical -> `unchanged`.
- Required: `bank`, `branch`, `accountNumber` (same as Settings validation). Missing -> `error` row.
- Same `accountNumber` twice in one file -> second row is an `error` (like duplicate codes in the other imports).
- If the database already holds several accounts with the same number, the row is an `error` ("ambiguous"), never guess.
- Defaults: `currency` = EGP, `isActive` = true, `sortOrder` = max existing + position in file when empty.
- The import never deletes accounts. Commit invalidates the `Catalog` cache region (already used by bootstrap / offer catalog).

## 2. Select banks per offer

- Draft payload and offer detail get `bankAccountIds: Guid[] | null`.
- Create/update/revise/calculate honour it. `null` or omitted = all active accounts at save time (backward compatible), and
  the resolved list is stored. An empty array -> 400 "Select at least one bank account." An unknown or inactive id -> 400.
- Storage: nullable `uuid[]` column `bank_account_ids` on `offers`, NO foreign key, so deleting an account in Settings never
  fails because an offer used it. Offers created before this feature stay `null` (= all banks, as their snapshot already shows).
- The printed document / snapshot lists only the selected accounts, ordered by `sortOrder`. Filter the cached
  `OfferCatalog.BankAccounts` in memory (`LoadCatalogAsync`), do not add queries.
- Revising an offer copies the selection from the source version.
- Offer detail returns `bankAccountIds` (null for legacy offers).

## 3. Frontend

- Import page: new source "Bank accounts" (columns, labels, EN + AR strings, mock import support).
- Offer builder: a "Bank accounts" card in the last step with one checkbox per active account (bank, branch, account number,
  currency), select all / none, at least one required. Default = all active accounts. The live preview shows only the selected ones.
- Offer details/print uses the offer's own selection (`null` = all). New version / duplicate keeps the selection.

## Done when
Backend: `dotnet build` and `dotnet test` green, one migration, docs/API.md updated, tests for import (new/update/unchanged/
duplicates/ambiguous) and for offer selection (default, subset, empty, unknown id, revise).
Frontend: `pnpm typecheck`, `pnpm test`, `pnpm build` green, tests for the selector and the import source.
