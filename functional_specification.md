# BotEnergy — Functional Specification

> This document describes **only the business functionality that is currently implemented** in the system, extracted directly from the source code. It intentionally contains no implementation details. Every statement below is derived from actual code behavior.

---

# Project Purpose

BotEnergy is a self-service platform for IoT dispensing/charging stations (fuel dispensers, car-wash boxes, EV chargers, water dispensers, vacuum cleaners, vending machines).

A customer uses a mobile app to connect to a physical device via QR code, blocks money through the Payme payment system (pre-authorization / "hold"), receives a product (fuel, water, electricity, etc.) in real time, and pays only for what was actually delivered. Unused blocked money is automatically returned.

The platform side lets administrators and merchant staff manage merchants, stations, devices, products, users, roles, payments and reports.

---

# Functional Modules

| Module | Description |
|---|---|
| **Customer Authentication** | Self-registration by phone number with OTP, password setup, login, token refresh, password reset. |
| **Platform Authentication** | Login and token refresh for platform staff (no self-registration). |
| **Profile** | View own profile and balance, update own contact data, app bootstrap call. |
| **Sessions** | QR-based pending session, device connection, heartbeat, pause/resume on device connectivity loss, closing, history. |
| **Processes (Dispensing)** | Start / stop / pause / resume product delivery, real-time telemetry, automatic completion, watchdog finalization. |
| **Hold Invoices (Payme pre-authorization)** | Blocking money before dispensing, FIFO consumption, capture of used amount, refund of unused amount, expiry, operator management. |
| **QR Top-Up Payments** | Direct Payme QR payment to personal or organization balance, from mobile app or from a device. |
| **Balance & Billing** | Balance reading, admin balance top-up, payment reversal. |
| **Merchant Management** | Merchant CRUD, Payme credentials configuration. |
| **Organization Management** | Corporate organization CRUD with a shared balance. |
| **Station Management** | Stations with mandatory geo-coordinates, bound to a merchant. |
| **Device Management** | Device registration with unique serial numbers, activation, expert counter reset. |
| **Product Management** | Products bound to a device, with device-type compatibility rules and per-unit pricing. |
| **User Administration** | Platform users (Manage / Merchant staff), corporate customer users, natural (individual) customer users. |
| **Roles & Permissions** | Role management for platform and customer sides, permission catalogs restricted by role kind. |
| **Reports** | Personal usage report, organization usage report, merchant sales report; paginated view and Excel export. |
| **Real-Time Notifications** | Live session/process/balance/device-status updates to all screens watching a session, plus per-user push notifications. |
| **Device Identity & Messaging Security** | Device authentication by serial number; authenticated, replay-protected device messaging. |

---

# Business Rules

## Users and account types

- There are two independent user groups: **Platform** users (subtypes: *Manage* — full platform control; *Merchant* — staff scoped to one merchant) and **Customer** users (subtypes: *Natural* — individual; *Corporate* — organization employee).
- Only Natural (individual) customers can self-register. Corporate users are created by an administrator. Platform users are created only by other authorized platform users.
- A Natural user owns a personal balance. A Corporate user has no personal balance — they share their organization's balance.
- Each user has exactly one role. A newly self-registered customer automatically receives the default global "Natural" role; without it they would have no permissions.
- A blocked user cannot log in, cannot refresh tokens, cannot create sessions, and cannot make payments.
- A deleted user cannot refresh tokens.
- Phone numbers are unique within each user group (customer / platform / merchant registries are checked separately).
- All phone numbers must be in the canonical format: starts with `998`, exactly 12 digits, no `+` sign (e.g. `998901234567`). Input is softly normalized (spaces, dashes, parentheses, dots, a leading `+` or `00` are stripped) but a bare 9-digit local number is **not** auto-prefixed.

## Registration and login (customers)

- Registration is a strict 3-step flow: **Register → Verify OTP → Set Password**. Login is possible only after all three steps.
- Registering an already fully-registered phone returns a message directing the user to log in (no duplicate account is created).
- Registering a phone whose OTP is already verified directs the user to the password-setup step.
- Registering a phone that started but did not finish verification re-sends a new OTP.
- The OTP code is 6 digits, valid for 3 minutes, with a maximum of 5 verification attempts; after the limit the code is invalidated and a new OTP must be requested.
- A successfully verified OTP state is kept for 10 minutes (time window to set the password).
- The universal test OTP code `123456` works only when explicitly enabled (development environments only).
- OTP state is not persistent — a service restart clears all pending OTPs.
- The password must be at least 6 characters.
- Setting the password completes registration, marks the account verified, and immediately signs the user in (tokens are returned).
- Setting a password twice is rejected — the user is directed to log in.
- Password reset is available **only to fully registered users** and follows its own 3-step flow: request (OTP sent) → verify OTP → set new password. The OTP verification is consumed once the new password is set.
- Login failures do not disclose whether the phone exists: unknown phone and wrong password produce equivalent "phone or password incorrect" outcomes.
- If registration is incomplete, login returns explicit guidance about which step remains (verify OTP or set password).

## Tokens and sessions of authentication

- An access token is valid for 15 minutes; a refresh token for 7 days.
- Refresh tokens are single-use: every refresh revokes the old token and issues a new pair (rotation).
- Customer and platform refresh tokens are namespaced and not interchangeable — a customer token can never refresh a platform session and vice versa.
- Platform APIs accept only platform tokens; customer-facing APIs accept only customer tokens (separate audiences).
- Authentication endpoints are rate-limited per IP address: 30 requests per minute by default; exceeding the limit returns a "too many requests" response with a 60-second retry hint.

## Permissions

- Every protected action requires a named permission present in the caller's token; a missing permission results in access denial.
- Permission names follow the `{Area}.{Action}` convention; if an endpoint does not declare one explicitly, it is derived from the endpoint's controller and action names.
- Permissions are split into a **platform catalog** and a **customer catalog**; a role can only hold permissions allowed for its role kind:
  - **Manage roles** may hold any permission (full control).
  - **Merchant roles** may hold platform permissions **except** Manage-only ones: organization management, merchant registration/deletion/global listing, admin balance top-up, payment global listing and reversal, hold capture/refund/retry, setting Payme credentials, and the global natural-users list.
  - **Natural roles** are limited to self-service: session and process control, own profile, own reports, own top-ups, and hold-invoice operations.
  - **Corporate roles** = the natural set **plus** organization top-up and transaction history, organization reports, and management of the organization's sub-users.
