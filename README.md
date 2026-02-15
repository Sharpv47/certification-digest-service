# Certification Digest Service

Cloud-hosted ASP.NET Core API that tracks certification expirations and sends automated email digests using SendGrid.

Built with:
- .NET 8
- ASP.NET Core Minimal APIs
- Entity Framework Core (SQL Server)
- Azure SQL Database (Serverless)
- SendGrid Email API

---

## Overview

This service monitors certification records (e.g., TIPS, Food Service, Crowd Control, etc.) and sends automated digest notifications for certifications expiring within a configurable time window.

The system:
- Stores certification records in Azure SQL
- Generates expiring-record digests
- Deduplicates notifications by ISO week
- Sends email summaries via SendGrid
- Logs notification history in the database

This project demonstrates:
- Cloud database provisioning (Azure SQL Serverless)
- EF Core migrations and seeding
- Minimal API architecture
- External email integration (SendGrid)
- Idempotent scheduled-style logic
- Production-style resiliency (`EnableRetryOnFailure`)

---

## Architecture

Client (Swagger / Scheduled Trigger)
        ↓
ASP.NET Core Minimal API
        ↓
Entity Framework Core
        ↓
Azure SQL Database (Serverless)
        ↓
SendGrid Email API

---

## Key Endpoints

### Send Digest

POST /certifications/send-digest?days=60

Generates a digest for certifications expiring within the next X days.

Features:
- ISO week deduplication
- Database-backed notification log
- Plain text digest formatting
- Configurable expiration window

---

### Test Email

POST /email/test

Sends a basic SendGrid test email to validate integration.

---

## Example Email Output

PTCA Certification Digest (next 60 days)  
Generated: 2026-02-15T02:41:39Z UTC  

Total expiring: 2  

2026-02-25 - Bob Smith - FoodService  
2026-03-12 - John Doe - TIPS  

---

## Configuration

appsettings.Development.json:

```
{
  "ConnectionStrings": {
    "Default": "Server=your-server.database.windows.net;Database=certdb;User Id=...;Password=...;TrustServerCertificate=True;"
  },
  "SendGrid": {
    "ApiKey": "SG.xxxxxx",
    "FromEmail": "yourverified@email.com",
    "FromName": "Cert Tracker",
    "ToEmail": "your@email.com"
  }
}
```

---

## Database

Entities:
- CertificationRecord
- NotificationLog

Database is provisioned on:
Azure SQL Database (Serverless – General Purpose)

EF Core Migrations used for schema management.

---

## Future Enhancements

- Scheduled background job (Azure Function or Worker Service)
- Google Sheets ingestion integration
- SMS alerts (Twilio)
- Individual reminder emails
- Domain authentication for production email deliverability
- Frontend dashboard (Blazor or React)

---

## Why This Project Exists

This project was built to demonstrate:

- Real cloud database usage (not local SQL only)
- Production-style retry handling
- External service integration
- Idempotent notification logic
- Clean minimal API design

It reflects practical real-world business automation needs.

---

## Author

James Niewadomski  
Lead Software Engineer  
Long Island, NY  
