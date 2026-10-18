# AE6 V2 HID protocol notes

The device was observed on Windows 11 while the official DAREU driver was displaying a live battery value.

## Device match

- USB VID: `0x260D`
- 2.4G receiver PID: `0x1261`
- HID usage page: `0xFF02`
- HID usage: `0x0002`
- Feature report length: `64` bytes

The `FF02/02` collection is the vendor-defined battery/control collection. The application opens it only for the battery status transaction described below.

## Known command contract

The official driver defines the power class and battery command as:

- `CLASS_POWER = 0x07`
- `PWR_CMD_BAT_STATUS = 0x00`
- `GET_CMD = 0x80`

The common report header is:

```text
index 0: report ID
index 1: target/status
index 2: payload length
index 3: command class
index 4: command ID and GET/SET flag
index 5: profile
index 6: protocol-specific check/status field
index 7+: payload
```

The battery response payload is:

```text
payload[0] = battery status
payload[1] = current level (0..100)
payload[2] = maximum level
payload[3] = critical level
```

So the current level is at absolute response index `8`.

The receiver's target/status byte for this AE6 V2 path is `0x02`. The implementation rejects reports with a different target, malformed reports, or out-of-range levels.

The read transaction sends a 64-byte zero-filled battery-status query with class `0x07`, command `0x80` (`0x00 | GET_CMD`), and then reads the 64-byte feature response. This is the only feature report write exposed by the application; it is a read-only battery query and cannot be used to change device settings.

## Resource and safety rules

- Enumerate the HID collection for each refresh so unplug/replug and sleep/resume recover without a persistent handle.
- Allocate exactly the capability-reported 64-byte feature buffer.
- Allow only the battery-status query followed by `HidD_GetFeature`.
- Do not expose settings writes, output reports, pairing, DPI, lighting, profile, or firmware operations.
- Poll every five minutes with one immediate read at startup.
- Do not retry in a tight loop.