- Attempting to attach a permission not allowed for the role kind is rejected with an explicit message naming the permission.

## Access scope

- A *Manage* user has no scope limit — they see and manage everything.
- A *Merchant* user sees and manages only entities belonging to their own merchant (its stations, devices, products, operators, roles, hold invoices, reports).
- A *Corporate* user sees and manages only entities of their own organization (its users, roles, reports, transactions).
- Scope checks apply to reading a single record, listing, updating, and deleting alike. Out-of-scope access is rejected as "not in your scope".
- Listings for a caller with no applicable scope return an empty list rather than an error.

## Sessions (customer ↔ device)

- A user may have **at most one active session** at a time. Active means any of: Created, Connected, InProcess, Paused, Settling. Creating a second one is rejected until the first is closed.
- Session creation produces a *pending session token* (rendered as a QR code) valid for **30 minutes**; repeated creation within that window returns the same token (idempotent).
- A pending session becomes a real session only when a device scans the QR: the device submits the user id and token; the token comparison must match exactly; the device must be known and active; the user must still have no active session. On success the session is *Connected* and the mobile app is notified in real time with the device's active product list.
- Once connected, the pending token is invalidated immediately.
- A session has a **sliding idle timeout of 30 minutes**. Any activity (heartbeat, telemetry, process action) refreshes it. An idle session is closed automatically with close reason *Timeout* and the user receives a push notification.
- The mobile app can send heartbeats to keep a session alive; heartbeats to a closed session are rejected.
- If a device stops communicating for **90 seconds**, it is marked offline; its live sessions (Connected/InProcess) become **Paused** and the user is notified ("device lost"). The idle timer keeps running — an abandoned paused session is eventually closed by timeout.
- When the device comes back online, its Paused sessions **auto-resume**: to InProcess if an unfinished process exists, otherwise to Connected; the user is notified ("device reconnected").
- A user cannot close a session while an unfinished process is running — the process must be stopped first. Exception: a **Paused** session (device offline) may be closed manually.
- Closing a session finalizes all unfinished processes (records the end, settles the money, releases the device) and notifies the device so it can clear its screen/QR.
- If the session used hold invoices, closing moves it to **Settling**: capture/refund targets are queued and a background watcher completes the money movement, after which the session is finally *Closed* and the user is notified ("settlement finished, unused funds returned"). Without hold invoices the session closes immediately.
- Session close reasons: *UserClosed*, *Timeout*, *DeviceLost*.
- A user can view only their own sessions (current, by id, history). Session history is paginated and can be filtered by a date period.
- Multiple screens (e.g. tablet and phone) can watch the same session in real time; balance/process/device-status changes are broadcast to all of them and to the device.

## Processes (product delivery)

- A session can have **only one unfinished process at a time**; starting a second is rejected.
- A process can start only when: the session belongs to the caller, its status is Connected or InProcess, a device is attached, the chosen product exists, is active, and belongs to that device.
- **Funding is hold-only ("no hold = no fuel")**: a process cannot start unless the session has a positive confirmed hold balance. There is no fallback to the internal account balance.
- The maximum deliverable amount is limited by the available hold balance divided by the product's unit price. A requested amount above the affordable maximum is silently reduced to that maximum. If the affordable amount is zero, the start is rejected ("hold balance insufficient — create a new invoice").
- Starting a process locks the device to the user; if another user holds the lock, the start is rejected ("device busy").
- **Stop is not instantaneous**: the stop command is sent to the device, the client sees a transient "Stopping" state, and the process is finalized only when the device confirms the final delivered amount. Liquids/energy delivered during inertia are included.
- **Pause follows the same confirm pattern**: a "Pausing" transient state, and the device's confirmation records the final amount and sets the process to Paused. Pausing an already-paused process is acknowledged idempotently. No money is taken on pause — the process may be resumed.
- Resume is allowed only for a Paused process; it immediately returns the process to InProcess and instructs the device to continue.
- **Telemetry**: the device periodically reports the cumulative delivered amount. Reports are accepted only if the session token and the device serial both match the process. Out-of-order or duplicate reports are ignored. Every accepted report refreshes the session idle timer and is broadcast to watchers with the running cost.
- The delivered amount can only grow — a report lower than the recorded amount never decreases it.
- **Auto-completion**: when the delivered amount reaches the requested limit, the process ends automatically with reason *Completed*, money is settled, a stop command is sent, and the device lock is released.
- **Device-finished report**: when the device itself reports completion, the process is finalized with the device's end reason. If the process is already ended, the report is acknowledged without settling money again (idempotent).
- **Watchdog**: a process that stays unconfirmed for **60 seconds** after a stop/pause command (or whose telemetry stalls) is force-finalized with reason *DeviceError*, using the last known delivered amount, and money is settled.
- Money for a process is settled **exactly once**, regardless of how many finalization paths race (device confirmation, watchdog, session close).
- Process end reasons: *Completed*, *UserStopped*, *DeviceError*, *OutOfResource*.
- Only the session owner can control (start/stop/pause/resume) its processes.

## Hold invoices (Payme pre-authorization)

