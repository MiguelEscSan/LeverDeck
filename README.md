# LeverDeck

Turn any phone or tablet into a Stream Deck–style control surface for your Windows PC.

- One executable on the PC (.NET), serving an installable PWA (Angular).
- Connects over the LAN (`leverdeck.local`) or USB (`adb reverse`).
- Pair once with a QR code; profiles live on the PC and sync to every device.
- Free and local: no accounts, no cloud, no app stores.

## Requirements

- Windows 10 or 11.
- .NET 10 SDK (to run the agent from source).
- Node 24 (to build the PWA in `web/`).

## HTTPS on the LAN (optional)

HTTP works without any setup. HTTPS lets phones pair over `https://leverdeck.local:8765` with a certificate the
phone trusts. Run once from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1
```

The script installs mkcert if it is missing, trusts its local root CA on this PC, and adds the private firewall rule
(run it from an elevated prompt for that step, or follow the `netsh` command it prints). Then:

1. Start the agent with HTTPS. The agent issues its certificate on the first start:

   ```powershell
   $env:LeverDeck__Https__Enabled = "true"; dotnet run --project agent/src/LeverDeck.App
   ```

2. On each phone, install the root CA from `%LOCALAPPDATA%\mkcert\rootCA.pem` as a CA certificate, then pair over HTTPS.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the architecture and project rules.
