# PulseFit Manager

The back office for PulseFit, a chain of three gyms (Downtown, Riverside, Northgate). Staff use it to run
memberships, class bookings, the front door and billing.

It's a demo app: there are no passwords (you pick a staff member to sign in as), payments are
simulated, and the sample data is rebuilt every night.

## What it does

- **Members** — join, change plan, freeze, cancel, promo codes, card on file, notes, full history.
- **Classes** — a weekly schedule across the three gyms, rosters, waitlists, attendance.
- **Front desk** — check-ins (with the reason when someone is turned away), guest passes, day passes.
- **Billing** — every charge, failed payments, fee waivers, refunds and refund approvals.
- **Plans & pricing**, **Reports**, **Staff**, **Audit log**, **Background jobs**.

## The house rules

All of them live in [`Domain/GymRules.cs`](src/PulseFit.Api/Domain/GymRules.cs).

| Area | Rule |
|---|---|
| Plans | Basic $29/mo: home gym only, no classes. Plus $49/mo: every gym, 4 classes a month. Premium $79/mo: every gym, unlimited classes, 2 guest passes a month. Yearly is 10× monthly, paid up front. |
| Classes | Booking opens 7 days ahead. Cancelling within 4 hours is a $10 late cancel; a no-show is $10. 3 no-shows in 30 days blocks booking for 7 days. A full class has a waitlist. |
| Freezing | Up to 2 months a calendar year, $5 a month while frozen. |
| Cancelling | 30 days' notice, billed as normal until then. Yearly plans ended early pay a $75 exit fee. |
| Plan changes | Upgrades start now and the difference is charged for the rest of the cycle; downgrades wait for the next billing date. |
| Payments | A failed payment gives 5 days' grace, then access is suspended until the card is updated. |
| Discounts | Students 20%, corporate 15%. Promo codes don't stack with either. |
| Staff | Front desk may waive one fee per member every 90 days. Managers may refund up to $100; bigger refunds need the owner. |

## Roles

| Role | Can |
|---|---|
| Front desk | Check-ins, guest passes, bookings, new members, fee waivers (limited) |
| Trainer | Their classes and attendance |
| Manager | Everything about memberships and money, refunds up to $100 |
| Owner | Everything, including prices, refund approvals and background jobs |

See [`Domain/Permissions.cs`](src/PulseFit.Api/Domain/Permissions.cs).

## Background jobs

They run every 15 minutes, in this order ([`Jobs/`](src/PulseFit.Api/Jobs)):

1. `billing-run` — bills memberships whose billing date has come (or the freeze fee while frozen), and applies downgrades.
2. `grace-period` — suspends members still unpaid 5 days after a failed payment.
3. `freeze-end` — reactivates memberships whose freeze is over.
4. `cancellations` — closes memberships whose notice period is over.
5. `no-shows` — charges no-show fees after a class ends and blocks repeat no-shows.

Every job writes to the audit log as `system:<job>`.

## Layout

```
src/PulseFit.Api/
  Domain/        entities, enums, rules, permissions
  Services/      the business logic: memberships, billing, bookings, check-ins, audit
  Jobs/          background jobs and their scheduler
  Queries/       read-only views for the screens
  Controllers/   thin HTTP endpoints
  Data/          EF Core (SQLite) and the demo seed
web/src/
  services/      one API client per area
  stores/        signed-in staff, reference data
  composables/   screen logic (loading, member actions)
  components/    layout/, ui/, members/, billing/
  views/         one per screen
```

## Run it

```bash
cd web && npm install && npm run build      # builds into the API's wwwroot
cd ../src/PulseFit.Api && dotnet run --urls http://localhost:5080
```

For front-end work, run the API as above and `npm run dev` in `web/` (Vite proxies `/api`).

Or with Docker: `docker build -t pulsefit . && docker run -p 8080:8080 pulsefit`.

### Settings

| Setting | What |
|---|---|
| `ConnectionStrings:Gym` | SQLite file. Default `pulsefit.db`. |
| `Gym:TimeZone` | The gyms' time zone. Default `America/New_York`. |
| `Jobs:IntervalMinutes` | How often the background jobs run. Default 15. |
| `Demo:ResetOnStartup`, `Demo:NightlyReset` | Rebuild the sample data on start / at 3am. |
| `RepoHelpdesk:WidgetScriptUrl`, `RepoHelpdesk:WidgetKey` | Shows the RepoHelpdesk chat widget. |
| `RepoHelpdesk:DashboardUrl`, `RepoHelpdesk:RepoUrl` | Links in the demo bar. |