- A hold invoice blocks a chosen amount of the customer's money at the payment provider before dispensing.
- Creating an invoice requires: a positive amount; a session that belongs to the caller and is Connected or InProcess (Paused and Settling are rejected with specific explanations); an active payment context; and the merchant having Payme credentials configured — there is **no** shared/platform fallback: an unconfigured merchant means rejection ("contact the administrator").
- An **idempotency key is mandatory** for invoice creation; a repeated request with the same key returns the existing invoice instead of creating a new one.
- At most **10 active invoices** per session.
- After creation the customer must confirm the payment in the Payme app; the system polls the provider until the money is held.
- When the money is held, the invoice amount is added to the session's hold balance and every watcher (phones, tablet, device screen) is notified in real time.
- An invoice unpaid within **30 minutes** is cancelled at the provider and marked *Expired*.
- Optionally an SMS invoice can be sent to a phone number; an SMS failure does not fail the invoice (the customer can still pay in-app).
- **User cancellation** rules:
  - An invoice whose money has been even partially consumed **cannot** be cancelled — the user must finish the session instead.
  - A paid-but-unused invoice is queued for refund; its amount is removed from the session's hold balance immediately.
  - An unpaid invoice is cancelled at the provider immediately.
  - Any other state rejects cancellation with the current status named.
  - Only the invoice's owner can cancel it.
- **Consumption is FIFO**: dispensing cost is charged against invoices in the order they were created; an invoice becomes *PartiallyConsumed* / *FullyConsumed* accordingly. If the hold balance does not fully cover the cost, the shortage is recorded as a warning.
- **Session settlement** assigns each invoice a target:
  - Paid and completely unused → full refund.
  - Paid and (partially) used → capture of exactly the consumed amount; the remainder is automatically released by the provider.
  - Unpaid at settlement time → cancel (if it turns out to have been paid in the race window, the money is returned).
  - Never paid, never sent → cancelled outright.
- The settlement executor retries transient provider failures with increasing delays, up to **8 attempts**; after that the invoice is marked *Failed* and requires operator intervention.
- "Already processed" answers from the provider are reconciled by re-checking the receipt's real state, so retries are idempotent and money is never captured or refunded twice.
- If an invoice queued for refund turns out to have consumed money (race between cancel and dispensing), it is auto-corrected to a capture so merchant money is not lost.
- Invoice status can only move along an explicit allowed-transition table; illegal jumps (e.g. Refunded → Hold) are impossible. Terminal states: *Captured*, *Refunded*, *Cancelled*, *Expired*.
- Every provider interaction and operator action on an invoice is recorded as an audit step with request/response payloads. Repeated "still waiting" poll results are not spammed into the audit — a step is written only when the provider state actually changes.
- The user can view the invoice list of their own session and the session's payment balance (held / consumed / available).

## Hold invoice operator management

- Operators can list hold invoices with filters (merchant, session, status, date period). A merchant-scoped operator sees only their merchant's invoices; listing without a resolvable merchant scope yields an empty list.
- Operators can view a single invoice and its full audit trail.
- **Force capture**: amount defaults to the consumed amount; must be positive; allowed only from states the transition table permits.
- **Force refund**: queued for the watcher; if a held, unconsumed amount was counted in the session balance it is removed.
- **Force cancel**: allowed only where the state machine permits; a paid (Hold) invoice cannot be cancelled — refund must be used.
- **Retry** is available only for *Failed* invoices; the retry target is derived automatically: capture if money was consumed, refund otherwise.
- Every operator action requires a reason and is written to the audit trail together with the operator's identity.
- Capture, refund and retry are Manage-only capabilities.

## QR top-up payments (direct Payme payment)

- A customer can top up either their **own** balance or their **organization's** balance:
  - Personal top-up is allowed only for Natural users.
  - Organization top-up is allowed only for Corporate users attached to an organization; the organization is always taken from the caller's own profile, never from the request (prevents topping up or viewing another organization).
- The amount must be positive and a payment token must be present.
- A blocked user cannot pay.
- The payment succeeds only when the provider confirms the final "paid" state; any other provider outcome fails the transaction with the provider's message.
- On success the balance is credited and all of the user's devices/screens are notified in real time.
- If the provider confirmed the payment but the internal balance credit failed, the transaction is marked failed, flagged for **manual reconciliation**, and the user is told to contact an operator.
- An idempotency key replays the earlier result instead of charging twice.
- A device can also submit a QR payment on behalf of the session's user: it is rejected if the session is missing, closed, or settling (a Paused session still accepts a top-up); the result is pushed back to the device and to the user's phones; duplicate submissions are de-duplicated by a client reference.
- Every payment records an audit trail of steps (initiated, validated, receipt created, pay requested/responded, balance credited / failed / reversed).

## Payment administration

- Administrators can list all payment transactions with status and date-period filters, view one transaction with its full audit steps.
- **Reversal** is allowed only for *Succeeded* transactions and requires a reason. The reversed amount is subtracted from the payee's balance (personal or organizational). A reversal may drive the balance negative — this is recorded as a warning, not blocked. The transaction becomes *Reversed* and the action is audited with the administrator's identity.

## Balance rules

- A Natural user's top-up credits their personal balance; a Corporate user's top-up credits their organization's balance (the system routes automatically).
- Admin top-up requires a positive amount and an existing user.
- Balance mutations are atomic — concurrent operations can never double-charge or lose money for the same process.
- Balance reading: Natural → own balance; Corporate → organization balance (0 if no organization data).

## Merchants

- A merchant's phone number must be unique among merchants.
- Only merchant phone and active flag can be edited after creation.
- Payme credentials (cashbox id + key) are **write-only**: both fields are required; after saving, only the last 4 characters of the key are ever shown. A per-merchant enable flag controls whether Payme is active.
- Merchant listing: Manage sees all; a merchant user sees only their own merchant.
- Registering a merchant, deleting one, and listing all merchants are Manage-only capabilities.

## Organizations

- An organization is created with a name, tax id, address, phone, an initial balance, and an active flag.
- After creation only address, phone and active flag can be edited.
- Organization management is Manage-only; a corporate user can view only their own organization.

## Stations

- A station must belong to an existing, **active** merchant.
- Name, textual address and geo-coordinates are mandatory. Latitude must be within −90…90, longitude within −180…180, and they must always be provided **as a pair**.
- Coordinates can never be cleared, only replaced (again, only as a pair).
- A merchant staff member may create stations only for their own merchant; creating for another merchant requires the global merchant-registration capability.
- Read/update/delete are limited to the caller's merchant scope.

## Devices

