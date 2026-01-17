# RaTool - Domain to IP

A simple Windows desktop tool to bulk resolve domain names to IP addresses.

## Features

- Load domain list from text file (one domain per line)
- Parallel DNS resolution with configurable thread count (5, 10, 20, 50, 100)
- Real-time logging with timestamps
- Export results to text file (format: `domain|ip`)

## Usage

1. Click **Browse** to select your input file containing domain names
2. Click **Browse** to set the output file location
3. Select the number of threads
4. Click **START**

## Requirements

- .NET 9.0 or later
- Windows

## Build

```bash
dotnet build
dotnet run
```
