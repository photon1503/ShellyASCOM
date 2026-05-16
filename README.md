# ShellyASCOM

ASCOM Switch driver for Shelly devices.

## Features

- Supports multiple Shelly devices in one driver instance.
- Supports **Shelly Gen1** and **Shelly Gen2** switch APIs.
- Automatically detects API generation per device and stores it in ASCOM Profile.
- Controls switch state (read/write) for relay channel `0`.

## Requirements

- Windows with .NET Framework 4.7.2 runtime
- ASCOM Platform (version compatible with this project)

## Configuration

Open the ASCOM driver setup dialog and add one device per line:

`FriendlyName,IPAddress`

Example:

`Observatory Roof,192.168.1.40`

`Power Strip,192.168.1.41`

Notes:

- At least one device is required.
- Friendly name can be empty (IP will be used as display name).

## API Endpoints Used

### Gen2

- Read: `/rpc/Switch.GetStatus?id=0`
- Write: `/rpc/Switch.Set?id=0&on=true|false`

### Gen1

- Read: `/relay/0`
- Write: `/relay/0?turn=on|off`

### Generation Detection

- Preferred: `/shelly` (uses `gen` when available)
- Fallback probing of Gen2/Gen1 switch endpoints

## Build

Build with Visual Studio (solution/project targets .NET Framework 4.7.2).

## Repository

- Main branch: `master`
- Remote: `origin` (`https://github.com/photon1503/ShellyASCOM`)