- A device must be registered to an existing, **active** station.
- The device serial number is globally unique — even a deactivated or soft-deleted device keeps its serial reserved; re-using it is rejected.
- Merchant staff may register devices only at their own merchant's stations; otherwise the global registration capability is required.
- Editable after registration: model, firmware version, online flag, active flag.
- A device authenticates itself by serial number and receives its secret token; unknown or inactive devices are rejected.
- Device messaging is secured behaviorally: every message must be authenticated with the device's secret; messages older than 60 seconds (or more than 5 seconds from the future) are ignored; repeated/replayed messages are rejected via strictly-increasing message counters.
- Message counters are **never reset automatically** — not on session connect, not on server restart. The only reset path is an expert admin action (Manage-only), intended solely for after a device memory re-flash; without it a re-flashed device's messages would all be rejected as replays.

## Products

- A product must belong to an existing, **active** device.
- Product type must be compatible with the device type:
  - Fuel dispenser → petrol, diesel, methane, propane.
  - Wash box → water, foam, wax.
  - Charger → electricity.
  - Water dispenser → purified / cold / hot water.
  - Vacuum cleaner → vacuum service.
  - Vending machine → coffee, tea, cold drink, snack.
- An incompatible type is rejected with the allowed list named in the message.
- Merchant staff can create products only on devices of their own merchant's active stations.
- Editable after creation: name, description, price, active flag. Type and unit are fixed.
- Customers are ever offered **only active** products; inactive products cannot start a process.
- Units of measure: liter, cubic meter, kilowatt-hour, second, piece.

## Platform user administration

- A platform user is created with a subtype (Manage or Merchant), a phone (unique among platform users), and a role.
- A Merchant-type user requires a merchant id; the merchant must exist and be active; the assigned role must belong to that same merchant.
- A Manage-type user must receive a global (non-merchant) role.
- A merchant operator may create only Merchant-type users, only in their own merchant, with their own merchant's roles.
- A new platform user has no password; an administrator sets it once ("set password" is rejected if already set), which activates the account. Password reset afterwards is allowed only for activated users.
- Block / unblock validate the current state (blocking an already-blocked user is rejected, and vice versa).
- Deletion is a soft delete.
- A merchant operator can manage only Merchant-type users of their own merchant.

## Customer user administration

