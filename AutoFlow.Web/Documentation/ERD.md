AutoFlow ERP & CRM System

Current Entity Relationship Documentation

This ERD documentation reflects the current AutoFlow implementation and the final 10-module project scope.

AutoFlow is an integrated ERP and CRM system for automotive repair shop operations. The system connects customer, vehicle, appointment, repair job, parts, supplier, billing/payment, CRM follow-up, service history, and reporting workflows.

1. Customer Management

Customer

CustomerId (PK)

FirstName

LastName

Phone

Email

Address

CreatedAt

UpdatedAt

Relationship

One Customer can have many Vehicles.

Customer 1 ──── * Vehicle

2. Vehicle Records

Vehicle

VehicleId (PK)

CustomerId (FK)

PlateNumber

VIN

Make

Model

Year

Color

Mileage

ImageUrl

IsArchived

CreatedAt

UpdatedAt

Relationship

Customer 1 ──── * Vehicle

A vehicle belongs to one customer and can have multiple appointments, repair jobs, and service records.

3. Service Appointment Scheduling

Appointment

AppointmentId (PK)

CustomerId (FK)

VehicleId (FK)

AppointmentDate

Reason

Status

Notes

CreatedAt

UpdatedAt

Relationships

Customer 1 ──── * Appointment
Vehicle 1 ──── * Appointment

Appointments are associated with the customer and vehicle involved in the scheduled service.

4. Repair Job Orders

JobOrder

JobOrderId (PK)

VehicleId (FK)

AppointmentId (FK, Optional)

JobOrderDate

Complaint / Description

Diagnosis

Notes

Status

LaborCost

TotalCost

CreatedAt

UpdatedAt

Relationship

Vehicle 1 ──── * JobOrder
Appointment 1 ──── 0..1 JobOrder

A Job Order represents the actual repair work performed on a vehicle.

When a job is completed, AutoFlow creates the related billing record and retains the completed service as service history.

5. Parts Inventory

Part

PartId (PK)

Name

PartNumber

UnitPrice

StockQuantity

SupplierId (FK)

CreatedAt

UpdatedAt

Relationship

Supplier 1 ──── * Part

Parts are stored in inventory and may be used in repair job orders.

6. Supplier Management

Supplier

SupplierId (PK)

Name

ContactPerson

Phone

Email

Address

City

Status

Notes

CreatedAt

UpdatedAt

Relationship

Supplier 1 ──── * Part

A supplier can provide multiple parts. The current implementation stores the supplier relationship directly through SupplierId in Part.

7. Billing & Payments

Billing

BillingId (PK)

JobOrderId (FK)

InvoiceNumber

IssuedAt

TotalAmount

AmountPaid

Status

CreatedAt

UpdatedAt

Payment

PaymentId (PK)

BillingId (FK)

AmountPaid

PaymentMethod

PaymentDate

Notes

CreatedAt

Relationships

JobOrder 1 ──── 0..1 Billing
Billing 1 ──── * Payment

A completed Job Order can generate one billing record. A billing record can have multiple payment records, allowing partial payments.

8. Customer Follow-ups CRM

CustomerFollowUp

FollowUpId (PK)

CustomerId (FK)

VehicleId (FK)

ServiceRecordId (FK, Optional)

FollowUpDate

FollowUpType

Subject

Notes

Status

NextFollowUpDate

CreatedAt

UpdatedAt

Relationships

Customer 1 ──── * CustomerFollowUp
Vehicle 1 ──── * CustomerFollowUp
ServiceRecord 1 ──── * CustomerFollowUp

Customer Follow-ups CRM stores follow-up activities related to customers and their vehicles. A follow-up may optionally reference a service record.

9. Service History

ServiceRecord

ServiceRecordId (PK)

VehicleId (FK)

ServiceDate

Mileage

Complaint

Diagnosis

Status

Notes

CreatedAt

UpdatedAt

Relationship

Vehicle 1 ──── * ServiceRecord

Service History stores service records for vehicles.

Completed repair jobs are retained as service history through the application workflow. The ServiceRecord table uses the documented service-history fields and is connected to Vehicle.

10. Reports

Reports are not stored as a separate transactional entity.

Reports are generated from existing AutoFlow database records.

Current report families include:

Daily Sales

Monthly Sales

Outstanding Payments

Completed Repairs

Active Repair Jobs

Appointment Statistics

Parts Inventory

Low Stock Parts

Reports use current database information from the corresponding modules rather than hardcoded operational records.

Main System Relationships

Customer
   │
   ├── 1 ──── * Vehicle
   │              │
   │              ├── 1 ──── * Appointment
   │              │
   │              ├── 1 ──── * JobOrder
   │              │              │
   │              │              ├── 1 ──── * JobOrderPart ──── * Part
   │              │              │
   │              │              └── 1 ──── 0..1 Billing
   │              │                                      │
   │              │                                      └── 1 ──── * Payment
   │              │
   │              └── 1 ──── * ServiceRecord
   │
   └── 1 ──── * CustomerFollowUp

Supplier
   │
   └── 1 ──── * Part

Repair Job Parts Relationship

Repair jobs and parts are connected through the existing JobOrderPart relationship.

JobOrder 1 ──── * JobOrderPart * ──── 1 Part

The application records the part used, quantity, and applicable price information for the repair job.

Inventory stock is adjusted when parts are added to or removed from a repair job.

Core Business Flow

Customer
    ↓
Vehicle
    ↓
Service Appointment
    ↓
Repair Job Order
    ↓
Diagnosis + Labor
    ↓
Parts Inventory
    ↓
Job Completed
    ├──────────────→ Billing
    │                   ↓
    │                Payment
    │
    └──────────────→ Service History
                            ↓
                     Customer Follow-up
                            ↓
                         Reports

The workflow connects the operational modules instead of treating them as independent pages.

Application Architecture

Frontend / Razor Views
        ↓
ASP.NET Core MVC
        ↓
Controllers
        ↓
Entity Framework Core
        ↓
PostgreSQL
        ↓
Supabase

Authentication and authorization are handled through Firebase Authentication and ASP.NET Core cookie/RBAC authorization.

Storage

Vehicle image/file references are stored by URL/path where applicable. The PostgreSQL database stores the corresponding application data and relationships.




const scripts = [...document.scripts];
scripts.find(s => s.textContent.includes("firebaseConfig"))?.textContent.includes('apiKey:\n                ""')