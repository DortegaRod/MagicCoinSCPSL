# MagicCoinSCPSL

A plugin for **SCP: Secret Laboratory** servers running the [EXILED](https://github.com/ExMod-Team/EXILED) framework.

## What it does

When a player drops a **Coin** at a specific location inside the **SCP-173 room**, the coin is consumed and replaced with a **random item** from a curated pool of allowed items. Every player also receives a coin at the start of each round so they can interact with the mechanic right away.

## Requirements

- SCP: Secret Laboratory (dedicated server)
- EXILED **8.9.11** or higher

## Installation

1. Build the project or download the latest release `.dll`.
2. Place the `.dll` inside your server's `EXILED/Plugins` folder.
3. Start or restart the server — the plugin will generate its config automatically.

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `is_enabled` | `true` | Enable or disable the plugin |
| `debug` | `false` | Enable debug logging |

## Author

**Megalón** — v1.0.0