- A corporate user is created into a specific organization by someone with access to that organization (Manage or the organization's own admin).
- The organization must exist and be active; the role must belong to the same organization; the phone must be unique among customer users.
- A corporate user is created "OTP pre-verified"; an administrator sets the password once, which activates the account for login.
- Corporate sub-user listing, block/unblock, delete are limited to the organization scope.
- Natural (individual) users cannot be created by administrators (they only self-register). Administrators may **view, search, block/unblock and delete** them — and these actions on natural users are possible **only for Manage** (they belong to no organization).
- The global list of all natural users is Manage-only.

## Roles

- Platform roles are either global (Manage) or bound to one merchant. Merchant staff create/edit/delete only their own merchant's roles; Manage can manage all and may create roles for any existing merchant.
- Customer roles are bound to one organization. Manage must state the organization explicitly; a corporate admin's roles are always created in their own organization. Global "Natural" roles exist (for individuals); the default one is auto-assigned at self-registration.
- A role has a name, description, active flag, and a permission set. Updating replaces the permission set; removed permissions are detached, new ones attached.
- Every attached permission is validated against the role kind's allowed catalog (see Permissions above).
- Reading a role, its permissions, updating and deleting are scope-checked.
- The "allowed permissions" catalog for a role kind can be queried; requesting the Manage catalog requires Manage rights.

## Reports

- Three report scopes exist:
  - **Personal usage** — the caller sees only their own consumption (works identically for Natural and Corporate users).
  - **Organization usage** — all processes performed by the organization's employees.
  - **Merchant sales** — services delivered at the merchant's stations; optionally narrowed to one station (omitted station = all stations).
- The reporting period (`from`/`to`) is mandatory; `to` must be after `from`; the period may not exceed **366 days**.
- Reports are available as a paginated list or as a full Excel file (all rows of the period, no pagination).
- Report rows contain: start/end time, product, unit, delivered and requested amounts, unit price, total cost, status, end reason, station, device serial — plus the user's phone in organization and merchant reports (not in the personal report).
- Export file names encode the scope and the period.

## Bootstrap and device preview

- A single "bootstrap" call at app start returns the user profile, the active session (or none), and current server time — designed for resuming after the app restarts.
- Before connecting, the app can look up a device by the serial number from its QR code and receive the device's info and the product types allowed for its device type. Only known, active devices are returned. This works for any logged-in customer without a special permission.

## Data lifecycle

- Nothing is ever physically deleted: deletion is always a soft delete, and soft-deleted records disappear from every query and listing.
- All timestamps are recorded in local server time.

## Idempotency and retry safety

- Session creation and process start accept an idempotency key; hold-invoice creation **requires** one; QR top-up recommends one.
- A duplicated request with the same key replays the original successful response (marked as a replay) for 24 hours.
- Two concurrent requests with the same key: only one executes; the other is rejected as a conflict.
- A failed (non-successful) response does not consume the key — the client may fix the input and retry.

---

# User Actions

## Customer (mobile app)

| Action | System behavior | Validation | Possible errors | Restrictions |
|---|---|---|---|---|
| Register | Creates an unverified account, sends OTP, returns user id. Existing accounts get step-appropriate guidance or an OTP re-send. | Phone canonical format; email required and must contain `@`; device identifier required. | Invalid phone/email format. | Individuals only; no duplicate accounts. |
| Verify OTP | Marks OTP verified, directs to password setup. | User id positive; OTP exactly 6 characters. | Wrong/expired OTP; user not found; already verified (informational). | Max 5 attempts; 3-minute code lifetime. |
| Set password | Activates the account and signs the user in (tokens returned). | Password ≥ 6 chars; OTP must be verified. | OTP not verified; password already set; user not found. | One-time action. |
| Login | Returns access + refresh tokens; records login time. | Phone format; password non-empty. | Wrong credentials; incomplete registration (with step hint); not verified; blocked. | — |
| Refresh token | Revokes old refresh token, issues a new token pair. | Token must exist and belong to the customer group. | Invalid/expired token; user blocked; user deleted. | Single-use tokens. |
| Reset password | OTP-driven 3-step flow; sets a new password. | Only fully registered users; OTP verified before set. | Unknown phone; not fully registered; wrong OTP. | OTP verification consumed on success. |
| View profile / balance | Returns own profile; balance resolved by user type (own vs organization). | — | User not found. | Own data only. |
| Update profile | Updates only provided fields (email, device identifier). | — | User not found. | Phone number cannot be changed here. |
| Bootstrap | Returns profile + active session (nullable) + server time. | — | User not found. | — |
| Create session | Issues a pending QR token (30 min), idempotent while pending. | No active session; user not blocked. | Active session exists; blocked; user not found. | One active session per user. |
| Heartbeat | Refreshes the 30-minute idle timer; returns next deadline. | Session exists, owned, not closed. | Not found; not owned; closed; state changed. | — |
| View current session / by id / history | Returns own session data; history is paginated and date-filterable. | Ownership. | Not found; not owned. | Own sessions only. |
| Close session | Finalizes open processes, settles money, closes or moves to settlement; notifies device and watchers. | Owned; not already closed; not settling; no active process (unless Paused). | Not found; not owned; already closed; settling in progress; active process running. | Paused sessions may be closed with an active process. |
| Start process | Creates the delivery, locks the device, commands it to start, notifies watchers. | Session Connected/InProcess; device attached; product active and on this device; positive confirmed hold balance; no other unfinished process. | Session/product not found or inactive; wrong device; no hold; hold insufficient; process already running; device busy. | Amount capped by affordable hold; hold-only funding. |
| Stop process | Sends stop; waits for device confirmation; watchdog backstop after 60 s. | Owned; not already ended. | Not found; not owned; already ended. | Final amount comes from the device. |
| Pause process | Sends pause; device confirmation records amount and pauses. | Owned; status Started/InProcess. | Wrong state; already ended. | No money taken while paused. |
| Resume process | Sends resume; process returns to InProcess. | Owned; status Paused. | Not paused. | — |
| Create hold invoice | Creates provider hold receipt; polls until customer pays; adds to session hold balance. | Positive amount; session Connected/InProcess; owned; merchant Payme configured; ≤ 10 active invoices; idempotency key required. | Session paused/settling; payment context inactive; limit exceeded; merchant unconfigured; provider failure. | Pays in Payme app; 30-minute payment TTL. |
| Cancel hold invoice | Refunds a paid unused invoice or cancels an unpaid one. | Owned; zero consumption. | Partially used; state not cancellable; state changed concurrently. | Used invoices settle at session close instead. |
| View session invoices / payment balance | Lists invoices (FIFO order) and held/consumed/available amounts. | Session owned. | Not found; not owned. | Own session only. |
| QR top-up | Charges Payme, credits own or organization balance, notifies all user's screens. | Positive amount; token present; payee type matches user type; not blocked. | Provider declined; provider unreachable; paid-but-not-credited (manual reconciliation). | Self: Natural only; Organization: Corporate only. |
| View payment history | Own transactions; corporate users may also view their organization's. | Corporate + attached organization for org history. | Not a corporate user / no organization. | Organization is taken from the caller's profile, never from the request. |
| Personal usage report / Excel | Paginated rows or a streamed Excel file for the chosen period. | Period required; `to` > `from`; ≤ 366 days. | Invalid period. | Own consumption only. |
| Device preview (QR scan) | Returns device info and allowed product types by serial. | Device known and active. | Device not found/inactive. | Login required, no special permission. |

## Corporate administrator (customer side)

| Action | System behavior | Validation | Possible errors | Restrictions |
|---|---|---|---|---|
| Create corporate user | Creates an employee account in the organization (OTP pre-verified). | Org accessible, existing, active; unique phone; role belongs to the org. | Duplicate phone; org/role not found; org inactive; wrong-org role. | Own organization only (Manage: any). |
| Set employee password | One-time activation of the account. | Not already set. | Already set; out of scope. | — |
| List / view / block / unblock / delete employees | Standard management within the organization. | Scope check per user. | Not found; out of scope. | Natural users manageable only by Manage. |
| Manage corporate roles | Create/update/delete roles with permission sets. | Permissions must be in the corporate catalog. | Disallowed permission named in the error. | Own organization only. |
| Organization report / Excel | Consumption of all organization employees. | Period rules as above. | Invalid period. | — |
| Organization transactions | Payment history of own organization. | Corporate + organization attached. | — | — |

## Platform administrator (Manage) and merchant staff

| Action | System behavior | Validation | Possible errors | Restrictions |
|---|---|---|---|---|
| Platform login / refresh | Tokens for the platform surface. | Password set; verified; not blocked. | Wrong credentials; no password ("contact administrator"); blocked. | No self-registration. |
| Manage merchants | Create/list/view/update/delete; set Payme credentials. | Unique phone; cashbox+key both required. | Duplicate phone; not found; out of scope. | Register/delete/list-all and credentials: Manage-only. Key shown masked afterwards. |
| Manage organizations | Full CRUD with initial balance. | Scope. | Not found; out of scope. | Manage-only catalog. |
| Manage stations | CRUD with mandatory coordinates. | Merchant active; lat/lng ranges; pair rule. | Merchant not found/inactive; out of scope. | Merchant staff: own merchant only. |
| Manage devices | Register/list/view/update/delete. | Station active; serial globally unique. | Duplicate serial; station not found/inactive; out of scope. | Merchant staff: own merchant only. |
| Reset device message counters | Zeroes the device's replay counters. | Device in scope. | Not found; no permission. | Expert, Manage-only; only after device memory re-flash. |
| Manage products | CRUD; allowed-types lookup per device type. | Device active; type compatible with device type. | Incompatible type (with allowed list); not found; out of scope. | Merchant staff: own merchant only. |
| Manage platform users | Create/list/view/set-password/reset-password/block/unblock/delete. | Unique phone; role-merchant consistency; merchant active. | Duplicate; role/merchant not found; wrong role scope. | Merchant operator: only Merchant-type users of own merchant. |
| Manage platform roles | CRUD with permission catalogs per role kind. | Permission allowed for kind. | Disallowed permission named. | Merchant staff: own merchant roles only. |
| View natural users | Global list (search/sort/paginate), view, block/unblock, delete. | Manage scope. | Out of scope. | List is Manage-only; no creation path. |
| Admin balance top-up | Credits user's (or their organization's) balance. | Positive amount; user exists. | Invalid amount; user not found. | Manage-only permission. |
| Payment audit | List all transactions (status/date filters), view with steps, reverse. | Reverse: Succeeded only, reason required. | Wrong state; missing reason; not found. | Reverse & global list: Manage-only. Reversal may create a negative balance (logged). |
| Hold invoice operations | List/view/steps; force capture/refund/cancel; retry failed. | State-machine transition must be legal; capture amount > 0; retry only from Failed. | Illegal state; state changed concurrently; out of scope. | Capture/refund/retry: Manage-only; every action requires a reason and is audited. |
| Merchant sales report / Excel | Sales of a merchant's stations, optional single-station filter. | Period rules. | Invalid period. | — |

