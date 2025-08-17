# 📱 Barcode Scanner

A **.NET MAUI application** that allows scanning various barcode formats such as **GS1-128, Code128, QR Code, Code39, EAN, UPC**, and more using the **ZXing.Net.Maui** library.  

## 🚀 Features
- Scan multiple barcode formats:
  - GS1-128
  - Code128
  - Code39, Code93
  - EAN-8, EAN-13
  - UPC-A, UPC-E
  - QR Code, Aztec, PDF417, DataMatrix
- Display scanned results (GTIN, Quantity, Expiry Date, etc. for GS1).
- Cross-platform support (Android, iOS, Windows via .NET MAUI).

## 🛠️ Tech Stack
- **.NET MAUI** (.NET 8)
- **C#**
- **ZXing.Net.Maui** (Barcode scanning library)

## ⚡ Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (with **.NET MAUI workload**) or VSCode + MAUI extension
- Android/iOS emulator or real device

### Installation & Run
```bash
# Clone the repository
git clone https://github.com/TranQuocBao18/Barcode-Scanner.git
cd Barcode-Scanner

# Restore dependencies
dotnet restore

# Run on Android
dotnet build -t:Run -f net8.0-android

# Run on Windows
dotnet build -t:Run -f net8.0-windows10.0.19041.0