# ScanMate Pocket

ScanMate Pocket is a **.NET MAUI Android application** designed for warehouse and retail operations such as **inventory counting, barcode scanning, sales processing, and data export**.

The application is optimized for **Zebra TC21 handheld devices**, but also includes a **development fallback scanner** allowing the app to run on Android emulators or other Android devices during development.

---

# Project Overview

ScanMate Pocket is a mobile companion application used by warehouse or store staff to perform operational tasks quickly using a barcode scanner.

The system supports:

- Inventory counting
- Barcode scanning
- Sales recording
- Product lookup
- Data export (Excel)
- Local SQLite data storage
- Zebra hardware scanner integration

The application is designed for **offline-first usage**, where operations can be performed locally and later synchronized or exported.

---

# Key Features

### Barcode Scanning
- Zebra TC21 hardware scanner integration
- Development fallback scanner (keyboard input)
- Event-driven scanning architecture

### Inventory Operations
- Inventory counting
- Loot/box scanning
- Product quantity tracking
- Section-based counting

### Sales Module
- Sales recording
- Barcode-based product lookup
- Sales list management

### Data Export
- Excel export using **MiniExcel**
- Export progress UI
- Export filtering

### Local Database
- SQLite database
- Local storage for scanned products
- Fast offline operations

---

# Technology Stack

| Layer | Technology |
|------|-------------|
| Mobile Framework | .NET MAUI |
| Language | C# |
| Database | SQLite |
| Excel Export | MiniExcel |
| UI Framework | XAML |
| Barcode Scanner | Zebra DataWedge |
| Architecture | MVVM + Service Layer |

---

# Supported Devices

| Device | Support |
|------|------|
| Zebra TC21 | Full hardware scanner support |
| Android Emulator | Development fallback scanner |
| Generic Android devices | Keyboard scanning |

---

# Project Architecture

The project follows a modular architecture separating core logic, services, UI, and database access.


ZebraSCannerTest1
│
├── Core
│ ├── Enums
│ ├── Interfaces
│ └── Services
│
├── Database
│ ├── DbFactory
│ ├── Repositories
│ └── Entities
│
├── Features
│ ├── Inventory
│ ├── Scanning
│ └── ImportExport
│
├── UI
│ ├── Views
│ ├── ViewModels
│ └── Controls
│
└── Resources


---

# Scanning Architecture

The application uses a **scanner abstraction layer** to allow multiple scanning implementations.


IScanningService
│
├── ScanningService (Zebra hardware)
└── DevScanningService (development fallback)


### Zebra Scanner

On Zebra TC21 devices the application listens for **DataWedge broadcast intents**.

### Development Scanner

When running on emulator or non-Zebra devices the app allows barcode input through a text field.

---

# Installation

## Prerequisites

- Visual Studio 2022 or later
- .NET 9 SDK
- Android SDK
- Android Emulator (optional)
- Zebra TC21 device (recommended for production testing)

---

# Build Instructions

Clone repository:


git clone <repository-url>


Open solution in **Visual Studio**.

Build project:


Build → Rebuild Solution


Run on device:


Debug → Start Debugging


---

# Running on Zebra TC21

Requirements:

- Zebra TC21 device
- DataWedge enabled

Configure DataWedge profile:


Profile Name: ScanMate
Intent Output: Enabled
Intent Action: com.scanmate.SCANNER
Delivery: Broadcast


The application will automatically receive scanned barcodes through broadcast intents.

---

# Running on Emulator

When running on Android emulator the hardware scanner is unavailable.

Use **development scanning mode**:

1. Navigate to scanning page
2. Enter barcode manually
3. Press Enter

The application will simulate a scan event.

---

# Database

The application uses **SQLite** for local data storage.

Database responsibilities:

- Store scanned products
- Store inventory data
- Store sales records
- Store export logs

Database initialization occurs on application startup.

---

# Excel Export

Excel export is implemented using **MiniExcel**.

Export supports:

- Sales data
- Inventory results
- Filtered datasets

Export progress is displayed through a progress popup.

---

# Permissions

Android permissions required:


INTERNET
READ_EXTERNAL_STORAGE
WRITE_EXTERNAL_STORAGE


Zebra devices may also require DataWedge permissions.

---

# Development Guidelines

### Coding Style

- Follow C# naming conventions
- Use dependency injection for services
- Avoid business logic in UI layer
- Use repository pattern for database access

### Folder Rules


Core → shared logic
Features → domain functionality
Database → persistence layer
UI → presentation


---

# Future Improvements

Planned improvements include:

- Camera barcode scanning fallback
- Backend API synchronization
- Inventory reconciliation workflows
- Multi-warehouse support
- Cloud sync integration
- Role-based user access

---

# Related Systems

ScanMate Pocket is designed to integrate with:

- ERP systems
- Warehouse Management Systems
- Retail POS systems
- Backend APIs

---

# License

Internal project – not intended for public distribution.

---

# Authors

ScanMate development team.