---

# Business Scenarios

## 1. New customer onboarding

1. Customer submits phone, email and device identifier.
2. System normalizes and validates the phone, creates an unverified account, assigns the default individual role, sends an OTP.
3. Customer submits the 6-digit OTP within 3 minutes (max 5 attempts).
4. Customer sets a password (≥ 6 chars) within 10 minutes of verification.
5. Account becomes verified; tokens are issued; the customer is signed in.
- If any step is repeated, the system responds idempotently with guidance to the next required step.

## 2. Getting fuel/service at a device (happy path)

1. Customer opens the app; **Bootstrap** restores profile and any active session.
2. Customer creates a session → receives a QR token (valid 30 min).
3. Customer shows the QR to the device; the device sends the token to the server.
4. Server verifies token, device and single-session rule → session *Connected*; a payment context is opened; the app receives the device's active products in real time.
5. Customer creates a **hold invoice** for a chosen amount and confirms payment in the Payme app.
6. The watcher detects the paid hold; the session's hold balance increases; all screens and the device update in real time.
7. Customer starts a **process** for a product; the deliverable amount is capped by the affordable hold; the device is locked to the customer and starts dispensing.
8. The device streams telemetry; the app shows delivered amount and running cost live; each report keeps the session alive.
9. Delivery ends by reaching the limit (auto-complete), by user stop (device confirms the final amount), or by device-side finish.
10. The cost is consumed from the hold invoices FIFO, exactly once.
11. Customer closes the session → it enters *Settling*: consumed amounts are captured, unused amounts refunded.
12. When all invoices are settled the session closes; the customer receives a push: settlement finished, unused funds returned.

## 3. Stop with inertia confirmation

1. Customer presses Stop.
2. The system sends the stop command but does **not** finalize the process; the app shows "Stopping" and disables controls.
3. The device stops the flow (inertia included) and reports the final delivered amount.
4. The system finalizes the process with the device's number, settles money, releases the device.
5. If no confirmation arrives within 60 seconds, the watchdog finalizes with the last known amount and reason *DeviceError*.

## 4. Device connection lost mid-session

1. The device goes silent for 90 seconds → marked offline.
2. Live sessions on it become *Paused*; the customer gets a push "device lost, session paused".
3. Running processes are not force-ended immediately — the 60-second watchdog ends them with the last real delivered amount (so captures are not guessed).
4. If the device reconnects, paused sessions auto-resume and the customer is notified.
5. If it never reconnects, the idle timeout (30 min) closes the session; hold invoices settle as usual (capture used / refund unused).
6. A customer may also close a *Paused* session manually without stopping the (unreachable) process first.

## 5. Hold invoice that is never paid

1. Customer creates an invoice but does not confirm payment in Payme.
2. The watcher polls; after 30 minutes the receipt is cancelled at the provider and the invoice becomes *Expired*.
3. The session's balance never included the amount; nothing is refunded because nothing was held.

## 6. Cancelling an unused invoice

1. Customer cancels a paid, completely unused invoice.
2. The amount immediately leaves the session's available balance; a refund is queued.
3. The watcher cancels the receipt at the provider; the invoice becomes *Refunded*.
4. If the invoice was even partially consumed, cancellation is refused — the money is settled at session close instead.

## 7. Failed settlement and operator recovery

1. A capture or refund keeps failing at the provider; after 8 attempts the invoice becomes *Failed*.
2. An operator inspects the invoice and its audit steps.
3. The operator retries: the system automatically chooses capture (if money was consumed) or refund, with a mandatory reason recorded.
4. The watcher executes the retried target; the invoice reaches a terminal state; the session can then close.

## 8. QR top-up initiated at the device

1. Customer scans a Payme QR at the device; the device forwards the payment request with the session token.
2. The system rejects it if the session is closed or settling (a paused session still accepts top-ups).
3. The payment is processed against the session owner's personal balance; duplicates are filtered by client reference.
4. The result is shown on the device and pushed to the customer's phones.

## 9. Merchant onboarding

