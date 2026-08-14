# AutoFlow ERP & CRM System

## System Purpose

AutoFlow is an ERP and CRM system designed for automotive repair shops.

The system manages:

- Customers
- Vehicles
- Service Appointments
- Repair Jobs
- Services
- Parts Inventory
- Suppliers
- Billing
- Payments
- Customer Follow-ups
- Service History
- Reports

---

# 1. Customers

Stores customer information.

### Customers

- CustomerId (PK)
- FirstName
- LastName
- Phone
- Email
- Address
- CreatedAt
- UpdatedAt

---

# 2. Vehicles

Stores vehicles owned by customers.

### Vehicles

- VehicleId (PK)
- CustomerId (FK)
- PlateNumber
- VIN
- Make
- Model
- Year
- Color
- Mileage
- ImageUrl
- CreatedAt
- UpdatedAt

### Relationship

One Customer can have many Vehicles.

Customer 1 ──── * Vehicle

---

# 3. Service Appointments

Manages scheduled customer visits.

### ServiceAppointments

- AppointmentId (PK)
- CustomerId (FK)
- VehicleId (FK)
- AppointmentDate
- Reason
- Status
- Notes
- CreatedAt
- UpdatedAt

### Relationship

Customer 1 ──── * ServiceAppointment

Vehicle 1 ──── * ServiceAppointment

---

# 4. Repair Jobs

Represents the actual repair work performed.

### RepairJobs

- RepairJobId (PK)
- VehicleId (FK)
- AppointmentId (FK, Optional)
- JobNumber
- Complaint
- Diagnosis
- Notes
- Status
- StartDate
- CompletionDate
- LaborCost
- CreatedAt
- UpdatedAt

### Relationship

Vehicle 1 ──── * RepairJob

Appointment 1 ──── 0..1 RepairJob

---

# 5. Services

Stores services offered by the repair shop.

### Services

- ServiceId (PK)
- ServiceName
- Description
- StandardPrice
- EstimatedDuration
- IsActive
- CreatedAt

---

# 6. Repair Job Services

Connects repair jobs with multiple services.

### RepairJobServices

- RepairJobServiceId (PK)
- RepairJobId (FK)
- ServiceId (FK)
- Quantity
- UnitPrice
- Subtotal

### Relationship

RepairJob * ──── * Service

through RepairJobServices

---

# 7. Parts Inventory

Stores parts available in the shop.

### Parts

- PartId (PK)
- PartNumber
- PartName
- Description
- QuantityInStock
- ReorderLevel
- CostPrice
- SellingPrice
- Location
- IsActive
- CreatedAt
- UpdatedAt

---

# 8. Repair Job Parts

Records which parts were used in a repair.

### RepairJobParts

- RepairJobPartId (PK)
- RepairJobId (FK)
- PartId (FK)
- Quantity
- UnitPrice
- Subtotal

### Relationship

RepairJob * ──── * Part

through RepairJobParts

---

# 9. Suppliers

Stores supplier information.

### Suppliers

- SupplierId (PK)
- SupplierName
- ContactPerson
- Phone
- Email
- Address
- CreatedAt
- UpdatedAt

---

# 10. Supplier Parts

Allows suppliers to provide multiple parts.

### SupplierParts

- SupplierPartId (PK)
- SupplierId (FK)
- PartId (FK)
- SupplierPrice
- LeadTimeDays

### Relationship

Supplier * ──── * Part

through SupplierParts

---

# 11. Invoices

Stores billing information.

### Invoices

- InvoiceId (PK)
- RepairJobId (FK)
- InvoiceNumber
- InvoiceDate
- Subtotal
- Tax
- Discount
- TotalAmount
- Status
- CreatedAt

### Relationship

RepairJob 1 ──── 0..1 Invoice

---

# 12. Payments

Records payments made by customers.

### Payments

- PaymentId (PK)
- InvoiceId (FK)
- PaymentDate
- Amount
- PaymentMethod
- ReferenceNumber
- Notes
- CreatedAt

### Relationship

Invoice 1 ──── * Payment

---

# 13. Customer Follow-ups CRM

Stores customer follow-up activities.

### CustomerFollowUps

- FollowUpId (PK)
- CustomerId (FK)
- RepairJobId (FK, Optional)
- FollowUpDate
- FollowUpType
- Subject
- Notes
- Status
- NextFollowUpDate
- CreatedAt

### Relationship

Customer 1 ──── * CustomerFollowUp

RepairJob 1 ──── * CustomerFollowUp

---

# 14. Service History

Service history is generated from completed repair jobs.

The system will display:

- Customer
- Vehicle
- Repair Job
- Services performed
- Parts used
- Labor cost
- Total cost
- Completion date
- Diagnosis
- Repair notes

Service History does not necessarily require a separate database table.

It can be generated from existing RepairJob, RepairJobServices, RepairJobParts, Invoice and Payment data.

---

# 15. Reports

Reports will be generated from the system data.

Examples:

- Daily Sales
- Monthly Sales
- Outstanding Payments
- Completed Repairs
- Active Repair Jobs
- Appointment Statistics
- Parts Inventory
- Low Stock Parts
- Supplier Purchases
- Customer Service History
- Most Frequently Used Services
- Revenue by Service
- Revenue by Vehicle
- Revenue by Customer

Reports are generated from existing database tables.

---

# Main System Relationships

Customer
    │
    ├── Vehicles
    │       │
    │       ├── Appointments
    │       │
    │       └── Repair Jobs
    │               │
    │               ├── Services
    │               │      through RepairJobServices
    │               │
    │               ├── Parts
    │               │      through RepairJobParts
    │               │
    │               └── Invoice
    │                       │
    │                       └── Payments
    │
    └── Customer Follow-ups


Supplier
    │
    └── Parts
          through SupplierParts


# Core Business Flow

Customer
    ↓
Vehicle
    ↓
Appointment
    ↓
Repair Job
    ↓
Diagnosis
    ↓
Services + Parts
    ↓
Invoice
    ↓
Payment
    ↓
Customer Follow-up
    ↓
Service History


# AutoFlow Architecture

Frontend / UI
    ↓
ASP.NET Core MVC
    ↓
Controllers
    ↓
Business Logic
    ↓
Entity Framework Core
    ↓
PostgreSQL
    ↓
Supabase


# Storage

Supabase Storage will be used for files such as:

- Vehicle Images
- Customer Documents
- Repair Documents

The database will store the corresponding file URL/path rather than the actual image binary.