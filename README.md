# IP Location Broker 🛰️

This is a .NET 8 console app that acts as a proxy/broker to fetch the location (city, country) of a given IP address using third-party providers like `ipinfo.io` and `ip-api.com`.

---

## 🛠️ Features

- Uses multiple free IP geolocation providers.
- Intelligent routing based on:
  - Least error count (last 5 mins)
  - Lowest average response time
  - Provider rate limits (requests/minute)
- Thread-safe and concurrent-friendly design.
- Easily extendable to new providers.
- Console-based test runner with dummy IPs.

---

## 🚀 How It Works

1. You provide an IP.
2. The broker selects the **most reliable provider** at that moment.
3. The provider is called using `HttpClient`.
4. The response (location data) is printed.
5. Stats are logged internally to pick the best provider next time.

---

## 🔧 Setup & Run

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)

### Run the App

```bash
dotnet run