1. Manage registers a merchant (unique phone).
2. Manage (or the merchant's staff, within scope) creates stations with mandatory coordinates, registers devices with unique serials, and defines products compatible with each device type.
3. Manage sets the merchant's Payme credentials (write-only) and enables Payme.
4. Manage creates merchant roles and merchant staff users bound to merchant roles, then activates them by setting one-time passwords.
5. From then on, merchant staff manage their own stations/devices/products/operators and read their own sales reports.

## 10. Corporate client onboarding

1. Manage creates an organization (with a balance) and its roles.
2. Manage or the organization's admin creates corporate employees, assigns organization roles, sets one-time passwords.
3. Employees log in as customers and consume services; all their spending draws on the organization's balance.
4. The organization admin tops up the organization balance via Payme QR and monitors the organization's usage report and transaction history.

## 11. Usage report with export

1. User selects a period (must be valid and ≤ 366 days).
2. System validates access scope (own data / own organization / own merchant).
3. The paginated view returns one page at a time; the export returns every row of the period as an Excel file.
4. An empty period returns an empty result, not an error.

---

# Validation Rules

## Format & required fields

- Phone: canonical `998` + 9 digits (12 digits total, no `+`); soft normalization of separators and international prefixes; 9-digit local numbers are not auto-completed.
- Email (registration): required, must contain `@`.
- Device identifier (registration): required.
- Password: minimum 6 characters.
- OTP: exactly 6 characters.
- User id in multi-step auth flows: positive.
- Merchant Payme credentials: cashbox id and key both required (trimmed).
- Station: name, address, latitude (−90…90) and longitude (−180…180) required; coordinates always as a pair.
- Reverse payment: reason required.
- Operator hold actions: reason required.
- Hold invoice / top-up / admin top-up amount: strictly positive.
- Telemetry: process id positive, delivered total non-negative.

## Temporal rules

- OTP lifetime 3 minutes; verified-OTP window 10 minutes.
- Access token 15 minutes; refresh token 7 days (rotating, single-use).
- Pending session QR: 30 minutes.
- Session idle timeout: 30 minutes (sliding).
- Device offline threshold: 90 seconds.
- Stop/pause confirmation watchdog: 60 seconds.
- Hold invoice payment TTL: 30 minutes.
- Report period: `from` and `to` required, `to` after `from`, at most 366 days.
- Device message timestamps: rejected if older than 60 seconds or more than 5 seconds in the future.

## Duplicate prevention

- One account per phone per user group; one merchant per phone; one device per serial (including inactive/deleted).
- One active session per user; one unfinished process per session.
- Max 10 active hold invoices per session; one payment context per session (idempotent creation).
- Idempotency keys de-duplicate session creation, process start, hold-invoice creation and QR payments (24-hour replay window; concurrent duplicate → conflict).
- OTP attempt cap (5) prevents brute force.
- Message replay prevention via monotonic counters and timestamp window.

## Consistency & state checks

- Product must belong to the session's device; product type must match device type.
- Role must belong to the target merchant/organization; permission must be allowed for the role kind.
- Session token and device serial must both match the process for any device report.
- Invoice status transitions only along the allowed-transition table.
- Payment reversal only from *Succeeded*; hold retry only from *Failed*.
- Delivered amount never decreases.
- Block/unblock (platform users) verify the current state before switching.
- Set-password is a one-time action; reset-password requires an activated account.

---

# Permissions

## Permission model

- Permissions are named strings attached to roles; a user's effective permissions travel in their token.
- Manage roles: unrestricted (any permission).
- Merchant roles: platform permissions minus the Manage-only set.
- Natural roles: self-service set only.
- Corporate roles: natural set + organization-level payment/report/user-management permissions.

## Manage-only capabilities

- Organization management (create/list/view/update/delete).
- Merchant registration, deletion, global listing, Payme credential setup.
- Admin balance top-up.
- Global payment listing and payment reversal.
- Hold invoice capture, refund, retry.
- Global list of natural (individual) users; block/unblock/delete of natural users.
- Device message-counter reset.
- Creating Manage-kind roles / viewing the Manage permission catalog.

## What each actor may do (summary)

- **Natural customer**: sessions, processes, own profile/balance, own reports, own top-ups and transaction history, hold invoices of own sessions, device preview.
- **Corporate customer**: everything a natural customer can, plus organization top-up, organization transaction history, organization reports, and (if the role includes it) managing the organization's users and roles.
- **Merchant staff**: manage own merchant's stations, devices, products, operators (Merchant-type only), merchant roles, view own merchant's hold invoices and sales reports.
- **Manage**: everything, everywhere.
- Some profile endpoints (view own profile, own balance, update own profile) require only a valid login, no named permission. The device-preview endpoint likewise. Authentication endpoints require no login.

---

# Filters

- **Universal list filters** (`field:value` items): exact-match filtering on boolean / enum / numeric / text columns; multiple items combine with AND; invalid field names or unparseable values are silently ignored; enum values accepted by number or by name (case-insensitive).
- **Session history**: date period (`from`, `to`).
- **Payment history (user/org/admin)**: payment status; admin additionally date period.
- **Hold invoice operator list**: merchant, session, status, date period; merchant-scoped operators are always forced to their own merchant.
- **Reports**: mandatory date period; merchant report optionally narrowed to a single station.
- **Stations/devices/products by parent**: list by merchant, by station, by device (scope-checked).
- **Scope filters are implicit**: merchant staff and corporate users always see pre-filtered data of their own merchant/organization.

---

# Search Logic

- A single `Search` text applies a **case-insensitive substring match across all text fields** of the listed entity simultaneously (OR across fields).
- Fields marked as sensitive are excluded from search.
- Search wildcards in user input are escaped — a literal `%` or `_` is searched literally.
- Empty/whitespace search means no filtering.
- Search combines with equality filters (AND) and sorting.

---

# Sorting

- Lists sort by **exactly one column** chosen by name (case-insensitive), ascending or descending.
- An empty or unknown column name falls back to the default: **Id ascending**.
- When sorting by any non-Id column, a hidden ascending-Id tiebreaker guarantees stable pagination (no rows duplicated or lost across pages).

---

# Statuses

## Session
| Status | Meaning |
|---|---|
| Created | Session record exists, device not yet connected. |
| Connected | Device linked via QR; ready to start a process. |
| InProcess | A delivery process is running. |
| Paused | Device connection lost; session frozen, auto-resumes on reconnect. |
| Settling | Close requested; hold invoices are being captured/refunded. |
| Closed | Finished (terminal). |

Close reasons: **UserClosed**, **Timeout** (30-min inactivity), **DeviceLost**.

## Process
| Status | Meaning |
|---|---|
| Started | Start command sent, no telemetry yet. |
| InProcess | Delivering (telemetry flowing). |
| Paused | Confirmed paused by the device; resumable. |
| Ended | Finished (terminal). |

End reasons: **Completed** (limit reached), **UserStopped**, **DeviceError** (incl. watchdog), **OutOfResource**.

Transient client-visible states (not stored): **Stopping**, **Pausing** — command sent, awaiting device confirmation.

## Payment context (per session)
| Status | Meaning |
|---|---|
| Active | New invoices may be created; consumption allowed. |
| Settling | Session closing; new invoices blocked. |
| Settled | All invoices terminal; parent session closed. |

## Hold invoice
| Status | Meaning |
|---|---|
| Created | Record exists; provider receipt not yet created. |
| WaitingForConfirmation | Receipt created; awaiting customer payment. |
| Hold | Customer paid; money blocked at provider; added to session balance. |
| PartiallyConsumed | Part of the held amount used by dispensing. |
| FullyConsumed | Whole held amount used. |
| CapturePending | Capture of the consumed amount queued. |
| Captured | Consumed amount taken; remainder auto-released (terminal). |
| RefundPending | Full refund queued. |
| Refunded | Money returned (terminal). |
| Cancelled | Cancelled before payment (terminal). |
| Expired | Customer did not pay within 30 minutes (terminal). |
| Failed | Permanent error or retry limit; awaits operator (recoverable only via operator retry). |

## Payment transaction (QR top-up)
| Status | Meaning |
|---|---|
| Pending | Created, validation passed. |
| ReceiptCreated | Provider receipt exists. |
| Paying | Payment in progress. |
| Succeeded | Paid and balance credited. |
| Failed | Any failure (incl. paid-but-not-credited reconciliation case). |
| Cancelled | Cancelled. |
| Reversed | Manually reversed by an administrator. |

## Device connectivity
**Online** (communicating), **Offline** (silent, no live session affected), **Lost** (went silent while sessions were live).

## Account flags
**Verified** (registration complete / password set), **OTP verified** (mid-registration), **Blocked** (login and operations denied), **Deleted** (soft; invisible and unusable).

---

# Limits

- **1** active session per user.
- **1** unfinished process per session.
- **1** role per user.
- **10** active hold invoices per session (configurable default).
- **30** requests per minute per IP on authentication endpoints (configurable default).
- **5** OTP verification attempts per code.
- **3 minutes** OTP validity; **10 minutes** to use a verified OTP.
- **6** characters minimum password length; OTP exactly **6** digits.
- **15 minutes** access-token life; **7 days** refresh-token life.
- **30 minutes** pending-QR validity, session idle timeout, and hold-invoice payment TTL.
- **90 seconds** device silence before offline/pause handling.
- **60 seconds** stop/pause confirmation watchdog.
- **8** settlement attempts before an invoice fails to operator care.
- **366 days** maximum report period.
- **100** rows maximum per page (default 20); page number minimum 1.
- **24 hours** idempotency replay window; **30 seconds** concurrent-duplicate reservation.
- Delivered amount per process is capped by the affordable hold balance at start.
- Payme key is displayed with at most the **last 4** characters visible.
- Device message counters never reset automatically — one expert-only reset path exists.

---

# Error Conditions

## Authentication
- Unknown phone or wrong password at login (indistinguishable to the caller).
- Login attempted before completing registration (message names the missing step).
- Login of an unverified or blocked account.
- Platform login without an administrator-set password ("contact administrator").
- Wrong or expired OTP; OTP attempt limit exhausted.
- Password set attempted twice; password set before OTP verification.
- Password reset attempted by a not-fully-registered user.
- Invalid, expired, revoked, or wrong-group refresh token; refresh for a blocked or deleted account.
- Too many requests from one IP (retry after 60 seconds).

## Sessions & processes
- Creating a session while another is active.
- Device connect fails: no pending session, token mismatch, unknown device, or a racing active session.
- Acting on someone else's session/process ("not yours").
- Closing an already-closed session; closing during settlement; closing with a running process (non-paused sessions).
- Heartbeat to a closed session; heartbeat race ("state changed").
- Starting a process: session not connected; no device; product missing/inactive; product of another device; another process unfinished; no confirmed hold; hold insufficient; device locked by another user.
- Pausing a process in a non-pausable state; resuming a non-paused process; stopping an ended process.
- Telemetry/finish/pause reports with mismatched session token or device serial; duplicate or stale telemetry (ignored, not an error).

## Hold invoices & payments
- Invoice creation on a paused session ("no device connection"), on a settling session, with an inactive payment context, above the per-session limit, or for a merchant without Payme configured.
- Missing mandatory idempotency key on invoice creation.
- Cancelling a partially consumed invoice; cancelling from a non-cancellable state; concurrent state change during cancel.
- Provider communication failure at receipt creation (invoice marked failed).
- QR top-up: non-positive amount; empty payment token; blocked user; personal top-up by a corporate user; organization top-up by a natural user or without an organization; provider decline; provider state not "paid"; provider paid but balance credit failed (manual reconciliation, "contact operator").
- Device-originated payment on a missing/closed/settling session.
- Payment reversal of a non-succeeded transaction; reversal without a reason; payee entity mismatch during reversal.
- Operator force-capture with a non-positive amount; force actions from states the transition table forbids; cancel of a paid invoice (refund required); retry of a non-failed invoice; concurrent state changes ("try again").

## Administration
- Duplicate phone (customer, platform user, merchant); duplicate device serial.
- Creating entities under missing or inactive parents (merchant, station, device, organization).
- Role not found; role belonging to a different merchant/organization than the target user; global role given to a merchant user and vice versa.
- Attaching a permission not allowed for the role kind (permission named in the message).
- Merchant operator attempting: to create non-Merchant users, users for another merchant, stations/devices/products outside their merchant.
- Corporate admin acting outside their organization; non-Manage caller requesting the natural-users list or managing a natural user.
- Setting a password twice; resetting a password of a never-activated user; blocking an already-blocked platform user (and unblocking a non-blocked one).
- Any read/update/delete of an entity outside the caller's scope ("not in your scope").
- Payme credentials with a missing cashbox id or key.

## Reports
- Missing period; `to` not after `from`; period longer than 366 days.

## General
- Requested record not found (soft-deleted records are treated as non-existent).
- Empty result sets are returned as empty lists/pages — never as errors.
